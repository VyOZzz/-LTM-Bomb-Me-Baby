package bombmebaby;

import static org.junit.jupiter.api.Assertions.*;

import org.junit.jupiter.api.Test;

class RequestedGameplayRulesTest {
    @Test void minimumSizeFullChargeDealsExactlyEightyPointBlankDamage() {
        assertEquals(4_000, GameRules.MAX_CHARGE_MS);
        double radius = GameRules.blastRadius(1, 1);
        assertEquals(80, GameRules.damage(1, 1, 0, radius), 1e-9);
        assertEquals(160, GameRules.damage(2, 1, 0, GameRules.blastRadius(2, 1)), 1e-9,
            "Bomb damage scales multiplicatively with size");
    }

    @Test void fullChargeExplosionActuallyRemovesEightyHp() {
        GameRoom room = startedRoom();
        PlayerState source = room.players.get(0), target = room.players.get(1), spare = room.players.get(2);
        park(source, 0, 0); park(target, 0, 0); park(spare, 30, 30);
        long now = 50_000;
        source.charging = true;
        source.chargeSince = now - GameRules.MAX_CHARGE_MS;
        room.action(source, "EXPLODE", 0, 0, now);
        assertEquals(20, target.hp, 1e-9);
        assertTrue(target.alive);
    }

    @Test void energyPickupHealsOneHpAndGrantsShortSpeedBoost() {
        GameRoom room = startedRoom();
        PlayerState player = room.players.get(0), other = room.players.get(1), spare = room.players.get(2);
        park(player, 0, 0); park(other, 30, 30); park(spare, -30, -30);
        player.hp = 50;
        room.orbs.clear();
        room.orbs.add(new GameRoom.Pickup(999, 0, 0));
        long now = 60_000;
        room.update(now, 0);
        assertEquals(51, player.hp, 1e-9);
        assertEquals(now + GameRules.ENERGY_SPEED_BOOST_MS, player.energyBoostUntil);
        assertEquals(1.12, GameRules.ENERGY_SPEED_FACTOR, 1e-9);
    }

    private static GameRoom startedRoom() {
        GameRoom room = new GameRoom("RULES", SyncMode.BASELINE, GameMode.JOURNEY);
        room.join(new PlayerState("A", null));
        room.join(new PlayerState("B", null));
        room.join(new PlayerState("C", null));
        for (PlayerState player : room.players) room.ready(player, true);
        room.orbs.clear();
        return room;
    }

    private static void park(PlayerState player, double x, double z) {
        player.x = x; player.z = z; player.dx = 0; player.dz = 0;
        player.knockbackX = 0; player.knockbackZ = 0;
    }
}
