using System.Collections.Generic;
using UnityEngine;

/// Builds the 3D look of players and pickups from the Kenney Platformer Kit (Art/Models/Platformer)
/// and drives their per-frame flourishes (fuse sparks, boost dust). BombGame keeps owning movement,
/// rolling, shield and charge aura; this only replaces what they look like.
sealed class BombActorView : MonoBehaviour {
    static Material darkBombMaterial;
    static readonly Dictionary<Color, Material> GlowMaterials = new Dictionary<Color, Material>();

    Transform fuse;
    Vector3 fuseScale;
    ParticleSystem fuseSparks, boostDust;
    bool charging, boost;
    float charge;

    /// Fitted copy of a model under a fresh root: horizontal size = diameter, bottom at bottomY,
    /// height clamped to maxHeight (stretched flat if needed). Null when the model is missing.
    internal static GameObject Fitted(string model, float diameter, float bottomY, float maxHeight = 0) {
        GameObject prefab = BombArt.Model(model);
        if (prefab == null) return null;
        var root = new GameObject(prefab.name);
        GameObject instance = Instantiate(prefab, root.transform, false);
        foreach (Collider collider in instance.GetComponentsInChildren<Collider>()) Destroy(collider);
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return root;
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
        float scale = diameter / Mathf.Max(.001f, Mathf.Max(bounds.size.x, bounds.size.z));
        float scaleY = maxHeight > 0 ? Mathf.Min(scale, maxHeight / Mathf.Max(.001f, bounds.size.y)) : scale;
        instance.transform.localScale = Vector3.Scale(instance.transform.localScale, new Vector3(scale, scaleY, scale));
        instance.transform.localPosition = new Vector3(-bounds.center.x * scale, bottomY - bounds.min.y * scaleY, -bounds.center.z * scale);
        foreach (Renderer r in renderers) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        return root;
    }

    /// Black toy bomb with white eyes and the player's color as a band. Returns false when the
    /// model is missing so BombGame builds its primitive version instead.
    public static bool BuildBody(GameObject body, Color playerColor, out Transform rollingPivot) {
        rollingPivot = null;
        GameObject model = Fitted("Platformer/bomb", 1.02f, -.5f);
        if (model == null) return false;
        model.name = "Bomb model";
        model.transform.SetParent(body.transform, false);
        Material dark = DarkBombMaterial(model);
        if (dark != null) foreach (Renderer r in model.GetComponentsInChildren<Renderer>()) r.sharedMaterial = dark;
        Bounds bounds = LocalBounds(body.transform, model);
        // The cap makes the model taller than wide; the round body's centre sits one radius up.
        float bodyCenter = bounds.min.y + bounds.size.x * .5f;

        // The rolling code spins this pivot; it carries the player-color band so movement still reads
        // as rolling while the face and fuse stay upright.
        var pivot = new GameObject("Rolling band").transform;
        pivot.SetParent(body.transform, false);
        pivot.localPosition = new Vector3(0, bodyCenter, 0);
        GameObject band = Part(PrimitiveType.Cylinder, pivot, Vector3.zero,
            new Vector3(bounds.size.x * 1.03f, .07f, bounds.size.z * 1.03f), BombGame.MaterialFor(playerColor));
        band.name = "Color band";
        rollingPivot = pivot;

        float front = bounds.min.z;
        for (int side = -1; side <= 1; side += 2) {
            GameObject eye = Part(PrimitiveType.Sphere, body.transform,
                new Vector3(side * .14f, bodyCenter + .1f, front + .05f), new Vector3(.11f, .21f, .08f),
                BombGame.MaterialFor(BombUi.Text));
            eye.name = "Eye";
            Part(PrimitiveType.Sphere, eye.transform, new Vector3(0, -.12f, -.35f), new Vector3(.55f, .5f, .5f),
                BombGame.MaterialFor(BombUi.BombBody)).name = "Pupil";
        }

        var view = body.AddComponent<BombActorView>();
        GameObject fuseObject = Part(PrimitiveType.Cylinder, body.transform, new Vector3(0, bounds.max.y + .06f, 0),
            new Vector3(.07f, .1f, .07f), BombGame.MaterialFor(new Color(.45f, .33f, .24f)));
        fuseObject.name = "Fuse";
        view.fuse = fuseObject.transform;
        view.fuseScale = view.fuse.localScale;
        view.fuseSparks = BombVfx.Emitter(body.transform, new Vector3(0, bounds.max.y + .2f, 0), new BombVfx.Spec {
            sprite = "spark_01", lifetime = new Vector2(.15f, .35f), speed = new Vector2(.6f, 1.6f),
            size = new Vector2(.08f, .2f), color = new Color(1f, .8f, .35f), endColor = new Color(1f, .35f, .1f, 0),
            gravity = .4f, radius = .03f, growth = .3f
        });
        view.boostDust = BombVfx.Emitter(body.transform, new Vector3(0, -.42f, 0), new BombVfx.Spec {
            sprite = "dirt_01", additive = false, lifetime = new Vector2(.3f, .55f), speed = new Vector2(.2f, .7f),
            size = new Vector2(.25f, .5f), color = new Color(.85f, .8f, .78f, .6f), endColor = new Color(.8f, .78f, .75f, 0),
            radius = .3f, hemisphere = true, growth = 1.8f
        });
        return true;
    }

