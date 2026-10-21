using UnityEngine;

/// Palette and IMGUI drawing helpers shared by the menu, lobby and in-match overlay.
/// Colors follow docs/concepts: twilight violet world, warm stone paths, black toy bombs,
/// orange charge, cyan shield, blue boost, lime Energy, magenta Core.
static class BombUi {
    public static readonly Color Night = new Color(.13f, .1f, .2f);
    public static readonly Color Panel = new Color(.09f, .08f, .15f, .88f);
    public static readonly Color PanelRaised = new Color(.17f, .14f, .26f);
    public static readonly Color Text = new Color(1f, .96f, .9f);
    public static readonly Color TextMuted = new Color(.74f, .69f, .8f);
    public static readonly Color TextFaint = new Color(.55f, .5f, .63f);

    public static readonly Color Charge = new Color(1f, .55f, .12f);
    public static readonly Color Shield = new Color(.25f, .85f, 1f);
    public static readonly Color Boost = new Color(.27f, .5f, 1f);
    public static readonly Color Energy = new Color(.55f, .95f, .2f);
    public static readonly Color Core = new Color(1f, .27f, .86f);
    public static readonly Color Health = new Color(.4f, .86f, .32f);
    public static readonly Color Danger = new Color(1f, .33f, .3f);

    public static readonly Color BombBody = new Color(.1f, .09f, .12f);

    /// Accent per map, used for UI chips and as the base of each arena's palette.
    public static Color MapAccent(string mapId) {
        switch (mapId) {
            case "crossroads": return new Color(.66f, .55f, .98f);
            case "crater": return new Color(1f, .5f, .35f);
            default: return new Color(.42f, .84f, .55f);
        }
    }

    public static GUIStyle RoundBig, RoundSmall, Circle, Ring;

    public static void EnsureShapes() {
        if (RoundBig != null) return;
        RoundBig = Shape(RoundedTexture(64, 20), 20);
        RoundSmall = Shape(RoundedTexture(32, 10), 10);
        Circle = Shape(CircleTexture(128, 0), 0);
        Ring = Shape(CircleTexture(128, 6), 0);
    }

    public static void Fill(Rect rect, Color color, GUIStyle shape) {
        if (Event.current.type != EventType.Repaint) return;
        Color saved = GUI.color;
        GUI.color = saved * color;
        if (shape == null) GUI.DrawTexture(rect, Texture2D.whiteTexture);
        else shape.Draw(rect, false, false, false, false);
        GUI.color = saved;
    }

    /// Text field whose blinking caret uses the field's own ink colour. IMGUI draws the caret
    /// with one skin-wide colour that is nearly white, so it vanished on our light inputs.
    public static string TextField(Rect rect, string text, int maxLength, GUIStyle style) {
        GUISettings settings = GUI.skin.settings;
        Color savedCursor = settings.cursorColor;
        float savedFlash = settings.cursorFlashSpeed;
        settings.cursorColor = style.focused.textColor;
        settings.cursorFlashSpeed = 1.6f;
        string result = GUI.TextField(rect, text, maxLength, style);
        settings.cursorColor = savedCursor;
        settings.cursorFlashSpeed = savedFlash;
        return result;
    }

    /// Rounded progress bar with a darker track; value is clamped to 0..1.
    public static void Bar(Rect rect, float value, Color color) {
        Fill(rect, new Color(0, 0, 0, .45f), RoundSmall);
        float width = Mathf.Max(rect.height, rect.width * Mathf.Clamp01(value));
        if (value > .001f) Fill(new Rect(rect.x, rect.y, width, rect.height), color, RoundSmall);
    }

    public static GUIStyle TextStyle(int size, FontStyle weight, Color color, TextAnchor anchor = TextAnchor.UpperLeft) {
        var style = new GUIStyle { fontSize = size, fontStyle = weight, alignment = anchor, richText = false };
        style.normal.textColor = color;
        return style;
    }

    public static Rect Centered(Vector2 center, float size) => new Rect(center.x - size / 2, center.y - size / 2, size, size);

    public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, c.a * a);

    static GUIStyle Shape(Texture2D texture, int border) {
        var style = new GUIStyle { border = new RectOffset(border, border, border, border) };
        style.normal.background = texture;
        return style;
    }

    static Texture2D NewTexture(int size) => new Texture2D(size, size, TextureFormat.RGBA32, false) {
        wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave
    };

    static Texture2D RoundedTexture(int size, int radius) {
        Texture2D texture = NewTexture(size);
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++) {
                float cx = Mathf.Clamp(x + .5f, radius, size - radius);
                float cy = Mathf.Clamp(y + .5f, radius, size - radius);
                float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(cx, cy));
                pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(radius - d + .5f));
            }
        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }

    /// thickness 0 gives a filled disc, otherwise a ring of that many pixels.
    static Texture2D CircleTexture(int size, int thickness) {
        Texture2D texture = NewTexture(size);
        var pixels = new Color32[size * size];
        float r = size / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++) {
                float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(r, r));
                float a = Mathf.Clamp01(r - d - .5f);
                if (thickness > 0) a *= Mathf.Clamp01(d - (r - thickness) + .5f);
                pixels[y * size + x] = new Color(1, 1, 1, a);
            }
        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }
}
