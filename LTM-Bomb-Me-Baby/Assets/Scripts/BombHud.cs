using UnityEngine;
using static BombUi;

/// In-match overlay laid out after docs/concepts/bomb-me-baby-hud-concept.png:
/// player card top-left, match pill top-center, charge bottom-center, abilities bottom-right.
/// Everything is drawn at real screen pixels (sizes multiplied by u) so text stays sharp.
sealed class BombHud {
    public struct View {
        public PlayerData me;
        public string myId;
        public Color myColor;
        public int alive, total;
        public bool journey, inBush;
        public int chapter;
        public string mapName;
        public float matchTime;
        public float rttMs, serverMicros;
        public int sentPerSecond, receivedPerSecond;
        public long sentBytesPerSecond, receivedBytesPerSecond;
        public bool showNetwork;
        public string endpoint, syncMode, demoMap;
        public float networkSilence;
        public string[] disconnectedPlayers;

        // Minimap (both modes) and Journey objective.
        public string mapId;
        public float arenaSize;
        public ObstacleData[] obstacles;
        public BushData[] bushes;
        public MapDot[] dots;
        public ObjectiveData objective;
        public string carrierName, ownerName, challengerName;
        public Color ownerColor, challengerColor;
        public string toast;
        public float toastAlpha;
    }

    /// One player on the minimap. Only players this client can see are passed in.
    public struct MapDot {
        public float x, z;
        public Color color;
        public bool isMe, isCarrier;
    }

    // Mirrors GameRules.SHIELD_COOLDOWN_MS / BOOST_COOLDOWN_MS on the server.
    const float ShieldCooldown = 9f, BoostCooldown = 5f;

    float u = 1, stylesScale;
    GUIStyle name, label, labelRight, big, small, centerText, key, tagName, banner, bannerSub, toast;

    float S(float v) => v * u;
    Rect R(float x, float y, float w, float h) => new Rect(S(x), S(y), S(w), S(h));
    int Px(int size) => Mathf.RoundToInt(size * u);

    public void Draw(View v) {
        u = Mathf.Clamp(Screen.height / 1080f, .75f, 1.6f);
        EnsureStyles();
        float W = Screen.width / u, H = Screen.height / u;

        DrawPlayerCard(v);
        DrawMatchPill(v, W);
        DrawNetwork(v, W);
        DrawDisconnectedPlayers(v, W);
        DrawMinimap(v, W);
        if (v.objective != null && v.objective.Active) DrawObjective(v, W);
        if (!string.IsNullOrEmpty(v.toast) && v.toastAlpha > 0) DrawToast(v.toast, v.toastAlpha, W);
        if (v.me != null && v.me.alive) {
            bool carryingShard = v.objective != null && v.objective.state == "CARRIED"
                && v.objective.carrierId == v.myId;
            DrawCharge(v.me, W, H, carryingShard);
            DrawAbility(new Vector2(W - 178, H - 112), "KHIÊN", "E", Shield, v.me.shield, v.me.shieldCooldown, ShieldCooldown);
            DrawAbility(new Vector2(W - 74, H - 112), "TĂNG TỐC", "SHIFT", Boost, v.me.boost, v.me.boostCooldown, BoostCooldown);
        }
        if (v.matchTime < 8) DrawControlsHint(H, Mathf.Clamp01((8 - v.matchTime) / 1.5f), v.journey);
    }

    void DrawPlayerCard(View v) {
        Rect card = R(24, 20, 370, 96);
        Fill(card, Panel, RoundBig);
        Vector2 c = new Vector2(S(72), S(68));
        Fill(Centered(c, S(76)), v.myColor, Circle);
        Fill(Centered(c, S(64)), BombBody, Circle);
        Fill(Centered(c + new Vector2(S(-12), S(-12)), S(14)), new Color(1, 1, 1, .14f), Circle);
        Fill(new Rect(c.x - S(12), c.y - S(8), S(8), S(16)), Text, RoundSmall);
        Fill(new Rect(c.x + S(4), c.y - S(8), S(8), S(16)), Text, RoundSmall);

        PlayerData me = v.me;
        GUI.Label(R(128, 30, 250, 24), me == null ? "" : me.name, name);
        if (me == null || !me.alive) {
            Fill(R(128, 62, 160, 26), WithAlpha(Danger, .25f), RoundSmall);
            GUI.Label(R(128, 62, 160, 26), "ĐÃ BỊ LOẠI • ĐANG XEM", centerText);
            return;
        }
        GUI.Label(R(128, 58, 120, 18), "HP", label);
        GUI.Label(R(128, 58, 250, 18), $"{me.hp:0} / 100", labelRight);
        Bar(R(128, 78, 250, 12), me.hp / 100f, me.hp < 30 ? Danger : Health);
        string tag = v.inBush ? "ẨN TRONG BỤI" : $"KÍCH THƯỚC ×{me.size:0.00}";
        Color tagColor = v.inBush ? Health : TextMuted;
        float tagWidth = small.CalcSize(new GUIContent(tag)).x / u + 18;
        Fill(R(24, 124, tagWidth, 24), Panel, RoundSmall);
        small.normal.textColor = tagColor;
        GUI.Label(R(24, 124, tagWidth, 24), tag, small);
        small.normal.textColor = TextMuted;
    }

