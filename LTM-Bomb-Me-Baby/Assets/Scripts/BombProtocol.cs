using System;

[Serializable] public class BombMessage {
    public string type;
    public long seq;
    public string name;
    public string resumeToken;
    public string code;
    public string mode;
    public bool ready;
    public float directionX;
    public float directionZ;
    public long clientTime;
}

[Serializable] public class ServerMessage {
    public string type;
    public string code;
    public string playerId;
    public string winnerId;
    public string resumeToken;
    public string reason;
    public string from;
    public string to;
    public string by;
    public int pickupId;
    public long seq;
    public long lastSeq;
    public long tick;
    public long clientTime;
    public long serverTime;
    public long processingMicros;
    public long reconnectMs;
    public long reconnectDeadline;
    public float x;
    public float z;
    public float directionX;
    public float directionZ;
    public float radius;
    public float amount;
    public float strength;
    public bool shielded;
    public bool visible;
    public float arenaSize;
    public string mapId;
    public string mapName;
    public string mode;
    public string syncMode;
    public string demoMap;
    public int chapterIndex;
    public bool journeyComplete;
    public string[] winnerIds;
    public ScoreData[] scores;
    public PlayerData[] players;
    public PickupData[] orbs;
    public PickupData[] cores;
    public ObstacleData[] obstacles;
    public BushData[] bushes;
    public string phase;
    public ObjectiveData objective;
    // MATCHMAKING: status SEARCHING (with queue size) or CANCELLED.
    public string status;
    public int waiting;
    public int minPlayers;
    public int maxPlayers;
}

/// Journey shard objective in STATE_UPDATE. JsonUtility never leaves it null, so an empty
/// state means the snapshot had no objective (Quick mode).
[Serializable] public class ObjectiveData {
    public string state;          // WAITING, GROUND, CARRIED, PLACED
    public float x;
    public float z;
    public string carrierId;
    public string ownerId;
    public string challengerId;
    public float capture;         // 0..1 progress of the current challenger
    public SealBreakerData[] sealBreakers;
    public int fireLock;
    public int fireLockMax;
    public float carrierProtection;
    public float pedestalProtection;
    public float pedestalOverheat;
    public string pickupPriorityId;
    public float pickupPriority;
    public float pickupUnlock;
    public float pedestalX;
    public float pedestalZ;
    public float pedestalRadius;
    public float timeLeft;        // seconds until the chapter ends
    public float spawnIn;         // seconds until the shard appears

    public bool Active => !string.IsNullOrEmpty(state);

    public SealBreakerData Breaker(string playerId) {
        if (sealBreakers == null || string.IsNullOrEmpty(playerId)) return null;
        foreach (SealBreakerData breaker in sealBreakers)
            if (breaker != null && breaker.playerId == playerId) return breaker;
        return null;
    }
}

[Serializable] public class SealBreakerData {
    public string playerId;
    public float progress;
    public float stagger;
    public float resetFlash;
}

[Serializable] public class RoomEnvelope {
    public string type;
    public RoomData room;
}

[Serializable] public class RoomData {
    public string code;
    public string phase;
    public string mapId;
    public string mapName;
    public float arenaSize;
    public string mode;
    public int chapterIndex;
    public bool journeyComplete;
    public ScoreData[] scores;
    public int minPlayers;
    public int maxPlayers;
    public LobbyPlayer[] players;
}

[Serializable] public class LobbyPlayer {
    public string id;
    public string name;
    public bool ready;
    public bool connected;
    public float reconnectRemaining;
}

[Serializable] public class ScoreData {
    public string playerId;
    public string name;
    public int points;
    public int roundWins;      // shards brought home
    public int eliminations;
}

[Serializable] public class PlayerData {
    public string id;
    public string name;
    public float x;
    public float z;
    public float hp;
    public float size;
    public float baseSize;
    public bool alive;
    public bool connected;
    public float reconnectRemaining;
    public bool visible;
    public bool charging;
    public float charge;
    public float chargeCooldown;
    public bool shield;
    public float shieldCooldown;
    public bool boost;
    public float boostCooldown;
    public float stagger;
    public int kills;
}

[Serializable] public class PickupData {
    public int id;
    public float x;
    public float z;
}

[Serializable] public class ObstacleData {
    public float x;
    public float z;
    public float radius;
}

[Serializable] public class BushData {
    public float x;
    public float z;
    public float radius;
}
