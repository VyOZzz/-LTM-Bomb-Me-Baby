using System;
using System.IO;
using UnityEngine;

[Serializable]
public sealed class BombStoryFile {
    public BombStoryMap[] maps = Array.Empty<BombStoryMap>();
    public BombStoryEvent[] events = Array.Empty<BombStoryEvent>();
}

[Serializable]
public sealed class BombStoryMap {
    public string mapId;
    public string chapter;
    public string title;
    public string intro;
    public string hint;
}

[Serializable]
public sealed class BombStoryEvent {
    public string type;
    public string title;
    public string self;
    public string other;
    public float duration = 2.4f;
}

public sealed class BombStory {
    readonly BombStoryFile data;

    BombStory(BombStoryFile data) {
        this.data = data ?? new BombStoryFile();
    }

    public static BombStory Load(out string error) {
        error = "";
        string path = Path.Combine(Application.streamingAssetsPath, "story.json");
        try {
            if (!File.Exists(path)) {
                error = "Không tìm thấy StreamingAssets/story.json.";
                return new BombStory(null);
            }
            BombStoryFile parsed = JsonUtility.FromJson<BombStoryFile>(File.ReadAllText(path));
            if (parsed == null) throw new InvalidDataException("JSON rỗng hoặc sai cấu trúc.");
            return new BombStory(parsed);
        } catch (Exception exception) {
            error = "Không đọc được story.json: " + exception.Message;
            return new BombStory(null);
        }
    }

    public BombStoryMap FindMap(string mapId) {
        if (string.IsNullOrEmpty(mapId) || data.maps == null) return null;
        foreach (BombStoryMap map in data.maps)
            if (map != null && string.Equals(map.mapId, mapId, StringComparison.OrdinalIgnoreCase)) return map;
        return null;
    }

    public BombStoryEvent FindEvent(string type) {
        if (string.IsNullOrEmpty(type) || data.events == null) return null;
        foreach (BombStoryEvent item in data.events)
            if (item != null && string.Equals(item.type, type, StringComparison.OrdinalIgnoreCase)) return item;
        return null;
    }

    public static string Format(string template, ServerMessage message, Func<string, string> playerName) {
        if (string.IsNullOrEmpty(template)) return "";
        string playerId = !string.IsNullOrEmpty(message.playerId) ? message.playerId : message.to;
        string sourceId = !string.IsNullOrEmpty(message.by) ? message.by : message.from;
        return template
            .Replace("{player}", playerName(playerId))
            .Replace("{source}", playerName(sourceId))
            .Replace("{amount}", Mathf.Max(0, message.amount).ToString("0"))
            .Replace("{reason}", ReasonText(message.reason));
    }

    static string ReasonText(string reason) {
        switch (reason) {
            case "disconnect": return "mất kết nối";
            case "reflection": return "sát thương phản lại";
            case "explosion": return "vụ nổ";
            default: return "trận đấu";
        }
    }
}
