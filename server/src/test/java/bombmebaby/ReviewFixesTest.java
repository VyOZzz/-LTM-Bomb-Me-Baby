package bombmebaby;

import static org.junit.jupiter.api.Assertions.*;
import java.util.List;
import java.util.Map;
import org.junit.jupiter.api.Test;

class ReviewFixesTest {
    private static GameRoom room(GameMode mode, int count) {
        GameRoom r = new GameRoom("REVIEW", SyncMode.OPTIMIZED, mode);
        for (int i = 0; i < count; i++) r.join(new PlayerState("P" + i, null));
        assertTrue(r.startMatched(100_000));
        r.obstacles = List.of(); r.bushes = List.of(); r.orbs.clear();
        for (int i = 0; i < count; i++) { r.players.get(i).x = i * 15; r.players.get(i).z = 0; }
        return r;
    }

    @Test void leavingJourneyConcludesWithoutInventingFutureShards() {
        GameRoom r = room(GameMode.JOURNEY, 2);
        PlayerState a = r.players.get(0), b = r.players.get(1);
        r.leave(b);
        assertEquals(GameRoom.Phase.FINISHED, r.phase);
        assertTrue(r.journeyComplete);
        assertEquals("INSUFFICIENT_PLAYERS", r.journeyCompleteEvent().get("reason"));
        assertEquals(List.of(a.id), r.journeyWinnerIds);
        assertEquals(1, r.journeyScores.get(a.id).roundWins);
        r.ready(a, true);
        r.update(200_000, .05);
        assertEquals(0, r.chapterIndex);
        assertEquals(1, r.journeyScores.get(a.id).roundWins);
        assertFalse(r.join(new PlayerState("Replacement", null)));
    }

    @Test void timeoutAdvancesToNextChapterAndLeavingThenConcludesJourney() {
        GameRoom r = room(GameMode.JOURNEY, 2);
        r.update(100_000 + ShardObjective.CHAPTER_MS, .05);
        assertFalse(r.journeyComplete);
        assertEquals(GameRoom.Phase.FINISHED, r.phase);
        r.leave(r.players.get(1));
        assertTrue(r.journeyComplete);
        assertEquals(0, r.journeyScores.values().stream().mapToInt(s -> s.roundWins).sum());
    }

    @Test void disconnectReservationSurvivesUntilExactDeadline() {
        GameRoom r = room(GameMode.JOURNEY, 2);
        PlayerState b = r.players.get(1);
        b.connected = false; b.disconnectedAt = 101_000;
        r.update(101_000 + GameRules.RECONNECT_MS - 1, .05);
        assertFalse(r.journeyComplete);
        assertTrue(b.alive);
        r.update(101_000 + GameRules.RECONNECT_MS, .05);
        assertTrue(r.journeyComplete);
        assertEquals("INSUFFICIENT_PLAYERS", r.roomInfo().get("journeyEndReason"));
    }

    @Test void quickTimeoutDrawsAndReplayGetsFreshClock() {
        GameRoom r = room(GameMode.QUICK, 2);
        r.update(100_000 + GameRoom.QUICK_ROUND_MS - 1, .05);
        assertEquals(GameRoom.Phase.PLAYING, r.phase);
        r.update(100_000 + GameRoom.QUICK_ROUND_MS, .05);
        assertEquals("TIME", r.roundResult().get("reason"));
        assertEquals("", r.roundResult().get("winnerId"));
        assertEquals(0.0, r.snapshot(280_000).get("roundTimeLeft"));
        for (PlayerState p : r.players) r.ready(p, true);
        assertEquals(GameRoom.Phase.PLAYING, r.phase);
        double remaining = ((Number)r.snapshot(System.currentTimeMillis()).get("roundTimeLeft")).doubleValue();
        assertTrue(remaining > 179 && remaining <= 180);
    }

