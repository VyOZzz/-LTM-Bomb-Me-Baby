package bombmebaby;

final class GameRules {
    static final int MAX_PLAYERS = 10;
    static final int MIN_PLAYERS = 2;
    // Matchmaking starts immediately once MIN_PLAYERS are waiting in the same mode.
    static final int MATCH_MAX_PLAYERS = 4;
    static final double ENERGY_SIZE_VALUE = 0.12;
    static final int MAX_EXPLOSION_ENERGY_DROPS = 12;
    static final int INITIAL_ORBS = 110;
    static final int MAX_ORBS = 150;
    static final double MIN_SPAWN_DISTANCE = 28.0;
    static final double PLAYER_VISIBILITY_DISTANCE = 26.0;
    // Slightly smaller than the rendered sphere so movement near scenery feels forgiving.
    static final double PLAYER_RADIUS = 0.40;
    static final double MAX_SIZE = 2.6;
    static final double MAX_BASE = 1.4;
    /** Full charge is intentionally short enough to keep fights decisive. */
    static final long MAX_CHARGE_MS = 4_000;
    static final long BOMB_COOLDOWN_MS = 2_500;
    static final long SHIELD_MS = 1_500;
    static final long SHIELD_COOLDOWN_MS = 9_000;
    static final long BOOST_MS = 900;
    static final long BOOST_COOLDOWN_MS = 5_000;
    static final long RECONNECT_MS = 10_000;
    static final long CONNECTION_TIMEOUT_MS = 6_000;
    static final long WRITE_STALL_MS = 3_000;
    static final long MOVEMENT_STALE_MS = 500;
    static final double ENERGY_HEAL = 1;
    static final long ENERGY_SPEED_BOOST_MS = 650;
    static final double ENERGY_SPEED_FACTOR = 1.12;
    /** One balance knob for every normal explosion in every game mode. */
    static final double GLOBAL_DAMAGE_MULTIPLIER = 1.0;
    /** A size-1 bomb at full charge and point-blank range removes exactly 80 HP. */
    static final double FULL_CHARGE_DAMAGE = 80.0;
    static final double TAP_DAMAGE = 12.0;

    // Server-authoritative explosion impulse. Values are velocities in world units/second;
    // exponential decay keeps an ordinary blast displacement around 1-2.5 world units.
    static final double KNOCKBACK_MIN_SPEED = 5.5;
    static final double KNOCKBACK_MAX_SPEED = 16.0;
    static final double KNOCKBACK_DECAY_PER_SECOND = 7.0;
    static final double KNOCKBACK_STOP_SPEED = 0.08;
    static final double SHIELD_KNOCKBACK_FACTOR = 0.18;
    static final long SEAL_STAGGER_MS = 450;
    static final long SEAL_STAGGER_IMMUNITY_MS = 700;

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
        double power = (TAP_DAMAGE + (FULL_CHARGE_DAMAGE - TAP_DAMAGE) * chargedPower)
            * Math.max(1, size) * GLOBAL_DAMAGE_MULTIPLIER;
        return Math.max(0, power * (1 - 0.75 * distance / radius));
    }

    static double knockbackSpeed(double charge, double damage, double distance,
                                 double radius, double targetSize, boolean shielded) {
        double chargeFactor = clamp(charge, 0, 1);
        double damageFactor = clamp(damage / 45.0, 0, 1);
        double distanceFactor = radius <= 0 ? 0 : clamp(1 - distance / radius, 0, 1);
        double raw = KNOCKBACK_MIN_SPEED
            + 4.0 * chargeFactor + 3.0 * damageFactor + 3.5 * distanceFactor;
        double sizeAdjusted = raw / Math.sqrt(Math.max(0.5, targetSize));
        double speed = clamp(sizeAdjusted, KNOCKBACK_MIN_SPEED, KNOCKBACK_MAX_SPEED);
        return shielded ? speed * SHIELD_KNOCKBACK_FACTOR : speed;
    }
}
