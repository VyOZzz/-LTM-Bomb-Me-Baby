using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Creates the empty entry scene on first import. BombGame builds the prototype arena at runtime.
[InitializeOnLoad]
public static class BombProjectSetup {
    const string ScenePath = "Assets/Scenes/Main.unity";

    static BombProjectSetup() { EditorApplication.delayCall += EnsureScene; }

    static void EnsureScene() {
        if (File.Exists(ScenePath)) return;
        Directory.CreateDirectory("Assets/Scenes");
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        Debug.Log("Bomb Me Baby scene created at " + ScenePath);
    }
}
