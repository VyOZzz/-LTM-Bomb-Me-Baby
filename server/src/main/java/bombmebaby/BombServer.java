package bombmebaby;

import com.google.gson.Gson;
import com.google.gson.JsonElement;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;
import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStreamReader;
import java.io.PrintWriter;
import java.net.ServerSocket;
import java.net.Socket;
import java.nio.charset.StandardCharsets;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.TimeUnit;

/** Newline-delimited JSON over persistent TCP. The server owns every gameplay value. */
public final class BombServer {
    static final Gson JSON = new Gson();
    final Map<String,GameRoom> rooms = new ConcurrentHashMap<>();
    final Map<String,PlayerState> sessions = new ConcurrentHashMap<>();
    final ExecutorService clients = Executors.newFixedThreadPool(64);
    final ScheduledExecutorService gameLoop = Executors.newSingleThreadScheduledExecutor();
    private long previousTick = System.nanoTime();

    public static void main(String[] args) throws IOException {
        int port = args.length > 0 ? Integer.parseInt(args[0]) : 7777;
        new BombServer().run(port);
    }

    void run(int port) throws IOException {
        gameLoop.scheduleAtFixedRate(() -> {
            long nowNs = System.nanoTime();
            double dt = Math.min(0.1, (nowNs - previousTick) / 1_000_000_000.0);
            previousTick = nowNs;
            long now = System.currentTimeMillis();
            for (GameRoom room : rooms.values()) {
                try { room.update(now, dt); }
                catch (RuntimeException e) { System.err.println("Room " + room.code + ": " + e); }
            }
            sessions.values().removeIf(p -> !p.connected && now - p.disconnectedAt > GameRules.RECONNECT_MS + 30_000);
        }, 50, 50, TimeUnit.MILLISECONDS);
        try (ServerSocket listener = new ServerSocket(port)) {
            System.out.println("Bomb Me Baby server on TCP port " + port);
            while (true) {
                Socket socket = listener.accept();
                socket.setTcpNoDelay(true);
                clients.execute(new Connection(socket));
            }
        }
    }

    final class Connection implements Runnable {
        final Socket socket;
        final PrintWriter writer;
        PlayerState player;

        Connection(Socket socket) throws IOException {
            this.socket = socket;
            this.writer = new PrintWriter(socket.getOutputStream(), true, StandardCharsets.UTF_8);
        }

        synchronized void send(Object value) {
            writer.println(JSON.toJson(value));
            if (writer.checkError()) close();
        }

        void close() { try { socket.close(); } catch (IOException ignored) { } }

        @Override public void run() {
            try (socket; BufferedReader input = new BufferedReader(new InputStreamReader(socket.getInputStream(), StandardCharsets.UTF_8))) {
                String line;
                while ((line = input.readLine()) != null) {
                    if (line.length() > 2048) { error("MESSAGE_TOO_LARGE"); break; }
                    long begin = System.nanoTime();
                    try {
                        JsonObject message = JsonParser.parseString(line).getAsJsonObject();
                        handle(message);
                        if (player != null && !"PING".equals(string(message,"type")))
                            send(GameRoom.data("type","ACK","seq",number(message,"seq",-1),
                                "processingMicros",(System.nanoTime()-begin)/1000));
                    } catch (Exception e) { error("INVALID_MESSAGE"); }
                }
            } catch (IOException ignored) {
            } finally {
                // Atomically check that we are still the active connection before
                // marking the player disconnected.  A concurrent reconnect may have
                // already swapped in a new Connection for the same PlayerState.
                if (player != null) {
                    synchronized (player) {
                        if (player.connection != this) return; // superseded by reconnect
                        player.connected = false;
                        player.connection = null;
                        player.disconnectedAt = System.currentTimeMillis();
                    }
                    GameRoom room = player.room;
                    if (room != null) {
                        synchronized (room) {
                            player.dx = 0; player.dz = 0;
                            if (room.phase != GameRoom.Phase.PLAYING) room.leave(player);
                            else room.players.forEach(p -> p.send(GameRoom.data("type","PLAYER_DISCONNECTED","playerId",player.id)));
                        }
                    }
                }
            }
        }