    void DrawMatchPill(View v, float W) {
        string mode = v.journey ? $"CHƯƠNG {v.chapter + 1}/3  •  {v.mapName}" : $"ĐẤU NHANH  •  {v.mapName}";
        if (v.objective != null && v.objective.Active) {
            int seconds = Mathf.CeilToInt(v.objective.timeLeft);
            mode += $"  •  {seconds / 60}:{seconds % 60:00}";
        }
        float modeWidth = label.CalcSize(new GUIContent(mode)).x / u;
        float width = 150 + modeWidth;
        Rect pill = R(W / 2 - width / 2, 20, width, 54);
        Fill(pill, Panel, RoundBig);
        GUI.Label(R(W / 2 - width / 2 + 20, 20, 44, 54), v.alive.ToString(), big);
        GUI.Label(R(W / 2 - width / 2 + 58, 20, 80, 54), "CÒN LẠI", label);
        Fill(R(W / 2 - width / 2 + 136, 34, 2, 26), new Color(1, 1, 1, .15f), null);
        label.normal.textColor = v.journey ? Core : Charge;
        GUI.Label(R(W / 2 - width / 2 + 150, 20, modeWidth + 10, 54), mode, label);
        label.normal.textColor = TextMuted;
    }

    void DrawNetwork(View v, float W) {
        bool delayed = v.networkSilence >= 1.5f;
        Color dot = delayed ? Danger : v.rttMs < 60 ? Health : v.rttMs < 150 ? Charge : Danger;
        if (!v.showNetwork) {
            Rect chip = R(W - 24 - 180, 20, 180, 34);
            Fill(chip, Panel, RoundSmall);
            Fill(Centered(new Vector2(chip.x + S(18), chip.center.y), S(9)), dot, Circle);
            GUI.Label(R(W - 24 - 180 + 32, 20, 140, 34), delayed ? "MẠNG ĐANG CHẬM" : $"RTT {v.rttMs:0} ms", label);
            GUI.Label(R(W - 24 - 180, 20, 166, 34), "F3", labelRight);
            return;
        }
        Rect panel = R(W - 24 - 300, 20, 300, 176);
        Fill(panel, Panel, RoundBig);
        string mapConfig = string.IsNullOrEmpty(v.demoMap) ? "ngẫu nhiên" : v.demoMap;
        string[] lines = {
            $"RTT {v.rttMs:0} ms   •   Server {v.serverMicros:0} µs",
            $"Đích: {Short(v.endpoint, 31)}",
            $"Sync: {v.syncMode}   •   Map demo: {mapConfig}",
            $"Message gửi/nhận: {v.sentPerSecond} / {v.receivedPerSecond} /s",
            $"Byte gửi/nhận: {v.sentBytesPerSecond} / {v.receivedBytesPerSecond} B/s",
            "F3 để thu gọn"
        };
        for (int i = 0; i < lines.Length; i++)
            GUI.Label(R(W - 24 - 300 + 18, 32 + i * 24, 264, 22), lines[i], i == lines.Length - 1 ? small : label);
    }

