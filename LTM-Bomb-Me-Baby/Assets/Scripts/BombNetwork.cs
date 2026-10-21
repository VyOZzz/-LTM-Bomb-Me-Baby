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
    long sentBytes;
    long receivedBytes;
    int sentMessages;
    int receivedMessages;
    string host, name;
    int port;
    int connectionGeneration;
    string lastError = "";
    public string ResumeToken { get; set; }
    public bool Connected { get; private set; }
    public string LastError => lastError;
    public string Endpoint => string.IsNullOrEmpty(host) ? "" : host + ":" + port;
    public int SentMessages => Volatile.Read(ref sentMessages);
    public int ReceivedMessages => Volatile.Read(ref receivedMessages);
    public long SentBytes => Interlocked.Read(ref sentBytes);
    public long ReceivedBytes => Interlocked.Read(ref receivedBytes);

    public void Connect(string host, int port, string name) {
        Dispose();
        this.host = host.Trim();
        this.port = port;
        this.name = name;
        lastError = "";
        running = true;
        int generation = Interlocked.Increment(ref connectionGeneration);
        worker = new Thread(() => Run(generation)) { IsBackground = true, Name = "Bomb TCP client" };
        worker.Start();
    }

    void Run(int generation) {
        while (running && generation == Volatile.Read(ref connectionGeneration)) {
            try {
                using (var client = new TcpClient()) {
                    client.NoDelay = true;
                    socket = client;
                    var pending = client.ConnectAsync(host, port);
                    if (!pending.Wait(TimeSpan.FromSeconds(5)))
                        throw new SocketException((int)SocketError.TimedOut);
                    if (generation != Volatile.Read(ref connectionGeneration)) return;
                    socket = client;
                    var stream = client.GetStream();
                    lock (writeLock) writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
                    lastError = "";
                    Connected = true;
                    Send(new BombMessage { type = "HELLO", name = name, resumeToken = ResumeToken });
                    using (var reader = new StreamReader(stream, Encoding.UTF8)) {
                        string line;
                        while (running && (line = reader.ReadLine()) != null) {
                            inbox.Enqueue(line);
                            Interlocked.Increment(ref receivedMessages);
                            Interlocked.Add(ref receivedBytes, Encoding.UTF8.GetByteCount(line) + 1);
                        }
                    }
                }
            } catch (Exception e) when (e is IOException || e is SocketException || e is AggregateException) {
                if (generation != Volatile.Read(ref connectionGeneration)) return;
                Exception cause = e is AggregateException aggregate ? aggregate.GetBaseException() : e;
                lastError = Describe(cause);
                inbox.Enqueue("{\"type\":\"NETWORK_ERROR\"}");
            } finally {
                if (generation == Volatile.Read(ref connectionGeneration)) {
                    Connected = false;
                    lock (writeLock) writer = null;
                    socket = null;
                }
            }
            if (running && generation == Volatile.Read(ref connectionGeneration)) Thread.Sleep(1200);
        }
    }

    string Describe(Exception error) {
        if (error is SocketException socketError) {
            switch (socketError.SocketErrorCode) {
                case SocketError.HostNotFound:
                case SocketError.NoData:
                    return "Không tìm thấy địa chỉ server '" + host + "'.";
                case SocketError.ConnectionRefused:
                    return "Server " + Endpoint + " từ chối kết nối (server chưa chạy hoặc sai port).";
                case SocketError.TimedOut:
                    return "Hết thời gian chờ " + Endpoint + " (kiểm tra IP, firewall và port forwarding).";
                case SocketError.NetworkUnreachable:
                case SocketError.HostUnreachable:
                    return "Không thể đi tới " + Endpoint + " từ mạng hiện tại.";
            }
        }
        return "Không kết nối được tới " + Endpoint + ": " + error.Message;
    }

    public void Send(BombMessage message) {
        message.seq = Interlocked.Increment(ref sequence);
        message.clientTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        lock (writeLock) {
            if (writer == null) return;
            try {
                string json = JsonUtility.ToJson(message);
                writer.WriteLine(json);
                Interlocked.Increment(ref sentMessages);
                Interlocked.Add(ref sentBytes, Encoding.UTF8.GetByteCount(json) + 1);
            }
            catch (IOException) { Connected = false; }
        }
    }

    public void ContinueSequenceAfter(long lastAcceptedSequence) {
        long current;
        do {
            current = Interlocked.Read(ref sequence);
            if (current >= lastAcceptedSequence) return;
        } while (Interlocked.CompareExchange(ref sequence, lastAcceptedSequence, current) != current);
    }

    public bool TryReceive(out string line) { return inbox.TryDequeue(out line); }

    public void Dispose() {
        running = false;
        Interlocked.Increment(ref connectionGeneration);
        try { socket?.Close(); } catch (Exception) { }
        if (worker != null && worker.IsAlive) worker.Join(300);
        Connected = false;
        worker = null;
        lock (writeLock) writer = null;
    }
}