        private void handle(JsonObject m) {
            String type = string(m,"type");
            if (player == null) {
                if (!"HELLO".equals(type)) { error("HELLO_REQUIRED"); return; }
                String token = string(m,"resumeToken");
                PlayerState resumed = token.isEmpty() ? null : sessions.get(token);
                if (resumed != null) {
                    // Synchronize on the PlayerState so that only one Connection
                    // can claim a resume token at a time (fixes the race where
                    // two TCP connections try to resume the same session).
                    synchronized (resumed) {
                        if (!resumed.connected && System.currentTimeMillis() - resumed.disconnectedAt <= GameRules.RECONNECT_MS) {
                            player = resumed;
                            player.connection = this;
                            player.connected = true;
                            // Include lastSeq so the client knows which sequence
                            // number to continue from after a reconnect.
                            send(GameRoom.data("type","WELCOME","playerId",player.id,
                                "resumeToken",player.resumeToken,"lastSeq",player.lastSeq));
                        }
                    }
                    if (player != null) {
                        // Send room state and current snapshot outside the
                        // player lock (snapshot acquires the room lock).
                        if (player.room != null) {
                            send(GameRoom.data("type","ROOM_STATE","room",player.room.roomInfo()));
                            if (player.room.phase == GameRoom.Phase.PLAYING) {
                                // Always send a full snapshot (with obstacles/bushes)
                                // on reconnect regardless of sync mode so the client
                                // has a complete picture.
                                send(player.room.snapshot(System.currentTimeMillis(), player, true));
                            }
                        }
                        return;
                    }
                    // If we get here the token existed but the session was
                    // already connected or the reconnect window expired.
                    error("RESUME_FAILED");
                    return;
                }
                String name = string(m,"name").trim();
                if (name.isEmpty() || name.length() > 20) { error("INVALID_NAME"); return; }
                player = new PlayerState(name, this);
                sessions.put(player.resumeToken, player);
                send(GameRoom.data("type","WELCOME","playerId",player.id,"resumeToken",player.resumeToken));
                return;
            }
            long seq = number(m,"seq",-1);
            if (!"PING".equals(type)) {
                if (seq <= player.lastSeq) { error("DUPLICATE_SEQUENCE"); return; }
                player.lastSeq = seq;
            }
            GameRoom room = player.room;
            switch (type) {
                case "PING" -> send(GameRoom.data("type","PONG","clientTime",number(m,"clientTime",0),"serverTime",System.currentTimeMillis()));
                case "CREATE_ROOM" -> {
                    if (room != null) { error("ALREADY_IN_ROOM"); return; }
                    String code;
                    do { code = UUID.randomUUID().toString().substring(0,6).toUpperCase(); } while (rooms.containsKey(code));
                    GameRoom created = new GameRoom(code);
                    rooms.put(code, created);
                    created.join(player);
                }
                case "JOIN_ROOM" -> {
                    if (room != null) { error("ALREADY_IN_ROOM"); return; }
                    GameRoom target = rooms.get(string(m,"code").trim().toUpperCase());
                    if (target == null) { error("ROOM_NOT_FOUND"); return; }
                    if (!target.join(player)) error("ROOM_UNAVAILABLE");
                }
                case "LEAVE_ROOM" -> {
                    if (room != null) {
                        room.leave(player);
                        if (room.players.isEmpty()) rooms.remove(room.code, room);
                        send(GameRoom.data("type","LEFT_ROOM"));
                    }
                }
                case "READY" -> {
                    if (room == null) { error("NOT_IN_ROOM"); return; }
                    room.ready(player, bool(m,"ready"));
                }
                case "MOVE", "CHARGE_START", "EXPLODE", "USE_SHIELD", "USE_BOOST" -> {
                    if (room == null) { error("NOT_IN_ROOM"); return; }
                    double dx = decimal(m,"directionX",0), dz = decimal(m,"directionZ",0);
                    room.action(player,type,dx,dz,System.currentTimeMillis());
                }
                default -> error("UNKNOWN_ACTION");
            }
        }

        private void error(String code) { send(GameRoom.data("type","ERROR","code",code)); }
    }

    static String string(JsonObject object, String key) {
        JsonElement value = object.get(key);
        return value == null || value.isJsonNull() ? "" : value.getAsString();
    }
    static long number(JsonObject object, String key, long fallback) {
        JsonElement value = object.get(key);
        return value == null || value.isJsonNull() ? fallback : value.getAsLong();
    }
    static double decimal(JsonObject object, String key, double fallback) {
        JsonElement value = object.get(key);
        return value == null || value.isJsonNull() ? fallback : value.getAsDouble();
    }
    static boolean bool(JsonObject object, String key) {
        JsonElement value = object.get(key);
        return value != null && !value.isJsonNull() && value.getAsBoolean();
    }
}
