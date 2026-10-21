package bombmebaby;

import static org.junit.jupiter.api.Assertions.*;

import java.util.Map;
import org.junit.jupiter.api.Test;

class ShardObjectiveTest {
    @Test void shardSpawnsAwayFromPedestalAndIsCarriedThenPlaced() {
        GameRoom room = journeyRoom();
        PlayerState a = room.players.get(0), b = room.players.get(1);
        ShardObjective shard = room.objective;
        long t0 = shard.startedAt;
        park(b, 40, 40);

        room.update(t0 + ShardObjective.SPAWN_DELAY_MS - 1, 0);
        assertEquals(ShardObjective.State.WAITING, shard.state);
        room.update(t0 + ShardObjective.SPAWN_DELAY_MS, 0);
        assertEquals(ShardObjective.State.GROUND, shard.state);
        assertTrue(Math.hypot(shard.x, shard.z) >= room.map.halfSize * .45,
            "The shard must spawn away from the central pedestal");

        park(a, shard.x, shard.z);
        room.update(t0 + 10_100, 0);
        assertEquals(ShardObjective.State.CARRIED, shard.state);
        assertSame(a, shard.carrier);

        park(a, 1, 0);
        room.update(t0 + 10_200, 0);
        assertEquals(ShardObjective.State.PLACED, shard.state);
        assertSame(a, shard.owner);
        assertNull(shard.carrier);
    }

    @Test void ownerInRingOnlySlowsIndependentSealBreaking() {
        GameRoom room = journeyRoom();
        PlayerState a = room.players.get(0), b = room.players.get(1);
        long t = placeShard(room, a);

        park(a, -1, 0);
        park(b, 1, 0);
        room.update(t + ShardObjective.PEDESTAL_PROTECTION_MS, 0);
        assertSame(b, room.objective.challenger);
        room.update(t + ShardObjective.PEDESTAL_PROTECTION_MS + 3_000, 0);
        assertEquals(ShardObjective.State.PLACED, room.objective.state,
            "Owner presence slows seal breaking instead of pausing it");
        room.update(t + ShardObjective.PEDESTAL_PROTECTION_MS + 6_000, 0);
        assertEquals(ShardObjective.State.GROUND, room.objective.state);
        assertSame(b, room.objective.pickupPriority);
    }

    @Test void leavingTheRingResetsTheCapture() {
        GameRoom room = journeyRoom();
        PlayerState a = room.players.get(0), b = room.players.get(1);
        long t = placeShard(room, a);
        park(a, 20, 0);

        park(b, 1, 0);
        room.update(t, 0);
        room.update(t + 2_500, 0);
        park(b, 10, 0);
        room.update(t + 2_600, 0);
        park(b, 1, 0);
        room.update(t + 2_700, 0);
        room.update(t + 4_000, 0);
        assertSame(a, room.objective.owner, "Capture time must restart after leaving the ring");
    }

    @Test void carrierOnlyDropsTheShardWhenEliminated() {
        GameRoom room = journeyRoom();
        PlayerState a = room.players.get(0), b = room.players.get(1), c = room.players.get(2);
        ShardObjective shard = room.objective;
        long t0 = shard.startedAt;
        park(c, 40, -40);
        room.update(t0 + ShardObjective.SPAWN_DELAY_MS, 0);
        park(b, 40, 40);
        park(a, shard.x, shard.z);
        room.update(t0 + 10_100, 0);
        assertSame(a, shard.carrier);

        park(a, 20, 20);
        park(b, 20.5, 20);
        room.action(b, "CHARGE_START", 0, 0, t0 + 11_000);
        room.action(b, "EXPLODE", 0, 0, t0 + 11_100);
        assertTrue(a.alive, "Being hit without dying keeps the shard");
        assertSame(a, shard.carrier);

        a.hp = 1;
        room.action(b, "CHARGE_START", 0, 0, t0 + 14_000);
        room.action(b, "EXPLODE", 0, 0, t0 + 14_100);
        assertFalse(a.alive);
        assertEquals(ShardObjective.State.GROUND, shard.state);
        assertEquals(20, shard.x, 1e-9);
        assertEquals(20, shard.z, 1e-9);
    }

    @Test void pedestalOwnerWinsTheChapterWhenTimeRunsOut() {
        GameRoom room = journeyRoom();
        PlayerState a = room.players.get(0);
        placeShard(room, a);
        park(a, 25, 25);

        room.update(room.objective.startedAt + ShardObjective.CHAPTER_MS, 0);
        assertEquals(GameRoom.Phase.FINISHED, room.phase);
        Map<String,Object> score = room.scores().stream()
            .filter(item -> item.get("playerId").equals(a.id)).findFirst().orElseThrow();
        assertEquals(1, score.get("roundWins"));
    }

    @Test void carrierIsVisibleToEveryoneAndObjectiveIsInEverySnapshot() {
        GameRoom room = journeyRoom();
        PlayerState a = room.players.get(0), b = room.players.get(1);
        ShardObjective shard = room.objective;
        room.update(shard.startedAt + ShardObjective.SPAWN_DELAY_MS, 0);
        // Opposite side of the map: at least 2 × 0.45 × halfSize away, beyond visibility range.
        park(b, -shard.x, -shard.z);
        park(a, shard.x, shard.z);
        assertFalse(room.canSee(b, a), "Far apart players do not see each other");
        room.update(shard.startedAt + 10_100, 0);
        assertTrue(room.canSee(b, a), "The glowing carrier is visible from anywhere");

        Map<String,Object> snapshot = room.snapshot(shard.startedAt + 10_100, b);
        @SuppressWarnings("unchecked")
        Map<String,Object> objective = (Map<String,Object>) snapshot.get("objective");
        assertEquals("CARRIED", objective.get("state"));
        assertEquals(a.id, objective.get("carrierId"));
    }

    @Test void quickModeHasNoObjective() {
        GameRoom room = new GameRoom("QUICK9", SyncMode.OPTIMIZED, GameMode.QUICK);
        PlayerState a = new PlayerState("A", null), b = new PlayerState("B", null);
        room.join(a); room.join(b);
        room.ready(a, true); room.ready(b, true);
        assertNull(room.objective);
        assertFalse(room.snapshot(System.currentTimeMillis(), a).containsKey("objective"));
    }

    private static GameRoom journeyRoom() {
        GameRoom room = new GameRoom("SHARD3", SyncMode.OPTIMIZED, GameMode.JOURNEY);
        for (String name : new String[] {"A", "B", "C"}) room.join(new PlayerState(name, null));
        for (PlayerState p : room.players) room.ready(p, true);
        assertEquals(GameRoom.Phase.PLAYING, room.phase);
        // Keep the third player out of every test interaction unless a test moves them.
        park(room.players.get(2), -40, 40);
        return room;
    }

    /** Spawns the shard, lets owner pick it up and place it; returns the time of the last update. */
    private static long placeShard(GameRoom room, PlayerState owner) {
        ShardObjective shard = room.objective;
        long t = shard.startedAt + ShardObjective.SPAWN_DELAY_MS;
        for (PlayerState p : room.players) if (p != owner) park(p, 40, 40);
        room.update(t, 0);
        park(owner, shard.x, shard.z);
        room.update(t += 100, 0);
        park(owner, 0, 0);
        room.update(t += 100, 0);
        assertSame(owner, shard.owner);
        return t;
    }

    private static void park(PlayerState p, double x, double z) {
        p.x = x; p.z = z; p.dx = 0; p.dz = 0;
    }
}
