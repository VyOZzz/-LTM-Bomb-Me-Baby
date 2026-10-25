package bombmebaby;

import java.util.ArrayList;
import java.util.List;
import java.util.Map;

/** Server-authoritative Fire Heart objective for one Journey chapter. */
final class ShardObjective {
    enum State { WAITING, GROUND, CARRIED, PLACED }

    static final long SPAWN_DELAY_MS = 10_000;
    static final long MATCH_MS = 360_000;
    static final long CHAPTER_MS = MATCH_MS; // Compatibility alias for tools/tests.
    static final long CLAIM_MS = 30_000;
    static final long STEAL_INTERACTION_MS = 3_000;
    static final long SEAL_BREAK_MS = STEAL_INTERACTION_MS;
    static final long CAPTURE_MS = STEAL_INTERACTION_MS;
    static final long PEDESTAL_PROTECTION_MS = 0;
    static final long PEDESTAL_OVERHEAT_MS = 4_000;
    static final double FIRE_LOCK_MAX = 40;
    static final long FIRE_LOCK_REGEN_DELAY_MS = 3_000;
    static final double FIRE_LOCK_REGEN_PER_SECOND = 10;
    static final long GROUND_NEUTRAL_MS = 500;
    static final long FORMER_CARRIER_LOCKOUT_MS = 1_300;
    static final long BREAKER_PICKUP_PRIORITY_MS = 750;
    static final long CARRIER_DISCONNECT_DROP_MS = 2_000;
    static final long RESET_FLASH_MS = 450;
    static final double CARRIER_DROP_DISTANCE = 7.0;
    static final double PEDESTAL_DROP_DISTANCE = 3.45;
    static final double OWNER_DEFENSE_SPEED_FACTOR = 1.0;
    static final double PEDESTAL_X = 0, PEDESTAL_Z = 0, PEDESTAL_RADIUS = 3.0;
    static final double PICKUP_RANGE = 0.9;

    final long startedAt;
    private final double spawnX, spawnZ;
    State state = State.WAITING;
    double x, z;
    PlayerState carrier, owner;
    /** The sole player currently holding Interact to steal the placed shard. */
    PlayerState challenger;
    double fireLock = FIRE_LOCK_MAX;
    long pedestalProtectionUntil, pedestalOverheatUntil;
    PlayerState pickupPriority, formerCarrier;
    long pickupPriorityUntil, pickupUnlockAt, formerCarrierBlockedUntil;
    private long fireLockLastHitAt;
    private long lastUpdate;
    private long claimEndsAt;
    private long stealStartedAt;
    private double stealProgressMs;
    private PlayerState securedWinner;

    ShardObjective(long startedAt, double spawnX, double spawnZ) {
        this.startedAt = startedAt;
        this.lastUpdate = startedAt;
        this.spawnX = spawnX;
        this.spawnZ = spawnZ;
    }

