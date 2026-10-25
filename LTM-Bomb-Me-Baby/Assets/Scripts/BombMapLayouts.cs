using System;

/// Client-side copy of the server-authoritative static layouts. The editor preview uses these
/// values so its rocks and bushes match the map received from the server during play.
public static class BombMapLayouts {
    public sealed class Layout {
        public readonly string id;
        public readonly float arenaSize;
        public readonly ObstacleData[] obstacles;
        public readonly BushData[] bushes;

        public Layout(string id, float arenaSize, ObstacleData[] obstacles, BushData[] bushes) {
            this.id = id;
            this.arenaSize = arenaSize;
            this.obstacles = obstacles;
            this.bushes = bushes;
        }
    }

    static readonly Layout Meadow = new Layout("meadow", 48,
        Obstacles(
            -28,-24,1.8f, -11,-30,1.5f, 10,-27,2f, 31,-20,1.6f,
            -32,-2,2.1f, -12,-7,1.4f, 14,2,1.8f, 34,7,1.5f,
            -25,24,1.7f, -3,29,2f, 22,27,1.6f, 4,13,1.3f),
        Bushes(
            -39,-31,2.2f, -21,-35,2f, 2,-36,2.1f, 27,-34,2.3f,
            39,-8,2f, 27,11,2.2f, 38,31,2.4f, 13,37,2.1f,
            -14,38,2.2f, -38,29,2.3f, -40,8,2f, -17,11,1.9f,
            1,-15,2f, 12,19,1.9f));

    static readonly Layout Crater = new Layout("crater", 52,
        Obstacles(
            -9,-10,2.4f, 8,-11,2.2f, 13,4,2.1f, 5,14,2.3f,
            -11,12,2f, -15,0,2.2f, -35,-29,1.6f, 31,-33,1.8f,
            38,-5,1.5f, 33,29,1.9f, -2,37,1.7f, -37,25,1.8f),
        Bushes(
            -44,-39,2.2f, -18,-41,2f, 16,-43,2.3f, 43,-24,2.1f,
            45,13,2.4f, 18,42,2.2f, -20,44,2.3f, -45,34,2.1f,
            -46,-5,2.2f, -25,-17,2f, 26,-16,2f, 27,15,2.1f,
            -28,15,2.2f, 0,-25,1.9f, 1,26,2f));

    static readonly Layout Crossroads = new Layout("crossroads", 50,
        Obstacles(
            -31,-31,1.8f, -20,-20,1.6f, -9,-9,1.7f, 9,9,1.7f,
            20,20,1.6f, 31,31,1.8f, 31,-31,1.8f, 20,-20,1.6f,
            9,-9,1.7f, -9,9,1.7f, -20,20,1.6f, -31,31,1.8f),
        Bushes(
            -42,-21,2.1f, -42,4,2.2f, -41,29,2f, -22,42,2.3f,
            3,42,2f, 29,41,2.2f, 42,22,2.1f, 42,-4,2.3f,
            41,-29,2f, 20,-42,2.2f, -5,-42,2f, -30,-41,2.3f,
            -25,1,1.9f, 25,-1,1.9f, 1,25,2f, -1,-25,2f));

    public static Layout Get(string mapId) {
        switch ((mapId ?? "").ToLowerInvariant()) {
            case "meadow": return Meadow;
            case "crossroads": return Crossroads;
            case "crater": return Crater;
            default: throw new ArgumentException("Unknown map: " + mapId, nameof(mapId));
        }
    }

    static ObstacleData[] Obstacles(params float[] values) {
        var result = new ObstacleData[values.Length / 3];
        for (int i = 0; i < result.Length; i++)
            result[i] = new ObstacleData { x = values[i * 3], z = values[i * 3 + 1], radius = values[i * 3 + 2] };
        return result;
    }

    static BushData[] Bushes(params float[] values) {
        var result = new BushData[values.Length / 3];
        for (int i = 0; i < result.Length; i++)
            result[i] = new BushData { x = values[i * 3], z = values[i * 3 + 1], radius = values[i * 3 + 2] };
        return result;
    }
}
