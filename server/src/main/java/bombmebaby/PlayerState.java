package bombmebaby;

import java.util.UUID;

final class PlayerState {
    final String id = UUID.randomUUID().toString();
    final String name;
    final String resumeToken = UUID.randomUUID().toString();
    volatile BombServer.Connection connection;
    GameRoom room;
    double x, z, dx, dz, hp = 100, size = 1, baseSize = 1;
    boolean ready, alive = true, charging, connected = true;
    long chargeSince, shieldUntil, shieldReadyAt, boostUntil, boostReadyAt, disconnectedAt;
    long lastSeq = -1;
    int kills;

    PlayerState(String name, BombServer.Connection connection) {
        this.name = name;
        this.connection = connection;
    }

    void send(Object message) {
        BombServer.Connection current = connection;
        if (current != null) current.send(message);
    }
}