    void DrawDisconnectedPlayers(View v, float W) {
        if (v.disconnectedPlayers == null || v.disconnectedPlayers.Length == 0) return;
        float y = v.showNetwork ? 208 : 64;
        float height = 34 + v.disconnectedPlayers.Length * 24;
        Rect panel = R(W - 24 - 270, y, 270, height);
        Fill(panel, Panel, RoundBig);
        small.normal.textColor = Danger;
        GUI.Label(R(W - 24 - 252, y + 7, 234, 22), "MẤT KẾT NỐI — ĐANG GIỮ CHỖ", small);
        small.normal.textColor = TextMuted;
        for (int i = 0; i < v.disconnectedPlayers.Length; i++)
            GUI.Label(R(W - 24 - 252, y + 31 + i * 24, 234, 22), v.disconnectedPlayers[i], label);
    }

    static string Short(string value, int max) {
        if (string.IsNullOrEmpty(value) || value.Length <= max) return value ?? "";
        return "…" + value.Substring(value.Length - max + 1);
    }

    void DrawObjective(View v, float W) {
        ObjectiveData o = v.objective;
        string text;
        SealBreakerData mine = o.Breaker(v.myId);
        SealBreakerData leader = o.Breaker(o.challengerId);
        bool showMine = mine != null;
        bool showLeader = leader != null && (mine == null || leader.playerId != mine.playerId);
        switch (o.state) {
            case "WAITING": text = $"Mảnh Tim Lửa xuất hiện sau {Mathf.CeilToInt(o.spawnIn)}s"; break;
            case "GROUND":
                text = o.pickupPriority > 0
                    ? $"{v.challengerName} có quyền ưu tiên nhặt • {o.pickupPriority:0.0}s"
                    : o.pickupUnlock > 0 ? $"Mảnh đang bật rơi • nhặt được sau {o.pickupUnlock:0.0}s"
                    : "Mảnh đang nằm trên đất — xem minimap";
                if (o.pedestalOverheat > 0) text += $"  •  BỆ QUÁ NHIỆT {o.pedestalOverheat:0.0}s";
                break;
            case "CARRIED":
                text = o.carrierId == v.myId
                    ? $"Bạn đang cầm mảnh — không thể tấn công • Q để thả • Khóa Lửa {o.fireLock}/{Mathf.Max(2, o.fireLockMax)}"
                    : $"{v.carrierName} đang cầm mảnh — Khóa Lửa {o.fireLock}/{Mathf.Max(2, o.fireLockMax)}";
                if (o.carrierProtection > 0) text += $"  •  BẢO HỘ {o.carrierProtection:0.0}s";
                if (o.pedestalOverheat > 0) text += $"  •  BỆ QUÁ NHIỆT {o.pedestalOverheat:0.0}s";
                break;
            default:
                text = o.pedestalProtection > 0
                    ? $"Bệ của {v.ownerName} đang được bảo hộ • {o.pedestalProtection:0.0}s"
                    : $"{v.ownerName} đang giữ bệ • đối thủ trong vòng tự phá ấn";
                break;
        }
        float width = Mathf.Min(W - 48, Mathf.Max(380, label.CalcSize(new GUIContent(text)).x / u + 40));
        int bars = (showMine ? 1 : 0) + (showLeader ? 1 : 0);
        float height = 36 + bars * 24;
        Rect chip = R(W / 2 - width / 2, 82, width, height);
        Fill(chip, Panel, RoundSmall);
        Color stateColor = o.pedestalOverheat > 0 ? Danger : o.pedestalProtection > 0 ? Shield : Core;
        Fill(new Rect(chip.x + S(14), chip.y + S(13), S(10), S(10)), stateColor, Circle);
        label.normal.textColor = stateColor;
        GUI.Label(R(W / 2 - width / 2 + 32, 82, width - 40, 36), text, label);
        label.normal.textColor = TextMuted;
        int row = 0;
        if (showMine) {
            DrawSealBar(R(W / 2 - width / 2 + 16, 82 + 36 + row++ * 24, width - 32, 20),
                mine, "BẠN PHÁ ẤN", v.myColor);
        }
        if (showLeader) {
            DrawSealBar(R(W / 2 - width / 2 + 16, 82 + 36 + row * 24, width - 32, 20),
                leader, $"DẪN ĐẦU • {v.challengerName}", v.challengerColor);
        }
    }

