package bombmebaby;

import static org.junit.jupiter.api.Assertions.*;
import java.nio.charset.StandardCharsets;
import java.util.List;
import java.util.Map;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;
import org.junit.jupiter.api.Test;

/** Room regressions; production TCP/session behavior is tested in ConnectionTest. */
class ServerFixesTest {
    private GameRoom startedRoom(SyncMode mode) {
        GameRoom room = new GameRoom("FIX001", mode);
        room.map = GameMap.all().get(0);
        room.obstacles = room.map.obstacles;
        room.bushes = room.map.bushes;
        room.random.setSeed(42);
        room.join(new PlayerState("A", null));
        room.join(new PlayerState("B", null));
        for (PlayerState p : room.players) room.ready(p, true);
        room.orbs.clear(); // No random pickups changing collision-test sizes.
        return room;
    }

    @Test void disconnectedPlayerStaysAliveBeforeDeadline() {
        GameRoom room = startedRoom(SyncMode.BASELINE);
        PlayerState a = room.players.get(0);
        a.connected = false; a.disconnectedAt = 1000;
        room.update(1000 + GameRules.RECONNECT_MS - 1, 0);
        assertTrue(a.alive);
        assertEquals(GameRoom.Phase.PLAYING, room.phase);
    }

    @Test void staleMovementStopsWithoutTreatingStandingStillAsDisconnect() {
        GameRoom room = startedRoom(SyncMode.BASELINE);
        PlayerState a = room.players.get(0);
        a.x = 0; a.z = 0;
        room.action(a, "MOVE", 1, 0, 1_000);
        room.update(1_000 + GameRules.MOVEMENT_STALE_MS - 1, .1);
        double movedTo = a.x;
        assertTrue(movedTo > 0);

        room.update(1_000 + GameRules.MOVEMENT_STALE_MS, .1);
        assertEquals(movedTo, a.x, .0001);
        assertEquals(0, a.dx);
        assertTrue(a.connected, "Movement expiry only stops stale input");
    }

    @Test void disconnectAtDeadlineEliminatesAndDropsOnlyOneCore() {
        GameRoom room = startedRoom(SyncMode.BASELINE);
        PlayerState a = room.players.get(0);
        a.connected = false; a.disconnectedAt = 1000;
        room.update(1000 + GameRules.RECONNECT_MS, 0);
        room.update(1001 + GameRules.RECONNECT_MS, 0);
        assertFalse(a.alive);
        assertEquals(1, room.cores.size());
        assertEquals(GameRoom.Phase.FINISHED, room.phase);
    }

    @Test void allExpiredPlayersAreEliminatedInSameTick() {
        GameRoom room = startedRoom(SyncMode.BASELINE);
        for (PlayerState p : room.players) { p.connected = false; p.disconnectedAt = 1000; }
        room.update(1000 + GameRules.RECONNECT_MS, 0);
        assertTrue(room.players.stream().noneMatch(p -> p.alive));
        assertEquals(2, room.cores.size());
        assertEquals(GameRoom.Phase.FINISHED, room.phase);
        room.update(1001 + GameRules.RECONNECT_MS, 0);
        assertTrue(room.players.isEmpty(), "Finished rooms release every expired offline player");
    }

    @Test void closestEligiblePlayerWinsCoreEvenIfListedSecond() {
        GameRoom room = coreRoom();
        room.players.get(0).x = 10.8;
        room.players.get(1).x = 10.2;
        room.update(1000, 0);
        assertEquals(1, room.players.get(0).baseSize);
        assertEquals(1.1, room.players.get(1).baseSize, .0001);
        assertTrue(room.cores.isEmpty());
    }

    @Test void equalDistanceCoreIsAwardedOnceAcrossConcurrentTicks() throws Exception {
        GameRoom room = coreRoom();
        var pool = Executors.newFixedThreadPool(2);
        CountDownLatch start = new CountDownLatch(1);
        try {
            var a = pool.submit(() -> { start.await(); room.update(1000, 0); return null; });
            var b = pool.submit(() -> { start.await(); room.update(1000, 0); return null; });
            start.countDown();
            a.get(5, TimeUnit.SECONDS); b.get(5, TimeUnit.SECONDS);
            assertEquals(1.1, room.players.get(0).baseSize, .0001);
            assertEquals(1, room.players.get(1).baseSize);
            assertTrue(room.cores.isEmpty());
        } finally { pool.shutdownNow(); }
    }

    @Test void disconnectedOrDeadPlayerCannotCollectCore() {
        GameRoom room = new GameRoom("ELIG01", SyncMode.BASELINE);
        for (String name : List.of("Offline", "Dead", "Far away")) room.join(new PlayerState(name, null));
        for (PlayerState p : room.players) room.ready(p, true);
        room.orbs.clear();
        room.cores.add(new GameRoom.Pickup(999, 10, 10));
        for (PlayerState p : room.players) { p.x = 10; p.z = 10; }
        room.players.get(2).x = -10;
        room.players.get(0).connected = false;
        room.players.get(0).disconnectedAt = 1000;
        room.players.get(1).alive = false;
        room.update(1000, 0);
        assertEquals(GameRoom.Phase.PLAYING, room.phase);
        assertEquals(1, room.cores.size());
        assertTrue(room.players.stream().allMatch(p -> p.baseSize == 1));
    }

    @Test void coreOutsidePickupRangeRemains() {
        GameRoom room = coreRoom();
        for (PlayerState p : room.players) p.x = -10;
        room.update(1000, 0);
        assertEquals(1, room.cores.size());
    }

