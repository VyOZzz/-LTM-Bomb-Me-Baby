package bombmebaby;

/** Room rules selected when the room is created. */
enum GameMode {
    QUICK,
    JOURNEY;

    static GameMode fromProtocol(String value) {
        if (value == null || value.isBlank()) return QUICK;
        for (GameMode mode : values()) {
            if (mode.name().equalsIgnoreCase(value.trim())) return mode;
        }
        return null;
    }
}
