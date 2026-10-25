using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// One-click local playtest: starts the Java server in its own window. Bots are added from the
/// room lobby while playing in the Editor (see BombDevBots).
public static class BombTestTools {
    const int Port = 7777;

    [MenuItem("Tools/Bomb Me Baby/Test/Bật server local")]
    public static void StartLocalServer() {
        if (ServerRunning()) {
            Debug.Log($"Server đã chạy sẵn ở 127.0.0.1:{Port}.");
            return;
        }
        string script = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "scripts", "run_server.ps1"));
        if (!File.Exists(script)) {
            Debug.LogError("Không tìm thấy " + script);
            return;
        }
        // A visible window shows the server log and closes the server with it.
        Process.Start(new ProcessStartInfo("powershell",
            $"-NoExit -ExecutionPolicy Bypass -File \"{script}\" -Port {Port}") { UseShellExecute = true });
        Debug.Log($"Đang build và bật server ở 127.0.0.1:{Port}. Đợi cửa sổ server báo \"TCP port {Port}\" rồi bấm Play.");
    }

    static bool ServerRunning() {
        try {
            using (var client = new TcpClient()) return client.ConnectAsync("127.0.0.1", Port).Wait(300) && client.Connected;
        } catch (System.Exception) {
            return false;
        }
    }
}
