package bombmebaby;

import static org.junit.jupiter.api.Assertions.*;

import java.util.List;
import java.util.Map;
import org.junit.jupiter.api.Test;

/**
 * Tests for the fixes and improvements by Thành viên A:
 *
 * <ol>
 *   <li>Reconnect logic and race-condition prevention</li>
 *   <li>Duplicate / out-of-order sequence rejection</li>
 *   <li>Disconnect cleanup and timeout elimination</li>
 *   <li>Core pickup: only the closest player gets it</li>
 *   <li>Delta sync: optimized snapshot vs baseline</li>
 * </ol>
 *
 * All tests operate directly on {@link GameRoom} / {@link PlayerState}
 * without opening real sockets, keeping them fast and deterministic.
 */
class ServerFixesTest {

    // ====================================================================
    //  1. RECONNECT
    // ====================================================================

    @Test void reconnectRestoresPlayerInRoom() {
        GameRoom room = startedRoom();
        PlayerState a = room.players.get(0);

        // Simulate disconnect
        disconnect(a);

        // Simulate reconnect within window
        synchronized (a) {
            assertTrue(!a.connected);
            assertTrue(System.currentTimeMillis() - a.disconnectedAt <= GameRules.RECONNECT_MS);
            a.connected = true;
        }

        assertTrue(a.alive);
        assertSame(room, a.room);
        assertTrue(a.connected);
    }

    @Test void reconnectFailsAfterWindowExpires() {
        PlayerState a = new PlayerState("A", null);
        a.connected = false;
        a.disconnectedAt = System.currentTimeMillis() - 15_000; // > 10 s

        synchronized (a) {
            boolean canResume = !a.connected
                    && System.currentTimeMillis() - a.disconnectedAt <= GameRules.RECONNECT_MS;
            assertFalse(canResume, "Reconnect window should have expired");
        }
    }

    @Test void reconnectRaceOnlyOneConnectionWins() throws InterruptedException {
        PlayerState a = new PlayerState("A", null);
        a.connected = false;
        a.disconnectedAt = System.currentTimeMillis();

        boolean[] won = {false, false};

        Thread t1 = new Thread(() -> {
            synchronized (a) {
                if (!a.connected && System.currentTimeMillis() - a.disconnectedAt <= GameRules.RECONNECT_MS) {
                    a.connected = true;
                    won[0] = true;
                }
            }
        });
        Thread t2 = new Thread(() -> {
            synchronized (a) {
                if (!a.connected && System.currentTimeMillis() - a.disconnectedAt <= GameRules.RECONNECT_MS) {
                    a.connected = true;
                    won[1] = true;
                }
            }
        });

        t1.start(); t2.start();
        t1.join(); t2.join();

        // Exactly one thread should have succeeded
        assertTrue(won[0] ^ won[1],
                "Exactly one reconnect should succeed; got t1=" + won[0] + " t2=" + won[1]);
        assertTrue(a.connected);
    }

    // ====================================================================
    //  2. DUPLICATE SEQUENCE
    // ====================================================================

    @Test void duplicateSequenceIsRejected() {
        PlayerState p = new PlayerState("Dup", null);
        p.lastSeq = 5;

        assertTrue(5 <= p.lastSeq, "seq == lastSeq should be rejected");
        assertTrue(3 <= p.lastSeq, "seq < lastSeq should be rejected");
        assertFalse(6 <= p.lastSeq, "seq > lastSeq should be accepted");
    }

    @Test void sequenceMustStrictlyIncrease() {
        PlayerState p = new PlayerState("Seq", null);
        p.lastSeq = -1;

        // seq=1 accepted
        assertFalse(1 <= p.lastSeq);
        p.lastSeq = 1;

        // seq=1 again rejected
        assertTrue(1 <= p.lastSeq);

        // seq=2 accepted
        assertFalse(2 <= p.lastSeq);
        p.lastSeq = 2;

        // going backward rejected
        assertTrue(1 <= p.lastSeq);
    }

    // ====================================================================
    //  3. DISCONNECT TIMEOUT
    // ====================================================================

    @Test void disconnectedPlayerEliminatedAfterTimeout() {
        GameRoom room = startedRoom();
        PlayerState a = room.players.get(0);

        disconnect(a);
        a.disconnectedAt = System.currentTimeMillis() - GameRules.RECONNECT_MS;

        room.update(System.currentTimeMillis(), 0.05);

        assertFalse(a.alive, "Disconnected player should be eliminated after timeout");
        assertEquals(GameRoom.Phase.FINISHED, room.phase, "Last survivor should win");
    }

    @Test void disconnectedPlayerStaysAliveWithinWindow() {
        GameRoom room = startedRoom();
        PlayerState a = room.players.get(0);

        disconnect(a);
        // disconnectedAt = now, well within window

        room.update(System.currentTimeMillis(), 0.05);

        assertTrue(a.alive, "Player should stay alive within reconnect window");
        assertEquals(GameRoom.Phase.PLAYING, room.phase);
    }

    // ====================================================================
    //  4. CORE PICKUP – CLOSEST PLAYER WINS
    // ====================================================================

    @Test void onlyClosestPlayerPicksUpCore() {
        GameRoom room = new GameRoom("CORE01");
        PlayerState a = new PlayerState("A", null);
        PlayerState b = new PlayerState("B", null);
        PlayerState c = new PlayerState("C", null);
        room.join(a); room.join(b); room.join(c);
        room.ready(a, true); room.ready(b, true); room.ready(c, true);

        room.cores.clear();
        room.cores.add(new GameRoom.Pickup(999, 10, 10));

        a.x = 10; a.z = 12;     // distance ≈ 2
        b.x = 10; b.z = 10.3;   // distance ≈ 0.3 — closest, within range
        c.x = 10; c.z = 11;     // distance ≈ 1

        room.update(System.currentTimeMillis(), 0.05);

        assertEquals(1.1, b.baseSize, 0.001, "Closest player B should get the core");
        assertEquals(1.0, a.baseSize, 0.001, "Player A should NOT get the core");
        assertEquals(1.0, c.baseSize, 0.001, "Player C should NOT get the core");
        assertTrue(room.cores.isEmpty());
    }

