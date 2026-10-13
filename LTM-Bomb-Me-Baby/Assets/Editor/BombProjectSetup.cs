using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Builds an editable authoring scene. Dynamic network entities are placed under dedicated roots at runtime.
[InitializeOnLoad]
public static class BombProjectSetup {
    const string ScenePath = "Assets/Scenes/Main.unity";
    const string MaterialsPath = "Assets/Materials";

    static BombProjectSetup() {
        EditorApplication.delayCall += EnsureMainScene;
    }

    [MenuItem("Tools/Bomb Me Baby/Rebuild Main Scene")]
    public static void RebuildMainScene() {
        if (EditorApplication.isPlayingOrWillChangePlaymode) {
            Debug.LogWarning("Stop Play Mode before rebuilding the Bomb Me Baby scene.");
            return;
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        BuildScene(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        SetBuildScene();
        Selection.activeGameObject = GameObject.Find("Bomb Me Baby");
        Debug.Log("Bomb Me Baby authoring scene rebuilt at " + ScenePath);
    }

    static void EnsureMainScene() {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        Directory.CreateDirectory("Assets/Scenes");
        Scene scene;
        if (!File.Exists(ScenePath)) {
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildScene(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
        } else {
            scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (GameObject.Find("Bomb Me Baby") == null) {
                BuildScene(scene);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
        }
        SetBuildScene();
    }

    static void BuildScene(Scene scene) {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.55f, .62f, .72f);
        RenderSettings.fog = false;

        Material floorMaterial = GetOrCreateMaterial("Arena Floor", new Color(.08f, .16f, .24f));
        Material wallMaterial = GetOrCreateMaterial("Arena Wall", new Color(.16f, .34f, .48f));
        Material obstacleMaterial = GetOrCreateMaterial("Obstacle", new Color(.38f, .42f, .50f));
        Material bushDarkMaterial = GetOrCreateMaterial("Bush Dark", new Color(.10f, .36f, .16f));
        Material bushMaterial = GetOrCreateMaterial("Bush", new Color(.17f, .52f, .22f));
        Material bushLightMaterial = GetOrCreateMaterial("Bush Light", new Color(.28f, .68f, .31f));
        Material spawnMaterial = GetOrCreateMaterial("Spawn Marker", new Color(.14f, .62f, .92f));

        GameObject root = NewObject("Bomb Me Baby");
        GameObject systems = NewObject("Systems", root.transform);
        systems.AddComponent<BombGame>();

        GameObject presentation = NewObject("Presentation", root.transform);
        CreateCamera(presentation.transform);
        CreateLight(presentation.transform);

        GameObject environment = NewObject("Environment", root.transform);
        CreatePrimitive("Arena Floor", PrimitiveType.Cube, environment.transform,
            new Vector3(0, -.2f, 0), new Vector3(41, .3f, 41), floorMaterial);

        GameObject walls = NewObject("Walls", environment.transform);
        CreatePrimitive("West Wall", PrimitiveType.Cube, walls.transform,
            new Vector3(-20.7f, .6f, 0), new Vector3(.4f, 1.2f, 41.8f), wallMaterial);
        CreatePrimitive("East Wall", PrimitiveType.Cube, walls.transform,
            new Vector3(20.7f, .6f, 0), new Vector3(.4f, 1.2f, 41.8f), wallMaterial);
        CreatePrimitive("South Wall", PrimitiveType.Cube, walls.transform,
            new Vector3(0, .6f, -20.7f), new Vector3(41.8f, 1.2f, .4f), wallMaterial);
        CreatePrimitive("North Wall", PrimitiveType.Cube, walls.transform,
            new Vector3(0, .6f, 20.7f), new Vector3(41.8f, 1.2f, .4f), wallMaterial);

        GameObject obstacles = NewObject("Obstacles", environment.transform);
        CreateObstacle("Rock 1", obstacles.transform, new Vector3(-5, 0, -3), 1.3f, obstacleMaterial);
        CreateObstacle("Rock 2", obstacles.transform, new Vector3(5, 0, 4), 1.4f, obstacleMaterial);
        CreateObstacle("Rock 3", obstacles.transform, new Vector3(-6, 0, 7), 1.1f, obstacleMaterial);
        CreateObstacle("Rock 4", obstacles.transform, new Vector3(7, 0, -6), 1.2f, obstacleMaterial);

        GameObject bushes = NewObject("Bushes", environment.transform);
        Material[] bushMaterials = { bushDarkMaterial, bushMaterial, bushLightMaterial };
        CreateBush("Bush 1", bushes.transform, new Vector3(-11, 0, -6), 1.4f, bushMaterials);
        CreateBush("Bush 2", bushes.transform, new Vector3(-10, 0, 10), 1.6f, bushMaterials);
        CreateBush("Bush 3", bushes.transform, new Vector3(11, 0, 9), 1.5f, bushMaterials);
        CreateBush("Bush 4", bushes.transform, new Vector3(12, 0, -10), 1.7f, bushMaterials);
        CreateBush("Bush 5", bushes.transform, new Vector3(0, 0, 10), 1.3f, bushMaterials);
        CreateBush("Bush 6", bushes.transform, new Vector3(0, 0, -11), 1.3f, bushMaterials);

        GameObject spawns = NewObject("Spawn Points", environment.transform);
        Vector3[] spawnPositions = {
            new Vector3(-16, .02f, -16), new Vector3(16, .02f, 16),
            new Vector3(-16, .02f, 16), new Vector3(16, .02f, -16)
        };
        for (int i = 0; i < spawnPositions.Length; i++) {
            GameObject marker = CreatePrimitive("Spawn " + (i + 1), PrimitiveType.Cylinder, spawns.transform,
                spawnPositions[i], new Vector3(.85f, .03f, .85f), spawnMaterial);
            Collider collider = marker.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
        }

        GameObject dynamicObjects = NewObject("Dynamic Objects", root.transform);
        NewObject("Players", dynamicObjects.transform);
        NewObject("Pickups", dynamicObjects.transform);
        NewObject("Effects", dynamicObjects.transform);
        NewObject("Game UI Runtime IMGUI", root.transform);

        EditorSceneManager.MarkSceneDirty(scene);
    }

    static void CreateCamera(Transform parent) {
        GameObject cameraObject = NewObject("Arena Camera", parent);
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 12.5f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.035f, .055f, .085f);
        cameraObject.transform.position = new Vector3(0, 22, -22);
        cameraObject.transform.LookAt(Vector3.zero);
        cameraObject.AddComponent<AudioListener>();
    }

    static void CreateLight(Transform parent) {
        GameObject lightObject = NewObject("Sun", parent);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.shadows = LightShadows.Soft;
        lightObject.transform.rotation = Quaternion.Euler(45, -35, 0);
        RenderSettings.sun = light;
    }

    static void CreateObstacle(string name, Transform parent, Vector3 position, float radius, Material material) {
        CreatePrimitive(name, PrimitiveType.Cylinder, parent, position + Vector3.up * .65f,
            new Vector3(radius * 2, .65f, radius * 2), material);
    }

    static void CreateBush(string name, Transform parent, Vector3 position, float radius, Material[] materials) {
        GameObject root = NewObject(name, parent);
        root.transform.position = position;
        Vector3[] offsets = {
            new Vector3(0,.55f,0), new Vector3(-.55f,.42f,.15f),
            new Vector3(.5f,.45f,.25f), new Vector3(.05f,.4f,-.55f)
        };
        for (int i = 0; i < offsets.Length; i++) {
            GameObject leaf = CreatePrimitive("Leaves " + (i + 1), PrimitiveType.Sphere, root.transform,
                position + offsets[i] * radius, Vector3.one * radius * (i == 0 ? 1.15f : .9f), materials[i % materials.Length]);
            Collider collider = leaf.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
        }
    }

    static GameObject CreatePrimitive(string name, PrimitiveType type, Transform parent,
        Vector3 position, Vector3 scale, Material material) {
        GameObject gameObject = GameObject.CreatePrimitive(type);
        gameObject.name = name;
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.position = position;
        gameObject.transform.localScale = scale;
        Renderer renderer = gameObject.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = material;
        GameObjectUtility.SetStaticEditorFlags(gameObject,
            StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        return gameObject;
    }

    static GameObject NewObject(string name, Transform parent = null) {
        GameObject gameObject = new GameObject(name);
        if (parent != null) gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    static Material GetOrCreateMaterial(string name, Color color) {
        if (!AssetDatabase.IsValidFolder(MaterialsPath)) AssetDatabase.CreateFolder("Assets", "Materials");
        string assetPath = MaterialsPath + "/" + name.Replace(" ", "") + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (material != null) return material;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        material = new Material(shader) { name = name, color = color };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(material, assetPath);
        return material;
    }

    static void SetBuildScene() {
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
    }
}
