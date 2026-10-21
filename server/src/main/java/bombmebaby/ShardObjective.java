package bombmebaby;

import java.util.ArrayList;
import java.util.Iterator;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/** Server-authoritative Fire Heart objective for the current Journey chapter. */
final class ShardObjective {
    enum State { WAITING, GROUND, CARRIED, PLACED }

    static final long SPAWN_DELAY_MS = 10_000;
    static final long CHAPTER_MS = 180_000;
    static final long SEAL_BREAK_MS = 3_000;
    /** Compatibility alias retained for older tests/tools. */
    static final long CAPTURE_MS = SEAL_BREAK_MS;
    static final long PEDESTAL_PROTECTION_MS = 5_000;
    static final long PEDESTAL_OVERHEAT_MS = 4_000;
    static final long CARRIER_PROTECTION_MS = 2_000;
    static final long FIRE_LOCK_RECOVERY_MS = 6_000;
    static final long GROUND_NEUTRAL_MS = 500;
    static final long FORMER_CARRIER_LOCKOUT_MS = 1_300;
    static final long BREAKER_PICKUP_PRIORITY_MS = 750;
    static final long CARRIER_DISCONNECT_DROP_MS = 2_000;
    static final long RESET_FLASH_MS = 450;
    static final double MIN_LOCK_BREAK_CHARGE = 0.35;
    static final double MIN_LOCK_BREAK_DAMAGE = 10.0;
    static final int FIRE_LOCK_MAX = 2;
    static final double CARRIER_DROP_DISTANCE = 2.5;
    static final double PEDESTAL_DROP_DISTANCE = 3.45;
    static final double OWNER_DEFENSE_SPEED_FACTOR = 0.5;
    static final double PEDESTAL_X = 0, PEDESTAL_Z = 0, PEDESTAL_RADIUS = 3.0;
    static final double PICKUP_RANGE = 0.9;

    final long startedAt;
    private final double spawnX, spawnZ;
    State state = State.WAITING;
    double x, z;
    PlayerState carrier, owner;
    /** Leading breaker, retained for the compact/legacy DTO fields. */
    PlayerState challenger;
    int fireLock = FIRE_LOCK_MAX;
    long carrierProtectionUntil, pedestalProtectionUntil, pedestalOverheatUntil;
    PlayerState pickupPriority, formerCarrier;
    long pickupPriorityUntil, pickupUnlockAt, formerCarrierBlockedUntil;
    private long fireLockLastHitAt;
    private long lastUpdate;
    private final Map<PlayerState, Double> sealProgressMs = new LinkedHashMap<>();
    private final Map<PlayerState, Long> resetFlashUntil = new LinkedHashMap<>();

    ShardObjective(long startedAt, double spawnX, double spawnZ) {
        this.startedAt = startedAt;
        this.lastUpdate = startedAt;
        this.spawnX = spawnX;
        this.spawnZ = spawnZ;
    }

    /** Advances the objective; returns true once the chapter timer has run out. */
    boolean update(long now, List<PlayerState> players) {
        long dt = Math.max(0, now - lastUpdate);
        lastUpdate = now;
        clearInvalidPriority(now);
        recoverFireLock(now);
        switch (state) {
            case WAITING -> {
                if (now - startedAt >= SPAWN_DELAY_MS) {
                    state = State.GROUND;
                    x = spawnX;
                    z = spawnZ;
                    pickupUnlockAt = now;
                }
            }
            case GROUND -> {
                PlayerState taker = closestToucher(players, now);
                if (taker != null) pickUp(taker, now);
            }
            case CARRIED -> {
                if (carrier == null) break;
                x = carrier.x;
                z = carrier.z;
                if (!carrier.alive) dropAt(carrier.x, carrier.z, now);
                else if (!carrier.connected && now - carrier.disconnectedAt >= CARRIER_DISCONNECT_DROP_MS)
                    dropAt(carrier.x, carrier.z, now);
                else if (carrier.connected && onPedestal(carrier) && now >= pedestalOverheatUntil)
                    place(now);
            }
            case PLACED -> updateSealBreakers(players, now, dt);
        }
        return now - startedAt >= CHAPTER_MS;
    }

    /** Applies objective-specific consequences of an explosion that dealt real damage. */
    boolean onDamaged(PlayerState target, double charge, double actualDamage,
                      long now, double safeDropX, double safeDropZ) {
        boolean reset = false;
        if (state == State.PLACED && sealProgressMs.containsKey(target)) {
            sealProgressMs.put(target, 0.0);
            resetFlashUntil.put(target, now + RESET_FLASH_MS);
            reset = true;
        }
        if (state != State.CARRIED || target != carrier || now < carrierProtectionUntil
                || charge < MIN_LOCK_BREAK_CHARGE || actualDamage < MIN_LOCK_BREAK_DAMAGE) return reset;
        recoverFireLock(now);
        fireLock--;
        fireLockLastHitAt = now;
        if (fireLock <= 0) dropAt(safeDropX, safeDropZ, now);
        return reset;
    }

