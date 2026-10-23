using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Opt-in Editor integration checks. No test mode is included in player builds.
[InitializeOnLoad]
public static class BombValidation {
    const string Pending = "BombValidation.Pending";
    static IEnumerator checks;
    static double deadline;
    static int passed;
    static BombNetwork peer;
    static BombGame game;
    static double nextPeerPing;
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    static BombValidation() {
        if (SessionState.GetBool(Pending, false)) EditorApplication.update += Tick;
    }

    public static void Run() {
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }

    static void Tick() {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try {
            if (checks == null) { deadline = EditorApplication.timeSinceStartup + 90; checks = Verify(); }
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Unity validation timed out");
            if (checks.MoveNext()) return;
            Debug.Log("BOMB_VALIDATION_PASS checks=" + passed);
            Finish(0);
        } catch (Exception e) { Debug.LogError("BOMB_VALIDATION_FAIL " + e); Finish(1); }
    }

    static void Finish(int code) {
        peer?.Dispose();
        SessionState.SetBool(Pending, false);
        EditorApplication.update -= Tick;
        EditorApplication.Exit(code);
    }

    static T Field<T>(string name) => (T)typeof(BombGame).GetField(name, Private).GetValue(game);
    static void Deliver(string json) => typeof(BombGame).GetMethod("Handle", Private).Invoke(game, new object[] { json });
    static void Assert(bool ok, string label) {
        if (!ok) throw new Exception(label);
        passed++; Debug.Log("BOMB_CHECK_PASS " + label);
    }
    static int RuntimeMaterials() => Resources.FindObjectsOfTypeAll<Material>().Count(m => m.name.StartsWith(BombMaterialOwner.Prefix));

    static IEnumerator Verify() {
        yield return null;
        game = UnityEngine.Object.FindFirstObjectByType<BombGame>();
        Assert(game != null, "Main scene bootstraps BombGame");
        // Runtime view + audio imports, using fixtures emitted by production Java GameRoom.
        string fixtures = BombBuild.Argument("-bombFixtures", "");
        Assert(File.Exists(fixtures), "Server fixtures exist");
        string[] packets = File.ReadAllLines(fixtures);
        int previousMaterials = -1;
        for (int cycle = 0; cycle < 3; cycle++) {
            foreach (string packet in packets) {
                Deliver(packet);
                var msg = JsonUtility.FromJson<ServerMessage>(packet);
                if (msg.type == "STATE_UPDATE") {
                    Assert(Field<string>("phase") == "PLAYING", "Map plays: " + msg.mapId);
                    Assert(GameObject.Find("Runtime Map - " + msg.mapId) != null, "Map geometry: " + msg.mapId);
                    Assert(Field<PlayerData[]>("latestPlayers").Length == 2, "Both player states reach Unity");
                    Assert(Field<RoomData>("room").chapterIndex == msg.chapterIndex, "HUD chapter follows server");
                }
                if (msg.type == "GAME_OVER") Assert(Field<string>("phase") == "FINISHED", "Result stays FINISHED");
                if (msg.type == "LEFT_ROOM") Assert(Field<RoomData>("room") == null, "Leave clears local room");
                yield return null; yield return null;
                if (cycle == 0 && msg.type == "STATE_UPDATE" && !string.IsNullOrEmpty(BombBuild.Argument("-bombScreenshots", ""))) {
                    for (int frame = 0; frame < 45; frame++) yield return null;
                    CaptureMap(msg.mapId);
                }
            }
            yield return null; yield return null;
            int materials = RuntimeMaterials();
            Debug.Log("BOMB_MATERIALS cycle=" + cycle + " count=" + materials + " " + string.Join(", ",
                Resources.FindObjectsOfTypeAll<Material>().Where(m => m.name.StartsWith(BombMaterialOwner.Prefix)).Select(m => m.name + "#" + m.GetInstanceID())));
            if (previousMaterials >= 0) Assert(materials <= previousMaterials, "Runtime materials stable after three maps: " + materials);
            previousMaterials = materials;
        }
        foreach (string clip in new[] { "music_menu", "music_meadow", "music_crossroads", "music_crater", "mix_energy", "mix_shield_hit", "mix_shield", "mix_boost", "mix_explosion_1", "mix_explosion_2", "mix_explosion_3" })
            Assert(Resources.Load<AudioClip>("Audio/" + clip) != null, "Audio imports: " + clip);

        VerifyDeadInputAndSpectating();

        // Two ordinary TCP clients talking to the packaged Java server.
        int port = int.Parse(BombBuild.Argument("-bombTestPort", "17778"));
        BombNetwork local = Field<BombNetwork>("network");
        local.ResumeToken = "";
        local.Connect("127.0.0.1", port, "Unity check");
        peer = new BombNetwork(); peer.Connect("127.0.0.1", port, "TCP peer");
        while (!local.Connected || !peer.Connected) { PumpPeer(); yield return null; }
        local.Send(new BombMessage { type = "CREATE_ROOM", mode = "QUICK" });
        while (Field<RoomData>("room") == null) { PumpPeer(); yield return null; }
        string code = Field<RoomData>("room").code;
        peer.Send(new BombMessage { type = "JOIN_ROOM", code = code });
        peer.Send(new BombMessage { type = "READY", ready = true });
        local.Send(new BombMessage { type = "READY", ready = true });
        while (Field<string>("phase") != "PLAYING" || Field<PlayerData[]>("latestPlayers").Length != 2) { PumpPeer(); yield return null; }
        Assert(true, "TCP create -> join -> ready -> live snapshot in Unity");
        peer.Send(new BombMessage { type = "LEAVE_ROOM" });
        while (Field<string>("phase") != "FINISHED") { PumpPeer(); yield return null; }
        Assert(Field<string>("winnerId") == Field<string>("myId"), "TCP opponent leave restores correct winning popup");
        // A snapshot from before the terminal event must never reopen the round.
        Deliver("{\"type\":\"STATE_UPDATE\",\"roomCode\":\"" + code + "\",\"roundIndex\":1,\"phase\":\"PLAYING\"}");
        Assert(Field<string>("phase") == "FINISHED", "Unity ignores stale playing snapshot after result");
        local.Send(new BombMessage { type = "LEAVE_ROOM" });
        while (Field<RoomData>("room") != null) { PumpPeer(); yield return null; }
        Assert(Field<string>("phase") == "MENU", "TCP leave returns Unity to menu");
    }

