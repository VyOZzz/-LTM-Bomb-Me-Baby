using UnityEngine;
using System.Collections.Generic;

/// Central music and SFX player. Clips live under Resources/Audio so both the authored scene
/// and the runtime bootstrap path work without Inspector references.
public sealed class BombAudio : MonoBehaviour {
    public static BombAudio Instance { get; private set; }

    AudioSource oneShot;
    AudioSource chargeLoop;
    AudioSource shieldBody;
    AudioSource boostBody;
    AudioSource music;
    AudioClip chargeClip, explosionClip, explosionLowClip, energyClip, coreClip, shieldClip, boostClip;
    AudioClip shieldBodyClip, boostBodyClip;
    AudioClip damageClip, victoryClip, defeatClip, uiClickClip;
    AudioClip menuMusic, meadowMusic, crossroadsMusic, craterMusic, desiredMusic;
    static readonly List<AudioSource> SpatialSources = new List<AudioSource>();
    const int MaxSpatialVoices = 10;
    bool charging;
    float chargeAmount;
    float musicDuckUntil;
    float shieldBodyStartedAt = -1;
    float boostBodyStartedAt = -1;
    const float ShieldBodyDuration = 1.5f;
    const float BoostBodyDuration = .9f;

    void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(this);
            return;
        }
        Instance = this;

        oneShot = gameObject.AddComponent<AudioSource>();
        oneShot.playOnAwake = false;
        oneShot.spatialBlend = 0;

        chargeLoop = gameObject.AddComponent<AudioSource>();
        chargeLoop.playOnAwake = false;
        chargeLoop.loop = true;
        chargeLoop.spatialBlend = 0;

        shieldBody = CreateAbilityBodySource();
        boostBody = CreateAbilityBodySource();

        music = gameObject.AddComponent<AudioSource>();
        music.playOnAwake = false;
        music.loop = true;
        music.spatialBlend = 0;
        music.priority = 180;
        music.volume = 0;

        chargeClip = Load("sfx_charge_loop");
        explosionClip = Load("kenney_explosion_crunch");
        explosionLowClip = Load("kenney_explosion_low");
        energyClip = Load("kenney_energy_pickup");
        coreClip = Load("kenney_core_pickup");
        shieldClip = Load("kenney_shield");
        boostClip = Load("kenney_boost");
        shieldBodyClip = Load("kenney_shield_body");
        boostBodyClip = Load("kenney_boost_body");
        damageClip = Load("kenney_damage");
        victoryClip = Load("sfx_victory");
        defeatClip = Load("sfx_defeat");
        uiClickClip = Load("kenney_ui_click");
        menuMusic = Load("music_menu");
        meadowMusic = Load("music_meadow");
        crossroadsMusic = Load("music_crossroads");
        craterMusic = Load("music_crater");
        chargeLoop.clip = chargeClip;
        SetMenuMusic();
    }

    void Update() {
        chargeLoop.volume = BombSettings.Sfx * Mathf.Lerp(.12f, .32f, chargeAmount);
        chargeLoop.pitch = Mathf.Lerp(.82f, 1.42f, chargeAmount);
        if (charging && chargeClip != null) {
            if (!chargeLoop.isPlaying) chargeLoop.Play();
        } else if (chargeLoop.isPlaying) chargeLoop.Stop();

        UpdateShieldBody();
        UpdateBoostBody();

        bool ducking = charging || Time.unscaledTime < musicDuckUntil;
        float normalMusicLevel = desiredMusic == menuMusic ? .18f : .10f;
        float musicTarget = BombSettings.Music * (ducking ? .035f : normalMusicLevel);
        if (music.clip != desiredMusic) {
            music.volume = Mathf.MoveTowards(music.volume, 0, Time.unscaledDeltaTime * .75f);
            if (music.volume <= .01f) {
                music.Stop();
                music.clip = desiredMusic;
                if (desiredMusic != null) music.Play();
            }
        } else {
            // Gameplay cues win immediately; music returns slowly so repeated action
            // sounds do not fight a constantly pumping background track.
            float fadeSpeed = music.volume > musicTarget ? 2.8f : .28f;
            music.volume = Mathf.MoveTowards(music.volume, musicTarget, Time.unscaledDeltaTime * fadeSpeed);
        }
    }

    void OnDestroy() {
        if (Instance == this) Instance = null;
    }

    static AudioClip Load(string name) {
        AudioClip clip = Resources.Load<AudioClip>("Audio/" + name);
        if (clip == null) Debug.LogWarning("Missing audio clip: Resources/Audio/" + name);
        return clip;
    }

    AudioSource CreateAbilityBodySource() {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0;
        source.priority = 112;
        return source;
    }

    void UpdateShieldBody() {
        if (shieldBodyStartedAt < 0) return;
        float elapsed = Time.unscaledTime - shieldBodyStartedAt;
        if (elapsed >= ShieldBodyDuration) {
            shieldBody.Stop();
            shieldBodyStartedAt = -1;
            return;
        }

        float attack = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / .1f));
        float release = Mathf.SmoothStep(0, 1, Mathf.Clamp01((ShieldBodyDuration - elapsed) / .28f));
        shieldBody.volume = BombSettings.Sfx * .34f * Mathf.Min(attack, release);
        shieldBody.pitch = .94f + Mathf.Sin(elapsed * 9f) * .035f;
    }

    void UpdateBoostBody() {
        if (boostBodyStartedAt < 0) return;
        float elapsed = Time.unscaledTime - boostBodyStartedAt;
        if (elapsed >= BoostBodyDuration) {
            boostBody.Stop();
            boostBodyStartedAt = -1;
            return;
        }

        float attack = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / .05f));
        float release = Mathf.SmoothStep(0, 1, Mathf.Clamp01((BoostBodyDuration - elapsed) / .22f));
        float progress = Mathf.SmoothStep(0, 1, elapsed / BoostBodyDuration);
        boostBody.volume = BombSettings.Sfx * .46f * Mathf.Min(attack, release);
        boostBody.pitch = Mathf.Lerp(.82f, 1.28f, progress);
    }

    void StartAbilityBody(AudioSource source, AudioClip clip, ref float startedAt) {
        if (clip == null || BombSettings.Sfx <= 0) return;
        source.Stop();
        source.clip = clip;
        source.time = 0;
        source.volume = 0;
        startedAt = Time.unscaledTime;
        source.Play();
    }

    public void SetCharging(bool active, float amount = 0) {
        charging = active;
        chargeAmount = active ? Mathf.Clamp01(amount) : 0;
    }

    public void StopGameplayLoops() {
        SetCharging(false);
        shieldBodyStartedAt = -1;
        boostBodyStartedAt = -1;
        shieldBody.Stop();
        boostBody.Stop();
    }

    public void SetMenuMusic() => desiredMusic = menuMusic;

    public void SetMapMusic(string mapId) {
        switch (mapId) {
            case "meadow": desiredMusic = meadowMusic; break;
            case "crossroads": desiredMusic = crossroadsMusic; break;
            case "crater": desiredMusic = craterMusic; break;
            default: desiredMusic = menuMusic; break;
        }
    }

    public void PlayEnergyPickup(bool local, Vector3 position) {
        DuckMusic(.10f);
        PlayForPlayer(energyClip, local, position, .86f);
    }

    public void PlayCorePickup(bool local, Vector3 position) {
        DuckMusic(.24f);
        PlayForPlayer(coreClip, local, position, 1f);
    }

    public void PlayShield(bool local, Vector3 position) {
        DuckMusic(ShieldBodyDuration);
        PlayForPlayer(shieldClip, local, position, .95f);
        if (local) StartAbilityBody(shieldBody, shieldBodyClip, ref shieldBodyStartedAt);
    }

    public void PlayBoost(bool local, Vector3 position) {
        DuckMusic(BoostBodyDuration);
        PlayForPlayer(boostClip, local, position, .92f);
        if (local) StartAbilityBody(boostBody, boostBodyClip, ref boostBodyStartedAt);
    }

    public void PlayDamage(bool local, Vector3 position) {
        DuckMusic(.38f);
        PlayForPlayer(damageClip, local, position, local ? 1f : .72f);
    }
    public void PlayResult(bool won) {
        DuckMusic(1.4f);
        Play2D(won ? victoryClip : defeatClip, .9f);
    }

    public void PlayExplosion(Vector3 position, float radius, bool local) {
        // Layer a crunchy transient over a low-frequency body. The local player's blast
        // is 2D because the overhead camera's AudioListener sits about 31 units away.
        DuckMusic(.65f);
        float power = Mathf.InverseLerp(2, 12, radius);
        float crunchVolume = Mathf.Lerp(.76f, 1f, power);
        float lowVolume = Mathf.Lerp(.48f, .76f, power);
        if (local) {
            Play2D(explosionClip, crunchVolume);
            Play2D(explosionLowClip, lowVolume);
        } else {
            float range = Mathf.Lerp(42, 62, power);
            PlayAt(explosionClip, position, crunchVolume * .72f, range);
            PlayAt(explosionLowClip, position, lowVolume * .62f, range);
        }
    }

    public static void UiClick() {
        if (Instance != null) Instance.Play2D(Instance.uiClickClip, .55f);
    }

    void PlayForPlayer(AudioClip clip, bool local, Vector3 position, float volume) {
        if (local) Play2D(clip, volume);
        else PlayAt(clip, position, volume * .52f, 22);
    }

    void DuckMusic(float seconds) => musicDuckUntil = Mathf.Max(musicDuckUntil, Time.unscaledTime + seconds);

    void Play2D(AudioClip clip, float volume) {
        if (clip == null || BombSettings.Sfx <= 0) return;
        oneShot.PlayOneShot(clip, volume * BombSettings.Sfx);
    }

    static void PlayAt(AudioClip clip, Vector3 position, float volume, float maxDistance) {
        if (clip == null || BombSettings.Sfx <= 0) return;
        SpatialSources.RemoveAll(source => source == null || !source.isPlaying);
        if (SpatialSources.Count >= MaxSpatialVoices) return;
        GameObject emitter = new GameObject("SFX - " + clip.name);
        emitter.transform.position = position;
        AudioSource source = emitter.AddComponent<AudioSource>();
        source.clip = clip;
        source.playOnAwake = false;
        source.spatialBlend = 1;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 2.5f;
        source.maxDistance = maxDistance;
        source.dopplerLevel = 0;
        source.priority = clip.name.Contains("explosion") ? 96 : 128;
        source.volume = volume * BombSettings.Sfx;
        source.Play();
        SpatialSources.Add(source);
        Destroy(emitter, clip.length + .1f);
    }
}
