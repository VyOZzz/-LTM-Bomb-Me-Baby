package bombmebaby;

final class GameRules {
    static final int MAX_PLAYERS = 10;
    static final int INITIAL_ORBS = 110;
    static final int MAX_ORBS = 150;
    static final double MIN_SPAWN_DISTANCE = 28.0;
    static final double PLAYER_VISIBILITY_DISTANCE = 26.0;
    // Slightly smaller than the rendered sphere so movement near scenery feels forgiving.
    static final double PLAYER_RADIUS = 0.40;
    static final double MAX_SIZE = 2.6;
    static final double MAX_BASE = 1.4;
    static final long MAX_CHARGE_MS = 8_000;
    static final long SHIELD_MS = 1_500;
    static final long SHIELD_COOLDOWN_MS = 9_000;
    static final long BOOST_MS = 900;
    static final long BOOST_COOLDOWN_MS = 5_000;
    static final long RECONNECT_MS = 10_000;

    private GameRules() {}

    static double clamp(double value, double min, double max) {
        return Math.max(min, Math.min(max, value));
    }

    static double distance(double ax, double az, double bx, double bz) {
        return Math.hypot(ax - bx, az - bz);
    }

    static double blastRadius(double size, double charge) {
        double chargedPower = Math.pow(clamp(charge, 0, 1), 1.55);
        return 0.85 + size * 0.55 + chargedPower * 3.6;
    }

    static double damage(double size, double charge, double distance, double radius) {
        if (distance > radius) return 0;
        double chargedPower = Math.pow(clamp(charge, 0, 1), 1.35);
        double power = 7 + 25 * (size - 1) + 34 * chargedPower;
        return Math.max(0, power * (1 - 0.75 * distance / radius));
    }
}