    private void updateSealBreakers(List<PlayerState> players, long now, long dt) {
        boolean ownerPresent = owner != null && owner.alive && owner.connected && onPedestal(owner);
        long activeDt = Math.min(dt, Math.max(0, now - pedestalProtectionUntil));
        Iterator<Map.Entry<PlayerState, Double>> iterator = sealProgressMs.entrySet().iterator();
        while (iterator.hasNext()) {
            PlayerState p = iterator.next().getKey();
            if (!p.alive || !p.connected || !onPedestal(p) || p == owner) {
                iterator.remove();
                resetFlashUntil.remove(p);
            }
        }
        if (now < pedestalProtectionUntil) {
            sealProgressMs.clear();
            challenger = null;
            return;
        }
        for (PlayerState p : players) {
            if (p == owner || !p.alive || !p.connected || !onPedestal(p)) continue;
            double progress = sealProgressMs.getOrDefault(p, 0.0);
            long playerDt = Math.min(activeDt, Math.max(0, now - p.staggerUntil));
            if (playerDt > 0)
                progress += playerDt * (ownerPresent ? OWNER_DEFENSE_SPEED_FACTOR : 1.0);
            sealProgressMs.put(p, progress);
        }
        chooseLeader();
        PlayerState winner = null;
        double best = -1;
        for (Map.Entry<PlayerState, Double> entry : sealProgressMs.entrySet()) {
            if (entry.getValue() >= SEAL_BREAK_MS && entry.getValue() > best) {
                winner = entry.getKey();
                best = entry.getValue();
            }
        }
        if (winner != null) breakSeal(winner, now);
    }

    private void chooseLeader() {
        challenger = null;
        double lead = -1;
        for (Map.Entry<PlayerState, Double> entry : sealProgressMs.entrySet()) {
            if (entry.getValue() > lead) {
                challenger = entry.getKey();
                lead = entry.getValue();
            }
        }
    }

    private void place(long now) {
        owner = carrier;
        carrier = null;
        state = State.PLACED;
        x = PEDESTAL_X;
        z = PEDESTAL_Z;
        pedestalProtectionUntil = now + PEDESTAL_PROTECTION_MS;
        sealProgressMs.clear();
        resetFlashUntil.clear();
        challenger = null;
        fireLock = FIRE_LOCK_MAX;
    }

    private void breakSeal(PlayerState winner, long now) {
        double dx = winner.x - PEDESTAL_X;
        double dz = winner.z - PEDESTAL_Z;
        double length = Math.hypot(dx, dz);
        if (length < 0.001) { dx = 1; dz = 0; length = 1; }
        state = State.GROUND;
        x = PEDESTAL_X + dx / length * PEDESTAL_DROP_DISTANCE;
        z = PEDESTAL_Z + dz / length * PEDESTAL_DROP_DISTANCE;
        carrier = null;
        owner = null;
        challenger = null;
        sealProgressMs.clear();
        resetFlashUntil.clear();
        pedestalOverheatUntil = now + PEDESTAL_OVERHEAT_MS;
        pickupPriority = winner;
        pickupPriorityUntil = now + BREAKER_PICKUP_PRIORITY_MS;
        pickupUnlockAt = now;
    }

    private void pickUp(PlayerState taker, long now) {
        state = State.CARRIED;
        carrier = taker;
        // Carrying the objective replaces bomb combat. Cancel a charge that may
        // have started just before the pickup was resolved by the game tick.
        taker.charging = false;
        owner = null;
        fireLock = FIRE_LOCK_MAX;
        fireLockLastHitAt = 0;
        carrierProtectionUntil = now + CARRIER_PROTECTION_MS;
        pickupPriority = null;
        pickupPriorityUntil = 0;
        formerCarrier = null;
        formerCarrierBlockedUntil = 0;
    }

    private void dropAt(double dropX, double dropZ, long now) {
        PlayerState oldCarrier = carrier;
        state = State.GROUND;
        carrier = null;
        owner = null;
        x = dropX;
        z = dropZ;
        fireLock = FIRE_LOCK_MAX;
        fireLockLastHitAt = 0;
        pickupUnlockAt = now + GROUND_NEUTRAL_MS;
        pickupPriority = null;
        pickupPriorityUntil = 0;
        formerCarrier = oldCarrier;
        formerCarrierBlockedUntil = now + FORMER_CARRIER_LOCKOUT_MS;
    }