    @Test void quickTimeoutRanksKillsThenDamageAndCrownsTheLeader() {
        GameRoom r = room(GameMode.QUICK, 4);
        PlayerState killer = r.players.get(0), victim = r.players.get(1);
        PlayerState hitter = r.players.get(2), hit = r.players.get(3);
        killer.x = 0; victim.x = 0; victim.hp = 10;
        hitter.x = 30; hit.x = 30;
        killer.z = victim.z = -15; hitter.z = hit.z = 15;
        long now = 150_000;
        for (PlayerState source : List.of(killer, hitter)) {
            source.charging = true;
            source.chargeSince = now - GameRules.MAX_CHARGE_MS;
            r.action(source, "EXPLODE", 0, 0, now);
        }
        assertFalse(victim.alive);
        assertEquals(20, hit.hp, 1e-9);

        r.update(100_000 + GameRoom.QUICK_ROUND_MS, .05);
        assertEquals("TIME", r.roundResult().get("reason"));
        assertEquals(killer.id, r.roundResult().get("winnerId"), "A kill (+3) outranks more damage");
        @SuppressWarnings("unchecked")
        List<Map<String,Object>> board = (List<Map<String,Object>>) r.roomInfo().get("leaderboard");
        assertEquals(List.of(killer.id, hitter.id, hit.id, victim.id),
            board.stream().map(row -> row.get("playerId")).toList());
        assertEquals(3, board.get(0).get("points"));
        assertEquals(10L, board.get(0).get("damage"));
        assertEquals(80L, board.get(1).get("damage"));
        assertEquals(List.of(1, 2, 3, 4), board.stream().map(row -> row.get("rank")).toList());
    }

    @Test void finalQuickMinuteRevealsEveryPositionForTheMinimapOnly() {
        GameRoom r = room(GameMode.QUICK, 2);
        PlayerState viewer = r.players.get(0), hidden = r.players.get(1);
        viewer.x = 0; viewer.z = 0; hidden.x = 20; hidden.z = 20;
        long revealAt = 100_000 + GameRoom.QUICK_ROUND_MS - GameRoom.QUICK_REVEAL_MS;

        Map<String,Object> before = r.snapshot(revealAt - 1, viewer);
        assertEquals(false, before.get("revealAll"));
        assertEquals(0.0, otherPlayer(before, hidden.id).get("x"));

        Map<String,Object> after = r.snapshot(revealAt, viewer);
        assertEquals(true, after.get("revealAll"));
        assertEquals(20.0, otherPlayer(after, hidden.id).get("x"));
        assertEquals(false, otherPlayer(after, hidden.id).get("visible"), "3D view stays fogged");
    }

    @SuppressWarnings("unchecked")
    private static Map<String,Object> otherPlayer(Map<String,Object> snapshot, String id) {
        return ((List<Map<String,Object>>) snapshot.get("players")).stream()
            .filter(p -> p.get("id").equals(id)).findFirst().orElseThrow();
    }

    @Test void lastStandingWinnerTopsQuickBoardEvenWithFewerKills() {
        GameRoom r = room(GameMode.QUICK, 3);
        PlayerState killer = r.players.get(0), victim = r.players.get(1), survivor = r.players.get(2);
        killer.x = 0; victim.x = 0; victim.hp = 10; survivor.x = 30;
        killer.z = victim.z = -15; survivor.z = 15;
        killer.charging = true;
        killer.chargeSince = 150_000 - GameRules.MAX_CHARGE_MS;
        r.action(killer, "EXPLODE", 0, 0, 150_000);
        r.leave(killer);
        assertEquals(survivor.id, r.roundResult().get("winnerId"));
        @SuppressWarnings("unchecked")
        List<Map<String,Object>> board = (List<Map<String,Object>>) r.roundResult().get("leaderboard");
        assertEquals(List.of(survivor.id, killer.id, victim.id),
            board.stream().map(row -> row.get("playerId")).toList());
        assertEquals(List.of(1, 2, 3), board.stream().map(row -> row.get("rank")).toList());
    }

    @Test void quickTimeoutWithoutAnyFightingIsStillADraw() {
        GameRoom r = room(GameMode.QUICK, 3);
        r.update(100_000 + GameRoom.QUICK_ROUND_MS, .05);
        assertEquals("", r.roundResult().get("winnerId"));
        @SuppressWarnings("unchecked")
        List<Map<String,Object>> board = (List<Map<String,Object>>) r.roundResult().get("leaderboard");
        assertEquals(List.of(1, 1, 1), board.stream().map(row -> row.get("rank")).toList());
    }

