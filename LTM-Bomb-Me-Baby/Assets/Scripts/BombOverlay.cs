using UnityEngine;
using static BombUi;

/// Esc pause menu, settings, controls help and leave confirmation, drawn over the menu, lobby or match.
/// Multiplayer keeps running underneath; the overlay only blocks this client's input.
/// Every page also works from the keyboard: Up/Down (or Tab) move the highlight, Left/Right change
/// the highlighted value, Enter activates it or, on a slider, the page's main button.
sealed class BombOverlay {
    public enum Page { None, Pause, Settings, ConfirmLeave, Help }
    public enum Action { None, LeaveRoom, QuitGame }

    // Keys mirror BombGame.Update/HandleInput; keep both in sync when controls change.
    static readonly (string key, string action)[] MatchKeys = {
        ("W A S D  /  ← ↑ ↓ →", "Lăn quả bom của bạn"),
        ("GIỮ SPACE", "Nạp bom, giữ lâu để nạp đầy"),
        ("THẢ SPACE", "Kích nổ"),
        ("E", "Bật khiên"),
        ("SHIFT", "Tăng tốc"),
    };
    static readonly (string key, string action)[] JourneyKeys = {
        ("Q", "Thả Mảnh Tim Lửa đang mang"),
        ("H", "Ẩn / hiện bảng hướng dẫn hành trình"),
    };
    static readonly (string key, string action)[] OtherKeys = {
        ("F3", "Ẩn / hiện số liệu mạng"),
        ("ESC", "Tạm dừng hoặc quay lại"),
    };
    static readonly string[] FpsLabels = System.Array.ConvertAll<int, string>(BombSettings.FpsOptions, BombSettings.FpsLabel);
    // Secondary buttons sit on PanelRaised cards, so their face is lifted a touch to stay visible.
    static readonly Color Secondary = Color.Lerp(PanelRaised, Text, .1f);

    Page page;
    Page settingsBack;
    float u = 1, stylesScale;
    float openedAt, pageChangedAt;
    // Keyboard focus: highlighted item on the page and how many items the last pass drew.
    int focus, itemCount, nextItem;
    int keyStep;          // -1/+1 from Left/Right in the current event, until an item uses it
    bool keyActivate;     // Enter in the current event, until an item uses it
    bool focusPassive;    // the focused item ignores Enter, so the page's main button takes it
    bool mouseMoved;
    Vector2 lastMouse;
    GUIStyle title, subtitle, buttonText, label, valueText, valueRight, note, keyChip, helpText;

    public bool IsOpen => page != Page.None;

    public void OpenPause() => Go(Page.Pause);

    public void OpenSettings() {
        settingsBack = page;
        Go(Page.Settings);
    }

    /// Esc: step back one level, closing the overlay from its first page.
    public void Back() {
        if (page == Page.Settings) {
            BombSettings.Save();
            Go(settingsBack);
        } else if (page == Page.ConfirmLeave || page == Page.Help) Go(Page.Pause);
        else Go(Page.None);
    }

    void Go(Page next) {
        if (page == Page.None && next != Page.None) openedAt = Time.unscaledTime;
        page = next;
        pageChangedAt = Time.unscaledTime;
        focus = 0;
        itemCount = 0;
    }

    float S(float v) => v * u;
    int Px(int size) => Mathf.RoundToInt(size * u);

    static float Ease(float t) {
        t = Mathf.Clamp01(t);
        return 1 - (1 - t) * (1 - t) * (1 - t);
    }