    /** Drops the shard voluntarily. Returns false when the player is not its carrier. */
    boolean drop(PlayerState player, long now) {
        if (state != State.CARRIED || carrier != player) return false;
        dropAt(player.x, player.z, now);
        return true;
    }

    boolean carriedBy(PlayerState player) {
        return state == State.CARRIED && carrier == player;
    }

    private void recoverFireLock(long now) {
        if (state == State.CARRIED && fireLock < FIRE_LOCK_MAX && fireLockLastHitAt > 0
                && now - fireLockLastHitAt >= FIRE_LOCK_RECOVERY_MS) {
            fireLock = FIRE_LOCK_MAX;
            fireLockLastHitAt = 0;
        }
    }

    private void clearInvalidPriority(long now) {
        if (pickupPriority != null && (!pickupPriority.alive || !pickupPriority.connected
                || now >= pickupPriorityUntil)) {
            pickupPriority = null;
            pickupPriorityUntil = 0;
        }
    }

    void onEliminated(PlayerState p, long now) {
        if (p == carrier) dropAt(p.x, p.z, now);
        if (p == owner) owner = null;
        sealProgressMs.remove(p);
        resetFlashUntil.remove(p);
        if (p == challenger) chooseLeader();
        if (p == pickupPriority) {
            pickupPriority = null;
            pickupPriorityUntil = 0;
        }
    }

    /** Player who keeps the current chapter shard if the timer ended now. */
    PlayerState holder() {
        return state == State.PLACED ? owner : state == State.CARRIED ? carrier : null;
    }

    boolean reveals(PlayerState p) { return p != null && p == carrier; }

    Map<String,Object> data(long now) {
        List<Map<String,Object>> breakers = new ArrayList<>();
        for (Map.Entry<PlayerState, Double> entry : sealProgressMs.entrySet()) {
            PlayerState p = entry.getKey();
            breakers.add(GameRoom.data(
                "playerId", p.id,
                "progress", Math.min(1.0, entry.getValue() / SEAL_BREAK_MS),
                "stagger", Math.max(0, p.staggerUntil - now) / 1000.0,
                "resetFlash", Math.max(0, resetFlashUntil.getOrDefault(p, 0L) - now) / 1000.0));
        }
        double lead = challenger == null ? 0 : sealProgressMs.getOrDefault(challenger, 0.0);
        return GameRoom.data(
            "state", state.name(), "x", x, "z", z,
            "carrierId", id(carrier), "ownerId", id(owner), "challengerId", id(challenger),
            "capture", Math.min(1.0, lead / SEAL_BREAK_MS), "sealBreakers", breakers,
            "fireLock", fireLock, "fireLockMax", FIRE_LOCK_MAX,
            "carrierProtection", remaining(carrierProtectionUntil, now),
            "pedestalProtection", remaining(pedestalProtectionUntil, now),
            "pedestalOverheat", remaining(pedestalOverheatUntil, now),
            "pickupPriorityId", id(pickupPriority),
            "pickupPriority", remaining(pickupPriorityUntil, now),
            "pickupUnlock", remaining(pickupUnlockAt, now),
            "pedestalX", PEDESTAL_X, "pedestalZ", PEDESTAL_Z, "pedestalRadius", PEDESTAL_RADIUS,
            "timeLeft", Math.max(0, CHAPTER_MS - (now - startedAt)) / 1000.0,
            "spawnIn", Math.max(0, SPAWN_DELAY_MS - (now - startedAt)) / 1000.0);
    }

    private PlayerState closestToucher(List<PlayerState> players, long now) {
        if (now < pickupUnlockAt) return null;
        clearInvalidPriority(now);
        PlayerState best = null;
        double bestDistance = Double.POSITIVE_INFINITY;
        for (PlayerState p : players) {
            if (!p.alive || !p.connected) continue;
            if (pickupPriority != null && p != pickupPriority) continue;
            if (p == formerCarrier && now < formerCarrierBlockedUntil) continue;
            double distance = GameRules.distance(p.x, p.z, x, z);
            if (distance < 0.55 * p.size + PICKUP_RANGE && distance < bestDistance) {
                best = p;
                bestDistance = distance;
            }
        }
        return best;
    }

    static boolean onPedestal(PlayerState p) {
        return GameRules.distance(p.x, p.z, PEDESTAL_X, PEDESTAL_Z) <= PEDESTAL_RADIUS;
    }

    private static double remaining(long until, long now) {
        return Math.max(0, until - now) / 1000.0;
    }

    private static String id(PlayerState p) { return p == null ? "" : p.id; }
}
