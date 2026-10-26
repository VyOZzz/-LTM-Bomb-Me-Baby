using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

// Socket I/O stays off Unity's main thread. STATE_UPDATE messages are coalesced so a
// paused client resumes from the newest authoritative state instead of replaying a backlog.
public sealed class BombNetwork : IDisposable {
    // Bump on any incompatible protocol or rules change; must match BombServer.PROTOCOL_VERSION.
    public const int ProtocolVersion = 1;
    const int MaxQueuedMessages = 256;
    const int MaxQueuedWrites = 128;
    // The server closes an inbound-silent connection after 6 s. Waiting slightly longer
    // prevents a replacement socket from racing the old session during a network black hole.
    const int ServerSilenceMilliseconds = 7000;

    readonly LinkedList<string> inbox = new LinkedList<string>();
    readonly ConcurrentQueue<string> outbox = new ConcurrentQueue<string>();
    readonly AutoResetEvent writeSignal = new AutoResetEvent(false);
    readonly object inboxLock = new object();
    Thread worker;
    TcpClient socket;
    volatile bool running;
    volatile bool transportReady;
    long sequence;
    long sentBytes;
    long receivedBytes;
    long lastReceivedAt;
    int sentMessages;
    int receivedMessages;
    int queuedWrites;
    int retryAttempt;
    int connectionGeneration;
    int connectionAttempt;
    int failedAttempt;
    string host, name;
    int port;
    string lastError = "";

    public string ResumeToken { get; set; }
    public bool Connected { get; private set; }
    public string LastError => lastError;
    public string Endpoint => string.IsNullOrEmpty(host) ? "" : host + ":" + port;
    public int SentMessages => Volatile.Read(ref sentMessages);
    public int ReceivedMessages => Volatile.Read(ref receivedMessages);
    public long SentBytes => Interlocked.Read(ref sentBytes);
    public long ReceivedBytes => Interlocked.Read(ref receivedBytes);
    public int RetryAttempt => Volatile.Read(ref retryAttempt);
    public float SecondsSinceLastReceive {
        get {
            long receivedAt = Interlocked.Read(ref lastReceivedAt);
            return receivedAt <= 0 ? 0 : (float)Math.Max(0, (NowMilliseconds() - receivedAt) / 1000.0);
        }
    }

    public void Connect(string host, int port, string name) {
        Dispose();
        ClearQueues();
        this.host = host.Trim();
        this.port = port;
        this.name = name;
        lastError = "";
        retryAttempt = 0;
        running = true;
        int generation = Interlocked.Increment(ref connectionGeneration);
        worker = new Thread(() => Run(generation)) { IsBackground = true, Name = "Bomb TCP client" };
        worker.Start();
    }

    void Run(int generation) {
        while (running && generation == Volatile.Read(ref connectionGeneration)) {
            Interlocked.Increment(ref retryAttempt);
            int attempt = Interlocked.Increment(ref connectionAttempt);
            Interlocked.Exchange(ref failedAttempt, 0);
            try {
                using (var client = new TcpClient()) {
                    client.NoDelay = true;
                    socket = client;
                    var pending = client.ConnectAsync(host, port);
                    if (!pending.Wait(TimeSpan.FromSeconds(5)))
                        throw new SocketException((int)SocketError.TimedOut);
                    if (!running || generation != Volatile.Read(ref connectionGeneration)) return;

                    var stream = client.GetStream();
                    stream.WriteTimeout = 3000;
                    ResetOutbox();
                    Connected = false;
                    transportReady = true;
                    lastError = "";
                    Interlocked.Exchange(ref lastReceivedAt, NowMilliseconds());

                    var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
                    EnqueueRaw(JsonFor(new BombMessage {
                        type = "HELLO", protocolVersion = ProtocolVersion, name = name, resumeToken = ResumeToken
                    }));
                    var output = new Thread(() => WriteLoop(generation, attempt, client, writer)) {
                        IsBackground = true, Name = "Bomb TCP writer"
                    };
                    output.Start();

                    using (var reader = new StreamReader(stream, Encoding.UTF8)) {
                        string line;
                        while (running && generation == Volatile.Read(ref connectionGeneration)
                                && (line = reader.ReadLine()) != null) {
                            Interlocked.Exchange(ref retryAttempt, 0);
                            Interlocked.Exchange(ref lastReceivedAt, NowMilliseconds());
                            if (Volatile.Read(ref failedAttempt) != 0) break;
                            EnqueueIncoming(line);
                            Interlocked.Increment(ref receivedMessages);
                            Interlocked.Add(ref receivedBytes, Encoding.UTF8.GetByteCount(line) + 1);
                        }
                    }
                    if (running) SignalFailure(generation, attempt, client,
                        new IOException("Server đã đóng kết nối."));
                }
            } catch (Exception e) {
                if (running && generation == Volatile.Read(ref connectionGeneration))
                    SignalFailure(generation, attempt, socket,
                        e is AggregateException aggregate ? aggregate.GetBaseException() : e);
            } finally {
                if (generation == Volatile.Read(ref connectionGeneration)) {
                    Connected = false;
                    transportReady = false;
                    socket = null;
                    ResetOutbox();
                }
            }
            if (running && generation == Volatile.Read(ref connectionGeneration)) Thread.Sleep(1200);
        }
    }

