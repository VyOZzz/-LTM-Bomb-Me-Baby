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

    // Mirrors GameRules.BOMB_COOLDOWN_MS / SHIELD_COOLDOWN_MS / BOOST_COOLDOWN_MS on the server.
    const float BombCooldown = 2.5f, ShieldCooldown = 9f, BoostCooldown = 5f;
    static readonly Color BushInk = new Color(.16f, .4f, .22f, .9f);
    static readonly Color ObstacleInk = new Color(.62f, .58f, .68f);

    float u = 1, stylesScale;
    GUIStyle name, label, labelRight, big, small, centerText, key, tagName, banner, bannerSub, toast, button, legend;

    // Feedback state. It advances only on Repaint so the several OnGUI passes of a frame agree.
    float lastHp = -1, ghostHp, damagedAt = -9;
    readonly float[] lastCooldown = new float[2];
    readonly float[] readyAt = { -9, -9 };
    string shownToast, shownBanner;
    float shownToastAlpha, toastAt, bannerAt;

    float S(float v) => v * u;
    Rect R(float x, float y, float w, float h) => new Rect(S(x), S(y), S(w), S(h));
    int Px(int size) => Mathf.RoundToInt(size * u);

    static float EaseOut(float t) => 1 - (1 - t) * (1 - t) * (1 - t);

    static string Clock(float seconds) {
        int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
        return $"{s / 60}:{s % 60:00}";
    }

    public void Draw(View v) {
        u = Mathf.Clamp(Screen.height / 1080f, .75f, 1.6f);
        EnsureStyles();
        float W = Screen.width / u, H = Screen.height / u;
        shownBanner = null;
        if (Event.current.type == EventType.Repaint) TrackVitals(v.me);
        DrawVitalsOverlay(v.me);

        DrawPlayerCard(v);
        DrawMatchPill(v, W);
        // The right column stacks network, disconnected players and minimap without overlapping.
        float rightTop = DrawNetwork(v, W);
        rightTop = DrawDisconnectedPlayers(v, W, rightTop);
        DrawMinimap(v, W, rightTop);
        float toastTop = 150;
        if (v.objective != null && v.objective.Active) toastTop = Mathf.Max(toastTop, DrawObjective(v, W) + 12);
        if (!string.IsNullOrEmpty(v.toast) && v.toastAlpha > 0) DrawToast(v.toast, v.toastAlpha, W, toastTop);
        if (v.me != null && v.me.alive) {
            bool carryingShard = v.objective != null && v.objective.state == "CARRIED"
                && v.objective.carrierId == v.myId;
            DrawCharge(v.me, W, H, carryingShard);
            DrawAbility(0, new Vector2(W - 178, H - 112), "KHIÊN", "E", Shield, v.me.shield, v.me.shieldCooldown, ShieldCooldown);
            DrawAbility(1, new Vector2(W - 74, H - 112), "TĂNG TỐC", "SHIFT", Boost, v.me.boost, v.me.boostCooldown, BoostCooldown);
        } else if (v.me != null) DrawSpectating(W, H);
        if (BombSettings.ShowControls && v.matchTime < 8) DrawControlsHint(H, Mathf.Clamp01((8 - v.matchTime) / 1.5f), v.journey);
    }

    /// Detects HP drops for the hit flash and keeps a trailing "ghost" HP that drains after a hit.
    void TrackVitals(PlayerData me) {
        if (me == null || !me.alive) { lastHp = -1; return; }
        float now = Time.unscaledTime;
        if (lastHp < 0 || me.hp > lastHp) ghostHp = me.hp;
        else if (me.hp < lastHp - .5f) {
            damagedAt = now;
            ghostHp = Mathf.Max(ghostHp, lastHp);
        }
        if (now - damagedAt > .35f) ghostHp = Mathf.MoveTowards(ghostHp, me.hp, Time.unscaledDeltaTime * 60);
        lastHp = me.hp;
    }

    /// Red screen-edge vignette: a quick flash when hit and a slow pulse while HP is low.
    void DrawVitalsOverlay(PlayerData me) {
        if (me == null || !me.alive) return;
        float hit = Mathf.Clamp01(1 - (Time.unscaledTime - damagedAt) / .45f) * .5f;
        float low = me.hp <= 30
            ? Mathf.Lerp(.16f, .34f, Mathf.PingPong(Time.unscaledTime * 1.6f, 1)) * Mathf.Lerp(.6f, 1f, Mathf.InverseLerp(30, 5, me.hp))
            : 0;
        float alpha = Mathf.Max(hit, low);
        if (alpha > .01f) EdgeVignette(Danger, alpha);
    }

    static void EdgeVignette(Color color, float alpha) {
        const int steps = 16;
        float step = Mathf.Min(Screen.width, Screen.height) * .16f / steps;
        for (int i = 0; i < steps; i++) {
            float t = 1 - i / (float)steps;
            Color c = WithAlpha(color, alpha * t * t);
            float o = i * step;
            Fill(new Rect(0, o, Screen.width, step), c, null);
            Fill(new Rect(0, Screen.height - o - step, Screen.width, step), c, null);
            Fill(new Rect(o, 0, step, Screen.height), c, null);
            Fill(new Rect(Screen.width - o - step, 0, step, Screen.height), c, null);
        }
    }

    void DrawPlayerCard(View v) {
        Rect card = R(24, 20, 370, 96);
        Fill(card, Panel, RoundBig);
        float hit = Mathf.Clamp01(1 - (Time.unscaledTime - damagedAt) / .3f);
        if (hit > 0 && v.me != null && v.me.alive) Fill(card, WithAlpha(Danger, hit * .3f), RoundBig);
        Vector2 c = new Vector2(S(72), S(68));
        Fill(Centered(c, S(76)), v.myColor, Circle);
        Fill(Centered(c, S(64)), BombBody, Circle);
        Fill(Centered(c + new Vector2(S(-12), S(-12)), S(14)), new Color(1, 1, 1, .14f), Circle);
        Fill(new Rect(c.x - S(12), c.y - S(8), S(8), S(16)), Text, RoundSmall);
        Fill(new Rect(c.x + S(4), c.y - S(8), S(8), S(16)), Text, RoundSmall);

        PlayerData me = v.me;
        GUI.Label(R(128, 30, 250, 24), me == null ? "" : me.name, name);
        if (me == null) {
            Fill(R(128, 62, 190, 26), WithAlpha(TextMuted, .2f), RoundSmall);
            GUI.Label(R(128, 62, 190, 26), "ĐANG ĐỒNG BỘ DỮ LIỆU...", centerText);
            return;
        }
        if (!me.alive) {
            Fill(R(128, 62, 190, 26), WithAlpha(Danger, .25f), RoundSmall);
            GUI.Label(R(128, 62, 190, 26), "ĐÃ BỊ LOẠI • ĐANG XEM", centerText);
            return;
        }
        GUI.Label(R(128, 58, 120, 18), "HP", label);
        GUI.Label(R(128, 58, 250, 18), $"{Mathf.CeilToInt(me.hp)} / 100", labelRight);
        Rect track = R(128, 78, 250, 12);
        float hp01 = Mathf.Clamp01(me.hp / 100f), ghost01 = Mathf.Clamp01(Mathf.Max(ghostHp, me.hp) / 100f);
        Color hpColor = me.hp < 30 ? Danger : me.hp < 60 ? Charge : Health;
        Bar(track, hp01, hpColor, ghost01);

        float x = Chip(24, 124, v.inBush ? "ẨN TRONG BỤI" : $"KÍCH THƯỚC ×{me.size:0.00}", v.inBush ? Health : TextMuted);
        if (me.kills > 0) Chip(x + 8, 124, $"HẠ {me.kills}", Charge);
    }

    /// Small status pill under the player card; returns its right edge in virtual units.
    float Chip(float x, float y, string text, Color color) {
        float width = small.CalcSize(new GUIContent(text)).x / u + 18;
        Fill(R(x, y, width, 24), Panel, RoundSmall);
        small.normal.textColor = color;
        GUI.Label(R(x, y, width, 24), text, small);
        small.normal.textColor = TextMuted;
        return x + width;
    }

    void DrawMatchPill(View v, float W) {
        string mode = v.journey ? $"CHƯƠNG {v.chapter + 1}/3" : "ĐẤU NHANH";
        if (!string.IsNullOrEmpty(v.mapName)) mode += "  •  " + v.mapName;
        // Journey counts the objective down; otherwise the clock shows elapsed match time.
        bool countdown = v.objective != null && v.objective.Active;
        string clock = countdown ? Clock(v.objective.timeLeft) : Clock(Mathf.Floor(v.matchTime));
        bool urgent = countdown && v.objective.timeLeft <= 10;

        float modeWidth = label.CalcSize(new GUIContent(mode)).x / u;
        float clockWidth = labelRight.CalcSize(new GUIContent(clock)).x / u;
        float width = 150 + modeWidth + 24 + clockWidth + 22;
        float x0 = W / 2 - width / 2;
        Rect pill = R(x0, 20, width, 54);
        Fill(pill, Panel, RoundBig);
        GUI.Label(R(x0 + 20, 20, 44, 54), v.total > 0 ? v.alive.ToString() : "—", big);
        GUI.Label(R(x0 + 58, 20, 80, 54), "CÒN LẠI", label);
        Fill(R(x0 + 136, 34, 2, 26), new Color(1, 1, 1, .15f), null);
        label.normal.textColor = v.journey ? Core : Charge;
        GUI.Label(R(x0 + 150, 20, modeWidth + 10, 54), mode, label);
        label.normal.textColor = TextMuted;
        Fill(R(x0 + 150 + modeWidth + 11, 34, 2, 26), new Color(1, 1, 1, .15f), null);
        labelRight.normal.textColor = urgent && Mathf.FloorToInt(Time.unscaledTime * 4) % 2 == 0 ? Danger : Text;
        GUI.Label(R(x0 + 150 + modeWidth + 24, 20, clockWidth + 2, 54), clock, labelRight);
        labelRight.normal.textColor = Text;
    }

    /// Returns the top of the free space below the network widget, in virtual units.
    float DrawNetwork(View v, float W) {
        bool delayed = v.networkSilence >= 1.5f;
        Color dot = delayed ? Danger : v.rttMs < 60 ? Health : v.rttMs < 150 ? Charge : Danger;
        if (!v.showNetwork) {
            Rect chip = R(W - 24 - 180, 20, 180, 34);
            Fill(chip, Panel, RoundSmall);
            float blink = delayed ? Mathf.PingPong(Time.unscaledTime * 3, 1) : 1;
            Fill(Centered(new Vector2(chip.x + S(18), chip.center.y), S(9)), WithAlpha(dot, .35f + .65f * blink), Circle);
            GUI.Label(R(W - 24 - 180 + 32, 20, 140, 34), delayed ? "MẠNG ĐANG CHẬM" : $"RTT {v.rttMs:0} ms", label);
            GUI.Label(R(W - 24 - 180, 20, 166, 34), "F3", labelRight);
            return 20 + 34 + 10;
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
        return 20 + 176 + 10;
    }

    float DrawDisconnectedPlayers(View v, float W, float y) {
        if (v.disconnectedPlayers == null || v.disconnectedPlayers.Length == 0) return y;
        float height = 34 + v.disconnectedPlayers.Length * 24;
        Rect panel = R(W - 24 - 270, y, 270, height);
        Fill(panel, Panel, RoundBig);
        small.normal.textColor = Danger;
        GUI.Label(R(W - 24 - 252, y + 7, 234, 22), "MẤT KẾT NỐI — ĐANG GIỮ CHỖ", small);
        small.normal.textColor = TextMuted;
        for (int i = 0; i < v.disconnectedPlayers.Length; i++)
            GUI.Label(R(W - 24 - 252, y + 31 + i * 24, 234, 22), v.disconnectedPlayers[i], label);
        return y + height + 10;
    }

    static string Short(string value, int max) {
        if (string.IsNullOrEmpty(value) || value.Length <= max) return value ?? "";
        return "…" + value.Substring(value.Length - max + 1);
    }

    /// Returns the bottom of the objective chip in virtual units.
    float DrawObjective(View v, float W) {
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
                    ? $"Bạn đang cầm mảnh — không thể tấn công • Q để thả • Khóa Lửa {o.fireLock:0}/{o.fireLockMax:0}"
                    : $"{v.carrierName} đang cầm mảnh — nổ trúng để phá Khóa Lửa {o.fireLock:0}/{o.fireLockMax:0}";
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
        return 82 + height;
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

    /// Slides down and fades in whenever a new toast arrives.
    void DrawToast(string text, float alpha, float W, float top) {
        if (Event.current.type == EventType.Repaint) {
            if (text != shownToast || alpha > shownToastAlpha + .01f) toastAt = Time.unscaledTime;
            shownToast = text;
            shownToastAlpha = alpha;
        }
        float t = EaseOut(Mathf.Clamp01((Time.unscaledTime - toastAt) / .25f));
        Color saved = GUI.color;
        GUI.color = new Color(1, 1, 1, alpha * t);
        float width = toast.CalcSize(new GUIContent(text)).x / u + 48;
        Rect r = R(W / 2 - width / 2, top - 18 * (1 - t), width, 52);
        Fill(new Rect(r.x + S(5), r.y + S(6), r.width, r.height), new Color(0, 0, 0, .45f), RoundBig);
        Fill(r, PanelRaised, RoundBig);
        Fill(new Rect(r.x, r.y, S(6), r.height), Core, null);
        GUI.Label(r, text, toast);
        GUI.color = saved;
    }

    void DrawMinimap(View v, float W, float top) {
        if (v.arenaSize <= 0) return;
        float size = 220;
        Rect frame = R(W - 24 - size, top, size, size);
        Fill(new Rect(frame.x + S(5), frame.y + S(6), frame.width, frame.height), new Color(0, 0, 0, .4f), RoundBig);
        Fill(frame, Panel, RoundBig);
        Rect area = new Rect(frame.x + S(12), frame.y + S(12), frame.width - S(24), frame.height - S(24));
        Fill(area, WithAlpha(Color.Lerp(Night, MapAccent(v.mapId), .35f), .9f), RoundSmall);
        float half = v.arenaSize;
        float scale = area.width / (half * 2);
        Vector2 ToMap(float x, float z) => new Vector2(area.x + (x + half) * scale, area.y + (half - z) * scale);

        if (v.bushes != null) foreach (BushData b in v.bushes)
            Fill(Centered(ToMap(b.x, b.z), Mathf.Max(S(5), b.radius * 2 * scale)), BushInk, Circle);
        if (v.obstacles != null) foreach (ObstacleData o in v.obstacles)
            Fill(Centered(ToMap(o.x, o.z), Mathf.Max(S(4), o.radius * 2 * scale)), ObstacleInk, Circle);

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
            if (d.isMe) Fill(Centered(p, dot + S(10) + Mathf.PingPong(Time.unscaledTime * 10, S(5))), WithAlpha(Text, .5f), Ring);
            Fill(Centered(p, dot + S(4)), d.isMe ? Text : Night, Circle);
            Fill(Centered(p, dot), d.color, Circle);
        }

        if (hasObjective && (obj.state == "GROUND" || obj.state == "PLACED")) {
            Vector2 p = ToMap(obj.x, obj.z);
            float pulse = S(12) + Mathf.PingPong(Time.unscaledTime * 10, S(4));
            Diamond(p, pulse + S(4), Night);
            Diamond(p, pulse, Core);
        }
        DrawMinimapLegend(frame, hasObjective);
    }

    void DrawMinimapLegend(Rect frame, bool objective) {
        Rect strip = new Rect(frame.x, frame.yMax + S(6), frame.width, S(24));
        Fill(strip, Panel, RoundSmall);
        float x = strip.x + S(10);
        x = LegendItem(x, strip, "Bạn", Text, false);
        x = LegendItem(x, strip, "Bụi", BushInk, false);
        if (objective) {
            x = LegendItem(x, strip, "Mảnh", Core, true);
            LegendItem(x, strip, "Bệ", WithAlpha(Text, .6f), false, Ring);
        } else LegendItem(x, strip, "Vật cản", ObstacleInk, false);
    }

    float LegendItem(float x, Rect strip, string text, Color color, bool diamond, GUIStyle shape = null) {
        Vector2 c = new Vector2(x + S(5), strip.center.y);
        if (diamond) Diamond(c, S(8), color);
        else Fill(Centered(c, S(10)), color, shape ?? Circle);
        float width = legend.CalcSize(new GUIContent(text)).x;
        GUI.Label(new Rect(x + S(14), strip.y, width + 2, strip.height), text, legend);
        return x + S(14) + width + S(12);
    }

    static void Diamond(Vector2 center, float size, Color color) {
        Matrix4x4 saved = GUI.matrix;
        GUIUtility.RotateAroundPivot(45, center);
        Fill(Centered(center, size), color, RoundSmall);
        GUI.matrix = saved;
    }

    void DrawCharge(PlayerData me, float W, float H, bool carryingShard) {
        float width = Mathf.Min(560, W - 460);
        float x = W / 2 - width / 2, y = H - 92;
        Fill(R(x - 16, y - 34, width + 32, 78), Panel, RoundBig);
        bool charging = me.charging && !carryingShard;
        bool full = charging && me.charge >= .99f;
        bool reloading = !carryingShard && !charging && me.chargeCooldown > 0;
        bool blink = Mathf.PingPong(Time.unscaledTime * 4, 1) > .5f;
        Color hot = new Color(1, .95f, .6f);

        string caption = carryingShard ? "ĐANG GIỮ MẢNH • Q ĐỂ THẢ"
            : full ? "ĐẦY! THẢ SPACE ĐỂ NỔ"
            : charging ? "ĐANG NẠP • THẢ SPACE ĐỂ NỔ"
            : reloading ? $"BOM ĐANG HỒI • {me.chargeCooldown:0.0}s"
            : "Giữ SPACE để nạp, thả để nổ";
        label.normal.textColor = full ? (blink ? hot : Charge) : charging ? Charge : carryingShard ? Core : TextMuted;
        GUI.Label(R(x, y - 30, width, 24), caption, label);
        label.normal.textColor = TextMuted;
        if (charging) GUI.Label(R(x, y - 30, width, 24), $"{me.charge * 100:0}%", labelRight);

        // A full charge glows behind the bar so the release moment is obvious at a glance.
        if (full) Fill(R(x - 5, y - 5, width + 10, 32), WithAlpha(Charge, blink ? .45f : .2f), RoundSmall);
        Color fill = carryingShard ? Core : reloading ? WithAlpha(TextMuted, .55f)
            : full ? Color.Lerp(Charge, hot, Mathf.PingPong(Time.unscaledTime * 4, 1)) : Charge;
        float value = carryingShard ? 1 : charging ? me.charge
            : reloading ? Mathf.Clamp01(1 - me.chargeCooldown / BombCooldown) : 0;
        Bar(R(x, y, width, 22), value, fill);
        // Quarter ticks make the fill level readable without looking at the percentage.
        if (charging) for (int i = 1; i < 4; i++)
            Fill(R(x + width * i / 4f - 1, y + 4, 2, 14), new Color(0, 0, 0, .3f), null);
    }

    void DrawAbility(int slot, Vector2 center, string title, string keyName, Color color, bool active, float cooldown, float total) {
        if (Event.current.type == EventType.Repaint) {
            if (lastCooldown[slot] > 0 && cooldown <= 0) readyAt[slot] = Time.unscaledTime;
            lastCooldown[slot] = cooldown;
        }
        float flash = Mathf.Clamp01(1 - (Time.unscaledTime - readyAt[slot]) / .6f);

        Vector2 c = new Vector2(S(center.x), S(center.y));
        float d = S(84);
        Fill(Centered(c, d + S(10)), Panel, Circle);
        float ready = Mathf.Clamp01(1 - cooldown / total);
        // Kenney round button, Grey set tinted with the ability colour; plain discs without the art.
        GUIStyle face = Skin("Grey", "button_round_depth_gradient", 0, 0, 0, 0) ?? Circle;
        Fill(Centered(c, d), Color.Lerp(color, Night, .72f), face);
        // Liquid fill: clip the coloured button to the recovered fraction, rising from the bottom.
        Rect disc = Centered(c, d);
        float visible = d * ready;
        GUI.BeginClip(new Rect(disc.x, disc.yMax - visible, d, visible));
        Fill(new Rect(0, visible - d, d, d), cooldown > 0 ? Color.Lerp(color, Night, .35f) : color, face);
        GUI.EndClip();
        if (active) Fill(Centered(c, d + S(14) + Mathf.PingPong(Time.unscaledTime * 30, S(8))), color, Ring);
        if (flash > 0) {
            // Ready pop: a bright disc and a ring that expands outwards and fades.
            Fill(Centered(c, d), WithAlpha(Color.white, flash * .4f), Circle);
            Fill(Centered(c, d + S(10) + S(40) * (1 - flash)), WithAlpha(color, flash), Ring);
        }
        if (cooldown > 0) {
            key.normal.textColor = WithAlpha(Text, .6f);
            GUI.Label(new Rect(disc.x, disc.y + S(12), d, S(16)), keyName, key);
            GUI.Label(new Rect(disc.x, disc.y + S(8), d, d), $"{cooldown:0.0}", big);
        } else {
            key.normal.textColor = Night;
            GUI.Label(disc, keyName, key);
            key.normal.textColor = WithAlpha(Text, .6f);
        }
        centerText.normal.textColor = flash > 0 ? color : cooldown > 0 ? TextMuted : Text;
        GUI.Label(new Rect(c.x - S(60), c.y + d / 2 + S(6), S(120), S(20)), flash > 0 ? "SẴN SÀNG" : title, centerText);
        centerText.normal.textColor = Text;
    }

    void DrawSpectating(float W, float H) {
        const string text = "Bạn đã bị loại — đang xem phần còn lại của trận";
        float width = label.CalcSize(new GUIContent(text)).x / u + 48;
        Rect r = R(W / 2 - width / 2, H - 92, width, 44);
        Fill(r, Panel, RoundBig);
        Fill(new Rect(r.x, r.y, S(6), r.height), Danger, null);
        GUI.Label(r, text, centerText);
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
    /// The pill is kept inside the screen and the name gets a drop shadow so it reads over any floor.
    public void DrawNameTag(Vector2 screenTop, string playerName, float hp, bool isMe,
                            bool connected, float reconnectRemaining,
                            bool isCarrier, float fireLock, float fireLockMax) {
        EnsureStyles();
        string shownName = connected ? playerName : $"{playerName}  •  MẤT KẾT NỐI {Mathf.CeilToInt(reconnectRemaining)}s";
        float width = tagName.CalcSize(new GUIContent(shownName)).x + S(18);
        float nameOffset = isMe ? S(28) : S(40);
        Rect pill = new Rect(screenTop.x - width / 2, screenTop.y - nameOffset, width, S(22));
        pill.x = Mathf.Clamp(pill.x, S(4), Mathf.Max(S(4), Screen.width - width - S(4)));
        float cx = pill.center.x;
        Fill(pill, WithAlpha(Panel, .86f), RoundSmall);
        if (isMe) Fill(new Rect(pill.x + S(6), pill.yMax - S(3), pill.width - S(12), S(2)), Charge, null);
        tagName.normal.textColor = new Color(0, 0, 0, .75f);
        GUI.Label(new Rect(pill.x + 1, pill.y + 1, pill.width, pill.height), shownName, tagName);
        tagName.normal.textColor = !connected ? Danger : isMe ? Charge : Text;
        GUI.Label(pill, shownName, tagName);

        // Fire Lock: the shard's own health bar above the carrier's name.
        if (isCarrier && fireLockMax > 0) {
            float lockWidth = Mathf.Clamp(width, S(68), S(104));
            Bar(new Rect(cx - lockWidth / 2, pill.y - S(12), lockWidth, S(7)), fireLock / fireLockMax, Core);
        }

        if (isMe) return;

        float health = Mathf.Clamp01(hp / 100f);
        float barWidth = Mathf.Clamp(width, S(68), S(104));
        Rect track = new Rect(cx - barWidth / 2, pill.yMax + S(4), barWidth, S(8));
        Color healthColor = health <= .3f ? Danger : health <= .6f ? Charge : Health;
        Fill(new Rect(track.x - S(2), track.y - S(2), track.width + S(4), track.height + S(4)),
            WithAlpha(Night, .92f), RoundSmall);
        Bar(track, health, healthColor);
    }

    /// Reconnect card shown while the client waits for the server; once the hold window
    /// expires it offers a way back to the menu. Returns true when that button is clicked.
    public bool DrawReconnect(float secondsRemaining, string endpoint, int attempt) {
        u = Mathf.Clamp(Screen.height / 1080f, .75f, 1.6f);
        EnsureStyles();
        bool expired = secondsRemaining <= 0;
        float width = S(560), height = S(expired ? 164 : 104);
        Rect card = new Rect(Screen.width / 2f - width / 2, S(154), width, height);
        Fill(new Rect(card.x + S(6), card.y + S(8), width, height), new Color(0, 0, 0, .48f), RoundBig);
        Fill(card, Panel, RoundBig);
        Color accent = expired ? Danger : Charge;
        Fill(new Rect(card.x, card.y, width, S(6)), accent, null);
        banner.normal.textColor = accent;
        string dots = new string('.', 1 + Mathf.FloorToInt(Time.unscaledTime * 3) % 3);
        GUI.Label(new Rect(card.x, card.y + S(10), width, S(48)),
            expired ? "HẾT THỜI GIAN GIỮ PHIÊN" : "ĐANG KẾT NỐI LẠI" + dots, banner);
        string detail = expired
            ? "Server không phản hồi trong 10 giây. Bạn có thể quay lại menu."
            : $"{Short(endpoint, 34)}  •  còn {secondsRemaining:0.0}s  •  lần thử {Mathf.Max(1, attempt)}";
        GUI.Label(new Rect(card.x + S(20), card.y + S(60), width - S(40), S(34)), detail, bannerSub);
        if (!expired) {
            Bar(new Rect(card.x + S(40), card.yMax - S(14), width - S(80), S(6)), secondsRemaining / 10f, accent);
            return false;
        }
        Rect menu = new Rect(card.center.x - S(110), card.y + S(104), S(220), S(44));
        return Button(menu, "VỀ MENU CHÍNH", Charge, Night, button);
    }

    /// Centered result card shown while the round is FINISHED; drops in when its title changes.
    public void DrawBanner(string title, string subtitle, Color accent) {
        EnsureStyles();
        if (title != shownBanner) {
            shownBanner = title;
            bannerAt = Time.unscaledTime;
        }
        float t = EaseOut(Mathf.Clamp01((Time.unscaledTime - bannerAt) / .35f));
        Color saved = GUI.color;
        GUI.color = new Color(1, 1, 1, t);
        float width = S(520), height = S(120);
        Rect card = new Rect(Screen.width / 2f - width / 2, S(110) - S(30) * (1 - t), width, height);
        Fill(new Rect(card.x + S(6), card.y + S(8), width, height), new Color(0, 0, 0, .45f), RoundBig);
        Fill(card, Panel, RoundBig);
        Fill(new Rect(card.x, card.y, width, S(6)), accent, null);
        banner.normal.textColor = accent;
        GUI.Label(new Rect(card.x, card.y + S(16), width, S(52)), title, banner);
        GUI.Label(new Rect(card.x + S(20), card.y + S(68), width - S(40), S(40)), subtitle, bannerSub);
        GUI.color = saved;
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
        button = TextStyle(Px(17), FontStyle.Bold, Night, TextAnchor.MiddleCenter);
        legend = TextStyle(Px(11), FontStyle.Bold, TextMuted, TextAnchor.MiddleLeft);
    }
}