    /** Advances the objective; returns true once this chapter's configured timer has run out. */
    boolean update(long now, List<PlayerState> players) {
        long previousUpdate = lastUpdate;
        long dt = Math.min(100, Math.max(0, now - previousUpdate));
        lastUpdate = now;
        clearInvalidPriority(now);
        regenerateFireLock(previousUpdate, now);
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
                if (taker != null) pickUp(taker);
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
            case PLACED -> {
                if (owner != null && now >= claimEndsAt) {
                    securedWinner = owner;
                    cancelInteraction(null);
                } else updateSteal(now, dt);
            }
        }
        return now - startedAt >= MATCH_MS;
    }

    PlayerState securedWinner() { return securedWinner; }

    /** Starts the only allowed steal interaction. The server verifies all preconditions. */
    boolean beginSteal(PlayerState player, long now) {
        if (state != State.PLACED || player == null || player == owner || !player.alive
                || !player.connected || !onPedestal(player) || isMoving(player)
                || now < pedestalProtectionUntil || challenger != null && challenger != player) return false;
        if (challenger == player) return true;
        challenger = player;
        stealStartedAt = now;
        stealProgressMs = 0;
        return true;
    }

    /** Cancels on key release or a voluntary movement/combat action; null cancels whoever is active. */
    boolean cancelInteraction(PlayerState player) {
        if (challenger == null || player != null && challenger != player) return false;
        challenger = null;
        stealStartedAt = 0;
        stealProgressMs = 0;
        return true;
    }

    boolean isInteracting(PlayerState player) { return challenger == player; }

    private void updateSteal(long now, long dt) {
        if (challenger == null) return;
        if (!challenger.alive || !challenger.connected) {
            cancelInteraction(challenger);
            return;
        }
        // Damage and server knockback deliberately do not cancel or reset this hold.
        stealProgressMs += Math.min(dt, Math.max(0, now - stealStartedAt));
        if (stealProgressMs >= STEAL_INTERACTION_MS) completeSteal(challenger, now);
    }

    /** Applies objective-specific consequences of an explosion that dealt real damage. */
    boolean onDamaged(PlayerState target, double actualDamage,
                      long now, double safeDropX, double safeDropZ) {
        if (state != State.CARRIED || target != carrier || actualDamage <= 0) return false;
        fireLock = Math.max(0, fireLock - actualDamage);
        fireLockLastHitAt = now;
        if (fireLock <= 0) dropAt(safeDropX, safeDropZ, now);
        return false;
    }

    private void place(long now) {
        owner = carrier;
        carrier = null;
        state = State.PLACED;
        x = PEDESTAL_X;
        z = PEDESTAL_Z;
        pedestalProtectionUntil = now;
        claimEndsAt = now + CLAIM_MS;
        challenger = null;
        stealProgressMs = 0;
        fireLock = FIRE_LOCK_MAX;
    }

    /** A completed hold transfers the shard directly so the thief can run immediately. */
    private void completeSteal(PlayerState winner, long now) {
        state = State.CARRIED;
        carrier = winner;
        owner = null;
        challenger = null;
        stealProgressMs = 0;
        claimEndsAt = 0;
        pedestalOverheatUntil = now + PEDESTAL_OVERHEAT_MS;
        fireLock = FIRE_LOCK_MAX;
        fireLockLastHitAt = 0;
        winner.charging = false;
    }

    private void pickUp(PlayerState taker) {
        state = State.CARRIED;
        carrier = taker;
        taker.charging = false;
        owner = null;
        challenger = null;
        stealProgressMs = 0;
        claimEndsAt = 0;
        fireLock = FIRE_LOCK_MAX;
        fireLockLastHitAt = 0;
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
        challenger = null;
        stealProgressMs = 0;
        claimEndsAt = 0;
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

    boolean drop(PlayerState player, long now) {
        if (state != State.CARRIED || carrier != player) return false;
        dropAt(player.x, player.z, now);
        return true;
    }

    boolean carriedBy(PlayerState player) { return state == State.CARRIED && carrier == player; }

    private void regenerateFireLock(long previousUpdate, long now) {
        if (state != State.CARRIED || fireLock >= FIRE_LOCK_MAX) return;
        long regenFrom = Math.max(previousUpdate, fireLockLastHitAt + FIRE_LOCK_REGEN_DELAY_MS);
        long regenMs = now - regenFrom;
        if (regenMs > 0)
            fireLock = Math.min(FIRE_LOCK_MAX, fireLock + regenMs * FIRE_LOCK_REGEN_PER_SECOND / 1000.0);
    }

    private void clearInvalidPriority(long now) {
        if (pickupPriority != null && (!pickupPriority.alive || !pickupPriority.connected
                || now >= pickupPriorityUntil)) {
            pickupPriority = null;
            pickupPriorityUntil = 0;
        }
    }

    void onEliminated(PlayerState player, long now) {
        if (player == carrier) dropAt(player.x, player.z, now);
        if (player == challenger) cancelInteraction(player);
        if (player == owner && state == State.PLACED) {
            state = State.GROUND;
            owner = null;
            claimEndsAt = 0;
            x = PEDESTAL_X;
            z = PEDESTAL_Z;
            pickupUnlockAt = now + GROUND_NEUTRAL_MS;
        }
        if (player == pickupPriority) {
            pickupPriority = null;
            pickupPriorityUntil = 0;
        }
    }

    boolean reveals(PlayerState player) { return player != null && player == carrier; }

    Map<String,Object> data(long now) {
        List<Map<String,Object>> breakers = new ArrayList<>();
        if (challenger != null) breakers.add(GameRoom.data(
            "playerId", challenger.id,
            "progress", Math.min(1.0, stealProgressMs / STEAL_INTERACTION_MS),
            "stagger", 0,
            "resetFlash", 0));
        double claimLeft = state == State.PLACED ? remaining(claimEndsAt, now) : 0;
        return GameRoom.data(
            "state", state.name(), "x", x, "z", z,
            "carrierId", id(carrier), "ownerId", id(owner), "challengerId", id(challenger),
            "capture", Math.min(1.0, stealProgressMs / STEAL_INTERACTION_MS), "sealBreakers", breakers,
            "claimTimeLeft", claimLeft, "claimDuration", CLAIM_MS / 1000.0,
            "claimProgress", state == State.PLACED ? 1 - claimLeft / (CLAIM_MS / 1000.0) : 0,
            "fireLock", fireLock, "fireLockMax", FIRE_LOCK_MAX,
            "pedestalProtection", remaining(pedestalProtectionUntil, now),
            "pedestalOverheat", remaining(pedestalOverheatUntil, now),
            "pickupPriorityId", id(pickupPriority),
            "pickupPriority", remaining(pickupPriorityUntil, now),
            "pickupUnlock", remaining(pickupUnlockAt, now),
            "pedestalX", PEDESTAL_X, "pedestalZ", PEDESTAL_Z, "pedestalRadius", PEDESTAL_RADIUS,
            "timeLeft", Math.max(0, MATCH_MS - (now - startedAt)) / 1000.0,
            "spawnIn", Math.max(0, SPAWN_DELAY_MS - (now - startedAt)) / 1000.0);
    }

    private PlayerState closestToucher(List<PlayerState> players, long now) {
        if (now < pickupUnlockAt) return null;
        clearInvalidPriority(now);
        PlayerState best = null;
        double bestDistance = Double.POSITIVE_INFINITY;
        for (PlayerState player : players) {
            if (!player.alive || !player.connected) continue;
            if (pickupPriority != null && player != pickupPriority) continue;
            if (player == formerCarrier && now < formerCarrierBlockedUntil) continue;
            double distance = GameRules.distance(player.x, player.z, x, z);
            if (distance < 0.55 * player.size + PICKUP_RANGE && distance < bestDistance) {
                best = player;
                bestDistance = distance;
            }
        }
        return best;
    }

    static boolean onPedestal(PlayerState player) {
        return GameRules.distance(player.x, player.z, PEDESTAL_X, PEDESTAL_Z) <= PEDESTAL_RADIUS;
    }

    private static boolean isMoving(PlayerState player) {
        return Math.abs(player.dx) > 1e-6 || Math.abs(player.dz) > 1e-6;
    }

    private static double remaining(long until, long now) {
        return Math.max(0, until - now) / 1000.0;
    }

    private static String id(PlayerState player) { return player == null ? "" : player.id; }
}
