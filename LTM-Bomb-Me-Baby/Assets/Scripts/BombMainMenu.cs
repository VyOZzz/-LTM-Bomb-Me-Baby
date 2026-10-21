using UnityEngine;
using static BombUi;

/// Full-screen entry menu shown before connecting to the server.
/// The network endpoint stays behind an advanced disclosure so the first thing
/// players see is the game, not a debugging form.
sealed class BombMainMenu {
    public enum Choice { None, Connect, Quit }

    Texture2D background;
    bool advanced;
    float styleScale;
    GUIStyle logo, subtitle, label, field, button, secondary, helper, statusStyle, version;

    public void ShowNetworkSettings() { advanced = true; }

    public Choice Draw(ref string playerName, ref string host, ref string port, string status) {
        EnsureStyles();
        if (background == null) background = Resources.Load<Texture2D>("Menu/menu_background");

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

        float s = Mathf.Clamp(Screen.height / 900f, .72f, 1.35f);
        float cardWidth = Mathf.Clamp(470 * s, 360, Mathf.Min(580, Screen.width - 48));
        float cardHeight = advanced ? 650 * s : 535 * s;
        cardHeight = Mathf.Min(cardHeight, Screen.height - 48);
        Rect card = new Rect(Mathf.Max(24, Screen.width * .055f), (Screen.height - cardHeight) * .5f, cardWidth, cardHeight);
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
        playerName = TextField(new Rect(x, y, w, 52 * s), playerName ?? "", 20, field);
        y += 66 * s;

        if (advanced) {
            GUI.Label(new Rect(x, y, w, 24 * s), "ĐỊA CHỈ SERVER", label);
            y += 27 * s;
            host = TextField(new Rect(x, y, w * .7f - 6, 48 * s), host ?? "", -1, field);
            port = TextField(new Rect(x + w * .7f + 6, y, w * .3f - 6, 48 * s), port ?? "", 5, field);
            y += 58 * s;
            GUI.Label(new Rect(x, y, w, 38 * s),
                "127.0.0.1 chỉ dùng khi server chạy trên chính máy này.", helper);
            y += 46 * s;
        }

        Choice result = Choice.None;
        Rect play = new Rect(x, y, w, 56 * s);
        if (ActionButton(play, "VÀO ĐẤU TRƯỜNG", Charge, Night, button)) result = Choice.Connect;
        y += 68 * s;
        string advancedText = advanced ? "Ẩn cài đặt mạng" : "Cài đặt mạng nâng cao";
        if (ActionButton(new Rect(x, y, w, 44 * s), advancedText, PanelRaised, Text, secondary)) advanced = !advanced;

        if (!string.IsNullOrEmpty(status)) {
            Rect banner = new Rect(x, card.yMax - 62 * s, w, 38 * s);
            Fill(banner, new Color(0, 0, 0, .28f), RoundSmall);
            GUI.Label(new Rect(banner.x + 12, banner.y, banner.width - 24, banner.height), status, statusStyle);
        }

        if (GUI.Button(new Rect(Screen.width - 126, 24, 102, 38), "THOÁT", secondary)) {
            BombAudio.UiClick();
            result = Choice.Quit;
        }
        GUI.Label(new Rect(Screen.width - 260, Screen.height - 34, 236, 22), "Bản demo • Unity 6", version);
        return result;
    }

    static bool ActionButton(Rect rect, string text, Color face, Color textColor, GUIStyle style) {
        bool pressed = rect.Contains(Event.current.mousePosition) && Input.GetMouseButton(0);
        float push = pressed ? 4 : 0;
        Fill(new Rect(rect.x, rect.y + 6, rect.width, rect.height), Color.Lerp(face, Color.black, .55f), RoundSmall);
        Fill(new Rect(rect.x, rect.y + push, rect.width, rect.height), face, RoundSmall);
        style.normal.textColor = textColor;
        GUI.Label(new Rect(rect.x, rect.y + push, rect.width, rect.height), text, style);
        bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
        if (clicked) BombAudio.UiClick();
        return clicked;
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
        field.normal.background = field.hover.background = field.focused.background = RoundSmall.normal.background;
        // Text fields use Unity's light input surface, so their content must use
        // the dark ink colour instead of the cream text used on dark panels.
        field.normal.textColor = field.hover.textColor = field.active.textColor = Night;
        field.focused.textColor = field.onNormal.textColor = field.onHover.textColor = Night;
        field.onActive.textColor = field.onFocused.textColor = Night;
    }
}