    /// Energy (small, lime star) and Core (big, magenta jewel): glowing model that bobs and spins
    /// over a soft light pool on the ground. The root sits at the pickup centre, `size` above ground.
    public static GameObject CreatePickup(float size, Color color, Transform parent) {
        bool core = size > .3f;
        GameObject model = Fitted(core ? "Platformer/jewel" : "Platformer/star", size * 2.3f, -size);
        if (model == null) return null;
        var root = new GameObject(core ? "Core" : "Energy");
        if (parent != null) root.transform.SetParent(parent, false);
        model.transform.SetParent(root.transform, false);
        Material glow = GlowMaterial(color);
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>()) {
            r.sharedMaterial = glow;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        Texture2D poolTexture = BombArt.Particle("light_01");
        if (poolTexture != null) {
            GameObject pool = Part(PrimitiveType.Quad, root.transform, new Vector3(0, -size + .02f, 0),
                Vector3.one * size * (core ? 4.5f : 4f), null);
            pool.name = "Glow";
            pool.transform.localRotation = Quaternion.Euler(90, 0, 0);
            Shader shader = Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Sprites/Default");
            var renderer = pool.GetComponent<Renderer>();
            renderer.sharedMaterial = new Material(shader) { name = BombMaterialOwner.Prefix + "pickup pool", mainTexture = poolTexture, color = BombUi.WithAlpha(color, .55f) };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        root.AddComponent<BombBob>().Begin(model.transform, core ? .14f : .09f, core ? 70 : 120);
        BombMaterialOwner.Capture(root);
        return root;
    }

    public void Apply(PlayerData state) {
        charging = state.alive && state.charging;
        charge = state.charge;
        boost = state.alive && state.boost;
    }

    void Update() {
        if (fuse != null) {
            float pulse = charging ? 1 + Mathf.Abs(Mathf.Sin(Time.time * (10 + charge * 14))) * (.35f + charge * .5f) : 1;
            fuse.localScale = new Vector3(fuseScale.x * pulse, fuseScale.y, fuseScale.z * pulse);
        }
        if (fuseSparks != null) {
            ParticleSystem.EmissionModule emission = fuseSparks.emission;
            emission.rateOverTime = charging ? 28 + charge * 50 : 5;
        }
        if (boostDust != null) {
            ParticleSystem.EmissionModule emission = boostDust.emission;
            emission.rateOverTime = boost ? 36 : 0;
        }
    }

    static Bounds LocalBounds(Transform space, GameObject model) {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
        var bounds = new Bounds(Vector3.zero, Vector3.one);
        bool first = true;
        foreach (Renderer r in renderers) {
            Bounds world = r.bounds;
            Vector3 min = space.InverseTransformPoint(world.min), max = space.InverseTransformPoint(world.max);
            var local = new Bounds((min + max) / 2, new Vector3(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y), Mathf.Abs(max.z - min.z)));
            if (first) { bounds = local; first = false; } else bounds.Encapsulate(local);
        }
        return bounds;
    }

