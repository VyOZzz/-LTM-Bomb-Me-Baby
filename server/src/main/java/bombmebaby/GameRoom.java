package bombmebaby;

import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Random;

/** All room mutation happens under this monitor, including network actions and ticks. */
final class GameRoom {
    enum Phase { WAITING, PLAYING, FINISHED }
    record Pickup(int id, double x, double z) {}

    final String code;
    final List<PlayerState> players = new ArrayList<>();
    final List<Pickup> orbs = new ArrayList<>();
    final List<Pickup> cores = new ArrayList<>();
    final Random random = new Random();
    List<GameMap.Obstacle> obstacles;
    List<GameMap.Bush> bushes;
    GameMap map;
    Phase phase = Phase.WAITING;
    long tick;
    private int nextPickupId = 1;
    private long lastOrbAt;
    private boolean soloRound;
    private boolean hasStarted;

    GameRoom(String code) {
        this.code = code;
        applyMap(GameMap.random(random, null));
    }

    synchronized boolean join(PlayerState player) {
        if (phase == Phase.PLAYING || players.size() >= GameRules.MAX_PLAYERS || players.contains(player)) return false;
        players.add(player);
        player.room = this;
        player.ready = false;
        broadcast(data("type", "ROOM_STATE", "room", roomInfo()));
        return true;
    }

    synchronized void leave(PlayerState player) {
        if (!players.contains(player)) return;
        if (phase == Phase.PLAYING && player.alive) eliminate(player, "left", System.currentTimeMillis());
        players.remove(player);
        player.room = null;
        player.ready = false;
        checkGameOver();
        broadcast(data("type", "ROOM_STATE", "room", roomInfo()));
    }

    synchronized void ready(PlayerState player, boolean value) {
        if (phase == Phase.PLAYING) { reject(player,"MATCH_IN_PROGRESS"); return; }
        player.ready = value;
        broadcast(data("type", "ROOM_STATE", "room", roomInfo()));
        if (!players.isEmpty() && players.stream().allMatch(p -> p.ready && p.connected)) start(System.currentTimeMillis());
    }

    private void start(long now) {
        if (hasStarted) applyMap(GameMap.random(random, map.id));
        List<double[]> spawns = randomSpawns(players.size());
        hasStarted = true;
        phase = Phase.PLAYING;
        soloRound = players.size() == 1;
        tick = 0;
        orbs.clear(); cores.clear();
        for (int i = 0; i < players.size(); i++) {
            PlayerState p = players.get(i);
            p.x = spawns.get(i)[0]; p.z = spawns.get(i)[1]; p.dx = 0; p.dz = 0;
            p.hp = 100; p.size = 1; p.baseSize = 1; p.kills = 0;
            p.alive = true; p.charging = false; p.shieldUntil = 0;
            p.shieldReadyAt = 0; p.boostUntil = 0; p.boostReadyAt = 0;
            p.ready = false;
        }
        for (int i = 0; i < GameRules.INITIAL_ORBS; i++) spawnOrb();
        lastOrbAt = now;
        // GAME_START always includes the static map geometry so the client
        // has the full layout even when optimized STATE_UPDATEs omit it.
        broadcast(data("type", "GAME_START", "room", code,
            "mapId", map.id, "mapName", map.name, "arenaSize", map.halfSize,
            "obstacles", obstacles, "bushes", bushes));
        broadcastSnapshots(now);
    }

    synchronized void action(PlayerState p, String type, double dx, double dz, long now) {
        if (phase != Phase.PLAYING || !p.alive || !p.connected) { reject(p,"ACTION_UNAVAILABLE"); return; }
        switch (type) {
            case "MOVE" -> {
                if (!Double.isFinite(dx) || !Double.isFinite(dz)) { reject(p,"INVALID_MOVEMENT"); return; }
                double length = Math.hypot(dx, dz);
                p.dx = length > 1 ? dx / length : dx;
                p.dz = length > 1 ? dz / length : dz;
            }
            case "CHARGE_START" -> {
                if (!p.charging) { p.charging = true; p.chargeSince = now; }
                else reject(p,"ALREADY_CHARGING");
            }
            case "EXPLODE" -> {
                if (p.charging) explode(p, now);
                else reject(p,"NOT_CHARGING");
            }
            case "USE_SHIELD" -> {
                if (now >= p.shieldReadyAt) {
                    p.shieldUntil = now + GameRules.SHIELD_MS;
                    p.shieldReadyAt = now + GameRules.SHIELD_COOLDOWN_MS;
                    broadcast(data("type", "SHIELD_ACTIVATED", "playerId", p.id));
                } else reject(p,"SHIELD_COOLDOWN");
            }
            case "USE_BOOST" -> {
                if (now >= p.boostReadyAt) {
                    p.boostUntil = now + GameRules.BOOST_MS;
                    p.boostReadyAt = now + GameRules.BOOST_COOLDOWN_MS;
                    p.size = Math.max(p.baseSize, p.size - 0.23);
                    double ox = p.x - p.dx * 0.8, oz = p.z - p.dz * 0.8;
                    if (orbs.size() < GameRules.MAX_ORBS && free(ox, oz, 0.2)) orbs.add(new Pickup(nextPickupId++, ox, oz));
                    broadcast(data("type", "BOOST_ACTIVATED", "playerId", p.id));
                } else reject(p,"BOOST_COOLDOWN");
            }
            default -> { }
        }
    }

