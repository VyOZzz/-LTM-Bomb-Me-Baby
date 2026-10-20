package bombmebaby;

import java.util.List;
import java.util.Map;

/**
 * Journey chapter objective: one Fire Heart shard spawns after a short delay, a player
 * carries it to the pedestal in the middle of the map, then has to keep the pedestal until
 * the chapter timer runs out. Rules agreed on 20/10/2026:
 * <ul>
 *   <li>the carrier only loses the shard when eliminated (it drops where they fell);</li>
 *   <li>a placed shard changes owner when another player stands alone in the pedestal
 *       ring for {@link #CAPTURE_MS}; the owner standing in the ring pauses the capture;</li>
 *   <li>when the timer ends the pedestal owner, otherwise the carrier, wins the chapter.</li>
 * </ul>
 * Not thread-safe on its own: GameRoom calls it while holding the room monitor.
 */
final class ShardObjective {
    enum State { WAITING, GROUND, CARRIED, PLACED }

    static final long SPAWN_DELAY_MS = 10_000;
    static final long CHAPTER_MS = 180_000;
    static final long CAPTURE_MS = 3_000;
    static final double PEDESTAL_X = 0, PEDESTAL_Z = 0, PEDESTAL_RADIUS = 3.0;
    static final double PICKUP_RANGE = 0.9;

    final long startedAt;
    private final double spawnX, spawnZ;
    State state = State.WAITING;
    double x, z;
    PlayerState carrier, owner, challenger;
    private long captureMs, lastUpdate;

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
        switch (state) {
            case WAITING -> {
                if (now - startedAt >= SPAWN_DELAY_MS) { state = State.GROUND; x = spawnX; z = spawnZ; }
            }
            case GROUND -> {
                PlayerState taker = closestToucher(players);
                if (taker != null) { state = State.CARRIED; carrier = taker; }
            }
            case CARRIED -> {
                x = carrier.x; z = carrier.z;
                if (onPedestal(carrier)) {
                    state = State.PLACED;
                    owner = carrier;
                    carrier = null;
                    x = PEDESTAL_X; z = PEDESTAL_Z;
                }
            }
            case PLACED -> updateCapture(players, dt);
        }
        return now - startedAt >= CHAPTER_MS;
    }

    private void updateCapture(List<PlayerState> players, long dt) {
        boolean ownerPresent = owner != null && owner.alive && onPedestal(owner);
        PlayerState onlyChallenger = null;
        int challengers = 0;
        for (PlayerState p : players) {
            if (p == owner || !p.alive || !p.connected || !onPedestal(p)) continue;
            challengers++;
            onlyChallenger = p;
        }
        if (challengers == 0 || (challenger != null && !(challenger.alive && onPedestal(challenger)))) {
            challenger = null;
            captureMs = 0;
        }
        if (challengers != 1) return;          // empty or contested by several challengers: hold
        if (challenger != onlyChallenger) { challenger = onlyChallenger; captureMs = 0; }
        if (ownerPresent) return;               // owner defending: capture paused, not reset
        captureMs += dt;
        if (captureMs >= CAPTURE_MS) {
            owner = challenger;
            challenger = null;
            captureMs = 0;
        }
    }

    void onEliminated(PlayerState p) {
        if (p == carrier) {
            state = State.GROUND;
            carrier = null;
            x = p.x; z = p.z;
        }
        if (p == owner) owner = null;
        if (p == challenger) { challenger = null; captureMs = 0; }
    }

    /** Player who keeps the shard if the chapter ended right now, or null. */
    PlayerState holder() {
        return state == State.PLACED ? owner : state == State.CARRIED ? carrier : null;
    }

    /** The carrier glows: everyone can see them, even inside a bush or out of range. */
    boolean reveals(PlayerState p) {
        return p != null && p == carrier;
    }

    Map<String,Object> data(long now) {
        return GameRoom.data(
            "state", state.name(), "x", x, "z", z,
            "carrierId", id(carrier), "ownerId", id(owner), "challengerId", id(challenger),
            "capture", Math.min(1.0, captureMs / (double) CAPTURE_MS),
            "pedestalX", PEDESTAL_X, "pedestalZ", PEDESTAL_Z, "pedestalRadius", PEDESTAL_RADIUS,
            "timeLeft", Math.max(0, CHAPTER_MS - (now - startedAt)) / 1000.0,
            "spawnIn", Math.max(0, SPAWN_DELAY_MS - (now - startedAt)) / 1000.0);
    }

    private PlayerState closestToucher(List<PlayerState> players) {
        PlayerState best = null;
        double bestDistance = Double.POSITIVE_INFINITY;
        for (PlayerState p : players) {
            if (!p.alive || !p.connected) continue;
            double d = GameRules.distance(p.x, p.z, x, z);
            if (d < 0.55 * p.size + PICKUP_RANGE && d < bestDistance) { best = p; bestDistance = d; }
        }
        return best;
    }

    private static boolean onPedestal(PlayerState p) {
        return GameRules.distance(p.x, p.z, PEDESTAL_X, PEDESTAL_Z) <= PEDESTAL_RADIUS;
    }

    private static String id(PlayerState p) {
        return p == null ? "" : p.id;
    }
}