    void DrawSealBar(Rect row, SealBreakerData breaker, string caption, Color color) {
        Color feedback = breaker.resetFlash > 0 && Mathf.FloorToInt(Time.unscaledTime * 12) % 2 == 0 ? Danger : color;
        small.normal.textColor = feedback;
        GUI.Label(new Rect(row.x, row.y, row.width * .42f, row.height), caption, small);
        small.normal.textColor = TextMuted;
        Bar(new Rect(row.x + row.width * .43f, row.y + S(6), row.width * .57f, S(8)), breaker.progress, feedback);
        if (breaker.stagger > 0)
            GUI.Label(new Rect(row.x, row.y, row.width - S(6), row.height), "KHỰNG", labelRight);
    }

    void DrawToast(string text, float alpha, float W) {
        Color saved = GUI.color;
        GUI.color = new Color(1, 1, 1, alpha);
        float width = toast.CalcSize(new GUIContent(text)).x / u + 48;
        Rect r = R(W / 2 - width / 2, 150, width, 52);
        Fill(new Rect(r.x + S(5), r.y + S(6), r.width, r.height), new Color(0, 0, 0, .45f), RoundBig);
        Fill(r, PanelRaised, RoundBig);
        Fill(new Rect(r.x, r.y, S(6), r.height), Core, null);
        GUI.Label(r, text, toast);
        GUI.color = saved;
    }

    void DrawMinimap(View v, float W) {
        if (v.arenaSize <= 0) return;
        float size = 220;
        float top = v.showNetwork ? 160 : 66;
        Rect frame = R(W - 24 - size, top, size, size);
        Fill(new Rect(frame.x + S(5), frame.y + S(6), frame.width, frame.height), new Color(0, 0, 0, .4f), RoundBig);
        Fill(frame, Panel, RoundBig);
        Rect area = new Rect(frame.x + S(12), frame.y + S(12), frame.width - S(24), frame.height - S(24));
        Fill(area, WithAlpha(Color.Lerp(Night, MapAccent(v.mapId), .35f), .9f), RoundSmall);
        float half = v.arenaSize;
        float scale = area.width / (half * 2);
        Vector2 ToMap(float x, float z) => new Vector2(area.x + (x + half) * scale, area.y + (half - z) * scale);

        if (v.bushes != null) foreach (BushData b in v.bushes)
            Fill(Centered(ToMap(b.x, b.z), Mathf.Max(S(5), b.radius * 2 * scale)), new Color(.16f, .4f, .22f, .9f), Circle);
        if (v.obstacles != null) foreach (ObstacleData o in v.obstacles)
            Fill(Centered(ToMap(o.x, o.z), Mathf.Max(S(4), o.radius * 2 * scale)), new Color(.62f, .58f, .68f), Circle);

        ObjectiveData obj = v.objective;
        bool hasObjective = obj != null && obj.Active;
        if (hasObjective) {
            Vector2 p = ToMap(obj.pedestalX, obj.pedestalZ);
            float ring = Mathf.Max(S(14), obj.pedestalRadius * 2 * scale);
            bool owned = !string.IsNullOrEmpty(obj.ownerId);
            Fill(Centered(p, ring), WithAlpha(owned ? v.ownerColor : Text, owned ? .45f : .2f), Circle);
            Fill(Centered(p, ring), owned ? v.ownerColor : WithAlpha(Text, .6f), Ring);
        }

        if (v.dots != null) foreach (MapDot d in v.dots) {
            Vector2 p = ToMap(d.x, d.z);
            if (d.isCarrier) Fill(Centered(p, S(18) + Mathf.PingPong(Time.unscaledTime * 16, S(6))), Core, Ring);
            float dot = d.isMe ? S(12) : S(9);
            Fill(Centered(p, dot + S(4)), d.isMe ? Text : Night, Circle);
            Fill(Centered(p, dot), d.color, Circle);
        }

        if (hasObjective && (obj.state == "GROUND" || obj.state == "PLACED")) {
            Vector2 p = ToMap(obj.x, obj.z);
            float pulse = S(12) + Mathf.PingPong(Time.unscaledTime * 10, S(4));
            Matrix4x4 saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(45, p);
            Fill(Centered(p, pulse + S(4)), Night, RoundSmall);
            Fill(Centered(p, pulse), Core, RoundSmall);
            GUI.matrix = saved;
        }
    }

