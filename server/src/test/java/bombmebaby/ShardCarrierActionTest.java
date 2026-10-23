package bombmebaby;

import static org.junit.jupiter.api.Assertions.*;

import org.junit.jupiter.api.Test;

class ShardCarrierActionTest {
    @Test void pickingUpShardCancelsChargeAndCarrierCannotStartOrReleaseAttack() {
        Fixture f = carriedFixture();

        assertFalse(f.carrier.charging, "Pickup must cancel a charge already in progress");
        f.room.action(f.carrier, "CHARGE_START", 0, 0, f.now + 100);
        assertFalse(f.carrier.charging, "Carrier cannot start charging");

        f.carrier.charging = true; // Simulate a stale or malicious client request.
        f.room.action(f.carrier, "EXPLODE", 0, 0, f.now + 200);
        assertFalse(f.carrier.charging);
        assertEquals(100, f.other.hp, 1e-9, "Carrier explosion must never deal damage");
    }

    @Test void carrierCanDropShardAndCannotImmediatelyPickItBackUp() {
        Fixture f = carriedFixture();
        double x = f.carrier.x, z = f.carrier.z;

        f.room.action(f.carrier, "DROP_SHARD", 0, 0, f.now + 100);

        assertEquals(ShardObjective.State.GROUND, f.shard.state);
        assertNull(f.shard.carrier);
        assertEquals(x, f.shard.x, 1e-9);
        assertEquals(z, f.shard.z, 1e-9);
        f.room.update(f.now + ShardObjective.GROUND_NEUTRAL_MS + 200, 0);
        assertEquals(ShardObjective.State.GROUND, f.shard.state,
            "Former carrier remains locked out after a voluntary drop");
    }

    private static Fixture carriedFixture() {
        GameRoom room = new GameRoom("DROPQ1", SyncMode.OPTIMIZED, GameMode.JOURNEY);
        PlayerState carrier = new PlayerState("Carrier", null);
        PlayerState other = new PlayerState("Other", null);
        room.join(carrier);
        room.join(other);
        room.random.setSeed(42);
        room.ready(carrier, true);
        room.ready(other, true);
        room.orbs.clear();
        ShardObjective shard = room.objective;
        long now = shard.startedAt + ShardObjective.SPAWN_DELAY_MS;
        other.x = 40; other.z = 40;
        room.update(now, 0);
        carrier.x = shard.x; carrier.z = shard.z;
        // A real, recent charge: leaving chargeSince at zero auto-explodes before
        // pickup and can randomly damage the other player during fixture setup.
        room.action(carrier, "CHARGE_START", 0, 0, now);
        assertTrue(carrier.charging);
        room.update(now += 100, 0);
        assertSame(carrier, shard.carrier);
        return new Fixture(room, shard, carrier, other, now);
    }

    private record Fixture(GameRoom room, ShardObjective shard, PlayerState carrier,
                           PlayerState other, long now) {}
}
