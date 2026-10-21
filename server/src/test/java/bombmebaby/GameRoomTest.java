package bombmebaby;

import static org.junit.jupiter.api.Assertions.*;
import java.util.List;
import java.util.Map;
import org.junit.jupiter.api.Test;

class GameRoomTest {
    @Test void demoMapPropertyLocksQuickRoomsToRequestedMap() {
        String previous = System.getProperty("demo.map");
        try {
            System.setProperty("demo.map", "crossroads");
            assertEquals("crossroads", new GameRoom("DEMO01").map.id);
            assertEquals("crossroads", new GameRoom("DEMO02").map.id);
        } finally {
            if (previous == null) System.clearProperty("demo.map");
            else System.setProperty("demo.map", previous);
        }
    }

    @Test void aLonePlayerCannotStartEvenWhenReady() {
        GameRoom room = new GameRoom("SOLO01");
        PlayerState player = new PlayerState("Solo", null);
        room.join(player);

        room.ready(player,true);

        assertEquals(GameRoom.Phase.WAITING, room.phase);
        assertFalse(room.startMatched(1000));
        assertEquals(GameRoom.Phase.WAITING, room.phase);
        assertEquals(GameRules.MIN_PLAYERS, room.roomInfo().get("minPlayers"));
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

    @Test void explosionStartsServerAuthoritativeBombCooldown() {
        GameRoom room = startedRoom();
        PlayerState player = room.players.get(0);
        room.action(player,"CHARGE_START",0,0,1000);
        room.action(player,"EXPLODE",0,0,1500);

        assertEquals(1500 + GameRules.BOMB_COOLDOWN_MS, player.chargeReadyAt);
        room.action(player,"CHARGE_START",0,0,2000);
        assertFalse(player.charging);
        room.action(player,"CHARGE_START",0,0,1500 + GameRules.BOMB_COOLDOWN_MS);
        assertTrue(player.charging);
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

    @Test void quickModeKeepsLegacyMovementWithoutJourneyKnockback() {
        GameRoom room = startedRoom();
        PlayerState a = room.players.get(0), b = room.players.get(1);
        a.x = 0; a.z = 0; b.x = .5; b.z = 0;
        room.action(a,"CHARGE_START",0,0,1_000);
        room.action(a,"EXPLODE",0,0,2_000);

        assertEquals(0, b.knockbackX, .0001);
        assertEquals(0, b.knockbackZ, .0001);
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
        var snapshot = room.snapshot(1000, null, true);
        assertEquals(room.map.id,snapshot.get("mapId"));
        assertEquals(room.map.halfSize,snapshot.get("arenaSize"));
        assertSame(room.bushes,snapshot.get("bushes"));
    }

    @Test void tenPlayersCanStartAcrossMapsAndSeeds() {
        for (GameMap map : GameMap.all()) {
            for (int seed = 0; seed < 20; seed++) {
                GameRoom room = new GameRoom("SEED10");
                room.map = map; room.obstacles = map.obstacles; room.bushes = map.bushes;
                room.random.setSeed(seed);
                for (int i = 0; i < 10; i++) room.join(new PlayerState("P" + i, null));
                for (PlayerState player : room.players) room.ready(player, true);
                assertEquals(GameRoom.Phase.PLAYING, room.phase, map.id + " seed=" + seed);
                for (int i = 0; i < 10; i++) {
                    for (int j = i + 1; j < 10; j++) {
                        PlayerState a = room.players.get(i), b = room.players.get(j);
                        assertTrue(GameRules.distance(a.x, a.z, b.x, b.z) >= GameRules.MIN_SPAWN_DISTANCE,
                            map.id + " seed=" + seed);
                    }
                }
            }
        }
    }

    @Test void threeFragmentJourneyUsesFixedMapsScoresAndFinalRoundTieBreak() {
        GameRoom room = new GameRoom("STORY3", SyncMode.OPTIMIZED, GameMode.JOURNEY);
        PlayerState a = new PlayerState("A", null);
        PlayerState b = new PlayerState("B", null);
        PlayerState c = new PlayerState("C", null);
        room.join(a); room.join(b); room.join(c);

        readyAll(room);
        assertEquals("meadow", room.map.id);
        assertEquals(0, room.chapterIndex);
        eliminateTwoAndWin(room, a, b, c);
        assertScore(room, a, 5, 1);

        readyAll(room);
        assertEquals("crossroads", room.map.id);
        assertEquals(1, room.chapterIndex);
        eliminateTwoAndWin(room, b, a, c);
        assertScore(room, b, 5, 1);

        readyAll(room);
        assertEquals("crater", room.map.id);
        assertEquals(2, room.chapterIndex);
        eliminateTwoAndWin(room, c, a, b);

        assertTrue(room.journeyComplete);
        assertScore(room, c, 5, 1);
        assertEquals(List.of(c.id), room.journeyWinnerIds,
            "Equal points and round wins must be decided by the final-round winner");
        Map<String,Object> event = room.journeyCompleteEvent();
        assertEquals("JOURNEY_COMPLETE", event.get("type"));
        assertEquals(c.id, event.get("winnerId"));
        assertEquals(2, event.get("chapterIndex"));
        assertEquals(room.scores(), event.get("scores"));

        room.ready(a, true);
        assertEquals(GameRoom.Phase.FINISHED, room.phase,
            "A completed journey cannot start a fourth round");
    }

    @Test void journeyStateIsIncludedWhileQuickModeRemainsBackwardCompatible() {
        GameRoom journey = new GameRoom("STATE3", SyncMode.BASELINE, GameMode.JOURNEY);
        PlayerState player = new PlayerState("A", null);
        journey.join(player);
        Map<String,Object> info = journey.roomInfo();
        assertEquals("JOURNEY", info.get("mode"));
        assertEquals(0, info.get("chapterIndex"));
        assertEquals(1, ((List<?>) info.get("scores")).size());

        GameRoom quick = new GameRoom("QUICK1", SyncMode.BASELINE, GameMode.QUICK);
        quick.join(new PlayerState("Q", null));
        Map<String,Object> quickInfo = quick.roomInfo();
        assertEquals("QUICK", quickInfo.get("mode"));
        assertEquals(-1, quickInfo.get("chapterIndex"));
        assertEquals(List.of(), quickInfo.get("scores"));
    }

    @Test void reflectedEliminationAwardsJourneyPointToShieldOwner() {
        GameRoom room = new GameRoom("REFLJ3", SyncMode.BASELINE, GameMode.JOURNEY);
        PlayerState attacker = new PlayerState("Attacker", null);
        PlayerState defender = new PlayerState("Defender", null);
        room.join(attacker); room.join(defender);
        readyAll(room);
        attacker.x = 0; attacker.z = 0; attacker.hp = 1;
        defender.x = .5; defender.z = 0;
        room.action(defender,"USE_SHIELD",0,0,1_000);
        room.action(attacker,"CHARGE_START",0,0,1_100);
        room.action(attacker,"EXPLODE",0,0,2_000);
        assertFalse(attacker.alive);
        assertScore(room, defender, 4, 1);
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

    private void readyAll(GameRoom room) {
        for (PlayerState player : room.players) room.ready(player, true);
        assertEquals(GameRoom.Phase.PLAYING, room.phase);
    }

    private void eliminateTwoAndWin(GameRoom room, PlayerState winner,
                                    PlayerState firstVictim, PlayerState secondVictim) {
        explodeToEliminate(room, winner, firstVictim, 1_000);
        assertEquals(GameRoom.Phase.PLAYING, room.phase);
        explodeToEliminate(room, winner, secondVictim, 2_000);
        assertEquals(GameRoom.Phase.FINISHED, room.phase);
    }

    private void explodeToEliminate(GameRoom room, PlayerState killer, PlayerState victim, long now) {
        for (PlayerState player : room.players) {
            if (player != killer && player != victim && player.alive) {
                player.x = 30; player.z = 30;
            }
        }
        killer.x = 0; killer.z = 0;
        killer.chargeReadyAt = 0; // Scoring helper intentionally isolates elimination from cooldown timing.
        victim.x = .5; victim.z = 0; victim.hp = 1;
        room.action(killer,"CHARGE_START",0,0,now);
        room.action(killer,"EXPLODE",0,0,now + 500);
        assertFalse(victim.alive);
    }

    @SuppressWarnings("unchecked")
    private void assertScore(GameRoom room, PlayerState player, int points, int roundWins) {
        Map<String,Object> score = room.scores().stream()
            .filter(item -> item.get("playerId").equals(player.id)).findFirst().orElseThrow();
        assertEquals(points, score.get("points"));
        assertEquals(roundWins, score.get("roundWins"));
    }
}
