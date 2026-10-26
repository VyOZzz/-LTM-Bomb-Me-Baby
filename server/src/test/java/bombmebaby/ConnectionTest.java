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

    @Test void mismatchedOrMissingProtocolVersionIsRejected() throws Exception {
        Peer outdated = connect();
        outdated.send("type", "HELLO", "protocolVersion", BombServer.PROTOCOL_VERSION - 1, "name", "Old");
        JsonObject error = outdated.until("ERROR");
        assertEquals("VERSION_MISMATCH", error.get("code").getAsString());
        assertEquals(BombServer.PROTOCOL_VERSION, error.get("serverVersion").getAsInt());
        Peer unversioned = connect();
        unversioned.send("type", "HELLO", "name", "Legacy");
        assertEquals("VERSION_MISMATCH", unversioned.until("ERROR").get("code").getAsString());
        assertTrue(server.sessions.isEmpty());
    }

    @Test void unknownTokenFailsWithoutCreatingNewSession() throws Exception {
        Peer peer = connect();
        peer.send("type", "HELLO", "protocolVersion", BombServer.PROTOCOL_VERSION, "name", "A", "resumeToken", "missing-token");
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
        readyOpponent(peer);
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

    @Test void heartbeatWatchdogClosesASilentConnection() throws Exception {
        Peer peer = hello();
        peer.connection.lastReceivedAt = System.currentTimeMillis() - GameRules.CONNECTION_TIMEOUT_MS;
        peer.connection.closeIfUnhealthy(System.currentTimeMillis());
        peer.finished.get(5, TimeUnit.SECONDS);
        assertFalse(peer.player.connected);
        assertNull(peer.player.connection);
    }

    @Test void outboundQueueCoalescesSnapshotsButKeepsEvents() throws Exception {
        try (ServerSocket listener = new ServerSocket(0, 1, InetAddress.getLoopbackAddress());
             Socket client = new Socket(listener.getInetAddress(), listener.getLocalPort())) {
            var connection = server.new Connection(listener.accept());
            connection.send(GameRoom.data("type", "PLAYER_DAMAGED", "playerId", "A"));
            connection.send(GameRoom.data("type", "STATE_UPDATE", "tick", 1));
            connection.send(GameRoom.data("type", "STATE_UPDATE", "tick", 2));
            synchronized (connection.outboundLock) {
                assertEquals(2, connection.outbound.size());
                assertTrue(connection.outbound.getFirst().json().contains("PLAYER_DAMAGED"));
                assertTrue(connection.outbound.getLast().json().contains("\"tick\":2"));
            }
            connection.close();
        }
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
        old.player.charging = true;
        old.disconnect();
        assertFalse(old.player.connected);
        assertEquals(0, old.player.dx);
        assertFalse(old.player.charging);
        assertSame(room, old.player.room);
        Peer resumed = connect();
        resumed.send("type", "HELLO", "protocolVersion", BombServer.PROTOCOL_VERSION, "resumeToken", old.player.resumeToken);
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
        Future<?> first = server.clients.submit(() -> { start.await(); a.send("type", "HELLO", "protocolVersion", BombServer.PROTOCOL_VERSION, "resumeToken", old.player.resumeToken); return null; });
        Future<?> second = server.clients.submit(() -> { start.await(); b.send("type", "HELLO", "protocolVersion", BombServer.PROTOCOL_VERSION, "resumeToken", old.player.resumeToken); return null; });
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
        other.send("type", "HELLO", "protocolVersion", BombServer.PROTOCOL_VERSION, "resumeToken", active.player.resumeToken);
        assertEquals("RESUME_FAILED", other.until("ERROR").get("code").getAsString());
        assertSame(active.connection, active.player.connection);
    }

    @Test void resumeAtOrAfterDeadlineIsRejected() throws Exception {
        Peer old = playing();
        old.disconnect();
        synchronized (old.player) { old.player.disconnectedAt = System.currentTimeMillis() - GameRules.RECONNECT_MS; }
        Peer resumed = connect();
        resumed.send("type", "HELLO", "protocolVersion", BombServer.PROTOCOL_VERSION, "resumeToken", old.player.resumeToken);
        assertEquals("RESUME_FAILED", resumed.until("ERROR").get("code").getAsString());
        assertFalse(old.player.connected);
    }

    @Test void gameStartCarriesMapAndFollowingOptimizedSnapshotOmitsIt() throws Exception {
        Peer peer = hello();
        peer.request("type", "CREATE_ROOM", "seq", 1);
        readyOpponent(peer);
        peer.send("type", "READY", "seq", 2, "ready", true);
        JsonObject start = peer.until("GAME_START");
        assertFalse(start.getAsJsonArray("obstacles").isEmpty());
        assertFalse(start.getAsJsonArray("bushes").isEmpty());
        JsonObject snapshot = peer.until("STATE_UPDATE");
        assertFalse(snapshot.has("obstacles"));
        assertFalse(snapshot.has("bushes"));
        peer.until("ACK");
    }

    @Test void createJourneyRoomPublishesModeChapterAndScores() throws Exception {
        Peer peer = hello();
        peer.send("type", "CREATE_ROOM", "seq", 1, "mode", "journey");
        JsonObject room = peer.until("ROOM_STATE").getAsJsonObject("room");
        assertEquals("JOURNEY", room.get("mode").getAsString());
        assertEquals(0, room.get("chapterIndex").getAsInt());
        assertEquals(1, room.getAsJsonArray("scores").size());
        assertEquals("meadow", room.get("mapId").getAsString());
        assertEquals(GameRules.MIN_PLAYERS, room.get("minPlayers").getAsInt());
        peer.until("ACK");

        readyOpponent(peer);
        peer.send("type", "READY", "seq", 2, "ready", true);
        JsonObject start = peer.until("GAME_START");
        assertEquals("JOURNEY", start.get("mode").getAsString());
        assertEquals(0, start.get("chapterIndex").getAsInt());
        assertEquals("meadow", start.get("mapId").getAsString());
        peer.until("STATE_UPDATE");
        peer.until("ACK");
    }

    @Test void invalidRoomModeIsRejectedWithoutCreatingRoom() throws Exception {
        Peer peer = hello();
        peer.send("type", "CREATE_ROOM", "seq", 1, "mode", "unknown");
        assertEquals("INVALID_MODE", peer.until("ERROR").get("code").getAsString());
        peer.until("ACK");
        assertTrue(server.rooms.isEmpty());
    }

    @Test void joinWithModeOnlyEntersRoomsOfThatMode() throws Exception {
        Peer host = hello(), wrong = hello(), right = hello();
        host.request("type", "CREATE_ROOM", "seq", 1, "mode", "QUICK");
        String code = host.player.room.code;

        wrong.send("type", "JOIN_ROOM", "seq", 1, "code", code, "mode", "JOURNEY");
        JsonObject error = wrong.until("ERROR");
        assertEquals("MODE_MISMATCH", error.get("code").getAsString());
        assertEquals("QUICK", error.get("mode").getAsString());
        assertNull(wrong.player.room);

        right.request("type", "JOIN_ROOM", "seq", 1, "code", code.toLowerCase(), "mode", "quick");
        assertSame(host.player.room, right.player.room);
    }

    @Test void journeyCompleteEventIsBroadcastAfterThirdChapter() throws Exception {
        Peer a = hello(), b = hello();
        a.request("type", "CREATE_ROOM", "seq", 1, "mode", "JOURNEY");
        GameRoom room = a.player.room;
        b.request("type", "JOIN_ROOM", "seq", 1, "code", room.code);

        a.request("type", "READY", "seq", 2, "ready", true);
        b.request("type", "READY", "seq", 2, "ready", true);
        synchronized (room) {
            a.player.x = 0; a.player.z = 0;
            b.player.x = .5; b.player.z = 0; b.player.hp = 1;
        }
        a.request("type", "CHARGE_START", "seq", 3);
        a.request("type", "EXPLODE", "seq", 4);
        JsonObject gameOver = b.until("GAME_OVER");
        assertEquals(0, gameOver.get("chapterIndex").getAsInt());
        assertEquals(a.player.id, gameOver.get("winnerId").getAsString());

        finishJourneyChapter(room, a.player, b.player, 200_000);
        assertEquals(1, b.until("GAME_OVER").get("chapterIndex").getAsInt());
        finishJourneyChapter(room, a.player, b.player, 300_000);
        assertEquals(2, b.until("GAME_OVER").get("chapterIndex").getAsInt());
        JsonObject complete = b.until("JOURNEY_COMPLETE");
        assertEquals(2, complete.get("chapterIndex").getAsInt());
        assertEquals(a.player.id, complete.get("winnerId").getAsString());
        assertEquals(a.player.id, complete.getAsJsonArray("winnerIds").get(0).getAsString());
        assertEquals(3, complete.getAsJsonArray("scores").get(0).getAsJsonObject().get("roundWins").getAsInt());
    }

    @Test void optimizedExplosionEventHidesBushPositionFromOtherViewer() throws Exception {
        Peer hidden = hello(), viewer = hello();
        hidden.request("type", "CREATE_ROOM", "seq", 1);
        GameRoom room = hidden.player.room;
        viewer.request("type", "JOIN_ROOM", "seq", 1, "code", room.code);
        hidden.request("type", "READY", "seq", 2, "ready", true);
        viewer.request("type", "READY", "seq", 2, "ready", true);
        synchronized (room) {
            GameMap.Bush bush = room.bushes.get(0);
            hidden.player.x = bush.x(); hidden.player.z = bush.z();
            viewer.player.x = bush.x() + 8; viewer.player.z = bush.z();
            assertFalse(room.canSee(viewer.player, hidden.player));
        }
        hidden.request("type", "CHARGE_START", "seq", 3);
        hidden.request("type", "EXPLODE", "seq", 4);
        JsonObject event = viewer.until("PLAYER_EXPLODE");
        assertFalse(event.get("visible").getAsBoolean());
        assertEquals(0, event.get("x").getAsDouble());
        assertEquals(0, event.get("z").getAsDouble());
    }

    @Test void disconnectNotifiesOpponentThenTimeoutAwardsCorrectWinner() throws Exception {
        Peer a = hello(), b = hello();
        a.request("type", "CREATE_ROOM", "seq", 1);
        GameRoom room = a.player.room;
        b.request("type", "JOIN_ROOM", "seq", 1, "code", room.code);
        a.request("type", "READY", "seq", 2, "ready", true);
        b.request("type", "READY", "seq", 2, "ready", true);
        a.disconnect();
        JsonObject disconnected = b.until("PLAYER_DISCONNECTED");
        assertEquals(a.player.id, disconnected.get("playerId").getAsString());
        assertEquals(GameRules.RECONNECT_MS, disconnected.get("reconnectMs").getAsLong());
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

    @Test void twoSearchingPlayersWaitForCountdownThenStartWithoutReady() throws Exception {
        Peer a = searching("QUICK");
        Peer b = searching("QUICK");
        JsonObject queued = a.until("MATCHMAKING");
        assertEquals(2, queued.get("waiting").getAsInt());
        assertEquals(GameRules.MATCH_MAX_PLAYERS, queued.get("maxPlayers").getAsInt());
        long startsIn = queued.get("startsInMs").getAsLong();
        assertTrue(startsIn > 0 && startsIn <= GameRules.MATCH_WAIT_MS);
        server.matchmake(System.currentTimeMillis());
        assertTrue(server.rooms.isEmpty(), "A group smaller than a full lobby waits for the countdown");
        server.matchmake(afterWait());
        for (Peer peer : List.of(a, b)) {
            JsonObject found = peer.until("MATCH_FOUND");
            assertEquals("QUICK", found.get("mode").getAsString());
            assertEquals("QUICK", peer.until("GAME_START").get("mode").getAsString());
        }
        assertSame(a.player.room, b.player.room);
        synchronized (a.player.room) { assertEquals(GameRoom.Phase.PLAYING, a.player.room.phase); }
        assertNull(server.matchmaker.modeOf(a.player));
    }

    @Test void fullQueueMatchesAtOnceAndModesNeverMix() throws Exception {
        Peer journey = searching("JOURNEY");
        List<Peer> quick = new ArrayList<>();
        for (int i = 0; i < GameRules.MATCH_MAX_PLAYERS; i++) quick.add(searching("QUICK"));
        server.matchmake(System.currentTimeMillis());
        assertEquals(1, server.rooms.size());
        GameRoom room = quick.get(0).player.room;
        assertEquals(GameMode.QUICK, room.mode);
        for (Peer peer : quick) assertSame(room, peer.player.room);
        assertNull(journey.player.room);
        assertEquals(GameMode.JOURNEY, server.matchmaker.modeOf(journey.player));
    }

    @Test void countdownStartsWhenTheSecondPlayerJoinsAndExtraPlayersKeepWaiting() throws Exception {
        Peer first = searching("QUICK");
        assertEquals(-1, server.matchmaker.startsInMs(GameMode.QUICK, System.currentTimeMillis()), "Alone: no countdown");
        long joinedAt = System.currentTimeMillis();
        List<Peer> quick = new ArrayList<>(List.of(first));
        for (int i = 1; i <= GameRules.MATCH_MAX_PLAYERS; i++) quick.add(searching("QUICK"));
        server.matchmake(joinedAt + GameRules.MATCH_WAIT_MS - 1_000);
        assertEquals(1, server.rooms.size(), "Four players start at once without waiting");
        Peer extra = quick.get(GameRules.MATCH_MAX_PLAYERS);
        assertNull(extra.player.room);
        assertEquals(GameMode.QUICK, server.matchmaker.modeOf(extra.player));
        server.matchmake(afterWait());
        assertNull(extra.player.room, "One player never starts a match");
    }

    @Test void searchingBlocksRoomsAndCancelOrDisconnectLeavesQueue() throws Exception {
        Peer a = searching("QUICK");
        Peer b = searching("QUICK");
        a.send("type", "CREATE_ROOM", "seq", 2);
        assertEquals("ALREADY_SEARCHING", a.until("ERROR").get("code").getAsString());
        a.until("ACK");
        a.send("type", "CANCEL_MATCH", "seq", 3);
        assertEquals("CANCELLED", a.until("MATCHMAKING").get("status").getAsString());
        a.until("ACK");
        assertEquals(1, b.until("MATCHMAKING").get("waiting").getAsInt());
        b.disconnect();
        assertNull(server.matchmaker.modeOf(b.player));
        long now = System.currentTimeMillis();
        server.matchmake(now);
        assertTrue(server.rooms.isEmpty());
    }

    @Test void lobbyResumeExplicitlyClearsRoomAndLeaveIsIdempotent() throws Exception {
        Peer old = hello();
        old.request("type", "CREATE_ROOM", "seq", 1);
        old.disconnect();
        Peer resumed = connect();
        resumed.send("type", "HELLO", "protocolVersion", BombServer.PROTOCOL_VERSION, "resumeToken", old.player.resumeToken);
        assertFalse(resumed.until("WELCOME").get("hasRoom").getAsBoolean());
        resumed.until("LEFT_ROOM"); resumed.until("ACK");
        resumed.send("type", "LEAVE_ROOM", "seq", 2);
        resumed.until("LEFT_ROOM"); resumed.until("ACK");
        resumed.request("type", "CREATE_ROOM", "seq", 3);
        assertNotNull(old.player.room);
    }

    @Test void finishedQuickRoundResumesWithWinnerAndMap() throws Exception {
        Peer old = playing();
        GameRoom room = old.player.room;
        old.disconnect();
        synchronized (room) {
            room.players.stream().filter(p -> p != old.player).forEach(p -> p.alive = false);
            room.update(System.currentTimeMillis(), 0);
        }
        Peer resumed = connect();
        resumed.send("type", "HELLO", "protocolVersion", BombServer.PROTOCOL_VERSION, "resumeToken", old.player.resumeToken);
        assertTrue(resumed.until("WELCOME").get("hasRoom").getAsBoolean());
        JsonObject info = resumed.until("ROOM_STATE").getAsJsonObject("room");
        assertEquals(old.player.id, info.get("roundWinnerId").getAsString());
        assertEquals("FINISHED", resumed.until("STATE_UPDATE").get("phase").getAsString());
        assertEquals(old.player.id, resumed.until("GAME_OVER").get("winnerId").getAsString());
        resumed.until("ACK");
        resumed.player = old.player;
        resumed.disconnect();
        assertSame(room, old.player.room, "Results remain resumable until the deadline");
    }

    @Test void journeyResultResumesBetweenChapters() throws Exception {
        Peer a = hello();
        a.request("type", "CREATE_ROOM", "seq", 1, "mode", "JOURNEY");
        Peer b = readyOpponent(a);
        a.request("type", "READY", "seq", 2, "ready", true);
        GameRoom room = a.player.room;
        a.disconnect();
        synchronized (room) {
            b.player.alive = false;
            room.update(System.currentTimeMillis(), 0);
        }
        Peer resumed = connect();
        resumed.send("type", "HELLO", "protocolVersion", BombServer.PROTOCOL_VERSION, "resumeToken", a.player.resumeToken);
        resumed.until("WELCOME");
        assertEquals(0, resumed.until("ROOM_STATE").getAsJsonObject("room").get("chapterIndex").getAsInt());
        assertEquals(a.player.id, resumed.until("GAME_OVER").get("winnerId").getAsString());
        assertFalse(room.journeyComplete);
        resumed.until("ACK");
        resumed.player = a.player; a = resumed;
        a.disconnect();
        assertSame(room, a.player.room, "Between-chapter results also survive disconnect");
    }

    @Test void oversizedUnterminatedFrameClosesWithoutWaitingForNewline() throws Exception {
        Peer peer = hello();
        peer.writer.print("x".repeat(2049)); peer.writer.flush();
        peer.finished.get(2, TimeUnit.SECONDS);
        assertTrue(peer.connection.closed);
        assertNotNull(hello().player, "Other connections still work");
    }

    @Test void malformedJsonDoesNotPoisonFollowingFrame() throws Exception {
        Peer peer = connect();
        peer.writer.println("{broken");
        assertEquals("INVALID_MESSAGE", peer.until("ERROR").get("code").getAsString());
        peer.send("type", "HELLO", "protocolVersion", BombServer.PROTOCOL_VERSION, "name", "Recovered");
        peer.until("WELCOME"); peer.until("ACK");
    }

    @Test void outboundSnapshotsRespectEventsAndRecoveryGeometry() throws Exception {
        try (ServerSocket listener = new ServerSocket(0, 1, InetAddress.getLoopbackAddress());
             Socket client = new Socket(listener.getInetAddress(), listener.getLocalPort())) {
            var connection = server.new Connection(listener.accept());
            try {
                connection.send(GameRoom.data("type", "STATE_UPDATE", "tick", 1, "obstacles", List.of(1)));
                connection.send(GameRoom.data("type", "STATE_UPDATE", "tick", 2));
                connection.send(GameRoom.data("type", "GAME_OVER"));
                connection.send(GameRoom.data("type", "STATE_UPDATE", "tick", 3));
                connection.send(GameRoom.data("type", "STATE_UPDATE", "tick", 4));
                assertEquals(4, connection.outbound.size());
                assertTrue(connection.outbound.getFirst().geometry());
                var ordered = connection.outbound.stream().map(BombServer.Connection.Outbound::json).toList();
                assertTrue(ordered.get(1).contains("\"tick\":2"));
                assertTrue(ordered.get(2).contains("GAME_OVER"));
                assertTrue(ordered.get(3).contains("\"tick\":4"));
            } finally { connection.close(); }
        }
    }

    @Test void watchdogDoesNotMisreadAWriteFinishingBetweenVolatileReads() throws Exception {
        try (ServerSocket listener = new ServerSocket(0, 1, InetAddress.getLoopbackAddress());
             Socket client = new Socket(listener.getInetAddress(), listener.getLocalPort())) {
            var connection = server.new Connection(listener.accept());
            var stop = new java.util.concurrent.atomic.AtomicBoolean();
            Thread writer = new Thread(() -> {
                while (!stop.get()) {
                    connection.writeStartedAt = System.currentTimeMillis();
                    connection.writeStartedAt = 0;
                }
            });
            writer.start();
            try {
                for (int i = 0; i < 1_000_000 && !connection.closed; i++) {
                    long now = System.currentTimeMillis();
                    connection.lastReceivedAt = now;
                    connection.closeIfUnhealthy(now);
                }
                assertFalse(connection.closed, "Finishing a healthy write must not look like an epoch-old stall");
            } finally { stop.set(true); writer.join(2000); connection.close(); }
        }
    }

    @Test void earlyJourneyCompletionIsReplayedOnResume() throws Exception {
        Peer host = hello();
        host.request("type", "CREATE_ROOM", "seq", 1, "mode", "JOURNEY");
        Peer other = readyOpponent(host);
        host.request("type", "READY", "seq", 2, "ready", true);
        other.request("type", "LEAVE_ROOM", "seq", 3);
        assertEquals("INSUFFICIENT_PLAYERS", host.until("JOURNEY_COMPLETE").get("reason").getAsString());
        host.disconnect();
        Peer resumed = connect();
        resumed.send("type", "HELLO", "protocolVersion", BombServer.PROTOCOL_VERSION, "resumeToken", host.player.resumeToken);
        resumed.until("WELCOME");
        JsonObject info = resumed.until("ROOM_STATE").getAsJsonObject("room");
        assertTrue(info.get("journeyComplete").getAsBoolean());
        assertEquals("INSUFFICIENT_PLAYERS", info.get("journeyEndReason").getAsString());
        resumed.until("STATE_UPDATE");
        resumed.until("GAME_OVER");
        assertEquals(host.player.id, resumed.until("JOURNEY_COMPLETE").getAsJsonArray("winnerIds").get(0).getAsString());
        resumed.until("ACK");
    }

    @Test void selectedMatchCanBeCancelledAndCannotOverrideNewMode() throws Exception {
        Peer a = searching("QUICK"), b = searching("QUICK"), c = searching("QUICK");
        var selected = server.matchmaker.takeMatches(afterWait()).get(0);
        a.send("type", "FIND_MATCH", "seq", 2, "mode", "JOURNEY");
        assertEquals("ALREADY_SEARCHING", a.until("ERROR").get("code").getAsString());
        a.until("ACK");
        a.send("type", "CANCEL_MATCH", "seq", 3);
        assertEquals("CANCELLED", a.until("MATCHMAKING").get("status").getAsString());
        a.until("ACK");
        a.request("type", "FIND_MATCH", "seq", 4, "mode", "JOURNEY");
        server.formMatch(selected, System.currentTimeMillis());
        assertNull(a.player.room);
        assertEquals(GameMode.JOURNEY, server.matchmaker.modeOf(a.player));
        assertSame(b.player.room, c.player.room);
        assertEquals(GameRoom.Phase.PLAYING, b.player.room.phase);
        assertEquals(GameMode.QUICK, b.player.room.mode);
        Peer d = searching("JOURNEY");
        server.matchmake(afterWait());
        assertSame(a.player.room, d.player.room);
        assertEquals(GameMode.JOURNEY, a.player.room.mode);
    }

    @Test void cancelledReplacementDoesNotGetSeatedOrRequeuedByOldMatch() throws Exception {
        Peer a = searching("QUICK"), b = searching("QUICK");
        var selected = server.matchmaker.takeMatches(afterWait()).get(0);
        a.request("type", "CANCEL_MATCH", "seq", 2);
        a.request("type", "FIND_MATCH", "seq", 3, "mode", "JOURNEY");
        a.request("type", "CANCEL_MATCH", "seq", 4);
        server.formMatch(selected, 1000);
        assertNull(a.player.room);
        assertNull(server.matchmaker.modeOf(a.player));
        assertNull(b.player.room);
        assertEquals(GameMode.QUICK, server.matchmaker.modeOf(b.player));
        assertTrue(server.rooms.isEmpty(), "Incomplete group must not publish a temporary room");
        Peer c = searching("QUICK");
        server.matchmake(afterWait());
        assertSame(b.player.room, c.player.room);
        assertNull(a.player.room);
    }

    @Test void sameModeReplacementHasANewTicketAndFallbackKeepsFifo() throws Exception {
        Peer a = searching("QUICK"), b = searching("QUICK");
        var selected = server.matchmaker.takeMatches(afterWait()).get(0);
        a.request("type", "CANCEL_MATCH", "seq", 2);
        a.request("type", "FIND_MATCH", "seq", 3, "mode", "QUICK");
        server.formMatch(selected, 1000);
        assertTrue(server.rooms.isEmpty());
        var replacement = server.matchmaker.takeMatches(afterWait()).get(0);
        assertEquals(List.of(b.player, a.player), replacement.players());
        server.formMatch(replacement, System.currentTimeMillis());
        assertSame(a.player.room, b.player.room);
        assertNull(server.matchmaker.modeOf(a.player));
    }

    @Test void disconnectDuringSelectionDoesNotRequeueExpiredTicket() throws Exception {
        Peer a = searching("QUICK"), b = searching("QUICK");
        var selected = server.matchmaker.takeMatches(afterWait()).get(0);
        a.disconnect();
        server.formMatch(selected, 1000);
        assertNull(server.matchmaker.modeOf(a.player));
        assertEquals(GameMode.QUICK, server.matchmaker.modeOf(b.player));
        assertTrue(server.rooms.isEmpty());
    }

    @Test void concurrentCancelAndCommitHaveOneOutcomeWithoutDeadlock() throws Exception {
        for (int attempt = 0; attempt < 12; attempt++) {
            Peer a = searching("QUICK"), b = searching("QUICK");
            var selected = server.matchmaker.takeMatches(afterWait()).get(0);
            var forming = server.gameLoop.submit(() -> server.formMatch(selected, System.currentTimeMillis()));
            a.send("type", "CANCEL_MATCH", "seq", 2);
            forming.get(5, TimeUnit.SECONDS);
            boolean cancelled = false, found = false;
            for (;;) {
                JsonObject message = a.read();
                String type = message.get("type").getAsString();
                if (type.equals("MATCHMAKING") && message.get("status").getAsString().equals("CANCELLED")) cancelled = true;
                if (type.equals("MATCH_FOUND")) found = true;
                if (type.equals("ERROR")) assertEquals("NOT_SEARCHING", message.get("code").getAsString());
                if (type.equals("ACK")) break;
            }
            assertNotEquals(cancelled, found, "Either cancellation wins or room commit wins, never both");
            assertEquals(found, a.player.room != null);
            a.request("type", "LEAVE_ROOM", "seq", 3);
            b.request("type", "LEAVE_ROOM", "seq", 2);
            a.disconnect(); b.disconnect();
            assertTrue(server.rooms.isEmpty());
        }
    }

    @Test void quickNewcomerWaitsForNextRoundAndResumeDoesNotReplaySomeoneElsesResult() throws Exception {
        Peer host = playing();
        GameRoom room = host.player.room;
        synchronized (room) {
            PlayerState opponent = room.players.stream().filter(p -> p != host.player).findFirst().orElseThrow();
            opponent.alive = false;
            room.update(System.currentTimeMillis(), 0);
        }
        Peer newcomer = hello();
        newcomer.send("type", "JOIN_ROOM", "seq", 1, "code", room.code);
        JsonObject joined = newcomer.until("ROOM_STATE").getAsJsonObject("room");
        assertEquals("FINISHED", joined.get("phase").getAsString());
        assertTrue(room.participatedInRound(host.player));
        assertFalse(room.participatedInRound(newcomer.player));
        assertFalse(joined.getAsJsonArray("roundParticipantIds").contains(BombServer.JSON.toJsonTree(newcomer.player.id)));
        newcomer.until("ACK");
        newcomer.disconnect();
        Peer resumed = connect();
        resumed.send("type", "HELLO", "protocolVersion", BombServer.PROTOCOL_VERSION, "resumeToken", newcomer.player.resumeToken);
        resumed.until("WELCOME");
        boolean sawRoom = false, sawSnapshot = false;
        for (;;) {
            JsonObject message = resumed.read();
            String type = message.get("type").getAsString();
            assertNotEquals("GAME_OVER", type, "Newcomer never played this round");
            assertNotEquals("ERROR", type);
            if (type.equals("ROOM_STATE")) sawRoom = true;
            if (type.equals("STATE_UPDATE")) sawSnapshot = true;
            if (type.equals("ACK")) break;
        }
        assertTrue(sawRoom && sawSnapshot);
        var fixture = new ArrayList<String>();
        fixture.add(BombServer.JSON.toJson(GameRoom.data("type", "WELCOME", "playerId", newcomer.player.id, "hasRoom", false)));
        fixture.add(BombServer.JSON.toJson(GameRoom.data("type", "ROOM_STATE", "room", joined)));
        fixture.add(BombServer.JSON.toJson(room.snapshot(System.currentTimeMillis(), newcomer.player, true)));
        synchronized (room) {
            for (PlayerState player : List.copyOf(room.players)) room.ready(player, true);
            assertEquals(GameRoom.Phase.PLAYING, room.phase);
            assertTrue(room.participatedInRound(newcomer.player));
        }
        fixture.add(resumed.until("GAME_START").toString());
        fixture.add(resumed.until("STATE_UPDATE").toString());
        java.nio.file.Files.write(java.nio.file.Path.of("target/newcomer-fixtures.jsonl"), fixture);
    }

    private Peer hello() throws Exception {
        Peer peer = connect();
        peer.send("type", "HELLO", "protocolVersion", BombServer.PROTOCOL_VERSION, "name", "A");
        JsonObject welcome = peer.until("WELCOME");
        assertEquals("OPTIMIZED", welcome.get("syncMode").getAsString());
        assertTrue(welcome.has("demoMap"));
        peer.player = server.sessions.get(welcome.get("resumeToken").getAsString());
        peer.until("ACK");
        return peer;
    }

    private Peer playing() throws Exception {
        Peer peer = hello();
        peer.request("type", "CREATE_ROOM", "seq", 1);
        readyOpponent(peer);
        peer.request("type", "READY", "seq", 2, "ready", true);
        return peer;
    }

    private static void finishJourneyChapter(GameRoom room, PlayerState winner, PlayerState loser, long now) {
        synchronized (room) {
            room.ready(winner, true);
            room.ready(loser, true);
            loser.alive = false;
            room.update(now, 0);
        }
    }

    /** Rooms need two players to start: a second, already ready player for the host's room. */
    private Peer readyOpponent(Peer host) throws Exception {
        Peer opponent = hello();
        opponent.request("type", "JOIN_ROOM", "seq", 1, "code", host.player.room.code);
        opponent.request("type", "READY", "seq", 2, "ready", true);
        return opponent;
    }

    /** A game-loop time at which every ticket queued so far has finished its countdown. */
    private static long afterWait() { return System.currentTimeMillis() + GameRules.MATCH_WAIT_MS; }

    private Peer searching(String mode) throws Exception {
        Peer peer = hello();
        peer.send("type", "FIND_MATCH", "seq", 1, "mode", mode);
        peer.until("MATCHMAKING");
        peer.until("ACK");
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
