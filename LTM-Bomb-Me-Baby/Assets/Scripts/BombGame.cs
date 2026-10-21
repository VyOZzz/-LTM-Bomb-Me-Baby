using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public sealed class BombGame : MonoBehaviour {
    static readonly Vector3 CameraOffset = new Vector3(0, 22, -22);
    const float CameraSize = 10.5f;

    sealed class Avatar {
        public GameObject body;
        public Transform rollingVisual;
        public GameObject shield;
        public GameObject chargeAura;
        public TrailRenderer boostTrail;
        public Color color;
        public Vector3 target;
        public PlayerData state;
        public float visualY;
        public float verticalVelocity;
        public float squash;
    }

    sealed class HudNotice {
        public string title;
        public string body;
        public float duration;
        public int priority;
        public Color accent;
    }

    readonly BombNetwork network = new BombNetwork();
    readonly BombMainMenu mainMenu = new BombMainMenu();
    readonly BombModeMenu modeMenu = new BombModeMenu();
    readonly BombHud hud = new BombHud();
    readonly BombOverlay overlay = new BombOverlay();
    readonly BombShardView shardView = new BombShardView();
    readonly Dictionary<string, string> playerNames = new Dictionary<string, string>();
    int aliveCount, playerCount;
    float matchStartedAt;
    ObjectiveData objective;
    ObstacleData[] activeObstacles = Array.Empty<ObstacleData>();
    string toast = "";
    float toastUntil;
    readonly Queue<HudNotice> noticeQueue = new Queue<HudNotice>();
    readonly Dictionary<string, Avatar> avatars = new Dictionary<string, Avatar>();
    readonly Dictionary<int, GameObject> orbViews = new Dictionary<int, GameObject>();
    readonly Dictionary<int, GameObject> coreViews = new Dictionary<int, GameObject>();
    readonly Color[] playerColors = {
        new Color(1,.34f,.26f), new Color(.24f,.74f,1), new Color(1,.75f,.2f), new Color(.53f,.96f,.43f),
        new Color(.76f,.38f,1), new Color(1,.42f,.76f), new Color(.18f,.92f,.76f), new Color(1,.58f,.18f),
        new Color(.62f,.76f,1), new Color(.92f,.95f,.35f)
    };
    string host = "127.0.0.1", port = "7777", playerName = "Bom";
    string joinCode = "", myId = "", status = "Nhập tên rồi kết nối tới server.", phase = "MENU", winnerId = "";
    RoomData room;
    float nextMoveAt, nextPingAt, nextMetricsAt, rttMs, serverMicros;
    const float ReconnectWindowSeconds = 10;
    float reconnectStartedAt = -1;
    string activeSyncMode = "?", configuredDemoMap = "";
    int previousSent, previousReceived, sentPerSecond, receivedPerSecond;
    long previousSentBytes, previousReceivedBytes, sentBytesPerSecond, receivedBytesPerSecond;
    bool charging;
    bool showNetworkSettings;
    BombStory story;
    BombStoryCinematic cinematic;
    BombAudio gameAudio;
    HudNotice activeNotice;
    float noticeStartedAt, noticeEndsAt;
    [SerializeField] Camera gameCamera;
    [SerializeField] Transform actorsRoot;
    [SerializeField] Transform pickupsRoot;
    [SerializeField] Transform effectsRoot;
    GameObject authoredEnvironment;
    GameObject runtimeMapRoot;
    BushData[] activeBushes = Array.Empty<BushData>();
    string activeMapId = "", activeMapName = "";
    float activeArenaSize;
    GUIStyle titleStyle, bodyStyle, smallStyle, panelStyle, buttonStyle, fieldStyle;
    GUIStyle heroStyle, sectionStyle, quietButtonStyle, metricStyle;
    GUIStyle noticeTitleStyle, noticeBodyStyle;
    Texture2D buttonTexture, buttonHoverTexture, quietButtonTexture;
    static readonly Color HudPanel = new Color(.035f,.047f,.075f,.88f);
    static readonly Color HudPanelSoft = new Color(.055f,.075f,.11f,.78f);
    static readonly Color WarmWhite = new Color(1f,.96f,.88f);
    static readonly Color AttackOrange = new Color(1f,.43f,.08f);
    static readonly Color ShieldCyan = new Color(.12f,.9f,1f);
    static readonly Color BoostBlue = new Color(.18f,.55f,1f);
    static readonly Color EnergyGreen = new Color(.35f,.92f,.24f);
    static readonly Color CoreMagenta = new Color(1f,.16f,.72f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap() {
        if (FindFirstObjectByType<BombGame>() == null) new GameObject("Bomb Me Baby").AddComponent<BombGame>();
    }

    void Awake() {
        playerName = "Bom " + UnityEngine.Random.Range(100, 999);
        LoadServerSettings();
        story = BombStory.Load(out string storyError);
        if (!string.IsNullOrEmpty(storyError)) Debug.LogWarning(storyError);
        cinematic = GetComponent<BombStoryCinematic>();
        if (cinematic == null) cinematic = gameObject.AddComponent<BombStoryCinematic>();
        gameAudio = GetComponent<BombAudio>();
        if (gameAudio == null) gameAudio = gameObject.AddComponent<BombAudio>();
        if (gameCamera == null) gameCamera = Camera.main;
        if (actorsRoot == null) actorsRoot = FindSceneRoot("Players");
        if (pickupsRoot == null) pickupsRoot = FindSceneRoot("Pickups");
        if (effectsRoot == null) effectsRoot = FindSceneRoot("Effects");
        authoredEnvironment = GameObject.Find("Environment");
    }

    void LoadServerSettings() {
        // Keep localhost convenient in the Editor, but never silently ship it as the
        // destination of a standalone client running on somebody else's computer.
        host = PlayerPrefs.GetString("BombServerHost", Application.isEditor ? host : "");
        port = PlayerPrefs.GetString("BombServerPort", port);

        // A server.txt file beside the executable lets a release be pointed at a server
        // without rebuilding it. Its first non-empty line is HOST or HOST:PORT.
        try {
            string directory = Directory.GetParent(Application.dataPath)?.FullName;
            string path = string.IsNullOrEmpty(directory) ? "" : Path.Combine(directory, "server.txt");
            if (!string.IsNullOrEmpty(path) && File.Exists(path)) {
                foreach (string rawLine in File.ReadAllLines(path)) {
                    string line = rawLine.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    int separator = line.LastIndexOf(':');
                    if (separator > 0 && separator < line.Length - 1 && int.TryParse(line.Substring(separator + 1), out int configuredPort)) {
                        host = line.Substring(0, separator).Trim();
                        port = configuredPort.ToString();
                    } else host = line;
                    break;
                }
            }
        } catch (Exception e) {
            Debug.LogWarning("Cannot read server.txt: " + e.Message);
        }

        foreach (string argument in Environment.GetCommandLineArgs()) {
            const string prefix = "--server=";
            if (!argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            string endpoint = argument.Substring(prefix.Length).Trim();
            int separator = endpoint.LastIndexOf(':');
            if (separator > 0 && separator < endpoint.Length - 1 && int.TryParse(endpoint.Substring(separator + 1), out int configuredPort)) {
                host = endpoint.Substring(0, separator).Trim();
                port = configuredPort.ToString();
            } else host = endpoint;
        }
    }

    void Start() {
        Application.targetFrameRate = 60;
        BombSettings.Load();
        Application.runInBackground = true;
        if (gameCamera == null) BuildPresentation();
        // Every quality level ships with MSAA off, which makes ball and rock edges jagged.
        QualitySettings.antiAliasing = 4;
        gameCamera.allowMSAA = true;
        gameCamera.clearFlags = CameraClearFlags.SolidColor;
        gameCamera.backgroundColor = BombUi.Night;
        gameCamera.orthographic = true;
        gameCamera.orthographicSize = CameraSize;
    }

    static Transform FindSceneRoot(string objectName) {
        GameObject found = GameObject.Find(objectName);
        return found == null ? null : found.transform;
    }

    void OnDestroy() { network.Dispose(); }

    void Update() {
        TickNotice();
        while (network.TryReceive(out string line)) {
            try { Handle(line); }
            catch (Exception e) { status = "Dữ liệu server không hợp lệ: " + e.Message; }
        }
        if (network.Connected && Time.unscaledTime >= nextPingAt) {
            nextPingAt = Time.unscaledTime + 2;
            network.Send(new BombMessage { type = "PING" });
        }
        if (Time.unscaledTime >= nextMetricsAt) UpdateNetworkMetrics();
        if (Input.GetKeyDown(KeyCode.F3)) {
            BombSettings.ShowNetwork = !BombSettings.ShowNetwork;
            BombSettings.Save();
        }
        if (Input.GetKeyDown(KeyCode.Escape) && (cinematic == null || !cinematic.IsPlaying)) {
            if (overlay.IsOpen) overlay.Back();
            else if (room == null && !string.IsNullOrEmpty(myId)) modeMenu.Back();
            else if (room != null) {
                overlay.OpenPause();
                // The match keeps running on the server: stop rolling instead of drifting.
                if (phase == "PLAYING") network.Send(new BombMessage { type = "MOVE", directionX = 0, directionZ = 0 });
            }
        }
        if (phase == "PLAYING" && !overlay.IsOpen && (cinematic == null || !cinematic.IsPlaying)
                && network.Connected && !string.IsNullOrEmpty(myId)) HandleInput();
        foreach (Avatar avatar in avatars.Values) {
            if (avatar.body == null) continue;
            if (avatar.visualY > 0 || avatar.verticalVelocity > 0) {
                avatar.verticalVelocity -= 12f * Time.deltaTime;
                avatar.visualY += avatar.verticalVelocity * Time.deltaTime;
                if (avatar.visualY <= 0) {
                    avatar.visualY = 0;
                    avatar.verticalVelocity = 0;
                    avatar.squash = .16f;
                }
            }
            avatar.squash = Mathf.MoveTowards(avatar.squash, 0, Time.deltaTime * 1.8f);
            Vector3 previous = avatar.body.transform.position;
            float smoothing = 1 - Mathf.Exp(-22 * Time.deltaTime);
            Vector3 visualTarget = avatar.target + Vector3.up * avatar.visualY;
            avatar.body.transform.position = Vector3.Lerp(previous, visualTarget, smoothing);
            Vector3 travel = avatar.body.transform.position - previous;
            travel.y = 0;
            if (avatar.rollingVisual != null && travel.sqrMagnitude > .000001f) {
                float radius = Mathf.Max(.05f, avatar.body.transform.localScale.x * .5f);
                Vector3 axis = Vector3.Cross(Vector3.up, travel.normalized);
                avatar.rollingVisual.Rotate(axis, travel.magnitude / radius * Mathf.Rad2Deg, Space.World);
            }
            if (avatar.state != null) {
                float scale = avatar.state.size;
                Vector3 visualScale = new Vector3(scale * (1 + avatar.squash), scale * (1 - avatar.squash * 1.5f),
                    scale * (1 + avatar.squash));
                avatar.body.transform.localScale = Vector3.Lerp(avatar.body.transform.localScale, visualScale, 10 * Time.deltaTime);
                avatar.shield.SetActive(avatar.state.shield);
                avatar.shield.transform.localScale = Vector3.one * 1.6f;
                if (avatar.chargeAura != null) {
                    avatar.chargeAura.SetActive(avatar.state.charging);
                    avatar.chargeAura.transform.localScale = Vector3.one * (1.15f + avatar.state.charge * .9f);
                }
                if (avatar.boostTrail != null) avatar.boostTrail.emitting = avatar.state.boost;
            }
        }
        Avatar carrier = objective != null ? AvatarOf(objective.carrierId) : null;
        shardView.Tick(carrier?.body.transform, carrier?.state != null ? carrier.state.size : 1);
        FollowLocalPlayer();
    }

    void UpdateNetworkMetrics() {
        nextMetricsAt = Time.unscaledTime + 1;
        int sent = network.SentMessages;
        int received = network.ReceivedMessages;
        long sentBytes = network.SentBytes;
        long receivedBytes = network.ReceivedBytes;
        sentPerSecond = sent - previousSent;
        receivedPerSecond = received - previousReceived;
        sentBytesPerSecond = sentBytes - previousSentBytes;
        receivedBytesPerSecond = receivedBytes - previousReceivedBytes;
        previousSent = sent;
        previousReceived = received;
        previousSentBytes = sentBytes;
        previousReceivedBytes = receivedBytes;
    }

    void HandleInput() {
        if (Time.unscaledTime >= nextMoveAt) {
            nextMoveAt = Time.unscaledTime + .04f;
            float horizontal = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0)
                - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
            float vertical = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1 : 0)
                - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1 : 0);
            Vector3 cameraRight = gameCamera == null ? Vector3.right : gameCamera.transform.right;
            Vector3 cameraForward = gameCamera == null ? Vector3.forward : gameCamera.transform.forward;
            cameraRight.y = 0;
            cameraForward.y = 0;
            cameraRight.Normalize();
            cameraForward.Normalize();
            Vector3 worldDirection = Vector3.ClampMagnitude(cameraRight * horizontal + cameraForward * vertical, 1);
            network.Send(new BombMessage { type = "MOVE", directionX = worldDirection.x, directionZ = worldDirection.z });
        }
        float bombCooldown = avatars.TryGetValue(myId, out Avatar localAvatar) && localAvatar.state != null
            ? localAvatar.state.chargeCooldown : 0;
        if (Input.GetKeyDown(KeyCode.Space) && !charging && bombCooldown <= 0) {
            charging = true;
            network.Send(new BombMessage { type = "CHARGE_START" });
        }
        // Also release when Space was let go while the pause menu swallowed the KeyUp.
        if (charging && (Input.GetKeyUp(KeyCode.Space) || !Input.GetKey(KeyCode.Space))) {
            charging = false;
            network.Send(new BombMessage { type = "EXPLODE" });
        }
        if (Input.GetKeyDown(KeyCode.E)) network.Send(new BombMessage { type = "USE_SHIELD" });
        if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
            network.Send(new BombMessage { type = "USE_BOOST" });
    }

    void FollowLocalPlayer() {
        if (gameCamera == null || phase != "PLAYING" || !avatars.TryGetValue(myId, out Avatar me) || me.body == null)
            return;
        Vector3 focus = me.body.transform.position;
        focus.y = 0;
        Vector3 desiredPosition = focus + CameraOffset;
        float follow = 1 - Mathf.Exp(-6f * Time.unscaledDeltaTime);
        gameCamera.transform.position = Vector3.Lerp(gameCamera.transform.position, desiredPosition, follow);
        gameCamera.transform.rotation = Quaternion.LookRotation(focus - gameCamera.transform.position, Vector3.up);
    }

    void Handle(string line) {
        ServerMessage msg = JsonUtility.FromJson<ServerMessage>(line);
        if (msg == null || string.IsNullOrEmpty(msg.type)) return;
        switch (msg.type) {
            case "WELCOME":
                bool resumed = reconnectStartedAt >= 0;
                myId = msg.playerId;
                network.ResumeToken = msg.resumeToken;
                network.ContinueSequenceAfter(msg.lastSeq);
                if (!string.IsNullOrEmpty(msg.syncMode)) activeSyncMode = msg.syncMode;
                configuredDemoMap = msg.demoMap ?? "";
                reconnectStartedAt = -1;
                status = resumed ? "Đã kết nối lại với server." : "";
                break;
            case "ROOM_STATE":
                if (room == null) status = "";
                room = JsonUtility.FromJson<RoomEnvelope>(line).room;
                if (room != null) {
                    phase = room.phase;
                    if (phase == "WAITING")
                        status = "Đã vào phòng " + room.code + ". Chờ mọi người sẵn sàng.";
                    else if (phase == "PLAYING" && status.Contains("Đang tự thử lại"))
                        status = "Đã kết nối lại vào trận.";
                }
                break;
            case "LEFT_ROOM":
                room = null; phase = "MENU"; winnerId = "";
                status = "Đã rời phòng. Tạo phòng hoặc nhập mã phòng.";
                gameAudio.StopGameplayLoops();
                gameAudio.SetMenuMusic();
                ClearNotices();
                ClearActors();
                break;
            case "GAME_START":
                phase = "PLAYING"; winnerId = ""; charging = false;
                matchStartedAt = Time.unscaledTime;
                objective = null;
                shardView.Clear();
                toast = "";
                ApplyMap(msg);
                status = msg.mode == "JOURNEY"
                    ? $"Chương {msg.chapterIndex + 1}/3: {msg.mapName}"
                    : "Trận đấu bắt đầu" + (string.IsNullOrEmpty(msg.mapName) ? "!" : ": " + msg.mapName);
                ClearNotices();
                ShowMapIntro(msg.mapId);
                gameAudio.SetMapMusic(msg.mapId);
                gameAudio.PlayMatchStart();
                break;
            case "STATE_UPDATE":
                phase = msg.phase;
                ApplySnapshot(msg);
                break;
            case "PLAYER_EXPLODE":
                if (msg.playerId == myId) charging = false;
                if (msg.visible) {
                    Vector3 explosionPosition = new Vector3(msg.x, .45f, msg.z);
                    gameAudio.PlayExplosion(explosionPosition, msg.radius);
                    StartCoroutine(Explosion(explosionPosition, msg.radius));
                }
                break;
            case "PLAYER_KNOCKED_BACK":
                if (avatars.TryGetValue(msg.playerId, out Avatar knocked)) {
                    float bounce = Mathf.Lerp(3.2f, 6.2f, Mathf.InverseLerp(0, 16, msg.strength));
                    if (msg.shielded) bounce *= .45f;
                    knocked.verticalVelocity = Mathf.Max(knocked.verticalVelocity, bounce);
                    knocked.squash = 0;
                }
                break;
            case "ENERGY_PICKED":
                gameAudio.PlayEnergyPickup(msg.playerId == myId, PositionOf(msg.playerId));
                ShowStoryEvent(msg);
                break;
            case "CORE_PICKED":
                gameAudio.PlayCorePickup(msg.playerId == myId, PositionOf(msg.playerId));
                ShowStoryEvent(msg);
                break;
            case "PLAYER_DAMAGED":
                gameAudio.PlayDamage(msg.playerId == myId, PositionOf(msg.playerId));
                ShowStoryEvent(msg);
                break;
            case "DAMAGE_REFLECTED":
                gameAudio.PlayDamage(msg.to == myId, PositionOf(msg.to));
                ShowStoryEvent(msg);
                break;
            case "SHIELD_ACTIVATED":
                gameAudio.PlayShield(msg.playerId == myId, PositionOf(msg.playerId));
                ShowStoryEvent(msg);
                break;
            case "BOOST_ACTIVATED":
                gameAudio.PlayBoost(msg.playerId == myId, PositionOf(msg.playerId));
                ShowStoryEvent(msg);
                break;
            case "PLAYER_ELIMINATED":
                if (msg.playerId == myId) gameAudio.StopGameplayLoops();
                else gameAudio.PlayDamage(false, PositionOf(msg.playerId));
                ShowStoryEvent(msg);
                break;
            case "GAME_OVER":
                phase = "FINISHED";
                winnerId = msg.winnerId;
                gameAudio.StopGameplayLoops();
                if (msg.mode != "JOURNEY" || msg.chapterIndex < 2) gameAudio.PlayResult(winnerId == myId);
                if (msg.mode != "JOURNEY")
                    status = winnerId == myId ? "Bạn chiến thắng!" : "Trận đấu kết thúc.";
                else if (string.IsNullOrEmpty(winnerId))
                    status = "Hết giờ — không ai giữ được mảnh Tim Lửa.";
                else if (msg.reason == "TIME")
                    status = winnerId == myId ? "Hết giờ! Bạn giữ được mảnh Tim Lửa."
                        : $"Hết giờ! {NameOf(winnerId)} giữ được mảnh Tim Lửa.";
                else
                    status = winnerId == myId ? "Bạn là người cuối cùng — mảnh thuộc về bạn!"
                        : $"{NameOf(winnerId)} là người cuối cùng và giữ mảnh.";
                break;
            case "JOURNEY_COMPLETE":
                phase = "FINISHED";
                winnerId = msg.winnerId;
                gameAudio.StopGameplayLoops();
                gameAudio.PlayResult(winnerId == myId);
                int shards = 0;
                if (msg.scores != null) foreach (ScoreData s in msg.scores) if (s.playerId == winnerId) shards = s.roundWins;
                if (string.IsNullOrEmpty(winnerId)) status = "Hành trình kết thúc: đồng hạng.";
                else if (shards >= 3)
                    status = winnerId == myId ? "Tim Lửa Trọn Vẹn! Bạn mang về đủ cả 3 mảnh."
                        : $"Tim Lửa Trọn Vẹn! {NameOf(winnerId)} mang về đủ cả 3 mảnh.";
                else
                    status = winnerId == myId ? $"Bạn là Người Giữ Lửa với {shards} mảnh!"
                        : $"{NameOf(winnerId)} là Người Giữ Lửa với {shards} mảnh.";
                ClearNotices();
                break;
            case "PONG":
                rttMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - msg.clientTime;
                break;
            case "ACK":
                serverMicros = msg.processingMicros;
                break;
            case "ERROR":
                if (msg.code == "RESUME_FAILED") {
                    reconnectStartedAt = -1;
                    network.ResumeToken = "";
                    myId = ""; room = null; phase = "MENU"; winnerId = "";
                    ClearNotices();
                    ClearActors();
                    gameAudio.StopGameplayLoops();
                    gameAudio.SetMenuMusic();
                    status = "Phiên chơi đã hết hạn. Hãy kết nối lại từ menu.";
                } else status = ErrorText(msg.code, msg.mode);
                break;
            case "NETWORK_ERROR":
                gameAudio.StopGameplayLoops();
                if (room != null && reconnectStartedAt < 0) reconnectStartedAt = Time.unscaledTime;
                status = network.LastError + " Đang thử kết nối lại...";
                break;
        }
    }

    void ShowMapIntro(string mapId) {
        BombStoryMap map = story?.FindMap(mapId);
        if (map == null) return;
        string title = string.IsNullOrEmpty(map.chapter) ? map.title : map.chapter + "  •  " + map.title;
        string body = map.intro;
        if (!string.IsNullOrEmpty(map.hint)) body += "\n" + map.hint;
        EnqueueNotice(title, body, 6f, AttackOrange, 2);
    }

    void ShowStoryEvent(ServerMessage message) {
        BombStoryEvent item = story?.FindEvent(message.type);
        if (item == null) return;
        bool localSubject = message.playerId == myId || message.to == myId;
        string template = localSubject ? item.self : item.other;
        string body = BombStory.Format(template, message, PlayerName);
        if (string.IsNullOrEmpty(body)) return;
        EnqueueNotice(item.title, body, Mathf.Max(1.2f, item.duration), NoticeAccent(message.type), 1);
    }

    string PlayerName(string playerId) {
        if (string.IsNullOrEmpty(playerId)) return "đối thủ";
        if (playerId == myId) return "bạn";
        if (room?.players != null)
            foreach (LobbyPlayer player in room.players)
                if (player.id == playerId) return player.name;
        if (avatars.TryGetValue(playerId, out Avatar avatar) && avatar.state != null)
            return avatar.state.name;
        return "đối thủ";
    }

    static Color NoticeAccent(string type) {
        switch (type) {
            case "ENERGY_PICKED": return EnergyGreen;
            case "CORE_PICKED": return CoreMagenta;
            case "SHIELD_ACTIVATED": return ShieldCyan;
            case "BOOST_ACTIVATED": return BoostBlue;
            case "PLAYER_DAMAGED":
            case "DAMAGE_REFLECTED":
            case "PLAYER_ELIMINATED": return new Color(1f,.28f,.22f);
            default: return AttackOrange;
        }
    }

    void EnqueueNotice(string title, string body, float duration, Color accent, int priority) {
        var notice = new HudNotice { title = title, body = body, duration = duration, accent = accent, priority = priority };
        if (activeNotice == null) {
            ActivateNotice(notice);
            return;
        }
        if (activeNotice.title == title) {
            ActivateNotice(notice);
            return;
        }
        if (priority > activeNotice.priority) {
            if (noticeQueue.Count < 4) noticeQueue.Enqueue(activeNotice);
            ActivateNotice(notice);
            return;
        }
        foreach (HudNotice queued in noticeQueue)
            if (queued.title == title) return;
        if (noticeQueue.Count < 4) noticeQueue.Enqueue(notice);
    }

    void ActivateNotice(HudNotice notice) {
        activeNotice = notice;
        noticeStartedAt = Time.unscaledTime;
        noticeEndsAt = noticeStartedAt + notice.duration;
    }

    void TickNotice() {
        if (activeNotice == null || Time.unscaledTime < noticeEndsAt) return;
        activeNotice = null;
        if (noticeQueue.Count > 0) ActivateNotice(noticeQueue.Dequeue());
    }

    void ClearNotices() {
        activeNotice = null;
        noticeQueue.Clear();
    }

    void ApplySnapshot(ServerMessage snapshot) {
        ApplyMap(snapshot);
        if (snapshot.players == null) return;
        playerCount = snapshot.players.Length;
        aliveCount = 0;
        foreach (PlayerData p in snapshot.players) {
            if (p.alive) aliveCount++;
            playerNames[p.id] = p.name;
            if (p.id == myId) gameAudio.SetCharging(p.alive && p.charging, p.charge);
        }
        HashSet<string> seen = new HashSet<string>();
        for (int i = 0; i < snapshot.players.Length; i++) {
            PlayerData p = snapshot.players[i];
            seen.Add(p.id);
            bool shouldRender = p.id == myId || p.visible;
            if (!shouldRender && !avatars.ContainsKey(p.id)) continue;
            if (!avatars.TryGetValue(p.id, out Avatar a)) {
                GameObject body = new GameObject("Bomb " + p.name);
                body.name = "Bomb " + p.name;
                if (actorsRoot != null) body.transform.SetParent(actorsRoot, false);
                // Concept look: black toy bomb with white eyes; the player's color is the band.
                Color playerColor = playerColors[i % playerColors.Length];
                GameObject rollingVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                rollingVisual.name = "Rolling ball";
                rollingVisual.transform.SetParent(body.transform, false);
                rollingVisual.GetComponent<Renderer>().material = MaterialFor(BombUi.BombBody);
                Destroy(rollingVisual.GetComponent<Collider>());
                GameObject rollBand = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                rollBand.name = "Roll band";
                rollBand.transform.SetParent(rollingVisual.transform, false);
                rollBand.transform.localScale = new Vector3(1.02f,.06f,1.02f);
                rollBand.GetComponent<Renderer>().material = MaterialFor(playerColor);
                Destroy(rollBand.GetComponent<Collider>());
                for (int side = -1; side <= 1; side += 2) {
                    GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    eye.name = "Eye";
                    eye.transform.SetParent(body.transform, false);
                    eye.transform.localPosition = new Vector3(side * .13f,.1f,-.44f);
                    eye.transform.localScale = new Vector3(.1f,.2f,.08f);
                    eye.GetComponent<Renderer>().material = MaterialFor(BombUi.Text);
                    Destroy(eye.GetComponent<Collider>());
                }
                GameObject fuse = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                fuse.transform.SetParent(body.transform, false);
                fuse.transform.localPosition = new Vector3(0,.53f,0);
                fuse.transform.localScale = new Vector3(.09f,.2f,.09f);
                fuse.GetComponent<Renderer>().material = MaterialFor(new Color(.45f,.33f,.24f));
                Destroy(fuse.GetComponent<Collider>());
                GameObject shield = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                shield.name = "Shield";
                shield.transform.SetParent(body.transform, false);
                shield.GetComponent<Renderer>().material = TransparentMaterial(BombUi.WithAlpha(BombUi.Shield,.28f));
                Destroy(shield.GetComponent<Collider>());
                GameObject chargeAura = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                chargeAura.name = "Charge Aura";
                chargeAura.transform.SetParent(body.transform, false);
                chargeAura.GetComponent<Renderer>().material = TransparentMaterial(BombUi.WithAlpha(BombUi.Charge,.22f));
                Destroy(chargeAura.GetComponent<Collider>());
                TrailRenderer boostTrail = body.AddComponent<TrailRenderer>();
                boostTrail.time = .32f;
                boostTrail.startWidth = .6f;
                boostTrail.endWidth = .05f;
                boostTrail.minVertexDistance = .12f;
                boostTrail.material = TransparentMaterial(BombUi.WithAlpha(BombUi.Boost,.65f));
                boostTrail.emitting = false;
                a = new Avatar {
                    body = body, rollingVisual = rollingVisual.transform, shield = shield,
                    chargeAura = chargeAura, boostTrail = boostTrail, color = playerColor
                };
                avatars.Add(p.id,a);
                body.transform.position = new Vector3(p.x,.55f,p.z);
            }
            a.state = p;
            if (shouldRender) a.target = new Vector3(p.x, .55f * p.size, p.z);
            a.body.SetActive(p.alive && shouldRender);
        }
        foreach (string id in new List<string>(avatars.Keys)) {
            if (seen.Contains(id)) continue;
            Destroy(avatars[id].body);
            avatars.Remove(id);
        }
        SyncPickups(snapshot.orbs, orbViews, .19f, BombUi.Energy);
        SyncPickups(snapshot.cores, coreViews, .43f, BombUi.Core);

        ObjectiveData next = snapshot.objective != null && snapshot.objective.Active ? snapshot.objective : null;
        if (next != null) AnnounceObjective(objective, next);
        objective = next;
        Avatar carrier = next != null ? AvatarOf(next.carrierId) : null;
        shardView.Apply(next, effectsRoot, carrier?.body.transform, carrier?.state != null ? carrier.state.size : 1,
            next != null ? ColorOf(next.ownerId) : BombUi.Text, next != null ? ColorOf(next.challengerId) : BombUi.Charge);
    }

    /// Big centered message when the shard changes hands, derived by diffing snapshots.
    void AnnounceObjective(ObjectiveData before, ObjectiveData now) {
        if (before == null) return;
        if (before.state == "WAITING" && now.state == "GROUND") Toast("Mảnh Tim Lửa đã xuất hiện! Xem minimap.");
        else if (now.state == "CARRIED" && before.carrierId != now.carrierId) Toast($"{NameOf(now.carrierId)} đã nhặt được mảnh!");
        else if (before.state == "CARRIED" && now.state == "GROUND") {
            Avatar previousCarrier = AvatarOf(before.carrierId);
            if (previousCarrier?.state != null && !previousCarrier.state.alive)
                Toast("Người cầm mảnh bị hạ — Mảnh Tim Lửa rơi xuống!");
            else if (previousCarrier?.state != null && !previousCarrier.state.connected)
                Toast("Người cầm mất kết nối — Mảnh Tim Lửa rơi xuống!");
            else Toast("Khóa Lửa đã vỡ — Mảnh Tim Lửa bật khỏi người cầm!");
        }
        else if (before.state != "PLACED" && now.state == "PLACED") Toast($"{NameOf(now.ownerId)} đã đặt mảnh lên bệ đá!");
        else if (before.state == "PLACED" && now.state == "GROUND")
            Toast($"{NameOf(now.pickupPriorityId)} đã phá ấn — mảnh bật khỏi bệ!");
    }

    static string ErrorText(string code, string roomMode) {
        switch (code) {
            case "ROOM_NOT_FOUND": return "Không tìm thấy phòng với mã này. Kiểm tra lại với chủ phòng.";
            case "ROOM_UNAVAILABLE": return "Phòng đã đầy hoặc đang trong trận, chưa vào được.";
            case "MODE_MISMATCH":
                return "Mã này là phòng " + (roomMode == "JOURNEY" ? "Hành trình Ba Mảnh" : "Đấu nhanh")
                    + ". Quay lại chọn đúng chế độ rồi nhập lại mã.";
            case "ALREADY_IN_ROOM": return "Bạn đang ở trong một phòng khác.";
            default: return "Server từ chối: " + code;
        }
    }

    void Toast(string text) {
        toast = text;
        toastUntil = Time.unscaledTime + 3;
    }

    Avatar AvatarOf(string id) => !string.IsNullOrEmpty(id) && avatars.TryGetValue(id, out Avatar a) ? a : null;

    Vector3 PositionOf(string id) {
        Avatar avatar = AvatarOf(id);
        return avatar?.body != null ? avatar.body.transform.position : Vector3.zero;
    }

    string NameOf(string id) =>
        id == myId ? "Bạn" : !string.IsNullOrEmpty(id) && playerNames.TryGetValue(id, out string n) ? n : "Ai đó";

    Color ColorOf(string id) => AvatarOf(id)?.color ?? BombUi.Text;

    void ApplyMap(ServerMessage snapshot) {
        if (string.IsNullOrEmpty(snapshot.mapId) || snapshot.mapId == activeMapId) return;
        activeMapId = snapshot.mapId;
        activeMapName = snapshot.mapName;
        activeArenaSize = snapshot.arenaSize;
        activeBushes = snapshot.bushes ?? Array.Empty<BushData>();
        activeObstacles = snapshot.obstacles ?? Array.Empty<ObstacleData>();

        if (authoredEnvironment != null) authoredEnvironment.SetActive(false);
        if (runtimeMapRoot != null) Destroy(runtimeMapRoot);
        runtimeMapRoot = new GameObject("Runtime Map - " + activeMapId);

        Color floorColor;
        Color wallColor;
        Color rockColor;
        // Concept palette: violet-grey stone everywhere, ground tinted per region.
        switch (activeMapId) {
            case "crater":
                floorColor = new Color(.40f,.24f,.25f);
                wallColor = new Color(.46f,.32f,.36f);
                rockColor = new Color(.56f,.44f,.48f);
                break;
            case "crossroads":
                floorColor = new Color(.46f,.38f,.44f);
                wallColor = new Color(.38f,.32f,.50f);
                rockColor = new Color(.60f,.55f,.66f);
                break;
            default:
                floorColor = new Color(.25f,.43f,.33f);
                wallColor = new Color(.42f,.37f,.50f);
                rockColor = new Color(.58f,.54f,.62f);
                break;
        }

        float diameter = activeArenaSize * 2;
        CreateMapPrimitive("Arena floor", PrimitiveType.Cube, runtimeMapRoot.transform,
            new Vector3(0,-.2f,0), new Vector3(diameter + 1,.3f,diameter + 1), floorColor);
        CreateMapPrimitive("West wall", PrimitiveType.Cube, runtimeMapRoot.transform,
            new Vector3(-activeArenaSize-.7f,.6f,0), new Vector3(.5f,1.2f,diameter + 1.8f), wallColor);
        CreateMapPrimitive("East wall", PrimitiveType.Cube, runtimeMapRoot.transform,
            new Vector3(activeArenaSize+.7f,.6f,0), new Vector3(.5f,1.2f,diameter + 1.8f), wallColor);
        CreateMapPrimitive("South wall", PrimitiveType.Cube, runtimeMapRoot.transform,
            new Vector3(0,.6f,-activeArenaSize-.7f), new Vector3(diameter + 1.8f,1.2f,.5f), wallColor);
        CreateMapPrimitive("North wall", PrimitiveType.Cube, runtimeMapRoot.transform,
            new Vector3(0,.6f,activeArenaSize+.7f), new Vector3(diameter + 1.8f,1.2f,.5f), wallColor);

        if (snapshot.obstacles != null) foreach (ObstacleData obstacle in snapshot.obstacles) {
            CreateMapPrimitive("Rock", PrimitiveType.Cylinder, runtimeMapRoot.transform,
                new Vector3(obstacle.x,.65f,obstacle.z),
                new Vector3(obstacle.radius * 2,.65f,obstacle.radius * 2), rockColor);
        }
        foreach (BushData bush in activeBushes)
            CreateRuntimeBush(new Vector3(bush.x,0,bush.z), bush.radius, runtimeMapRoot.transform);
    }

    void SyncPickups(PickupData[] pickups, Dictionary<int, GameObject> views, float size, Color color) {
        HashSet<int> seen = new HashSet<int>();
        if (pickups != null) foreach (PickupData p in pickups) {
            seen.Add(p.id);
            if (!views.TryGetValue(p.id, out GameObject view)) {
                view = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                view.name = size > .3f ? "Core" : "Energy";
                if (pickupsRoot != null) view.transform.SetParent(pickupsRoot, false);
                view.transform.localScale = Vector3.one * size * 2;
                view.GetComponent<Renderer>().material = MaterialFor(color);
                Destroy(view.GetComponent<Collider>());
                views[p.id] = view;
            }
            view.transform.position = new Vector3(p.x, size + .08f, p.z);
        }
        foreach (int id in new List<int>(views.Keys)) if (!seen.Contains(id)) {
            Destroy(views[id]); views.Remove(id);
        }
    }

    IEnumerator Explosion(Vector3 position, float radius) {
        GameObject effect = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        effect.name = "Explosion";
        if (effectsRoot != null) effect.transform.SetParent(effectsRoot, false);
        Destroy(effect.GetComponent<Collider>());
        effect.GetComponent<Renderer>().material = TransparentMaterial(BombUi.WithAlpha(BombUi.Charge,.4f));
        effect.transform.position = position;
        float time = 0;
        while (time < .35f) {
            time += Time.deltaTime;
            effect.transform.localScale = Vector3.one * Mathf.Lerp(.1f,radius * 2,time/.35f);
            yield return null;
        }
        Destroy(effect);
    }

    void BuildPresentation() {
        var cameraObject = new GameObject("Arena camera");
        gameCamera = cameraObject.AddComponent<Camera>();
        gameCamera.transform.position = CameraOffset;
        gameCamera.transform.LookAt(Vector3.zero);
        gameCamera.orthographic = true;
        gameCamera.orthographicSize = CameraSize;
        gameCamera.backgroundColor = BombUi.Night;
        cameraObject.AddComponent<AudioListener>();
        RenderSettings.ambientLight = new Color(.7f,.75f,.85f);
        var lightObject = new GameObject("Sun");
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        lightObject.transform.rotation = Quaternion.Euler(45,-35,0);
    }

    GameObject CreateMapPrimitive(string objectName, PrimitiveType type, Transform parent,
                                  Vector3 position, Vector3 scale, Color color) {
        GameObject item = GameObject.CreatePrimitive(type);
        item.name = objectName;
        item.transform.SetParent(parent, false);
        item.transform.position = position;
        item.transform.localScale = scale;
        item.GetComponent<Renderer>().material = MaterialFor(color);
        return item;
    }

    void CreateRuntimeBush(Vector3 position, float radius, Transform parent) {
        GameObject root = new GameObject("Bush");
        root.transform.SetParent(parent, false);
        root.transform.position = position;
        Color[] colors = { new Color(.12f,.42f,.20f), new Color(.18f,.55f,.25f), new Color(.28f,.66f,.30f) };
        Vector3[] offsets = {
            new Vector3(0,.55f,0), new Vector3(-.55f,.42f,.15f),
            new Vector3(.5f,.45f,.25f), new Vector3(.05f,.4f,-.55f)
        };
        for (int i = 0; i < offsets.Length; i++) {
            GameObject leaf = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            leaf.transform.SetParent(root.transform, false);
            leaf.transform.localPosition = offsets[i] * radius;
            leaf.transform.localScale = Vector3.one * radius * (i == 0 ? 1.15f : .9f);
            leaf.GetComponent<Renderer>().material = MaterialFor(colors[i % colors.Length]);
            Destroy(leaf.GetComponent<Collider>());
        }
    }

    internal static Material MaterialFor(Color color) {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var material = new Material(shader);
        material.color = color;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor",color);
        return material;
    }

    internal static Material TransparentMaterial(Color color) {
        var material = new Material(Shader.Find("Standard"));
        material.SetFloat("_Mode", 3);
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.renderQueue = 3000;
        material.color = color;
        return material;
    }

    void ClearActors() {
        foreach (Avatar a in avatars.Values) Destroy(a.body);
        avatars.Clear();
        foreach (GameObject go in orbViews.Values) Destroy(go);
        orbViews.Clear();
        foreach (GameObject go in coreViews.Values) Destroy(go);
        coreViews.Clear();
        shardView.Clear();
        objective = null;
    }

    void OnGUI() {
        if (cinematic != null && cinematic.IsPlaying) {
            cinematic.Draw();
            return;
        }
        // While the overlay is open the screen underneath is drawn but not interactive.
        GUI.enabled = !overlay.IsOpen;
        DrawScreen();
        GUI.enabled = true;
        if (reconnectStartedAt >= 0) {
            float remaining = Mathf.Max(0, ReconnectWindowSeconds - (Time.unscaledTime - reconnectStartedAt));
            hud.DrawReconnect(remaining, network.Endpoint, network.RetryAttempt);
        }
        switch (overlay.Draw(room != null, phase == "PLAYING")) {
            case BombOverlay.Action.LeaveRoom:
                network.Send(new BombMessage { type = "LEAVE_ROOM" });
                break;
            case BombOverlay.Action.QuitGame:
                QuitGame();
                break;
        }
    }

    void DrawScreen() {
        if (titleStyle == null) {
            BombUi.EnsureShapes();
            titleStyle = BombUi.TextStyle(24, FontStyle.Bold, BombUi.Text);
            bodyStyle = BombUi.TextStyle(16, FontStyle.Normal, BombUi.Text);
            bodyStyle.wordWrap = true;
            bodyStyle.margin = new RectOffset(0, 0, 3, 3);
            smallStyle = BombUi.TextStyle(12, FontStyle.Normal, BombUi.TextMuted);
            smallStyle.margin = new RectOffset(0, 0, 2, 2);
            panelStyle = new GUIStyle {
                padding = new RectOffset(18, 18, 16, 16), border = new RectOffset(20, 20, 20, 20)
            };
            panelStyle.normal.background = BombUi.RoundBig.normal.background;
            buttonStyle = new GUIStyle(GUI.skin.button) {
                fontSize = 16, fontStyle = FontStyle.Bold, border = new RectOffset(10, 10, 10, 10),
                margin = new RectOffset(0, 0, 6, 6)
            };
            buttonStyle.normal.background = buttonStyle.hover.background = buttonStyle.active.background
                = BombUi.RoundSmall.normal.background;
            buttonStyle.normal.textColor = buttonStyle.hover.textColor = buttonStyle.active.textColor = BombUi.Night;
            fieldStyle = new GUIStyle(GUI.skin.textField) {
                fontSize = 16, border = new RectOffset(10, 10, 10, 10), padding = new RectOffset(10, 10, 7, 7)
            };
            fieldStyle.normal.background = fieldStyle.hover.background = fieldStyle.focused.background
                = BombUi.RoundSmall.normal.background;
            fieldStyle.normal.textColor = fieldStyle.hover.textColor = fieldStyle.focused.textColor = BombUi.Text;
        }
        if (!string.IsNullOrEmpty(myId) && room == null) {
            switch (modeMenu.Draw(playerName, status, ref joinCode)) {
                case BombModeMenu.Choice.Create:
                    status = "";
                    RunAfterJourneyIntro(() => network.Send(new BombMessage {
                        type = "CREATE_ROOM", mode = modeMenu.SelectedMode
                    }));
                    break;
                case BombModeMenu.Choice.Join:
                    status = "Đang tìm phòng " + joinCode + "...";
                    RunAfterJourneyIntro(() => network.Send(new BombMessage {
                        type = "JOIN_ROOM", code = joinCode, mode = modeMenu.SelectedMode
                    }));
                    break;
                case BombModeMenu.Choice.Settings:
                    overlay.OpenSettings();
                    break;
                case BombModeMenu.Choice.Quit:
                    QuitGame();
                    break;
            }
            return;
        }
        if (string.IsNullOrEmpty(myId)) {
            switch (mainMenu.Draw(ref playerName, ref host, ref port, status)) {
                case BombMainMenu.Choice.Connect:
                    ConnectToConfiguredServer();
                    break;
                case BombMainMenu.Choice.Quit:
                    QuitGame();
                    break;
            }
            return;
        }
        if (phase == "PLAYING") {
            DrawMatchHud();
            return;
        }
        if (phase == "FINISHED") {
            bool won = winnerId == myId;
            hud.DrawBanner(won ? "CHIẾN THẮNG!" : "TRẬN ĐẤU KẾT THÚC", status, won ? BombUi.Charge : BombUi.TextMuted);
        }
        // Lobby / connect card: hugs its content instead of filling the screen height.
        GUILayout.BeginArea(new Rect(16,14,360,Screen.height-28));
        GUI.backgroundColor = BombUi.Panel;
        GUILayout.BeginVertical(panelStyle);
        GUI.backgroundColor = BombUi.PanelRaised;
        GUILayout.Label("BOMB ME BABY", titleStyle);
        if (!string.IsNullOrEmpty(status) && phase != "FINISHED") GUILayout.Label(status, bodyStyle);
        GUILayout.Space(8);
        {
            GUILayout.Label(room.mode == "JOURNEY"
                ? $"Hành trình Ba Mảnh  •  Chương {room.chapterIndex + 1}/3" : "Đấu nhanh", bodyStyle);
            GUILayout.Label($"Mã phòng: {room.code}" + (string.IsNullOrEmpty(room.mapName) ? "" : $"  •  {room.mapName}"), smallStyle);
            if (room.scores != null && room.scores.Length > 0) {
                GUILayout.Space(6);
                GUILayout.Label("Mảnh Tim Lửa", bodyStyle);
                foreach (ScoreData score in room.scores)
                    GUILayout.Label($"{score.name}:  {score.roundWins} mảnh  •  {score.eliminations} lần hạ", smallStyle);
            }
            GUILayout.Space(6);
            if (room.players != null) foreach (LobbyPlayer p in room.players)
                GUILayout.Label((p.id == myId ? "▶ " : "   ") + p.name + (p.ready ? "  ✓" : "") + (p.connected ? "" : "  (mất kết nối)"), bodyStyle);
            if (!room.journeyComplete) {
                bool myReady = false;
                if (room.players != null) foreach (LobbyPlayer p in room.players) if (p.id == myId) myReady = p.ready;
                GUILayout.Space(6);
                if (PanelButton(myReady ? "Bỏ sẵn sàng" : "Sẵn sàng", myReady ? BombUi.TextMuted : BombUi.Health))
                    network.Send(new BombMessage { type = "READY", ready = !myReady });
                int capacity = room.maxPlayers > 0 ? room.maxPlayers : 10;
                GUILayout.Label($"Có thể bắt đầu một mình để test; tối đa {capacity} người.", smallStyle);
            }
            if (PanelButton("Rời phòng", BombUi.PanelRaised, BombUi.Text)) network.Send(new BombMessage { type = "LEAVE_ROOM" });
        }
        GUILayout.EndVertical();
        GUI.backgroundColor = Color.white;
        GUILayout.EndArea();
        if (phase == "FINISHED") DrawResultPopup();
    }

    void RunAfterJourneyIntro(Action action) {
        if (modeMenu.SelectedMode == "JOURNEY" && cinematic != null) {
            status = "Đang mở câu chuyện Hành trình Ba Mảnh...";
            cinematic.Play(() => {
                status = "";
                action();
            });
        } else action();
    }

    void DrawMatchHud() {
        avatars.TryGetValue(myId, out Avatar me);
        PlayerData state = me?.state;
        hud.Draw(new BombHud.View {
            me = state, myId = myId, myColor = me != null ? me.color : BombUi.TextMuted,
            alive = aliveCount, total = playerCount,
            journey = room != null && room.mode == "JOURNEY", chapter = room != null ? room.chapterIndex : 0,
            mapName = activeMapName, matchTime = Time.unscaledTime - matchStartedAt,
            inBush = state != null && BushIndex(state.x, state.z) >= 0,
            rttMs = rttMs, serverMicros = serverMicros,
            sentPerSecond = sentPerSecond, receivedPerSecond = receivedPerSecond,
            sentBytesPerSecond = sentBytesPerSecond, receivedBytesPerSecond = receivedBytesPerSecond,
            showNetwork = BombSettings.ShowNetwork,
            endpoint = network.Endpoint, syncMode = activeSyncMode, demoMap = configuredDemoMap,
            mapId = activeMapId, arenaSize = activeArenaSize,
            obstacles = activeObstacles, bushes = activeBushes, dots = MinimapDots(),
            objective = objective,
            carrierName = objective != null ? NameOf(objective.carrierId) : "",
            ownerName = objective != null ? NameOf(objective.ownerId) : "",
            challengerName = objective != null ? NameOf(!string.IsNullOrEmpty(objective.challengerId)
                ? objective.challengerId : objective.pickupPriorityId) : "",
            ownerColor = objective != null ? ColorOf(objective.ownerId) : BombUi.Text,
            challengerColor = objective != null ? ColorOf(!string.IsNullOrEmpty(objective.challengerId)
                ? objective.challengerId : objective.pickupPriorityId) : BombUi.Charge,
            toast = toast, toastAlpha = Mathf.Clamp01(toastUntil - Time.unscaledTime)
        });
        foreach (KeyValuePair<string, Avatar> entry in avatars) {
            Avatar a = entry.Value;
            if (a.body == null || a.state == null || !a.state.alive || !a.body.activeInHierarchy) continue;
            Vector3 pos = gameCamera.WorldToScreenPoint(a.body.transform.position + Vector3.up * (a.state.size * .5f + .3f));
            if (pos.z > 0 && pos.x >= 0 && pos.x <= Screen.width && pos.y >= 0 && pos.y <= Screen.height)
                hud.DrawNameTag(
                    new Vector2(pos.x, Screen.height - pos.y),
                    a.state.name,
                    a.state.hp,
                    entry.Key == myId,
                    objective != null && objective.carrierId == entry.Key,
                    objective != null ? objective.fireLock : 0,
                    objective != null ? objective.fireLockMax : 2);
        }
        DrawStoryNotice();
    }

    void EnsureGuiStyles() {
        if (titleStyle != null) return;
        titleStyle = LabelStyle(28, FontStyle.Bold, WarmWhite, TextAnchor.MiddleLeft);
        heroStyle = LabelStyle(42, FontStyle.Bold, WarmWhite, TextAnchor.MiddleCenter);
        sectionStyle = LabelStyle(18, FontStyle.Bold, WarmWhite, TextAnchor.MiddleLeft);
        bodyStyle = LabelStyle(16, FontStyle.Normal, WarmWhite, TextAnchor.MiddleLeft);
        smallStyle = LabelStyle(13, FontStyle.Normal, new Color(.82f,.86f,.92f), TextAnchor.MiddleLeft);
        metricStyle = LabelStyle(12, FontStyle.Normal, new Color(.65f,.74f,.82f), TextAnchor.MiddleRight);
        noticeTitleStyle = LabelStyle(16, FontStyle.Bold, WarmWhite, TextAnchor.MiddleLeft);
        noticeBodyStyle = LabelStyle(14, FontStyle.Normal, WarmWhite, TextAnchor.UpperLeft);
        noticeBodyStyle.wordWrap = true;
        buttonTexture = SolidTexture(new Color(.94f,.28f,.06f));
        buttonHoverTexture = SolidTexture(new Color(1f,.43f,.08f));
        quietButtonTexture = SolidTexture(new Color(.12f,.16f,.23f));
        buttonStyle = new GUIStyle(GUI.skin.button) {
            fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(16,16,10,10)
        };
        buttonStyle.normal.background = buttonTexture;
        buttonStyle.hover.background = buttonHoverTexture;
        buttonStyle.active.background = buttonHoverTexture;
        buttonStyle.normal.textColor = Color.white;
        buttonStyle.hover.textColor = Color.white;
        quietButtonStyle = new GUIStyle(buttonStyle) { fontSize = 15 };
        quietButtonStyle.normal.background = quietButtonTexture;
    }

    static GUIStyle LabelStyle(int size, FontStyle weight, Color color, TextAnchor alignment) {
        var style = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = weight, alignment = alignment };
        style.normal.textColor = color;
        return style;
    }

    static Texture2D SolidTexture(Color color) {
        var texture = new Texture2D(1,1) { hideFlags = HideFlags.HideAndDontSave };
        texture.SetPixel(0,0,color);
        texture.Apply();
        return texture;
    }

    static void FillRect(Rect rect, Color color) {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    static void Panel(Rect rect, Color accent) {
        FillRect(rect, HudPanel);
        FillRect(new Rect(rect.x,rect.y,5,rect.height),accent);
        FillRect(new Rect(rect.x,rect.y,rect.width,1),new Color(1,1,1,.12f));
    }

    void DrawFrontEnd() {
        FillRect(new Rect(0,0,Screen.width,Screen.height),new Color(.02f,.03f,.055f,.42f));
        float width = Mathf.Min(520,Screen.width - 40);
        float height = room == null ? (string.IsNullOrEmpty(myId) ? 430 : 420) : Mathf.Min(610,Screen.height - 40);
        Rect card = new Rect((Screen.width-width)*.5f,(Screen.height-height)*.5f,width,height);
        Panel(card,AttackOrange);
        GUILayout.BeginArea(new Rect(card.x+34,card.y+24,card.width-68,card.height-48));
        GUILayout.Label("BOMB ME BABY", heroStyle);
        GUILayout.Label("Đấu trường bom nhiều người chơi", new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleCenter });
        GUILayout.Space(18);
        DrawStatusBanner();
        GUILayout.Space(14);
        if (string.IsNullOrEmpty(myId)) DrawConnectMenu();
        else if (room == null) DrawRoomMenu();
        else DrawLobbyMenu();
        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        GUILayout.Label(network.Connected ? "● ĐÃ KẾT NỐI" : "● NGOẠI TUYẾN", new GUIStyle(smallStyle) {
            normal = { textColor = network.Connected ? EnergyGreen : new Color(1f,.4f,.35f) }
        });
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Thoát",quietButtonStyle,GUILayout.Width(100),GUILayout.Height(34))) QuitGame();
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    void DrawStatusBanner() {
        Rect banner = GUILayoutUtility.GetRect(10,44,GUILayout.ExpandWidth(true));
        FillRect(banner,HudPanelSoft);
        GUI.Label(new Rect(banner.x+14,banner.y,banner.width-28,banner.height),status,smallStyle);
    }

    void DrawConnectMenu() {
        GUILayout.Label("TÊN NGƯỜI CHƠI",sectionStyle);
        playerName = GUILayout.TextField(playerName,20,GUILayout.Height(40));
        GUILayout.Space(12);
        if (GUILayout.Button("VÀO ĐẤU TRƯỜNG",buttonStyle,GUILayout.Height(48))) ConnectToConfiguredServer();
        GUILayout.Space(8);
        if (GUILayout.Button(showNetworkSettings ? "Ẩn cài đặt mạng" : "Cài đặt mạng nâng cao",quietButtonStyle,GUILayout.Height(38)))
            showNetworkSettings = !showNetworkSettings;
        if (showNetworkSettings) DrawNetworkSettings();
    }

    void DrawRoomMenu() {
        GUILayout.Label("CHỌN CÁCH CHƠI",sectionStyle);
        GUILayout.Space(8);
        if (GUILayout.Button("TẠO PHÒNG MỚI",buttonStyle,GUILayout.Height(50)))
            network.Send(new BombMessage { type = "CREATE_ROOM" });
        GUILayout.Space(18);
        GUILayout.Label("HOẶC NHẬP MÃ PHÒNG",sectionStyle);
        joinCode = GUILayout.TextField(joinCode.ToUpperInvariant(),6,GUILayout.Height(40));
        GUILayout.Space(8);
        if (GUILayout.Button("VÀO PHÒNG",quietButtonStyle,GUILayout.Height(44)))
            network.Send(new BombMessage { type = "JOIN_ROOM", code = joinCode });
    }

    void DrawLobbyMenu() {
        GUILayout.BeginHorizontal();
        GUILayout.Label("PHÒNG " + room.code,titleStyle);
        GUILayout.FlexibleSpace();
        if (!string.IsNullOrEmpty(room.mapName)) GUILayout.Label(room.mapName.ToUpperInvariant(),smallStyle);
        GUILayout.EndHorizontal();
        GUILayout.Space(8);
        int playerCount = room.players == null ? 0 : room.players.Length;
        int capacity = room.maxPlayers > 0 ? room.maxPlayers : 10;
        GUILayout.Label($"NGƯỜI CHƠI  {playerCount}/{capacity}",sectionStyle);
        GUILayout.Space(5);
        if (room.players != null) foreach (LobbyPlayer player in room.players) {
            Rect row = GUILayoutUtility.GetRect(10,38,GUILayout.ExpandWidth(true));
            FillRect(row,player.id == myId ? new Color(1f,.43f,.08f,.18f) : HudPanelSoft);
            GUI.Label(new Rect(row.x+12,row.y,row.width-120,row.height),(player.id == myId ? "▶  " : "    ") + player.name,bodyStyle);
            string state = !player.connected ? "MẤT KẾT NỐI" : player.ready ? "SẴN SÀNG" : "ĐANG CHỜ";
            Color stateColor = !player.connected ? new Color(1f,.4f,.35f) : player.ready ? EnergyGreen : new Color(.7f,.74f,.8f);
            GUI.Label(new Rect(row.x+row.width-150,row.y,138,row.height),state,new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleRight, normal = { textColor = stateColor } });
        }
        GUILayout.FlexibleSpace();
        bool myReady = IsLocalPlayerReady();
        if (GUILayout.Button(myReady ? "BỎ SẴN SÀNG" : "SẴN SÀNG",myReady ? quietButtonStyle : buttonStyle,GUILayout.Height(48)))
            network.Send(new BombMessage { type = "READY", ready = !myReady });
        GUILayout.Space(7);
        if (GUILayout.Button("Rời phòng",quietButtonStyle,GUILayout.Height(36)))
            network.Send(new BombMessage { type = "LEAVE_ROOM" });
    }

    void DrawGameplayHud() {
        avatars.TryGetValue(myId,out Avatar me);
        PlayerData state = me?.state;
        int alive = 0, total = 0;
        foreach (Avatar avatar in avatars.Values) if (avatar.state != null) { total++; if (avatar.state.alive) alive++; }

        Rect playerPanel = new Rect(22,20,320,112);
        Panel(playerPanel,EnergyGreen);
        GUI.Label(new Rect(40,28,280,28),state == null ? playerName : state.name,sectionStyle);
        float hp = state == null ? 0 : Mathf.Clamp01(state.hp/100f);
        GUI.Label(new Rect(40,58,45,22),"HP",smallStyle);
        DrawBar(new Rect(82,62,228,16),hp,EnergyGreen);
        GUI.Label(new Rect(40,87,270,24),state == null ? "Đang đồng bộ..." : $"{state.hp:0} / 100    •    KÍCH THƯỚC {state.size:0.00}",smallStyle);

        Rect roundPanel = new Rect((Screen.width-330)*.5f,20,330,66);
        Panel(roundPanel,AttackOrange);
        GUI.Label(new Rect(roundPanel.x+16,roundPanel.y+5,298,28),$"{alive} / {Mathf.Max(total,alive)} NGƯỜI CÒN LẠI",new GUIStyle(sectionStyle) { alignment = TextAnchor.MiddleCenter });
        GUI.Label(new Rect(roundPanel.x+16,roundPanel.y+32,298,22),string.IsNullOrEmpty(activeMapName) ? "ĐANG CHIẾN ĐẤU" : activeMapName.ToUpperInvariant(),new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleCenter });

        float charge = state == null ? 0 : Mathf.Clamp01(state.charge);
        Rect chargePanel = new Rect((Screen.width-540)*.5f,Screen.height-104,540,76);
        Panel(chargePanel,AttackOrange);
        GUI.Label(new Rect(chargePanel.x+18,chargePanel.y+7,160,24),charging ? "THẢ SPACE ĐỂ NỔ" : "GIỮ SPACE • NẠP BOM",smallStyle);
        DrawBar(new Rect(chargePanel.x+18,chargePanel.y+39,504,20),charge,AttackOrange);

        float abilityWidth = 132;
        float abilityY = Screen.height-126;
        DrawAbility(new Rect(Screen.width-298,abilityY,abilityWidth,98),"E","KHIÊN",state?.shieldCooldown ?? 0,ShieldCyan);
        DrawAbility(new Rect(Screen.width-154,abilityY,abilityWidth,98),"SHIFT","TĂNG TỐC",state?.boostCooldown ?? 0,BoostBlue);

        Rect helpPanel = new Rect(22,Screen.height-105,300,77);
        FillRect(helpPanel,HudPanelSoft);
        GUI.Label(new Rect(35,helpPanel.y+8,275,24),"WASD / PHÍM MŨI TÊN  •  DI CHUYỂN",smallStyle);
        GUI.Label(new Rect(35,helpPanel.y+34,275,24),state != null && BushIndex(state.x,state.z) >= 0 ? "ẨN THÂN TRONG BỤI" : "SPACE  •  NẠP VÀ NỔ BOM",new GUIStyle(smallStyle) { normal = { textColor = state != null && BushIndex(state.x,state.z) >= 0 ? EnergyGreen : smallStyle.normal.textColor } });

        GUI.Label(new Rect(Screen.width-420,10,398,22),$"RTT {rttMs:0} ms  •  {sentPerSecond}/{receivedPerSecond} msg/s  •  {sentBytesPerSecond}/{receivedBytesPerSecond} B/s",metricStyle);
    }

    void DrawStoryNotice() {
        if (activeNotice == null) return;
        if (noticeTitleStyle == null) {
            noticeTitleStyle = BombUi.TextStyle(16, FontStyle.Bold, BombUi.Text);
            noticeBodyStyle = BombUi.TextStyle(14, FontStyle.Normal, BombUi.Text);
            noticeBodyStyle.wordWrap = true;
        }
        float remaining = noticeEndsAt - Time.unscaledTime;
        float fadeIn = Mathf.Clamp01((Time.unscaledTime - noticeStartedAt) / .18f);
        float fadeOut = Mathf.Clamp01(remaining / .22f);
        float alpha = Mathf.Min(fadeIn, fadeOut);
        float width = Mathf.Min(620, Screen.width - 44);
        Rect card = new Rect((Screen.width - width) * .5f, 98, width, 88);
        FillRect(card, new Color(HudPanel.r, HudPanel.g, HudPanel.b, HudPanel.a * alpha));
        FillRect(new Rect(card.x, card.y, 5, card.height), new Color(activeNotice.accent.r, activeNotice.accent.g, activeNotice.accent.b, alpha));
        FillRect(new Rect(card.x, card.yMax - 4, card.width * Mathf.Clamp01(remaining / activeNotice.duration), 4),
            new Color(activeNotice.accent.r, activeNotice.accent.g, activeNotice.accent.b, alpha));
        Color previous = GUI.color;
        GUI.color = new Color(1, 1, 1, alpha);
        GUI.Label(new Rect(card.x + 18, card.y + 8, card.width - 36, 24), activeNotice.title, noticeTitleStyle);
        GUI.Label(new Rect(card.x + 18, card.y + 34, card.width - 36, 47), activeNotice.body, noticeBodyStyle);
        GUI.color = previous;
    }

    void DrawAbility(Rect rect, string key, string label, float cooldown, Color accent) {
        Panel(rect,accent);
        Rect keyRect = new Rect(rect.x+12,rect.y+12,44,44);
        FillRect(keyRect,new Color(accent.r,accent.g,accent.b,.2f));
        GUI.Label(keyRect,key,new GUIStyle(sectionStyle) { alignment = TextAnchor.MiddleCenter, normal = { textColor = accent } });
        GUI.Label(new Rect(rect.x+64,rect.y+12,rect.width-74,26),label,new GUIStyle(smallStyle) { fontStyle = FontStyle.Bold });
        string value = cooldown > 0 ? cooldown.ToString("0.0") + "s" : "SẴN SÀNG";
        GUI.Label(new Rect(rect.x+64,rect.y+39,rect.width-74,22),value,new GUIStyle(smallStyle) { normal = { textColor = cooldown > 0 ? WarmWhite : accent } });
        DrawBar(new Rect(rect.x+12,rect.y+75,rect.width-24,8),cooldown > 0 ? Mathf.Clamp01(1-cooldown/8f) : 1,accent);
    }

    void DrawBar(Rect rect, float value, Color fill) {
        FillRect(rect,new Color(0,0,0,.55f));
        Rect inner = new Rect(rect.x+2,rect.y+2,Mathf.Max(0,(rect.width-4)*Mathf.Clamp01(value)),Mathf.Max(0,rect.height-4));
        FillRect(inner,fill);
    }

    void DrawWorldNameplate(Rect rect, string text) {
        FillRect(rect,new Color(.02f,.03f,.05f,.76f));
        GUI.Label(rect,text,new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleCenter });
    }

    void DrawResultPopup() {
        const float width = 430;
        const float height = 230;
        Rect popup = new Rect((Screen.width - width) * .5f, (Screen.height - height) * .5f, width, height);
        GUI.Box(popup, GUIContent.none);
        GUILayout.BeginArea(new Rect(popup.x + 24, popup.y + 22, width - 48, height - 44));
        bool won = !string.IsNullOrEmpty(winnerId) && winnerId == myId;
        GUILayout.Label(won ? "CHIẾN THẮNG!" : "BẠN ĐÃ THUA", titleStyle);
        GUILayout.Space(8);
        GUILayout.Label(won ? "Bạn là người sống sót cuối cùng." : WinnerText(), bodyStyle);
        GUILayout.FlexibleSpace();
        bool ready = IsLocalPlayerReady();
        if (GUILayout.Button(ready ? "Đang chờ người chơi khác..." : "Chơi lại", GUILayout.Height(38)) && !ready) {
            BombAudio.UiClick();
            network.Send(new BombMessage { type = "READY", ready = true });
        }
        if (GUILayout.Button("Rời phòng", GUILayout.Height(32))) {
            BombAudio.UiClick();
            network.Send(new BombMessage { type = "LEAVE_ROOM" });
        }
        GUILayout.EndArea();
    }

    string WinnerText() {
        if (string.IsNullOrEmpty(winnerId)) return "Không còn người chơi sống sót.";
        if (room?.players != null)
            foreach (LobbyPlayer player in room.players)
                if (player.id == winnerId) return player.name + " đã chiến thắng.";
        return "Đối thủ đã chiến thắng.";
    }

    bool IsLocalPlayerReady() {
        if (room?.players == null) return false;
        foreach (LobbyPlayer player in room.players)
            if (player.id == myId) return player.ready;
        return false;
    }

    void ConnectToConfiguredServer() {
        playerName = playerName.Trim();
        if (string.IsNullOrEmpty(playerName)) {
            status = "Hãy nhập tên người chơi trước khi vào đấu trường.";
            return;
        }
        host = host.Trim();
        if (string.IsNullOrEmpty(host)) {
            status = "Bản game chưa được cấu hình server. Hãy liên hệ người gửi game.";
            showNetworkSettings = true;
            mainMenu.ShowNetworkSettings();
            return;
        }
        if (!int.TryParse(port, out int parsed) || parsed < 1 || parsed > 65535) {
            status = "Cấu hình server có port không hợp lệ.";
            showNetworkSettings = true;
            mainMenu.ShowNetworkSettings();
            return;
        }
        PlayerPrefs.SetString("BombServerHost", host);
        PlayerPrefs.SetString("BombServerPort", port);
        PlayerPrefs.Save();
        status = "Đang vào server...";
        network.Connect(host, parsed, playerName);
    }

    void DrawNetworkSettings() {
        GUILayout.Space(6);
        GUILayout.Label("Dành cho chủ phòng / kiểm thử", smallStyle);
        GUILayout.Label("Địa chỉ server", bodyStyle); host = GUILayout.TextField(host);
        GUILayout.Label("Port", bodyStyle); port = GUILayout.TextField(port,5);
        if (host == "127.0.0.1" || host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            GUILayout.Label("Chỉ dùng khi server chạy trên chính máy này.", smallStyle);
    }

    void QuitGame() {
        network.Dispose();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// Players this client is allowed to see: itself, visible opponents and the shard carrier.
    BombHud.MapDot[] MinimapDots() {
        var dots = new List<BombHud.MapDot>();
        foreach (KeyValuePair<string, Avatar> entry in avatars) {
            PlayerData s = entry.Value.state;
            bool isMe = entry.Key == myId;
            if (s == null || !s.alive || (!isMe && !s.visible)) continue;
            dots.Add(new BombHud.MapDot {
                x = s.x, z = s.z, color = entry.Value.color, isMe = isMe,
                isCarrier = objective != null && objective.carrierId == entry.Key
            });
        }
        return dots.ToArray();
    }

    bool PanelButton(string label, Color face) => PanelButton(label, face, BombUi.Night);

    bool PanelButton(string label, Color face, Color text) {
        Color savedBackground = GUI.backgroundColor;
        GUI.backgroundColor = face;
        buttonStyle.normal.textColor = buttonStyle.hover.textColor = buttonStyle.active.textColor = text;
        bool clicked = GUILayout.Button(label, buttonStyle, GUILayout.Height(38));
        GUI.backgroundColor = savedBackground;
        if (clicked) BombAudio.UiClick();
        return clicked;
    }

    int BushIndex(float x, float z) {
        for (int i = 0; i < activeBushes.Length; i++) {
            float dx = x - activeBushes[i].x;
            float dz = z - activeBushes[i].z;
            if (dx * dx + dz * dz <= activeBushes[i].radius * activeBushes[i].radius) return i;
        }
        return -1;
    }
}