    public Action Draw(bool inRoom, bool inMatch) {
        if (page == Page.None) return Action.None;
        u = Mathf.Clamp(Screen.height / 1080f, .75f, 1.6f);
        EnsureStyles();
        ReadKeyboard();

        float fade = Ease((Time.unscaledTime - openedAt) / .2f);
        float enter = Ease((Time.unscaledTime - pageChangedAt) / .22f);
        Color savedColor = GUI.color;
        Matrix4x4 savedMatrix = GUI.matrix;
        GUI.color = WithAlpha(savedColor, fade);
        Fill(new Rect(0, 0, Screen.width, Screen.height), WithAlpha(Night, .72f), null);
        // Each page settles in: fades up, grows from 94% and rises a few pixels.
        GUIUtility.ScaleAroundPivot(Vector2.one * Mathf.Lerp(.94f, 1, enter), new Vector2(Screen.width / 2f, Screen.height / 2f));
        GUI.matrix = Matrix4x4.Translate(new Vector3(0, S(18) * (1 - enter), 0)) * GUI.matrix;
        GUI.color = WithAlpha(savedColor, fade * enter);

        Page drawn = page;
        nextItem = 0;
        focusPassive = false;
        Action result = Action.None;
        switch (page) {
            case Page.Pause: result = DrawPause(inRoom, inMatch); break;
            case Page.Settings: DrawSettings(); break;
            case Page.ConfirmLeave: result = DrawConfirm(); break;
            case Page.Help: DrawHelp(); break;
        }
        if (page == drawn) {
            itemCount = nextItem;
            // Left/Right that no value used walks between buttons, e.g. Stay / Leave.
            if (keyStep != 0) MoveFocus(keyStep);
        }
        GUI.matrix = savedMatrix;
        GUI.color = savedColor;
        return result;
    }

    void ReadKeyboard() {
        Event e = Event.current;
        keyStep = 0;
        keyActivate = false;
        if (e.type == EventType.Repaint) {
            mouseMoved = e.mousePosition != lastMouse;
            lastMouse = e.mousePosition;
        }
        if (e.type != EventType.KeyDown) return;
        switch (e.keyCode) {
            case KeyCode.UpArrow: MoveFocus(-1); e.Use(); break;
            case KeyCode.DownArrow: MoveFocus(1); e.Use(); break;
            case KeyCode.Tab: MoveFocus(e.shift ? -1 : 1); e.Use(); break;
            case KeyCode.LeftArrow: keyStep = -1; e.Use(); break;
            case KeyCode.RightArrow: keyStep = 1; e.Use(); break;
            case KeyCode.Return:
            case KeyCode.KeypadEnter: keyActivate = true; e.Use(); break;
        }
    }

    void MoveFocus(int step) {
        if (itemCount == 0) return;
        focus = (focus + step + itemCount) % itemCount;
        BombAudio.UiHover();
    }

    Action DrawPause(bool inRoom, bool inMatch) {
        Rect card = Card(420, inRoom ? 490 : 410, Charge);
        GUI.Label(Row(card, 26, 48), "TẠM DỪNG", title);
        GUI.Label(Row(card, 72, 24), inMatch ? "Trận vẫn tiếp diễn với những người chơi khác" : "Nhấn Esc để quay lại", subtitle);
        TitleRule(card, 104);
        float y = 116;
        if (Button(Row(card, y, 54, 28), "TIẾP TỤC", Charge, Night)) Go(Page.None);
        y += 70;
        if (Button(Row(card, y, 54, 28), "CÀI ĐẶT", Secondary, Text, "Âm thanh, hiển thị và khung hình")) OpenSettings();
        y += 70;
        if (Button(Row(card, y, 54, 28), "HƯỚNG DẪN", Secondary, Text, "Phím điều khiển và cách chơi")) Go(Page.Help);
        y += 70;
        if (inRoom) {
            string hint = inMatch ? "Rời trận đang chơi và quay về chọn chế độ" : "Rời phòng và quay về chọn chế độ";
            if (Button(Row(card, y, 54, 28), "VỀ MENU CHÍNH", Secondary, Text, hint)) {
                if (inMatch) Go(Page.ConfirmLeave);
                else { Go(Page.None); return Action.LeaveRoom; }
            }
            y += 70;
        }
        if (Button(Row(card, y, 54, 28), "THOÁT GAME", Secondary, Danger)) return Action.QuitGame;
        return Action.None;
    }

