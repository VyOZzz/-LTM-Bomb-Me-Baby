package bombmebaby;

import java.util.ArrayList;
import java.util.EnumMap;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

/**
 * FIFO matchmaking queue per game mode. It only uses its own monitor and never calls out
 * while holding it, so it adds no edge to the player -> room lock order.
 */
final class Matchmaker {
    record Ticket(PlayerState player, GameMode mode, long generation, long queuedAt) {}
    record Match(GameMode mode, List<Ticket> tickets) {
        List<PlayerState> players() { return tickets.stream().map(Ticket::player).toList(); }
    }

    private final Map<GameMode,List<Ticket>> queues = new EnumMap<>(GameMode.class);
    // Includes tickets selected for a match but not yet committed to a room.
    private final Map<PlayerState,Ticket> requests = new HashMap<>();
    private long generation;

    /** Queues the player; returns everyone now waiting for that mode, or null if already queued. */
    synchronized List<PlayerState> enqueue(PlayerState player, GameMode mode, long now) {
        if (modeOf(player) != null) return null;
        Ticket ticket = new Ticket(player, mode, ++generation, now);
        requests.put(player, ticket);
        queues.computeIfAbsent(mode, m -> new ArrayList<>()).add(ticket);
        return waiting(mode);
    }

    /** Puts the players of a match that fell apart back at the head of their queue. */
    synchronized List<PlayerState> requeue(Match match) {
        List<Ticket> rest = new ArrayList<>();
        for (Ticket ticket : match.tickets()) {
            if (!isCurrent(ticket)) continue;
            PlayerState player = ticket.player();
            if (player.connected && player.room == null) rest.add(ticket);
            else complete(ticket);
        }
        queues.computeIfAbsent(match.mode(), m -> new ArrayList<>()).addAll(0, rest);
        return waiting(match.mode());
    }

    synchronized boolean isCurrent(Ticket ticket) { return ticket.equals(requests.get(ticket.player())); }

    synchronized void complete(Ticket ticket) { requests.remove(ticket.player(), ticket); }

    /** Mode the player is waiting for, or null. */
    synchronized GameMode modeOf(PlayerState player) {
        Ticket ticket = requests.get(player);
        return ticket == null ? null : ticket.mode();
    }

    /** Milliseconds until the head group starts without a full lobby, or -1 while fewer than MIN_PLAYERS wait. */
    synchronized long startsInMs(GameMode mode, long now) {
        List<Ticket> queue = queues.get(mode);
        if (queue == null || queue.size() < GameRules.MIN_PLAYERS) return -1;
        return Math.max(0, startDeadline(queue) - now);
    }

    /**
     * The countdown runs from the moment a minimum group first existed, so a player who waited
     * alone does not make the next arrival start instantly. Requeued tickets keep their time.
     */
    private static long startDeadline(List<Ticket> queue) {
        return queue.get(GameRules.MIN_PLAYERS - 1).queuedAt() + GameRules.MATCH_WAIT_MS;
    }

    /** Removes the player; returns who is left waiting for that mode, or null if not queued. */
    synchronized List<PlayerState> remove(PlayerState player) {
        Ticket ticket = requests.remove(player);
        if (ticket == null) return null;
        queues.get(ticket.mode()).remove(ticket);
        return waiting(ticket.mode());
    }

    private List<PlayerState> waiting(GameMode mode) {
        return queues.get(mode).stream().map(Ticket::player).toList();
    }

    /** Full groups start at once; a smaller group starts once its countdown has run out. */
    synchronized List<Match> takeMatches(long now) {
        List<Match> matches = new ArrayList<>();
        for (var entry : queues.entrySet()) {
            GameMode mode = entry.getKey();
            List<Ticket> queue = entry.getValue();
            queue.removeIf(ticket -> {
                PlayerState p = ticket.player();
                if (p.connected && p.room == null) return false;
                complete(ticket);
                return true;
            });
            while (queue.size() >= GameRules.MATCH_MAX_PLAYERS)
                matches.add(take(mode, queue, GameRules.MATCH_MAX_PLAYERS));
            if (queue.size() >= GameRules.MIN_PLAYERS && now >= startDeadline(queue))
                matches.add(take(mode, queue, queue.size()));
        }
        return matches;
    }

    private Match take(GameMode mode, List<Ticket> queue, int count) {
        List<Ticket> tickets = List.copyOf(queue.subList(0, count));
        queue.subList(0, count).clear();
        return new Match(mode, tickets);
    }
}
