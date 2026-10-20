using UnityEngine;

/// Player preferences, persisted with PlayerPrefs. Music and Sfx are read by the
/// audio code when clips are added; Master is applied to the AudioListener now.
static class BombSettings {
    public static float Master = 1, Music = .8f, Sfx = 1;
    public static bool Fullscreen = true, ShowNetwork;

    public static void Load() {
        Master = PlayerPrefs.GetFloat("audio.master", Master);
        Music = PlayerPrefs.GetFloat("audio.music", Music);
        Sfx = PlayerPrefs.GetFloat("audio.sfx", Sfx);
        Fullscreen = PlayerPrefs.GetInt("display.fullscreen", Screen.fullScreen ? 1 : 0) == 1;
        ShowNetwork = PlayerPrefs.GetInt("display.network", 0) == 1;
        Apply();
    }

    public static void Save() {
        PlayerPrefs.SetFloat("audio.master", Master);
        PlayerPrefs.SetFloat("audio.music", Music);
        PlayerPrefs.SetFloat("audio.sfx", Sfx);
        PlayerPrefs.SetInt("display.fullscreen", Fullscreen ? 1 : 0);
        PlayerPrefs.SetInt("display.network", ShowNetwork ? 1 : 0);
        PlayerPrefs.Save();
        Apply();
    }

    static void Apply() {
        AudioListener.volume = Master;
        if (Screen.fullScreen != Fullscreen) Screen.fullScreen = Fullscreen;
    }
}
