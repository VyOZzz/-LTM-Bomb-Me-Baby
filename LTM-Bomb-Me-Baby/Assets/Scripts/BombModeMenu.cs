using UnityEngine;
using static BombUi;

/// Main menu after connecting, in two steps like most party games:
/// 1. pick a mode (two cards), 2. find a random match of that mode, create a private room,
/// or join a friend's with a code.
/// Drawn with IMGUI like the rest of the client; colors and shapes come from BombUi.
sealed class BombModeMenu {
    public enum Choice { None, Create, Join, FindMatch, CancelMatch, Settings, Quit }

    sealed class Card {
        public string mode, key, tag, title, description, tooltip;
        public string[,] rows;
        public Color accent;
        public float lift;
    }

    static readonly string[] JourneyMaps = { "meadow", "crossroads", "crater" };
    static readonly string[] JourneyNames = { "Đồng cỏ", "Ngã tư", "Núi lửa" };
    const int CodeLength = 6;

    readonly Card[] cards = {
        new Card {
            mode = "QUICK", key = "1", tag = "1 TRẬN  •  2–10 NGƯỜI", title = "ĐẤU NHANH",
            description = "Ăn năng lượng, nạp nổ và sống sót cuối cùng. Tối đa 3 phút; hết giờ còn nhiều người thì hòa.",
            rows = new[,] { { "Bản đồ", "Ngẫu nhiên mỗi trận" }, { "Thắng khi", "Chỉ còn mình bạn" } },
            tooltip = "Phím 1  •  Một trận sinh tồn, hợp để làm quen", accent = Charge
        },
        new Card {
            mode = "JOURNEY", key = "2", tag = "3 CHƯƠNG  •  TRANH MẢNH", title = "HÀNH TRÌNH BA MẢNH",
            description = "Mỗi chương một mảnh Tim Lửa. Cướp mảnh, đặt lên bệ đá giữa map và giữ bệ đến hết 3 phút.",
            rows = new[,] { { "Lộ trình", "Đồng cỏ • Ngã tư • Núi lửa" }, { "Thắng", "Mang về nhiều mảnh nhất" } },
            tooltip = "Phím 2  •  Ba chương liên tiếp, có lời dẫn truyện", accent = Core
        }
    };

    bool roomStep, wasRoomStep;
    float roomStepShownAt;
    int selected;
    // Matchmaking state as last reported by the server (MATCHMAKING messages).
    bool searching;
    int waiting, minPlayers = 2;
    float searchStartedAt;
    Vector2 lastMouse;
    // Layout is in virtual units scaled by GUI.matrix; text is drawn unscaled with
    // font sizes multiplied by s, because IMGUI stretches glyph bitmaps and blurs them.
    float s = 1, stylesScale;
    GUIStyle field, kicker, heading, chip, chipCenter, tagStyle, cardTitle, bodyText, rowLabel, rowValue, cta, keycap, placeholder, footer, mapLabel;

    /// Protocol mode of the card the player picked: QUICK or JOURNEY.
    public string SelectedMode => cards[selected].mode;

    public bool Searching => searching;

    /// Esc on the menu: from the room step back to the mode cards. Returns false on the first step
    /// and while searching, so the queued mode stays on screen until the search is cancelled.
    public bool Back() {
        if (!roomStep || searching) return false;
        roomStep = false;
        return true;
    }

    public void ShowSearch(string mode, int waitingPlayers, int requiredPlayers) {
        if (!searching) searchStartedAt = Time.unscaledTime;
        searching = true;
        waiting = waitingPlayers;
        if (requiredPlayers > 0) minPlayers = requiredPlayers;
        for (int i = 0; i < cards.Length; i++) if (cards[i].mode == mode) selected = i;
        roomStep = true;
    }

    public void StopSearch() { searching = false; }