    Action DrawConfirm() {
        Rect card = Card(460, 250, Danger);
        GUI.Label(Row(card, 26, 44), "RỜI TRẬN?", title);
        GUI.Label(Row(card, 74, 50, 32), "Bạn sẽ bị loại khỏi trận này và quay về menu chính.", subtitle);
        TitleRule(card, 138);
        float half = (card.width - S(56) - S(16)) / 2;
        Rect stay = new Rect(card.x + S(28), card.y + S(160), half, S(54));
        Rect leave = new Rect(stay.xMax + S(16), stay.y, half, S(54));
        // Staying is first so Enter never drops a player out of a match by accident.
        if (Button(stay, "Ở LẠI", Secondary, Text)) Go(Page.Pause);
        if (Button(leave, "RỜI TRẬN", Danger, Night)) { Go(Page.None); return Action.LeaveRoom; }
        return Action.None;
    }

    void DrawSettings() {
        Rect card = Card(520, 730, Shield);
        GUI.Label(Row(card, 26, 48), "CÀI ĐẶT", title);
        TitleRule(card, 80);
        float y = 92;
        GUI.Label(Row(card, y, 22), "ÂM THANH", label);
        y += 32;
        BombSettings.Master = Slider(card, ref y, "Âm lượng tổng", BombSettings.Master, Charge);
        BombSettings.Music = Slider(card, ref y, "Nhạc", BombSettings.Music, Core);
        BombSettings.Sfx = Slider(card, ref y, "Hiệu ứng", BombSettings.Sfx, Shield);
        AudioListener.volume = BombSettings.Master;
        GUI.Label(Row(card, y - 6, 22), "Nhạc tự đổi theo menu/map; hiệu ứng lớn sẽ hạ nhạc trong chốc lát.", note);
        y += 34;
        GUI.Label(Row(card, y, 22), "HIỂN THỊ", label);
        y += 32;
        bool fullscreen = Toggle(card, ref y, "Toàn màn hình", BombSettings.Fullscreen);
        bool vsync = Toggle(card, ref y, "Đồng bộ dọc (VSync)", BombSettings.VSync,
            "Khớp khung hình với tần số quét của màn hình, chống xé hình.");
        int fpsIndex = Mathf.Max(0, System.Array.IndexOf(BombSettings.FpsOptions, BombSettings.TargetFps));
        fpsIndex = Cycle(card, ref y, "Giới hạn khung hình", FpsLabels, fpsIndex, !vsync,
            vsync ? "Tắt VSync để tự chọn giới hạn khung hình." : "Giới hạn thấp giúp máy mát hơn và đỡ tốn pin.");
        BombSettings.ShowNetwork = Toggle(card, ref y, "Hiện số liệu mạng (F3)", BombSettings.ShowNetwork,
            "Ping, số gói tin và băng thông ở góc màn hình.");
        BombSettings.ShowControls = Toggle(card, ref y, "Hiện gợi ý điều khiển", BombSettings.ShowControls,
            "Bảng phím tắt nhỏ ở góc dưới khi vào trận.");
        int fps = BombSettings.FpsOptions[fpsIndex];
        if (fullscreen != BombSettings.Fullscreen || vsync != BombSettings.VSync || fps != BombSettings.TargetFps) {
            BombSettings.Fullscreen = fullscreen;
            BombSettings.VSync = vsync;
            BombSettings.TargetFps = fps;
            BombSettings.Apply();
        }

        float half = (card.width - S(56) - S(16)) / 2;
        Rect reset = new Rect(card.x + S(28), card.y + S(646), half, S(54));
        Rect done = new Rect(reset.xMax + S(16), reset.y, half, S(54));
        if (Button(reset, "MẶC ĐỊNH", Secondary, Text, "Khôi phục mọi tuỳ chọn về như lúc mới cài.")) BombSettings.ResetDefaults();
        if (Button(done, "XONG", Shield, Night, null, true)) Back();
    }