    void WriteLoop(int generation, int attempt, TcpClient client, StreamWriter writer) {
        try {
            while (running && generation == Volatile.Read(ref connectionGeneration)
                    && attempt == Volatile.Read(ref connectionAttempt)) {
                if (NowMilliseconds() - Interlocked.Read(ref lastReceivedAt) >= ServerSilenceMilliseconds)
                    throw new SocketException((int)SocketError.TimedOut);
                if (!outbox.TryDequeue(out string json)) {
                    writeSignal.WaitOne(200);
                    continue;
                }
                Interlocked.Decrement(ref queuedWrites);
                writer.WriteLine(json);
                Interlocked.Increment(ref sentMessages);
                Interlocked.Add(ref sentBytes, Encoding.UTF8.GetByteCount(json) + 1);
            }
        } catch (Exception e) {
            SignalFailure(generation, attempt, client, e);
        }
    }

    void SignalFailure(int generation, int attempt, TcpClient client, Exception error) {
        if (!running || generation != Volatile.Read(ref connectionGeneration)
                || attempt != Volatile.Read(ref connectionAttempt)
                || Interlocked.CompareExchange(ref failedAttempt, 1, 0) != 0) return;
        lastError = Describe(error);
        Connected = false;
        transportReady = false;
        lock (inboxLock) {
            // Overflow requires a full resume, rather than silently losing lifecycle events.
            if (inbox.Count >= MaxQueuedMessages) inbox.Clear();
            inbox.AddLast("{\"type\":\"NETWORK_ERROR\"}");
        }
        try { client?.Close(); } catch (Exception) { }
    }

    string Describe(Exception error) {
        if (error is IOException && error.InnerException is SocketException inner) error = inner;
        if (error is SocketException socketError) {
            switch (socketError.SocketErrorCode) {
                case SocketError.HostNotFound:
                case SocketError.NoData:
                    return "Không tìm thấy địa chỉ server '" + host + "'.";
                case SocketError.ConnectionRefused:
                    return "Server " + Endpoint + " từ chối kết nối (server chưa chạy hoặc sai port).";
                case SocketError.TimedOut:
                    return "Server " + Endpoint + " không phản hồi.";
                case SocketError.NetworkUnreachable:
                case SocketError.HostUnreachable:
                    return "Không thể đi tới " + Endpoint + " từ mạng hiện tại.";
            }
        }
        return "Không kết nối được tới " + Endpoint + ": " + error.Message;
    }

    public void MarkWelcomed() {
        if (transportReady) Connected = true;
    }

    public void Send(BombMessage message) {
        if (!Connected || !transportReady) return;
        string json = JsonFor(message);
        if (Interlocked.Increment(ref queuedWrites) > MaxQueuedWrites) {
            Interlocked.Decrement(ref queuedWrites);
            SignalFailure(Volatile.Read(ref connectionGeneration), Volatile.Read(ref connectionAttempt), socket,
                new IOException("Hàng đợi gửi mạng đã đầy."));
            return;
        }
        outbox.Enqueue(json);
        writeSignal.Set();
    }

    string JsonFor(BombMessage message) {
        message.seq = Interlocked.Increment(ref sequence);
        message.clientTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return UnityEngine.JsonUtility.ToJson(message);
    }

    void EnqueueRaw(string json) {
        Interlocked.Increment(ref queuedWrites);
        outbox.Enqueue(json);
        writeSignal.Set();
    }

    void EnqueueIncoming(string line) {
        lock (inboxLock) {
            // Only replace adjacent snapshots. Events are ordering barriers, and a full
            // recovery snapshot must survive a following optimized snapshot without geometry.
            if (IsSnapshot(line) && inbox.Last != null && IsSnapshot(inbox.Last.Value)
                    && (!HasGeometry(inbox.Last.Value) || HasGeometry(line))) {
                inbox.Last.Value = line;
                return;
            }
            if (inbox.Count >= MaxQueuedMessages)
                throw new IOException("Hàng đợi nhận mạng đã đầy; đang khôi phục trạng thái.");
            inbox.AddLast(line);
        }
    }

    static bool IsSnapshot(string line) => line.IndexOf("\"type\":\"STATE_UPDATE\"", StringComparison.Ordinal) >= 0;
    static bool HasGeometry(string line) => line.IndexOf("\"obstacles\":", StringComparison.Ordinal) >= 0;

    public void ContinueSequenceAfter(long lastAcceptedSequence) {
        long current;
        do {
            current = Interlocked.Read(ref sequence);
            if (current >= lastAcceptedSequence) return;
        } while (Interlocked.CompareExchange(ref sequence, lastAcceptedSequence, current) != current);
    }

    public bool TryReceive(out string line) {
        lock (inboxLock) {
            if (inbox.First == null) {
                line = null;
                return false;
            }
            line = inbox.First.Value;
            inbox.RemoveFirst();
            return true;
        }
    }

    void ResetOutbox() {
        while (outbox.TryDequeue(out _)) { }
        Interlocked.Exchange(ref queuedWrites, 0);
    }

    void ClearQueues() {
        lock (inboxLock) inbox.Clear();
        ResetOutbox();
    }

    static long NowMilliseconds() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public void Dispose() {
        running = false;
        transportReady = false;
        Interlocked.Increment(ref connectionGeneration);
        try { socket?.Close(); } catch (Exception) { }
        writeSignal.Set();
        if (worker != null && worker.IsAlive) worker.Join(300);
        Connected = false;
        socket = null;
        worker = null;
        ResetOutbox();
    }
}
