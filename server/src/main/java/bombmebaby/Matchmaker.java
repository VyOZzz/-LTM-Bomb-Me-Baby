package bombmebaby;

import java.util.ArrayList;
import java.util.EnumMap;
import java.util.List;
import java.util.Map;

/**
 * FIFO matchmaking queue per game mode. It only uses its own monitor and never calls out
 * while holding it, so it adds no edge to the player -> room lock order.
 */
final class Matchmaker {
    record Match(GameMode mode, List<PlayerState> players) {}

    private final Map<GameMode,List<PlayerState>> queues = new EnumMap<>(GameMode.class);

    /** Queues the player; returns everyone now waiting for that mode, or null if already queued. */
    synchronized List<PlayerState> enqueue(PlayerState player, GameMode mode) {
        if (modeOf(player) != null) return null;
        List<PlayerState> queue = queues.computeIfAbsent(mode, m -> new ArrayList<>());
        queue.add(player);
        return List.copyOf(queue);
    }

    /** Puts the players of a match that fell apart back at the head of their queue. */
    synchronized List<PlayerState> requeue(GameMode mode, List<PlayerState> players) {
        List<PlayerState> queue = queues.computeIfAbsent(mode, m -> new ArrayList<>());
        queue.addAll(0, players);
        return List.copyOf(queue);
    }

    /** Mode the player is waiting for, or null. */
    synchronized GameMode modeOf(PlayerState player) {
        for (var entry : queues.entrySet()) if (entry.getValue().contains(player)) return entry.getKey();
        return null;
    }

    /** Removes the player; returns who is left waiting for that mode, or null if not queued. */
    synchronized List<PlayerState> remove(PlayerState player) {
        GameMode mode = modeOf(player);
        if (mode == null) return null;
        List<PlayerState> queue = queues.get(mode);
        queue.remove(player);
        return List.copyOf(queue);
    }

    /** Start as soon as the minimum group exists; finding a match must never look stuck. */
    synchronized List<Match> takeMatches(long now) {
        List<Match> matches = new ArrayList<>();
        for (var entry : queues.entrySet()) {
            GameMode mode = entry.getKey();
            List<PlayerState> queue = entry.getValue();
            queue.removeIf(p -> !p.connected || p.room != null);
            while (queue.size() >= GameRules.MIN_PLAYERS) {
                int count = Math.min(GameRules.MATCH_MAX_PLAYERS, queue.size());
                matches.add(take(mode, queue, count));
            }
        }
        return matches;
    }

    private Match take(GameMode mode, List<PlayerState> queue, int count) {
        List<PlayerState> players = List.copyOf(queue.subList(0, count));
        queue.subList(0, count).clear();
        return new Match(mode, players);
    }
}
