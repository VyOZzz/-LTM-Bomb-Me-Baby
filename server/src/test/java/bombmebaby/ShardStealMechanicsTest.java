package bombmebaby;

import static org.junit.jupiter.api.Assertions.*;

import java.util.List;
import java.util.Map;
import org.junit.jupiter.api.Test;

class ShardStealMechanicsTest {
    @Test void weakChargeOrLowActualDamageDoesNotBreakFireLock() {
        Fixture f = carried();
        long hit = f.time + ShardObjective.CARRIER_PROTECTION_MS;

        f.shard.onDamaged(f.a, ShardObjective.MIN_LOCK_BREAK_CHARGE - .01, 50, hit, 5, 0);
        f.shard.onDamaged(f.a, 1, ShardObjective.MIN_LOCK_BREAK_DAMAGE - .01, hit + 1, 5, 0);

        assertEquals(ShardObjective.FIRE_LOCK_MAX, f.shard.fireLock);
        assertSame(f.a, f.shard.carrier);
    }

    @Test void shieldedExplosionDoesNotBreakLockOrResetSeal() {
        Fixture carried = carried();
        long hit = carried.time + ShardObjective.CARRIER_PROTECTION_MS;
        park(carried.a, .5, 0);
        park(carried.b, 0, 0);
        carried.a.shieldUntil = hit + 1;
        fullyCharge(carried.b, hit);
        carried.room.action(carried.b, "EXPLODE", 0, 0, hit);
        assertEquals(ShardObjective.FIRE_LOCK_MAX, carried.shard.fireLock);

        Fixture placed = placed();
        long active = placed.time + ShardObjective.PEDESTAL_PROTECTION_MS;
        park(placed.a, 20, 0);
        park(placed.b, 1, 0);
        placed.room.update(active, 0);
        advance(placed.room, active, active + 1_500);
        double before = progress(placed.shard, placed.b, active + 1_500);
        park(placed.c, 4.5, 0);
        placed.b.shieldUntil = active + 1_601;
        fullyCharge(placed.c, active + 1_600);
        placed.room.action(placed.c, "EXPLODE", 0, 0, active + 1_600);
        assertEquals(before, progress(placed.shard, placed.b, active + 1_600), .001);
        assertEquals(0, placed.b.staggerUntil);
    }

    @Test void twoValidHitsDropShardAndLockRecoversWhenSecondHitIsLate() {
        Fixture f = carried();
        long hit = f.time + ShardObjective.CARRIER_PROTECTION_MS;
        f.shard.onDamaged(f.a, .7, 20, hit, 8, 0);
        assertEquals(1, f.shard.fireLock);
        f.shard.onDamaged(f.a, .7, 20, hit + ShardObjective.FIRE_LOCK_RECOVERY_MS, 8, 0);
        assertEquals(1, f.shard.fireLock, "Lock first recovers to two, then this hit removes one pip");
        f.shard.onDamaged(f.a, .7, 20, hit + ShardObjective.FIRE_LOCK_RECOVERY_MS + 100, 8, 0);
        assertEquals(ShardObjective.State.GROUND, f.shard.state);
        assertEquals(8, f.shard.x, .001);
    }

    @Test void formerCarrierCannotRepickBeforeLockoutAndNewCarrierGetsProtection() {
        Fixture f = carried();
        long hit = f.time + ShardObjective.CARRIER_PROTECTION_MS;
        f.shard.onDamaged(f.a, .8, 20, hit, 8, 0);
        f.shard.onDamaged(f.a, .8, 20, hit + 100, 8, 0);
        park(f.a, 8, 0);
        park(f.b, 30, 30);
        f.room.update(hit + ShardObjective.GROUND_NEUTRAL_MS + 150, 0);
        assertEquals(ShardObjective.State.GROUND, f.shard.state);

        park(f.a, 30, 30);
        park(f.b, 8, 0);
        long pickup = hit + ShardObjective.GROUND_NEUTRAL_MS + 200;
        f.room.update(pickup, 0);
        assertSame(f.b, f.shard.carrier);
        f.shard.onDamaged(f.b, 1, 50, pickup + 1_000, 10, 0);
        assertEquals(ShardObjective.FIRE_LOCK_MAX, f.shard.fireLock);
    }

    @Test void pedestalProtectionBlocksAllSealProgress() {
        Fixture f = placed();
        park(f.a, 20, 0);
        park(f.b, 1, 0);
        f.room.update(f.time + ShardObjective.PEDESTAL_PROTECTION_MS - 1, 0);
        assertNull(f.shard.challenger);
        assertEquals(0, breakers(f.shard, f.time).size());
    }

