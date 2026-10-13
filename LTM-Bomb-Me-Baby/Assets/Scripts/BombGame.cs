using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class BombGame : MonoBehaviour {
    static readonly Vector3 CameraOffset = new Vector3(0, 22, -22);

    sealed class Avatar {
        public GameObject body;
        public GameObject shield;
        public GameObject chargeAura;
        public TrailRenderer boostTrail;
        public Vector3 target;
        public PlayerData state;
    }

    readonly BombNetwork network = new BombNetwork();
    readonly Dictionary<string, Avatar> avatars = new Dictionary<string, Avatar>();
    readonly Dictionary<int, GameObject> orbViews = new Dictionary<int, GameObject>();
    readonly Dictionary<int, GameObject> coreViews = new Dictionary<int, GameObject>();
    readonly Color[] playerColors = { new Color(1,.34f,.26f), new Color(.24f,.74f,1), new Color(1,.75f,.2f), new Color(.53f,.96f,.43f) };
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
    GameObject[] walls;
    GUIStyle titleStyle, bodyStyle, smallStyle;

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
    }

    void Start() {
        Application.targetFrameRate = 60;
        Application.runInBackground = true;
        if (gameCamera == null) BuildArena();
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
        if (phase == "PLAYING" && network.Connected && !string.IsNullOrEmpty(myId)) HandleInput();
        foreach (Avatar avatar in avatars.Values) {
            if (avatar.body == null) continue;
            avatar.body.transform.position = Vector3.Lerp(avatar.body.transform.position, avatar.target, 22 * Time.deltaTime);
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
        if (Input.GetKeyUp(KeyCode.Space) && charging) {
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
                status = "Đã kết nối. Tạo phòng hoặc nhập mã phòng.";
                break;
            case "ROOM_STATE":
                room = JsonUtility.FromJson<RoomEnvelope>(line).room;
                if (room != null) phase = room.phase;
                break;
            case "LEFT_ROOM":
                room = null; phase = "MENU"; winnerId = "";
                ClearActors();
                break;
            case "GAME_START":
                phase = "PLAYING"; winnerId = ""; charging = false;
                status = "Trận đấu bắt đầu!";
                break;
            case "STATE_UPDATE":
                phase = msg.phase;
                ApplySnapshot(msg);
                break;
            case "PLAYER_EXPLODE":
                if (msg.playerId == myId) charging = false;
                StartCoroutine(Explosion(new Vector3(msg.x, .45f, msg.z), msg.radius));
                break;
            case "GAME_OVER":
                phase = "FINISHED";
                winnerId = msg.winnerId;
                status = winnerId == myId ? "Bạn chiến thắng!" : "Trận đấu kết thúc.";
                break;
            case "PONG":
                rttMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - msg.clientTime;
                break;
            case "ACK":
                serverMicros = msg.processingMicros;
                break;
            case "ERROR":
                status = "Server từ chối: " + msg.code;
                break;
            case "NETWORK_ERROR":
                status = "Mất kết nối. Đang thử kết nối lại...";
                break;
        }
    }

    void ApplySnapshot(ServerMessage snapshot) {
        if (snapshot.players == null) return;
        HashSet<string> seen = new HashSet<string>();
        for (int i = 0; i < snapshot.players.Length; i++) {
            PlayerData p = snapshot.players[i];
            seen.Add(p.id);
            bool shouldRender = p.id == myId || p.visible;
            if (!shouldRender && !avatars.ContainsKey(p.id)) continue;
            if (!avatars.TryGetValue(p.id, out Avatar a)) {
                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                body.name = "Bomb " + p.name;
                if (actorsRoot != null) body.transform.SetParent(actorsRoot, false);
                body.GetComponent<Renderer>().material = MaterialFor(playerColors[i % playerColors.Length]);
                GameObject fuse = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                fuse.transform.SetParent(body.transform, false);
                fuse.transform.localPosition = new Vector3(0,.53f,0);
                fuse.transform.localScale = new Vector3(.09f,.2f,.09f);
                fuse.GetComponent<Renderer>().material = MaterialFor(new Color(.12f,.12f,.17f));
                GameObject shield = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                shield.name = "Shield";
                shield.transform.SetParent(body.transform, false);
                shield.GetComponent<Renderer>().material = TransparentMaterial(new Color(.3f,.87f,1,.25f));
                Destroy(shield.GetComponent<Collider>());
                GameObject chargeAura = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                chargeAura.name = "Charge Aura";
                chargeAura.transform.SetParent(body.transform, false);
                chargeAura.GetComponent<Renderer>().material = TransparentMaterial(new Color(1,.34f,.08f,.2f));
                Destroy(chargeAura.GetComponent<Collider>());
                TrailRenderer boostTrail = body.AddComponent<TrailRenderer>();
                boostTrail.time = .32f;
                boostTrail.startWidth = .6f;
                boostTrail.endWidth = .05f;
                boostTrail.minVertexDistance = .12f;
                boostTrail.material = TransparentMaterial(new Color(.2f,.85f,1,.65f));
                boostTrail.emitting = false;
                a = new Avatar { body = body, shield = shield, chargeAura = chargeAura, boostTrail = boostTrail };
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
        SyncPickups(snapshot.orbs, orbViews, .19f, new Color(.35f,1,.54f));
        SyncPickups(snapshot.cores, coreViews, .43f, new Color(1,.38f,.86f));
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
        effect.GetComponent<Renderer>().material = TransparentMaterial(new Color(1,.53f,.12f,.38f));
        effect.transform.position = position;
        float time = 0;
        while (time < .35f) {
            time += Time.deltaTime;
            effect.transform.localScale = Vector3.one * Mathf.Lerp(.1f,radius * 2,time/.35f);
            yield return null;
        }
        Destroy(effect);
    }

    void BuildArena() {
        var cameraObject = new GameObject("Arena camera");
        gameCamera = cameraObject.AddComponent<Camera>();
        gameCamera.transform.position = CameraOffset;
        gameCamera.transform.LookAt(Vector3.zero);
        gameCamera.orthographic = true;
        gameCamera.orthographicSize = 12.5f;
        gameCamera.backgroundColor = new Color(.08f,.11f,.17f);
        cameraObject.AddComponent<AudioListener>();
        RenderSettings.ambientLight = new Color(.7f,.75f,.85f);
        var lightObject = new GameObject("Sun");
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        lightObject.transform.rotation = Quaternion.Euler(45,-35,0);
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Arena floor";
        ground.transform.position = new Vector3(0,-.2f,0);
        ground.transform.localScale = new Vector3(41,.3f,41);
        ground.GetComponent<Renderer>().material = MaterialFor(new Color(.15f,.22f,.30f));
        walls = new GameObject[4];
        for (int i = 0; i < 4; i++) {
            walls[i] = GameObject.CreatePrimitive(PrimitiveType.Cube);
            walls[i].name = "Arena wall";
            walls[i].transform.position = i < 2 ? new Vector3(i == 0 ? -20.7f : 20.7f,.6f,0) : new Vector3(0,.6f,i == 2 ? -20.7f : 20.7f);
            walls[i].transform.localScale = i < 2 ? new Vector3(.4f,1.2f,41.8f) : new Vector3(41.8f,1.2f,.4f);
            walls[i].GetComponent<Renderer>().material = MaterialFor(new Color(.29f,.4f,.52f));
        }
        Vector3[] obstaclePositions = { new Vector3(-5,0,-3), new Vector3(5,0,4), new Vector3(-6,0,7), new Vector3(7,0,-6) };
        float[] obstacleRadii = { 1.3f, 1.4f, 1.1f, 1.2f };
        for (int i = 0; i < obstaclePositions.Length; i++) {
            var rock = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rock.name = "Obstacle";
            rock.transform.position = obstaclePositions[i] + Vector3.up * .65f;
            float radius = obstacleRadii[i];
            rock.transform.localScale = new Vector3(radius * 2, .65f, radius * 2);
            rock.GetComponent<Renderer>().material = MaterialFor(new Color(.43f,.46f,.55f));
        }
        Vector3[] bushPositions = {
            new Vector3(-11,0,-6), new Vector3(-10,0,10), new Vector3(11,0,9),
            new Vector3(12,0,-10), new Vector3(0,0,10), new Vector3(0,0,-11)
        };
        float[] bushRadii = { 1.4f, 1.6f, 1.5f, 1.7f, 1.3f, 1.3f };
        for (int i = 0; i < bushPositions.Length; i++) CreateRuntimeBush(bushPositions[i], bushRadii[i]);
    }

    void CreateRuntimeBush(Vector3 position, float radius) {
        GameObject root = new GameObject("Bush");
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

    static Material MaterialFor(Color color) {
        var material = new Material(Shader.Find("Standard"));
        material.color = color;
        return material;
    }

    static Material TransparentMaterial(Color color) {
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
    }

    void OnGUI() {
        if (titleStyle == null) {
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 25, fontStyle = FontStyle.Bold };
            titleStyle.normal.textColor = Color.white;
            bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            bodyStyle.normal.textColor = Color.white;
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            smallStyle.normal.textColor = Color.white;
        }
        GUILayout.BeginArea(new Rect(16,14,340,Screen.height-28), GUI.skin.box);
        GUILayout.Label("BOMB ME BABY", titleStyle);
        GUILayout.Label(status, bodyStyle);
        GUILayout.Space(10);
        if (string.IsNullOrEmpty(myId)) {
            GUILayout.Label("Tên", bodyStyle); playerName = GUILayout.TextField(playerName,20);
            GUILayout.Label("Server IP", bodyStyle); host = GUILayout.TextField(host);
            GUILayout.Label("Port", bodyStyle); port = GUILayout.TextField(port,5);
            if (GUILayout.Button("Kết nối", GUILayout.Height(34)) && int.TryParse(port,out int parsed))
                network.Connect(host,parsed,playerName);
        } else if (room == null) {
            if (GUILayout.Button("Tạo phòng", GUILayout.Height(34))) network.Send(new BombMessage { type = "CREATE_ROOM" });
            GUILayout.Label("Mã phòng", bodyStyle); joinCode = GUILayout.TextField(joinCode,6);
            if (GUILayout.Button("Vào phòng", GUILayout.Height(34))) network.Send(new BombMessage { type = "JOIN_ROOM", code = joinCode });
        } else {
            GUILayout.Label("Phòng: " + room.code + "  •  " + phase, bodyStyle);
            if (room.players != null) foreach (LobbyPlayer p in room.players)
                GUILayout.Label((p.id == myId ? "▶ " : "   ") + p.name + (p.ready ? " ✓" : "") + (p.connected ? "" : " (mất kết nối)"), bodyStyle);
            if (phase != "PLAYING") {
                bool myReady = false;
                if (room.players != null) foreach (LobbyPlayer p in room.players) if (p.id == myId) myReady = p.ready;
                if (GUILayout.Button(myReady ? "Bỏ sẵn sàng" : "Sẵn sàng", GUILayout.Height(34)))
                    network.Send(new BombMessage { type = "READY", ready = !myReady });
                GUILayout.Label("Có thể bắt đầu một mình để test; tối đa 4 người.", smallStyle);
            }
            if (GUILayout.Button("Rời phòng")) network.Send(new BombMessage { type = "LEAVE_ROOM" });
        }
        if (phase == "PLAYING") {
            GUILayout.Space(14);
            GUILayout.Label("WASD / phím mũi tên: Di chuyển", bodyStyle);
            GUILayout.Label("Giữ / thả Space: Nạp / Nổ", bodyStyle);
            GUILayout.Label("E: Khiên 1.5 giây", bodyStyle);
            GUILayout.Label("Shift: Tăng tốc", bodyStyle);
            if (avatars.TryGetValue(myId,out Avatar me) && me.state != null) {
                GUILayout.Space(8);
                GUILayout.Label($"HP {me.state.hp:0}   Size {me.state.size:0.00}   Charge {me.state.charge*100:0}%",bodyStyle);
                GUILayout.Label($"Khiên {me.state.shieldCooldown:0.0}s   Boost {me.state.boostCooldown:0.0}s",bodyStyle);
                if (BushIndex(me.state.x, me.state.z) >= 0)
                    GUILayout.Label("ẨN THÂN: người ngoài bụi không nhìn thấy bạn", bodyStyle);
            }
        }
        if (phase == "FINISHED") GUILayout.Label(winnerId == myId ? "CHIẾN THẮNG!" : "Trận đấu kết thúc",titleStyle);
        GUILayout.FlexibleSpace();
        GUILayout.Label($"RTT {rttMs:0} ms  •  Server {serverMicros:0} µs",smallStyle);
        GUILayout.Label($"Gửi {network.SentMessages}  •  Nhận {network.ReceivedMessages}",smallStyle);
        GUILayout.Label($"{sentPerSecond}/{receivedPerSecond} msg/s  •  {sentBytesPerSecond}/{receivedBytesPerSecond} B/s",smallStyle);
        GUILayout.EndArea();
        foreach (Avatar a in avatars.Values) {
            if (a.body == null || a.state == null || !a.state.alive) continue;
            Vector3 pos = gameCamera.WorldToScreenPoint(a.body.transform.position + Vector3.up * (a.state.size + .4f));
            if (pos.z > 0) GUI.Label(new Rect(pos.x-60,Screen.height-pos.y-16,120,22),$"{a.state.name}  {a.state.hp:0} HP",smallStyle);
        }
    }

    static int BushIndex(float x, float z) {
        Vector3[] centers = {
            new Vector3(-11,0,-6), new Vector3(-10,0,10), new Vector3(11,0,9),
            new Vector3(12,0,-10), new Vector3(0,0,10), new Vector3(0,0,-11)
        };
        float[] radii = { 1.4f, 1.6f, 1.5f, 1.7f, 1.3f, 1.3f };
        for (int i = 0; i < centers.Length; i++) {
            float dx = x - centers[i].x;
            float dz = z - centers[i].z;
            if (dx * dx + dz * dz <= radii[i] * radii[i]) return i;
        }
        return -1;
    }
}
