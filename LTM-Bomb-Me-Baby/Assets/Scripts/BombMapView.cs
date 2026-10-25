using System.Collections.Generic;
using UnityEngine;

/// Builds the arena for the active map from the CC0 art in Resources/Art (see BombArt):
/// a tiled ground per map, a ring of rocks as the arena edge, rock obstacles and bush clusters
/// sized to the server's collision circles, and light non-blocking decoration. Every piece falls
/// back to the old coloured primitives when its art is missing, so the game runs without it.
sealed class BombMapView {
    sealed class Style {
        public string ground;
        public float tile;
        public Color groundTint, borderTint, floorFallback, wallFallback, rockFallback;
        public string[] obstacleModels, wallModels, bushModels, decorModels;
        public Color obstacleTint, wallTint, bushTint, decorTint;
        public bool lava;
    }

    static readonly string[] RockLarge = { "rock_largeA", "rock_largeB", "rock_largeC", "rock_largeD", "rock_largeE", "rock_largeF" };
    static readonly string[] StoneLarge = { "stone_largeA", "stone_largeB", "stone_largeC", "stone_largeD", "stone_largeE", "stone_largeF" };
    static readonly string[] Bushes = { "plant_bush", "plant_bushLarge", "plant_bushDetailed", "plant_bushSmall", "plant_bushTriangle", "plant_bushLargeTriangle" };

    static readonly Style Meadow = new Style {
        ground = "meadow", tile = 4.5f,
        groundTint = new Color(.92f, 1f, .9f), borderTint = new Color(.55f, .62f, .58f),
        floorFallback = new Color(.25f, .43f, .33f), wallFallback = new Color(.42f, .37f, .50f), rockFallback = new Color(.58f, .54f, .62f),
        obstacleModels = RockLarge, obstacleTint = new Color(.9f, .86f, .92f),
        wallModels = new[] { "stone_tallA", "stone_tallB", "stone_tallC", "stone_largeA", "stone_largeC", "stone_largeE" },
        wallTint = new Color(.78f, .74f, .88f),
        bushModels = Bushes, bushTint = Color.white,
        decorModels = new[] { "grass", "grass_large", "grass_leafs", "flower_redA", "flower_yellowA", "flower_purpleA", "mushroom_red", "rock_smallA", "stone_smallA" },
        decorTint = Color.white
    };

    static readonly Style Crossroads = new Style {
        ground = "crossroads", tile = 3.5f,
        groundTint = new Color(.92f, .9f, .96f), borderTint = new Color(.5f, .48f, .56f),
        floorFallback = new Color(.46f, .38f, .44f), wallFallback = new Color(.38f, .32f, .50f), rockFallback = new Color(.60f, .55f, .66f),
        obstacleModels = StoneLarge, obstacleTint = new Color(.86f, .84f, .92f),
        wallModels = new[] { "stone_tallA", "stone_tallB", "stone_tallC", "stone_largeB", "stone_largeD", "stone_largeF" },
        wallTint = new Color(.7f, .66f, .82f),
        bushModels = Bushes, bushTint = new Color(.82f, .9f, .78f),
        decorModels = new[] { "grass", "grass_leafs", "stone_smallA", "stone_smallC", "rock_smallC", "mushroom_tan" },
        decorTint = new Color(.9f, .9f, .9f)
    };

    static readonly Style Crater = new Style {
        ground = "crater", tile = 3.5f,
        groundTint = new Color(1f, .78f, .72f), borderTint = new Color(.42f, .28f, .26f),
        floorFallback = new Color(.40f, .24f, .25f), wallFallback = new Color(.46f, .32f, .36f), rockFallback = new Color(.56f, .44f, .48f),
        obstacleModels = RockLarge, obstacleTint = new Color(.55f, .36f, .36f),
        wallModels = new[] { "rock_tallA", "rock_tallB", "rock_tallC", "rock_largeB", "rock_largeD", "rock_largeF" },
        wallTint = new Color(.48f, .32f, .32f),
        bushModels = Bushes, bushTint = new Color(.85f, .62f, .38f),
        decorModels = new[] { "rock_smallA", "rock_smallC", "stone_smallC", "grass" },
        decorTint = new Color(.7f, .5f, .4f),
        lava = true
    };

