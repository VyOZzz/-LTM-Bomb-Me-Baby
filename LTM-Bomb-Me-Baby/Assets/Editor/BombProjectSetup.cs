using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Builds an editable authoring scene. Dynamic network entities are placed under dedicated roots at runtime.
[InitializeOnLoad]
public static class BombProjectSetup {
    const string ScenePath = "Assets/Scenes/Main.unity";

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

    [MenuItem("Tools/Bomb Me Baby/Map Preview/Show Meadow")]
    static void PreviewMeadow() => ShowMapPreview("meadow");

    [MenuItem("Tools/Bomb Me Baby/Map Preview/Show Crossroads")]
    static void PreviewCrossroads() => ShowMapPreview("crossroads");

    [MenuItem("Tools/Bomb Me Baby/Map Preview/Show Crater")]
    static void PreviewCrater() => ShowMapPreview("crater");

    [MenuItem("Tools/Bomb Me Baby/Map Preview/Rebuild Visible Preview")]
    static void RebuildVisiblePreview() {
        GameObject environment = GameObject.Find("Environment");
        if (environment == null) return;
        foreach (Transform child in environment.transform) {
            if (!child.gameObject.activeSelf || !child.name.StartsWith("Map Preview - ")) continue;
            RebuildMapPreview(child.name.Substring("Map Preview - ".Length), true);
            return;
        }
        RebuildMapPreview("meadow", true);
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
            } else {
                GameObject environment = GameObject.Find("Environment");
                if (environment != null && EnsureMapPreviews(environment.transform)) {
                    EditorSceneManager.SaveScene(scene, ScenePath);
                }
            }
        }
        SetBuildScene();
    }

    /// Older scenes kept a hand-made placeholder arena that never matched the real maps.
    static bool UpgradeEnvironment() {
        GameObject environment = GameObject.Find("Environment");
        if (environment == null || environment.GetComponent<BombMapPreview>() != null) return false;
        for (int i = environment.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(environment.transform.GetChild(i).gameObject);
        environment.AddComponent<BombMapPreview>();
        Debug.Log("Bomb Me Baby: Environment now previews the real map layout.");
        return true;
    }

    static void BuildScene(Scene scene) {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.55f, .62f, .72f);
        RenderSettings.fog = false;

        GameObject root = NewObject("Bomb Me Baby");
        GameObject systems = NewObject("Systems", root.transform);
        systems.AddComponent<BombGame>();

        GameObject presentation = NewObject("Presentation", root.transform);
        CreateCamera(presentation.transform);
        CreateLight(presentation.transform);

        GameObject environment = NewObject("Environment", root.transform);
        EnsureMapPreviews(environment.transform);

        GameObject dynamicObjects = NewObject("Dynamic Objects", root.transform);
        NewObject("Players", dynamicObjects.transform);
        NewObject("Pickups", dynamicObjects.transform);
        NewObject("Effects", dynamicObjects.transform);
        NewObject("Game UI Runtime IMGUI", root.transform);

        EditorSceneManager.MarkSceneDirty(scene);
    }

    static void ShowMapPreview(string mapId) {
        if (EditorApplication.isPlayingOrWillChangePlaymode) {
            Debug.LogWarning("Stop Play Mode before changing the map preview.");
            return;
        }
        GameObject environment = GameObject.Find("Environment");
        if (environment == null) {
            Debug.LogError("Cannot show map preview: the active scene has no Environment object.");
            return;
        }
        EnsureMapPreviews(environment.transform);
        GameObject selected = null;
        foreach (Transform child in environment.transform) {
            bool active = child.name == "Map Preview - " + mapId;
            if (child.name.StartsWith("Map Preview - ")) child.gameObject.SetActive(active);
            if (active) selected = child.gameObject;
        }
        EditorSceneManager.MarkSceneDirty(environment.scene);
        Selection.activeGameObject = selected;
        SceneView.lastActiveSceneView?.FrameSelected();
    }

    static void RebuildMapPreview(string mapId, bool recordUndo) {
        if (EditorApplication.isPlayingOrWillChangePlaymode) {
            Debug.LogWarning("Stop Play Mode before changing the map preview.");
            return;
        }

        GameObject environment = GameObject.Find("Environment");
        if (environment == null) {
            Debug.LogError("Cannot build map preview: the active scene has no Environment object.");
            return;
        }

        Transform existing = environment.transform.Find("Map Preview - " + mapId);
        if (existing != null) {
            if (recordUndo) Undo.DestroyObjectImmediate(existing.gameObject);
            else Object.DestroyImmediate(existing.gameObject);
        }

        GameObject preview = BuildMapPreview(environment.transform, mapId);
        foreach (Transform child in environment.transform)
            if (child.name.StartsWith("Map Preview - ")) child.gameObject.SetActive(child.gameObject == preview);
        if (recordUndo) Undo.RegisterCreatedObjectUndo(preview, "Build " + mapId + " map preview");
        EditorSceneManager.MarkSceneDirty(environment.scene);
        Selection.activeGameObject = preview;
        SceneView.lastActiveSceneView?.FrameSelected();
        Debug.Log("Scene preview now matches the runtime " + mapId + " map. Generated objects are under Environment/" + preview.name + ".");
    }

    static bool EnsureMapPreviews(Transform environment) {
        bool changed = false;
        string visible = null;
        bool hasPreview = false;
        foreach (Transform child in environment)
            if (child.name.StartsWith("Map Preview - ")) {
                hasPreview = true;
                if (child.gameObject.activeSelf) visible = child.name;
            }
        if (!hasPreview) {
            while (environment.childCount > 0) Object.DestroyImmediate(environment.GetChild(0).gameObject);
            changed = true;
        }
        foreach (string mapId in new[] { "meadow", "crossroads", "crater" }) {
            Transform existing = environment.Find("Map Preview - " + mapId);
            if (existing != null) continue;
            GameObject preview = BuildMapPreview(environment, mapId);
            preview.SetActive(false);
            changed = true;
        }
        if (string.IsNullOrEmpty(visible)) {
            Transform meadow = environment.Find("Map Preview - meadow");
            if (meadow != null) meadow.gameObject.SetActive(true);
        }
        return changed;
    }

    static GameObject BuildMapPreview(Transform environment, string mapId) {
        BombMapLayouts.Layout layout = BombMapLayouts.Get(mapId);
        return new BombMapView().Build(layout.id, layout.arenaSize, layout.obstacles, layout.bushes,
            environment, "Map Preview - " + layout.id);
    }

    static void CreateCamera(Transform parent) {
        GameObject cameraObject = NewObject("Arena Camera", parent);
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 10.5f;
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

    static GameObject NewObject(string name, Transform parent = null) {
        GameObject gameObject = new GameObject(name);
        if (parent != null) gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    static void SetBuildScene() {
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
    }
}