    @Test void baselineKeepsStaticMapAndAllPickups() {
        GameRoom room = startedRoom(SyncMode.BASELINE);
        PlayerState viewer = room.players.get(0);
        room.orbs.add(new GameRoom.Pickup(991, viewer.x + 40, viewer.z));
        var snap = room.snapshot(1000, viewer);
        assertEquals(room.obstacles, snap.get("obstacles"));
        assertEquals(room.bushes, snap.get("bushes"));
        assertEquals(room.orbs, snap.get("orbs"));
        assertEquals(room.players.get(1).x, state(snap, 1).get("x"));
    }

    @Test void optimizedOmitsStaticMapAndCullsPickupsAtThirtyUnits() {
        GameRoom room = startedRoom(SyncMode.OPTIMIZED);
        PlayerState viewer = room.players.get(0);
        viewer.x = viewer.z = 0;
        var boundary = new GameRoom.Pickup(991, 30, 0);
        var outside = new GameRoom.Pickup(992, 30.01, 0);
        room.orbs.addAll(List.of(boundary, outside));
        room.cores.addAll(List.of(boundary, outside));
        var snap = room.snapshot(1000, viewer);
        assertFalse(snap.containsKey("obstacles"));
        assertFalse(snap.containsKey("bushes"));
        assertEquals(List.of(boundary), snap.get("orbs"));
        assertEquals(List.of(boundary), snap.get("cores"));
        assertEquals(2, room.orbs.size(), "Culling must not delete authoritative pickups");
    }

    @Test void optimizedHidesBushOpponentButShowsSelfAndSameBush() {
        GameRoom room = startedRoom(SyncMode.OPTIMIZED);
        PlayerState viewer = room.players.get(0), hidden = room.players.get(1);
        var bush = room.bushes.get(0);
        hidden.x = bush.x(); hidden.z = bush.z();
        viewer.x = hidden.x + 8; viewer.z = hidden.z;
        var snap = room.snapshot(1000, viewer);
        assertEquals(false, state(snap, 1).get("visible"));
        assertEquals(0.0, state(snap, 1).get("x"));
        assertEquals(0.0, state(snap, 1).get("z"));
        assertEquals(viewer.x, state(snap, 0).get("x"));
        viewer.x = hidden.x;
        snap = room.snapshot(1000, viewer);
        assertEquals(true, state(snap, 1).get("visible"));
        assertEquals(hidden.x, state(snap, 1).get("x"));
    }

    @Test void optimizedHidesDistantOpponent() {
        GameRoom room = startedRoom(SyncMode.OPTIMIZED);
        var viewer = room.players.get(0);
        var other = room.players.get(1);
        other.x = viewer.x + 40; other.z = viewer.z;
        var hidden = state(room.snapshot(1000, viewer), 1);
        assertEquals(false, hidden.get("visible"));
        assertEquals(0.0, hidden.get("x"));
        assertEquals(0.0, hidden.get("z"));
    }

    @Test void optimizedRecoveryIncludesMapWithoutRevealingHiddenState() {
        GameRoom room = startedRoom(SyncMode.OPTIMIZED);
        var viewer = room.players.get(0);
        var other = room.players.get(1);
        other.x = viewer.x + 40;
        room.orbs.add(new GameRoom.Pickup(991, viewer.x + 40, viewer.z));
        var snap = room.snapshot(1000, viewer, true);
        assertEquals(room.obstacles, snap.get("obstacles"));
        assertEquals(room.bushes, snap.get("bushes"));
        assertEquals(List.of(), snap.get("orbs"));
        assertEquals(false, state(snap, 1).get("visible"));
        assertEquals(0.0, state(snap, 1).get("x"));
        assertEquals(0.0, state(snap, 1).get("z"));
    }

    @Test void returnedSnapshotDoesNotShareMutablePickupLists() {
        GameRoom room = startedRoom(SyncMode.BASELINE);
        room.orbs.add(new GameRoom.Pickup(991, 0, 0));
        room.cores.add(new GameRoom.Pickup(992, 1, 1));
        var snap = room.snapshot(1000);
        String before = BombServer.JSON.toJson(snap);
        room.orbs.clear(); room.cores.clear();
        assertEquals(before, BombServer.JSON.toJson(snap));
    }

    @Test void optimizedSnapshotUsesFewerUtf8BytesForIdenticalState() {
        GameRoom room = startedRoom(SyncMode.OPTIMIZED);
        PlayerState viewer = room.players.get(0);
        room.orbs.add(new GameRoom.Pickup(991, viewer.x + 40, viewer.z));
        int baseline = bytes(room.snapshot(1000, viewer, true, true));
        int optimized = bytes(room.snapshot(1000, viewer));
        assertTrue(optimized < baseline, "Compare same room, tick, viewer and state");
        System.out.printf("[Snapshot fixture] baseline=%d optimized=%d UTF-8 bytes (JSON only)%n", baseline, optimized);
    }

    private int bytes(Object value) { return BombServer.JSON.toJson(value).getBytes(StandardCharsets.UTF_8).length; }

    @SuppressWarnings("unchecked")
    private Map<String,Object> state(Map<String,Object> snap, int index) {
        return ((List<Map<String,Object>>) snap.get("players")).get(index);
    }

    private GameRoom coreRoom() {
        GameRoom room = startedRoom(SyncMode.BASELINE);
        room.cores.add(new GameRoom.Pickup(999, 10, 10));
        for (PlayerState p : room.players) { p.x = 10; p.z = 10; }
        return room;
    }
}
