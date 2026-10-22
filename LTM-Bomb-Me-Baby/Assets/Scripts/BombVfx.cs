using System.Collections.Generic;
using UnityEngine;

/// One-shot particle effects built in code from the Kenney Particle Pack sprites (Art/Particles).
/// Every effect returns false / does nothing when its sprites are missing, so callers can keep
/// their primitive fallback. Live effects are capped so ten players spamming bombs stay cheap.
static class BombVfx {
    const int MaxLiveEffects = 48;
    static readonly List<GameObject> Live = new List<GameObject>();
    static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

    /// Particle look; built per effect with an object initializer.
    internal sealed class Spec {
        public string sprite;
        public bool additive = true;
        public int count = 10;
        public Vector2 lifetime = new Vector2(.4f, .8f);
        public Vector2 speed = new Vector2(1, 3);
        public Vector2 size = new Vector2(.4f, .8f);
        public Color color = Color.white;
        public Color endColor = new Color(1, 1, 1, 0);
        public float gravity;
        public float radius = .1f;
        public float growth = 1;
        public bool hemisphere;
        public bool flat;
        public bool spin = true;
    }

    public static bool Explosion(Vector3 position, float radius, Transform parent) {
        if (BombArt.Particle("fire_01") == null) return false;
        Prune();
        float r = Mathf.Max(1, radius);
        Emit(parent, position, new Spec {
            sprite = "light_01", count = 1, lifetime = new Vector2(.18f, .18f), speed = Vector2.zero,
            size = new Vector2(r * 2.6f, r * 2.6f), color = new Color(1f, .85f, .55f), growth = 1.3f, spin = false
        }, true);
        Emit(parent, position + Vector3.up * .05f, new Spec {
            sprite = "circle_05", count = 1, lifetime = new Vector2(.35f, .35f), speed = Vector2.zero,
            size = new Vector2(r * .6f, r * .6f), color = BombUi.Charge, growth = 3.4f, flat = true, spin = false
        }, true);
        Emit(parent, position, new Spec {
            sprite = "fire_01", count = Mathf.RoundToInt(10 + r * 3), lifetime = new Vector2(.3f, .55f),
            speed = new Vector2(r * 1.2f, r * 2.6f), size = new Vector2(.7f, 1.4f) * Mathf.Sqrt(r),
            color = new Color(1f, .62f, .2f), endColor = new Color(.7f, .1f, .05f, 0), radius = .3f, hemisphere = true
        }, true);
        Emit(parent, position, new Spec {
            sprite = "smoke_04", additive = false, count = Mathf.RoundToInt(6 + r * 2), lifetime = new Vector2(.8f, 1.5f),
            speed = new Vector2(r * .4f, r * 1.1f), size = new Vector2(.9f, 1.8f) * Mathf.Sqrt(r),
            color = new Color(.28f, .24f, .3f, .75f), endColor = new Color(.2f, .18f, .22f, 0), gravity = -.12f,
            radius = r * .3f, hemisphere = true, growth = 1.6f
        }, true);
        Emit(parent, position, new Spec {
            sprite = "spark_05", count = Mathf.RoundToInt(10 + r * 2), lifetime = new Vector2(.25f, .6f),
            speed = new Vector2(r * 2, r * 4.5f), size = new Vector2(.15f, .35f), color = new Color(1f, .9f, .5f),
            endColor = new Color(1f, .5f, .1f, 0), gravity = .8f, hemisphere = true, growth = .4f
        }, true);
        Decal(parent, position, "scorch_02", r * 1.6f, new Color(.1f, .07f, .08f, .8f), 4f);
        Flash(parent, position + Vector3.up * .8f, BombUi.Charge, r * 2.2f, .25f);
        return true;
    }

    public static void PickupSparkle(Vector3 position, Color color, bool big, Transform parent) {
        Emit(parent, position + Vector3.up * .4f, new Spec {
            sprite = "star_06", count = big ? 14 : 7, lifetime = new Vector2(.35f, .7f), speed = new Vector2(1.2f, 2.6f),
            size = new Vector2(.18f, big ? .45f : .3f), color = Color.Lerp(color, Color.white, .35f),
            endColor = BombUi.WithAlpha(color, 0), gravity = -.2f, radius = .25f, growth = .3f
        });
        Emit(parent, position + Vector3.up * .05f, new Spec {
            sprite = "circle_02", count = 1, lifetime = new Vector2(.3f, .3f), speed = Vector2.zero,
            size = new Vector2(.5f, .5f), color = color, growth = big ? 3.5f : 2.4f, flat = true, spin = false
        });
    }

