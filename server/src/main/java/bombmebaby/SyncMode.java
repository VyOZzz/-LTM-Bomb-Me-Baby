package bombmebaby;

/**
 * Controls how STATE_UPDATE snapshots are built.
 *
 * <p><b>BASELINE</b> — the original prototype behaviour: every snapshot
 * includes the full list of obstacles, bushes, all players (with x/z even
 * when invisible), and every orb/core on the map.  This is the reference
 * point for the network experiment.</p>
 *
 * <p><b>OPTIMIZED</b> — selective synchronization for regular snapshots
 * (not field-by-field delta encoding):
 * <ul>
 *   <li>obstacles and bushes are sent once in GAME_START, omitted from
 *       STATE_UPDATE;</li>
 *   <li>invisible players have their x/z zeroed out so the client cannot
 *       derive those positions from regular snapshots;</li>
 *   <li>orbs and cores beyond the viewer's visibility distance are
 *       excluded from the snapshot.</li>
 * </ul></p>
 * Recovery currently forces a baseline snapshot; coordinate-bearing events
 * also remain unchanged. This mode is not a comprehensive anti-cheat boundary.
 *
 * The active mode is selected via the system property {@code sync.mode}:
 * <pre>
 *   mvn exec:java -Dexec.args="7777" -Dsync.mode=optimized
 *   mvn exec:java -Dexec.args="7777" -Dsync.mode=baseline   # default
 * </pre>
 */
enum SyncMode {
    BASELINE,
    OPTIMIZED;

    /** Distance from a viewer beyond which orbs/cores are culled (optimized mode). */
    static final double PICKUP_VISIBILITY = 30.0;

    private static final SyncMode ACTIVE;
    static {
        String prop = System.getProperty("sync.mode", "baseline");
        ACTIVE = "optimized".equalsIgnoreCase(prop) ? OPTIMIZED : BASELINE;
        System.out.println("[SyncMode] active = " + ACTIVE);
    }

    static SyncMode active() { return ACTIVE; }
}
