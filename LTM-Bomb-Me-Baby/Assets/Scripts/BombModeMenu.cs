using UnityEngine;
using static BombUi;

/// Main menu after connecting, in two steps like most party games:
/// 1. pick a mode (two cards), 2. create a room of that mode or join a friend's with a code.
/// Drawn with IMGUI like the rest of the client; colors and shapes come from BombUi.
sealed class BombModeMenu {
    public enum Choice { None, Create, Join, Settings, Quit }

    sealed class Card {
        public string mode, key, tag, title, description;
        public string[,] rows;
        public Color accent;
        public float lift;
    }

    static readonly string[] JourneyMaps = { "meadow", "crossroads", "crater" };
    static readonly string[] JourneyNames = { "Đồng cỏ", "Ngã tư", "Núi lửa" };
    const int CodeLength = 6;

    readonly Card[] cards = {
        new Card {
            mode = "QUICK", key = "1", tag = "1 TRẬN  •  1–10 NGƯỜI", title = "ĐẤU NHANH",
            description = "Vào trận ngay. Ăn năng lượng cho bóng to ra, nạp nổ và là quả bom cuối cùng còn lăn.",
            rows = new[,] { { "Bản đồ", "Ngẫu nhiên mỗi trận" }, { "Thắng khi", "Chỉ còn mình bạn" } },
            accent = Charge
        },
        new Card {
            mode = "JOURNEY", key = "2", tag = "3 CHƯƠNG  •  TRANH MẢNH", title = "HÀNH TRÌNH BA MẢNH",
            description = "Mỗi chương một mảnh Tim Lửa. Cướp mảnh, đặt lên bệ đá giữa map và giữ bệ đến hết 3 phút.",
            rows = new[,] { { "Lộ trình", "Đồng cỏ • Ngã tư • Núi lửa" }, { "Thắng", "Mang về nhiều mảnh nhất" } },
            accent = Core
        }
    };

    bool roomStep;
    int selected;
    Vector2 lastMouse;
    // Layout is in virtual units scaled by GUI.matrix; text is drawn unscaled with
    // font sizes multiplied by s, because IMGUI stretches glyph bitmaps and blurs them.
    float s = 1, stylesScale;
    GUIStyle field, kicker, heading, chip, tagStyle, cardTitle, bodyText, rowLabel, rowValue, cta, keycap, footer, mapLabel;

    /// Protocol mode of the card the player picked: QUICK or JOURNEY.
    public string SelectedMode => cards[selected].mode;

    /// Esc on the menu: from the room step back to the mode cards. Returns false on the first step.
    public bool Back() {
        if (!roomStep) return false;
        roomStep = false;
        return true;
    }