    void DrawHelp() {
        Rect card = Card(580, 690, Energy);
        GUI.Label(Row(card, 26, 48), "HƯỚNG DẪN", title);
        GUI.Label(Row(card, 72, 24), "Lăn, nạp, nổ và trụ lại đến cuối cùng", subtitle);
        TitleRule(card, 104);
        float y = 116;
        y = KeySection(card, y, "TRONG TRẬN", MatchKeys);
        y = KeySection(card, y, "HÀNH TRÌNH BA MẢNH", JourneyKeys);
        y = KeySection(card, y, "KHÁC", OtherKeys);
        GUI.Label(Row(card, y + 2, 22), "Mẹo: nấp trong bụi cỏ để đối thủ không nhìn thấy bạn.", note);
        if (Button(Row(card, 600, 54, 28), "ĐÃ HIỂU", Energy, Night)) Back();
    }

    float KeySection(Rect card, float y, string heading, (string key, string action)[] rows) {
        GUI.Label(Row(card, y, 22), heading, label);
        y += 30;
        foreach (var (key, action) in rows) {
            Rect row = Row(card, y, 32);
            Rect chip = new Rect(row.x, row.y, S(190), row.height);
            Fill(chip, new Color(0, 0, 0, .3f), RoundSmall);
            GUI.Label(chip, key, keyChip);
            GUI.Label(new Rect(chip.xMax + S(16), row.y, row.width - chip.width - S(16), row.height), action, helpText);
            y += 38;
        }
        return y;
    }

    /// Registers the next keyboard-focusable item; moving the pointer over it takes the focus.
    int Item(Rect hit) {
        int index = nextItem++;
        if (Event.current.type == EventType.Repaint && mouseMoved && hit.Contains(Event.current.mousePosition)) focus = index;
        return index;
    }

    /// True once per Enter press, for the focused item only.
    bool TakeActivate(int index) {
        if (!keyActivate || index != focus) return false;
        keyActivate = false;
        return true;
    }

    /// Pulsing halo behind the keyboard-focused control.
    void FocusRing(Rect rect, Color accent) {
        float pulse = .55f + .25f * Mathf.Sin(Time.unscaledTime * 5);
        Fill(new Rect(rect.x - S(4), rect.y - S(4), rect.width + S(8), rect.height + S(12)),
            WithAlpha(Color.Lerp(accent, Text, .45f), pulse), RoundSmall);
    }

    /// Soft row background under the hovered or focused setting, with an accent tick when focused.
    void RowHighlight(Rect area, bool hover, bool focused, Color accent) {
        if (!hover && !focused) return;
        Fill(area, WithAlpha(Text, hover ? .07f : .05f), RoundSmall);
        if (focused) Fill(new Rect(area.x, area.y + S(8), S(4), area.height - S(16)), accent, null);
    }

