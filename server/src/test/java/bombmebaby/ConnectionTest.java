package bombmebaby;

import static org.junit.jupiter.api.Assertions.*;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;
import java.io.BufferedReader;
import java.io.InputStreamReader;
import java.io.PrintWriter;
import java.net.InetAddress;
import java.net.ServerSocket;
import java.net.Socket;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.Future;
import java.util.concurrent.TimeUnit;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.Test;

/** Real loopback TCP, production Connection.run/handle/finally, no copied logic. */
class ConnectionTest {
    final BombServer server = new BombServer(SyncMode.OPTIMIZED);
    final List<Peer> peers = new ArrayList<>();

    @AfterEach void cleanup() throws Exception {
        for (Peer peer : peers) peer.socket.close();
        server.clients.shutdown();
        server.gameLoop.shutdownNow();
        assertTrue(server.clients.awaitTermination(5, TimeUnit.SECONDS));
    }

    @Test void helloRequiredBeforeRoomActions() throws Exception {
        Peer peer = connect();
        peer.send("type", "CREATE_ROOM", "seq", 1);
        assertEquals("HELLO_REQUIRED", peer.until("ERROR").get("code").getAsString());
        assertTrue(server.rooms.isEmpty());
    }

    @Test void unknownTokenFailsWithoutCreatingNewSession() throws Exception {
        Peer peer = connect();
        peer.send("type", "HELLO", "name", "A", "resumeToken", "missing-token");
        assertEquals("RESUME_FAILED", peer.until("ERROR").get("code").getAsString());
        assertTrue(server.sessions.isEmpty());
    }

    @Test void duplicateAndOlderRequestsDoNotChangeRoomOrMovement() throws Exception {
        Peer peer = hello();
        peer.request("type", "CREATE_ROOM", "seq", 1);
        peer.send("type", "CREATE_ROOM", "seq", 1);
        assertEquals("DUPLICATE_SEQUENCE", peer.until("ERROR").get("code").getAsString());
        peer.until("ACK");
        assertEquals(1, server.rooms.size());
        peer.request("type", "READY", "seq", 2, "ready", true);
        peer.request("type", "MOVE", "seq", 5, "directionX", 1);
        for (long seq : new long[] {5, 3}) {
            peer.send("type", "MOVE", "seq", seq, "directionX", -1);
            assertEquals("DUPLICATE_SEQUENCE", peer.until("ERROR").get("code").getAsString());
            peer.until("ACK");
            synchronized (peer.player.room) { assertEquals(1, peer.player.dx); }
        }
        peer.request("type", "MOVE", "seq", 6, "directionX", -1);
        synchronized (peer.player.room) { assertEquals(-1, peer.player.dx); }
    }

    @Test void pingDoesNotAdvanceActionSequence() throws Exception {
        Peer peer = hello();
        peer.send("type", "PING", "seq", 999, "clientTime", 123);
        assertEquals(123, peer.until("PONG").get("clientTime").getAsLong());
        peer.request("type", "CREATE_ROOM", "seq", 1);
        assertNotNull(peer.player.room);
        assertEquals(1, peer.player.lastSeq);
    }

    @Test void disconnectInLobbyRemovesPlayerAndEmptyRoom() throws Exception {
        Peer peer = hello();
        peer.request("type", "CREATE_ROOM", "seq", 1);
        peer.disconnect();
        assertFalse(peer.player.connected);
        assertNull(peer.player.connection);
        assertNull(peer.player.room);
        assertTrue(server.rooms.isEmpty());
    }