    static readonly Dictionary<(Material, Color), Material> TintCache = new Dictionary<(Material, Color), Material>();
    static readonly Dictionary<Material, Material> FadeCache = new Dictionary<Material, Material>();

    const float PedestalClearRadius = 4.5f;
    const float BorderWidth = 24;

    GameObject root;
    readonly List<Renderer[]> bushRenderers = new List<Renderer[]>();
    readonly List<Material[][]> bushMaterials = new List<Material[][]>();
    int fadedBush = -1;

    public GameObject Build(string mapId, float arenaSize, ObstacleData[] obstacles, BushData[] bushes,
                            string rootName = "Runtime Map - ", Transform parent = null) {
        Clear();
        root = new GameObject(rootName + mapId);
        if (parent != null) root.transform.SetParent(parent, false);
        Style style = mapId == "crater" ? Crater : mapId == "crossroads" ? Crossroads : Meadow;
        int seed = 17;
        foreach (char c in mapId) seed = seed * 31 + c;   // string.GetHashCode is not stable across runs
        var random = new System.Random(seed);

        BuildGround(style, arenaSize);
        BuildEdge(style, arenaSize, random);
        foreach (ObstacleData obstacle in obstacles) BuildObstacle(style, obstacle);
        foreach (BushData bush in bushes) BuildBush(style, bush);
        BuildDecor(style, arenaSize, obstacles, bushes, random);
        if (style.lava) BuildLava(arenaSize, random);
        BombMaterialOwner.Capture(root);
        return root;
    }

    public void Clear() {
        if (root != null) BombArt.Discard(root);
        root = null;
        bushRenderers.Clear();
        bushMaterials.Clear();
        fadedBush = -1;
    }

    /// Fades the bush the local player is hiding in so they can still see their bomb; -1 restores all.
    public void SetLocalBush(int index) {
        if (index == fadedBush) return;
        if (fadedBush >= 0 && fadedBush < bushRenderers.Count) SetBushFaded(fadedBush, false);
        fadedBush = index;
        if (fadedBush >= 0 && fadedBush < bushRenderers.Count) SetBushFaded(fadedBush, true);
    }

    void SetBushFaded(int index, bool faded) {
        Renderer[] renderers = bushRenderers[index];
        Material[][] originals = bushMaterials[index];
        for (int i = 0; i < renderers.Length; i++) {
            if (renderers[i] == null) continue;
            if (!faded) { renderers[i].sharedMaterials = originals[i]; continue; }
            var fadedMaterials = new Material[originals[i].Length];
            for (int m = 0; m < fadedMaterials.Length; m++) fadedMaterials[m] = Faded(originals[i][m]);
            renderers[i].sharedMaterials = fadedMaterials;
        }
    }

    void BuildGround(Style style, float arenaSize) {
        float diameter = arenaSize * 2 + 1;
        float outer = diameter + BorderWidth * 2;
        GameObject floor = Primitive("Arena floor", PrimitiveType.Cube, new Vector3(0, -.2f, 0), new Vector3(diameter, .3f, diameter), style.floorFallback);
        GameObject border = Primitive("Arena surroundings", PrimitiveType.Cube, new Vector3(0, -.26f, 0), new Vector3(outer, .3f, outer),
            Color.Lerp(style.floorFallback, Color.black, .35f));
        Material ground = GroundMaterial(style.ground, style.groundTint, diameter / style.tile);
        if (ground != null) ReplaceGroundMaterial(floor, ground);
        Material surroundings = GroundMaterial(style.ground, style.borderTint, outer / style.tile);
        if (surroundings != null) ReplaceGroundMaterial(border, surroundings);
    }

