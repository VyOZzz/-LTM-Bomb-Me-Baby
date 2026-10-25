/// Client copy of the layouts in server/src/main/java/bombmebaby/GameMap.java, used only by the
/// scene preview. In a match the server still sends the real layout; keep these numbers in sync.
static class BombMaps {
    public sealed class Layout {
        public string id, name;
        public float halfSize;
        public ObstacleData[] obstacles;
        public BushData[] bushes;
    }

    static readonly Layout[] All = {
        new Layout {
            id = "meadow", name = "Đồng cỏ rộng", halfSize = 48,
            obstacles = new[] {
                O(-28,-24,1.8f), O(-11,-30,1.5f), O(10,-27,2.0f),
                O(31,-20,1.6f), O(-32,-2,2.1f), O(-12,-7,1.4f),
                O(14,2,1.8f), O(34,7,1.5f), O(-25,24,1.7f),
                O(-3,29,2.0f), O(22,27,1.6f), O(4,13,1.3f) },
            bushes = new[] {
                B(-39,-31,2.2f), B(-21,-35,2.0f), B(2,-36,2.1f),
                B(27,-34,2.3f), B(39,-8,2.0f), B(27,11,2.2f),
                B(38,31,2.4f), B(13,37,2.1f), B(-14,38,2.2f),
                B(-38,29,2.3f), B(-40,8,2.0f), B(-17,11,1.9f),
                B(1,-15,2.0f), B(12,19,1.9f) }
        },
        new Layout {
            id = "crater", name = "Miệng núi lửa", halfSize = 52,
            obstacles = new[] {
                O(-9,-10,2.4f), O(8,-11,2.2f), O(13,4,2.1f),
                O(5,14,2.3f), O(-11,12,2.0f), O(-15,0,2.2f),
                O(-35,-29,1.6f), O(31,-33,1.8f), O(38,-5,1.5f),
                O(33,29,1.9f), O(-2,37,1.7f), O(-37,25,1.8f) },
            bushes = new[] {
                B(-44,-39,2.2f), B(-18,-41,2.0f), B(16,-43,2.3f),
                B(43,-24,2.1f), B(45,13,2.4f), B(18,42,2.2f),
                B(-20,44,2.3f), B(-45,34,2.1f), B(-46,-5,2.2f),
                B(-25,-17,2.0f), B(26,-16,2.0f), B(27,15,2.1f),
                B(-28,15,2.2f), B(0,-25,1.9f), B(1,26,2.0f) }
        },
        new Layout {
            id = "crossroads", name = "Ngã tư bỏ hoang", halfSize = 50,
            obstacles = new[] {
                O(-31,-31,1.8f), O(-20,-20,1.6f), O(-9,-9,1.7f),
                O(9,9,1.7f), O(20,20,1.6f), O(31,31,1.8f),
                O(31,-31,1.8f), O(20,-20,1.6f), O(9,-9,1.7f),
                O(-9,9,1.7f), O(-20,20,1.6f), O(-31,31,1.8f) },
            bushes = new[] {
                B(-42,-21,2.1f), B(-42,4,2.2f), B(-41,29,2.0f),
                B(-22,42,2.3f), B(3,42,2.0f), B(29,41,2.2f),
                B(42,22,2.1f), B(42,-4,2.3f), B(41,-29,2.0f),
                B(20,-42,2.2f), B(-5,-42,2.0f), B(-30,-41,2.3f),
                B(-25,1,1.9f), B(25,-1,1.9f), B(1,25,2.0f),
                B(-1,-25,2.0f) }
        }
    };

    public static Layout ById(string id) {
        foreach (Layout layout in All) if (layout.id == id) return layout;
        return All[0];
    }

    static ObstacleData O(float x, float z, float radius) => new ObstacleData { x = x, z = z, radius = radius };
    static BushData B(float x, float z, float radius) => new BushData { x = x, z = z, radius = radius };
}
