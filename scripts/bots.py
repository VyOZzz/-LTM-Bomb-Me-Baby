"""Simple bots for playtesting Bomb Me Baby against a live server.

Join the room you created in the game (bots ready up automatically, every round):
    python scripts/bots.py --room ABC123 --count 3

Or wait in matchmaking so your own "Tìm trận" pairs with the bot:
    python scripts/bots.py --find QUICK          # or JOURNEY

Use --host/--port for a remote server (e.g. the ngrok address). Add --passive for
target dummies that never move or attack. Ctrl+C stops every bot.

Bots wander, chase the nearest visible player, charge and explode near them, and raise
their shield when hurt. They only send the same intents a real client sends.
"""
import argparse
import json
import math
import random
import socket
import sys
import threading
import time

TICK = 0.1


class Bot(threading.Thread):
    def __init__(self, index, args, stop):
        super().__init__(daemon=True, name=f"bot-{index}")
        self.name_ = f"Bot {index}"
        self.args = args
        self.stop = stop
        self.random = random.Random()
        self.seq = 0
        self.lock = threading.Lock()
        self.id = None
        self.phase = "WAITING"
        self.ready = False
        self.players = []
        self.charging_until = 0.0
        self.heading = (0.0, 0.0)
        self.heading_until = 0.0
        self.last_hp = 100.0

    def log(self, text):
        # One write per line: print() from several threads interleaves text and newline.
        sys.stdout.write(f"[{self.name_}] {text}\n")
        sys.stdout.flush()

    def send(self, kind, **data):
        with self.lock:
            self.seq += 1
            line = json.dumps({"type": kind, "seq": self.seq, "clientTime": int(time.time() * 1000), **data})
            self.sock.sendall((line + "\n").encode("utf-8"))

    def run(self):
        while not self.stop.is_set():
            try:
                self.play()
            except (OSError, ConnectionError, ValueError) as error:
                self.log(f"mất kết nối: {error}; thử lại sau 2 giây")
            self.id = None
            self.stop.wait(2)

    def play(self):
        self.sock = socket.create_connection((self.args.host, self.args.port), timeout=5)
        self.sock.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
        self.sock.settimeout(None)
        reader = self.sock.makefile("r", encoding="utf-8")
        self.seq = 0
        self.ready = False
        self.send("HELLO", name=self.name_, resumeToken="")
        threading.Thread(target=self.act_loop, daemon=True).start()
        try:
            for line in reader:
                if self.stop.is_set():
                    return
                self.handle(json.loads(line))
            raise ConnectionError("server đóng kết nối")
        finally:
            self.sock.close()

    def handle(self, message):
        kind = message.get("type")
        if kind == "WELCOME":
            self.id = message["playerId"]
            if self.args.room:
                self.send("JOIN_ROOM", code=self.args.room)
            else:
                self.send("FIND_MATCH", mode=self.args.find)
                self.log(f"đang tìm trận {self.args.find}")
        elif kind == "ROOM_STATE":
            room = message.get("room") or {}
            self.phase = room.get("phase", self.phase)
            if self.ready and self.phase == "PLAYING":
                self.ready = False
            me = next((p for p in room.get("players", []) if p.get("id") == self.id), None)
            # Ready again after every round, so the human only has to press Ready.
            if me and not me.get("ready") and self.phase != "PLAYING" and not room.get("journeyComplete"):
                self.send("READY", ready=True)
                self.ready = True
        elif kind == "MATCH_FOUND":
            self.log(f"đã vào phòng {message.get('room')}")
        elif kind == "GAME_START":
            self.phase = "PLAYING"
            self.last_hp = 100.0
            self.log(f"trận bắt đầu trên map {message.get('mapName')}")
        elif kind == "STATE_UPDATE":
            self.phase = message.get("phase", self.phase)
            self.players = message.get("players") or []
        elif kind == "GAME_OVER":
            winner = message.get("winnerId")
            self.log("thắng ván" if winner == self.id else "hết ván")
        elif kind == "ERROR":
            code = message.get("code")
            if code in ("ROOM_NOT_FOUND", "ROOM_UNAVAILABLE", "MODE_MISMATCH", "INVALID_MODE"):
                self.log(f"không vào được phòng: {code}")
                self.stop.set()

    def act_loop(self):
        last_ping = 0.0
        while not self.stop.is_set() and not self.sock._closed:
            now = time.monotonic()
            try:
                if now - last_ping >= 2:
                    self.send("PING")
                    last_ping = now
                if self.phase == "PLAYING" and not self.args.passive:
                    self.act(now)
            except OSError:
                return
            time.sleep(TICK)

    def act(self, now):
        me = next((p for p in self.players if p.get("id") == self.id), None)
        if not me or not me.get("alive"):
            return
        hp = me.get("hp", 100)
        if hp < self.last_hp - 8 and me.get("shieldCooldown", 1) <= 0:
            self.send("USE_SHIELD")
        self.last_hp = hp

        target = self.nearest_enemy(me)
        if me.get("charging"):
            # Keep walking toward the target while charging, then release.
            if now >= self.charging_until or (target and target[1] < 1.5):
                self.send("EXPLODE")
        elif target and target[1] < self.args.aggro and me.get("chargeCooldown", 1) <= 0:
            self.send("CHARGE_START")
            self.charging_until = now + self.random.uniform(0.6, 2.5)

        if target and target[1] < self.args.aggro * 2:
            other = target[0]
            dx, dz = other["x"] - me["x"], other["z"] - me["z"]
            length = math.hypot(dx, dz) or 1
            direction = (dx / length, dz / length)
        else:
            if now >= self.heading_until:
                angle = self.random.uniform(0, math.tau)
                self.heading = (math.cos(angle), math.sin(angle))
                self.heading_until = now + self.random.uniform(1.0, 3.0)
            direction = self.heading
        self.send("MOVE", directionX=direction[0], directionZ=direction[1])

    def nearest_enemy(self, me):
        best = None
        for other in self.players:
            if other.get("id") == self.id or not other.get("alive") or not other.get("visible"):
                continue
            distance = math.hypot(other["x"] - me["x"], other["z"] - me["z"])
            if best is None or distance < best[1]:
                best = (other, distance)
        return best


def main():
    sys.stdout.reconfigure(encoding="utf-8")  # Windows consoles default to cp1252
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=7777)
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--room", help="mã phòng bạn tạo trong game")
    group.add_argument("--find", choices=["QUICK", "JOURNEY"], help="xếp hàng tìm trận chế độ này")
    parser.add_argument("--count", type=int, default=1, help="số bot (mặc định 1)")
    parser.add_argument("--first", type=int, default=1, help="số thứ tự của bot đầu tiên, để tên không trùng")
    parser.add_argument("--passive", action="store_true", help="bot đứng yên làm bia")
    parser.add_argument("--aggro", type=float, default=7.0, help="khoảng cách bot bắt đầu gồng bom")
    args = parser.parse_args()
    if args.room:
        args.room = args.room.strip().upper()
    if args.find and args.count > 1:
        print("Lưu ý: nhiều bot cùng tìm trận sẽ tự ghép với nhau; nên dùng --room để có nhiều bot.")

    stop = threading.Event()
    bots = [Bot(args.first + i, args, stop) for i in range(args.count)]
    for bot in bots:
        bot.start()
    try:
        while not stop.is_set():
            stop.wait(0.5)
    except KeyboardInterrupt:
        stop.set()
    print("Đã dừng bot.")


if __name__ == "__main__":
    main()
