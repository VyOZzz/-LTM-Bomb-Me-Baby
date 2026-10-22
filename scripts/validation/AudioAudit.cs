// Copy this file to Assets/Editor in an isolated audio validation project.
// Requires BombAudio.cs, BombSettings.cs and the 23 clips loaded by BombAudio.
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class AudioAudit {
    static int checks;
    static void Check(bool condition, string message) {
        if (!condition) throw new Exception(message);
        checks++;
    }
    static object Field(BombAudio audio, string name) => typeof(BombAudio)
        .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(audio);
    static object Call(BombAudio audio, string name, params object[] args) => typeof(BombAudio)
        .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(audio, args);
    public static void Run() {
        try {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AudioClip[] clips = Resources.LoadAll<AudioClip>("Audio");
            Check(clips.Length == 23, "Expected 23 active downloaded/user clips, got " + clips.Length);
            foreach (AudioClip clip in clips) {
                Check(clip.length > .005f && clip.samples > 0, "Invalid imported audio: " + clip.name);
                Check(clip.frequency == 44100 || clip.name == "music_menu", "Unexpected sample rate");
                Check(!clip.name.StartsWith("sfx_"), "Generated audio is still active");
                if (clip.name.StartsWith("music_"))
                    Check(clip.loadType == AudioClipLoadType.Streaming, "Music must stream: " + clip.name);
            }
            var go = new GameObject("Audio validation");
            go.AddComponent<AudioListener>();
            var audio = go.AddComponent<BombAudio>();
            if (BombAudio.Instance == null) Call(audio, "Awake");
            Check(BombAudio.Instance == audio, "Audio singleton did not initialize");
            Check(((AudioSource[])Field(audio, "music")).Length == 2, "Crossfade requires two sources");
            var settings = typeof(BombAudio).Assembly.GetType("BombSettings");
            var sfx = settings.GetField("Sfx", BindingFlags.Public | BindingFlags.Static);
            foreach (string map in new[] { "meadow", "crossroads", "crater" }) {
                audio.SetMapMusic(map);
                Check(((AudioClip)Field(audio, "desiredMusic")).name == "music_" + map, "Incorrect map music");
            }
            audio.SetMenuMusic();
            Check(((AudioClip)Field(audio, "desiredMusic")).name == "music_menu", "Lobby track changed");
            audio.SetListeningPoint(Vector3.zero, Vector3.right);
            object[] near = { false, new Vector3(1, 40, 0), 25f, 0f };
            object[] far = { false, new Vector3(30, 0, 0), 25f, 0f };
            object[] right = { false, new Vector3(6, 0, 0), 25f, 0f };
            object[] left = { false, new Vector3(-6, 0, 0), 25f, 0f };
            Check((float)Call(audio, "Audibility", near) > .6f, "Camera height attenuates nearby audio");
            Check((float)Call(audio, "Audibility", far) == 0, "Distant audio is not culled");
            Call(audio, "Audibility", right); Call(audio, "Audibility", left);
            Check((float)right[3] > 0 && (float)left[3] < 0, "Stereo direction is reversed");
            audio.PlayShield(true, Vector3.zero);
            Check(((AudioSource)Field(audio, "shieldBody")).clip.name == "mix_shield_body", "Shield layer missing");
            audio.PlayBoost(true, Vector3.zero);
            Check(((AudioSource)Field(audio, "boostBody")).clip.name == "mix_boost_body", "Boost layer missing");
            audio.SetCharging(true, .8f);
            sfx.SetValue(null, 0f);
            Call(audio, "Update");
            foreach (AudioSource source in go.GetComponents<AudioSource>())
                if (source.clip != null && !source.clip.name.StartsWith("music_"))
                    Check(source.volume == 0, "SFX slider does not mute active audio");
            sfx.SetValue(null, 1f);
            audio.PlayResult(true);
            Check(((AudioSource)Field(audio, "result")).clip.name == "mix_victory", "Victory mapping");
            audio.PlayResult(false);
            Check(((AudioSource)Field(audio, "result")).clip.name == "mix_defeat", "Defeat mapping");
            audio.SetMenuMusic();
            Check((float)Field(audio, "shieldStarted") < 0 && (float)Field(audio, "boostStarted") < 0,
                "Ability tails survive leaving the match");
            Check(!(bool)Field(audio, "charging"), "Charge survives leaving the match");
            UnityEngine.Object.DestroyImmediate(go);
            Debug.Log("AUDIO_AUDIT_PASS checks=" + checks + " clips=" + clips.Length);
            EditorApplication.Exit(0);
        } catch (Exception error) {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }
}
