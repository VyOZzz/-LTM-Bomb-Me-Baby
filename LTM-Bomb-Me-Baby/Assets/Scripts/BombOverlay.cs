using UnityEngine;
using static BombUi;

/// Esc pause menu, settings and leave confirmation, drawn over the menu, lobby or match.
/// Multiplayer keeps running underneath; the overlay only blocks this client's input.
sealed class BombOverlay {
    public enum Page { None, Pause, Settings, ConfirmLeave }
    public enum Action { None, LeaveRoom, QuitGame }

    Page page;
    Page settingsBack;
    float u = 1, stylesScale;
    GUIStyle title, subtitle, buttonText, label, valueText, valueRight, note;

    public bool IsOpen => page != Page.None;

    public void OpenPause() => page = Page.Pause;

    public void OpenSettings() {
        settingsBack = page;
        page = Page.Settings;
    }

    /// Esc: step back one level, closing the overlay from its first page.
    public void Back() {
        if (page == Page.Settings) {
            BombSettings.Save();
            page = settingsBack;
        } else if (page == Page.ConfirmLeave) page = Page.Pause;
        else page = Page.None;
    }

    float S(float v) => v * u;
    int Px(int size) => Mathf.RoundToInt(size * u);

    public Action Draw(bool inRoom, bool inMatch) {
        if (page == Page.None) return Action.None;
        u = Mathf.Clamp(Screen.height / 1080f, .75f, 1.6f);
        EnsureStyles();
        Fill(new Rect(0, 0, Screen.width, Screen.height), WithAlpha(Night, .72f), null);
        switch (page) {
            case Page.Pause: return DrawPause(inRoom, inMatch);
            case Page.Settings: DrawSettings(); return Action.None;
            case Page.ConfirmLeave: return DrawConfirm();
        }
        return Action.None;
    }

    Action DrawPause(bool inRoom, bool inMatch) {
        Rect card = Card(420, inRoom ? 420 : 340, Charge);
        GUI.Label(Row(card, 26, 48), "TẠM DỪNG", title);
        GUI.Label(Row(card, 72, 24), inMatch ? "Trận vẫn tiếp diễn với những người chơi khác" : "Nhấn Esc để quay lại", subtitle);
        float y = 116;
        if (Button(Row(card, y, 54, 28), "TIẾP TỤC", Charge, Night)) page = Page.None;
        y += 70;
        if (Button(Row(card, y, 54, 28), "CÀI ĐẶT", PanelRaised, Text)) OpenSettings();
        y += 70;
        if (inRoom) {
            if (Button(Row(card, y, 54, 28), "VỀ MENU CHÍNH", PanelRaised, Text)) {
                if (inMatch) page = Page.ConfirmLeave;
                else { page = Page.None; return Action.LeaveRoom; }
            }
            y += 70;
        }
        if (Button(Row(card, y, 54, 28), "THOÁT GAME", PanelRaised, Danger)) return Action.QuitGame;
        return Action.None;
    }

    Action DrawConfirm() {
        Rect card = Card(460, 250, Danger);
        GUI.Label(Row(card, 26, 44), "RỜI TRẬN?", title);
        GUI.Label(Row(card, 74, 50, 32), "Bạn sẽ bị loại khỏi trận này và quay về menu chính.", subtitle);
        float half = (card.width - S(56) - S(16)) / 2;
        Rect stay = new Rect(card.x + S(28), card.y + S(160), half, S(54));
        Rect leave = new Rect(stay.xMax + S(16), stay.y, half, S(54));
        if (Button(stay, "Ở LẠI", PanelRaised, Text)) page = Page.Pause;
        if (Button(leave, "RỜI TRẬN", Danger, Night)) { page = Page.None; return Action.LeaveRoom; }
        return Action.None;
    }

