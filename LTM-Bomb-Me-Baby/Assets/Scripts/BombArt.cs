using System.Collections.Generic;
using UnityEngine;

/// Loads the CC0 art under Resources/Art (Kenney kits, ambientCG ground textures; see
/// docs/ASSET_CREDITS.md) and caches it. Every getter returns null when the file is missing,
/// so callers keep their primitive fallback and the game still runs without the art.
static class BombArt {
    static readonly Dictionary<string, Object> Cache = new Dictionary<string, Object>();

    static T Load<T>(string path) where T : Object {
        if (Cache.TryGetValue(path, out Object cached)) return cached as T;
        T asset = Resources.Load<T>("Art/" + path);
        Cache[path] = asset;
        return asset;
    }

    /// Model prefab, e.g. Model("Nature/plant_bushLarge") or Model("Platformer/bomb").
    public static GameObject Model(string name) => Load<GameObject>("Models/" + name);

    /// Instance of a model under parent with colliders stripped (the server owns collision).
    public static GameObject Spawn(string name, Transform parent) {
        GameObject prefab = Model(name);
        if (prefab == null) return null;
        GameObject instance = Object.Instantiate(prefab, parent, false);
        instance.name = prefab.name;
        foreach (Collider collider in instance.GetComponentsInChildren<Collider>()) Discard(collider);
        return instance;
    }

    /// Destroy that also works outside Play mode, where the map preview builds the same art.
    public static void Discard(Object target) {
        if (target == null) return;
        if (Application.isPlaying) Object.Destroy(target);
        else Object.DestroyImmediate(target);
    }

    /// Ground texture per map: Ground("meadow", "color") or Ground("crater", "normal").
    public static Texture2D Ground(string mapId, string channel) => Load<Texture2D>($"Ground/{mapId}_{channel}");

    /// Particle sprite from the Kenney Particle Pack, e.g. Particle("smoke_04").
    public static Texture2D Particle(string name) => Load<Texture2D>("Particles/" + name);

    /// Kenney UI Pack image, e.g. Ui("Yellow", "button_rectangle_depth_flat").
    public static Texture2D Ui(string color, string name) => Load<Texture2D>($"UI/{color}/{name}");
}
