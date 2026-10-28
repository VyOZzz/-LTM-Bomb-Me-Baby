"""Measure network behaviour of a live Bomb Me Baby server with N simulated clients.

Example (server already running):
    python scripts/load_test.py --clients 4 --duration 60 --label baseline

Every simulated client joins one room, readies up, then sends MOVE at --move-hz
and PING at --ping-hz until --duration seconds pass. Clients never explode, so
nobody is eliminated and the match keeps running for the whole measurement.

Output:
- Raw CSV, one row per message sent or received:
  experiments/raw/<label>_N<clients>_<timestamp>.csv
- One summary row appended to experiments/summary.csv.
"""
import argparse
import csv
import datetime
import json
import hashlib
from pathlib import Path
import os
import random
import socket
import subprocess
import threading
import time

RAW_FIELDS = ["t_ms", "client", "direction", "type", "bytes", "rtt_ms", "processing_us"]
SUMMARY_FIELDS = [
    "run_at", "label", "commit", "server_source_sha256", "sync_mode", "map_id", "host", "port", "clients", "duration_s",
    "move_hz", "ping_hz", "seed", "game_seed",
    "recv_msgs_per_client_s", "recv_bytes_per_client_s", "server_out_bytes_s",
    "sent_msgs_per_client_s", "sent_bytes_per_client_s",
    "state_updates_per_client_s", "state_update_avg_bytes",
    "rtt_samples", "rtt_mean_ms", "rtt_p50_ms", "rtt_p95_ms", "rtt_max_ms",
    "processing_mean_us", "processing_p95_us", "errors", "raw_file",
]


class Client:
    def __init__(self, index, host, port, recorder):
        self.index = index
        self.recorder = recorder
        self.sock = socket.create_connection((host, port), timeout=5)
        self.sock.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
        self.reader = self.sock.makefile("rb")
        self.seq = 0
        self.geometry_in_snapshot = None  # True: baseline snapshots carry obstacles/bushes
        self.send_lock = threading.Lock()
        self.send("HELLO", protocolVersion=1, name=f"Load {index}", resumeToken="")
        self.welcome = self.until("WELCOME")

    def send(self, kind, **data):
        with self.send_lock:
            self.seq += 1
            payload = (json.dumps({"type": kind, "seq": self.seq, **data}) + "\n").encode("utf-8")
            self.sock.sendall(payload)
        self.recorder.record(self.index, "sent", kind, len(payload))

    def read(self):
        line = self.reader.readline()
        if not line:
            raise ConnectionError(f"client {self.index}: server closed connection")
        return line, json.loads(line)

    def until(self, kind, timeout=10):
        """Setup helper: block until a message of this type arrives (not recorded)."""
        self.sock.settimeout(timeout)
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            _, message = self.read()
            if message.get("type") == "ERROR":
                raise RuntimeError(f"client {self.index}: {message}")
            if message.get("type") == kind:
                return message
        raise TimeoutError(f"client {self.index}: timed out waiting for {kind}")

    def receive_loop(self, stop):
        # Blocking reads: a timed-out socket file cannot be read again. Snapshots
        # arrive at 10 Hz, so the loop still notices `stop` quickly.
        self.sock.settimeout(None)
        while not stop.is_set():
            try:
                line, message = self.read()
            except (OSError, ValueError, ConnectionError):
                if not stop.is_set():
                    self.recorder.record(self.index, "recv", "CONNECTION_LOST", 0)
                return
            kind = message.get("type", "")
            rtt_ms = processing_us = ""
            if kind == "PONG":
                # clientTime carries perf_counter in microseconds; the server only echoes it.
                rtt_ms = (now_us() - int(message.get("clientTime", 0))) / 1000
            elif kind == "ACK":
                processing_us = message.get("processingMicros", "")
            elif kind == "STATE_UPDATE" and self.geometry_in_snapshot is None:
                self.geometry_in_snapshot = "obstacles" in message
            self.recorder.record(self.index, "recv", kind, len(line), rtt_ms, processing_us)

    def send_loop(self, stop, move_hz, ping_hz, rng):
        move_every = 1 / move_hz if move_hz > 0 else None
        ping_every = 1 / ping_hz if ping_hz > 0 else None
        next_move = next_ping = time.monotonic()
        direction = (0.0, 0.0)
        while not stop.is_set():
            now = time.monotonic()
            try:
                if move_every and now >= next_move:
                    if rng.random() < 0.2:  # change heading about every 0.5 s at 10 Hz
                        direction = (rng.uniform(-1, 1), rng.uniform(-1, 1))
                    self.send("MOVE", directionX=direction[0], directionZ=direction[1])
                    next_move += move_every
                if ping_every and now >= next_ping:
                    self.send("PING", clientTime=now_us())
                    next_ping += ping_every
            except OSError:
                return
            wake = min(t for t in (next_move if move_every else None,
                                   next_ping if ping_every else None) if t is not None)
            time.sleep(max(0.0, wake - time.monotonic()))

    def close(self):
        try:
            self.send("LEAVE_ROOM")
        except OSError:
            pass
        try:
            self.reader.close()
            self.sock.close()
        except OSError:
            pass