    float Slider(Rect card, ref float y, string name, float value, Color color) {
        Rect area = Row(card, y - 8, 60, 16);
        Rect track = Row(card, y + 30, 12);
        Rect hit = new Rect(track.x - S(10), track.y - S(12), track.width + S(20), track.height + S(24));
        int index = Item(area);
        int id = GUIUtility.GetControlID(FocusType.Passive);
        bool focused = index == focus;
        bool hover = area.Contains(Event.current.mousePosition) || GUIUtility.hotControl == id;
        TrackHover(id, hover, area);
        RowHighlight(area, hover, focused, color);
        GUI.Label(Row(card, y, 22), name, valueText);
        GUI.Label(Row(card, y, 22), $"{value * 100:0}%", valueRight);
        Event e = Event.current;
        switch (e.GetTypeForControl(id)) {
            case EventType.MouseDown:
                if (hit.Contains(e.mousePosition)) { GUIUtility.hotControl = id; value = ValueAt(track, e); e.Use(); }
                break;
            case EventType.MouseDrag:
                if (GUIUtility.hotControl == id) { value = ValueAt(track, e); e.Use(); }
                break;
            case EventType.MouseUp:
                if (GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; e.Use(); }
                break;
        }
        if (focused) {
            focusPassive = true;
            if (keyStep != 0) {
                value = Mathf.Clamp01(Mathf.Round((value + keyStep * .05f) * 20) / 20);
                keyStep = 0;
                BombAudio.UiHover();
            }
        }
        Vector2 thumb = new Vector2(track.x + track.width * value, track.center.y);
        if (!SkinnedSlider(track, thumb, value, color, hover || focused)) {
            Bar(track, value, color);
            float knob = S(hover || focused ? 28 : 24);
            Fill(Centered(thumb, knob), Text, Circle);
            Fill(Centered(thumb, knob / 2), color, Circle);
        }
        y += 64;
        return value;
    }

    /// Kenney slider: grey groove, coloured fill up to the value and a pointer handle.
    bool SkinnedSlider(Rect track, Vector2 thumb, float value, Color color, bool active) {
        string set = SkinSet(color);
        GUIStyle groove = Skin("Grey", "slide_horizontal_grey", 8, 8, 0, 0);
        GUIStyle fill = Skin(set, "slide_horizontal_color", 8, 8, 0, 0);
        GUIStyle handle = Skin(set, "slide_hangle", 0, 0, 0, 0);
        if (groove == null || fill == null || handle == null) return false;
        Rect bar = new Rect(track.x, track.center.y - S(8), track.width, S(16));
        Fill(bar, Color.white, groove);
        if (value > .001f) Fill(new Rect(bar.x, bar.y, Mathf.Max(bar.height, bar.width * value), bar.height), Color.white, fill);
        float grow = active ? 1.15f : 1;
        Fill(new Rect(thumb.x - S(12) * grow, thumb.y - S(13) * grow, S(24) * grow, S(32) * grow), Color.white, handle);
        return true;
    }

    /// Kenney rule between a page's heading and its content.
    void TitleRule(Rect card, float y) => Divider(Row(card, y, 3, 90), WithAlpha(TextFaint, .8f));

    static float ValueAt(Rect track, Event e) => Mathf.Clamp01((e.mousePosition.x - track.x) / track.width);

    bool Toggle(Rect card, ref float y, string name, bool value, string tooltip = null) {
        Rect row = Row(card, y, 36);
        Rect area = Row(card, y - 6, 48, 16);
        int index = Item(area);
        int id = GUIUtility.GetControlID(FocusType.Passive);
        bool focused = index == focus;
        bool hover = area.Contains(Event.current.mousePosition);
        TrackHover(id, hover, area, tooltip);
        RowHighlight(area, hover, focused, Health);
        GUI.Label(row, name, valueText);
        // Kenney check box (green tick when on); the procedural switch is the fallback.
        GUIStyle on = Skin("Green", "check_square_color_checkmark", 6, 6, 6, 6);
        GUIStyle off = Skin("Grey", "check_square_grey", 6, 6, 6, 6);
        if (on != null && off != null) {
            Rect box = new Rect(row.xMax - S(34), row.center.y - S(16), S(32), S(32));
            Fill(box, hover || focused ? Color.white : new Color(.9f, .9f, .92f), value ? on : off);
        } else {
            Rect knobTrack = new Rect(row.xMax - S(64), row.y + S(4), S(64), S(28));
            Fill(knobTrack, value ? Health : new Color(0, 0, 0, .35f), RoundSmall);
            Fill(Centered(new Vector2(value ? knobTrack.xMax - S(14) : knobTrack.x + S(14), knobTrack.center.y), S(22)), Text, Circle);
        }
        bool flip = GUI.Button(area, GUIContent.none, GUIStyle.none) || TakeActivate(index);
        if (focused && keyStep != 0) { flip = true; keyStep = 0; }
        if (flip) {
            BombAudio.UiClick();
            value = !value;
        }
        y += 50;
        return value;
    }

