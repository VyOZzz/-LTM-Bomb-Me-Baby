using UnityEngine;

/// Central SFX player. Clips live under Resources/Audio so both the authored scene
/// and the runtime bootstrap path work without Inspector references.
public sealed class BombAudio : MonoBehaviour {
    public static BombAudio Instance { get; private set; }

    AudioSource oneShot;
    AudioSource chargeLoop;
    AudioClip chargeClip, explosionClip, energyClip, coreClip, shieldClip, boostClip;
    AudioClip damageClip, matchStartClip, victoryClip, defeatClip, uiClickClip;
    bool charging;
    float chargeAmount;

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

        chargeClip = Load("sfx_charge_loop");
        explosionClip = Load("sfx_explosion");
        energyClip = Load("sfx_energy_pickup");
        coreClip = Load("sfx_core_pickup");
        shieldClip = Load("sfx_shield");
        boostClip = Load("sfx_boost");
        damageClip = Load("sfx_damage");
        matchStartClip = Load("sfx_match_start");
        victoryClip = Load("sfx_victory");
        defeatClip = Load("sfx_defeat");
        uiClickClip = Load("sfx_ui_click");
        chargeLoop.clip = chargeClip;
    }

    void Update() {
        chargeLoop.volume = BombSettings.Sfx * Mathf.Lerp(.16f, .42f, chargeAmount);
        chargeLoop.pitch = Mathf.Lerp(.82f, 1.42f, chargeAmount);
        if (charging && chargeClip != null) {
            if (!chargeLoop.isPlaying) chargeLoop.Play();
        } else if (chargeLoop.isPlaying) chargeLoop.Stop();
    }

    void OnDestroy() {
        if (Instance == this) Instance = null;
    }

    static AudioClip Load(string name) {
        AudioClip clip = Resources.Load<AudioClip>("Audio/" + name);
        if (clip == null) Debug.LogWarning("Missing SFX clip: Resources/Audio/" + name);
        return clip;
    }

    public void SetCharging(bool active, float amount = 0) {
        charging = active;
        chargeAmount = active ? Mathf.Clamp01(amount) : 0;
    }

    public void StopGameplayLoops() => SetCharging(false);

    public void PlayMatchStart() => Play2D(matchStartClip, .82f);
    public void PlayEnergyPickup(bool local, Vector3 position) => PlayForPlayer(energyClip, local, position, .72f);
    public void PlayCorePickup(bool local, Vector3 position) => PlayForPlayer(coreClip, local, position, .88f);
    public void PlayShield(bool local, Vector3 position) => PlayForPlayer(shieldClip, local, position, .8f);
    public void PlayBoost(bool local, Vector3 position) => PlayForPlayer(boostClip, local, position, .8f);
    public void PlayDamage(bool local, Vector3 position) => PlayForPlayer(damageClip, local, position, local ? .9f : .62f);
    public void PlayResult(bool won) => Play2D(won ? victoryClip : defeatClip, .95f);

    public void PlayExplosion(Vector3 position, float radius) {
        // A larger blast is louder and carries further, while the listener still gets
        // normal Unity 3D attenuation and stereo positioning.
        PlayAt(explosionClip, position, Mathf.Lerp(.62f, 1f, Mathf.InverseLerp(2, 12, radius)),
            Mathf.Lerp(18, 34, Mathf.InverseLerp(2, 12, radius)));
    }

    public static void UiClick() {
        if (Instance != null) Instance.Play2D(Instance.uiClickClip, .55f);
    }

    void PlayForPlayer(AudioClip clip, bool local, Vector3 position, float volume) {
        if (local) Play2D(clip, volume);
        else PlayAt(clip, position, volume * .7f, 24);
    }

    void Play2D(AudioClip clip, float volume) {
        if (clip == null || BombSettings.Sfx <= 0) return;
        oneShot.PlayOneShot(clip, volume * BombSettings.Sfx);
    }

    static void PlayAt(AudioClip clip, Vector3 position, float volume, float maxDistance) {
        if (clip == null || BombSettings.Sfx <= 0) return;
        GameObject emitter = new GameObject("SFX - " + clip.name);
        emitter.transform.position = position;
        AudioSource source = emitter.AddComponent<AudioSource>();
        source.clip = clip;
        source.playOnAwake = false;
        source.spatialBlend = 1;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 2.5f;
        source.maxDistance = maxDistance;
        source.volume = volume * BombSettings.Sfx;
        source.Play();
        Destroy(emitter, clip.length + .1f);
    }
}
