#if UNITY_EDITOR
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// Editor-only playtest helper: each bot is one scripts/bots.py process that joins the current
/// room like a normal client. Bots are killed when the room is left or Play mode stops.
static class BombDevBots {
    static readonly List<Process> Running = new List<Process>();
    static int nextNumber = 1;

    public static int Count {
        get {
            Running.RemoveAll(process => {
                if (!process.HasExited) return false;
                process.Dispose();
                return true;
            });
            return Running.Count;
        }
    }

    public static void Add(string host, string port, string roomCode) {
        string script = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "scripts", "bots.py"));
        if (!File.Exists(script)) {
            Debug.LogError("Không tìm thấy " + script);
            return;
        }
        int number = nextNumber++;
        var start = new ProcessStartInfo("python",
                $"-u \"{script}\" --host {host} --port {port} --room {roomCode} --first {number}") {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        try {
            Process process = Process.Start(start);
            process.OutputDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) Debug.Log(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) Debug.LogWarning("[Bot " + number + "] " + e.Data); };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            Running.Add(process);
        } catch (System.ComponentModel.Win32Exception) {
            Debug.LogError("Không chạy được python. Cài Python 3 và thêm vào PATH để dùng bot.");
        }
    }

    public static void StopAll() {
        foreach (Process process in Running) {
            try { if (!process.HasExited) process.Kill(); } catch (System.InvalidOperationException) { }
            process.Dispose();
        }
        Running.Clear();
        nextNumber = 1;
    }
}
#endif