    @Test void ownerOnlyHalvesProgressAndMultipleChallengersAdvanceIndependently() {
        Fixture f = placed();
        park(f.a, -1, 0);
        park(f.b, 1, 0);
        park(f.c, 0, 2);
        long active = f.time + ShardObjective.PEDESTAL_PROTECTION_MS;
        f.room.update(active, 0);
        advance(f.room, active, active + 2_000);

        assertEquals(1.0 / 3.0, progress(f.shard, f.b, active + 2_000), .02);
        assertEquals(1.0 / 3.0, progress(f.shard, f.c, active + 2_000), .02);
        assertEquals(ShardObjective.State.PLACED, f.shard.state);
    }

    @Test void realDamageResetsOnlyThatBreakerAndAppliesStagger() {
        Fixture f = placed();
        park(f.a, 20, 0);
        park(f.b, 1, 0);
        park(f.c, -2, 0);
        long active = f.time + ShardObjective.PEDESTAL_PROTECTION_MS;
        f.room.update(active, 0);
        advance(f.room, active, active + 1_200);
        double otherProgress = progress(f.shard, f.c, active + 1_200);

        // Use the owner as a source outside the ring so C remains an independent breaker.
        park(f.a, 4.5, 0);
        fullyCharge(f.a, active + 1_300);
        f.room.action(f.a, "EXPLODE", 0, 0, active + 1_300);

        assertEquals(0, progress(f.shard, f.b, active + 1_300), .001);
        assertTrue(f.b.staggerUntil > active + 1_300);
        assertEquals(otherProgress, progress(f.shard, f.c, active + 1_300), .001);
    }

    @Test void leavingDeathAndDisconnectRemoveOnlyAffectedProgress() {
        Fixture f = placed();
        park(f.a, 20, 0);
        park(f.b, 1, 0);
        park(f.c, 2, 0);
        long active = f.time + ShardObjective.PEDESTAL_PROTECTION_MS;
        f.room.update(active, 0);
        f.room.update(active + 500, 0);

        park(f.b, 10, 0);
        f.room.update(active + 600, 0);
        assertNull(findBreaker(f.shard, f.b, active + 600));
        assertNotNull(findBreaker(f.shard, f.c, active + 600));

        f.c.connected = false;
        f.c.disconnectedAt = active + 600;
        f.room.update(active + 700, 0);
        assertNull(findBreaker(f.shard, f.c, active + 700));

        f.b.alive = true;
        park(f.b, 1, 0);
        f.room.update(active + 800, 0);
        f.room.update(active + 900, 0);
        assertNotNull(findBreaker(f.shard, f.b, active + 900));
        f.b.alive = false;
        f.room.update(active + 1_000, 0);
        assertNull(findBreaker(f.shard, f.b, active + 1_000));
    }

    @Test void completedSealBouncesShardGrantsPriorityAndOverheatsPedestal() {
        Fixture f = placed();
        park(f.a, 20, 0);
        park(f.b, 2.5, 0);
        park(f.c, 20, 20);
        long active = f.time + ShardObjective.PEDESTAL_PROTECTION_MS;
        f.room.update(active, 0);
        long broken = active + ShardObjective.SEAL_BREAK_MS;
        advance(f.room, active, broken);

        assertEquals(ShardObjective.State.GROUND, f.shard.state);
        assertSame(f.b, f.shard.pickupPriority);
        assertTrue(Math.hypot(f.shard.x, f.shard.z) > ShardObjective.PEDESTAL_RADIUS);
        assertEquals(ShardObjective.PEDESTAL_OVERHEAT_MS,
            f.shard.pedestalOverheatUntil - broken);

        park(f.b, f.shard.x, f.shard.z);
        f.room.update(broken + 50, 0);
        assertSame(f.b, f.shard.carrier);
        park(f.b, 0, 0);
        f.room.update(broken + 100, 0);
        assertEquals(ShardObjective.State.CARRIED, f.shard.state,
            "Overheated pedestal cannot accept the shard");
        f.room.update(broken + ShardObjective.PEDESTAL_OVERHEAT_MS, 0);
        assertEquals(ShardObjective.State.PLACED, f.shard.state);
    }

    @Test void deadOrDisconnectedPriorityHolderLosesPriorityImmediately() {
        Fixture f = placed();
        park(f.a, 20, 0);
        park(f.b, 1, 0);
        long active = f.time + ShardObjective.PEDESTAL_PROTECTION_MS;
        f.room.update(active, 0);
        long broken = active + ShardObjective.SEAL_BREAK_MS;
        advance(f.room, active, broken);
        f.b.connected = false;
        f.b.disconnectedAt = broken;
        park(f.c, f.shard.x, f.shard.z);
        f.room.update(broken + 10, 0);
        assertSame(f.c, f.shard.carrier);
        assertNull(f.shard.pickupPriority);
    }

