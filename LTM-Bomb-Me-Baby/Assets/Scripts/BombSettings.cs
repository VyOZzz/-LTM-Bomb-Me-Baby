using UnityEngine;

/// Player preferences persisted with PlayerPrefs. Music and Sfx are read live by
/// BombAudio; Master is applied to the AudioListener. ShowControls is read by the HUD
/// to decide whether the in-match controls hint is drawn.
static class BombSettings {
    /// Frame-rate caps offered in settings; -1 means uncapped.
    public static readonly int[] FpsOptions = { 30, 60, 120, 144, -1 };

    public static float Master = 1, Music = .8f, Sfx = 1;
    public static bool Fullscreen = true, ShowNetwork, VSync = true, ShowControls = true;
    public static int TargetFps = 60;

    public static void Load() {
        Master = PlayerPrefs.GetFloat("audio.master", Master);
        Music = PlayerPrefs.GetFloat("audio.music", Music);
        Sfx = PlayerPrefs.GetFloat("audio.sfx", Sfx);
        Fullscreen = PlayerPrefs.GetInt("display.fullscreen", Screen.fullScreen ? 1 : 0) == 1;
        ShowNetwork = PlayerPrefs.GetInt("display.network", 0) == 1;
        VSync = PlayerPrefs.GetInt("display.vsync", QualitySettings.vSyncCount > 0 ? 1 : 0) == 1;
        TargetFps = PlayerPrefs.GetInt("display.fps", TargetFps);
        if (System.Array.IndexOf(FpsOptions, TargetFps) < 0) TargetFps = 60;
        ShowControls = PlayerPrefs.GetInt("display.controls", 1) == 1;
        Apply();
    }

    public static void Save() {
        PlayerPrefs.SetFloat("audio.master", Master);
        PlayerPrefs.SetFloat("audio.music", Music);
        PlayerPrefs.SetFloat("audio.sfx", Sfx);
        PlayerPrefs.SetInt("display.fullscreen", Fullscreen ? 1 : 0);
        PlayerPrefs.SetInt("display.network", ShowNetwork ? 1 : 0);
        PlayerPrefs.SetInt("display.vsync", VSync ? 1 : 0);
        PlayerPrefs.SetInt("display.fps", TargetFps);
        PlayerPrefs.SetInt("display.controls", ShowControls ? 1 : 0);
        PlayerPrefs.Save();
        Apply();
    }

    /// Restores every option to its first-launch value and saves it.
    public static void ResetDefaults() {
        Master = 1; Music = .8f; Sfx = 1;
        Fullscreen = true; ShowNetwork = false; VSync = true; ShowControls = true;
        TargetFps = 60;
        Save();
    }

    /// Pushes the current values to Unity; cheap enough to call on every change.
    public static void Apply() {
        AudioListener.volume = Master;
        if (Screen.fullScreen != Fullscreen) Screen.fullScreen = Fullscreen;
        QualitySettings.vSyncCount = VSync ? 1 : 0;
        // The frame cap is ignored by Unity while VSync is on.
        Application.targetFrameRate = TargetFps;
    }

    public static string FpsLabel(int fps) => fps < 0 ? "Không giới hạn" : fps + " FPS";
}