    public Choice Draw(string playerName, string status, ref string joinCode) {
        s = Mathf.Clamp(Screen.height / 860f, .7f, 1.6f);
        EnsureStyles();
        Matrix4x4 baseMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1));
        float width = Screen.width / s, height = Screen.height / s;
        Choice choice = Choice.None;

        DrawBackdrop(width, height);
        Label(new Rect(56, 40, 600, 24), "BOMB ME BABY", kicker);
        Label(new Rect(54, 62, 900, 56), roomStep ? cards[selected].title : "Chọn chế độ chơi", heading);
        choice = DrawHeader(width, playerName);

        Choice step = roomStep ? DrawRoomStep(width, height, ref joinCode) : DrawModeStep(width, height);
        if (step != Choice.None) choice = step;
        if (!string.IsNullOrEmpty(status))
            Label(new Rect(0, height - 76, width, 24), status, footer);
        Label(new Rect(0, height - 40, width, 24),
            roomStep ? "Esc để chọn chế độ khác" : "1 / 2 hoặc ← → rồi Enter để chọn", footer);

        GUI.matrix = baseMatrix;
        return choice;
    }

    Choice DrawHeader(float width, string playerName) {
        Choice choice = Choice.None;
        float chipWidth = Mathf.Max(170, chip.CalcSize(new GUIContent(playerName)).x / s + 52);
        Rect quitRect = new Rect(width - 56 - 96, 52, 96, 38);
        Rect settingsRect = new Rect(quitRect.x - 12 - 112, 52, 112, 38);
        if (HeaderButton(quitRect, "Thoát", Danger)) choice = Choice.Quit;
        if (HeaderButton(settingsRect, "Cài đặt", Text)) choice = Choice.Settings;
        Rect chipRect = new Rect(settingsRect.x - 20 - chipWidth, 52, chipWidth, 38);
        Fill(chipRect, Panel, RoundSmall);
        Fill(new Rect(chipRect.x + 16, chipRect.y + 14, 10, 10), Health, Circle);
        Label(new Rect(chipRect.x + 34, chipRect.y, chipWidth - 40, 38), playerName, chip);
        return choice;
    }

    Choice DrawModeStep(float width, float height) {
        bool stacked = width < 820;
        float gap = 28;
        float cardWidth = stacked ? Mathf.Min(520, width - 80) : Mathf.Min(440, (width - 112 - gap) / 2);
        float cardHeight = stacked ? Mathf.Min(330, (height - 300) / 2) : Mathf.Min(470, height - 280);
        float left = (width - (stacked ? cardWidth : cardWidth * 2 + gap)) / 2;
        float top = stacked ? 136 : Mathf.Max(140, (height - cardHeight) / 2 - 10);

        Event e = Event.current;
        if (e.type == EventType.MouseMove || e.mousePosition != lastMouse) {
            for (int i = 0; i < cards.Length; i++)
                if (CardRect(i, left, top, cardWidth, cardHeight, gap, stacked).Contains(e.mousePosition)) selected = i;
            lastMouse = e.mousePosition;
        }

        for (int i = 0; i < cards.Length; i++) {
            Rect rect = CardRect(i, left, top, cardWidth, cardHeight, gap, stacked);
            if (e.type == EventType.Repaint)
                cards[i].lift = Mathf.MoveTowards(cards[i].lift, i == selected ? 1 : 0, Time.unscaledDeltaTime * 7);
            if (DrawCard(cards[i], rect, i)) { selected = i; roomStep = true; }
        }

        if (GUI.enabled && e.type == EventType.KeyDown) {
            if (e.keyCode == KeyCode.LeftArrow || e.keyCode == KeyCode.UpArrow) { selected = 0; e.Use(); }
            else if (e.keyCode == KeyCode.RightArrow || e.keyCode == KeyCode.DownArrow) { selected = 1; e.Use(); }
            else if (e.keyCode == KeyCode.Alpha1 || e.keyCode == KeyCode.Keypad1) { selected = 0; roomStep = true; e.Use(); }
            else if (e.keyCode == KeyCode.Alpha2 || e.keyCode == KeyCode.Keypad2) { selected = 1; roomStep = true; e.Use(); }
            else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { roomStep = true; e.Use(); }
        }
        return Choice.None;
    }

    /// Step 2: create a room of the chosen mode, or join a friend's room with its code.
    Choice DrawRoomStep(float width, float height, ref string joinCode) {
        Card card = cards[selected];
        Choice choice = Choice.None;
        Rect back = new Rect(56, 124, 210, 36);
        if (HeaderButton(back, "← Chọn chế độ khác", TextMuted)) roomStep = false;

        bool stacked = width < 820;
        float gap = 28;
        float panelWidth = stacked ? Mathf.Min(520, width - 80) : Mathf.Min(440, (width - 112 - gap) / 2);
        float panelHeight = stacked ? 250 : 330;
        float left = (width - (stacked ? panelWidth : panelWidth * 2 + gap)) / 2;
        float top = Mathf.Max(184, (height - (stacked ? panelHeight * 2 + gap : panelHeight)) / 2);
        Rect create = new Rect(left, top, panelWidth, panelHeight);
        Rect join = stacked ? new Rect(left, top + panelHeight + gap, panelWidth, panelHeight)
                            : new Rect(left + panelWidth + gap, top, panelWidth, panelHeight);

        Event e = Event.current;
        bool fieldFocused = GUI.GetNameOfFocusedControl() == "join-code";
        string code = joinCode ?? "";
        bool codeReady = code.Length == CodeLength;
        // Read Enter before the text field gets the event: a focused TextField swallows it.
        bool enter = GUI.enabled && e.type == EventType.KeyDown
            && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter);
        if (enter && fieldFocused) {
            if (codeReady) choice = Choice.Join;
            e.Use();
        }

        // Create panel
        Panel3D(create, card.accent);
        tagStyle.normal.textColor = card.accent;
        Label(new Rect(create.x + 28, create.y + 26, create.width - 56, 18), "CHỦ PHÒNG", tagStyle);
        Label(new Rect(create.x + 28, create.y + 48, create.width - 56, 40), "TẠO PHÒNG MỚI", cardTitle);
        Label(new Rect(create.x + 28, create.y + 96, create.width - 56, 80),
            "Bạn tạo phòng rồi gửi mã 6 ký tự cho bạn bè. Mọi người bấm Sẵn sàng là bắt đầu.", bodyText);
        if (BigButton(new Rect(create.x + 28, create.yMax - 74, create.width - 56, 50), "TẠO PHÒNG", card.accent, Night))
            choice = Choice.Create;

        // Join panel
        Panel3D(join, Text);
        tagStyle.normal.textColor = TextMuted;
        Label(new Rect(join.x + 28, join.y + 26, join.width - 56, 18), "CÓ MÃ PHÒNG", tagStyle);
        Label(new Rect(join.x + 28, join.y + 48, join.width - 56, 40), "VÀO PHÒNG BẠN BÈ", cardTitle);
        Rect input = new Rect(join.x + 28, join.y + 104, join.width - 56, 64);
        Color savedBackground = GUI.backgroundColor;
        GUI.backgroundColor = fieldFocused ? Color.Lerp(Night, card.accent, .25f) : Night;
        Matrix4x4 savedMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.identity;
        GUI.SetNextControlName("join-code");
        string typed = GUI.TextField(ToScreen(input), code, CodeLength + 2, field);
        GUI.matrix = savedMatrix;
        GUI.backgroundColor = savedBackground;
        joinCode = NormalizeCode(typed);
        if (joinCode.Length == 0 && !fieldFocused) Label(input, "Nhập mã, ví dụ 3FA9C1", keycap);
        Label(new Rect(join.x + 28, input.yMax + 8, join.width - 56, 20),
            $"{joinCode.Length}/{CodeLength}  •  chỉ gồm số 0–9 và chữ A–F", rowLabel);
        if (BigButton(new Rect(join.x + 28, join.yMax - 74, join.width - 56, 50), "VÀO PHÒNG",
                joinCode.Length == CodeLength ? Text : PanelRaised, joinCode.Length == CodeLength ? Night : TextFaint)
                && joinCode.Length == CodeLength)
            choice = Choice.Join;
        return choice;
    }

    /// Room codes are 6 hex digits; accept lowercase and the common O→0 mix-up.
    static string NormalizeCode(string raw) {
        var sb = new System.Text.StringBuilder();
        foreach (char c in (raw ?? "").ToUpperInvariant()) {
            char d = c == 'O' ? '0' : c;
            if ((d >= '0' && d <= '9') || (d >= 'A' && d <= 'F')) sb.Append(d);
            if (sb.Length == CodeLength) break;
        }
        return sb.ToString();
    }

    Rect ToScreen(Rect r) => new Rect(r.x * s, r.y * s, r.width * s, r.height * s);

    void Label(Rect rect, string text, GUIStyle style) {
        Matrix4x4 saved = GUI.matrix;
        GUI.matrix = Matrix4x4.identity;
        GUI.Label(ToScreen(rect), text, style);
        GUI.matrix = saved;
    }

    void Panel3D(Rect rect, Color accent) {
        Fill(new Rect(rect.x + 7, rect.y + 9, rect.width, rect.height), new Color(0, 0, 0, .5f), RoundBig);
        Fill(rect, PanelRaised, RoundBig);
        Fill(new Rect(rect.x + 28, rect.y, rect.width - 56, 5), accent, null);
    }

    bool BigButton(Rect button, string text, Color face, Color textColor) {
        bool pressed = GUI.enabled && button.Contains(Event.current.mousePosition) && Input.GetMouseButton(0);
        float push = pressed ? 4 : 0;
        Fill(new Rect(button.x, button.y + 5, button.width, button.height), Color.Lerp(face, Color.black, .45f), RoundSmall);
        Fill(new Rect(button.x, button.y + push, button.width, button.height), face, RoundSmall);
        cta.normal.textColor = textColor;
        Label(new Rect(button.x, button.y + push, button.width, button.height), text, cta);
        cta.normal.textColor = Night;
        bool clicked = GUI.Button(button, GUIContent.none, GUIStyle.none);
        if (clicked) BombAudio.UiClick();
        return clicked;
    }

    bool HeaderButton(Rect rect, string text, Color textColor) {
        bool hover = rect.Contains(Event.current.mousePosition);
        Fill(new Rect(rect.x, rect.y + 4, rect.width, rect.height), new Color(0, 0, 0, .4f), RoundSmall);
        Fill(rect, hover ? PanelRaised : Panel, RoundSmall);
        chip.normal.textColor = textColor;
        chip.alignment = TextAnchor.MiddleCenter;
        Label(rect, text, chip);
        chip.alignment = TextAnchor.MiddleLeft;
        chip.normal.textColor = Text;
        bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
        if (clicked) BombAudio.UiClick();
        return clicked;
    }

    static Rect CardRect(int index, float left, float top, float width, float height, float gap, bool stacked) =>
        stacked ? new Rect(left, top + index * (height + gap), width, height)
                : new Rect(left + index * (width + gap), top, width, height);

    bool DrawCard(Card card, Rect rect, int index) {
        float lift = Mathf.SmoothStep(0, 1, card.lift);
        Rect panel = new Rect(rect.x, rect.y - 8 * lift, rect.width, rect.height);

        // Hard offset shadow instead of a blur: reads as "toy" rather than "web card".
        Fill(new Rect(panel.x + 7, panel.y + 9 + 6 * lift, panel.width, panel.height), new Color(0, 0, 0, .5f), RoundBig);
        if (lift > .01f)
            Fill(new Rect(panel.x - 3, panel.y - 3, panel.width + 6, panel.height + 6), WithAlpha(card.accent, lift), RoundBig);
        Fill(panel, PanelRaised, RoundBig);

        Rect art = new Rect(panel.x + 12, panel.y + 12, panel.width - 24, Mathf.Min(150, panel.height * .36f));
        Fill(art, Color.Lerp(Night, card.accent, .2f), RoundBig);
        if (index == 0) DrawBombArt(art, lift); else DrawShardArt(art, lift);

        Rect key = new Rect(art.xMax - 40, art.y + 12, 28, 28);
        Fill(key, new Color(0, 0, 0, .35f), RoundSmall);
        Label(key, card.key, keycap);

        float x = panel.x + 26, w = panel.width - 52, y = art.yMax + 18;
        tagStyle.normal.textColor = card.accent;
        Label(new Rect(x, y, w, 18), card.tag, tagStyle);
        y += 22;
        float titleHeight = cardTitle.CalcHeight(new GUIContent(card.title), w * s) / s;
        Label(new Rect(x, y, w, titleHeight), card.title, cardTitle);
        y += titleHeight + 6;
        float bodyHeight = bodyText.CalcHeight(new GUIContent(card.description), w * s) / s;
        Label(new Rect(x, y, w, bodyHeight), card.description, bodyText);
        y += bodyHeight + 12;

        for (int r = 0; r < card.rows.GetLength(0) && y + 24 < panel.yMax - 76; r++) {
            Fill(new Rect(x, y + 11, 4, 4), card.accent, Circle);
            Label(new Rect(x + 14, y, 90, 26), card.rows[r, 0], rowLabel);
            Label(new Rect(x + 104, y, w - 104, 26), card.rows[r, 1], rowValue);
            y += 26;
        }

        Rect button = new Rect(x, panel.yMax - 66, w, 48);
        bool pressed = button.Contains(Event.current.mousePosition) && Input.GetMouseButton(0);
        float push = pressed ? 4 : 0;
        Fill(new Rect(button.x, button.y + 5, button.width, button.height), Color.Lerp(card.accent, Color.black, .45f), RoundSmall);
        Fill(new Rect(button.x, button.y + push, button.width, button.height), card.accent, RoundSmall);
        Label(new Rect(button.x, button.y + push, button.width, button.height), "CHỌN", cta);

        bool clicked = GUI.Button(panel, GUIContent.none, GUIStyle.none);
        if (clicked) BombAudio.UiClick();
        return clicked;
    }

    /// The in-game bomb: black body, white eyes, lit fuse, orange charge ring.
    void DrawBombArt(Rect art, float lift) {
        Vector2 center = art.center + new Vector2(0, 6);
        Matrix4x4 saved = GUI.matrix;
        GUIUtility.RotateAroundPivot(Time.unscaledTime * 6, center);
        for (int i = 0; i < 12; i++) {
            GUIUtility.RotateAroundPivot(30, center);
            Fill(new Rect(center.x - 2, art.y - 30, 4, art.height * .42f), WithAlpha(Charge, .16f), null);
        }
        GUI.matrix = saved;

        float pulse = 1 + Mathf.Sin(Time.unscaledTime * 3) * .04f * (1 + lift);
        Fill(Centered(center, 118 * pulse), WithAlpha(Charge, .45f), Ring);
        Fill(Centered(center, 84), BombBody, Circle);
        Fill(Centered(center + new Vector2(-16, -16), 18), new Color(1, 1, 1, .16f), Circle);
        Fill(new Rect(center.x - 14, center.y - 12, 9, 18), Text, RoundSmall);
        Fill(new Rect(center.x + 5, center.y - 12, 9, 18), Text, RoundSmall);

        Vector2 fuseBase = center + new Vector2(20, -34);
        saved = GUI.matrix;
        GUIUtility.RotateAroundPivot(35, fuseBase);
        Fill(new Rect(fuseBase.x - 6, fuseBase.y - 18, 12, 22), new Color(.45f, .33f, .24f), RoundSmall);
        GUI.matrix = saved;
        float spark = 12 + Mathf.PingPong(Time.unscaledTime * 30, 8);
        Fill(Centered(fuseBase + new Vector2(14, -18), spark + 8), WithAlpha(Charge, .5f), Circle);
        Fill(Centered(fuseBase + new Vector2(14, -18), spark), new Color(1, .9f, .55f), Circle);
    }

    /// Three Core-like crystals, one per chapter, tinted with that arena's accent.
    void DrawShardArt(Rect art, float lift) {
        float step = art.width / 3.4f;
        float startX = art.center.x - step;
        for (int i = 0; i < 3; i++) {
            Color color = MapAccent(JourneyMaps[i]);
            float bob = Mathf.Sin(Time.unscaledTime * 2.2f + i * 1.3f) * (3 + 3 * lift);
            Vector2 c = new Vector2(startX + i * step, art.center.y - 10 + bob);
            if (i < 2)
                for (float t = .28f; t < .74f; t += .11f)
                    Fill(Centered(new Vector2(c.x + step * t, art.center.y - 10), 5), new Color(1, 1, 1, .25f), Circle);
            Fill(Centered(c, 70), WithAlpha(Core, .22f + .12f * lift), Circle);
            Matrix4x4 saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(45, c);
            Fill(Centered(c + new Vector2(3, 4), 44), new Color(0, 0, 0, .35f), RoundSmall);
            Fill(Centered(c, 44), color, RoundSmall);
            Fill(Centered(c - new Vector2(8, 8), 14), new Color(1, 1, 1, .4f), RoundSmall);
            GUI.matrix = saved;
            mapLabel.normal.textColor = Color.Lerp(color, Text, .4f);
            Label(new Rect(c.x - 50, art.yMax - 34, 100, 20), $"{i + 1}. {JourneyNames[i]}", mapLabel);
        }
    }

    void DrawBackdrop(float width, float height) {
        Fill(new Rect(0, 0, width, height), Night, null);
        Matrix4x4 saved = GUI.matrix;
        Vector2 pivot = new Vector2(width / 2, height / 2);
        GUIUtility.RotateAroundPivot(-24, pivot);
        float span = Mathf.Max(width, height) * 1.6f;
        float drift = Time.unscaledTime * 12 % 64;
        for (float x = -span / 2; x < span / 2; x += 64)
            Fill(new Rect(pivot.x + x + drift, pivot.y - span / 2, 22, span), new Color(1, 1, 1, .02f), null);
        GUI.matrix = saved;
        Fill(new Rect(0, height - 6, width, 6), cards[selected].accent, null);
    }

    int Px(int size) => Mathf.RoundToInt(size * s);

    void EnsureStyles() {
        EnsureShapes();
        if (field != null && Mathf.Approximately(stylesScale, s)) return;
        stylesScale = s;
        kicker = TextStyle(Px(14), FontStyle.Bold, Charge);
        heading = TextStyle(Px(40), FontStyle.Bold, Text);
        chip = TextStyle(Px(16), FontStyle.Bold, Text, TextAnchor.MiddleLeft);
        tagStyle = TextStyle(Px(13), FontStyle.Bold, Text);
        cardTitle = TextStyle(Px(28), FontStyle.Bold, Text);
        cardTitle.wordWrap = true;
        bodyText = TextStyle(Px(16), FontStyle.Normal, TextMuted);
        bodyText.wordWrap = true;
        rowLabel = TextStyle(Px(14), FontStyle.Normal, TextFaint, TextAnchor.MiddleLeft);
        rowValue = TextStyle(Px(16), FontStyle.Bold, Text, TextAnchor.MiddleLeft);
        cta = TextStyle(Px(18), FontStyle.Bold, Night, TextAnchor.MiddleCenter);
        keycap = TextStyle(Px(15), FontStyle.Bold, WithAlpha(Text, .55f), TextAnchor.MiddleCenter);
        footer = TextStyle(Px(14), FontStyle.Normal, TextFaint, TextAnchor.MiddleCenter);
        mapLabel = TextStyle(Px(13), FontStyle.Bold, Text, TextAnchor.MiddleCenter);

        field = new GUIStyle(GUI.skin.textField) {
            fontSize = Px(34), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
            border = new RectOffset(10, 10, 10, 10), padding = new RectOffset(Px(12), Px(12), Px(8), Px(8))
        };
        field.normal.background = field.hover.background = field.focused.background = RoundSmall.normal.background;
        // This input is rendered on Unity's light text-field surface.
        field.normal.textColor = field.hover.textColor = field.active.textColor = Night;
        field.focused.textColor = field.onNormal.textColor = field.onHover.textColor = Night;
        field.onActive.textColor = field.onFocused.textColor = Night;
    }
}
