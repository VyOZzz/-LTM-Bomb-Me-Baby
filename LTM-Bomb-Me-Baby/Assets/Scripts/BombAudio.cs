using UnityEngine;

/// Downloaded recordings, mixed for a top-down arena. See docs/AUDIO_CREDITS.md.
public sealed class BombAudio : MonoBehaviour {
    public static BombAudio Instance { get; private set; }

    enum Cue { Explosion, Energy, Core, Shield, Boost, Damage, Land, Drop, ShieldHit, Ui, UiHover, Count }
    sealed class Voice {
        public AudioSource source;
        public float gain;
        public bool local;
        public Vector3 position;
        public float range;
    }

    readonly Voice[] voices = new Voice[20];
    readonly float[] nextCue = new float[(int)Cue.Count * 2];
    readonly AudioSource[] music = new AudioSource[2];
    readonly float[] musicWeight = new float[2];
    AudioSource chargeLoop, result, uiHover;
    AudioClip chargeClip, energyClip, coreClip, shieldClip, boostClip;
    AudioClip shieldHitClip, dropClip;
    AudioClip damageClip, victoryClip, defeatClip, uiClickClip;
    AudioClip[] explosions, landings;
    AudioClip menuMusic, meadowMusic, crossroadsMusic, craterMusic, desiredMusic;
    Vector3 listeningPosition, listeningRight = Vector3.right;
    bool hasListeningPosition, charging;
    float chargeAmount, chargeGain, musicDuckUntil, musicDuck = 1;
    int activeMusic, explosionVariant, landingVariant;
    const float UiHoverGain = .40f;