    @Test void resumeRestoresIdentitySequenceRoomAndFullMap() throws Exception {
        Peer old = playing();
        GameRoom room = old.player.room;
        old.request("type", "MOVE", "seq", 10, "directionX", 1);
        old.disconnect();
        assertFalse(old.player.connected);
        assertEquals(0, old.player.dx);
        assertSame(room, old.player.room);
        Peer resumed = connect();
        resumed.send("type", "HELLO", "resumeToken", old.player.resumeToken);
        JsonObject welcome = resumed.read();
        assertEquals("WELCOME", welcome.get("type").getAsString());
        assertEquals(old.player.id, welcome.get("playerId").getAsString());
        assertEquals(10, welcome.get("lastSeq").getAsLong());
        resumed.player = old.player;
        assertEquals(room.code, resumed.until("ROOM_STATE").getAsJsonObject("room").get("code").getAsString());
        var snapshot = resumed.until("STATE_UPDATE");
        assertFalse(snapshot.getAsJsonArray("obstacles").isEmpty());
        assertFalse(snapshot.getAsJsonArray("bushes").isEmpty());
        resumed.until("ACK");
        resumed.send("type", "MOVE", "seq", 10, "directionX", 1);
        assertEquals("DUPLICATE_SEQUENCE", resumed.until("ERROR").get("code").getAsString());
        resumed.until("ACK");
        resumed.request("type", "MOVE", "seq", 11, "directionX", -1);
        synchronized (room) { assertEquals(-1, old.player.dx); }
        // A late cleanup from the old connection must leave the new one intact.
        old.connection.disconnect();
        assertTrue(old.player.connected);
        assertSame(resumed.connection, old.player.connection);
        synchronized (room) { assertEquals(-1, old.player.dx); }
    }

    @Test void simultaneousResumeAllowsExactlyOneTcpConnection() throws Exception {
        Peer old = playing();
        old.disconnect();
        Peer a = connect(), b = connect();
        CountDownLatch start = new CountDownLatch(1);
        Future<?> first = server.clients.submit(() -> { start.await(); a.send("type", "HELLO", "resumeToken", old.player.resumeToken); return null; });
        Future<?> second = server.clients.submit(() -> { start.await(); b.send("type", "HELLO", "resumeToken", old.player.resumeToken); return null; });
        start.countDown();
        first.get(5, TimeUnit.SECONDS); second.get(5, TimeUnit.SECONDS);
        JsonObject ar = a.read(), br = b.read();
        boolean aWon = ar.get("type").getAsString().equals("WELCOME");
        boolean bWon = br.get("type").getAsString().equals("WELCOME");
        assertTrue(aWon ^ bWon);
        assertEquals("RESUME_FAILED", (aWon ? br : ar).get("code").getAsString());
        Peer winner = aWon ? a : b, loser = aWon ? b : a;
        winner.until("ACK");
        loser.disconnect();
        assertTrue(old.player.connected);
        assertSame(winner.connection, old.player.connection);
    }

    @Test void activeSessionCannotBeClaimedAgain() throws Exception {
        Peer active = hello(), other = connect();
        other.send("type", "HELLO", "resumeToken", active.player.resumeToken);
        assertEquals("RESUME_FAILED", other.until("ERROR").get("code").getAsString());
        assertSame(active.connection, active.player.connection);
    }

    @Test void resumeAtOrAfterDeadlineIsRejected() throws Exception {
        Peer old = playing();
        old.disconnect();
        synchronized (old.player) { old.player.disconnectedAt = System.currentTimeMillis() - GameRules.RECONNECT_MS; }
        Peer resumed = connect();
        resumed.send("type", "HELLO", "resumeToken", old.player.resumeToken);
        assertEquals("RESUME_FAILED", resumed.until("ERROR").get("code").getAsString());
        assertFalse(old.player.connected);
    }

    @Test void gameStartCarriesMapAndFollowingOptimizedSnapshotOmitsIt() throws Exception {
        Peer peer = hello();
        peer.request("type", "CREATE_ROOM", "seq", 1);
        peer.send("type", "READY", "seq", 2, "ready", true);
        JsonObject start = peer.until("GAME_START");
        assertFalse(start.getAsJsonArray("obstacles").isEmpty());
        assertFalse(start.getAsJsonArray("bushes").isEmpty());
        JsonObject snapshot = peer.until("STATE_UPDATE");
        assertFalse(snapshot.has("obstacles"));
        assertFalse(snapshot.has("bushes"));
        peer.until("ACK");
    }