    synchronized void update(long now, double dt) {
        if (phase != Phase.PLAYING) return;
        tick++;
        for (PlayerState p : players) {
            if (!p.alive) continue;
            if (!p.connected && now - p.disconnectedAt >= GameRules.RECONNECT_MS) {
                eliminate(p, "disconnect", now);
                checkGameOver();
                continue;
            }
            if (p.connected) {
                double speed = 5.2 / Math.sqrt(p.size) * (now < p.boostUntil ? 1.85 : 1);
                double nx = GameRules.clamp(p.x + p.dx * speed * dt, -map.halfSize, map.halfSize);
                double nz = GameRules.clamp(p.z + p.dz * speed * dt, -map.halfSize, map.halfSize);
                if (free(nx, p.z, GameRules.PLAYER_RADIUS * p.size)) p.x = nx;
                if (free(p.x, nz, GameRules.PLAYER_RADIUS * p.size)) p.z = nz;
                pickup(p, now);
            }
            if (p.charging && now - p.chargeSince >= GameRules.MAX_CHARGE_MS) explode(p, now);
        }
        if (phase != Phase.PLAYING) return;
        pickupCores();
        if (now - lastOrbAt >= 500 && orbs.size() < GameRules.MAX_ORBS) { spawnOrb(); lastOrbAt = now; }
        if (tick % 2 == 0) broadcastSnapshots(now);
    }

    private void pickup(PlayerState p, long now) {
        for (int i = orbs.size() - 1; i >= 0; i--) {
            Pickup o = orbs.get(i);
            if (GameRules.distance(p.x,p.z,o.x,o.z) < 0.55 * p.size + 0.35) {
                orbs.remove(i);
                p.size = Math.min(GameRules.MAX_SIZE, p.size + 0.12);
                p.hp = Math.min(100, p.hp + 3);
                broadcast(data("type","ENERGY_PICKED","playerId",p.id,"pickupId",o.id));
            }
        }
    }

    private void pickupCores() {
        for (int i = cores.size() - 1; i >= 0; i--) {
            Pickup core = cores.get(i);
            PlayerState closest = null;
            double bestDistance = Double.POSITIVE_INFINITY;
            for (PlayerState p : players) {
                if (!p.alive || !p.connected) continue;
                double distance = GameRules.distance(p.x,p.z,core.x,core.z);
                if (distance < 0.55 * p.size + 0.45 && distance < bestDistance) {
                    closest = p;
                    bestDistance = distance;
                }
            }
            if (closest == null) continue;
            cores.remove(i);
            closest.baseSize = Math.min(GameRules.MAX_BASE, closest.baseSize + 0.1);
            closest.size = Math.max(closest.size, closest.baseSize);
            broadcast(data("type","CORE_PICKED","playerId",closest.id,"pickupId",core.id));
        }
    }

    private void explode(PlayerState source, long now) {
        source.charging = false;
        double charge = GameRules.clamp((now - source.chargeSince) / (double) GameRules.MAX_CHARGE_MS, 0, 1);
        double radius = GameRules.blastRadius(source.size, charge);
        double damageDone = 0;
        for (PlayerState target : players) {
            if (target == source || !target.alive) continue;
            double distance = GameRules.distance(source.x,source.z,target.x,target.z);
            double damage = GameRules.damage(source.size,charge,distance,radius);
            if (damage <= 0) continue;
            if (now < target.shieldUntil) {
                damageDone += damage * 0.5;
                broadcast(data("type","DAMAGE_REFLECTED","from",target.id,"to",source.id,"amount",damage * 0.5));
            } else {
                target.hp = Math.max(0, target.hp - damage);
                broadcast(data("type","PLAYER_DAMAGED","playerId",target.id,"by",source.id,"amount",damage));
                if (target.hp <= 0) { source.kills++; eliminate(target, "explosion", now); }
            }
        }
        source.hp = Math.max(0, source.hp - damageDone);
        source.size = source.baseSize;
        broadcast(data("type","PLAYER_EXPLODE","playerId",source.id,"x",source.x,"z",source.z,"radius",radius));
        if (source.hp <= 0) eliminate(source, "reflection", now);
        checkGameOver();
    }

