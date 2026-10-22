using UnityEditor;
using UnityEngine;

/// Import rules for the CC0 art in Resources/Art: ground textures tile (normal maps marked as such),
/// UI images and particle sprites are clamped with transparent alpha, models skip colliders/animation.
/// Also builds the ground/lava material assets that BombMapView prefers, so player builds keep the
/// normal-map and emission shader variants (runtime-only materials would have them stripped).
sealed class BombArtImport : AssetPostprocessor {
    const string Root = "Assets/Resources/Art/";
    const string MaterialsFolder = Root + "Materials";

    void OnPreprocessTexture() {
        if (assetPath.StartsWith(Root)) Configure((TextureImporter)assetImporter, assetPath);
    }

    void OnPreprocessModel() {
        if (!assetPath.StartsWith(Root)) return;
        var importer = (ModelImporter)assetImporter;
        importer.addCollider = false;
        importer.importAnimation = false;
        importer.animationType = ModelImporterAnimationType.None;
        importer.importCameras = false;
        importer.importLights = false;
    }

    /// Applies the rules; returns true when a setting changed (the texture then needs a reimport).
    static bool Configure(TextureImporter importer, string path) {
        bool ground = path.Contains("/Ground/");
        bool ui = path.Contains("/UI/");
        bool particle = path.Contains("/Particles/");
        if (!ground && !ui && !particle) return false;
        TextureImporterType type = ground && path.Contains("_normal") ? TextureImporterType.NormalMap : TextureImporterType.Default;
        TextureWrapMode wrap = ground ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        bool mipmaps = !ui;
        bool alpha = ui || particle;
        // UI images are 9-sliced at their real size: no power-of-two stretch, no compression blur.
        TextureImporterNPOTScale npot = ui ? TextureImporterNPOTScale.None : TextureImporterNPOTScale.ToNearest;
        TextureImporterCompression compression = ui ? TextureImporterCompression.Uncompressed : TextureImporterCompression.Compressed;
        if (importer.textureType == type && importer.wrapMode == wrap && importer.mipmapEnabled == mipmaps
            && importer.alphaIsTransparency == alpha && importer.npotScale == npot
            && importer.textureCompression == compression) return false;
        importer.textureType = type;
        importer.wrapMode = wrap;
        importer.mipmapEnabled = mipmaps;
        importer.alphaIsTransparency = alpha;
        importer.npotScale = npot;
        importer.textureCompression = compression;
        return true;
    }

    /// The art was copied in while the Editor was open, so fix textures imported before these rules existed.
    [InitializeOnLoadMethod]
    static void FixExistingArt() {
        EditorApplication.delayCall += () => {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/Art" })) {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is TextureImporter importer && Configure(importer, path))
                    importer.SaveAndReimport();
            }
            foreach (string map in new[] { "meadow", "crossroads", "crater" })
                EnsureMaterial("Ground_" + map, $"Ground/{map}_color.jpg", $"Ground/{map}_normal.jpg", null);
            EnsureMaterial("Lava", "Ground/lava_color.jpg", null, "Ground/lava_emission.jpg");
            EnsureShaderAnchor("ParticleAdditive", "Legacy Shaders/Particles/Additive");        };
    }

    /// BombVfx finds particle shaders by name at runtime; a material in Resources that uses
    /// the shader makes sure the player build ships it.
    static void EnsureShaderAnchor(string name, string shaderName) {
        string path = MaterialsFolder + "/" + name + ".mat";
        Shader shader = Shader.Find(shaderName);
        if (shader == null || AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
        if (!AssetDatabase.IsValidFolder(MaterialsFolder)) AssetDatabase.CreateFolder("Assets/Resources/Art", "Materials");
        AssetDatabase.CreateAsset(new Material(shader) { name = name }, path);
    }

    static void EnsureMaterial(string name, string color, string normal, string emission) {
        string path = MaterialsFolder + "/" + name + ".mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
        var colorMap = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + color);
        if (colorMap == null) return;
        if (!AssetDatabase.IsValidFolder(MaterialsFolder)) AssetDatabase.CreateFolder("Assets/Resources/Art", "Materials");
        var material = new Material(Shader.Find("Standard")) { name = name, mainTexture = colorMap };
        material.SetFloat("_Glossiness", .08f);
        var normalMap = normal != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(Root + normal) : null;
        if (normalMap != null) {
            material.SetTexture("_BumpMap", normalMap);
            material.EnableKeyword("_NORMALMAP");
        }
        var emissionMap = emission != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(Root + emission) : null;
        if (emissionMap != null) {
            material.SetTexture("_EmissionMap", emissionMap);
            material.SetColor("_EmissionColor", Color.white * 1.4f);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        AssetDatabase.CreateAsset(material, path);
    }
}