    static void PumpPeer() {
        while (peer.TryReceive(out string line)) {
            var msg = JsonUtility.FromJson<ServerMessage>(line);
            if (msg.type == "WELCOME") { peer.MarkWelcomed(); peer.ResumeToken = msg.resumeToken; }
            if (msg.type == "ERROR" || msg.type == "NETWORK_ERROR") throw new Exception("TCP peer: " + line);
        }
        if (peer.Connected && EditorApplication.timeSinceStartup >= nextPeerPing) {
            nextPeerPing = EditorApplication.timeSinceStartup + 1;
            peer.Send(new BombMessage { type = "PING" });
        }
    }

    static void Set(string name, object value) => typeof(BombGame).GetField(name, Private).SetValue(game, value);
    static object Call(string name) => typeof(BombGame).GetMethod(name, Private).Invoke(game, null);

    static void VerifyDeadInputAndSpectating() {
        BombNetwork local = Field<BombNetwork>("network");
        var outbox = (System.Collections.Concurrent.ConcurrentQueue<string>)
            typeof(BombNetwork).GetField("outbox", Private).GetValue(local);
        try {
            Deliver("{\"type\":\"WELCOME\",\"playerId\":\"dead-local\",\"hasRoom\":false}");
            Deliver("{\"type\":\"ROOM_STATE\",\"room\":{\"code\":\"SPECTATE\",\"mode\":\"QUICK\",\"phase\":\"PLAYING\",\"roundIndex\":1}}");
            var snapshot = new ServerMessage {
                type = "STATE_UPDATE", phase = "PLAYING", roomCode = "SPECTATE", roundIndex = 1,
                mapId = "meadow", mode = "QUICK", arenaSize = 48, roundTimeLeft = 123,
                spectatorTargetId = "living", players = new[] {
                    new PlayerData { id = "dead-local", name = "Local", alive = false, connected = true, visible = true, size = 1 },
                    new PlayerData { id = "living", name = "Living", alive = true, connected = true, visible = true, size = 1, hp = 100, x = 35 },
                    new PlayerData { id = "next", name = "Next", alive = true, connected = true, visible = true, size = 1, hp = 100, x = 40 }
                }, orbs = Array.Empty<PickupData>(), cores = Array.Empty<PickupData>()
            };
            Deliver(JsonUtility.ToJson(snapshot));
            typeof(BombNetwork).GetField("transportReady", Private).SetValue(local, true);
            local.MarkWelcomed();
            Set("nextMoveAt", -1f); Set("nextPingAt", -1f); Set("charging", true);
            Call("Update");
            string[] sent = outbox.ToArray();
            Assert(sent.Any(s => s.Contains("\"PING\"")), "Dead client keeps heartbeat");
            Assert(!sent.Any(s => s.Contains("\"MOVE\"") || s.Contains("\"EXPLODE\"")), "Dead Update sends no gameplay input");
            Assert(!Field<bool>("charging"), "Death clears local charge");
            var viewed = Call("ViewAvatar");
            Assert(((PlayerData)viewed.GetType().GetField("state").GetValue(viewed)).id == "living", "Camera and listener select server spectator target");
            Assert(Field<float>("roundTimeLeft") == 123, "Quick HUD receives server countdown");
            snapshot.spectatorTargetId = "next"; snapshot.players[1].alive = false;
            Deliver(JsonUtility.ToJson(snapshot));
            viewed = Call("ViewAvatar");
            Assert(((PlayerData)viewed.GetType().GetField("state").GetValue(viewed)).id == "next", "Spectator advances after followed player dies");
            // No local state yet: input must also be blocked during join/reconnect.
            Set("myId", "not-in-snapshot"); Set("nextMoveAt", -1f);
            int queued = outbox.Count;
            Call("HandleInput");
            Assert(outbox.Count == queued, "Missing local state sends no gameplay input");
            Set("myId", "dead-local");
            Deliver("{\"type\":\"ROOM_STATE\",\"room\":{\"code\":\"SPECTATE\",\"mode\":\"JOURNEY\",\"phase\":\"FINISHED\",\"chapterIndex\":0,\"journeyComplete\":true,\"journeyEndReason\":\"INSUFFICIENT_PLAYERS\"}}");
            Assert((bool)Call("JourneyFinished") && !(bool)Call("JourneyHasNextChapter"), "Early Journey result cannot offer impossible next chapter");
        } finally {
            local.Dispose();
            Call("ResetRoomState");
            Set("nextPingAt", 0f); Set("nextMoveAt", 0f);
        }
    }

    static void CaptureMap(string map) {
        Camera camera = Field<Camera>("gameCamera");
        string directory = BombBuild.Argument("-bombScreenshots", "");
        Directory.CreateDirectory(directory);
        RenderTexture target = RenderTexture.GetTemporary(1280, 720, 24);
        RenderTexture oldTarget = camera.targetTexture, oldActive = RenderTexture.active;
        Texture2D image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(directory, map + ".png"), image.EncodeToPNG());
            Debug.Log("BOMB_SCREENSHOT " + map);
        } finally {
            camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.Destroy(image);
        }
    }
}
