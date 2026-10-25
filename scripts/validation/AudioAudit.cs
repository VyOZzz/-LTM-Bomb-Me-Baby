// Copy this file to Assets/Editor in an isolated audio validation project.
// Requires BombAudio.cs, BombSettings.cs and the 20 clips loaded by BombAudio.
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
            Check(clips.Length == 20, "Expected 20 active downloaded/user clips, got " + clips.Length);
            foreach (AudioClip clip in clips) {
                Check(clip.length > .005f && clip.samples > 0, "Invalid imported audio: " + clip.name);
                Check(clip.frequency == 44100 || clip.name == "music_menu", "Unexpected sample rate");
                Check(!clip.name.StartsWith("sfx_"), "Generated audio is still active");
                if (clip.name.StartsWith("music_"))
                    Check(clip.loadType == AudioClipLoadType.Streaming, "Music must stream: " + clip.name);
            }
            foreach (string name in new[] { "mix_explosion_low", "mix_shield_body", "mix_boost_body" })
                Check(Resources.Load<AudioClip>("Audio/" + name) == null, "Retired layer still ships: " + name);
            foreach (string name in new[] { "mix_explosion_1", "mix_explosion_2", "mix_explosion_3", "mix_shield", "mix_shield_hit", "mix_boost" }) {
                AudioClip clip = Resources.Load<AudioClip>("Audio/" + name);
                float maxLength = name.StartsWith("mix_explosion_") ? 1.7f : .85f;
                Check(clip != null && clip.length < maxLength, "Soft cue is missing or too long: " + name);
                var importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip));
                Check(importer.defaultSampleSettings.compressionFormat == AudioCompressionFormat.PCM,
                    "Short cue must not be compressed a second time: " + name);
                clip.LoadAudioData();
                float[] samples = new float[clip.samples * clip.channels];
                Check(clip.GetData(samples, 0), "Cannot read imported samples: " + name);
                float peak = 0;
                foreach (float sample in samples) {
                    if (float.IsNaN(sample) || float.IsInfinity(sample)) throw new Exception("Invalid sample: " + name);
                    peak = Mathf.Max(peak, Mathf.Abs(sample));
                }
                float maxPeak = name.StartsWith("mix_explosion_") ? .60f : .40f;
                Check(peak > .01f && peak < maxPeak, "Silent or excessive imported peak: " + name + " " + peak);
                Check(Mathf.Abs(samples[0]) < .01f && Mathf.Abs(samples[samples.Length - 1]) < .01f,
                    "Cue edges can click: " + name);
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
            Check(Array.Exists(go.GetComponents<AudioSource>(), source => source.clip != null
                && source.clip.name == "mix_shield" && !source.loop), "Shield activation must be a one-shot");
            audio.PlayBoost(true, Vector3.zero);
            Check(Array.Exists(go.GetComponents<AudioSource>(), source => source.clip != null
                && source.clip.name == "mix_boost" && !source.loop), "Boost must be a one-shot");
            foreach (AudioSource source in go.GetComponents<AudioSource>())
                Check(!source.loop || source.clip == null || source.clip.name == "mix_charge"
                    || source.clip.name.StartsWith("music_"), "Unexpected gameplay loop");
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
            Check((float)Field(audio, "chargeGain") == 0, "Charge tail survives leaving the match");
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