class Recorder:
    def __init__(self):
        self.rows = []
        self.lock = threading.Lock()
        self.start = None

    def record(self, client, direction, kind, size, rtt_ms="", processing_us=""):
        if self.start is None:
            return  # setup traffic is not part of the measurement
        t_ms = (time.perf_counter() - self.start) * 1000
        with self.lock:
            self.rows.append([round(t_ms, 3), client, direction, kind, size,
                              round(rtt_ms, 3) if rtt_ms != "" else "", processing_us])


def now_us():
    return time.perf_counter_ns() // 1000


def percentile(values, p):
    if not values:
        return ""
    ordered = sorted(values)
    rank = max(0, min(len(ordered) - 1, round(p / 100 * len(ordered) + 0.5) - 1))
    return ordered[rank]


def mean(values):
    return sum(values) / len(values) if values else ""


def git_commit():
    try:
        return subprocess.run(["git", "describe", "--always", "--dirty"], capture_output=True,
                              text=True, check=True, cwd=os.path.dirname(__file__)).stdout.strip()
    except (OSError, subprocess.CalledProcessError):
        return ""


def server_source_hash():
    root = Path(__file__).resolve().parent.parent / "server"
    digest = hashlib.sha256()
    for source in sorted((root / "src" / "main").rglob("*.java")):
        digest.update(source.relative_to(root).as_posix().encode("utf-8"))
        digest.update(source.read_bytes())
    digest.update((root / "pom.xml").read_bytes())
    return digest.hexdigest()


def detect_sync_mode(clients):
    """Baseline repeats map geometry in every STATE_UPDATE; optimized sends it only in GAME_START."""
    seen = {c.geometry_in_snapshot for c in clients} - {None}
    if len(seen) != 1:
        return "unknown"
    return "baseline" if seen.pop() else "optimized"