    static GameObject Part(PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 localScale, Material material) {
        GameObject item = GameObject.CreatePrimitive(type);
        Destroy(item.GetComponent<Collider>());
        item.transform.SetParent(parent, false);
        item.transform.localPosition = localPosition;
        item.transform.localScale = localScale;
        if (material != null) item.GetComponent<Renderer>().sharedMaterial = material;
        return item;
    }

    /// Emissive flat-colour material shared by every pickup of that colour.
    internal static Material GlowMaterial(Color color) {
        if (GlowMaterials.TryGetValue(color, out Material cached)) return cached;
        Material material = BombGame.MaterialFor(color);
        material.name = "Shared pickup glow"; // Cached across rounds, owned by the cache.
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", color * .75f);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", .8f);
        GlowMaterials[color] = material;
        return material;
    }

    /// The kit's bomb samples the blue swatches of its colour atlas; repaint those near-black so the
    /// body matches the concept while the orange cap keeps its colour. The atlas is not CPU-readable,
    /// so it is copied through a RenderTexture once.
    static Material DarkBombMaterial(GameObject model) {
        if (darkBombMaterial != null) return darkBombMaterial;
        Renderer renderer = model.GetComponentInChildren<Renderer>();
        if (renderer == null || renderer.sharedMaterial == null) return null;
        Material source = renderer.sharedMaterial;
        Texture atlas = source.mainTexture;
        darkBombMaterial = new Material(source) { name = "Bomb body (dark)" };
        if (atlas == null) {
            darkBombMaterial.color = BombUi.BombBody;
            return darkBombMaterial;
        }
        RenderTexture temporary = RenderTexture.GetTemporary(atlas.width, atlas.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(atlas, temporary);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = temporary;
        var copy = new Texture2D(atlas.width, atlas.height, TextureFormat.RGBA32, false) {
            filterMode = atlas.filterMode, wrapMode = TextureWrapMode.Clamp, name = "colormap (dark bomb)"
        };
        copy.ReadPixels(new Rect(0, 0, atlas.width, atlas.height), 0, 0);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(temporary);
        Color[] pixels = copy.GetPixels();
        for (int i = 0; i < pixels.Length; i++) {
            Color.RGBToHSV(pixels[i], out float h, out float s, out float v);
            if (h < .52f || h > .8f || s < .12f) continue;
            float g = Mathf.Lerp(.06f, .24f, v);
            pixels[i] = new Color(g, g * .95f, g * 1.12f, pixels[i].a);
        }
        copy.SetPixels(pixels);
        copy.Apply();
        darkBombMaterial.mainTexture = copy;
        if (darkBombMaterial.HasProperty("_Glossiness")) darkBombMaterial.SetFloat("_Glossiness", .55f);
        return darkBombMaterial;
    }
}

/// Spins and bobs a child model around its root (pickups), each with its own phase.
sealed class BombBob : MonoBehaviour {
    Transform target;
    Vector3 basePosition;
    float height, degreesPerSecond, phase;

    public void Begin(Transform model, float bobHeight, float spinSpeed) {
        target = model;
        basePosition = model.localPosition;
        height = bobHeight;
        degreesPerSecond = spinSpeed;
        phase = Random.value * Mathf.PI * 2;
    }

    void Update() {
        if (target == null) return;
        target.localPosition = basePosition + Vector3.up * (Mathf.Sin(Time.time * 2.4f + phase) * height + height);
        target.localRotation = Quaternion.Euler(0, Time.time * degreesPerSecond + phase * Mathf.Rad2Deg, 0);
    }
}