    static void ReplaceGroundMaterial(GameObject target, Material material) {
        Renderer renderer = target.GetComponent<Renderer>();
        // Primitive allocated a fallback material which is no longer referenced.
        BombArt.Discard(renderer.sharedMaterial);
        renderer.sharedMaterial = material;
    }

    /// Ring of rocks just outside the play bounds. The south side faces the camera, so it stays low
    /// enough not to hide players near that edge.
    void BuildEdge(Style style, float arenaSize, System.Random random) {
        float centre = arenaSize + 1.5f;
        for (int side = 0; side < 4; side++) {
            float maxHeight = side == 0 ? 1.3f : side == 1 ? 3.2f : 2.4f;
            for (float t = -centre; t <= centre; t += 2.1f) {
                float jitter = Range(random, -.35f, .35f);
                Vector3 position;
                switch (side) {
                    case 0: position = new Vector3(t, 0, -centre + jitter); break;   // south
                    case 1: position = new Vector3(t, 0, centre + jitter); break;    // north
                    case 2: position = new Vector3(-centre + jitter, 0, t); break;   // west
                    default: position = new Vector3(centre + jitter, 0, t); break;   // east
                }
                string model = Pick(style.wallModels, random);
                if (!Place(model, position, Range(random, 2.6f, 3.4f), maxHeight, Range(random, 0, 360), style.wallTint)) {
                    BuildFallbackWalls(style, arenaSize);
                    return;
                }
            }
        }
    }

    void BuildFallbackWalls(Style style, float arenaSize) {
        float diameter = arenaSize * 2;
        Primitive("West wall", PrimitiveType.Cube, new Vector3(-arenaSize - .7f, .6f, 0), new Vector3(.5f, 1.2f, diameter + 1.8f), style.wallFallback);
        Primitive("East wall", PrimitiveType.Cube, new Vector3(arenaSize + .7f, .6f, 0), new Vector3(.5f, 1.2f, diameter + 1.8f), style.wallFallback);
        Primitive("South wall", PrimitiveType.Cube, new Vector3(0, .6f, -arenaSize - .7f), new Vector3(diameter + 1.8f, 1.2f, .5f), style.wallFallback);
        Primitive("North wall", PrimitiveType.Cube, new Vector3(0, .6f, arenaSize + .7f), new Vector3(diameter + 1.8f, 1.2f, .5f), style.wallFallback);
    }

    void BuildObstacle(Style style, ObstacleData obstacle) {
        var random = new System.Random(Seed(obstacle.x, obstacle.z));
        Vector3 position = new Vector3(obstacle.x, 0, obstacle.z);
        if (Place(Pick(style.obstacleModels, random), position, obstacle.radius * 2, obstacle.radius * 1.6f,
                Range(random, 0, 360), style.obstacleTint))
            return;
        Primitive("Rock", PrimitiveType.Cylinder, new Vector3(obstacle.x, .65f, obstacle.z),
            new Vector3(obstacle.radius * 2, .65f, obstacle.radius * 2), style.rockFallback);
    }