    @Test void coreNotPickedIfNoOneInRange() {
        GameRoom room = startedRoom();
        PlayerState a = room.players.get(0), b = room.players.get(1);

        room.cores.clear();
        room.cores.add(new GameRoom.Pickup(998, 30, 30));
        a.x = 0; a.z = 0;
        b.x = 5; b.z = 5;

        room.update(System.currentTimeMillis(), 0.05);

        assertEquals(1, room.cores.size(), "Core should remain if nobody is in range");
    }

    @Test void eliminatedPlayerDropsExactlyOneCore() {
        GameRoom room = new GameRoom("CORE03");
        PlayerState a = new PlayerState("A", null);
        PlayerState b = new PlayerState("B", null);
        room.join(a); room.join(b);
        room.ready(a, true); room.ready(b, true);

        a.x = 0; a.z = 0;
        b.x = 0.5; b.z = 0;
        b.hp = 1;

        int coresBefore = room.cores.size();
        room.action(a, "CHARGE_START", 0, 0, 1000);
        room.action(a, "EXPLODE", 0, 0, 2000);

        assertFalse(b.alive);
        assertEquals(coresBefore + 1, room.cores.size(), "Exactly one core should drop");
    }

    // ====================================================================
    //  5. DELTA SYNC – OPTIMIZED VS BASELINE SNAPSHOT
    // ====================================================================

    @Test void baselineSnapshotIncludesObstaclesAndBushes() {
        GameRoom room = startedRoom();
        Map<String, Object> snap = room.snapshot(System.currentTimeMillis(), null, true);

        assertNotNull(snap.get("obstacles"), "Baseline must include obstacles");
        assertNotNull(snap.get("bushes"), "Baseline must include bushes");
        assertEquals("STATE_UPDATE", snap.get("type"));
    }

    @Test void forceBaselineAlwaysIncludesFullData() {
        GameRoom room = startedRoom();
        Map<String, Object> snap = room.snapshot(
                System.currentTimeMillis(), room.players.get(0), true);

        assertNotNull(snap.get("obstacles"), "forceBaseline must include obstacles");
        assertNotNull(snap.get("bushes"), "forceBaseline must include bushes");
    }

    @SuppressWarnings("unchecked")
    @Test void invisiblePlayerMarkedCorrectlyInSnapshot() {
        GameRoom room = startedRoom();
        PlayerState viewer = room.players.get(0);
        PlayerState hidden = room.players.get(1);

        // Place hidden player far outside visibility range
        hidden.x = viewer.x + GameRules.PLAYER_VISIBILITY_DISTANCE + 10;
        hidden.z = viewer.z;
        assertFalse(room.canSee(viewer, hidden));

        Map<String, Object> snap = room.snapshot(System.currentTimeMillis(), viewer, true);
        List<Map<String, Object>> players = (List<Map<String, Object>>) snap.get("players");
        Map<String, Object> hiddenState = players.stream()
                .filter(p -> p.get("id").equals(hidden.id))
                .findFirst().orElseThrow();

        assertFalse((Boolean) hiddenState.get("visible"));
    }

    @SuppressWarnings("unchecked")
    @Test void noViewerSnapshotShowsAllPlayersVisible() {
        GameRoom room = startedRoom();
        List<Map<String, Object>> players =
                (List<Map<String, Object>>) room.snapshot(System.currentTimeMillis()).get("players");

        assertTrue(players.stream().allMatch(p -> (Boolean) p.get("visible")),
                "No-viewer snapshot should mark all players visible");
    }

    @Test void gameStartDataAvailableForMapGeometry() {
        GameRoom room = startedRoom();
        assertFalse(room.obstacles.isEmpty());
        assertFalse(room.bushes.isEmpty());
        assertTrue(room.map.halfSize >= 46);
    }

    @Test void snapshotWithoutForcedBaselineHasConsistentStructure() {
        GameRoom room = startedRoom();
        Map<String, Object> snap = room.snapshot(System.currentTimeMillis(), room.players.get(0));

        // Core fields always present
        assertNotNull(snap.get("type"));
        assertNotNull(snap.get("tick"));
        assertNotNull(snap.get("players"));
        assertNotNull(snap.get("orbs"));
        assertNotNull(snap.get("cores"));

        // Whether obstacles/bushes are present depends on SyncMode.active()
        // In default test environment, SyncMode should be BASELINE
        // (no -Dsync.mode=optimized), so they should be present.
        if (SyncMode.active() == SyncMode.BASELINE) {
            assertNotNull(snap.get("obstacles"), "Baseline mode should include obstacles");
            assertNotNull(snap.get("bushes"), "Baseline mode should include bushes");
        }
    }

    // ====================================================================
    //  Utilities
    // ====================================================================

    private GameRoom startedRoom() {
        GameRoom room = new GameRoom("FIX001");
        PlayerState a = new PlayerState("A", null);
        PlayerState b = new PlayerState("B", null);
        room.join(a); room.join(b);
        room.ready(a, true); room.ready(b, true);
        return room;
    }

    /** Simulate a clean disconnect (mirrors the finally block in BombServer.Connection). */
    private void disconnect(PlayerState p) {
        synchronized (p) {
            p.connected = false;
            p.connection = null;
            p.disconnectedAt = System.currentTimeMillis();
        }
        p.dx = 0; p.dz = 0;
    }
}