    void DrawCharge(PlayerData me, float W, float H, bool carryingShard) {
        float width = Mathf.Min(560, W - 460);
        float x = W / 2 - width / 2, y = H - 92;
        Fill(R(x - 16, y - 34, width + 32, 78), Panel, RoundBig);
        bool charging = me.charging && !carryingShard;
        label.normal.textColor = charging ? Charge : TextMuted;
        GUI.Label(R(x, y - 30, width, 24), carryingShard ? "ĐANG GIỮ MẢNH • Q ĐỂ THẢ"
            : charging ? "NẠP NỔ" : "Giữ SPACE để nạp, thả để nổ", label);
        label.normal.textColor = TextMuted;
        if (charging) GUI.Label(R(x, y - 30, width, 24), $"{me.charge * 100:0}%", labelRight);
        Color fill = carryingShard ? Core
            : me.charge >= .99f ? Color.Lerp(Charge, new Color(1, .95f, .6f), Mathf.PingPong(Time.unscaledTime * 4, 1)) : Charge;
        Bar(R(x, y, width, 22), carryingShard ? 1 : charging ? me.charge : 0, fill);
    }

    void DrawAbility(Vector2 center, string title, string keyName, Color color, bool active, float cooldown, float total) {
        Vector2 c = new Vector2(S(center.x), S(center.y));
        float d = S(84);
        Fill(Centered(c, d + S(10)), Panel, Circle);
        float ready = Mathf.Clamp01(1 - cooldown / total);
        Fill(Centered(c, d), WithAlpha(color, .18f), Circle);
        // Liquid fill: clip the colored disc to the recovered fraction, rising from the bottom.
        Rect disc = Centered(c, d);
        float visible = d * ready;
        GUI.BeginClip(new Rect(disc.x, disc.yMax - visible, d, visible));
        Fill(new Rect(0, visible - d, d, d), WithAlpha(color, cooldown > 0 ? .55f : 1f), Circle);
        GUI.EndClip();
        if (active) Fill(Centered(c, d + S(14) + Mathf.PingPong(Time.unscaledTime * 30, S(8))), color, Ring);
        if (cooldown > 0) GUI.Label(Centered(c, d), $"{cooldown:0.0}", big);
        else {
            key.normal.textColor = Night;
            GUI.Label(Centered(c, d), keyName, key);
            key.normal.textColor = WithAlpha(Text, .6f);
        }
        GUI.Label(new Rect(c.x - S(60), c.y + d / 2 + S(6), S(120), S(20)), title, centerText);
    }

    void DrawControlsHint(float H, float alpha, bool journey) {
        string text = "WASD di chuyển   •   Giữ SPACE nạp, thả để nổ   •   E khiên   •   SHIFT tăng tốc"
            + (journey ? "   •   Q thả mảnh" : "");
        float width = small.CalcSize(new GUIContent(text)).x / u + 32;
        Color saved = GUI.color;
        GUI.color = new Color(1, 1, 1, alpha);
        Fill(R(24, H - 60, width, 36), Panel, RoundSmall);
        GUI.Label(R(40, H - 60, width, 36), text, small);
        GUI.color = saved;
    }

    /// Small name pill above a ball; visible opponents also get an overhead HP bar.
    public void DrawNameTag(Vector2 screenTop, string playerName, float hp, bool isMe,
                            bool connected, float reconnectRemaining,
                            bool isCarrier, int fireLock, int fireLockMax) {
        EnsureStyles();
        string shownName = connected ? playerName : $"{playerName}  •  MẤT KẾT NỐI {Mathf.CeilToInt(reconnectRemaining)}s";
        float width = tagName.CalcSize(new GUIContent(shownName)).x + S(18);
        float nameOffset = isMe ? S(28) : S(40);
        Rect pill = new Rect(screenTop.x - width / 2, screenTop.y - nameOffset, width, S(22));
        Fill(pill, WithAlpha(Panel, .8f), RoundSmall);
        tagName.normal.textColor = !connected ? Danger : isMe ? Charge : Text;
        GUI.Label(pill, shownName, tagName);

        if (isCarrier) {
            int maxLocks = Mathf.Max(2, fireLockMax);
            float pip = S(8), gap = S(4);
            float total = maxLocks * pip + (maxLocks - 1) * gap;
            for (int i = 0; i < maxLocks; i++)
                Fill(new Rect(screenTop.x - total / 2 + i * (pip + gap), pill.y - S(12), pip, S(7)),
                    i < fireLock ? Core : WithAlpha(Danger, .35f), RoundSmall);
        }

        if (isMe) return;

        float health = Mathf.Clamp01(hp / 100f);
        float barWidth = Mathf.Clamp(width, S(68), S(104));
        Rect track = new Rect(screenTop.x - barWidth / 2, pill.yMax + S(4), barWidth, S(8));
        Color healthColor = health <= .3f ? Danger : health <= .6f ? Charge : Health;
        Fill(new Rect(track.x - S(2), track.y - S(2), track.width + S(4), track.height + S(4)),
            WithAlpha(Night, .92f), RoundSmall);
        Bar(track, health, healthColor);
    }