    /// One large bush in the middle and three smaller ones around it, together covering the circle.
    void BuildBush(Style style, BushData bush) {
        var random = new System.Random(Seed(bush.x, bush.z));
        var cluster = new GameObject("Bush");
        cluster.transform.SetParent(root.transform, false);
        Vector3 centre = new Vector3(bush.x, 0, bush.z);
        bool placed = Place(Pick(style.bushModels, random), centre, bush.radius * 1.7f, bush.radius * 1.3f,
            Range(random, 0, 360), style.bushTint, cluster.transform);
        if (placed) {
            float start = Range(random, 0, 360);
            for (int i = 0; i < 3; i++) {
                float angle = (start + i * 120 + Range(random, -20, 20)) * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * bush.radius * .55f;
                Place(Pick(style.bushModels, random), centre + offset, bush.radius * Range(random, .9f, 1.1f), bush.radius,
                    Range(random, 0, 360), style.bushTint, cluster.transform);
            }
        } else {
            Color[] colors = { new Color(.12f, .42f, .20f), new Color(.18f, .55f, .25f), new Color(.28f, .66f, .30f) };
            Vector3[] offsets = {
                new Vector3(0, .55f, 0), new Vector3(-.55f, .42f, .15f),
                new Vector3(.5f, .45f, .25f), new Vector3(.05f, .4f, -.55f)
            };
            for (int i = 0; i < offsets.Length; i++)
                Primitive("Leaves", PrimitiveType.Sphere, centre + offsets[i] * bush.radius,
                    Vector3.one * bush.radius * (i == 0 ? 1.15f : .9f), colors[i % colors.Length], cluster.transform);
        }
        Renderer[] renderers = cluster.GetComponentsInChildren<Renderer>();
        var materials = new Material[renderers.Length][];
        for (int i = 0; i < renderers.Length; i++) materials[i] = renderers[i].sharedMaterials;
        bushRenderers.Add(renderers);
        bushMaterials.Add(materials);
    }

    /// Small grass, flowers and pebbles that never touch obstacles, bushes or the Journey pedestal.
    void BuildDecor(Style style, float arenaSize, ObstacleData[] obstacles, BushData[] bushes, System.Random random) {
        int count = Mathf.RoundToInt(arenaSize * arenaSize * 4 / 90);
        float limit = arenaSize - 1;
        for (int i = 0, attempts = 0; i < count && attempts < count * 6; attempts++) {
            Vector3 position = new Vector3(Range(random, -limit, limit), 0, Range(random, -limit, limit));
            if (!IsClear(position, obstacles, bushes)) continue;
            i++;
            if (!Place(Pick(style.decorModels, random), position, Range(random, .45f, 1.1f), .6f, Range(random, 0, 360), style.decorTint))
                return;
        }
    }

    static bool IsClear(Vector3 p, ObstacleData[] obstacles, BushData[] bushes) {
        if (p.x * p.x + p.z * p.z < PedestalClearRadius * PedestalClearRadius) return false;
        foreach (ObstacleData o in obstacles)
            if (Sq(p.x - o.x) + Sq(p.z - o.z) < Sq(o.radius + 1)) return false;
        foreach (BushData b in bushes)
            if (Sq(p.x - b.x) + Sq(p.z - b.z) < Sq(b.radius + .8f)) return false;
        return true;
    }

    /// Glowing lava pools on the crater's outer rim, outside the play bounds.
    void BuildLava(float arenaSize, System.Random random) {
        Material lava = Resources.Load<Material>("Art/Materials/Lava");
        if (lava == null) {
            Texture2D color = BombArt.Ground("lava", "color");
            if (color == null) return;
            lava = new Material(Shader.Find("Standard")) { name = BombMaterialOwner.Prefix + "lava", mainTexture = color };
            Texture2D emission = BombArt.Ground("lava", "emission");
            if (emission != null) {
                lava.SetTexture("_EmissionMap", emission);
                lava.SetColor("_EmissionColor", Color.white * 1.4f);
                lava.EnableKeyword("_EMISSION");
            }
        }
        for (int i = 0; i < 14; i++) {
            float angle = Range(random, 0, Mathf.PI * 2);
            // Points on the square ring between the rock edge and the end of the surroundings.
            float distance = arenaSize + Range(random, 5, 14);
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            direction /= Mathf.Max(Mathf.Abs(direction.x), Mathf.Abs(direction.z));
            GameObject pool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pool.name = "Lava pool";
            BombArt.Discard(pool.GetComponent<Collider>());
            pool.transform.SetParent(root.transform, false);
            pool.transform.position = direction * distance + Vector3.up * -.09f;
            float size = Range(random, 3, 7);
            pool.transform.localScale = new Vector3(size, .01f, size * Range(random, .5f, 1));
            pool.transform.rotation = Quaternion.Euler(0, Range(random, 0, 360), 0);
            pool.GetComponent<Renderer>().sharedMaterial = lava;
        }
    }

