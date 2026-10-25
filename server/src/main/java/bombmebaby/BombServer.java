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
import java.util.ArrayDeque;
import java.util.List;
import java.util.Map;
import java.util.Set;
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
    final Set<Connection> connections = ConcurrentHashMap.newKeySet();
    final Matchmaker matchmaker = new Matchmaker();
    final ExecutorService clients = Executors.newFixedThreadPool(64);
    final ScheduledExecutorService gameLoop = Executors.newSingleThreadScheduledExecutor();
    final SyncMode syncMode;
    private long previousTick = System.nanoTime();

    BombServer() { this(SyncMode.active()); }

    BombServer(SyncMode syncMode) { this.syncMode = syncMode; }

    public static void main(String[] args) throws IOException {
        int port = args.length > 0 ? Integer.parseInt(args[0]) : 7777;
        String demoMap = GameMap.configuredDemoMapId();
        if (!demoMap.isEmpty()) System.out.println("[Demo] QUICK rooms locked to map = " + demoMap);
        new BombServer().run(port);
    }

    void run(int port) throws IOException {
        gameLoop.scheduleAtFixedRate(() -> {
            long nowNs = System.nanoTime();
            double dt = Math.min(0.1, (nowNs - previousTick) / 1_000_000_000.0);
            previousTick = nowNs;
            long now = System.currentTimeMillis();
            connections.forEach(connection -> connection.closeIfUnhealthy(now));
            try { matchmake(now); }
            catch (RuntimeException e) { System.err.println("Matchmaking: " + e); }
            for (GameRoom room : rooms.values()) {
                try { room.update(now, dt); }
                catch (RuntimeException e) { System.err.println("Room " + room.code + ": " + e); }
                removeIfEmpty(room);
            }
            sessions.values().removeIf(p -> {
                synchronized (p) {
                    return !p.connected && now - p.disconnectedAt > GameRules.RECONNECT_MS + 30_000;
                }
            });
        }, 50, 50, TimeUnit.MILLISECONDS);
        try (ServerSocket listener = new ServerSocket(port)) {
            System.out.println("Bomb Me Baby server on TCP port " + port);
            while (true) {
                Socket socket = listener.accept();
                if (connections.size() >= 64) { socket.close(); continue; }
                socket.setTcpNoDelay(true);
                clients.execute(new Connection(socket));
            }
        }
    }

    GameRoom createRoom(GameMode mode) {
        return createRoom(mode, null);
    }

    GameRoom createRoom(GameMode mode, PlayerState host) {
        while (true) {
            String code = UUID.randomUUID().toString().substring(0,6).toUpperCase();
            GameRoom created = new GameRoom(code, syncMode, mode);
            synchronized (created) {
                if (rooms.putIfAbsent(created.code, created) != null) continue;
                if (host != null) created.join(host);
                return created;
            }
        }
    }

    void removeIfEmpty(GameRoom room) {
        synchronized (room) {
            if (room.isEmpty()) rooms.remove(room.code, room);
        }
    }

    /** Game-loop thread: turns queued players into rooms that start right away. */
    void matchmake(long now) {
        for (Matchmaker.Match match : matchmaker.takeMatches(now)) formMatch(match, now);
    }

    void formMatch(Matchmaker.Match match, long now) {
        List<PlayerState> ordered = match.players().stream()
            .sorted(java.util.Comparator.comparing(p -> p.id)).toList();
        // Lock the whole group (at most four), always by id, before deciding whether
        // it can start. Cancellation/disconnect cannot race seating or fallback.
        withPlayers(ordered, 0, () -> {
            List<PlayerState> eligible = match.tickets().stream()
                .filter(matchmaker::isCurrent).map(Matchmaker.Ticket::player)
                .filter(p -> p.room == null && p.connected).toList();
            if (eligible.size() < GameRules.MIN_PLAYERS) {
                announceQueue(match.mode(), matchmaker.requeue(match));
                return;
            }
            match.tickets().forEach(matchmaker::complete);
            GameRoom room = createRoom(match.mode());
            synchronized (room) {
                for (PlayerState p : eligible) {
                    p.send(GameRoom.data("type","MATCH_FOUND","room",room.code,"mode",match.mode().name()));
                    room.join(p);
                }
                room.startMatched(now);
            }
        });
    }

    private static void withPlayers(List<PlayerState> players, int index, Runnable action) {
        if (index == players.size()) { action.run(); return; }
        synchronized (players.get(index)) { withPlayers(players, index + 1, action); }
    }

    static void announceQueue(GameMode mode, List<PlayerState> waiting) {
        Object status = GameRoom.data("type","MATCHMAKING","status","SEARCHING","mode",mode.name(),
            "waiting",waiting.size(),"minPlayers",GameRules.MIN_PLAYERS,"maxPlayers",GameRules.MATCH_MAX_PLAYERS);
        for (PlayerState p : waiting) p.send(status);
    }

    final class Connection implements Runnable {
        record Outbound(String json, boolean snapshot, boolean geometry) {}

        static final int MAX_OUTBOUND = 128;
        final Socket socket;
        final PrintWriter writer;
        final Object outboundLock = new Object();
        final ArrayDeque<Outbound> outbound = new ArrayDeque<>();
        PlayerState player;
        volatile boolean closed;
        volatile long lastReceivedAt = System.currentTimeMillis();
        volatile long writeStartedAt;
        Thread outputThread;

        Connection(Socket socket) throws IOException {
            this.socket = socket;
            this.writer = new PrintWriter(socket.getOutputStream(), true, StandardCharsets.UTF_8);
            connections.add(this);
        }

        void send(Object value) {
            boolean snapshot = value instanceof Map<?,?> map && "STATE_UPDATE".equals(map.get("type"));
            boolean geometry = value instanceof Map<?,?> map && map.containsKey("obstacles");
            Outbound item = new Outbound(JSON.toJson(value), snapshot, geometry);
            boolean overflow = false;
            synchronized (outboundLock) {
                if (closed) return;
                Outbound tail = outbound.peekLast();
                if (snapshot && tail != null && tail.snapshot() && (!tail.geometry() || geometry))
                    outbound.removeLast();
                if (outbound.size() >= MAX_OUTBOUND) overflow = true;
                else {
                    outbound.addLast(item);
                    outboundLock.notifyAll();
                }
            }
            if (overflow) close();
        }

        void close() {
            if (closed) return;
            closed = true;
            connections.remove(this);
            try { socket.close(); } catch (IOException ignored) { }
            synchronized (outboundLock) { outboundLock.notifyAll(); }
        }

        void closeIfUnhealthy(long now) {
            if (closed) return;
            // The writer can reset this volatile field to zero between two reads.
            // Capture it once or a completed write can appear to have started at epoch zero.
            long started = writeStartedAt;
            if (now - lastReceivedAt >= GameRules.CONNECTION_TIMEOUT_MS
                    || started > 0 && now - started >= GameRules.WRITE_STALL_MS) close();
        }

        private void startWriter() {
            outputThread = new Thread(this::writeLoop, "Bomb TCP writer");
            outputThread.setDaemon(true);
            outputThread.start();
        }

        private void writeLoop() {
            try {
                while (!closed) {
                    Outbound item;
                    synchronized (outboundLock) {
                        while (outbound.isEmpty() && !closed) outboundLock.wait();
                        if (closed) return;
                        item = outbound.removeFirst();
                    }
                    writeStartedAt = System.currentTimeMillis();
                    writer.println(item.json());
                    if (writer.checkError()) throw new IOException("Socket write failed");
                    writeStartedAt = 0;
                }
            } catch (IOException | InterruptedException ignored) {
                close();
            } finally {
                writeStartedAt = 0;
            }
        }

        @Override public void run() {
            startWriter();
            try (socket; BufferedReader input = new BufferedReader(new InputStreamReader(socket.getInputStream(), StandardCharsets.UTF_8))) {
                String line;
                while ((line = readFrame(input)) != null) {
                    long begin = System.nanoTime();
                    try {
                        JsonObject message = JsonParser.parseString(line).getAsJsonObject();
                        lastReceivedAt = System.currentTimeMillis();
                        handle(message);
                        if (player != null && !"PING".equals(string(message,"type")))
                            send(GameRoom.data("type","ACK","seq",number(message,"seq",-1),
                                "processingMicros",(System.nanoTime()-begin)/1000));
                    } catch (Exception e) { error("INVALID_MESSAGE"); }
                }
            } catch (IOException ignored) {
            } finally {
                disconnect();
                close();
            }
        }

        // Lock order for lifecycle transitions: player -> room -> connection writer.
        // GameRoom must never acquire the player monitor in the opposite order.
        private void withSessionState(PlayerState session, Runnable action) {
            synchronized (session) {
                GameRoom room = session.room;
                if (room == null) action.run();
                else synchronized (room) { action.run(); }
            }
        }

        void disconnect() {
            if (player == null) return;
            withSessionState(player, () -> {
                if (player.connection != this) return;
                leaveQueue(player);
                player.disconnectedAt = System.currentTimeMillis();
                player.connection = null;
                player.connected = false;
                player.dx = 0; player.dz = 0; player.charging = false;
                GameRoom room = player.room;
                if (room == null) return;
                if (!room.keepsDisconnectedSession()) {
                    room.leave(player);
                    if (room.players.isEmpty()) rooms.remove(room.code, room);
                } else {
                    Map<String,Object> roomState = GameRoom.data("type","ROOM_STATE","room",room.roomInfo());
                    room.players.forEach(p -> p.send(roomState));
                    room.players.forEach(p -> p.send(GameRoom.data("type","PLAYER_DISCONNECTED",
                        "playerId",player.id,"reconnectMs",GameRules.RECONNECT_MS,
                        "reconnectDeadline",player.disconnectedAt + GameRules.RECONNECT_MS)));
                }
            });
        }

        private boolean resume(PlayerState session) {
            withSessionState(session, () -> {
                // Match update()'s >= timeout boundary exactly.
                if (session.connected || System.currentTimeMillis() - session.disconnectedAt >= GameRules.RECONNECT_MS) return;
                player = session;
                // Keep the room locked until WELCOME and recovery state are sent:
                // a tick cannot send STATE_UPDATE ahead of WELCOME.
                send(GameRoom.data("type","WELCOME","playerId",player.id,
                    "resumeToken",player.resumeToken,"lastSeq",player.lastSeq,"hasRoom",player.room != null,
                    "syncMode",syncMode.name(),"demoMap",GameMap.configuredDemoMapId()));
                player.connection = this;
                player.connected = true;
                GameRoom room = player.room;
                if (room != null) {
                    Map<String,Object> roomState = GameRoom.data("type","ROOM_STATE","room",room.roomInfo());
                    room.players.forEach(p -> p.send(roomState));
                    if (room.phase == GameRoom.Phase.PLAYING)
                        send(room.snapshot(System.currentTimeMillis(), player, true));
                    if (room.phase == GameRoom.Phase.FINISHED) {
                        send(room.snapshot(System.currentTimeMillis(), player, true));
                        if (room.participatedInRound(player)) send(room.roundResult());
                        if (room.journeyComplete) send(room.journeyCompleteEvent());
                    }
                    room.players.forEach(p -> p.send(GameRoom.data("type","PLAYER_RECONNECTED","playerId",player.id)));
                } else send(GameRoom.data("type","LEFT_ROOM"));
            });
            return player != null;
        }

        private void handle(JsonObject m) {
            String type = string(m,"type");
            if (player == null) {
                if (!"HELLO".equals(type)) { error("HELLO_REQUIRED"); return; }
                String token = string(m,"resumeToken");
                PlayerState resumed = token.isEmpty() ? null : sessions.get(token);
                if (!token.isEmpty()) {
                    if (resumed == null || !resume(resumed)) error("RESUME_FAILED");
                    return;
                }
                String name = string(m,"name").trim();
                if (name.isEmpty() || name.length() > 20) { error("INVALID_NAME"); return; }
                player = new PlayerState(name, this);
                sessions.put(player.resumeToken, player);
                send(GameRoom.data("type","WELCOME","playerId",player.id,"resumeToken",player.resumeToken,
                    "syncMode",syncMode.name(),"demoMap",GameMap.configuredDemoMapId()));
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
                // Entering a room locks the player first, like matchmake(), so a match that is
                // being formed and a manual CREATE/JOIN can never both seat the same player.
                case "CREATE_ROOM" -> {
                    GameMode mode = GameMode.fromProtocol(string(m,"mode"));
                    if (mode == null) { error("INVALID_MODE"); return; }
                    synchronized (player) {
                        if (!canEnterRoom()) return;
                        createRoom(mode, player);
                    }
                }
                case "JOIN_ROOM" -> {
                    GameRoom target = rooms.get(string(m,"code").trim().toUpperCase());
                    if (target == null) { error("ROOM_NOT_FOUND"); return; }
                    // Optional: a client that picked a mode first only joins rooms of that mode.
                    GameMode expected = GameMode.fromProtocol(string(m,"mode"));
                    if (!string(m,"mode").isBlank() && expected != target.mode) {
                        send(GameRoom.data("type","ERROR","code","MODE_MISMATCH","mode",target.mode.name()));
                        return;
                    }
                    synchronized (player) {
                        if (!canEnterRoom()) return;
                        synchronized (target) {
                            if (rooms.get(target.code) != target || !target.join(player)) error("ROOM_UNAVAILABLE");
                        }
                    }
                }
                case "FIND_MATCH" -> {
                    GameMode mode = GameMode.fromProtocol(string(m,"mode"));
                    if (mode == null) { error("INVALID_MODE"); return; }
                    synchronized (player) {
                        if (!canEnterRoom()) return;
                        announceQueue(mode, matchmaker.enqueue(player, mode));
                    }
                }
                case "CANCEL_MATCH" -> {
                    synchronized (player) {
                        if (!leaveQueue(player)) { error("NOT_SEARCHING"); return; }
                        send(GameRoom.data("type","MATCHMAKING","status","CANCELLED"));
                    }
                }
                case "LEAVE_ROOM" -> {
                    withSessionState(player, () -> {
                        leaveQueue(player);
                        GameRoom current = player.room;
                        if (current != null) {
                            current.leave(player);
                            removeIfEmpty(current);
                        }
                        send(GameRoom.data("type","LEFT_ROOM"));
                    });
                }
                case "READY" -> {
                    if (room == null) { error("NOT_IN_ROOM"); return; }
                    room.ready(player, bool(m,"ready"));
                }
                case "MOVE", "CHARGE_START", "EXPLODE", "DROP_SHARD", "USE_SHIELD", "USE_BOOST",
                        "INTERACT_START", "INTERACT_STOP" -> {
                    if (room == null) { error("NOT_IN_ROOM"); return; }
                    double dx = decimal(m,"directionX",0), dz = decimal(m,"directionZ",0);
                    room.action(player,type,dx,dz,System.currentTimeMillis());
                }
                default -> error("UNKNOWN_ACTION");
            }
        }

        /** Caller holds the player monitor. */
        private boolean canEnterRoom() {
            if (player.room != null) { error("ALREADY_IN_ROOM"); return false; }
            if (matchmaker.modeOf(player) != null) { error("ALREADY_SEARCHING"); return false; }
            return true;
        }

        private boolean leaveQueue(PlayerState session) {
            GameMode mode = matchmaker.modeOf(session);
            List<PlayerState> waiting = matchmaker.remove(session);
            if (waiting == null) return false;
            announceQueue(mode, waiting);
            return true;
        }

        private void error(String code) { send(GameRoom.data("type","ERROR","code",code)); }

    }

    /** Bound allocation even if a peer never sends a newline. Limit is UTF-16 characters. */
    static String readFrame(java.io.Reader input) throws IOException {
        StringBuilder frame = new StringBuilder(256);
        int c;
        while ((c = input.read()) != -1) {
            if (c == '\n') return frame.toString();
            if (frame.length() >= 2048) throw new IOException("MESSAGE_TOO_LARGE");
            frame.append((char)c);
        }
        return frame.isEmpty() ? null : frame.toString();
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
