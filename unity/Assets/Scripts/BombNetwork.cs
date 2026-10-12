using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

// One reader thread owns the socket. Unity objects are touched only by Update on the main thread.
public sealed class BombNetwork : IDisposable {
    readonly ConcurrentQueue<string> inbox = new ConcurrentQueue<string>();
    readonly object writeLock = new object();
    Thread worker;
    TcpClient socket;
    StreamWriter writer;
    volatile bool running;
    long sequence;
    string host, name;
    int port;
    public string ResumeToken { get; set; }
    public bool Connected { get; private set; }
    public int SentMessages { get; private set; }
    public int ReceivedMessages { get; private set; }

    public void Connect(string host, int port, string name) {
        Dispose();
        this.host = host;
        this.port = port;
        this.name = name;
        running = true;
        worker = new Thread(Run) { IsBackground = true, Name = "Bomb TCP client" };
        worker.Start();
    }

    void Run() {
        while (running) {
            try {
                using (var client = new TcpClient()) {
                    client.NoDelay = true;
                    client.Connect(host, port);
                    socket = client;
                    var stream = client.GetStream();
                    lock (writeLock) writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
                    Connected = true;
                    Send(new BombMessage { type = "HELLO", name = name, resumeToken = ResumeToken });
                    using (var reader = new StreamReader(stream, Encoding.UTF8)) {
                        string line;
                        while (running && (line = reader.ReadLine()) != null) {
                            inbox.Enqueue(line);
                            ReceivedMessages++;
                        }
                    }
                }
            } catch (Exception e) when (e is IOException || e is SocketException) {
                inbox.Enqueue("{\"type\":\"NETWORK_ERROR\"}");
            } finally {
                Connected = false;
                lock (writeLock) writer = null;
                socket = null;
            }
            if (running) Thread.Sleep(1200);
        }
    }

    public void Send(BombMessage message) {
        message.seq = Interlocked.Increment(ref sequence);
        message.clientTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        lock (writeLock) {
            if (writer == null) return;
            try { writer.WriteLine(JsonUtility.ToJson(message)); SentMessages++; }
            catch (IOException) { Connected = false; }
        }
    }

    public bool TryReceive(out string line) { return inbox.TryDequeue(out line); }

    public void Dispose() {
        running = false;
        try { socket?.Close(); } catch (Exception) { }
        if (worker != null && worker.IsAlive) worker.Join(300);
        Connected = false;
        worker = null;
        lock (writeLock) writer = null;
    }
}
