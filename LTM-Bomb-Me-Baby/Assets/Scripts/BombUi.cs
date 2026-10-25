using System.Collections.Generic;
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
    /// Progress bar drawn with the Kenney slider groove and fill. The neutral Grey set is tinted
    /// with the exact palette colour, so Core stays magenta instead of snapping to a Kenney set.
    /// `ghost` (0..1) draws a pale trailing segment behind the fill, e.g. HP just lost.
    public static void Bar(Rect rect, float value, Color color, float ghost = 0) {
        GUIStyle groove = Skin("Grey", "slide_horizontal_grey", 8, 8, 0, 0);
        GUIStyle fill = Skin("Grey", "slide_horizontal_color", 8, 8, 0, 0);
        if (groove == null || fill == null) {
            groove = fill = RoundSmall;
            Fill(rect, new Color(0, 0, 0, .45f), groove);
        } else Fill(rect, new Color(.32f, .28f, .42f), groove);
        if (ghost > value + .001f)
            Fill(new Rect(rect.x, rect.y, Mathf.Max(rect.height, rect.width * Mathf.Clamp01(ghost)), rect.height),
                WithAlpha(Text, .7f), fill);
        if (value > .001f)
            Fill(new Rect(rect.x, rect.y, Mathf.Max(rect.height, rect.width * Mathf.Clamp01(value)), rect.height), color, fill);
    }

    public static GUIStyle TextStyle(int size, FontStyle weight, Color color, TextAnchor anchor = TextAnchor.UpperLeft) {
        var style = new GUIStyle { fontSize = size, fontStyle = weight, alignment = anchor, richText = false };
        style.normal.textColor = color;
        return style;
    }

    static int hoveredButton;
    static float hoverStartedAt;
    static string pendingTooltip;
    static Rect pendingTooltipAnchor;
    static GUIStyle tooltipStyle;

    // Kenney UI Pack colour sets (Resources/Art/UI) and the face colour of each, sampled from the PNGs.
    static readonly string[] SkinSets = { "Yellow", "Red", "Green", "Blue", "Grey" };
    static readonly Color[] SkinFaces = {
        new Color32(255, 204, 0, 255), new Color32(238, 39, 71, 255), new Color32(22, 187, 119, 255),
        new Color32(28, 159, 215, 255), new Color32(218, 220, 231, 255)
    };
    const int GreySkin = 4;
    /// Height in pixels of the dark lip under a Kenney "depth" button; 9-slicing keeps it unscaled.
    const int SkinDepth = 4;
    static readonly Dictionary<string, GUIStyle> Skins = new Dictionary<string, GUIStyle>();

    /// A Kenney UI Pack image as a cached 9-sliced style (borders in texture pixels), drawn with
    /// Fill(rect, tint, style). Null when the art is missing, so callers keep their procedural look.
    public static GUIStyle Skin(string set, string image, int left, int right, int top, int bottom) {
        string key = set + "/" + image;
        if (Skins.TryGetValue(key, out GUIStyle style)) return style;
        Texture2D texture = BombArt.Ui(set, image);
        if (texture != null) {
            style = new GUIStyle { border = new RectOffset(left, right, top, bottom) };
            style.normal.background = texture;
        }
        Skins[key] = style;
        return style;
    }

    /// Kenney colour set closest to a palette colour: Charge→Yellow, Danger/Core→Red,
    /// Health/Energy→Green, Shield/Boost→Blue, dark or washed-out faces→Grey.
    public static string SkinSet(Color face) => SkinSets[SkinIndex(face)];

    static int SkinIndex(Color face) {
        Color.RGBToHSV(face, out float h, out float s, out float v);
        if (s < .35f || v < .45f) return GreySkin;
        if (h < .03f || h >= .8f) return 1;
        if (h < .2f) return 0;
        if (h < .45f) return 2;
        return 3;
    }

    /// Light tint toward the caller's colour so Charge reads orange rather than Kenney yellow.
    /// Grey carries the game's violet: dark faces stay dark so secondary buttons sit in the theme.
    static Color SkinTint(Color face, int skin) {
        if (skin != GreySkin) return Color.Lerp(Color.white, face, .25f);
        Color.RGBToHSV(face, out _, out _, out float v);
        return Color.Lerp(Color.white, face, v < .45f ? .7f : .3f);
    }

    static float Luma(Color c) => .2126f * c.r + .7152f * c.g + .0722f * c.b;

    /// Keeps the caller's ink when it stands out from the face, otherwise pushes it toward
    /// dark ink (light faces) or white (dark and saturated faces) until it does.
    static Color ReadableInk(Color ink, Color face) {
        float faceLuma = Luma(face);
        if (Mathf.Abs(Luma(ink) - faceLuma) >= .4f) return ink;
        Color target = faceLuma > .6f ? Night : Color.white;
        foreach (float t in new[] { .35f, .6f }) {
            Color candidate = Color.Lerp(ink, target, t);
            if (Mathf.Abs(Luma(candidate) - faceLuma) >= .4f) return candidate;
        }
        return target;
    }

    /// Chunky button shared by every screen, drawn with the Kenney UI Pack "depth" buttons:
    /// brightens on hover, sinks onto its lip while pressed, greys out when disabled, ticks on
    /// hover and clicks on press. Falls back to the procedural rounded button without the art.
    /// Returns true on click.
    public static bool Button(Rect rect, string text, Color face, Color ink, GUIStyle textStyle,
                              bool enabled = true, string tooltip = null) {
        int id = GUIUtility.GetControlID(FocusType.Passive);
        bool interactive = enabled && GUI.enabled;
        bool hover = rect.Contains(Event.current.mousePosition);
        TrackHover(id, hover && interactive, rect, tooltip);
        bool pressed = interactive && hover && Input.GetMouseButton(0);
        if (ButtonFace(rect, text, face, ink, textStyle, interactive, hover, pressed)) {
            bool skinnedClick = GUI.Button(rect, GUIContent.none, GUIStyle.none) && interactive;
            if (skinnedClick) BombAudio.UiClick();
            return skinnedClick;
        }
        float depth = Mathf.Clamp(rect.height * .1f, 3, 7);
        float lift = pressed ? -depth * .7f : hover && interactive ? depth * .35f : 0;
        if (!interactive) {
            face = Color.Lerp(face, PanelRaised, .7f);
            ink = WithAlpha(ink, .45f);
        }
        Fill(new Rect(rect.x, rect.y + depth, rect.width, rect.height), Color.Lerp(face, Color.black, .55f), RoundSmall);
        Rect top = new Rect(rect.x, rect.y - lift, rect.width, rect.height);
        Fill(top, hover && interactive ? Color.Lerp(face, Color.white, .1f) : face, RoundSmall);
        Color savedInk = textStyle.normal.textColor;
        textStyle.normal.textColor = ink;
        GUI.Label(top, text, textStyle);
        textStyle.normal.textColor = savedInk;
        bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none) && interactive;
        if (clicked) BombAudio.UiClick();
        return clicked;
    }

    /// Draws only the Kenney face of Button, for controls with their own hit area (e.g. a whole
    /// card that is clickable). False when the skin images are missing, so the caller draws its own.
    public static bool ButtonFace(Rect rect, string text, Color face, Color ink, GUIStyle textStyle,
                              bool interactive, bool hover, bool pressed) {
        int skin = interactive ? SkinIndex(face) : GreySkin;
        string set = SkinSets[skin];
        GUIStyle raised = Skin(set, "button_rectangle_depth_gradient", 10, 10, 8, 8 + SkinDepth);
        GUIStyle sunk = Skin(set, "button_rectangle_gradient", 10, 10, 8, 8);
        if (raised == null || sunk == null) return false;
        EnsureShapes();

        Color tint = interactive ? SkinTint(face, skin) : Color.Lerp(Color.white, PanelRaised, .45f);
        if (hover && interactive && !pressed) tint = Color.Lerp(tint, Color.white, .3f);
        Color faceColor = SkinFaces[skin] * tint;
        ink = ReadableInk(ink, faceColor);
        if (!interactive) ink = WithAlpha(ink, .45f);

        // Pressed: the flat variant sits where the raised face would be after dropping onto its lip.
        Rect top = pressed ? new Rect(rect.x, rect.y + SkinDepth, rect.width, rect.height - SkinDepth)
                           : new Rect(rect.x, rect.y, rect.width, rect.height - SkinDepth);
        Fill(new Rect(rect.x + 2, rect.y + 4, rect.width, rect.height), new Color(0, 0, 0, .28f), RoundSmall);
        Fill(pressed ? top : rect, tint, pressed ? sunk : raised);
        Color savedInk = textStyle.normal.textColor;
        textStyle.normal.textColor = ink;
        GUI.Label(top, text, textStyle);
        textStyle.normal.textColor = savedInk;
        return true;
    }

    /// Thin Kenney divider rule (e.g. under a card title), tinted; nothing without the art.
    public static void Divider(Rect rect, Color tint) {
        GUIStyle divider = Skin("Extra", "divider_edges", 2, 2, 0, 0);
        if (divider != null) Fill(rect, tint, divider);
    }

    /// Kenney text-field background (white with a grey rim), or null without the art.
    public static Texture2D InputBackground => BombArt.Ui("Extra", "input_rectangle");

    /// Plays the hover tick once when the pointer enters a control and remembers its tooltip.
    /// Call from custom controls that do not go through Button.
    public static void TrackHover(int id, bool hover, Rect rect, string tooltip = null) {
        if (Event.current.type != EventType.Repaint) return;
        hover &= GUI.enabled;
        if (hover) {
            if (hoveredButton != id) {
                hoveredButton = id;
                hoverStartedAt = Time.unscaledTime;
                BombAudio.UiHover();
            }
            if (!string.IsNullOrEmpty(tooltip)) {
                pendingTooltip = tooltip;
                pendingTooltipAnchor = GUIUtility.GUIToScreenRect(rect);
            }
        } else if (hoveredButton == id) hoveredButton = 0;
    }

    /// Draws the tooltip of the control under the pointer after a short delay.
    /// Call once at the very end of OnGUI so it sits above every screen.
    public static void DrawTooltip() {
        string text = pendingTooltip;
        Rect anchor = pendingTooltipAnchor;
        if (Event.current.type == EventType.Repaint) pendingTooltip = null;
        if (string.IsNullOrEmpty(text) || hoveredButton == 0 || Time.unscaledTime - hoverStartedAt < .45f) return;
        if (tooltipStyle == null) {
            tooltipStyle = TextStyle(14, FontStyle.Normal, Text, TextAnchor.MiddleLeft);
            tooltipStyle.wordWrap = true;
            tooltipStyle.padding = new RectOffset(12, 12, 8, 8);
        }
        var content = new GUIContent(text);
        float width = Mathf.Min(320, tooltipStyle.CalcSize(content).x);
        float height = tooltipStyle.CalcHeight(content, width);
        float x = Mathf.Clamp(anchor.center.x - width / 2, 8, Screen.width - width - 8);
        float y = anchor.yMax + 10;
        if (y + height > Screen.height - 8) y = anchor.y - height - 10;
        Rect box = new Rect(x, y, width, height);
        Fill(new Rect(box.x + 3, box.y + 4, box.width, box.height), new Color(0, 0, 0, .45f), RoundSmall);
        Fill(box, new Color(Panel.r, Panel.g, Panel.b, .97f), RoundSmall);
        GUI.Label(box, content, tooltipStyle);
    }

    /// Raised card with a soft drop shadow and an accent strip along the top edge.
    public static void Card(Rect rect, Color accent, float unit = 1) {
        Fill(new Rect(rect.x + 7 * unit, rect.y + 9 * unit, rect.width, rect.height), new Color(0, 0, 0, .5f), RoundBig);
        Fill(rect, PanelRaised, RoundBig);
        Fill(new Rect(rect.x + 28 * unit, rect.y, rect.width - 56 * unit, 5 * unit), accent, null);
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