    void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(this);
            return;
        }
        Instance = this;
        for (int i = 0; i < voices.Length; i++)
            voices[i] = new Voice { source = CreateSource(false, 128) };
        chargeLoop = CreateSource(true, 100);
        result = CreateSource(false, 64);
        uiHover = CreateSource(false, 80);
        for (int i = 0; i < music.Length; i++) music[i] = CreateSource(true, 180);

        chargeClip = Load("mix_charge");
        explosions = new[] { Load("mix_explosion_1"), Load("mix_explosion_2"), Load("mix_explosion_3") };
        energyClip = Load("mix_energy");
        coreClip = Load("mix_core");
        shieldClip = Load("mix_shield");
        shieldHitClip = Load("mix_shield_hit");
        boostClip = Load("mix_boost");
        damageClip = Load("mix_damage");
        landings = new[] { Load("mix_land_1"), Load("mix_land_2") };
        dropClip = Load("mix_drop");
        victoryClip = Load("mix_victory");
        defeatClip = Load("mix_defeat");
        uiClickClip = Load("mix_ui");
        // The 10 ms click is too short for a quiet hover under the lobby music.
        // Reuse the soft confirmation recording, with one dedicated voice to avoid stacking.
        uiHover.clip = energyClip;
        uiHover.pitch = 1.25f;
        // User-selected lobby recording and its playback level stay unchanged.
        menuMusic = Load("music_menu");
        meadowMusic = Load("music_meadow");
        crossroadsMusic = Load("music_crossroads");
        craterMusic = Load("music_crater");
        chargeLoop.clip = chargeClip;
        SetMenuMusic();
    }

    AudioSource CreateSource(bool loop, int priority) {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0;
        source.dopplerLevel = 0;
        source.priority = priority;
        source.volume = 0;
        return source;
    }

    static AudioClip Load(string name) {
        AudioClip clip = Resources.Load<AudioClip>("Audio/" + name);
        if (clip == null) Debug.LogWarning("Missing audio clip: Resources/Audio/" + name);
        return clip;
    }

    void Update() {
        float dt = Time.unscaledDeltaTime;
        float chargeTarget = charging ? Mathf.Lerp(.16f, .32f, chargeAmount) : 0;
        chargeGain = Mathf.MoveTowards(chargeGain, chargeTarget, dt * (charging ? 1.5f : 2.8f));
        chargeLoop.volume = BombSettings.Sfx * chargeGain;
        chargeLoop.pitch = Mathf.Lerp(.85f, 1.3f, chargeAmount);
        if (charging && chargeClip != null && !chargeLoop.isPlaying) chargeLoop.Play();
        if (!charging && chargeGain <= .001f) chargeLoop.Stop();
        result.volume = BombSettings.Sfx * .65f;
        uiHover.volume = BombSettings.Sfx * UiHoverGain;
        UpdateVoices();
        UpdateMusic(dt);
    }

    void UpdateVoices() {
        float totalGain = 0;
        foreach (Voice voice in voices) {
            float unused;
            if (voice.source.isPlaying)
                totalGain += voice.gain * Audibility(voice.local, voice.position, voice.range, out unused);
        }
        // Reserve headroom for loops and music even when several explosions overlap.
        float headroom = Mathf.Min(1, .9f / Mathf.Max(.001f, totalGain));
        foreach (Voice voice in voices) {
            if (!voice.source.isPlaying) continue;
            float pan, attenuation = Audibility(voice.local, voice.position, voice.range, out pan);
            voice.source.volume = voice.gain * attenuation * headroom * BombSettings.Sfx;
            voice.source.panStereo = pan;
        }
    }

    void UpdateMusic(float dt) {
        // Use two sources so the next track enters while the previous one fades out.
        if (music[activeMusic].clip != desiredMusic) {
            int incoming = 1 - activeMusic;
            AudioSource source = music[incoming];
            if (source.clip != desiredMusic) {
                source.Stop();
                source.clip = desiredMusic;
                musicWeight[incoming] = 0;
                source.volume = 0;
            }
            if (desiredMusic != null) {
                // Streaming data may not be ready on the first frame after loading.
                if (desiredMusic.loadState == AudioDataLoadState.Unloaded) desiredMusic.LoadAudioData();
                if (desiredMusic.loadState != AudioDataLoadState.Loaded) return;
                if (!source.isPlaying) source.Play();
            }
            activeMusic = incoming;
        }
        bool duck = BombSettings.Sfx > .001f && (charging || Time.unscaledTime < musicDuckUntil || result.isPlaying);
        musicDuck = Mathf.MoveTowards(musicDuck, duck ? .24f : 1, dt * (duck ? 8 : .7f));
        for (int i = 0; i < music.Length; i++) {
            musicWeight[i] = Mathf.MoveTowards(musicWeight[i], i == activeMusic ? 1 : 0, dt / 1.2f);
            float level = music[i].clip == menuMusic ? .18f : .26f;
            music[i].volume = BombSettings.Music * level * musicWeight[i] * musicDuck;
            if (i != activeMusic && musicWeight[i] == 0) music[i].Stop();
        }
    }

    public void SetListeningPoint(Vector3 position, Vector3 screenRight) {
        listeningPosition = position;
        screenRight.y = 0;
        listeningRight = screenRight.sqrMagnitude > .01f ? screenRight.normalized : Vector3.right;
        hasListeningPosition = true;
    }

    float Audibility(bool local, Vector3 position, float range, out float pan) {
        pan = 0;
        if (local) return 1;
        if (!hasListeningPosition) return 0;
        Vector3 offset = position - listeningPosition;
        offset.y = 0;
        float distance = offset.magnitude;
        pan = Mathf.Clamp(Vector3.Dot(offset, listeningRight) / 18f, -.65f, .65f);
        // Camera height must not make nearby pickups and abilities inaudible.
        return .65f * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(2, range, distance)));
    }

    bool AllowCue(Cue cue, bool local, float interval) {
        if (BombSettings.Sfx <= 0) return false;
        int index = (int)cue + (local ? 0 : (int)Cue.Count);
        if (Time.unscaledTime < nextCue[index]) return false;
        nextCue[index] = Time.unscaledTime + interval;
        return true;
    }

    void Play(AudioClip clip, bool local, Vector3 position, float gain, float range = 25, float pitch = 1) {
        if (clip == null || BombSettings.Sfx <= 0) return;
        float pan, attenuation = Audibility(local, position, range, out pan);
        if (attenuation < .005f) return;
        // Reserve 12 voices for local feedback; remote activity cannot flood the mix.
        int start = local ? 0 : 12, end = local ? 12 : voices.Length;
        int playing = 0;
        Voice free = null;
        for (int i = start; i < end; i++) {
            if (voices[i].source.isPlaying) playing++;
            else if (free == null) free = voices[i];
        }
        if (free == null) return;
        free.gain = gain / Mathf.Sqrt(1 + playing * .25f);
        free.local = local;
        free.position = position;
        free.range = range;
        free.source.clip = clip;
        free.source.pitch = pitch;
        free.source.panStereo = pan;
        free.source.volume = free.gain * attenuation * BombSettings.Sfx;
        free.source.Play();
        UpdateVoices();
    }

    void DuckMusic(float seconds) {
        if (BombSettings.Sfx > 0) musicDuckUntil = Mathf.Max(musicDuckUntil, Time.unscaledTime + seconds);
    }

    public void SetCharging(bool active, float amount = 0) {
        charging = active;
        chargeAmount = active ? Mathf.Clamp01(amount) : 0;
    }

    public void StopGameplayLoops() {
        charging = false;
        chargeAmount = chargeGain = 0;
        chargeLoop.Stop();
    }

    public void SetMenuMusic() {
        StopGameplayLoops();
        uiHover.Stop();
        foreach (Voice voice in voices) voice.source.Stop();
        result.Stop();
        hasListeningPosition = false;
        musicDuckUntil = 0;
        desiredMusic = menuMusic;
    }

    public void SetMapMusic(string mapId) {
        StopGameplayLoops();
        uiHover.Stop();
        result.Stop();
        musicDuckUntil = 0;
        System.Array.Clear(nextCue, 0, nextCue.Length);
        switch (mapId) {
            case "meadow": desiredMusic = meadowMusic; break;
            case "crossroads": desiredMusic = crossroadsMusic; break;
            case "crater": desiredMusic = craterMusic; break;
            default: desiredMusic = menuMusic; break;
        }
    }

    public void PlayEnergyPickup(bool local, Vector3 position) {
        if (AllowCue(Cue.Energy, local, .065f))
            Play(energyClip, local, position, .48f, 20, Random.Range(.97f, 1.06f));
    }

    public void PlayCorePickup(bool local, Vector3 position) {
        if (!AllowCue(Cue.Core, local, .12f)) return;
        if (local) DuckMusic(.25f);
        Play(coreClip, local, position, .60f);
    }

    public void PlayShield(bool local, Vector3 position) {
        if (!AllowCue(Cue.Shield, local, .15f)) return;
        Play(shieldClip, local, position, .48f);
        // One short activation cue; engine loops made the shield sound distorted.
        if (local) DuckMusic(.2f);
    }

    public void PlayBoost(bool local, Vector3 position) {
        if (!AllowCue(Cue.Boost, local, .15f)) return;
        Play(boostClip, local, position, .50f, 25, Random.Range(.98f, 1.02f));
        if (local) DuckMusic(.2f);
    }

    public void PlayShieldHit(bool local, Vector3 position) {
        if (AllowCue(Cue.ShieldHit, local, .10f))
            Play(shieldHitClip, local, position, .44f, 25, .9f);
    }

    public void PlayLanding(bool local, Vector3 position, float speed) {
        if (AllowCue(Cue.Land, local, .12f))
            Play(landings[landingVariant++ % landings.Length], local, position,
                Mathf.Lerp(.18f, .40f, Mathf.InverseLerp(1, 7, speed)), 20, Random.Range(.94f, 1.04f));
    }

    public void PlayDrop(bool local, Vector3 position) {
        if (AllowCue(Cue.Drop, local, .15f)) Play(dropClip, local, position, .42f, 24);
    }

    public void PlayDamage(bool local, Vector3 position) {
        if (!AllowCue(Cue.Damage, local, .10f)) return;
        if (local) DuckMusic(.35f);
        Play(damageClip, local, position, .58f);
    }

    public void PlayResult(bool won) {
        StopGameplayLoops();
        foreach (Voice voice in voices) voice.source.Stop();
        result.Stop();
        result.clip = won ? victoryClip : defeatClip;
        result.volume = BombSettings.Sfx * .65f;
        if (result.clip != null && BombSettings.Sfx > 0) {
            DuckMusic(result.clip.length + .4f);
            result.Play();
        }
    }

    public void PlayExplosion(Vector3 position, float radius, bool local = false) {
        if (!AllowCue(Cue.Explosion, local, local ? .08f : .12f)) return;
        float pan;
        if (Audibility(local, position, 38, out pan) < .005f) return;
        DuckMusic(.55f);
        float power = Mathf.InverseLerp(2, 12, radius);
        // Each clip already contains the low impact; avoid stacking another bass layer.
        Play(explosions[explosionVariant++ % explosions.Length], local, position,
            Mathf.Lerp(.64f, .86f, power), 38, Random.Range(.98f, 1.02f));
    }

    public static void UiClick() {
        if (Instance != null && Instance.AllowCue(Cue.Ui, true, .045f))
            Instance.Play(Instance.uiClickClip, true, Vector3.zero, .30f);
    }

    /// Call once when the pointer enters a button, not on every hovered frame.
    public static void UiHover() {
        // Separate cooldowns keep a hover sound from suppressing the next click.
        if (Instance == null || Instance.uiHover.clip == null || !Instance.AllowCue(Cue.UiHover, true, .12f)) return;
        Instance.uiHover.Stop();
        Instance.uiHover.volume = BombSettings.Sfx * UiHoverGain;
        Instance.uiHover.Play();
    }

    void OnDestroy() {
        if (Instance == this) Instance = null;
    }
}