    void DrawSettings() {
        Rect card = Card(500, 580, Shield);
        GUI.Label(Row(card, 26, 48), "CÀI ĐẶT", title);
        float y = 92;
        GUI.Label(Row(card, y, 22), "ÂM THANH", label);
        y += 32;
        BombSettings.Master = Slider(card, ref y, "Âm lượng tổng", BombSettings.Master, Charge);
        BombSettings.Music = Slider(card, ref y, "Nhạc", BombSettings.Music, Core);
        BombSettings.Sfx = Slider(card, ref y, "Hiệu ứng", BombSettings.Sfx, Shield);
        AudioListener.volume = BombSettings.Master;
        GUI.Label(Row(card, y - 6, 22), "Nhạc và hiệu ứng áp dụng khi game có file âm thanh.", note);
        y += 34;
        GUI.Label(Row(card, y, 22), "HIỂN THỊ", label);
        y += 32;
        BombSettings.Fullscreen = Toggle(card, ref y, "Toàn màn hình", BombSettings.Fullscreen);
        BombSettings.ShowNetwork = Toggle(card, ref y, "Hiện số liệu mạng (F3)", BombSettings.ShowNetwork);
        if (Button(Row(card, 496, 54, 28), "XONG", Shield, Night)) Back();
    }

    float Slider(Rect card, ref float y, string name, float value, Color color) {
        GUI.Label(Row(card, y, 22), name, valueText);
        GUI.Label(Row(card, y, 22), $"{value * 100:0}%", valueRight);
        Rect track = Row(card, y + 30, 12);
        Rect hit = new Rect(track.x - S(10), track.y - S(12), track.width + S(20), track.height + S(24));
        int id = GUIUtility.GetControlID(FocusType.Passive);
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
        Bar(track, value, color);
        Vector2 thumb = new Vector2(track.x + track.width * value, track.center.y);
        Fill(Centered(thumb, S(24)), Text, Circle);
        Fill(Centered(thumb, S(12)), color, Circle);
        y += 64;
        return value;
    }

    static float ValueAt(Rect track, Event e) => Mathf.Clamp01((e.mousePosition.x - track.x) / track.width);

    bool Toggle(Rect card, ref float y, string name, bool value) {
        Rect row = Row(card, y, 36);
        GUI.Label(row, name, valueText);
        Rect knobTrack = new Rect(row.xMax - S(64), row.y + S(4), S(64), S(28));
        Fill(knobTrack, value ? Health : PanelRaised, RoundSmall);
        Fill(Centered(new Vector2(value ? knobTrack.xMax - S(14) : knobTrack.x + S(14), knobTrack.center.y), S(22)), Text, Circle);
        if (GUI.Button(row, GUIContent.none, GUIStyle.none)) {
            BombAudio.UiClick();
            value = !value;
        }
        y += 50;
        return value;
    }

    /// Chunky button with a hard drop shadow; sinks while pressed.
    bool Button(Rect rect, string text, Color face, Color textColor) {
        bool pressed = GUI.enabled && rect.Contains(Event.current.mousePosition) && Input.GetMouseButton(0);
        bool hover = rect.Contains(Event.current.mousePosition);
        float push = pressed ? S(4) : 0;
        Fill(new Rect(rect.x, rect.y + S(5), rect.width, rect.height), Color.Lerp(face, Color.black, .5f), RoundSmall);
        Rect top = new Rect(rect.x, rect.y + push, rect.width, rect.height);
        Fill(top, hover ? Color.Lerp(face, Color.white, .08f) : face, RoundSmall);
        buttonText.normal.textColor = textColor;
        GUI.Label(top, text, buttonText);
        bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
        if (clicked) BombAudio.UiClick();
        return clicked;
    }

    Rect Card(float width, float height, Color accent) {
        Rect card = new Rect(Screen.width / 2f - S(width) / 2, Screen.height / 2f - S(height) / 2, S(width), S(height));
        Fill(new Rect(card.x + S(7), card.y + S(9), card.width, card.height), new Color(0, 0, 0, .5f), RoundBig);
        Fill(card, PanelRaised, RoundBig);
        Fill(new Rect(card.x + S(28), card.y, card.width - S(56), S(5)), accent, null);
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
    }
}
