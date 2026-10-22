using UnityEngine;
using static BombUi;

/// Full-screen entry menu shown before connecting to the server.
/// The network endpoint stays behind an advanced disclosure so the first thing
/// players see is the game, not a debugging form.
sealed class BombMainMenu {
    public enum Choice { None, Connect, Quit }

    static readonly string[] FieldOrder = { "menu-name", "menu-host", "menu-port" };

    Texture2D background;
    bool advanced;
    float styleScale;
    // The card slides in whenever the menu reappears after another screen.
    float openedAt = -1, lastDrawAt = -1;
    GUIStyle logo, subtitle, label, field, button, secondary, helper, error, statusStyle, version;

    public void ShowNetworkSettings() { advanced = true; }

    public Choice Draw(ref string playerName, ref string host, ref string port, string status) {
        EnsureStyles();
        if (background == null) background = Resources.Load<Texture2D>("Menu/menu_background");
        float now = Time.unscaledTime;
        if (lastDrawAt < 0 || now - lastDrawAt > .5f) openedAt = now;
        lastDrawAt = now;
        float appear = Mathf.SmoothStep(0, 1, Mathf.Clamp01((now - openedAt) / .45f));

        Rect screen = new Rect(0, 0, Screen.width, Screen.height);
        if (background != null) GUI.DrawTexture(screen, background, ScaleMode.ScaleAndCrop, false);
        else Fill(screen, Night, null);

        // Darken the artwork just enough for high-contrast menu text.
        Fill(screen, new Color(.02f, .015f, .05f, .18f), null);
        float shadeWidth = Mathf.Clamp(Screen.width * .47f, 500, 820);
        for (int i = 0; i < 14; i++) {
            float t = i / 13f;
            Fill(new Rect(0, 0, shadeWidth * (1 - t * .12f), Screen.height),
                new Color(.025f, .018f, .07f, .075f * (1 - t)), null);
        }

        string nameError = string.IsNullOrWhiteSpace(playerName) ? "Hãy nhập tên để mọi người nhận ra bạn." : null;
        string serverError = ServerError(host, port);
        bool busy = !string.IsNullOrEmpty(status) && status.StartsWith("Đang");
        bool canConnect = nameError == null && serverError == null && !busy;

        // Read Enter and Tab before the text fields get the event: a focused TextField swallows both.
        Choice result = Choice.None;
        Event e = Event.current;
        if (GUI.enabled && e.type == EventType.KeyDown) {
            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) {
                if (canConnect) { BombAudio.UiClick(); result = Choice.Connect; }
                e.Use();
            } else if (e.keyCode == KeyCode.Tab || e.character == '\t') {
                if (e.keyCode == KeyCode.Tab) FocusNext(e.shift);
                e.Use();
            }
        }

        Color savedColor = GUI.color;
        GUI.color = WithAlpha(savedColor, appear);
        float s = Mathf.Clamp(Screen.height / 900f, .72f, 1.35f);
        float cardWidth = Mathf.Clamp(470 * s, 360, Mathf.Min(580, Screen.width - 48));
        float cardHeight = advanced ? 690 * s : 555 * s;
        cardHeight = Mathf.Min(cardHeight, Screen.height - 48);
        float slide = (1 - appear) * -40 * s;
        Rect card = new Rect(Mathf.Max(24, Screen.width * .055f) + slide, (Screen.height - cardHeight) * .5f, cardWidth, cardHeight);
        Fill(new Rect(card.x + 10, card.y + 12, card.width, card.height), new Color(0, 0, 0, .42f), RoundBig);
        Fill(card, new Color(Panel.r, Panel.g, Panel.b, .94f), RoundBig);
        Fill(new Rect(card.x, card.y, 7, card.height), Charge, null);

        float x = card.x + 40 * s;
        float w = card.width - 80 * s;
        float y = card.y + 34 * s;
        GUI.Label(new Rect(x, y, w, 64 * s), "BOMB ME BABY", logo);
        y += 66 * s;
        GUI.Label(new Rect(x, y, w, 58 * s), "Lăn vào đấu trường.\nTrở thành quả bom cuối cùng.", subtitle);
        y += 82 * s;

        GUI.Label(new Rect(x, y, w, 24 * s), "TÊN NGƯỜI CHƠI", label);
        y += 27 * s;
        GUI.SetNextControlName(FieldOrder[0]);
        playerName = TextField(new Rect(x, y, w, 52 * s), playerName ?? "", 20, field);
        y += 56 * s;
        if (nameError != null) GUI.Label(new Rect(x, y, w, 20 * s), nameError, error);
        y += 30 * s;

        if (advanced) {
            GUI.Label(new Rect(x, y, w, 24 * s), "ĐỊA CHỈ SERVER", label);
            y += 27 * s;
            GUI.SetNextControlName(FieldOrder[1]);
            host = TextField(new Rect(x, y, w * .7f - 6, 48 * s), host ?? "", -1, field);
            GUI.SetNextControlName(FieldOrder[2]);
            port = Digits(TextField(new Rect(x + w * .7f + 6, y, w * .3f - 6, 48 * s), port ?? "", 5, field));
            y += 58 * s;
            if (serverError != null) GUI.Label(new Rect(x, y, w, 38 * s), serverError, error);
            else GUI.Label(new Rect(x, y, w, 38 * s),
                "127.0.0.1 chỉ dùng khi server chạy trên chính máy này. Địa chỉ ngrok có dạng 0.tcp.ap.ngrok.io.", helper);
            y += 46 * s;
        } else if (serverError != null && nameError == null) {
            // Same row as the name message, which takes priority.
            GUI.Label(new Rect(x, y - 30 * s, w, 20 * s), "Địa chỉ server chưa hợp lệ, mở cài đặt mạng để sửa.", error);
        }