    private void eliminate(PlayerState p, String reason, long now) {
        if (!p.alive) return;
        p.alive = false; p.hp = 0; p.charging = false; p.dx = 0; p.dz = 0;
        Pickup core = new Pickup(nextPickupId++, p.x, p.z);
        cores.add(core);
        broadcast(data("type","PLAYER_ELIMINATED","playerId",p.id,"reason",reason));
        broadcast(data("type","CORE_SPAWNED","pickupId",core.id,"x",core.x,"z",core.z));
    }

    private void checkGameOver() {
        if (phase != Phase.PLAYING) return;
        List<PlayerState> survivors = players.stream().filter(p -> p.alive).toList();
        // Solo rooms are a gameplay-test mode: one living player must not end
        // the round after every explosion. A solo round still finishes if the
        // player is eliminated or disconnects and no survivors remain.
        if (soloRound && survivors.size() == 1) return;
        if (survivors.size() <= 1) {
            phase = Phase.FINISHED;
            broadcast(data("type","GAME_OVER","winnerId",survivors.isEmpty() ? "" : survivors.get(0).id,"room",code));
            broadcast(data("type","ROOM_STATE","room",roomInfo()));
        }
    }

    private void spawnOrb() {
        for (int i = 0; i < 40; i++) {
            double margin = 1.0;
            double span = (map.halfSize - margin) * 2;
            double x = -map.halfSize + margin + random.nextDouble() * span;
            double z = -map.halfSize + margin + random.nextDouble() * span;
            if (free(x,z,0.25)) { orbs.add(new Pickup(nextPickupId++,x,z)); return; }
        }
    }

    private boolean free(double x, double z, double radius) {
        if (Math.abs(x) + radius > map.halfSize || Math.abs(z) + radius > map.halfSize) return false;
        for (GameMap.Obstacle o : obstacles)
            if (GameRules.distance(x,z,o.x(),o.z()) < radius + o.radius()) return false;
        return true;
    }

    private boolean freeSpawn(double x, double z) {
        if (!free(x, z, GameRules.PLAYER_RADIUS)) return false;
        for (GameMap.Bush bush : bushes)
            if (GameRules.distance(x,z,bush.x(),bush.z()) < bush.radius() + 2.0) return false;
        return true;
    }

    /** Farthest-point sampling keeps every randomized start outside initial visibility range. */
    private List<double[]> randomSpawns(int count) {
        List<double[]> candidates = new ArrayList<>();
        double margin = 3.0;
        double span = (map.halfSize - margin) * 2;
        for (int i = 0; i < 2_000; i++) {
            double x = -map.halfSize + margin + random.nextDouble() * span;
            double z = -map.halfSize + margin + random.nextDouble() * span;
            if (freeSpawn(x,z)) candidates.add(new double[] {x,z});
        }
        if (candidates.size() < count) throw new IllegalStateException("Map has too few spawn candidates: " + map.id);

        List<double[]> result = new ArrayList<>();
        result.add(candidates.remove(random.nextInt(candidates.size())));
        while (result.size() < count) {
            int bestIndex = -1;
            double bestDistance = -1;
            for (int i = 0; i < candidates.size(); i++) {
                double[] candidate = candidates.get(i);
                double nearest = result.stream()
                    .mapToDouble(spawn -> GameRules.distance(candidate[0],candidate[1],spawn[0],spawn[1]))
                    .min().orElse(Double.POSITIVE_INFINITY);
                if (nearest > bestDistance) { bestDistance = nearest; bestIndex = i; }
            }
            if (bestIndex < 0 || bestDistance < GameRules.MIN_SPAWN_DISTANCE)
                throw new IllegalStateException("Cannot separate " + count + " spawns on map " + map.id);
            result.add(candidates.remove(bestIndex));
        }
        return result;
    }

    private void applyMap(GameMap selected) {
        map = selected;
        obstacles = selected.obstacles;
        bushes = selected.bushes;
    }