    /// Instantiates a model so its footprint (largest horizontal size) is `footprint`, its height is
    /// at most `maxHeight`, and its base sits on the ground at `position`. False when the art is missing.
    bool Place(string model, Vector3 position, float footprint, float maxHeight, float yaw, Color tint, Transform parent = null) {
        GameObject item = BombArt.Spawn("Nature/" + model, parent != null ? parent : root.transform);
        if (item == null) return false;
        Transform t = item.transform;
        t.position = Vector3.zero;
        t.rotation = Quaternion.Euler(0, yaw, 0);
        t.localScale = Vector3.one;
        Renderer[] renderers = item.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) { BombArt.Discard(item); return false; }
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        float size = Mathf.Max(bounds.size.x, bounds.size.z);
        if (size < .0001f) { BombArt.Discard(item); return false; }
        float scale = footprint / size;
        float height = bounds.size.y * scale;
        float scaleY = height > maxHeight ? scale * maxHeight / height : scale;
        t.localScale = new Vector3(scale, scaleY, scale);
        t.position = position - new Vector3(bounds.center.x * scale, bounds.min.y * scaleY, bounds.center.z * scale);
        if (tint != Color.white) foreach (Renderer r in renderers) {
            Material[] materials = r.sharedMaterials;
            for (int m = 0; m < materials.Length; m++) materials[m] = Tinted(materials[m], tint);
            r.sharedMaterials = materials;
        }
        return true;
    }

    GameObject Primitive(string name, PrimitiveType type, Vector3 position, Vector3 scale, Color color, Transform parent = null) {
        GameObject item = GameObject.CreatePrimitive(type);
        item.name = name;
        BombArt.Discard(item.GetComponent<Collider>());
        item.transform.SetParent(parent != null ? parent : root.transform, false);
        item.transform.position = position;
        item.transform.localScale = scale;
        item.GetComponent<Renderer>().sharedMaterial = BombGame.MaterialFor(color);
        return item;
    }

    /// Ground material with world-space tiling. Prefers the prebuilt asset (made by BombArtImport so
    /// player builds keep the normal-map shader variant), otherwise builds one from the textures.
    static Material GroundMaterial(string mapId, Color tint, float tiling) {
        Material source = Resources.Load<Material>("Art/Materials/Ground_" + mapId);
        Material material;
        if (source != null) material = new Material(source);
        else {
            Texture2D color = BombArt.Ground(mapId, "color");
            if (color == null) return null;
            material = new Material(Shader.Find("Standard")) { mainTexture = color };
            Texture2D normal = BombArt.Ground(mapId, "normal");
            if (normal != null) {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }
            material.SetFloat("_Glossiness", .08f);
        }
        material.name = BombMaterialOwner.Prefix + "ground";
        material.color = tint;
        material.mainTextureScale = new Vector2(tiling, tiling);
        return material;
    }

    static Material Tinted(Material source, Color tint) {
        if (source == null) return null;
        if (TintCache.TryGetValue((source, tint), out Material cached)) return cached;
        var material = new Material(source) { color = source.color * tint };
        TintCache[(source, tint)] = material;
        return material;
    }

    static Material Faded(Material source) {
        if (source == null) return null;
        if (FadeCache.TryGetValue(source, out Material cached)) return cached;
        var material = new Material(source);
        material.SetFloat("_Mode", 3);
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.renderQueue = 3000;
        Color color = material.color;
        color.a = .35f;
        material.color = color;
        FadeCache[source] = material;
        return material;
    }

    static int Seed(float x, float z) => Mathf.RoundToInt(x * 73.856f) * 19349663 ^ Mathf.RoundToInt(z * 19.349f) * 83492791;
    static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);
    static string Pick(string[] options, System.Random random) => options[random.Next(options.Length)];
    static float Sq(float v) => v * v;
}
