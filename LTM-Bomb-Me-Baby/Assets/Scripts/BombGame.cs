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
    readonly Dictionary<string, float> reconnectDeadlines = new Dictionary<string, float>();
    int aliveCount, playerCount;
    float matchStartedAt;
    string spectatorTargetId = "";
    float roundTimeLeft = 180;
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
    string[] winnerIds = Array.Empty<string>();
    string lastResultSound = "";
    int activeRoundIndex;
    bool recoveryMusicPending;
    float nextMoveAt, nextPingAt, nextMetricsAt, rttMs, serverMicros;
    const float ReconnectWindowSeconds = 10;
    float reconnectStartedAt = -1;
    bool reconnectExpired;
    bool journeyGuideVisible;
    bool journeyCompleteReceived;
    string activeSyncMode = "?", configuredDemoMap = "";
    int previousSent, previousReceived, sentPerSecond, receivedPerSecond;
    long previousSentBytes, previousReceivedBytes, sentBytesPerSecond, receivedBytesPerSecond;
    bool charging;
    PlayerData[] latestPlayers = Array.Empty<PlayerData>();
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
    readonly BombMapView mapView = new BombMapView();
    BushData[] activeBushes = Array.Empty<BushData>();
    string activeMapId = "", activeMapName = "";
    float activeArenaSize;
    GUIStyle titleStyle, bodyStyle, smallStyle, sectionStyle, sectionRightStyle, codeStyle, nameStyle, badgeStyle;
    GUIStyle cellStyle, cellRightStyle, buttonStyle, quietButtonStyle;
    GUIStyle resultTitleStyle, centeredBodyStyle, centeredMutedStyle;
    GUIStyle noticeTitleStyle, noticeBodyStyle;
    float ui = 1, uiStylesScale, noticeStylesScale;
    float codeCopiedUntil;
    static readonly Color HudPanel = new Color(.035f,.047f,.075f,.88f);
    /// Secondary button face: a step lighter than the PanelRaised cards it sits on.
    static readonly Color QuietFace = new Color(.24f,.2f,.35f);
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

    void OnDestroy() { network.Dispose(); mapView.Clear(); shardView.Clear(); }

    void Update() {
        TickNotice();
        while (network.TryReceive(out string line)) {
            try { Handle(line); }
            catch (Exception e) { status = "Dữ liệu server không hợp lệ: " + e.Message; }
        }
        if (reconnectStartedAt >= 0 && !reconnectExpired
                && Time.unscaledTime - reconnectStartedAt >= ReconnectWindowSeconds)
            ExpireReconnect();
        if (network.Connected && Time.unscaledTime >= nextPingAt) {
            nextPingAt = Time.unscaledTime + 2;
            network.Send(new BombMessage { type = "PING" });
        }
        if (Time.unscaledTime >= nextMetricsAt) UpdateNetworkMetrics();
        if (Input.GetKeyDown(KeyCode.F3)) {
            BombSettings.ShowNetwork = !BombSettings.ShowNetwork;
            BombSettings.Save();
        }
        if (Input.GetKeyDown(KeyCode.H) && phase == "PLAYING" && room != null && room.mode == "JOURNEY")
            journeyGuideVisible = !journeyGuideVisible;
        if (Input.GetKeyDown(KeyCode.Escape) && (cinematic == null || !cinematic.IsPlaying)) {
            if (overlay.IsOpen) overlay.Back();
            else if (room == null && !string.IsNullOrEmpty(myId)) modeMenu.Back();
            else if (room != null) {
                overlay.OpenPause();
                // The match keeps running on the server: stop rolling instead of drifting.
                if (phase == "PLAYING" && LocalAlive()) network.Send(new BombMessage { type = "MOVE", directionX = 0, directionZ = 0 });
            }
        }
        if (room != null && !overlay.IsOpen && (cinematic == null || !cinematic.IsPlaying)) {
            bool enter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            if (enter || Input.GetKeyDown(KeyCode.R)) HandleRoomShortcut(enter);
        }
        if (phase == "PLAYING" && !overlay.IsOpen && (cinematic == null || !cinematic.IsPlaying)
                && network.Connected && !string.IsNullOrEmpty(myId)) HandleInput();
        Avatar listeningAvatar = ViewAvatar();
        if (listeningAvatar?.body != null)
            gameAudio.SetListeningPoint(listeningAvatar.body.transform.position,
                gameCamera != null ? gameCamera.transform.right : Vector3.right);
        foreach (Avatar avatar in avatars.Values) {
            if (avatar.body == null) continue;
            if (avatar.visualY > 0 || avatar.verticalVelocity > 0) {
                avatar.verticalVelocity -= 12f * Time.deltaTime;
                avatar.visualY += avatar.verticalVelocity * Time.deltaTime;
                if (avatar.visualY <= 0) {
                    if (phase == "PLAYING" && avatar.body.activeInHierarchy && avatar.state != null)
                        gameAudio.PlayLanding(avatar.state.id == myId, avatar.body.transform.position,
                            Mathf.Abs(avatar.verticalVelocity));
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
        if (!LocalAlive()) { charging = false; return; }
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
        bool carryingShard = objective != null && objective.state == "CARRIED" && objective.carrierId == myId;
        if (carryingShard) charging = false;
        if (Input.GetKeyDown(KeyCode.Q) && carryingShard)
            network.Send(new BombMessage { type = "DROP_SHARD" });
        if (Input.GetKeyDown(KeyCode.Space) && !carryingShard && !charging && bombCooldown <= 0) {
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
        Avatar me = ViewAvatar();
        if (gameCamera == null || phase != "PLAYING" || me?.body == null)
            return;
        Vector3 focus = me.body.transform.position;
        focus.y = 0;
        mapView.SetLocalBush(me.body.activeSelf ? BushIndex(focus.x, focus.z) : -1);
        Vector3 desiredPosition = focus + CameraOffset;
        float follow = 1 - Mathf.Exp(-6f * Time.unscaledDeltaTime);
        gameCamera.transform.position = Vector3.Lerp(gameCamera.transform.position, desiredPosition, follow);
        gameCamera.transform.rotation = Quaternion.LookRotation(focus - gameCamera.transform.position, Vector3.up);
    }

    bool LocalAlive() => AvatarOf(myId)?.state?.alive == true;

    Avatar ViewAvatar() => AvatarOf(LocalAlive() || string.IsNullOrEmpty(spectatorTargetId)
        ? myId : spectatorTargetId);

    void Handle(string line) {
        ServerMessage msg = JsonUtility.FromJson<ServerMessage>(line);
        if (msg == null || string.IsNullOrEmpty(msg.type)) return;
        switch (msg.type) {
            case "WELCOME":
                bool resumed = reconnectStartedAt >= 0;
                network.MarkWelcomed();
                myId = msg.playerId;
                network.ResumeToken = msg.resumeToken;
                network.ContinueSequenceAfter(msg.lastSeq);
                if (!string.IsNullOrEmpty(msg.syncMode)) activeSyncMode = msg.syncMode;
                configuredDemoMap = msg.demoMap ?? "";
                reconnectStartedAt = -1;
                reconnectExpired = false;
                status = resumed ? "Đã kết nối lại với server." : "";
                if (!msg.hasRoom) ResetRoomState();
                recoveryMusicPending = msg.hasRoom;
                if (resumed) Toast(msg.hasRoom ? "Đã kết nối lại vào phòng." : "Đã kết nối lại — hãy chọn phòng.");
                // The server drops a disconnected player from the matchmaking queue.
                modeMenu.StopSearch();
                break;
            case "MATCHMAKING":
                if (msg.status == "SEARCHING") {
                    status = "";
                    modeMenu.ShowSearch(msg.mode, msg.waiting, msg.minPlayers);
                } else {
                    modeMenu.StopSearch();
                    status = "Đã hủy tìm trận.";
                }
                break;
            case "MATCH_FOUND":
                modeMenu.StopSearch();
                status = "Đã tìm thấy trận!";
                break;
            case "ROOM_STATE":
                if (room == null) status = "";
                room = JsonUtility.FromJson<RoomEnvelope>(line).room;
                if (room != null) {
                    TrackReconnectDeadlines(room.players);
                    phase = room.phase;
                    activeRoundIndex = room.roundIndex;
                    if (phase == "FINISHED") {
                        journeyCompleteReceived = room.journeyComplete;
                        winnerIds = room.journeyComplete ? room.winnerIds ?? Array.Empty<string>() : Array.Empty<string>();
                        winnerId = room.journeyComplete ? (winnerIds.Length == 1 ? winnerIds[0] : "") : room.roundWinnerId;
                        if (WaitingForNextRound()) status = "Đang chờ lượt tiếp theo — bấm Sẵn sàng để tham gia.";
                    }
                    if (phase == "WAITING") {
                        gameAudio.SetMenuMusic();
                        status = "Đã vào phòng " + room.code + ". Chờ đủ người và mọi người sẵn sàng.";
                    } else if (phase == "PLAYING" && status.Contains("Đang tự thử lại"))
                        status = "Đã kết nối lại vào trận.";
                }
                break;
            case "LEFT_ROOM":
                ResetRoomState();
                status = "Đã rời phòng. Tìm trận, tạo phòng hoặc nhập mã phòng.";
                break;
            case "GAME_START":
                phase = "PLAYING"; winnerId = ""; charging = false;
                winnerIds = Array.Empty<string>();
                activeRoundIndex = msg.roundIndex;
                recoveryMusicPending = false;
                journeyCompleteReceived = false;
                journeyGuideVisible = msg.mode == "JOURNEY" && msg.chapterIndex == 0;
                matchStartedAt = Time.unscaledTime;
                spectatorTargetId = ""; roundTimeLeft = 180;
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
                break;
            case "STATE_UPDATE":
                if (room == null) break;
                // Current servers identify every snapshot so delayed packets from another
                // room/round cannot revive stale state. Older course-demo servers omitted
                // these fields; accept their ordered snapshots instead of leaving the HUD
                // permanently at zero players after GAME_START.
                bool wrongRoom = !string.IsNullOrEmpty(msg.roomCode) && msg.roomCode != room.code;
                bool wrongRound = msg.roundIndex > 0 && msg.roundIndex != activeRoundIndex;
                if (wrongRoom || wrongRound || phase == "FINISHED" && msg.phase == "PLAYING") break;
                phase = msg.phase;
                if (recoveryMusicPending) { gameAudio.SetMapMusic(msg.mapId); recoveryMusicPending = false; }
                ApplySnapshot(msg);
                break;
            case "PLAYER_EXPLODE":
                if (msg.playerId == myId) charging = false;
                if (msg.visible) {
                    Vector3 explosionPosition = new Vector3(msg.x, .45f, msg.z);
                    gameAudio.PlayExplosion(explosionPosition, msg.radius, msg.playerId == myId);
                    StartCoroutine(Explosion(explosionPosition, msg.radius));
                }
                break;
            case "PLAYER_KNOCKED_BACK":
                if (avatars.TryGetValue(msg.playerId, out Avatar knocked)) {
                    float bounce = Mathf.Lerp(3.2f, 6.2f, Mathf.InverseLerp(0, 16, msg.strength));
                    if (msg.shielded) bounce *= .45f;
                    if (msg.shielded && knocked.body.activeInHierarchy)
                        gameAudio.PlayShieldHit(msg.playerId == myId, knocked.body.transform.position);
                    knocked.verticalVelocity = Mathf.Max(knocked.verticalVelocity, bounce);
                    knocked.squash = 0;
                }
                break;
            case "ENERGY_PICKED":
                PlayPlayerSound(msg.type, msg.playerId);
                EventEffect(msg.type, msg.playerId);
                ShowStoryEvent(msg);
                break;
            case "CORE_PICKED":
                PlayPlayerSound(msg.type, msg.playerId);
                EventEffect(msg.type, msg.playerId);
                ShowStoryEvent(msg);
                break;
            case "CORE_SPAWNED":
                gameAudio.PlayDrop(false, new Vector3(msg.x, 0, msg.z));
                break;
            case "PLAYER_DAMAGED":
                PlayPlayerSound(msg.type, msg.playerId);
                EventEffect(msg.type, msg.playerId);
                ShowStoryEvent(msg);
                break;
            case "DAMAGE_REFLECTED":
                PlayPlayerSound(msg.type, msg.to);
                EventEffect(msg.type, msg.to);
                ShowStoryEvent(msg);
                break;
            case "SHIELD_ACTIVATED":
                PlayPlayerSound(msg.type, msg.playerId);
                EventEffect(msg.type, msg.playerId);
                ShowStoryEvent(msg);
                break;
            case "BOOST_ACTIVATED":
                PlayPlayerSound(msg.type, msg.playerId);
                EventEffect(msg.type, msg.playerId);
                ShowStoryEvent(msg);
                break;
            case "PLAYER_ELIMINATED":
                reconnectDeadlines.Remove(msg.playerId);
                EventEffect(msg.type, msg.playerId);
                if (msg.playerId == myId) {
                    charging = false;
                    Avatar local = AvatarOf(myId);
                    if (local?.state != null) local.state.alive = false;
                    gameAudio.StopGameplayLoops();
                }
                else PlayPlayerSound(msg.type, msg.playerId);
                ShowStoryEvent(msg);
                break;
            case "GAME_OVER":
                if (WaitingForNextRound()) break;
                phase = "FINISHED";
                winnerId = msg.winnerId;
                winnerIds = Array.Empty<string>();
                gameAudio.StopGameplayLoops();
                if (msg.mode != "JOURNEY" || msg.chapterIndex < 2) PlayResultOnce(false);
                if (msg.mode != "JOURNEY")
                    status = msg.reason == "TIME" ? "Hết 3 phút — trận đấu hòa."
                        : winnerId == myId ? "Bạn chiến thắng!" : "Trận đấu kết thúc.";
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
                journeyCompleteReceived = true;
                if (room != null) room.journeyEndReason = msg.reason;
                winnerId = msg.winnerId;
                winnerIds = msg.winnerIds ?? Array.Empty<string>();
                gameAudio.StopGameplayLoops();
                PlayResultOnce(true);
                int shards = 0;
                if (msg.scores != null) foreach (ScoreData s in msg.scores) if (s.playerId == winnerId) shards = s.roundWins;
                if (string.IsNullOrEmpty(winnerId)) status = "Hành trình kết thúc: đồng hạng.";
                else if (shards >= 3)
                    status = winnerId == myId ? "Tim Lửa Trọn Vẹn! Bạn mang về đủ cả 3 mảnh."
                        : $"Tim Lửa Trọn Vẹn! {NameOf(winnerId)} mang về đủ cả 3 mảnh.";
                else
                    status = winnerId == myId ? $"Bạn là Người Giữ Lửa với {shards} mảnh!"
                        : $"{NameOf(winnerId)} là Người Giữ Lửa với {shards} mảnh.";
                if (msg.reason == "INSUFFICIENT_PLAYERS")
                    status = "Hành trình kết thúc sớm vì không đủ người. Xếp hạng theo mảnh và lượt hạ gục đã đạt.";
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
                    ExpireReconnect();
                } else status = ErrorText(msg.code, msg.mode);
                if (msg.code == "NOT_SEARCHING") modeMenu.StopSearch();
                break;
            case "PLAYER_DISCONNECTED":
                reconnectDeadlines[msg.playerId] = Time.unscaledTime + Mathf.Max(0, msg.reconnectMs / 1000f);
                Toast($"{NameOf(msg.playerId)} mất kết nối — giữ chỗ 10 giây.");
                break;
            case "PLAYER_RECONNECTED":
                reconnectDeadlines.Remove(msg.playerId);
                if (msg.playerId != myId) Toast($"{NameOf(msg.playerId)} đã quay lại trận.");
                break;
            case "NETWORK_ERROR":
                gameAudio.StopGameplayLoops();
                if (room != null && reconnectStartedAt < 0) reconnectStartedAt = Time.unscaledTime;
                charging = false;
                status = network.LastError + " Đang thử kết nối lại...";
                modeMenu.StopSearch();
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
        spectatorTargetId = snapshot.spectatorTargetId ?? "";
        roundTimeLeft = snapshot.roundTimeLeft;
        if (snapshot.players == null) return;
        latestPlayers = snapshot.players;
        TrackReconnectDeadlines(snapshot.players);
        playerCount = snapshot.players.Length;
        aliveCount = 0;
        foreach (PlayerData p in snapshot.players) {
            if (p.alive) aliveCount++;
            playerNames[p.id] = p.name;
            if (p.id == myId) {
                if (!p.alive) charging = false;
                gameAudio.SetCharging(p.alive && p.charging, p.charge);
            }
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
                // Kenney bomb model when available; the primitive bomb below is the fallback.
                if (!BombActorView.BuildBody(body, playerColor, out Transform rollingVisual)) {
                    GameObject rollingBall = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    rollingBall.name = "Rolling ball";
                    rollingBall.transform.SetParent(body.transform, false);
                    rollingBall.GetComponent<Renderer>().material = MaterialFor(BombUi.BombBody);
                    Destroy(rollingBall.GetComponent<Collider>());
                    rollingVisual = rollingBall.transform;
                    GameObject rollBand = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    rollBand.name = "Roll band";
                    rollBand.transform.SetParent(rollingVisual, false);
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
                }
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
                BombMaterialOwner.Capture(body);
                a = new Avatar {
                    body = body, rollingVisual = rollingVisual, shield = shield,
                    chargeAura = chargeAura, boostTrail = boostTrail, color = playerColor
                };
                avatars.Add(p.id,a);
                body.transform.position = new Vector3(p.x,.55f,p.z);
            }
            a.state = p;
            if (a.body.TryGetComponent(out BombActorView actorView)) actorView.Apply(p);
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
        if (next != null && next.state == "CARRIED" && next.carrierId == myId) charging = false;
        Avatar carrier = next != null ? AvatarOf(next.carrierId) : null;
        shardView.Apply(next, effectsRoot, carrier?.body.transform, carrier?.state != null ? carrier.state.size : 1,
            next != null ? ColorOf(next.ownerId) : BombUi.Text, next != null ? ColorOf(next.challengerId) : BombUi.Charge);
    }

    /// Big centered message when the shard changes hands, derived by diffing snapshots.
    void AnnounceObjective(ObjectiveData before, ObjectiveData now) {
        if (before == null) return;
        // Objective changes arrive as snapshots. Only transitions trigger audio;
        // joining/reconnecting to an existing objective must stay silent.
        if (now.state == "CARRIED" && before.carrierId != now.carrierId)
            gameAudio.PlayCorePickup(now.carrierId == myId, new Vector3(now.x, 0, now.z));
        else if ((before.state != "GROUND" && now.state == "GROUND")
                || (before.state != "PLACED" && now.state == "PLACED"))
            gameAudio.PlayDrop(before.carrierId == myId || now.ownerId == myId,
                new Vector3(now.x, 0, now.z));
        if (before.state == "WAITING" && now.state == "GROUND") Toast("Mảnh Tim Lửa đã xuất hiện! Xem minimap.");
        else if (now.state == "CARRIED" && before.carrierId != now.carrierId) Toast($"{NameOf(now.carrierId)} đã nhặt được mảnh!");
        else if (before.state == "CARRIED" && now.state == "GROUND") {
            Avatar previousCarrier = AvatarOf(before.carrierId);
            if (previousCarrier?.state != null && !previousCarrier.state.alive)
                Toast("Người cầm mảnh bị hạ — Mảnh Tim Lửa rơi xuống!");
            else if (previousCarrier?.state != null && !previousCarrier.state.connected)
                Toast("Người cầm mất kết nối — Mảnh Tim Lửa rơi xuống!");
            else Toast("Mảnh Tim Lửa đã rơi khỏi người cầm!");
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
            case "ALREADY_SEARCHING": return "Bạn đang tìm trận. Hủy tìm trước khi tạo hoặc vào phòng.";
            case "CARRYING_SHARD": return "Đang cầm Mảnh Tim Lửa nên không thể tấn công. Nhấn Q để thả mảnh.";
            case "NOT_CARRYING_SHARD": return "Bạn không cầm Mảnh Tim Lửa để thả.";
            case "NOT_SEARCHING": return "Bạn không còn trong hàng chờ tìm trận.";
            default: return "Server từ chối: " + code;
        }
    }

    void Toast(string text) {
        toast = text;
        toastUntil = Time.unscaledTime + 3;
    }

    Avatar AvatarOf(string id) => !string.IsNullOrEmpty(id) && avatars.TryGetValue(id, out Avatar a) ? a : null;

    void PlayPlayerSound(string kind, string id) {
        // Metadata events also arrive for hidden opponents. Never invent an audio
        // origin or use a hidden avatar's last visible position.
        if (!TryEffectPosition(id, out Vector3 at)) return;
        bool local = id == myId;
        switch (kind) {
            case "ENERGY_PICKED": gameAudio.PlayEnergyPickup(local, at); break;
            case "CORE_PICKED": gameAudio.PlayCorePickup(local, at); break;
            case "SHIELD_ACTIVATED": gameAudio.PlayShield(local, at); break;
            case "BOOST_ACTIVATED": gameAudio.PlayBoost(local, at); break;
            default: gameAudio.PlayDamage(local, at); break;
        }
    }

    string NameOf(string id) =>
        id == myId ? "Bạn" : !string.IsNullOrEmpty(id) && playerNames.TryGetValue(id, out string n) ? n : "Ai đó";

    Color ColorOf(string id) => AvatarOf(id)?.color ?? BombUi.Text;

    void ApplyMap(ServerMessage snapshot) {
        // GAME_START and snapshots advance chapter/map even when ROOM_STATE has not arrived.
        if (room != null) {
            if (snapshot.roundParticipantIds != null) room.roundParticipantIds = snapshot.roundParticipantIds;
            room.phase = phase;
            room.roundIndex = snapshot.roundIndex;
            room.chapterIndex = snapshot.chapterIndex;
            room.mode = snapshot.mode;
            room.mapId = snapshot.mapId; room.mapName = snapshot.mapName;
            room.arenaSize = snapshot.arenaSize;
            if (snapshot.scores != null) room.scores = snapshot.scores;
        }
        if (string.IsNullOrEmpty(snapshot.mapId) || snapshot.mapId == activeMapId) return;
        activeMapId = snapshot.mapId;
        activeMapName = snapshot.mapName;
        activeArenaSize = snapshot.arenaSize;
        activeBushes = snapshot.bushes ?? Array.Empty<BushData>();
        activeObstacles = snapshot.obstacles ?? Array.Empty<ObstacleData>();

        if (authoredEnvironment != null) authoredEnvironment.SetActive(false);
        mapView.Build(activeMapId, activeArenaSize, activeObstacles, activeBushes);
    }

    void SyncPickups(PickupData[] pickups, Dictionary<int, GameObject> views, float size, Color color) {
        HashSet<int> seen = new HashSet<int>();
        if (pickups != null) foreach (PickupData p in pickups) {
            seen.Add(p.id);
            if (!views.TryGetValue(p.id, out GameObject view)) {
                view = BombActorView.CreatePickup(size, color, pickupsRoot);
                if (view == null) {
                    view = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    view.name = size > .3f ? "Core" : "Energy";
                    if (pickupsRoot != null) view.transform.SetParent(pickupsRoot, false);
                    view.transform.localScale = Vector3.one * size * 2;
                    view.GetComponent<Renderer>().sharedMaterial = BombActorView.GlowMaterial(color);
                    Destroy(view.GetComponent<Collider>());
                }
                views[p.id] = view;
            }
            view.transform.position = new Vector3(p.x, size + .08f, p.z);
        }
        foreach (int id in new List<int>(views.Keys)) if (!seen.Contains(id)) {
            Destroy(views[id]); views.Remove(id);
        }
    }

    /// Where to draw an event effect for this player; false while they are hidden or unknown.
    bool TryEffectPosition(string id, out Vector3 position) {
        Avatar avatar = AvatarOf(id);
        position = avatar?.body != null ? avatar.body.transform.position : Vector3.zero;
        return avatar?.body != null && avatar.body.activeInHierarchy;
    }

    /// Particle feedback for gameplay events, drawn at the player's feet.
    void EventEffect(string kind, string id) {
        if (!TryEffectPosition(id, out Vector3 at)) return;
        Vector3 ground = new Vector3(at.x, 0, at.z);
        switch (kind) {
            case "ENERGY_PICKED": BombVfx.PickupSparkle(ground, BombUi.Energy, false, effectsRoot); break;
            case "CORE_PICKED": BombVfx.PickupSparkle(ground, BombUi.Core, true, effectsRoot); break;
            case "PLAYER_DAMAGED":
            case "DAMAGE_REFLECTED": BombVfx.Hit(ground, effectsRoot); break;
            case "SHIELD_ACTIVATED": BombVfx.ShieldRing(ground, effectsRoot); break;
            case "BOOST_ACTIVATED": BombVfx.BoostBurst(ground, effectsRoot); break;
            case "PLAYER_ELIMINATED": BombVfx.EliminationPuff(ground, ColorOf(id), effectsRoot); break;
        }
    }

    IEnumerator Explosion(Vector3 position, float radius) {
        if (BombVfx.Explosion(position, radius, effectsRoot)) yield break;
        GameObject effect = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        effect.name = "Explosion";
        if (effectsRoot != null) effect.transform.SetParent(effectsRoot, false);
        Destroy(effect.GetComponent<Collider>());
        effect.GetComponent<Renderer>().material = TransparentMaterial(BombUi.WithAlpha(BombUi.Charge,.4f));
        BombMaterialOwner.Capture(effect);
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


    internal static Material MaterialFor(Color color) {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var material = new Material(shader) { name = BombMaterialOwner.Prefix + "opaque" };
        material.color = color;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor",color);
        return material;
    }

    internal static Material TransparentMaterial(Color color) {
        var material = new Material(Shader.Find("Standard")) { name = BombMaterialOwner.Prefix + "transparent" };
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
        latestPlayers = Array.Empty<PlayerData>();
        reconnectDeadlines.Clear();
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
            if (hud.DrawReconnect(remaining, network.Endpoint, network.RetryAttempt)) ReturnToMenuAfterReconnect();
        }
        switch (overlay.Draw(room != null, phase == "PLAYING")) {
            case BombOverlay.Action.LeaveRoom:
                network.Send(new BombMessage { type = "LEAVE_ROOM" });
                break;
            case BombOverlay.Action.QuitGame:
                QuitGame();
                break;
        }
        BombUi.DrawTooltip();
    }

    void DrawScreen() {
        EnsureScreenStyles();
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
                case BombModeMenu.Choice.FindMatch:
                    status = "";
                    RunAfterJourneyIntro(() => network.Send(new BombMessage {
                        type = "FIND_MATCH", mode = modeMenu.SelectedMode
                    }));
                    break;
                case BombModeMenu.Choice.CancelMatch:
                    network.Send(new BombMessage { type = "CANCEL_MATCH" });
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
            if (journeyGuideVisible) DrawJourneyGuide();
            return;
        }
        // The result card carries the outcome text itself, so the lobby stays hidden behind it.
        if (phase == "FINISHED" && !WaitingForNextRound()) DrawResultPopup();
        else DrawLobby();
    }

    float U(float value) => value * ui;
    int Px(float value) => Mathf.RoundToInt(value * ui);

    void EnsureScreenStyles() {
        BombUi.EnsureShapes();
        ui = Mathf.Clamp(Screen.height / 1080f, .75f, 1.6f);
        if (titleStyle != null && Mathf.Approximately(uiStylesScale, ui)) return;
        uiStylesScale = ui;
        titleStyle = BombUi.TextStyle(Px(28), FontStyle.Bold, BombUi.Text, TextAnchor.MiddleLeft);
        bodyStyle = BombUi.TextStyle(Px(17), FontStyle.Normal, BombUi.Text, TextAnchor.UpperLeft);
        bodyStyle.wordWrap = true;
        smallStyle = BombUi.TextStyle(Px(14), FontStyle.Normal, BombUi.TextMuted, TextAnchor.MiddleLeft);
        smallStyle.wordWrap = true;
        sectionStyle = BombUi.TextStyle(Px(13), FontStyle.Bold, BombUi.TextFaint, TextAnchor.MiddleLeft);
        sectionRightStyle = BombUi.TextStyle(Px(13), FontStyle.Bold, BombUi.TextFaint, TextAnchor.MiddleRight);
        codeStyle = BombUi.TextStyle(Px(46), FontStyle.Bold, BombUi.Charge, TextAnchor.MiddleLeft);
        nameStyle = BombUi.TextStyle(Px(18), FontStyle.Bold, BombUi.Text, TextAnchor.MiddleLeft);
        nameStyle.clipping = TextClipping.Clip;
        badgeStyle = BombUi.TextStyle(Px(12), FontStyle.Bold, BombUi.Text, TextAnchor.MiddleCenter);
        cellStyle = BombUi.TextStyle(Px(15), FontStyle.Normal, BombUi.Text, TextAnchor.MiddleLeft);
        cellStyle.clipping = TextClipping.Clip;
        cellRightStyle = BombUi.TextStyle(Px(15), FontStyle.Bold, BombUi.Text, TextAnchor.MiddleRight);
        buttonStyle = BombUi.TextStyle(Px(19), FontStyle.Bold, BombUi.Night, TextAnchor.MiddleCenter);
        quietButtonStyle = BombUi.TextStyle(Px(15), FontStyle.Bold, BombUi.Text, TextAnchor.MiddleCenter);
        resultTitleStyle = BombUi.TextStyle(Px(40), FontStyle.Bold, BombUi.Charge, TextAnchor.MiddleCenter);
        centeredBodyStyle = BombUi.TextStyle(Px(18), FontStyle.Bold, BombUi.Text, TextAnchor.MiddleCenter);
        centeredBodyStyle.wordWrap = true;
        centeredMutedStyle = BombUi.TextStyle(Px(15), FontStyle.Normal, BombUi.TextMuted, TextAnchor.MiddleCenter);
        centeredMutedStyle.wordWrap = true;
    }

    /// Room lobby: mode, shareable code, map, roster with ready badges, journey scores and actions.
    /// The card hugs its content; player rows shrink before the card would leave the screen.
    void DrawLobby() {
        int count = room.players == null ? 0 : room.players.Length;
        int capacity = room.maxPlayers > 0 ? room.maxPlayers : 10;
        int minimum = room.minPlayers > 0 ? room.minPlayers : 2;
        int scoreRows = room.scores == null ? 0 : room.scores.Length;
        bool journey = room.mode == "JOURNEY";
        bool showStatus = !string.IsNullOrEmpty(status);

        float pad = U(32), gap = U(6);
        float topH = U(154) + (showStatus ? U(40) : 0) + U(34);
        float scoresH = scoreRows > 0 ? U(38) + (scoreRows + 1) * U(26) : 0;
        float actionsH = room.journeyComplete ? U(44) : U(168);
        float fixedH = topH + scoresH + U(14) + actionsH + pad;
        float width = Mathf.Min(U(580), Screen.width - 32);
        float maxHeight = Screen.height - 32;
        int rows = Mathf.Max(1, count);
        float rowH = Mathf.Clamp((maxHeight - fixedH) / rows - gap, U(28), U(46));
        float height = Mathf.Min(maxHeight, fixedH + rows * (rowH + gap));
        Rect card = new Rect((Screen.width - width) * .5f, (Screen.height - height) * .5f, width, height);
        Color modeAccent = journey ? BombUi.Core : BombUi.Charge;
        BombUi.Card(card, modeAccent, ui);

        float x = card.x + pad, w = card.width - pad * 2, y = card.y + U(28);
        sectionStyle.normal.textColor = modeAccent;
        GUI.Label(new Rect(x, y, w, U(24)),
            journey ? $"HÀNH TRÌNH BA MẢNH  •  CHƯƠNG {room.chapterIndex + 1}/3" : "ĐẤU NHANH", sectionStyle);
        sectionStyle.normal.textColor = BombUi.TextFaint;
        if (!string.IsNullOrEmpty(room.mapName))
            DrawChip(x + w, y, U(24), room.mapName.ToUpperInvariant(), BombUi.MapAccent(room.mapId));
        y += U(36);
        GUI.Label(new Rect(x, y, w, U(18)), "MÃ PHÒNG", sectionStyle);
        y += U(20);
        GUI.Label(new Rect(x, y, w - U(170), U(56)), room.code, codeStyle);
        bool copied = Time.unscaledTime < codeCopiedUntil;
        if (BombUi.Button(new Rect(x + w - U(160), y + U(6), U(160), U(44)), copied ? "ĐÃ SAO CHÉP ✓" : "SAO CHÉP",
                copied ? BombUi.Health : QuietFace, copied ? BombUi.Night : BombUi.Text, quietButtonStyle,
                true, "Sao chép mã phòng để gửi cho bạn bè")) {
            GUIUtility.systemCopyBuffer = room.code;
            codeCopiedUntil = Time.unscaledTime + 1.8f;
        }
        y += U(70);
        if (showStatus) {
            GUI.Label(new Rect(x, y, w, U(34)), status, smallStyle);
            y += U(40);
        }

        GUI.Label(new Rect(x, y, w, U(24)), $"NGƯỜI CHƠI  {count}/{capacity}", sectionStyle);
        GUI.Label(new Rect(x, y, w, U(24)), $"SẴN SÀNG  {ReadyCount()}/{count}", sectionRightStyle);
        y += U(34);
        for (int i = 0; i < count; i++) {
            LobbyPlayer p = room.players[i];
            bool isMe = p.id == myId;
            Rect row = new Rect(x, y, w, rowH);
            BombUi.Fill(row, isMe ? BombUi.WithAlpha(BombUi.Charge, .16f) : new Color(0, 0, 0, .22f), BombUi.RoundSmall);
            Color color = AvatarOf(p.id)?.color ?? playerColors[i % playerColors.Length];
            BombUi.Fill(BombUi.Centered(new Vector2(row.x + U(22), row.center.y), Mathf.Min(U(16), rowH * .45f)), color, BombUi.Circle);
            string state;
            Color stateColor;
            if (!p.connected) {
                state = $"MẤT KẾT NỐI  {Mathf.CeilToInt(ReconnectRemaining(p.id, p.reconnectRemaining))}s";
                stateColor = BombUi.Danger;
            } else if (p.ready) {
                state = "SẴN SÀNG";
                stateColor = BombUi.Health;
            } else {
                state = "ĐANG CHỜ";
                stateColor = BombUi.TextMuted;
            }
            float chipH = Mathf.Min(U(26), rowH - U(10));
            float chipW = DrawChip(row.xMax - U(10), row.center.y - chipH / 2, chipH, state, stateColor);
            GUI.Label(new Rect(row.x + U(40), row.y, row.width - U(60) - chipW, rowH), isMe ? p.name + "  (bạn)" : p.name, nameStyle);
            y += rowH + gap;
        }
        if (scoreRows > 0) {
            y += U(8);
            GUI.Label(new Rect(x, y, w, U(22)), "MẢNH TIM LỬA", sectionStyle);
            y += U(30);
            DrawScoreTable(new Rect(x, y, w, (scoreRows + 1) * U(26)), room.scores);
        }

        float by = card.yMax - pad - U(44);
        if (BombUi.Button(new Rect(x, by, w, U(44)), "RỜI PHÒNG", QuietFace, BombUi.Text, quietButtonStyle))
            network.Send(new BombMessage { type = "LEAVE_ROOM" });
        if (room.journeyComplete) return;
        bool myReady = IsLocalPlayerReady();
        by -= U(72);
        if (BombUi.Button(new Rect(x, by, w, U(60)), myReady ? "BỎ SẴN SÀNG" : "SẴN SÀNG",
                myReady ? QuietFace : BombUi.Health, myReady ? BombUi.Text : BombUi.Night, buttonStyle,
                true, "Phím tắt: R hoặc Enter"))
            ToggleReady();
        by -= U(52);
        int missing = minimum - count;
        GUI.Label(new Rect(x, by, w, U(44)), (missing > 0
            ? $"Cần thêm {missing} người nữa mới bắt đầu được. Gửi mã phòng cho bạn bè."
            : $"Bắt đầu khi mọi người sẵn sàng; tối thiểu {minimum}, tối đa {capacity} người.") + "  Phím tắt: R.", smallStyle);
    }

    /// Small rounded label tinted with its accent, sized to the text and right-aligned at xMax.
    /// Returns the chip width so callers can keep text clear of it.
    float DrawChip(float xMax, float y, float height, string text, Color accent) {
        badgeStyle.normal.textColor = accent;
        float width = badgeStyle.CalcSize(new GUIContent(text)).x + U(22);
        Rect chip = new Rect(xMax - width, y, width, height);
        BombUi.Fill(chip, BombUi.WithAlpha(accent, .2f), BombUi.RoundSmall);
        GUI.Label(chip, text, badgeStyle);
        return width;
    }

    /// Journey standings: one header row then one row per player, the local player in orange.
    void DrawScoreTable(Rect area, ScoreData[] scores) {
        float rowH = U(26), column = U(64);
        float nameWidth = area.width - column * 3;
        Rect Cell(float rowY, int index) => new Rect(area.x + nameWidth + column * index, rowY, column, rowH);
        GUI.Label(new Rect(area.x, area.y, nameWidth, rowH), "NGƯỜI CHƠI", sectionStyle);
        GUI.Label(Cell(area.y, 0), "MẢNH", sectionRightStyle);
        GUI.Label(Cell(area.y, 1), "HẠ", sectionRightStyle);
        GUI.Label(Cell(area.y, 2), "ĐIỂM", sectionRightStyle);
        float y = area.y + rowH;
        foreach (ScoreData score in scores) {
            Color ink = score.playerId == myId ? BombUi.Charge : BombUi.Text;
            cellStyle.normal.textColor = cellRightStyle.normal.textColor = ink;
            GUI.Label(new Rect(area.x, y, nameWidth, rowH), score.name, cellStyle);
            GUI.Label(Cell(y, 0), score.roundWins.ToString(), cellRightStyle);
            GUI.Label(Cell(y, 1), score.eliminations.ToString(), cellRightStyle);
            GUI.Label(Cell(y, 2), score.points.ToString(), cellRightStyle);
            y += rowH;
        }
        cellStyle.normal.textColor = cellRightStyle.normal.textColor = BombUi.Text;
    }

    int ReadyCount() {
        int ready = 0;
        if (room?.players != null) foreach (LobbyPlayer player in room.players) if (player.ready) ready++;
        return ready;
    }

    void ToggleReady() => network.Send(new BombMessage { type = "READY", ready = !IsLocalPlayerReady() });

    bool JourneyFinished() => room != null && room.mode == "JOURNEY"
        && (room.journeyComplete || journeyCompleteReceived || room.chapterIndex >= 2);

    bool JourneyHasNextChapter() => room != null && room.mode == "JOURNEY" && room.chapterIndex < 2
        && !room.journeyComplete && !journeyCompleteReceived;

    /// R / Enter: toggle ready in the lobby, ready up for the next round, or close the journey guide.
    void HandleRoomShortcut(bool enter) {
        if (phase == "PLAYING") {
            if (enter && journeyGuideVisible) {
                BombAudio.UiClick();
                journeyGuideVisible = false;
            }
            return;
        }
        if (phase == "FINISHED") {
            if (JourneyFinished() || IsLocalPlayerReady()) return;
            BombAudio.UiClick();
            network.Send(new BombMessage { type = "READY", ready = true });
            return;
        }
        if (room.journeyComplete) return;
        BombAudio.UiClick();
        ToggleReady();
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
            roundTimeLeft = roundTimeLeft, spectatorName = string.IsNullOrEmpty(spectatorTargetId) ? "" : NameOf(spectatorTargetId),
            inBush = state != null && BushIndex(state.x, state.z) >= 0,
            rttMs = rttMs, serverMicros = serverMicros,
            sentPerSecond = sentPerSecond, receivedPerSecond = receivedPerSecond,
            sentBytesPerSecond = sentBytesPerSecond, receivedBytesPerSecond = receivedBytesPerSecond,
            showNetwork = BombSettings.ShowNetwork,
            endpoint = network.Endpoint, syncMode = activeSyncMode, demoMap = configuredDemoMap,
            networkSilence = network.SecondsSinceLastReceive,
            disconnectedPlayers = DisconnectedPlayerLabels(),
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
                    a.state.connected,
                    a.state.reconnectRemaining,
                    objective != null && objective.carrierId == entry.Key,
                    objective != null ? objective.fireLock : 0,
                    objective != null ? objective.fireLockMax : 2);
        }
        DrawStoryNotice();
    }

    /// Story/event notice. Sits at ~28% of the screen height, below the top-centre objective panel
    /// and toast, fades in and out and drains a thin line along its bottom edge.
    void DrawStoryNotice() {
        if (activeNotice == null) return;
        if (noticeTitleStyle == null || !Mathf.Approximately(noticeStylesScale, ui)) {
            noticeStylesScale = ui;
            noticeTitleStyle = BombUi.TextStyle(Px(17), FontStyle.Bold, BombUi.Text);
            noticeBodyStyle = BombUi.TextStyle(Px(15), FontStyle.Normal, BombUi.Text);
            noticeBodyStyle.wordWrap = true;
        }
        float remaining = noticeEndsAt - Time.unscaledTime;
        float fadeIn = Mathf.Clamp01((Time.unscaledTime - noticeStartedAt) / .18f);
        float fadeOut = Mathf.Clamp01(remaining / .22f);
        float alpha = Mathf.Min(fadeIn, fadeOut);
        float width = Mathf.Min(U(620), Screen.width - 44);
        Rect card = new Rect((Screen.width - width) * .5f, Screen.height * .28f, width, U(92));
        Color accent = activeNotice.accent;
        BombUi.Fill(card, BombUi.WithAlpha(HudPanel, alpha), BombUi.RoundSmall);
        BombUi.Fill(new Rect(card.x + U(8), card.y + U(12), U(5), card.height - U(24)), BombUi.WithAlpha(accent, alpha), BombUi.RoundSmall);
        BombUi.Fill(new Rect(card.x + U(12), card.yMax - U(7), (card.width - U(24)) * Mathf.Clamp01(remaining / activeNotice.duration), U(3)),
            BombUi.WithAlpha(accent, alpha), null);
        Color previous = GUI.color;
        GUI.color = new Color(1, 1, 1, alpha);
        GUI.Label(new Rect(card.x + U(24), card.y + U(10), card.width - U(42), U(26)), activeNotice.title, noticeTitleStyle);
        GUI.Label(new Rect(card.x + U(24), card.y + U(37), card.width - U(42), U(48)), activeNotice.body, noticeBodyStyle);
        GUI.color = previous;
    }

    /// End-of-round card: outcome, why it ended, journey standings, then ready-up and leave.
    bool WaitingForNextRound() => phase == "FINISHED" && room?.mode == "QUICK"
        && room.roundParticipantIds != null && Array.IndexOf(room.roundParticipantIds, myId) < 0;

    void DrawResultPopup() {
        MatchOutcome outcome = BombResult.Resolve(myId, winnerId, winnerIds);
        bool won = outcome == MatchOutcome.Win || outcome == MatchOutcome.SharedWin;
        bool draw = outcome == MatchOutcome.Draw;
        bool completedJourney = JourneyFinished();
        bool nextChapter = JourneyHasNextChapter();
        bool ready = IsLocalPlayerReady();
        int scoreRows = room != null && room.mode == "JOURNEY" && room.scores != null ? room.scores.Length : 0;
        Color accent = nextChapter || draw ? BombUi.Core : won ? BombUi.Charge : BombUi.Danger;
        string title = nextChapter ? "SANG CHƯƠNG MỚI" : outcome == MatchOutcome.SharedWin ? "ĐỒNG CHIẾN THẮNG!"
            : draw ? "HÒA!" : won ? "CHIẾN THẮNG!" : "BẠN ĐÃ THUA";
        string body;
        if (nextChapter)
            body = string.IsNullOrEmpty(winnerId) ? "Chương này kết thúc mà chưa ai giữ được Mảnh Tim Lửa."
                : winnerId == myId ? "Bạn đã giành Mảnh Tim Lửa của chương này!"
                : NameOf(winnerId) + " đã giành Mảnh Tim Lửa của chương này.";
        else body = outcome == MatchOutcome.SharedWin ? "Bạn cùng đứng đầu bảng Hành trình."
            : draw ? "Trận đấu kết thúc mà không có người chiến thắng." : won ? "Bạn là người chiến thắng chung cuộc." : WinnerText();
        string detail = nextChapter ? $"Tiếp theo: Chương {room.chapterIndex + 2}/3. Điểm và số mảnh được giữ lại." : "";
        if (completedJourney && room?.journeyEndReason == "INSUFFICIENT_PLAYERS")
            detail = "Kết thúc sớm do thiếu người. Chỉ tính các chương đã chơi; không cộng mảnh của chương chưa diễn ra.";
        else if (room?.mode == "QUICK" && room.roundEndReason == "TIME")
            detail = "Hết 3 phút mà vẫn còn nhiều người sống: hòa.";
        int count = room?.players == null ? 0 : room.players.Length;
        string readyLine = completedJourney ? "Rời phòng để bắt đầu một hành trình mới."
            : ready ? $"Đã sẵn sàng  •  {ReadyCount()}/{count} người"
            : $"Nhấn R hoặc Enter để sẵn sàng  •  {ReadyCount()}/{count} người đã sẵn sàng";

        float pad = U(32);
        float width = Mathf.Min(U(560), Screen.width - 32);
        float w = width - pad * 2;
        float statusH = string.IsNullOrEmpty(status) ? 0 : centeredMutedStyle.CalcHeight(new GUIContent(status), w) + U(8);
        float bodyH = centeredBodyStyle.CalcHeight(new GUIContent(body), w) + U(6);
        float detailH = detail.Length == 0 ? 0 : centeredMutedStyle.CalcHeight(new GUIContent(detail), w) + U(6);
        float scoresH = scoreRows > 0 ? U(14) + (scoreRows + 1) * U(26) : 0;
        float height = U(86) + statusH + bodyH + detailH + scoresH + U(40) + U(114) + pad;
        height = Mathf.Min(height, Screen.height - 32);
        Rect card = new Rect((Screen.width - width) * .5f, (Screen.height - height) * .5f, width, height);
        BombUi.Card(card, accent, ui);

        float x = card.x + pad, y = card.y + U(30);
        resultTitleStyle.normal.textColor = accent;
        GUI.Label(new Rect(x, y, w, U(52)), title, resultTitleStyle);
        y += U(56);
        if (statusH > 0) {
            GUI.Label(new Rect(x, y, w, statusH), status, centeredMutedStyle);
            y += statusH;
        }
        GUI.Label(new Rect(x, y, w, bodyH), body, centeredBodyStyle);
        y += bodyH;
        if (detailH > 0) {
            GUI.Label(new Rect(x, y, w, detailH), detail, centeredMutedStyle);
            y += detailH;
        }
        if (scoreRows > 0) DrawScoreTable(new Rect(x, y + U(14), w, (scoreRows + 1) * U(26)), room.scores);

        float by = card.yMax - pad - U(44);
        if (BombUi.Button(new Rect(x, by, w, U(44)), "RỜI PHÒNG", QuietFace, BombUi.Text, quietButtonStyle))
            network.Send(new BombMessage { type = "LEAVE_ROOM" });
        by -= U(70);
        Rect primary = new Rect(x, by, w, U(58));
        if (completedJourney) BombUi.Button(primary, "HÀNH TRÌNH ĐÃ KẾT THÚC", accent, BombUi.Night, buttonStyle, false);
        else if (BombUi.Button(primary, ready ? "ĐANG CHỜ NGƯỜI CHƠI KHÁC..."
                    : nextChapter ? $"SẴN SÀNG SANG CHƯƠNG {room.chapterIndex + 2}" : "CHƠI LẠI",
                BombUi.Health, BombUi.Night, buttonStyle, !ready, "Phím tắt: R hoặc Enter"))
            network.Send(new BombMessage { type = "READY", ready = true });
        GUI.Label(new Rect(x, by - U(36), w, U(28)), readyLine, centeredMutedStyle);
    }

    static readonly string[] JourneySteps = {
        "Mảnh Tim Lửa xuất hiện sau 10 giây — tìm viên pha lê hồng trên minimap.",
        "Chạm để mang mảnh về bệ đá giữa bản đồ. Nổ trúng người giữ 2 lần bằng cú nạp đủ mạnh để làm rơi mảnh.",
        "Chờ bảo hộ bệ hết rồi đứng trong vòng để phá ấn: 3 giây, hoặc 6 giây nếu chủ mảnh đang thủ bệ. Giữ mảnh đến hết giờ để thắng chương.",
        "Khi cầm mảnh, bạn không thể nổ bom; nhấn Q để thả mảnh. E dùng Khiên • Shift Tăng tốc."
    };

    void DrawJourneyGuide() {
        const string footer = "Sau 3 chương: nhiều mảnh nhất thắng; nếu hòa, xét số lần hạ đối thủ rồi người thắng chương cuối. Vẫn hòa thì đồng chiến thắng. Điểm chỉ để thống kê.";
        float pad = U(28), number = U(28);
        float width = Mathf.Min(U(560), Screen.width - 32);
        float w = width - pad * 2;
        float textW = w - number - U(14);
        float stepsH = 0;
        foreach (string step in JourneySteps)
            stepsH += Mathf.Max(number, bodyStyle.CalcHeight(new GUIContent(step), textW)) + U(12);
        float footerH = smallStyle.CalcHeight(new GUIContent(footer), w);
        float height = Mathf.Min(U(72) + stepsH + footerH + U(24) + U(52) + pad, Screen.height - 32);
        float top = Screen.height >= 620 ? Mathf.Max(U(164), (Screen.height - height) * .5f) : 16;
        top = Mathf.Max(16, Mathf.Min(top, Screen.height - height - 16));
        Rect panel = new Rect(16, top, width, height);
        BombUi.Card(panel, BombUi.Core, ui);

        float x = panel.x + pad, y = panel.y + U(24);
        GUI.Label(new Rect(x, y, w, U(40)), "CÁCH CHƠI CỐT TRUYỆN", titleStyle);
        y += U(48);
        badgeStyle.normal.textColor = BombUi.Night;
        for (int i = 0; i < JourneySteps.Length; i++) {
            float stepH = Mathf.Max(number, bodyStyle.CalcHeight(new GUIContent(JourneySteps[i]), textW));
            Rect badge = new Rect(x, y, number, number);
            BombUi.Fill(badge, BombUi.Core, BombUi.Circle);
            GUI.Label(badge, (i + 1).ToString(), badgeStyle);
            GUI.Label(new Rect(x + number + U(14), y + U(3), textW, stepH), JourneySteps[i], bodyStyle);
            y += stepH + U(12);
        }
        GUI.Label(new Rect(x, y, w, footerH), footer, smallStyle);

        Rect button = new Rect(x, panel.yMax - pad - U(52), w * .5f, U(52));
        if (BombUi.Button(button, "ĐÃ HIỂU", BombUi.Core, BombUi.Night, buttonStyle, true, "Phím tắt: Enter"))
            journeyGuideVisible = false;
        sectionRightStyle.normal.textColor = BombUi.TextMuted;
        GUI.Label(new Rect(button.xMax + U(12), button.y, w - button.width - U(12), button.height), "NHẤN H ĐỂ MỞ LẠI", sectionRightStyle);
        sectionRightStyle.normal.textColor = BombUi.TextFaint;
    }

    string WinnerText() {
        if (winnerIds.Length > 1) return "Những người đồng chiến thắng: " + string.Join(", ", Array.ConvertAll(winnerIds, NameOf)) + ".";
        if (string.IsNullOrEmpty(winnerId)) return "Không còn người chơi sống sót.";
        if (room?.players != null)
            foreach (LobbyPlayer player in room.players)
                if (player.id == winnerId) return player.name + " đã chiến thắng.";
        return "Đối thủ đã chiến thắng.";
    }

    void PlayResultOnce(bool finalJourney) {
        string key = (room?.code ?? "") + ":" + activeRoundIndex + ":" + finalJourney;
        if (lastResultSound == key) return;
        lastResultSound = key;
        MatchOutcome result = BombResult.Resolve(myId, winnerId, winnerIds);
        if (result != MatchOutcome.Draw) gameAudio.PlayResult(result != MatchOutcome.Loss);
    }

    void ResetRoomState() {
        modeMenu.StopSearch();
        room = null; phase = "MENU"; winnerId = ""; activeRoundIndex = 0;
        winnerIds = Array.Empty<string>(); lastResultSound = "";
        journeyGuideVisible = false; journeyCompleteReceived = false; charging = false;
        spectatorTargetId = ""; roundTimeLeft = 180;
        gameAudio.StopGameplayLoops(); gameAudio.SetMenuMusic();
        ClearNotices(); ClearActors();
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
            mainMenu.ShowNetworkSettings();
            return;
        }
        if (!int.TryParse(port, out int parsed) || parsed < 1 || parsed > 65535) {
            status = "Cấu hình server có port không hợp lệ.";
            mainMenu.ShowNetworkSettings();
            return;
        }
        PlayerPrefs.SetString("BombServerHost", host);
        PlayerPrefs.SetString("BombServerPort", port);
        PlayerPrefs.Save();
        status = "Đang vào server...";
        network.Connect(host, parsed, playerName);
    }

    void ExpireReconnect() {
        if (reconnectExpired) return;
        if (reconnectStartedAt < 0) reconnectStartedAt = Time.unscaledTime - ReconnectWindowSeconds;
        reconnectExpired = true;
        charging = false;
        network.Dispose();
        network.ResumeToken = "";
        status = "Phiên chơi đã hết hạn sau 10 giây không kết nối được.";
        gameAudio.StopGameplayLoops();
    }

    void ReturnToMenuAfterReconnect() {
        reconnectStartedAt = -1;
        reconnectExpired = false;
        myId = "";
        ResetRoomState();
        latestPlayers = Array.Empty<PlayerData>();
        ClearNotices();
        ClearActors();
        gameAudio.SetMenuMusic();
        status = "Phiên trước đã hết hạn. Hãy kết nối lại để vào server.";
    }

    string[] DisconnectedPlayerLabels() {
        var labels = new List<string>();
        foreach (PlayerData player in latestPlayers) {
            if (player == null || !player.alive || player.connected) continue;
            labels.Add($"{player.name}  •  còn {Mathf.CeilToInt(ReconnectRemaining(player.id, player.reconnectRemaining))}s");
        }
        return labels.ToArray();
    }

    float ReconnectRemaining(string playerId, float fallback) {
        return reconnectDeadlines.TryGetValue(playerId, out float deadline)
            ? Mathf.Max(0, deadline - Time.unscaledTime)
            : Mathf.Max(0, fallback);
    }

    void TrackReconnectDeadlines(PlayerData[] players) {
        if (players == null) return;
        foreach (PlayerData player in players) {
            if (player == null) continue;
            if (player.connected) reconnectDeadlines.Remove(player.id);
            else if (!reconnectDeadlines.ContainsKey(player.id))
                reconnectDeadlines[player.id] = Time.unscaledTime + Mathf.Max(0, player.reconnectRemaining);
        }
    }

    void TrackReconnectDeadlines(LobbyPlayer[] players) {
        if (players == null) return;
        foreach (LobbyPlayer player in players) {
            if (player == null) continue;
            if (player.connected) reconnectDeadlines.Remove(player.id);
            else if (!reconnectDeadlines.ContainsKey(player.id))
                reconnectDeadlines[player.id] = Time.unscaledTime + Mathf.Max(0, player.reconnectRemaining);
        }
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

    int BushIndex(float x, float z) {
        for (int i = 0; i < activeBushes.Length; i++) {
            float dx = x - activeBushes[i].x;
            float dz = z - activeBushes[i].z;
            if (dx * dx + dz * dz <= activeBushes[i].radius * activeBushes[i].radius) return i;
        }
        return -1;
    }
}