    public static void Hit(Vector3 position, Transform parent) {
        Emit(parent, position + Vector3.up * .5f, new Spec {
            sprite = "spark_02", count = 9, lifetime = new Vector2(.15f, .35f), speed = new Vector2(2.5f, 5f),
            size = new Vector2(.15f, .35f), color = new Color(1f, .45f, .35f), endColor = new Color(1f, .2f, .2f, 0),
            radius = .3f, growth = .5f
        });
    }

    public static void ShieldRing(Vector3 position, Transform parent) {
        Emit(parent, position + Vector3.up * .05f, new Spec {
            sprite = "circle_03", count = 1, lifetime = new Vector2(.45f, .45f), speed = Vector2.zero,
            size = new Vector2(.8f, .8f), color = BombUi.Shield, growth = 3.2f, flat = true, spin = false
        });
        Emit(parent, position + Vector3.up * .5f, new Spec {
            sprite = "magic_02", count = 8, lifetime = new Vector2(.4f, .7f), speed = new Vector2(.6f, 1.4f),
            size = new Vector2(.3f, .55f), color = Color.Lerp(BombUi.Shield, Color.white, .3f),
            endColor = BombUi.WithAlpha(BombUi.Shield, 0), radius = .7f, growth = .6f
        });
    }

    public static void BoostBurst(Vector3 position, Transform parent) {
        Emit(parent, position + Vector3.up * .1f, new Spec {
            sprite = "dirt_02", additive = false, count = 8, lifetime = new Vector2(.35f, .6f), speed = new Vector2(1.5f, 3f),
            size = new Vector2(.35f, .7f), color = new Color(.85f, .8f, .75f, .7f), endColor = new Color(.8f, .75f, .7f, 0),
            radius = .4f, hemisphere = true, growth = 1.8f
        });
        Emit(parent, position + Vector3.up * .05f, new Spec {
            sprite = "twirl_02", count = 1, lifetime = new Vector2(.35f, .35f), speed = Vector2.zero,
            size = new Vector2(.9f, .9f), color = BombUi.Boost, growth = 2.4f, flat = true
        });
    }

    public static void EliminationPuff(Vector3 position, Color color, Transform parent) {
        Emit(parent, position + Vector3.up * .4f, new Spec {
            sprite = "smoke_07", additive = false, count = 12, lifetime = new Vector2(.7f, 1.3f), speed = new Vector2(.8f, 2f),
            size = new Vector2(.7f, 1.3f), color = new Color(.85f, .82f, .9f, .85f), endColor = new Color(.6f, .58f, .65f, 0),
            gravity = -.15f, radius = .4f, growth = 1.7f
        }, true);
        Emit(parent, position + Vector3.up * .5f, new Spec {
            sprite = "star_04", count = 10, lifetime = new Vector2(.4f, .8f), speed = new Vector2(2f, 4f),
            size = new Vector2(.2f, .4f), color = Color.Lerp(color, Color.white, .3f), endColor = BombUi.WithAlpha(color, 0),
            gravity = .6f, growth = .4f
        }, true);
    }

    /// Looping emitter owned by a long-lived object (fuse sparks, boost dust); rate starts at 0.
    public static ParticleSystem Emitter(Transform parent, Vector3 localPosition, Spec spec) {
        ParticleSystem system = Build(parent, spec, true);
        if (system == null) return null;
        system.transform.localPosition = localPosition;
        system.Play();
        return system;
    }

    /// Spawns a burst; non-essential effects are dropped while too many are alive.
    static void Emit(Transform parent, Vector3 position, Spec spec, bool essential = false) {
        Prune();
        if (!essential && Live.Count >= MaxLiveEffects) return;
        ParticleSystem system = Build(parent, spec, false);
        if (system == null) return;
        system.transform.position = position;
        system.Play();
        Live.Add(system.gameObject);
    }

