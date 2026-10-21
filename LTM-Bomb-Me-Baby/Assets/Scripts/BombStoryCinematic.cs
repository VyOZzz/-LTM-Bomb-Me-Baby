using System;
using System.IO;
using UnityEngine;
using UnityEngine.Video;
using static BombUi;

/// Plays the Journey intro as a real MP4 from StreamingAssets. If the codec or
/// file is unavailable, it falls back to the same three illustrated frames.
public sealed class BombStoryCinematic : MonoBehaviour {
    const float Duration = 16f;
    readonly string[] titles = {
        "TRÁI TIM LỬA",
        "BA MẢNH THẤT LẠC",
        "HÀNH TRÌNH BẮT ĐẦU"
    };
    readonly string[] narration = {
        "Thuở xa xưa, Tim Lửa giữ cho các đấu trường luôn rực sáng và bình yên.",
        "Một cơn bão ma thuật đã xé trái tim thành ba mảnh, cuốn chúng tới Đồng Cỏ, Ngã Tư và Miệng Núi Lửa.",
        "Hãy vượt qua ba chương, tranh lấy từng mảnh và đưa chúng trở về bệ đá trước khi ngọn lửa tắt hẳn."
    };

    VideoPlayer player;
    RenderTexture videoTexture;
    Texture2D[] fallbackFrames;
    Action finished;
    float startedAt;
    bool fallback, pendingFinish;
    GUIStyle chapterStyle, titleStyle, narrationStyle, skipStyle;

    public bool IsPlaying { get; private set; }
    public bool HasPlayedThisSession { get; private set; }

    void Awake() {
        player = gameObject.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.isLooping = false;
        player.renderMode = VideoRenderMode.RenderTexture;
        player.audioOutputMode = VideoAudioOutputMode.Direct;
        player.prepareCompleted += Prepared;
        player.loopPointReached += Ended;
        player.errorReceived += Failed;
        fallbackFrames = new[] {
            Resources.Load<Texture2D>("Menu/story_01"),
            Resources.Load<Texture2D>("Menu/story_02"),
            Resources.Load<Texture2D>("Menu/story_03")
        };
    }

    public void Play(Action onFinished) {
        if (HasPlayedThisSession) { onFinished?.Invoke(); return; }
        finished = onFinished;
        IsPlaying = true;
        pendingFinish = false;
        fallback = false;
        startedAt = Time.unscaledTime;

        if (videoTexture == null) {
            videoTexture = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32) {
                name = "Journey Intro Cinematic", filterMode = FilterMode.Bilinear
            };
            videoTexture.Create();
        }
        player.targetTexture = videoTexture;
        string path = Path.Combine(Application.streamingAssetsPath, "story_intro.mp4");
        if (!File.Exists(path)) { fallback = true; return; }
        player.url = path.Replace('\\', '/');
        player.Prepare();
    }

    void Prepared(VideoPlayer source) {
        if (!IsPlaying) return;
        startedAt = Time.unscaledTime;
        source.Play();
    }

    void Ended(VideoPlayer source) { if (IsPlaying) pendingFinish = true; }
    void Failed(VideoPlayer source, string message) {
        Debug.LogWarning("Không phát được story_intro.mp4, dùng cinematic ảnh: " + message);
        fallback = true;
        startedAt = Time.unscaledTime;
    }

    void Update() {
        if (!IsPlaying) return;
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space)) pendingFinish = true;
        if (!fallback && !player.isPrepared && Time.unscaledTime - startedAt > 4f) {
            fallback = true;
            startedAt = Time.unscaledTime;
        }
        if (fallback && Time.unscaledTime - startedAt >= Duration) pendingFinish = true;
        if (pendingFinish) Finish();
    }

    public void Draw() {
        if (!IsPlaying) return;
        EnsureStyles();
        float time = CurrentTime();
        int scene = time < 5.2f ? 0 : time < 10.4f ? 1 : 2;
        Texture image = fallback ? fallbackFrames[scene] : videoTexture;
        if (image != null) GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), image, ScaleMode.ScaleAndCrop, false);
        else Fill(new Rect(0, 0, Screen.width, Screen.height), Night, null);

        Fill(new Rect(0, 0, Screen.width, 110), new Color(0, 0, 0, .34f), null);
        Fill(new Rect(0, Screen.height - 245, Screen.width, 245), new Color(.015f, .01f, .035f, .9f), null);
        GUI.Label(new Rect(42, 25, Screen.width - 260, 34), "HÀNH TRÌNH BA MẢNH  •  PHIM MỞ ĐẦU", chapterStyle);
        GUI.Label(new Rect(54, Screen.height - 202, Screen.width - 108, 52), titles[scene], titleStyle);
        GUI.Label(new Rect(56, Screen.height - 145, Mathf.Min(980, Screen.width - 112), 74), narration[scene], narrationStyle);

        Rect skip = new Rect(Screen.width - 190, 26, 148, 46);
        Fill(skip, new Color(0, 0, 0, .52f), RoundSmall);
        GUI.Label(skip, "BỎ QUA  ESC", skipStyle);
        if (GUI.Button(skip, GUIContent.none, GUIStyle.none)) pendingFinish = true;

        float progress = Mathf.Clamp01(time / Duration);
        Fill(new Rect(0, Screen.height - 5, Screen.width, 5), new Color(1, 1, 1, .18f), null);
        Fill(new Rect(0, Screen.height - 5, Screen.width * progress, 5), Charge, null);
    }

    float CurrentTime() {
        if (!fallback && player != null && player.isPlaying) return Mathf.Clamp((float)player.time, 0, Duration);
        return Mathf.Clamp(Time.unscaledTime - startedAt, 0, Duration);
    }

    void Finish() {
        pendingFinish = false;
        IsPlaying = false;
        HasPlayedThisSession = true;
        if (player != null) player.Stop();
        Action callback = finished;
        finished = null;
        callback?.Invoke();
    }

    void EnsureStyles() {
        EnsureShapes();
        if (chapterStyle != null) return;
        float s = Mathf.Clamp(Screen.height / 900f, .75f, 1.35f);
        chapterStyle = TextStyle(Mathf.RoundToInt(14 * s), FontStyle.Bold, Charge, TextAnchor.MiddleLeft);
        titleStyle = TextStyle(Mathf.RoundToInt(34 * s), FontStyle.Bold, Text, TextAnchor.MiddleLeft);
        narrationStyle = TextStyle(Mathf.RoundToInt(20 * s), FontStyle.Normal, Text, TextAnchor.UpperLeft);
        narrationStyle.wordWrap = true;
        skipStyle = TextStyle(Mathf.RoundToInt(13 * s), FontStyle.Bold, Text, TextAnchor.MiddleCenter);
    }

    void OnDestroy() {
        if (player != null) {
            player.prepareCompleted -= Prepared;
            player.loopPointReached -= Ended;
            player.errorReceived -= Failed;
        }
        if (videoTexture != null) videoTexture.Release();
    }
}