def summarize(rows, args, elapsed, raw_file, sync_mode, map_id):
    recv = [r for r in rows if r[2] == "recv"]
    sent = [r for r in rows if r[2] == "sent"]
    states = [r for r in recv if r[3] == "STATE_UPDATE"]
    rtts = [r[5] for r in recv if r[5] != ""]
    processing = [r[6] for r in recv if r[6] != ""]
    per_client = args.clients * elapsed
    fmt = lambda v, d=2: round(v, d) if v != "" else ""
    return {
        "run_at": datetime.datetime.now().isoformat(timespec="seconds"),
        "label": args.label, "commit": git_commit(), "server_source_sha256": server_source_hash(), "sync_mode": sync_mode, "map_id": map_id,
        "host": args.host, "port": args.port,
        "clients": args.clients, "duration_s": round(elapsed, 2), "move_hz": args.move_hz,
        "ping_hz": args.ping_hz, "seed": args.seed, "game_seed": args.game_seed,
        "recv_msgs_per_client_s": fmt(len(recv) / per_client),
        "recv_bytes_per_client_s": fmt(sum(r[4] for r in recv) / per_client, 1),
        "server_out_bytes_s": fmt(sum(r[4] for r in recv) / elapsed, 1),
        "sent_msgs_per_client_s": fmt(len(sent) / per_client),
        "sent_bytes_per_client_s": fmt(sum(r[4] for r in sent) / per_client, 1),
        "state_updates_per_client_s": fmt(len(states) / per_client),
        "state_update_avg_bytes": fmt(mean([r[4] for r in states]), 1),
        "rtt_samples": len(rtts),
        "rtt_mean_ms": fmt(mean(rtts), 3), "rtt_p50_ms": fmt(percentile(rtts, 50), 3),
        "rtt_p95_ms": fmt(percentile(rtts, 95), 3), "rtt_max_ms": fmt(max(rtts) if rtts else "", 3),
        "processing_mean_us": fmt(mean(processing), 1), "processing_p95_us": percentile(processing, 95),
        "errors": sum(1 for r in recv if r[3] in ("ERROR", "CONNECTION_LOST")),
        "raw_file": raw_file,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=7777)
    parser.add_argument("--clients", type=int, default=2, help="2-10 simulated players in one room")
    parser.add_argument("--duration", type=float, default=60, help="measurement time in seconds")
    parser.add_argument("--move-hz", type=float, default=10)
    parser.add_argument("--ping-hz", type=float, default=1)
    parser.add_argument("--seed", type=int, default=1, help="random seed for movement")
    parser.add_argument("--expect-game-seed", type=int, help="require the server's advertised room RNG seed")
    parser.add_argument("--label", default="baseline", help="name of this experiment, e.g. baseline")
    parser.add_argument("--out-dir", default="experiments")
    args = parser.parse_args()
    if not 2 <= args.clients <= 10:
        parser.error("--clients must be between 2 and 10")
    if args.duration <= 0 or args.move_hz <= 0 or args.ping_hz <= 0:
        parser.error("duration and frequencies must be positive")
    existing_summary = Path(args.out_dir) / "summary.csv"
    if existing_summary.exists():
        with existing_summary.open(newline="", encoding="utf-8") as existing:
            if next(csv.reader(existing), []) != SUMMARY_FIELDS:
                parser.error("Existing CSV uses an older schema; choose a new --out-dir")

    recorder = Recorder()
    clients = []
    stop = threading.Event()
    threads = []
    game = {}
    try:
        for i in range(args.clients):
            clients.append(Client(i, args.host, args.port, recorder))
        seeds = {c.welcome.get("gameSeed") for c in clients}
        if len(seeds) != 1:
            raise RuntimeError(f"Inconsistent server seeds: {seeds}")
        args.game_seed = seeds.pop()
        if args.expect_game_seed is not None and args.game_seed != args.expect_game_seed:
            raise RuntimeError(f"Expected game seed {args.expect_game_seed}, got {args.game_seed}")
        clients[0].send("CREATE_ROOM")
        code = clients[0].until("ROOM_STATE")["room"]["code"]
        for c in clients[1:]:
            c.send("JOIN_ROOM", code=code)
            c.until("ROOM_STATE")
        for c in clients:
            c.send("READY", ready=True)
        for c in clients:
            game = c.until("GAME_START")
        print(f"Room {code}, map {game.get('mapId')}, {args.clients} clients. "
              f"Measuring for {args.duration:g} s...")

        recorder.start = time.perf_counter()
        for c in clients:
            rng = random.Random(args.seed * 1000 + c.index)
            threads.append(threading.Thread(target=c.receive_loop, args=(stop,), daemon=True))
            threads.append(threading.Thread(target=c.send_loop,
                                            args=(stop, args.move_hz, args.ping_hz, rng), daemon=True))
        for t in threads:
            t.start()
        time.sleep(args.duration)
    finally:
        elapsed = time.perf_counter() - recorder.start if recorder.start else 0
        recorder.start = None  # stop recording before teardown traffic
        stop.set()
        for t in threads:
            t.join(timeout=2)
        for c in clients:
            c.close()

    raw_dir = os.path.join(args.out_dir, "raw")
    os.makedirs(raw_dir, exist_ok=True)
    stamp = datetime.datetime.now().strftime("%Y%m%d-%H%M%S")
    raw_file = os.path.join(raw_dir, f"{args.label}_N{args.clients}_{stamp}.csv")
    with recorder.lock:
        rows = list(recorder.rows)
    with open(raw_file, "w", newline="", encoding="utf-8") as f:
        writer = csv.writer(f)
        writer.writerow(RAW_FIELDS)
        writer.writerows(rows)

    summary = summarize(rows, args, elapsed, raw_file.replace(os.sep, "/"),
                        detect_sync_mode(clients), game.get("mapId", ""))
    summary_file = os.path.join(args.out_dir, "summary.csv")
    new_file = not os.path.exists(summary_file)
    with open(summary_file, "a", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=SUMMARY_FIELDS)
        if new_file:
            writer.writeheader()
        writer.writerow(summary)

    for key in SUMMARY_FIELDS:
        print(f"{key:28} {summary[key]}")
    if summary["errors"]:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