    synchronized Map<String,Object> roomInfo() {
        return data("code",code,"phase",phase.name(),"mapId",map.id,"mapName",map.name,
            "arenaSize",map.halfSize,"maxPlayers",GameRules.MAX_PLAYERS,"players",players.stream().map(p -> data(
            "id",p.id,"name",p.name,"ready",p.ready,"connected",p.connected)).toList());
    }

    /** Full snapshot (baseline mode, no viewer). Used by existing tests. */
    synchronized Map<String,Object> snapshot(long now) { return snapshot(now, null); }

    /** Snapshot for a specific viewer, using the server-wide SyncMode. */
    synchronized Map<String,Object> snapshot(long now, PlayerState viewer) {
        return snapshot(now, viewer, false);
    }

    /**
     * Build a STATE_UPDATE snapshot.
     *
     * @param now           current epoch millis
     * @param viewer        the player who will receive this snapshot (null = spectator / test)
     * @param forceBaseline if true, include obstacles/bushes and full data even in optimized mode
     *                      (used on reconnect so the client gets a complete picture)
     */
    synchronized Map<String,Object> snapshot(long now, PlayerState viewer, boolean forceBaseline) {
        boolean optimized = !forceBaseline && SyncMode.active() == SyncMode.OPTIMIZED;

        // ---- player states ----
        List<Map<String,Object>> states = new ArrayList<>();
        for (PlayerState p : players) {
            boolean visible = viewer == null || canSee(viewer, p);
            // In optimized mode, hide exact coordinates of invisible players
            // so the client cannot derive positions it should not know.
            double px = (!optimized || visible) ? p.x : 0;
            double pz = (!optimized || visible) ? p.z : 0;
            states.add(data(
                "id",p.id,"name",p.name,"x",px,"z",pz,"hp",p.hp,"size",p.size,
                "baseSize",p.baseSize,"alive",p.alive,"connected",p.connected,
                "visible",visible,
                "charging",p.charging,"charge",p.charging ? GameRules.clamp((now-p.chargeSince)/8000.0,0,1) : 0,
                "shield",now < p.shieldUntil,"shieldCooldown",Math.max(0,p.shieldReadyAt-now)/1000.0,
                "boost",now < p.boostUntil,"boostCooldown",Math.max(0,p.boostReadyAt-now)/1000.0,
                "kills",p.kills));
        }

        // ---- pickups: optionally cull by distance ----
        List<?> visibleOrbs = orbs;
        List<?> visibleCores = cores;
        if (optimized && viewer != null) {
            visibleOrbs = orbs.stream()
                .filter(o -> GameRules.distance(viewer.x,viewer.z,o.x,o.z) <= SyncMode.PICKUP_VISIBILITY)
                .toList();
            visibleCores = cores.stream()
                .filter(c -> GameRules.distance(viewer.x,viewer.z,c.x,c.z) <= SyncMode.PICKUP_VISIBILITY)
                .toList();
        }

        Map<String,Object> snap = data("type","STATE_UPDATE","tick",tick,"phase",phase.name(),
            "mapId",map.id,"mapName",map.name,"arenaSize",map.halfSize,
            "players",states,"orbs",visibleOrbs,"cores",visibleCores);

        // In baseline mode (or forced), include static map geometry every tick.
        // In optimized mode the client already has them from GAME_START.
        if (!optimized) {
            snap.put("obstacles", obstacles);
            snap.put("bushes", bushes);
        }
        return snap;
    }

    synchronized boolean canSee(PlayerState viewer, PlayerState target) {
        if (viewer == null || viewer == target) return true;
        if (GameRules.distance(viewer.x,viewer.z,target.x,target.z) > GameRules.PLAYER_VISIBILITY_DISTANCE)
            return false;
        int targetBush = bushAt(target.x, target.z);
        return targetBush < 0 || bushAt(viewer.x, viewer.z) == targetBush;
    }

    private int bushAt(double x, double z) {
        for (int i = 0; i < bushes.size(); i++) {
            GameMap.Bush bush = bushes.get(i);
            if (GameRules.distance(x,z,bush.x(),bush.z()) <= bush.radius()) return i;
        }
        return -1;
    }

    private void broadcastSnapshots(long now) {
        for (PlayerState player : players) player.send(snapshot(now, player));
    }

    private void broadcast(Object message) { for (PlayerState p : players) p.send(message); }

    private void reject(PlayerState player, String code) {
        player.send(data("type","ERROR","code",code));
    }

    static Map<String,Object> data(Object... entries) {
        Map<String,Object> result = new LinkedHashMap<>();
        for (int i = 0; i < entries.length; i += 2) result.put((String) entries[i], entries[i+1]);
        return result;
    }
}