    @Test void carrierDisconnectDropsWithinTwoSeconds() {
        Fixture f = carried();
        f.a.connected = false;
        f.a.disconnectedAt = f.time;
        f.room.update(f.time + ShardObjective.CARRIER_DISCONNECT_DROP_MS - 1, 0);
        assertEquals(ShardObjective.State.CARRIED, f.shard.state);
        f.room.update(f.time + ShardObjective.CARRIER_DISCONNECT_DROP_MS, 0);
        assertEquals(ShardObjective.State.GROUND, f.shard.state);
        assertTrue(f.a.alive, "Objective drop happens before the room's reconnect grace elimination");
    }

    @Test void knockbackMovesOutwardDecaysAndCannotCrossArenaBoundary() {
        GameRoom room = new GameRoom("KBTEST", SyncMode.BASELINE, GameMode.JOURNEY);
        PlayerState source = new PlayerState("Source", null);
        PlayerState target = new PlayerState("Target", null);
        PlayerState spare = new PlayerState("Spare", null);
        room.join(source); room.join(target); room.join(spare);
        for (PlayerState p : room.players) room.ready(p, true);
        park(source, room.map.halfSize - 5, 0);
        park(target, room.map.halfSize - 1, 0);
        park(spare, -30, -30);
        fullyCharge(source, 10_000);
        room.action(source, "EXPLODE", 0, 0, 10_000);
        assertTrue(target.knockbackX > 0);
        double initial = target.knockbackX;
        for (int i = 1; i <= 20; i++) room.update(10_000 + i * 50, .05);
        assertTrue(target.x <= room.map.halfSize - GameRules.PLAYER_RADIUS * target.size + .0001);
        assertTrue(Math.abs(target.knockbackX) < initial);

        GameMap.Obstacle obstacle = room.obstacles.get(0);
        park(target, obstacle.x() - obstacle.radius() - GameRules.PLAYER_RADIUS - .05, obstacle.z());
        target.knockbackX = 12;
        double beforeObstacle = target.x;
        room.update(12_000, .05);
        assertEquals(beforeObstacle, target.x, .0001);
        assertEquals(0, target.knockbackX, .0001);
    }

    private static Fixture carried() {
        GameRoom room = room();
        ShardObjective shard = room.objective;
        long time = shard.startedAt + ShardObjective.SPAWN_DELAY_MS;
        room.update(time, 0);
        PlayerState a = room.players.get(0), b = room.players.get(1), c = room.players.get(2);
        park(b, 30, 30); park(c, -30, 30);
        park(a, shard.x, shard.z);
        room.update(time += 100, 0);
        assertSame(a, shard.carrier);
        return new Fixture(room, shard, a, b, c, time);
    }

    private static Fixture placed() {
        Fixture f = carried();
        park(f.a, 0, 0);
        f.room.update(f.time += 100, 0);
        assertEquals(ShardObjective.State.PLACED, f.shard.state);
        return f;
    }

    private static GameRoom room() {
        GameRoom room = new GameRoom("STEAL3", SyncMode.BASELINE, GameMode.JOURNEY);
        room.join(new PlayerState("A", null));
        room.join(new PlayerState("B", null));
        room.join(new PlayerState("C", null));
        for (PlayerState p : room.players) room.ready(p, true);
        return room;
    }

    private static void fullyCharge(PlayerState source, long explodeAt) {
        source.charging = true;
        source.chargeSince = explodeAt - GameRules.MAX_CHARGE_MS;
        source.chargeReadyAt = 0;
    }

    private static void advance(GameRoom room, long from, long to) {
        for (long now = from + 50; now <= to; now += 50) room.update(now, .05);
    }

    @SuppressWarnings("unchecked")
    private static List<Map<String,Object>> breakers(ShardObjective shard, long now) {
        return (List<Map<String,Object>>) shard.data(now).get("sealBreakers");
    }

    private static Map<String,Object> findBreaker(ShardObjective shard, PlayerState p, long now) {
        return breakers(shard, now).stream()
            .filter(item -> p.id.equals(item.get("playerId"))).findFirst().orElse(null);
    }

    private static double progress(ShardObjective shard, PlayerState p, long now) {
        Map<String,Object> item = findBreaker(shard, p, now);
        return item == null ? 0 : ((Number) item.get("progress")).doubleValue();
    }

    private static void park(PlayerState p, double x, double z) {
        p.x = x; p.z = z; p.dx = 0; p.dz = 0; p.knockbackX = 0; p.knockbackZ = 0;
    }

    private static final class Fixture {
        final GameRoom room;
        final ShardObjective shard;
        final PlayerState a, b, c;
        long time;

        Fixture(GameRoom room, ShardObjective shard, PlayerState a, PlayerState b,
                PlayerState c, long time) {
            this.room = room;
            this.shard = shard;
            this.a = a;
            this.b = b;
            this.c = c;
            this.time = time;
        }
    }
}
