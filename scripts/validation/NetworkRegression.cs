using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Text.Json;

// Queue tests compile the production network/DTO source without Unity engine dependencies.
namespace UnityEngine { public static class JsonUtility {
    public static string ToJson(object obj) => JsonSerializer.Serialize(obj, new JsonSerializerOptions { IncludeFields = true });
} }

class Program {
    static int checks;
    static readonly MethodInfo Enqueue = typeof(BombNetwork).GetMethod("EnqueueIncoming", BindingFlags.Instance | BindingFlags.NonPublic);
    static void Check(bool condition, string description) {
        if (!condition) throw new Exception(description);
        checks++; Console.WriteLine("PASS " + description);
    }
    static string Snapshot(int tick, bool geometry = false) => "{\"type\":\"STATE_UPDATE\",\"phase\":\"PLAYING\",\"tick\":" + tick + (geometry ? ",\"obstacles\":[]" : "") + "}";
    static string Event(string type) => "{\"type\":\"" + type + "\"}";
    static void Feed(BombNetwork network, params string[] frames) {
        foreach (string frame in frames) Enqueue.Invoke(network, new object[] { frame });
    }
    static List<string> Drain(BombNetwork network) {
        var frames = new List<string>();
        while (network.TryReceive(out string frame)) frames.Add(frame);
        return frames;
    }
    static void Main() {
        using var network = new BombNetwork();
        foreach (string barrier in new[] { "GAME_OVER", "LEFT_ROOM", "GAME_START", "ROOM_STATE" }) {
            string[] sent = { Snapshot(42), Event(barrier), Snapshot(1) };
            Feed(network, sent);
            Check(Drain(network).SequenceEqual(sent), "TCP ordering around " + barrier);
        }
        Feed(network, Snapshot(1), Snapshot(2), Snapshot(3));
        Check(Drain(network).SequenceEqual(new[] { Snapshot(3) }), "Adjacent snapshots coalesce");
        Feed(network, Snapshot(1, true), Snapshot(2), Snapshot(3));
        Check(Drain(network).SequenceEqual(new[] { Snapshot(1, true), Snapshot(3) }), "Recovery geometry survives optimized snapshots");
        for (int i = 0; i < 256; i++) Feed(network, Event("ACK"));
        bool overflow = false;
        try { Feed(network, Event("GAME_OVER")); }
        catch (TargetInvocationException e) when (e.InnerException is IOException) { overflow = true; }
        Check(overflow && Drain(network).Count == 256, "Overflow requests reconnect instead of silently dropping events");
        Check(BombResult.Resolve("me", "", null) == MatchOutcome.Draw, "No winner is a draw");
        Check(BombResult.Resolve("me", "me", null) == MatchOutcome.Win, "Single winner wins");
        Check(BombResult.Resolve("me", "other", null) == MatchOutcome.Loss, "Opponent winner loses");
        Check(BombResult.Resolve("me", "", new[] { "me", "other" }) == MatchOutcome.SharedWin, "Tied leader shares victory");
        Check(BombResult.Resolve("third", "", new[] { "me", "other" }) == MatchOutcome.Loss, "Non-leading player does not share tie");
        Console.WriteLine(checks + " C# regression checks passed.");
    }
}