    @Test void disconnectNotifiesOpponentThenTimeoutAwardsCorrectWinner() throws Exception {
        Peer a = hello(), b = hello();
        a.request("type", "CREATE_ROOM", "seq", 1);
        GameRoom room = a.player.room;
        b.request("type", "JOIN_ROOM", "seq", 1, "code", room.code);
        a.request("type", "READY", "seq", 2, "ready", true);
        b.request("type", "READY", "seq", 2, "ready", true);
        a.disconnect();
        assertEquals(a.player.id, b.until("PLAYER_DISCONNECTED").get("playerId").getAsString());
        long deadline = a.player.disconnectedAt + GameRules.RECONNECT_MS;
        room.update(deadline - 1, 0);
        synchronized (room) { assertTrue(a.player.alive); }
        room.update(deadline, 0);
        assertEquals("disconnect", b.until("PLAYER_ELIMINATED").get("reason").getAsString());
        assertEquals(b.player.id, b.until("GAME_OVER").get("winnerId").getAsString());
    }

    @Test void leaveRoomClearsMembershipAndAllowsCreatingAnotherRoom() throws Exception {
        Peer peer = hello();
        peer.request("type", "CREATE_ROOM", "seq", 1);
        GameRoom oldRoom = peer.player.room;
        peer.send("type", "LEAVE_ROOM", "seq", 2);
        peer.until("LEFT_ROOM"); peer.until("ACK");
        assertNull(peer.player.room);
        assertFalse(server.rooms.containsValue(oldRoom));
        peer.request("type", "CREATE_ROOM", "seq", 3);
        assertNotSame(oldRoom, peer.player.room);
        assertEquals(1, server.rooms.size());
    }

    private Peer hello() throws Exception {
        Peer peer = connect();
        peer.send("type", "HELLO", "name", "A");
        JsonObject welcome = peer.until("WELCOME");
        peer.player = server.sessions.get(welcome.get("resumeToken").getAsString());
        peer.until("ACK");
        return peer;
    }

    private Peer playing() throws Exception {
        Peer peer = hello();
        peer.request("type", "CREATE_ROOM", "seq", 1);
        peer.request("type", "READY", "seq", 2, "ready", true);
        return peer;
    }

    private Peer connect() throws Exception {
        try (ServerSocket listener = new ServerSocket(0, 1, InetAddress.getLoopbackAddress())) {
            Socket socket = new Socket(listener.getInetAddress(), listener.getLocalPort());
            socket.setSoTimeout(5000);
            var connection = server.new Connection(listener.accept());
            Peer peer = new Peer(socket, connection, server.clients.submit(connection));
            peers.add(peer);
            return peer;
        }
    }

    static final class Peer {
        final Socket socket;
        final BombServer.Connection connection;
        final Future<?> finished;
        final BufferedReader reader;
        final PrintWriter writer;
        PlayerState player;

        Peer(Socket socket, BombServer.Connection connection, Future<?> finished) throws Exception {
            this.socket = socket; this.connection = connection; this.finished = finished;
            reader = new BufferedReader(new InputStreamReader(socket.getInputStream(), StandardCharsets.UTF_8));
            writer = new PrintWriter(socket.getOutputStream(), true, StandardCharsets.UTF_8);
        }

        void send(Object... fields) { writer.println(BombServer.JSON.toJson(GameRoom.data(fields))); }
        void request(Object... fields) throws Exception { send(fields); until("ACK"); }

        JsonObject read() throws Exception {
            String line = reader.readLine();
            assertNotNull(line, "Unexpected EOF");
            return JsonParser.parseString(line).getAsJsonObject();
        }

        JsonObject until(String type) throws Exception {
            for (int i = 0; i < 100; i++) {
                JsonObject message = read();
                if (type.equals(message.get("type").getAsString())) return message;
                assertNotEquals("ERROR", message.get("type").getAsString(), message.toString());
            }
            throw new AssertionError("Missing " + type);
        }

        void disconnect() throws Exception { socket.close(); finished.get(5, TimeUnit.SECONDS); }
    }
}