        Rect play = new Rect(x, y, w, 56 * s);
        string dots = new string('.', 1 + Mathf.FloorToInt(now * 2.5f) % 3);
        string playText = busy ? "ĐANG KẾT NỐI" + dots : "VÀO ĐẤU TRƯỜNG";
        if (Button(play, playText, Charge, Night, button, canConnect, "Kết nối tới server (Enter)")) result = Choice.Connect;
        y += 68 * s;
        string advancedText = advanced ? "Ẩn cài đặt mạng" : "Cài đặt mạng nâng cao";
        if (Button(new Rect(x, y, w, 44 * s), advancedText, PanelRaised, Text, secondary, !busy,
                "Chỉ cần khi chơi qua Internet hoặc server ở máy khác.")) advanced = !advanced;

        if (!string.IsNullOrEmpty(status)) {
            Rect banner = new Rect(x, card.yMax - 62 * s, w, 38 * s);
            Fill(banner, new Color(0, 0, 0, .28f), RoundSmall);
            float textX = banner.x + 12;
            if (busy) {
                float pulse = .5f + .5f * Mathf.Sin(now * 6);
                Fill(Centered(new Vector2(banner.x + 20, banner.center.y), 10 + 4 * pulse), WithAlpha(Charge, .5f + .5f * pulse), Circle);
                textX += 20;
            }
            GUI.Label(new Rect(textX, banner.y, banner.xMax - 12 - textX, banner.height), status, statusStyle);
        }
        GUI.color = savedColor;

        if (Button(new Rect(Screen.width - 126, 24, 102, 38), "THOÁT", PanelRaised, Text, secondary, true, "Đóng game"))
            result = Choice.Quit;
        GUI.Label(new Rect(Screen.width - 260, Screen.height - 34, 236, 22), "Bản demo • Unity 6", version);
        return result;
    }

    static string ServerError(string host, string port) {
        if (string.IsNullOrWhiteSpace(host)) return "Nhập địa chỉ server, ví dụ 127.0.0.1.";
        if (!int.TryParse(port, out int value)) return "Nhập cổng server, ví dụ 7777.";
        if (value < 1 || value > 65535) return "Cổng phải là số từ 1 đến 65535.";
        return null;
    }

    static string Digits(string raw) {
        var sb = new System.Text.StringBuilder();
        foreach (char c in raw ?? "") if (c >= '0' && c <= '9') sb.Append(c);
        return sb.ToString();
    }

    /// Tab / Shift+Tab cycles through the visible text fields.
    void FocusNext(bool backwards) {
        int count = advanced ? FieldOrder.Length : 1;
        int current = System.Array.IndexOf(FieldOrder, GUI.GetNameOfFocusedControl());
        int next = current < 0 ? 0 : (current + (backwards ? count - 1 : 1)) % count;
        GUI.FocusControl(FieldOrder[next]);
    }

    void EnsureStyles() {
        EnsureShapes();
        float s = Mathf.Clamp(Screen.height / 900f, .72f, 1.35f);
        if (field != null && Mathf.Approximately(styleScale, s)) return;
        styleScale = s;
        int Px(float value) { return Mathf.RoundToInt(value * s); }

        logo = TextStyle(Px(50), FontStyle.Bold, Text, TextAnchor.MiddleLeft);
        subtitle = TextStyle(Px(19), FontStyle.Normal, TextMuted, TextAnchor.UpperLeft);
        subtitle.wordWrap = true;
        label = TextStyle(Px(13), FontStyle.Bold, Charge, TextAnchor.MiddleLeft);
        helper = TextStyle(Px(12), FontStyle.Normal, TextMuted, TextAnchor.UpperLeft);
        helper.wordWrap = true;
        error = TextStyle(Px(13), FontStyle.Bold, Danger, TextAnchor.UpperLeft);
        error.wordWrap = true;
        button = TextStyle(Px(18), FontStyle.Bold, Night, TextAnchor.MiddleCenter);
        secondary = TextStyle(Px(14), FontStyle.Bold, Text, TextAnchor.MiddleCenter);
        statusStyle = TextStyle(Px(13), FontStyle.Normal, Text, TextAnchor.MiddleLeft);
        statusStyle.clipping = TextClipping.Clip;
        version = TextStyle(Px(12), FontStyle.Normal, new Color(1, 1, 1, .62f), TextAnchor.MiddleRight);

        field = new GUIStyle(GUI.skin.textField) {
            fontSize = Px(18), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(Px(16), Px(16), Px(8), Px(8)),
            border = new RectOffset(10, 10, 10, 10)
        };
        // Kenney input box (white with a grey rim) when the UI art is present.
        field.normal.background = field.hover.background = field.focused.background =
            InputBackground != null ? InputBackground : RoundSmall.normal.background;
        // Text fields use a light input surface, so their content must use
        // the dark ink colour instead of the cream text used on dark panels.
        field.normal.textColor = field.hover.textColor = field.active.textColor = Night;
        field.focused.textColor = field.onNormal.textColor = field.onHover.textColor = Night;
        field.onActive.textColor = field.onFocused.textColor = Night;
    }
}
