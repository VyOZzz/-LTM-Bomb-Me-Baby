package bombmebaby;

import static org.junit.jupiter.api.Assertions.*;
import org.junit.jupiter.api.Test;

class GameRoomTest {
    @Test void oneReadyPlayerCanStartForSoloTesting() {
        GameRoom room = new GameRoom("SOLO01");
        PlayerState player = new PlayerState("Solo", null);
        room.join(player);

        room.ready(player,true);

        assertEquals(GameRoom.Phase.PLAYING, room.phase);
        assertTrue(player.alive);
        assertEquals(100, player.hp);
        assertEquals(GameRules.INITIAL_ORBS, room.orbs.size());

        room.action(player,"CHARGE_START",0,0,1000);
        room.action(player,"EXPLODE",0,0,2000);

        assertEquals(GameRoom.Phase.PLAYING, room.phase);
        assertFalse(player.charging);
    }

    @Test void requiresTwoReadyPlayersAndResetsRound() {
        GameRoom room = new GameRoom("ABC123");
        PlayerState a = new PlayerState("A", null);
        PlayerState b = new PlayerState("B", null);
        room.join(a); room.join(b);
        room.ready(a,true);
        assertEquals(GameRoom.Phase.WAITING, room.phase);
        room.ready(b,true);
        assertEquals(GameRoom.Phase.PLAYING, room.phase);
        assertEquals(100, a.hp);
        assertEquals(GameRules.INITIAL_ORBS, room.orbs.size());
    }

    @Test void chargeHasAVisibleEffectOnBlastRadius() {
        double quickTap = GameRules.blastRadius(1, 0.02);
        double fullCharge = GameRules.blastRadius(1, 1);

        assertTrue(quickTap < 1.5);
        assertTrue(fullCharge > quickTap * 3);
    }

    @Test void bushesHidePlayersWithoutBlockingMovement() {
        GameRoom room = startedRoom();
        PlayerState hidden = room.players.get(0), viewer = room.players.get(1);
        GameMap.Bush bush = room.bushes.get(0);
        hidden.x = bush.x(); hidden.z = bush.z();
        viewer.x = hidden.x + Math.min(GameRules.PLAYER_VISIBILITY_DISTANCE - 1, 8);
        viewer.z = hidden.z;

        assertFalse(room.canSee(viewer, hidden));

        viewer.x = bush.x(); viewer.z = bush.z();
        assertTrue(room.canSee(viewer, hidden));

        hidden.x = bush.x() - bush.radius() - 1; hidden.z = bush.z();
        room.action(hidden,"MOVE",1,0,1000);
        room.update(1050,.25);
        assertTrue(hidden.x > bush.x() - bush.radius() - 1);
    }

    @Test void explosionDealsMinimumDamageAndResetsSize() {
        GameRoom room = startedRoom();
        PlayerState a = room.players.get(0), b = room.players.get(1);
        a.x = 0; a.z = 0; b.x = 0.5; b.z = 0;
        a.size = 2;
        room.action(a,"CHARGE_START",0,0,1000);
        room.action(a,"EXPLODE",0,0,2000);
        assertTrue(b.hp < 100);
        assertEquals(a.baseSize,a.size);
        assertEquals(100,a.hp);
    }

    @Test void shieldReflectsAndDoesNotDamageTarget() {
        GameRoom room = startedRoom();
        PlayerState a = room.players.get(0), b = room.players.get(1);
        a.x = 0; a.z = 0; b.x = 0.5; b.z = 0;
        room.action(b,"USE_SHIELD",0,0,1000);
        room.action(a,"CHARGE_START",0,0,1100);
        room.action(a,"EXPLODE",0,0,2000);
        assertEquals(100,b.hp);
        assertTrue(a.hp < 100);
    }