    @Test void lastStandingStillWinsBeforeQuickDeadline() {
        GameRoom r = room(GameMode.QUICK, 2);
        String id = r.players.get(0).id;
        r.leave(r.players.get(1));
        assertEquals(id, r.roundResult().get("winnerId"));
        assertEquals("LAST_STANDING", r.roundResult().get("reason"));
    }

    private static GameRoom placed() {
        GameRoom r = room(GameMode.JOURNEY, 3);
        r.objective.state = ShardObjective.State.CARRIED;
        r.objective.carrier = r.players.get(0);
        r.players.get(0).x = 0;
        r.objective.update(100_000, r.players);
        r.players.get(0).x = 15;
        r.players.get(1).x = 1;
        return r;
    }

    @SuppressWarnings("unchecked")
    private static double progress(GameRoom r, PlayerState p, long now) {
        var rows = (List<Map<String,Object>>)r.objective.data(now).get("sealBreakers");
        return rows.stream().filter(row -> p.id.equals(row.get("playerId")))
            .mapToDouble(row -> ((Number)row.get("progress")).doubleValue()).findFirst().orElse(0);
    }

    @Test void stallCannotCreditTimeBeforeEntryOrInstantlyFinishEstablishedBreaker() {
        GameRoom r = placed();
        PlayerState b = r.players.get(1);
        b.x = 3.2;
        r.update(100_000, .05);
        r.action(b, "MOVE", -1, 0, 104_000);
        r.update(104_000, .1);
        assertTrue(b.x < 3);
        assertEquals(0, progress(r, b, 104_000));
        r.action(b, "MOVE", 0, 0, 104_001);
        r.action(b, "INTERACT_START", 0, 0, 104_002);
        r.update(108_000, .1);
        assertEquals(100.0 / 3_000, progress(r, b, 108_000), .0001);
        assertEquals(ShardObjective.State.PLACED, r.objective.state);
        r.update(400_000, .1);
        assertEquals(GameRoom.Phase.FINISHED, r.phase, "Wall-time deadline must not stretch after a stall");
    }

    @Test void realDamageDoesNotResetStealOrApplyStagger() {
        GameRoom r = placed();
        PlayerState owner = r.players.get(0), breaker = r.players.get(1), attacker = r.players.get(2);
        breaker.x = 0; attacker.x = .5;
        r.action(breaker, "INTERACT_START", 0, 0, 100_001);
        r.update(100_100, .05);
        assertTrue(progress(r, breaker, 100_100) > 0);
        double before = progress(r, breaker, 100_100);
        attacker.charging = true;
        attacker.chargeSince = 100_200 - GameRules.MAX_CHARGE_MS;
        r.action(attacker, "EXPLODE", 0, 0, 100_200);
        assertTrue(breaker.hp < 100 && breaker.hp > 0);
        assertEquals(before, progress(r, breaker, 100_200), .0001);
        assertEquals(0, breaker.staggerUntil);
        assertEquals(100, owner.hp);
    }

    @Test void spectatorUsesLivingPerspectiveForPlayersPickupsAndChangesAfterDeath() {
        GameRoom r = room(GameMode.QUICK, 4);
        PlayerState dead = r.players.get(0), followed = r.players.get(1), hidden = r.players.get(2);
        dead.alive = false; dead.x = -40;
        followed.x = 40; hidden.x = 43;
        r.bushes = List.of(new GameMap.Bush(43, 0, 1));
        r.orbs.add(new GameRoom.Pickup(999, 40, 0));
        r.orbs.add(new GameRoom.Pickup(998, -40, 0));
        Map<String,Object> snapshot = r.snapshot(101_000, dead);
        assertEquals(followed.id, snapshot.get("spectatorTargetId"));
        assertTrue(r.canSee(dead, followed));
        assertFalse(r.canSee(dead, hidden));
        assertEquals(List.of(new GameRoom.Pickup(999, 40, 0)), snapshot.get("orbs"));
        assertEquals("", r.snapshot(101_000, followed).get("spectatorTargetId"));
        followed.alive = false;
        assertEquals(hidden.id, r.snapshot(101_100, dead).get("spectatorTargetId"));
        hidden.connected = false;
        assertEquals(r.players.get(3).id, r.snapshot(101_200, dead).get("spectatorTargetId"));
    }
}