    /// Row that cycles through fixed values: click or Enter for the next one, Left/Right to step.
    int Cycle(Rect card, ref float y, string name, string[] options, int selected, bool enabled, string tooltip = null) {
        Rect row = Row(card, y, 36);
        Rect area = Row(card, y - 6, 48, 16);
        int index = Item(area);
        int id = GUIUtility.GetControlID(FocusType.Passive);
        bool focused = index == focus;
        bool hover = area.Contains(Event.current.mousePosition);
        TrackHover(id, hover, area, tooltip);
        RowHighlight(area, hover && enabled, focused, Charge);
        Color saved = GUI.color;
        if (!enabled) GUI.color = WithAlpha(saved, .45f);
        GUI.Label(row, name, valueText);
        GUI.Label(row, "◀  " + options[selected] + "  ▶", valueRight);
        GUI.color = saved;
        int step = 0;
        if (GUI.Button(area, GUIContent.none, GUIStyle.none)) step = 1;
        if (TakeActivate(index)) step = 1;
        if (focused && keyStep != 0) { step = keyStep; keyStep = 0; }
        if (step != 0 && enabled) {
            BombAudio.UiClick();
            selected = (selected + step + options.Length) % options.Length;
        }
        y += 50;
        return selected;
    }

    /// Shared chunky button plus keyboard focus; a primary button also takes Enter from a focused slider.
    bool Button(Rect rect, string text, Color face, Color ink, string tooltip = null, bool primary = false) {
        int index = Item(rect);
        if (index == focus) FocusRing(rect, face);
        bool clicked = BombUi.Button(rect, text, face, ink, buttonText, true, tooltip);
        if (TakeActivate(index) || primary && focusPassive && keyActivate) {
            keyActivate = false;
            BombAudio.UiClick();
            clicked = true;
        }
        return clicked;
    }

    Rect Card(float width, float height, Color accent) {
        Rect card = new Rect(Screen.width / 2f - S(width) / 2, Screen.height / 2f - S(height) / 2, S(width), S(height));
        BombUi.Card(card, accent, u);
        return card;
    }

    /// Full-width row inside a card at a vertical offset (virtual units), with side padding.
    Rect Row(Rect card, float y, float height, float padding = 28) =>
        new Rect(card.x + S(padding), card.y + S(y), card.width - S(padding * 2), S(height));

    void EnsureStyles() {
        EnsureShapes();
        if (title != null && Mathf.Approximately(stylesScale, u)) return;
        stylesScale = u;
        title = TextStyle(Px(34), FontStyle.Bold, Text, TextAnchor.MiddleCenter);
        subtitle = TextStyle(Px(15), FontStyle.Normal, TextMuted, TextAnchor.MiddleCenter);
        subtitle.wordWrap = true;
        buttonText = TextStyle(Px(18), FontStyle.Bold, Night, TextAnchor.MiddleCenter);
        label = TextStyle(Px(13), FontStyle.Bold, TextFaint, TextAnchor.MiddleLeft);
        valueText = TextStyle(Px(16), FontStyle.Bold, Text, TextAnchor.MiddleLeft);
        valueRight = TextStyle(Px(16), FontStyle.Bold, Text, TextAnchor.MiddleRight);
        note = TextStyle(Px(12), FontStyle.Italic, TextFaint, TextAnchor.MiddleLeft);
        keyChip = TextStyle(Px(15), FontStyle.Bold, Charge, TextAnchor.MiddleCenter);
        helpText = TextStyle(Px(16), FontStyle.Normal, Text, TextAnchor.MiddleLeft);
    }
}
