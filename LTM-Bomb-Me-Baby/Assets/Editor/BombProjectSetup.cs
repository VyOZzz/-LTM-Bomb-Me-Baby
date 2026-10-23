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
            } else if (UpgradeEnvironment()) {
                EditorSceneManager.SaveScene(scene, ScenePath);
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

        // The arena itself comes from the server each round; this only previews a real layout.
        NewObject("Environment", root.transform).AddComponent<BombMapPreview>();

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
