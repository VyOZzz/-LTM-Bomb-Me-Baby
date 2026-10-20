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
    public float x;
    public float z;
    public float radius;
    public float amount;
    public bool visible;
    public float arenaSize;
    public string mapId;
    public string mapName;
    public string mode;
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
    public int maxPlayers;
    public LobbyPlayer[] players;
}

[Serializable] public class LobbyPlayer {
    public string id;
    public string name;
    public bool ready;
    public bool connected;
}

[Serializable] public class ScoreData {
    public string playerId;
    public string name;
    public int points;
    public int roundWins;
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
    public bool visible;
    public bool charging;
    public float charge;
    public bool shield;
    public float shieldCooldown;
    public bool boost;
    public float boostCooldown;
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
