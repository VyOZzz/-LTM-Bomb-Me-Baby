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
    private static final List<String> JOURNEY_MAPS = List.of("meadow", "crossroads", "crater");
    private static final int ROUND_WIN_POINTS = 3;
    private static final int ELIMINATION_POINTS = 1;
    static final long QUICK_ROUND_MS = 180_000;

    static final class ScoreState {
        final String playerId;
        final String name;
        int points;
        int roundWins;      // chapters won = Fire Heart shards brought home
        int eliminations;

        ScoreState(PlayerState player) {
            playerId = player.id;
            name = player.name;
        }

        Map<String,Object> data() {
            return GameRoom.data("playerId",playerId,"name",name,
                "points",points,"roundWins",roundWins,"eliminations",eliminations);
        }
    }

    final String code;
    final SyncMode syncMode;
    final GameMode mode;
    final List<PlayerState> players = new ArrayList<>();
    final List<Pickup> orbs = new ArrayList<>();
    final List<Pickup> cores = new ArrayList<>();
    final Map<String,ScoreState> journeyScores = new LinkedHashMap<>();
    final Random random = new Random();
    List<GameMap.Obstacle> obstacles;
    List<GameMap.Bush> bushes;
    GameMap map;
    /** Shard and pedestal of the current Journey chapter; null in Quick mode. */
    ShardObjective objective;
    Phase phase = Phase.WAITING;
    long tick;
    int chapterIndex;
    boolean journeyComplete;
    List<String> journeyWinnerIds = List.of();
    String finalRoundWinnerId = "";
    String roundWinnerId = "";
    String roundEndReason = "";
    String journeyEndReason = "";
    private long roundStartedAt;
    int roundIndex;
    private int nextPickupId = 1;
    private long lastOrbAt;
    private boolean hasStarted;

    GameRoom(String code) {
        this(code, SyncMode.active(), GameMode.QUICK);
    }

    GameRoom(String code, SyncMode syncMode) {
        this(code, syncMode, GameMode.QUICK);
    }

    GameRoom(String code, SyncMode syncMode, GameMode mode) {
        this.code = code;
        this.syncMode = syncMode;
        this.mode = mode;
        applyMap(mode == GameMode.JOURNEY ? GameMap.byId(JOURNEY_MAPS.get(0)) : GameMap.random(random, null));
    }

    synchronized boolean join(PlayerState player) {
        if (phase == Phase.PLAYING || (mode == GameMode.JOURNEY && hasStarted)
                || players.size() >= GameRules.MAX_PLAYERS || players.contains(player)) return false;
        players.add(player);
        if (mode == GameMode.JOURNEY) journeyScores.put(player.id, new ScoreState(player));
        player.room = this;
        player.ready = false;
        broadcast(data("type", "ROOM_STATE", "room", roomInfo()));
        return true;
    }

    synchronized void leave(PlayerState player) {
        if (!players.contains(player)) return;
        if (phase == Phase.PLAYING && player.alive) eliminate(player, "left", System.currentTimeMillis());
        players.remove(player);
        if (mode == GameMode.JOURNEY && !hasStarted) journeyScores.remove(player.id);
        player.room = null;
        player.ready = false;
        checkGameOver();
        completeJourneyIfShort(System.currentTimeMillis());
        broadcast(data("type", "ROOM_STATE", "room", roomInfo()));
    }

    synchronized void ready(PlayerState player, boolean value) {
        if (phase == Phase.PLAYING) { reject(player,"MATCH_IN_PROGRESS"); return; }
        if (journeyComplete) { reject(player,"JOURNEY_COMPLETE"); return; }
        player.ready = value;
        broadcast(data("type", "ROOM_STATE", "room", roomInfo()));
        if (players.size() >= GameRules.MIN_PLAYERS && players.stream().allMatch(p -> p.ready && p.connected))
            start(System.currentTimeMillis());
    }

    /** Matchmade rooms skip the ready check: everyone in them already asked to play. */
    synchronized boolean startMatched(long now) {
        if (phase != Phase.WAITING || players.size() < GameRules.MIN_PLAYERS) return false;
        start(now);
        return true;
    }

    private void start(long now) {
        if (mode == GameMode.JOURNEY) {
            if (hasStarted) chapterIndex++;
            applyMap(GameMap.byId(JOURNEY_MAPS.get(chapterIndex)));
        } else if (hasStarted) {
            applyMap(GameMap.random(random, map.id));
        }
        List<double[]> spawns = randomSpawns(players.size());
        hasStarted = true;
        phase = Phase.PLAYING;
        roundStartedAt = now;
        roundIndex++;
        roundWinnerId = "";
        roundEndReason = "";
        tick = 0;
        orbs.clear(); cores.clear();
        for (int i = 0; i < players.size(); i++) {
            PlayerState p = players.get(i);
            p.x = spawns.get(i)[0]; p.z = spawns.get(i)[1]; p.dx = 0; p.dz = 0;
            p.knockbackX = 0; p.knockbackZ = 0;
            p.hp = 100; p.size = 1; p.baseSize = 1; p.kills = 0;
            p.alive = true; p.charging = false; p.chargeReadyAt = 0; p.shieldUntil = 0;
            p.shieldReadyAt = 0; p.boostUntil = 0; p.boostReadyAt = 0;
            p.staggerUntil = 0; p.staggerImmuneUntil = 0;
            p.ready = false;
        }
        for (int i = 0; i < GameRules.INITIAL_ORBS; i++) spawnOrb();
        lastOrbAt = now;
        if (mode == GameMode.JOURNEY) {
            double[] shard = shardSpawn();
            objective = new ShardObjective(now, shard[0], shard[1]);
        } else objective = null;
        // GAME_START always includes the static map geometry so the client
        // has the full layout even when optimized STATE_UPDATEs omit it.
        broadcast(data("type", "GAME_START", "room", code,
            "mapId", map.id, "mapName", map.name, "arenaSize", map.halfSize,
            "mode",mode.name(),"roundIndex",roundIndex,"chapterIndex",protocolChapterIndex(),"scores",scores(),
            "obstacles", obstacles, "bushes", bushes));
        broadcastSnapshots(now);
    }

    synchronized void action(PlayerState p, String type, double dx, double dz, long now) {
        if (phase != Phase.PLAYING || !p.alive || !p.connected) { reject(p,"ACTION_UNAVAILABLE"); return; }
        switch (type) {
            case "MOVE" -> {
                if (!Double.isFinite(dx) || !Double.isFinite(dz)) { reject(p,"INVALID_MOVEMENT"); return; }
                p.lastMoveAt = now;
                double length = Math.hypot(dx, dz);
                p.dx = length > 1 ? dx / length : dx;
                p.dz = length > 1 ? dz / length : dz;
            }
            case "CHARGE_START" -> {
                if (objective != null && objective.carriedBy(p)) reject(p,"CARRYING_SHARD");
                else if (now < p.chargeReadyAt) reject(p,"BOMB_COOLDOWN");
                else if (!p.charging) { p.charging = true; p.chargeSince = now; }
                else reject(p,"ALREADY_CHARGING");
            }
            case "EXPLODE" -> {
                if (objective != null && objective.carriedBy(p)) {
                    p.charging = false;
                    reject(p,"CARRYING_SHARD");
                } else if (p.charging) explode(p, now);
                else reject(p,"NOT_CHARGING");
            }
            case "DROP_SHARD" -> {
                if (objective == null || !objective.drop(p, now)) reject(p,"NOT_CARRYING_SHARD");
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
                    double sizeBefore = p.size;
                    p.size = Math.max(p.baseSize, p.size - 0.23);
                    double ox = p.x - p.dx * 0.8, oz = p.z - p.dz * 0.8;
                    if (sizeBefore - p.size + 1e-6 >= GameRules.ENERGY_SIZE_VALUE
                            && orbs.size() < GameRules.MAX_ORBS && free(ox, oz, 0.2))
                        orbs.add(new Pickup(nextPickupId++, ox, oz));
                    broadcast(data("type", "BOOST_ACTIVATED", "playerId", p.id));
                } else reject(p,"BOOST_COOLDOWN");
            }
            default -> { }
        }
    }

    synchronized void update(long now, double dt) {
        if (phase != Phase.PLAYING) {
            List<PlayerState> expired = players.stream()
                .filter(p -> !p.connected && now - p.disconnectedAt >= GameRules.RECONNECT_MS)
                .toList();
            for (PlayerState player : expired) leave(player);
            return;
        }
        tick++;
        // Resolve every expired disconnect before explosions or winner selection.
        for (PlayerState p : players) {
            if (p.alive && !p.connected && now - p.disconnectedAt >= GameRules.RECONNECT_MS) {
                eliminate(p, "disconnect", now);
            }
        }
        checkGameOver();
        completeJourneyIfShort(now);
        if (phase != Phase.PLAYING) return;
        if (mode == GameMode.QUICK && now - roundStartedAt >= QUICK_ROUND_MS) {
            finishRound(null, "TIME");
            return;
        }
        for (PlayerState p : players) {
            if (!p.alive) continue;
            if (p.connected) {
                if ((p.dx != 0 || p.dz != 0) && now - p.lastMoveAt >= GameRules.MOVEMENT_STALE_MS) {
                    p.dx = 0;
                    p.dz = 0;
                }
                double speed = 5.2 / Math.sqrt(p.size) * (now < p.boostUntil ? 1.85 : 1);
                if (now >= p.staggerUntil) movePlayer(p, p.dx * speed * dt, p.dz * speed * dt, false);
                pickup(p, now);
            }
            movePlayer(p, p.knockbackX * dt, p.knockbackZ * dt, true);
            double decay = Math.exp(-GameRules.KNOCKBACK_DECAY_PER_SECOND * Math.max(0, dt));
            p.knockbackX *= decay;
            p.knockbackZ *= decay;
            if (Math.hypot(p.knockbackX, p.knockbackZ) < GameRules.KNOCKBACK_STOP_SPEED) {
                p.knockbackX = 0;
                p.knockbackZ = 0;
            }
            if (p.charging && now - p.chargeSince >= GameRules.MAX_CHARGE_MS) explode(p, now);
            if (phase != Phase.PLAYING) return;
        }
        pickupCores();
        if (objective != null && objective.update(now, players)) {
            finishRound(objective.holder(), "TIME");
            return;
        }
        if (now - lastOrbAt >= 500 && orbs.size() < GameRules.MAX_ORBS) { spawnOrb(); lastOrbAt = now; }
        if (tick % 2 == 0) broadcastSnapshots(now);
    }

    private void pickup(PlayerState p, long now) {
        double previousSize = p.size;
        for (int i = orbs.size() - 1; i >= 0; i--) {
            Pickup o = orbs.get(i);
            if (GameRules.distance(p.x,p.z,o.x,o.z) < 0.55 * p.size + 0.35) {
                orbs.remove(i);
                p.size = Math.min(GameRules.MAX_SIZE, p.size + 0.12);
                p.hp = Math.min(100, p.hp + 3);
                broadcast(data("type","ENERGY_PICKED","playerId",p.id,"pickupId",o.id));
            }
        }
        if (p.size > previousSize) resolveGrowthOverlap(p);
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
            resolveGrowthOverlap(closest);
            broadcast(data("type","CORE_PICKED","playerId",closest.id,"pickupId",core.id));
        }
    }

    private void explode(PlayerState source, long now) {
        source.charging = false;
        source.chargeReadyAt = now + GameRules.BOMB_COOLDOWN_MS;
        double sizeBeforeExplosion = source.size;
        double charge = GameRules.clamp((now - source.chargeSince) / (double) GameRules.MAX_CHARGE_MS, 0, 1);
        double radius = GameRules.blastRadius(source.size, charge);
        double damageDone = 0;
        PlayerState lastReflector = null;
        for (PlayerState target : players) {
            if (target == source || !target.alive) continue;
            double distance = GameRules.distance(source.x,source.z,target.x,target.z);
            double damage = GameRules.damage(source.size,charge,distance,radius);
            if (damage <= 0) continue;
            boolean shielded = now < target.shieldUntil;
            if (mode == GameMode.JOURNEY)
                applyKnockback(source, target, charge, damage, distance, radius, shielded);
            if (shielded) {
                damageDone += damage * 0.5;
                lastReflector = target;
                broadcast(data("type","DAMAGE_REFLECTED","from",target.id,"to",source.id,"amount",damage * 0.5));
            } else {
                double hpBefore = target.hp;
                target.hp = Math.max(0, target.hp - damage);
                double actualDamage = hpBefore - target.hp;
                broadcast(data("type","PLAYER_DAMAGED","playerId",target.id,"by",source.id,"amount",damage));
                applyDamageConsequences(target, source, charge, actualDamage, now);
                if (target.hp <= 0) {
                    source.kills++;
                    awardElimination(source);
                    eliminate(target, "explosion", now);
                }
            }
        }
        double sourceHpBefore = source.hp;
        source.hp = Math.max(0, source.hp - damageDone);
        if (lastReflector != null)
            applyDamageConsequences(source, lastReflector, charge, sourceHpBefore - source.hp, now);
        dropExplosionEnergy(source, sizeBeforeExplosion);
        source.size = source.baseSize;
        broadcastExplosion(source, radius);
        if (source.hp <= 0) {
            if (lastReflector != null) {
                lastReflector.kills++;
                awardElimination(lastReflector);
            }
            eliminate(source, "reflection", now);
        }
        checkGameOver();
    }

    private void applyDamageConsequences(PlayerState target, PlayerState source,
                                         double charge, double actualDamage, long now) {
        if (target.hp <= 0 || actualDamage <= 0 || objective == null) return;
        double[] drop = safeShardDrop(target, source.x, source.z);
        if (objective.onDamaged(target, charge, actualDamage, now, drop[0], drop[1])
                && now >= target.staggerImmuneUntil) {
            target.staggerUntil = now + GameRules.SEAL_STAGGER_MS;
            target.staggerImmuneUntil = now + GameRules.SEAL_STAGGER_IMMUNITY_MS;
        }
    }

    /** Turn the temporary Size spent by an explosion back into visible Energy fragments. */
    private void dropExplosionEnergy(PlayerState source, double sizeBeforeExplosion) {
        double spentSize = Math.max(0, sizeBeforeExplosion - source.baseSize);
        int count = Math.min(GameRules.MAX_EXPLOSION_ENERGY_DROPS,
            (int) Math.floor(spentSize / GameRules.ENERGY_SIZE_VALUE + 1e-6));
        count = Math.min(count, GameRules.MAX_ORBS - orbs.size());
        if (count <= 0) return;

        double phase = random.nextDouble() * Math.PI * 2;
        for (int i = 0; i < count; i++) {
            double angle = phase + Math.PI * 2 * i / count;
            double distance = 1.0 + 0.35 * (i % 3);
            double x = GameRules.clamp(source.x + Math.cos(angle) * distance,
                -map.halfSize + .25, map.halfSize - .25);
            double z = GameRules.clamp(source.z + Math.sin(angle) * distance,
                -map.halfSize + .25, map.halfSize - .25);
            if (!free(x, z, .2)) { x = source.x; z = source.z; }
            orbs.add(new Pickup(nextPickupId++, x, z));
        }
    }

    private void awardElimination(PlayerState player) {
        if (mode != GameMode.JOURNEY) return;
        ScoreState score = journeyScores.get(player.id);
        if (score != null) {
            score.points += ELIMINATION_POINTS;
            score.eliminations++;
        }
    }

    private void eliminate(PlayerState p, String reason, long now) {
        if (!p.alive) return;
        p.alive = false; p.hp = 0; p.charging = false; p.dx = 0; p.dz = 0;
        p.knockbackX = 0; p.knockbackZ = 0;
        if (objective != null) objective.onEliminated(p, now);
        Pickup core = new Pickup(nextPickupId++, p.x, p.z);
        cores.add(core);
        broadcast(data("type","PLAYER_ELIMINATED","playerId",p.id,"reason",reason));
        broadcastCoreSpawn(core);
    }

    private void broadcastExplosion(PlayerState source, double radius) {
        for (PlayerState viewer : players) {
            boolean visible = syncMode != SyncMode.OPTIMIZED || canSee(viewer, source);
            viewer.send(data("type","PLAYER_EXPLODE","playerId",source.id,
                "x",visible ? source.x : 0,"z",visible ? source.z : 0,
                "radius",radius,"visible",visible));
        }
    }

    private void applyKnockback(PlayerState source, PlayerState target, double charge,
                                double damage, double distance, double radius, boolean shielded) {
        double dx = target.x - source.x;
        double dz = target.z - source.z;
        double length = Math.hypot(dx, dz);
        if (length < 0.001) {
            double angle = (target.id.hashCode() & 0xffff) / 65535.0 * Math.PI * 2;
            dx = Math.cos(angle);
            dz = Math.sin(angle);
            length = 1;
        }
        double speed = GameRules.knockbackSpeed(charge, damage, distance, radius, target.size, shielded);
        double dirX = dx / length, dirZ = dz / length;
        target.knockbackX += dirX * speed;
        target.knockbackZ += dirZ * speed;
        double total = Math.hypot(target.knockbackX, target.knockbackZ);
        if (total > GameRules.KNOCKBACK_MAX_SPEED) {
            target.knockbackX *= GameRules.KNOCKBACK_MAX_SPEED / total;
            target.knockbackZ *= GameRules.KNOCKBACK_MAX_SPEED / total;
        }
        for (PlayerState viewer : players) {
            if (syncMode == SyncMode.OPTIMIZED && !canSee(viewer, target)) continue;
            viewer.send(data("type","PLAYER_KNOCKED_BACK","playerId",target.id,
                "directionX",dirX,"directionZ",dirZ,"strength",speed,"shielded",shielded));
        }
    }

    private void movePlayer(PlayerState p, double moveX, double moveZ, boolean impulse) {
        double radius = GameRules.PLAYER_RADIUS * p.size;
        double nx = GameRules.clamp(p.x + moveX, -map.halfSize + radius, map.halfSize - radius);
        double nz = GameRules.clamp(p.z + moveZ, -map.halfSize + radius, map.halfSize - radius);
        if (free(nx, p.z, radius)) p.x = nx;
        else if (impulse) p.knockbackX = 0;
        if (free(p.x, nz, radius)) p.z = nz;
        else if (impulse) p.knockbackZ = 0;
    }

    /** Growing changes collision radius immediately; project out before the next move. */
    private void resolveGrowthOverlap(PlayerState p) {
        double radius = GameRules.PLAYER_RADIUS * p.size;
        double originX = p.x, originZ = p.z;
        for (int pass = 0; pass < 16; pass++) {
            p.x = GameRules.clamp(p.x, -map.halfSize + radius, map.halfSize - radius);
            p.z = GameRules.clamp(p.z, -map.halfSize + radius, map.halfSize - radius);
            if (free(p.x, p.z, radius)) return;
            for (GameMap.Obstacle rock : obstacles) {
                double dx = p.x - rock.x(), dz = p.z - rock.z();
                double distance = Math.hypot(dx, dz), separation = radius + rock.radius() + 0.001;
                if (distance >= separation) continue;
                if (distance < 1e-8) { dx = 1; dz = 0; distance = 1; }
                p.x = rock.x() + dx / distance * separation;
                p.z = rock.z() + dz / distance * separation;
            }
        }
        // Concave clusters can oscillate under projection. Find the nearest free ring
        // around the original position; this fallback only runs on growth, never each tick.
        for (double step = .1; step <= map.halfSize * 3; step += .1) {
            for (int angle = 0; angle < 64; angle++) {
                double theta = angle * Math.PI * 2 / 64;
                double x = originX + Math.cos(theta) * step, z = originZ + Math.sin(theta) * step;
                if (free(x, z, radius)) { p.x = x; p.z = z; return; }
            }
        }
        throw new IllegalStateException("No space for player radius on " + map.id);
    }

    private double[] safeShardDrop(PlayerState carrier, double explosionX, double explosionZ) {
        double dx = carrier.x - explosionX;
        double dz = carrier.z - explosionZ;
        double length = Math.hypot(dx, dz);
        if (length < 0.001) { dx = 1; dz = 0; length = 1; }
        dx /= length;
        dz /= length;
        for (double distance = ShardObjective.CARRIER_DROP_DISTANCE; distance >= 0; distance -= 0.25) {
            double sx = GameRules.clamp(carrier.x + dx * distance, -map.halfSize, map.halfSize);
            double sz = GameRules.clamp(carrier.z + dz * distance, -map.halfSize, map.halfSize);
            if (free(sx, sz, 0.25)) return new double[] {sx, sz};
        }
        return new double[] {carrier.x, carrier.z};
    }

    private void broadcastCoreSpawn(Pickup core) {
        for (PlayerState viewer : players) {
            PlayerState perspective = perspective(viewer);
            if (syncMode == SyncMode.OPTIMIZED
                    && GameRules.distance(perspective.x,perspective.z,core.x,core.z) > SyncMode.PICKUP_VISIBILITY) continue;
            viewer.send(data("type","CORE_SPAWNED","pickupId",core.id,"x",core.x,"z",core.z));
        }
    }

    private void checkGameOver() {
        if (phase != Phase.PLAYING) return;
        List<PlayerState> survivors = players.stream().filter(p -> p.alive).toList();
        if (survivors.size() <= 1) finishRound(survivors.isEmpty() ? null : survivors.get(0), "LAST_STANDING");
    }

    /** Ends the round; in Journey the winner keeps this chapter's shard (+1 roundWins). */
    private void finishRound(PlayerState winner, String reason) {
        if (mode == GameMode.JOURNEY && winner != null) {
            ScoreState score = journeyScores.get(winner.id);
            if (score != null) {
                score.points += ROUND_WIN_POINTS;
                score.roundWins++;
            }
        }
        phase = Phase.FINISHED;
        roundWinnerId = winner == null ? "" : winner.id;
        roundEndReason = reason;
        broadcast(roundResult());
        if (mode == GameMode.JOURNEY && chapterIndex == JOURNEY_MAPS.size() - 1) {
            finalRoundWinnerId = roundWinnerId;
            journeyComplete = true;
            journeyEndReason = "COMPLETED";
            journeyWinnerIds = selectJourneyWinners();
            broadcast(journeyCompleteEvent());
        }
        broadcast(data("type","ROOM_STATE","room",roomInfo()));
    }

    /** Keep reconnect reservations, but never leave an unfillable Journey at ready 1/1. */
    private void completeJourneyIfShort(long now) {
        if (mode != GameMode.JOURNEY || !hasStarted || journeyComplete) return;
        long participants = players.stream()
            .filter(p -> p.connected || now - p.disconnectedAt < GameRules.RECONNECT_MS).count();
        if (participants >= GameRules.MIN_PLAYERS) return;
        if (phase == Phase.PLAYING) return; // checkGameOver resolves the current chapter first.
        journeyComplete = true;
        journeyEndReason = "INSUFFICIENT_PLAYERS";
        journeyWinnerIds = selectJourneyWinners();
        broadcast(journeyCompleteEvent());
        broadcast(data("type","ROOM_STATE","room",roomInfo()));
    }

    synchronized Map<String,Object> roundResult() {
        return data("type","GAME_OVER","winnerId",roundWinnerId,"room",code,"reason",roundEndReason,
            "mode",mode.name(),"roundIndex",roundIndex,"chapterIndex",protocolChapterIndex(),"scores",scores());
    }

    /** Most shards wins; ties go to more eliminations, then to the final chapter's winner. */
    private List<String> selectJourneyWinners() {
        if (journeyScores.isEmpty()) return List.of();
        int bestRoundWins = journeyScores.values().stream().mapToInt(score -> score.roundWins).max().orElse(0);
        List<ScoreState> finalists = journeyScores.values().stream()
            .filter(score -> score.roundWins == bestRoundWins).toList();
        int bestEliminations = finalists.stream().mapToInt(score -> score.eliminations).max().orElse(0);
        finalists = finalists.stream().filter(score -> score.eliminations == bestEliminations).toList();
        if (finalists.size() > 1 && !finalRoundWinnerId.isEmpty()) {
            for (ScoreState score : finalists) {
                if (score.playerId.equals(finalRoundWinnerId)) return List.of(score.playerId);
            }
        }
        return finalists.stream().map(score -> score.playerId).toList();
    }

    Map<String,Object> journeyCompleteEvent() {
        String winnerId = journeyWinnerIds.size() == 1 ? journeyWinnerIds.get(0) : "";
        return data("type","JOURNEY_COMPLETE","room",code,"mode",mode.name(),
            "chapterIndex",protocolChapterIndex(),"winnerId",winnerId,
            "winnerIds",journeyWinnerIds,"scores",scores(),"reason",journeyEndReason);
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

    /** Open spot away from the central pedestal, so the shard has to be carried there. */
    private double[] shardSpawn() {
        double span = (map.halfSize - 4) * 2;
        for (int i = 0; i < 500; i++) {
            double x = -map.halfSize + 4 + random.nextDouble() * span;
            double z = -map.halfSize + 4 + random.nextDouble() * span;
            if (GameRules.distance(x, z, ShardObjective.PEDESTAL_X, ShardObjective.PEDESTAL_Z) >= map.halfSize * .45
                    && freeSpawn(x, z)) return new double[] {x, z};
        }
        return new double[] {map.halfSize * .5, 0};
    }

    private boolean freeSpawn(double x, double z) {
        if (!free(x, z, GameRules.PLAYER_RADIUS)) return false;
        for (GameMap.Bush bush : bushes)
            if (GameRules.distance(x,z,bush.x(),bush.z()) < bush.radius() + 2.0) return false;
        return true;
    }

    /**
     * Farthest-point sampling keeps every randomized start outside initial visibility range.
     *
     * A single greedy pass is not sufficient: an unlucky first point near the middle can
     * make a valid ten-player layout appear impossible. Try several well-spread starting
     * points and only fail after every candidate produces an incomplete layout.
     */
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

        // Boundary points are the best seeds for packing many players. Random tie-breaking
        // still gives different rounds while the retries prevent a flaky start failure.
        candidates.sort((a,b) -> Double.compare(Math.hypot(b[0],b[1]), Math.hypot(a[0],a[1])));
        int attempts = Math.min(96, candidates.size());
        List<double[]> largest = List.of();
        for (int attempt = 0; attempt < attempts; attempt++) {
            int seedIndex = (attempt * Math.max(1, candidates.size() / attempts)) % candidates.size();
            List<double[]> available = new ArrayList<>(candidates);
            List<double[]> result = new ArrayList<>();
            result.add(available.remove(seedIndex));
            while (result.size() < count) {
                int bestIndex = -1;
                double bestDistance = -1;
                for (int i = 0; i < available.size(); i++) {
                    double[] candidate = available.get(i);
                    double nearest = result.stream()
                        .mapToDouble(spawn -> GameRules.distance(candidate[0],candidate[1],spawn[0],spawn[1]))
                        .min().orElse(Double.POSITIVE_INFINITY);
                    if (nearest > bestDistance) { bestDistance = nearest; bestIndex = i; }
                }
                if (bestIndex < 0 || bestDistance < GameRules.MIN_SPAWN_DISTANCE) break;
                result.add(available.remove(bestIndex));
            }
            if (result.size() == count) {
                java.util.Collections.shuffle(result, random);
                return result;
            }
            if (result.size() > largest.size()) largest = result;
        }
        throw new IllegalStateException("Cannot separate " + count + " spawns on map " + map.id
            + " (best layout contained " + largest.size() + ")");
    }

    private void applyMap(GameMap selected) {
        map = selected;
        obstacles = selected.obstacles;
        bushes = selected.bushes;
    }

    private int protocolChapterIndex() {
        return mode == GameMode.JOURNEY ? chapterIndex : -1;
    }

    synchronized List<Map<String,Object>> scores() {
        if (mode != GameMode.JOURNEY) return List.of();
        return journeyScores.values().stream().map(ScoreState::data).toList();
    }

    synchronized Map<String,Object> roomInfo() {
        return data("code",code,"phase",phase.name(),"mapId",map.id,"mapName",map.name,
            "arenaSize",map.halfSize,"minPlayers",GameRules.MIN_PLAYERS,"maxPlayers",GameRules.MAX_PLAYERS,
            "mode",mode.name(),"chapterIndex",protocolChapterIndex(),"scores",scores(),
            "roundIndex",roundIndex,"roundWinnerId",roundWinnerId,"roundEndReason",roundEndReason,
            "winnerIds",journeyWinnerIds,"journeyComplete",journeyComplete,"journeyEndReason",journeyEndReason,"players",players.stream().map(p -> data(
            "id",p.id,"name",p.name,"ready",p.ready,"connected",p.connected,
            "reconnectRemaining",p.connected ? 0 : Math.max(0,p.disconnectedAt + GameRules.RECONNECT_MS - System.currentTimeMillis()) / 1000.0)).toList());
    }

    /** Snapshot without visibility filtering; still uses this room's sync mode. */
    synchronized Map<String,Object> snapshot(long now) { return snapshot(now, null); }

    /** Snapshot for a specific viewer, using the room SyncMode. */
    synchronized Map<String,Object> snapshot(long now, PlayerState viewer) {
        return snapshot(now, viewer, false);
    }

    /**
     * Build a STATE_UPDATE snapshot.
     *
     * @param now           current epoch millis
     * @param viewer        the player who will receive this snapshot (null = spectator / test)
     * @param includeStaticGeometry include obstacles/bushes even in optimized mode;
     *                              visibility and pickup culling remain enforced
     */
    synchronized Map<String,Object> snapshot(long now, PlayerState viewer, boolean includeStaticGeometry) {
        return snapshot(now, viewer, includeStaticGeometry, false);
    }

    /** Fourth argument is only for a controlled baseline comparison in tests. */
    synchronized Map<String,Object> snapshot(long now, PlayerState viewer,
                                             boolean includeStaticGeometry, boolean forceBaselineData) {
        boolean optimized = !forceBaselineData && syncMode == SyncMode.OPTIMIZED;
        PlayerState perspective = perspective(viewer);

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
                "reconnectRemaining",p.connected ? 0 : Math.max(0,p.disconnectedAt + GameRules.RECONNECT_MS - now)/1000.0,
                "visible",visible,
                "charging",p.charging,"charge",p.charging ? GameRules.clamp((now-p.chargeSince)/8000.0,0,1) : 0,
                "chargeCooldown",Math.max(0,p.chargeReadyAt-now)/1000.0,
                "shield",now < p.shieldUntil,"shieldCooldown",Math.max(0,p.shieldReadyAt-now)/1000.0,
                "boost",now < p.boostUntil,"boostCooldown",Math.max(0,p.boostReadyAt-now)/1000.0,
                "stagger",Math.max(0,p.staggerUntil-now)/1000.0,
                "kills",p.kills));
        }

        // ---- pickups: optionally cull by distance ----
        // A returned snapshot must not change when the next tick mutates pickups.
        List<?> visibleOrbs = List.copyOf(orbs);
        List<?> visibleCores = List.copyOf(cores);
        if (optimized && viewer != null) {
            visibleOrbs = orbs.stream()
                .filter(o -> GameRules.distance(perspective.x,perspective.z,o.x,o.z) <= SyncMode.PICKUP_VISIBILITY)
                .toList();
            visibleCores = cores.stream()
                .filter(c -> GameRules.distance(perspective.x,perspective.z,c.x,c.z) <= SyncMode.PICKUP_VISIBILITY)
                .toList();
        }

        Map<String,Object> snap = data("type","STATE_UPDATE","tick",tick,"phase",phase.name(),
            "roomCode",code,"roundIndex",roundIndex,
            "spectatorTargetId",viewer != null && !viewer.alive && perspective != viewer ? perspective.id : "",
            "roundTimeLeft",mode == GameMode.QUICK ? Math.max(0, Math.min(QUICK_ROUND_MS, QUICK_ROUND_MS - (now - roundStartedAt))) / 1000.0 : 0,
            "mapId",map.id,"mapName",map.name,"arenaSize",map.halfSize,
            "mode",mode.name(),"chapterIndex",protocolChapterIndex(),"scores",scores(),
            "players",states,"orbs",visibleOrbs,"cores",visibleCores);

        // In baseline mode (or forced), include static map geometry every tick.
        // In optimized mode the client already has them from GAME_START.
        if (!optimized || includeStaticGeometry) {
            snap.put("obstacles", obstacles);
            snap.put("bushes", bushes);
        }
        // The objective is public information: every viewer gets it, also in optimized mode.
        if (objective != null) snap.put("objective", objective.data(now));
        return snap;
    }

    synchronized boolean canSee(PlayerState viewer, PlayerState target) {
        if (viewer == null || viewer == target) return true;
        viewer = perspective(viewer);
        if (viewer == target) return true;
        if (objective != null && phase == Phase.PLAYING && objective.reveals(target)) return true;
        if (GameRules.distance(viewer.x,viewer.z,target.x,target.z) > GameRules.PLAYER_VISIBILITY_DISTANCE)
            return false;
        int targetBush = bushAt(target.x, target.z);
        return targetBush < 0 || bushAt(viewer.x, viewer.z) == targetBush;
    }

    /** Spectators share a living player's viewpoint, including bush and pickup culling. */
    private PlayerState perspective(PlayerState viewer) {
        if (viewer == null || viewer.alive) return viewer;
        return players.stream().filter(p -> p.alive && p.connected).findFirst()
            .orElseGet(() -> players.stream().filter(p -> p.alive).findFirst().orElse(viewer));
    }

    synchronized boolean keepsDisconnectedSession() {
        return hasStarted;
    }

    synchronized boolean isEmpty() { return players.isEmpty(); }

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
