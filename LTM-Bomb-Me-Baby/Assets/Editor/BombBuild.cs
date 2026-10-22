using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BombBuild {
    [MenuItem("Tools/Bomb Me Baby/Build Windows")]
    public static void Windows() {
        string output = Argument("-bombBuildOutput", Path.GetFullPath("../builds/windows/BombMeBaby.exe"));
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/Scenes/Main.unity" },
            locationPathName = output,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.StrictMode
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new Exception("Windows build failed: " + report.summary.result);
        Debug.Log("BOMB_BUILD_PASS " + report.summary.totalSize + " bytes, " + report.summary.totalTime);
    }

    public static string Argument(string key, string fallback) {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++) if (args[i] == key) return args[i + 1];
        return fallback;
    }
}