    public Choice Draw(string playerName, string status, ref string joinCode) {
        s = Mathf.Clamp(Screen.height / 860f, .7f, 1.6f);
        EnsureStyles();
        Matrix4x4 baseMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1));
        float width = Screen.width / s, height = Screen.height / s;
        Choice choice = Choice.None;

        if (roomStep && !wasRoomStep) roomStepShownAt = Time.unscaledTime;
        wasRoomStep = roomStep;
        DrawBackdrop(width, height);
        Label(new Rect(56, 40, 600, 24), "BOMB ME BABY", kicker);
        Label(new Rect(54, 62, 900, 56), roomStep ? cards[selected].title : "Chọn chế độ chơi", heading);
        choice = DrawHeader(width, playerName);

        Choice step = roomStep ? DrawRoomStep(width, height, ref joinCode) : DrawModeStep(width, height);
        if (step != Choice.None) choice = step;
        if (!string.IsNullOrEmpty(status))
            Label(new Rect(0, height - 76, width, 24), status, footer);
        Label(new Rect(0, height - 40, width, 24),
            searching ? "Bấm Hủy tìm để đổi chế độ" : roomStep ? "Enter để tìm trận  •  Esc để chọn chế độ khác"
                : "1 / 2 hoặc ← → rồi Enter để chọn", footer);

        GUI.matrix = baseMatrix;
        return choice;
    }

    Choice DrawHeader(float width, string playerName) {
        Choice choice = Choice.None;
        float chipWidth = Mathf.Max(170, chip.CalcSize(new GUIContent(playerName)).x / s + 52);
        Rect quitRect = new Rect(width - 56 - 96, 52, 96, 38);
        Rect settingsRect = new Rect(quitRect.x - 12 - 112, 52, 112, 38);
        if (HeaderButton(quitRect, "Thoát", Danger, "Đóng game")) choice = Choice.Quit;
        if (HeaderButton(settingsRect, "Cài đặt", Text, "Âm thanh và hiển thị")) choice = Choice.Settings;
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

    /// Step 2: find a random match of the chosen mode, create a private room, or join one with its code.
    Choice DrawRoomStep(float width, float height, ref string joinCode) {
        Card card = cards[selected];
        Choice choice = Choice.None;
        Rect back = new Rect(56, 124, 210, 36);
        if (!searching && HeaderButton(back, "← Chọn chế độ khác", TextMuted, "Esc")) roomStep = false;

        bool stacked = width < 820;
        float gap = 28;
        float panelWidth = stacked ? Mathf.Min(520, width - 80) : Mathf.Min(440, (width - 112 - gap) / 2);
        float panelHeight = 340;
        float joinHeight = stacked ? 250 : panelHeight;
        float left = (width - (stacked ? panelWidth : panelWidth * 2 + gap)) / 2;
        float top = Mathf.Max(164, (height - (stacked ? panelHeight + joinHeight + gap : panelHeight)) / 2);
        Rect create = new Rect(left, top, panelWidth, panelHeight);
        Rect join = stacked ? new Rect(left, top + panelHeight + gap, panelWidth, joinHeight)
                            : new Rect(left + panelWidth + gap, top, panelWidth, panelHeight);

        Event e = Event.current;
        bool fieldFocused = GUI.GetNameOfFocusedControl() == "join-code";
        string code = joinCode ?? "";
        // The server refuses rooms while queued, so joining waits until the search is cancelled.
        bool codeReady = code.Length == CodeLength && !searching;
        // Read Enter before the text field gets the event: a focused TextField swallows it.
        bool enter = GUI.enabled && e.type == EventType.KeyDown
            && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter);
        if (enter && fieldFocused) {
            if (codeReady) { BombAudio.UiClick(); choice = Choice.Join; }
            e.Use();
        } else if (enter && !searching && Time.unscaledTime - roomStepShownAt > .35f) {
            // Enter anywhere else is the panel's primary action; the delay keeps the Enter
            // that opened this step (or its key repeat) from also starting a search.
            BombAudio.UiClick();
            choice = Choice.FindMatch;
            e.Use();
        }

        // Public matchmaking and code-only rooms are distinct, visible actions.
        Panel3D(create, card.accent);
        tagStyle.normal.textColor = card.accent;
        Rect button = new Rect(create.x + 28, create.yMax - 74, create.width - 56, 50);
        if (searching) {
            float elapsed = Time.unscaledTime - searchStartedAt;
            int seconds = Mathf.FloorToInt(elapsed);
            string dots = new string('.', 1 + Mathf.FloorToInt(Time.unscaledTime * 2) % 3);
            // Pulsing beacon: the queue is alive even when the player count does not change.
            float pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * 5);
            Vector2 beacon = new Vector2(create.xMax - 44, create.y + 36);
            Fill(Centered(beacon, 18 + 14 * pulse), WithAlpha(card.accent, .35f * (1 - pulse)), Circle);
            Fill(Centered(beacon, 12), card.accent, Circle);
            Label(new Rect(create.x + 28, create.y + 26, create.width - 56, 18), $"ĐANG TÌM  •  {seconds / 60}:{seconds % 60:00}", tagStyle);
            Label(new Rect(create.x + 28, create.y + 48, create.width - 56, 40), "ĐANG TÌM TRẬN" + dots, cardTitle);
            Label(new Rect(create.x + 28, create.y + 96, create.width - 56, 80), waiting >= minPlayers
                ? $"Đã có {waiting} người. Trận sẽ bắt đầu sau vài giây, chờ thêm người vào cùng."
                : $"{waiting}/{minPlayers} người đang chờ chế độ này. Cần ít nhất {minPlayers} người để bắt đầu.", bodyText);
            Bar(new Rect(button.x, button.y - 20, button.width, 8), waiting / (float)Mathf.Max(1, minPlayers), card.accent);
            if (BigButton(button, "HỦY TÌM", PanelRaised, Danger, true, "Rời hàng chờ")) choice = Choice.CancelMatch;
        } else {
            Label(new Rect(create.x + 28, create.y + 26, create.width - 56, 18), "GHÉP NGẪU NHIÊN", tagStyle);
            Label(new Rect(create.x + 28, create.y + 48, create.width - 56, 40), "TÌM TRẬN", cardTitle);
            Label(new Rect(create.x + 28, create.y + 96, create.width - 56, 80),
                "Ghép với người chơi khác cũng đang tìm chế độ này. Đủ người là vào trận ngay.", bodyText);
            Rect privateButton = new Rect(button.x, button.y - 66, button.width, 50);
            if (BigButton(privateButton, "TẠO PHÒNG RIÊNG", PanelRaised, Text, true,
                    "Bạn bè vào bằng mã; người tìm trận ngẫu nhiên không vào phòng này"))
                choice = Choice.Create;
            if (BigButton(button, "TÌM TRẬN", card.accent, Night, true, "Ghép ngẫu nhiên với người đang chờ (Enter)"))
                choice = Choice.FindMatch;
        }

        // Join panel
        Panel3D(join, Text);
        tagStyle.normal.textColor = TextMuted;
        Label(new Rect(join.x + 28, join.y + 26, join.width - 56, 18), "CÓ MÃ PHÒNG", tagStyle);
        Label(new Rect(join.x + 28, join.y + 48, join.width - 56, 40), "VÀO PHÒNG BẠN BÈ", cardTitle);
        Rect input = new Rect(join.x + 28, join.y + 104, join.width - 56, 64);
        Color savedBackground = GUI.backgroundColor;
        // The Kenney box stays light (dark ink), warming toward the accent while focused.
        Color fieldTint = InputBackground != null ? Color.white : Night;
        GUI.backgroundColor = fieldFocused ? Color.Lerp(fieldTint, card.accent, InputBackground != null ? .18f : .25f) : fieldTint;
        Matrix4x4 savedMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.identity;
        Rect inputScreen = ToScreen(input);
        TrackHover(GUIUtility.GetControlID(FocusType.Passive), inputScreen.Contains(Event.current.mousePosition) && GUI.enabled,
            inputScreen, "Mã 6 ký tự chủ phòng thấy ở góc phòng chờ");
        GUI.SetNextControlName("join-code");
        string typed = TextField(inputScreen, code, CodeLength + 2, field);
        GUI.matrix = savedMatrix;
        GUI.backgroundColor = savedBackground;
        joinCode = NormalizeCode(typed);
        if (joinCode.Length == 0 && !fieldFocused) Label(input, "Nhập mã, ví dụ 3FA9C1", placeholder);
        Label(new Rect(join.x + 28, input.yMax + 8, join.width - 56, 20),
            $"{joinCode.Length}/{CodeLength}  •  chỉ gồm số 0–9 và chữ A–F", rowLabel);
        codeReady = joinCode.Length == CodeLength && !searching;
        if (BigButton(new Rect(join.x + 28, join.yMax - 74, join.width - 56, 50), "VÀO PHÒNG", Text, Night,
                codeReady, "Vào phòng với mã đã nhập (Enter)"))
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

    bool BigButton(Rect button, string text, Color face, Color textColor, bool enabled = true, string tooltip = null) =>
        ScaledButton(button, text, face, textColor, cta, enabled, tooltip);

    bool HeaderButton(Rect rect, string text, Color textColor, string tooltip = null) =>
        ScaledButton(rect, text, PanelRaised, textColor, chipCenter, true, tooltip);

    /// BombUi.Button laid out in virtual units but drawn unscaled, so its label stays crisp.
    bool ScaledButton(Rect rect, string text, Color face, Color ink, GUIStyle style, bool enabled, string tooltip) {
        Matrix4x4 saved = GUI.matrix;
        GUI.matrix = Matrix4x4.identity;
        bool clicked = Button(ToScreen(rect), text, face, ink, style, enabled, tooltip);
        GUI.matrix = saved;
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

        // The whole card is the hit area; the button only shows where to click.
        int id = GUIUtility.GetControlID(FocusType.Passive);
        bool hover = GUI.enabled && panel.Contains(Event.current.mousePosition);
        Matrix4x4 savedMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.identity;
        TrackHover(id, hover, ToScreen(panel), card.tooltip);
        GUI.matrix = savedMatrix;
        Rect button = new Rect(x, panel.yMax - 66, w, 48);
        bool pressed = hover && Input.GetMouseButton(0);
        string choose = lift > .5f ? "CHỌN  ▸" : "CHỌN";
        // Drawn in screen space like Label so the text keeps its pixel-sized font.
        GUI.matrix = Matrix4x4.identity;
        bool skinned = ButtonFace(ToScreen(button), choose, card.accent, Night, cta, true, lift > .5f, pressed);
        GUI.matrix = savedMatrix;
        if (!skinned) {
            float push = pressed ? 4 : -2 * lift;
            Fill(new Rect(button.x, button.y + 5, button.width, button.height), Color.Lerp(card.accent, Color.black, .45f), RoundSmall);
            Fill(new Rect(button.x, button.y + push, button.width, button.height),
                Color.Lerp(card.accent, Color.white, .1f * lift), RoundSmall);
            Label(new Rect(button.x, button.y + push, button.width, button.height), choose, cta);
        }

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
        chipCenter = TextStyle(Px(16), FontStyle.Bold, Text, TextAnchor.MiddleCenter);
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
        // Kenney input box (white with a grey rim) when the UI art is present.
        field.normal.background = field.hover.background = field.focused.background =
            InputBackground != null ? InputBackground : RoundSmall.normal.background;
        placeholder = TextStyle(Px(15), FontStyle.Bold, WithAlpha(InputBackground != null ? Night : Text, .45f), TextAnchor.MiddleCenter);
        // This input is rendered on a light text-field surface.
        field.normal.textColor = field.hover.textColor = field.active.textColor = Night;
        field.focused.textColor = field.onNormal.textColor = field.onHover.textColor = Night;
        field.onActive.textColor = field.onFocused.textColor = Night;
    }
}