    @Test void maxChargeAutoExplodesAndBoostConsumesSize() {
        GameRoom room = startedRoom();
        PlayerState a = room.players.get(0);
        a.size = 2;
        room.action(a,"USE_BOOST",0,0,1000);
        assertTrue(a.size < 2);
        room.action(a,"CHARGE_START",0,0,1000);
        room.update(9001,0);
        assertFalse(a.charging);
        assertEquals(a.baseSize,a.size);
    }

    @Test void movementIsNormalizedAndObstaclesBlockMovement() {
        GameRoom room = startedRoom();
        PlayerState a = room.players.get(0);
        GameMap.Obstacle obstacle = room.obstacles.get(0);
        a.x = obstacle.x() - obstacle.radius() - GameRules.PLAYER_RADIUS - .1;
        a.z = obstacle.z();
        double previousX = a.x;
        room.action(a,"MOVE",99,0,1000);
        assertEquals(1,a.dx);
        room.update(1050,0.5);
        assertEquals(previousX,a.x,0.0001);
    }

    @Test void supportsTenPlayersWithSeparatedRandomSpawns() {
        GameRoom room = new GameRoom("TEN010");
        for (int i = 0; i < GameRules.MAX_PLAYERS; i++) {
            PlayerState player = new PlayerState("P" + i, null);
            assertTrue(room.join(player));
        }
        assertFalse(room.join(new PlayerState("Extra", null)));
        for (PlayerState player : room.players) room.ready(player,true);

        assertEquals(GameRoom.Phase.PLAYING,room.phase);
        assertEquals(GameRules.MAX_PLAYERS,room.players.size());
        for (int i = 0; i < room.players.size(); i++) {
            PlayerState a = room.players.get(i);
            assertTrue(Math.abs(a.x) < room.map.halfSize);
            assertTrue(Math.abs(a.z) < room.map.halfSize);
            for (int j = i + 1; j < room.players.size(); j++) {
                PlayerState b = room.players.get(j);
                assertTrue(GameRules.distance(a.x,a.z,b.x,b.z) >= GameRules.MIN_SPAWN_DISTANCE);
                assertFalse(room.canSee(a,b));
            }
        }
    }

    @Test void shipsSeveralLargeMapLayoutsInSnapshots() {
        assertTrue(GameMap.all().size() >= 3);
        assertTrue(GameMap.all().stream().allMatch(map -> map.halfSize >= 46));

        GameRoom room = startedRoom();
        var snapshot = room.snapshot(1000);
        assertEquals(room.map.id,snapshot.get("mapId"));
        assertEquals(room.map.halfSize,snapshot.get("arenaSize"));
        assertSame(room.bushes,snapshot.get("bushes"));
    }

    @Test void eliminatedPlayerDropsContestedCoreAndLastSurvivorWins() {
        GameRoom room = new GameRoom("ABC123");
        PlayerState a = new PlayerState("A", null), b = new PlayerState("B", null), c = new PlayerState("C", null);
        room.join(a); room.join(b); room.join(c);
        room.ready(a,true); room.ready(b,true); room.ready(c,true);
        a.x = 0; a.z = 0; b.x = .5; b.z = 0; c.x = 8; c.z = 8;
        b.hp = 1;
        room.action(a,"CHARGE_START",0,0,1000);
        room.action(a,"EXPLODE",0,0,2000);
        assertFalse(b.alive);
        assertEquals(1,room.cores.size());
        assertEquals(GameRoom.Phase.PLAYING,room.phase);
        a.x = -6; a.z = -6;
        c.x = b.x; c.z = b.z;
        room.update(2050,.05);
        assertEquals(1.1,c.baseSize,0.0001);
        assertTrue(room.cores.isEmpty());
        room.leave(a);
        assertEquals(GameRoom.Phase.FINISHED,room.phase);
    }

    private GameRoom startedRoom() {
        GameRoom room = new GameRoom("ABC123");
        PlayerState a = new PlayerState("A", null), b = new PlayerState("B", null);
        room.join(a); room.join(b); room.ready(a,true); room.ready(b,true);
        return room;
    }
}