    static ParticleSystem Build(Transform parent, Spec spec, bool looping) {
        Material material = MaterialFor(spec.sprite, spec.additive);
        if (material == null) return null;
        var go = new GameObject("VFX " + spec.sprite);
        if (parent != null) go.transform.SetParent(parent, false);
        ParticleSystem system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = system.main;
        main.duration = looping ? 1 : Mathf.Max(.05f, spec.lifetime.y);
        main.loop = looping;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(spec.lifetime.x, spec.lifetime.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(spec.speed.x, spec.speed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(spec.size.x, spec.size.y);
        main.startColor = spec.color;
        main.startRotation = spec.spin ? new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2) : new ParticleSystem.MinMaxCurve(0);
        main.gravityModifier = spec.gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = Mathf.Max(spec.count, looping ? 64 : spec.count);
        if (!looping) main.stopAction = ParticleSystemStopAction.Destroy;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 0;
        if (!looping) emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)spec.count) });

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = spec.radius > 0;
        shape.shapeType = spec.hemisphere ? ParticleSystemShapeType.Hemisphere : ParticleSystemShapeType.Sphere;
        shape.radius = Mathf.Max(.01f, spec.radius);
        // Hemisphere points along +Z by default; aim it at the sky.
        if (spec.hemisphere) shape.rotation = new Vector3(-90, 0, 0);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, .35f), new GradientAlphaKey(0, 1) });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);
        // Tint toward endColor over the particle's life through the start color blend.
        if (spec.endColor != new Color(1, 1, 1, 0)) {
            var tint = new Gradient();
            tint.SetKeys(
                new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(TintFor(spec.color, spec.endColor), 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, .35f), new GradientAlphaKey(0, 1) });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(tint);
        }

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.enabled = !Mathf.Approximately(spec.growth, 1);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, spec.growth));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = material;
        renderer.renderMode = spec.flat ? ParticleSystemRenderMode.HorizontalBillboard : ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortingFudge = spec.flat ? 10 : 0;
        return system;
    }

    /// Per-channel multiplier that turns startColor into endColor (colorOverLifetime multiplies).
    static Color TintFor(Color start, Color end) => new Color(
        start.r > .01f ? Mathf.Clamp01(end.r / start.r) : 1,
        start.g > .01f ? Mathf.Clamp01(end.g / start.g) : 1,
        start.b > .01f ? Mathf.Clamp01(end.b / start.b) : 1, 1);

    /// Flat sprite on the ground that fades out (scorch marks).
    static void Decal(Transform parent, Vector3 position, string sprite, float size, Color color, float seconds) {
        Material source = MaterialFor(sprite, false);
        if (source == null) return;
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "Scorch";
        Object.Destroy(quad.GetComponent<Collider>());
        if (parent != null) quad.transform.SetParent(parent, false);
        quad.transform.position = new Vector3(position.x, .02f, position.z);
        quad.transform.rotation = Quaternion.Euler(90, Random.Range(0, 360f), 0);
        quad.transform.localScale = Vector3.one * size;
        var renderer = quad.GetComponent<Renderer>();
        renderer.material = new Material(source) { color = color };
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        quad.AddComponent<BombFade>().Begin(renderer.material, null, seconds);
        Live.Add(quad);
    }

    static void Flash(Transform parent, Vector3 position, Color color, float range, float seconds) {
        var go = new GameObject("Explosion flash");
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.position = position;
        Light light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = range;
        light.intensity = 4;
        go.AddComponent<BombFade>().Begin(null, light, seconds);
        Live.Add(go);
    }

    static void Prune() => Live.RemoveAll(item => item == null);

    /// Additive when the legacy particle shader is available (Editor, or a build that includes it),
    /// otherwise Sprites/Default, which is always included in builds.
    static Material MaterialFor(string sprite, bool additive) {
        string key = sprite + (additive ? "+" : "");
        if (Materials.TryGetValue(key, out Material cached)) return cached;
        Texture2D texture = BombArt.Particle(sprite);
        Material material = null;
        if (texture != null) {
            texture.wrapMode = TextureWrapMode.Clamp;
            Shader shader = additive ? Shader.Find("Legacy Shaders/Particles/Additive") : null;
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader != null) material = new Material(shader) { name = key, mainTexture = texture };
        }
        Materials[key] = material;
        return material;
    }
}

/// Fades a material's alpha and/or a light's intensity to zero, then destroys the object.
sealed class BombFade : MonoBehaviour {
    Material material;
    Light fadingLight;
    float seconds, startedAt, startAlpha, startIntensity;

    public void Begin(Material fadeMaterial, Light light, float duration) {
        material = fadeMaterial;
        fadingLight = light;
        seconds = Mathf.Max(.01f, duration);
        startedAt = Time.time;
        if (material != null) startAlpha = material.color.a;
        if (fadingLight != null) startIntensity = fadingLight.intensity;
    }

    void Update() {
        float t = (Time.time - startedAt) / seconds;
        if (t >= 1) { Destroy(gameObject); return; }
        // Decals hold for most of their life, lights fall off immediately.
        if (material != null) {
            Color c = material.color;
            c.a = startAlpha * (1 - Mathf.SmoothStep(.6f, 1, t));
            material.color = c;
        }
        if (fadingLight != null) fadingLight.intensity = startIntensity * (1 - t) * (1 - t);
    }

    void OnDestroy() {
        if (material != null) Destroy(material);
    }
}
