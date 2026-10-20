using System;
using System.Collections;
using System.Collections.Generic;
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
    }

    readonly BombNetwork network = new BombNetwork();
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
    int previousSent, previousReceived, sentPerSecond, receivedPerSecond;
    long previousSentBytes, previousReceivedBytes, sentBytesPerSecond, receivedBytesPerSecond;
    bool charging;
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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap() {
        if (FindFirstObjectByType<BombGame>() == null) new GameObject("Bomb Me Baby").AddComponent<BombGame>();
    }

    void Awake() {
        playerName = "Bom " + UnityEngine.Random.Range(100, 999);
        if (gameCamera == null) gameCamera = Camera.main;
        if (actorsRoot == null) actorsRoot = FindSceneRoot("Players");
        if (pickupsRoot == null) pickupsRoot = FindSceneRoot("Pickups");
        if (effectsRoot == null) effectsRoot = FindSceneRoot("Effects");
        authoredEnvironment = GameObject.Find("Environment");
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
        if (Input.GetKeyDown(KeyCode.Escape)) {
            if (overlay.IsOpen) overlay.Back();
            else if (room == null && !string.IsNullOrEmpty(myId)) modeMenu.Back();
            else if (room != null) {
                overlay.OpenPause();
                // The match keeps running on the server: stop rolling instead of drifting.
                if (phase == "PLAYING") network.Send(new BombMessage { type = "MOVE", directionX = 0, directionZ = 0 });
            }
        }
        if (phase == "PLAYING" && !overlay.IsOpen && network.Connected && !string.IsNullOrEmpty(myId)) HandleInput();
        foreach (Avatar avatar in avatars.Values) {
            if (avatar.body == null) continue;
            Vector3 previous = avatar.body.transform.position;
            float smoothing = 1 - Mathf.Exp(-22 * Time.deltaTime);
            avatar.body.transform.position = Vector3.Lerp(previous, avatar.target, smoothing);
            Vector3 travel = avatar.body.transform.position - previous;
            travel.y = 0;
            if (avatar.rollingVisual != null && travel.sqrMagnitude > .000001f) {
                float radius = Mathf.Max(.05f, avatar.body.transform.localScale.x * .5f);
                Vector3 axis = Vector3.Cross(Vector3.up, travel.normalized);
                avatar.rollingVisual.Rotate(axis, travel.magnitude / radius * Mathf.Rad2Deg, Space.World);
            }
            if (avatar.state != null) {
                float scale = avatar.state.size;
                avatar.body.transform.localScale = Vector3.Lerp(avatar.body.transform.localScale, Vector3.one * scale, 10 * Time.deltaTime);
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
        if (Input.GetKeyDown(KeyCode.Space) && !charging) {
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
                myId = msg.playerId;
                network.ResumeToken = msg.resumeToken;
                network.ContinueSequenceAfter(msg.lastSeq);
                status = "";
                break;
            case "ROOM_STATE":
                if (room == null) status = "";
                room = JsonUtility.FromJson<RoomEnvelope>(line).room;
                if (room != null) phase = room.phase;
                break;
            case "LEFT_ROOM":
                room = null; phase = "MENU"; winnerId = "";
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
                break;
            case "STATE_UPDATE":
                phase = msg.phase;
                ApplySnapshot(msg);
                break;
            case "PLAYER_EXPLODE":
                if (msg.playerId == myId) charging = false;
                if (msg.visible) StartCoroutine(Explosion(new Vector3(msg.x, .45f, msg.z), msg.radius));
                break;
            case "GAME_OVER":
                phase = "FINISHED";
                winnerId = msg.winnerId;
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
                int shards = 0;
                if (msg.scores != null) foreach (ScoreData s in msg.scores) if (s.playerId == winnerId) shards = s.roundWins;
                if (string.IsNullOrEmpty(winnerId)) status = "Hành trình kết thúc: đồng hạng.";
                else if (shards >= 3)
                    status = winnerId == myId ? "Tim Lửa Trọn Vẹn! Bạn mang về đủ cả 3 mảnh."
                        : $"Tim Lửa Trọn Vẹn! {NameOf(winnerId)} mang về đủ cả 3 mảnh.";
                else
                    status = winnerId == myId ? $"Bạn là Người Giữ Lửa với {shards} mảnh!"
                        : $"{NameOf(winnerId)} là Người Giữ Lửa với {shards} mảnh.";
                break;
            case "PONG":
                rttMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - msg.clientTime;
                break;
            case "ACK":
                serverMicros = msg.processingMicros;
                break;
            case "ERROR":
                status = ErrorText(msg.code, msg.mode);
                break;
            case "NETWORK_ERROR":
                status = "Mất kết nối. Đang thử kết nối lại...";
                break;
        }
    }

    void ApplySnapshot(ServerMessage snapshot) {
        ApplyMap(snapshot);
        if (snapshot.players == null) return;
        playerCount = snapshot.players.Length;
        aliveCount = 0;
        foreach (PlayerData p in snapshot.players) {
            if (p.alive) aliveCount++;
            playerNames[p.id] = p.name;
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
        else if (before.state == "CARRIED" && now.state == "GROUND") Toast("Người cầm mảnh bị hạ — mảnh rơi xuống đất!");
        else if (before.state != "PLACED" && now.state == "PLACED") Toast($"{NameOf(now.ownerId)} đã đặt mảnh lên bệ đá!");
        else if (now.state == "PLACED" && before.ownerId != now.ownerId)
            Toast(string.IsNullOrEmpty(now.ownerId) ? "Chủ bệ bị hạ — bệ đá đang vô chủ!" : $"{NameOf(now.ownerId)} đã chiếm bệ đá!");
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
        // While the overlay is open the screen underneath is drawn but not interactive.
        GUI.enabled = !overlay.IsOpen;
        DrawScreen();
        GUI.enabled = true;
        switch (overlay.Draw(room != null, phase == "PLAYING")) {
            case BombOverlay.Action.LeaveRoom:
                network.Send(new BombMessage { type = "LEAVE_ROOM" });
                break;
            case BombOverlay.Action.QuitGame:
                QuitGame();
                break;
        }
    }

    static void QuitGame() {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
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
                    network.Send(new BombMessage { type = "CREATE_ROOM", mode = modeMenu.SelectedMode });
                    break;
                case BombModeMenu.Choice.Join:
                    status = "Đang tìm phòng " + joinCode + "...";
                    network.Send(new BombMessage { type = "JOIN_ROOM", code = joinCode, mode = modeMenu.SelectedMode });
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
        if (string.IsNullOrEmpty(myId)) {
            GUILayout.Label("Tên", smallStyle); playerName = GUILayout.TextField(playerName,20,fieldStyle);
            GUILayout.Label("Server IP", smallStyle); host = GUILayout.TextField(host,fieldStyle);
            GUILayout.Label("Port", smallStyle); port = GUILayout.TextField(port,5,fieldStyle);
            GUILayout.Space(6);
            if (PanelButton("Kết nối", BombUi.Charge) && int.TryParse(port,out int parsed))
                network.Connect(host,parsed,playerName);
        } else {
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
    }

    void DrawMatchHud() {
        avatars.TryGetValue(myId, out Avatar me);
        PlayerData state = me?.state;
        hud.Draw(new BombHud.View {
            me = state, myColor = me != null ? me.color : BombUi.TextMuted,
            alive = aliveCount, total = playerCount,
            journey = room != null && room.mode == "JOURNEY", chapter = room != null ? room.chapterIndex : 0,
            mapName = activeMapName, matchTime = Time.unscaledTime - matchStartedAt,
            inBush = state != null && BushIndex(state.x, state.z) >= 0,
            rttMs = rttMs, serverMicros = serverMicros,
            sentPerSecond = sentPerSecond, receivedPerSecond = receivedPerSecond,
            sentBytesPerSecond = sentBytesPerSecond, receivedBytesPerSecond = receivedBytesPerSecond,
            showNetwork = BombSettings.ShowNetwork,
            mapId = activeMapId, arenaSize = activeArenaSize,
            obstacles = activeObstacles, bushes = activeBushes, dots = MinimapDots(),
            objective = objective,
            carrierName = objective != null ? NameOf(objective.carrierId) : "",
            ownerName = objective != null ? NameOf(objective.ownerId) : "",
            challengerName = objective != null ? NameOf(objective.challengerId) : "",
            ownerColor = objective != null ? ColorOf(objective.ownerId) : BombUi.Text,
            challengerColor = objective != null ? ColorOf(objective.challengerId) : BombUi.Charge,
            toast = toast, toastAlpha = Mathf.Clamp01(toastUntil - Time.unscaledTime)
        });
        foreach (KeyValuePair<string, Avatar> entry in avatars) {
            Avatar a = entry.Value;
            if (a.body == null || a.state == null || !a.state.alive || !a.body.activeInHierarchy) continue;
            Vector3 pos = gameCamera.WorldToScreenPoint(a.body.transform.position + Vector3.up * (a.state.size * .5f + .3f));
            if (pos.z > 0 && pos.x >= 0 && pos.x <= Screen.width && pos.y >= 0 && pos.y <= Screen.height)
                hud.DrawNameTag(new Vector2(pos.x, Screen.height - pos.y), a.state.name, entry.Key == myId);
        }
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
