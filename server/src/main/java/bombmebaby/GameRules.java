package bombmebaby;

final class GameRules {
    static final double ARENA = 12.0;
    static final double PLAYER_RADIUS = 0.48;
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
        return 1.5 + size * 1.15 + charge * 1.25;
    }

    static double damage(double size, double charge, double distance, double radius) {
        if (distance > radius) return 0;
        double power = 7 + 25 * (size - 1) + 26 * charge;
        return Math.max(0, power * (1 - 0.75 * distance / radius));
    }
}