    /// Centered result card shown while the round is FINISHED.
    public bool DrawReconnect(float secondsRemaining, string endpoint, int attempt) {
        u = Mathf.Clamp(Screen.height / 1080f, .75f, 1.6f);
        EnsureStyles();
        bool expired = secondsRemaining <= 0;
        float width = S(560), height = S(expired ? 154 : 104);
        Rect card = new Rect(Screen.width / 2f - width / 2, S(154), width, height);
        Fill(new Rect(card.x + S(6), card.y + S(8), width, height), new Color(0, 0, 0, .48f), RoundBig);
        Fill(card, Panel, RoundBig);
        Color accent = expired ? Danger : Charge;
        Fill(new Rect(card.x, card.y, width, S(6)), accent, null);
        banner.normal.textColor = accent;
        GUI.Label(new Rect(card.x, card.y + S(10), width, S(48)),
            expired ? "HẾT THỜI GIAN GIỮ PHIÊN" : "ĐANG KẾT NỐI LẠI", banner);
        string detail = expired
            ? "Server không phản hồi trong 10 giây. Bạn có thể quay lại menu."
            : $"{Short(endpoint, 34)}  •  còn {secondsRemaining:0.0}s  •  lần thử {Mathf.Max(1, attempt)}";
        GUI.Label(new Rect(card.x + S(20), card.y + S(60), width - S(40), S(34)), detail, bannerSub);
        if (expired) {
            Rect button = new Rect(card.center.x - S(90), card.y + S(105), S(180), S(34));
            return GUI.Button(button, "VỀ MENU");
        }
        return false;
    }

    public void DrawBanner(string title, string subtitle, Color accent) {
        EnsureStyles();
        float width = S(520), height = S(120);
        Rect card = new Rect(Screen.width / 2f - width / 2, S(110), width, height);
        Fill(new Rect(card.x + S(6), card.y + S(8), width, height), new Color(0, 0, 0, .45f), RoundBig);
        Fill(card, Panel, RoundBig);
        Fill(new Rect(card.x, card.y, width, S(6)), accent, null);
        banner.normal.textColor = accent;
        GUI.Label(new Rect(card.x, card.y + S(16), width, S(52)), title, banner);
        GUI.Label(new Rect(card.x + S(20), card.y + S(68), width - S(40), S(40)), subtitle, bannerSub);
    }

    void EnsureStyles() {
        EnsureShapes();
        if (name != null && Mathf.Approximately(stylesScale, u)) return;
        stylesScale = u;
        name = TextStyle(Px(19), FontStyle.Bold, Text, TextAnchor.MiddleLeft);
        label = TextStyle(Px(15), FontStyle.Bold, TextMuted, TextAnchor.MiddleLeft);
        labelRight = TextStyle(Px(15), FontStyle.Bold, Text, TextAnchor.MiddleRight);
        big = TextStyle(Px(26), FontStyle.Bold, Text, TextAnchor.MiddleCenter);
        small = TextStyle(Px(13), FontStyle.Bold, TextMuted, TextAnchor.MiddleLeft);
        small.padding = new RectOffset(Px(9), Px(9), 0, 0);
        centerText = TextStyle(Px(13), FontStyle.Bold, Text, TextAnchor.MiddleCenter);
        key = TextStyle(Px(15), FontStyle.Bold, WithAlpha(Text, .6f), TextAnchor.MiddleCenter);
        tagName = TextStyle(Px(13), FontStyle.Bold, Text, TextAnchor.MiddleCenter);
        banner = TextStyle(Px(38), FontStyle.Bold, Charge, TextAnchor.MiddleCenter);
        bannerSub = TextStyle(Px(16), FontStyle.Normal, TextMuted, TextAnchor.MiddleCenter);
        bannerSub.wordWrap = true;
        toast = TextStyle(Px(20), FontStyle.Bold, Text, TextAnchor.MiddleCenter);
    }
}
