package bombmebaby;

import java.util.ArrayList;
import java.util.List;
import java.util.Random;

/** Immutable server-authoritative layouts shared with clients through snapshots. */
final class GameMap {
    record Obstacle(double x, double z, double radius) {}
    record Bush(double x, double z, double radius) {}

    private static final List<GameMap> MAPS = List.of(
        new GameMap(
            "meadow", "Đồng cỏ rộng", 48,
            List.of(
                new Obstacle(-28,-24,1.8), new Obstacle(-11,-30,1.5), new Obstacle(10,-27,2.0),
                new Obstacle(31,-20,1.6), new Obstacle(-32,-2,2.1), new Obstacle(-12,-7,1.4),
                new Obstacle(14,2,1.8), new Obstacle(34,7,1.5), new Obstacle(-25,24,1.7),
                new Obstacle(-3,29,2.0), new Obstacle(22,27,1.6), new Obstacle(4,13,1.3)),
            List.of(
                new Bush(-39,-31,2.2), new Bush(-21,-35,2.0), new Bush(2,-36,2.1),
                new Bush(27,-34,2.3), new Bush(39,-8,2.0), new Bush(27,11,2.2),
                new Bush(38,31,2.4), new Bush(13,37,2.1), new Bush(-14,38,2.2),
                new Bush(-38,29,2.3), new Bush(-40,8,2.0), new Bush(-17,11,1.9),
                new Bush(1,-15,2.0), new Bush(12,19,1.9))),
        new GameMap(
            "crater", "Miệng núi lửa", 52,
            List.of(
                new Obstacle(-9,-10,2.4), new Obstacle(8,-11,2.2), new Obstacle(13,4,2.1),
                new Obstacle(5,14,2.3), new Obstacle(-11,12,2.0), new Obstacle(-15,0,2.2),
                new Obstacle(-35,-29,1.6), new Obstacle(31,-33,1.8), new Obstacle(38,-5,1.5),
                new Obstacle(33,29,1.9), new Obstacle(-2,37,1.7), new Obstacle(-37,25,1.8)),
            List.of(
                new Bush(-44,-39,2.2), new Bush(-18,-41,2.0), new Bush(16,-43,2.3),
                new Bush(43,-24,2.1), new Bush(45,13,2.4), new Bush(18,42,2.2),
                new Bush(-20,44,2.3), new Bush(-45,34,2.1), new Bush(-46,-5,2.2),
                new Bush(-25,-17,2.0), new Bush(26,-16,2.0), new Bush(27,15,2.1),
                new Bush(-28,15,2.2), new Bush(0,-25,1.9), new Bush(1,26,2.0))),
        new GameMap(
            "crossroads", "Ngã tư bỏ hoang", 50,
            List.of(
                new Obstacle(-31,-31,1.8), new Obstacle(-20,-20,1.6), new Obstacle(-9,-9,1.7),
                new Obstacle(9,9,1.7), new Obstacle(20,20,1.6), new Obstacle(31,31,1.8),
                new Obstacle(31,-31,1.8), new Obstacle(20,-20,1.6), new Obstacle(9,-9,1.7),
                new Obstacle(-9,9,1.7), new Obstacle(-20,20,1.6), new Obstacle(-31,31,1.8)),
            List.of(
                new Bush(-42,-21,2.1), new Bush(-42,4,2.2), new Bush(-41,29,2.0),
                new Bush(-22,42,2.3), new Bush(3,42,2.0), new Bush(29,41,2.2),
                new Bush(42,22,2.1), new Bush(42,-4,2.3), new Bush(41,-29,2.0),
                new Bush(20,-42,2.2), new Bush(-5,-42,2.0), new Bush(-30,-41,2.3),
                new Bush(-25,1,1.9), new Bush(25,-1,1.9), new Bush(1,25,2.0),
                new Bush(-1,-25,2.0)))
    );

    final String id;
    final String name;
    final double halfSize;
    final List<Obstacle> obstacles;
    final List<Bush> bushes;

    private GameMap(String id, String name, double halfSize,
                    List<Obstacle> obstacles, List<Bush> bushes) {
        this.id = id;
        this.name = name;
        this.halfSize = halfSize;
        this.obstacles = obstacles;
        this.bushes = bushes;
    }

    static List<GameMap> all() { return MAPS; }

    static GameMap random(Random random, String previousId) {
        List<GameMap> choices = new ArrayList<>(MAPS);
        if (previousId != null && choices.size() > 1)
            choices.removeIf(map -> map.id.equals(previousId));
        return choices.get(random.nextInt(choices.size()));
    }
}
