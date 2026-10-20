Copy toàn bộ đoạn này vào `scripts/network_smoke.py`:

```python
"""Run against a live server: python3 scripts/network_smoke.py [port]."""

import json
import socket
import sys
import time


PORT = int(sys.argv[1]) if len(sys.argv) > 1 else 7777


class Client:
    def __init__(self, name):
        self.sock = socket.create_connection(("127.0.0.1", PORT), timeout=5)
        self.sock.settimeout(5)
        self.file = self.sock.makefile("r", encoding="utf-8")
        self.seq = 0

        self.send("HELLO", name=name)
        self.until("WELCOME")

    def send(self, kind, **data):
        self.seq += 1
        message = {
            "type": kind,
            "seq": self.seq,
            **data,
        }

        self.sock.sendall(
            (json.dumps(message) + "\n").encode("utf-8")
        )

    def until(self, kind):
        deadline = time.monotonic() + 5

        while time.monotonic() < deadline:
            line = self.file.readline()

            if not line:
                raise RuntimeError("Server closed connection")

            message = json.loads(line)

            if message.get("type") == "ERROR":
                raise RuntimeError(message)

            if message.get("type") == kind:
                return message

        raise RuntimeError("Timed out waiting for " + kind)

    def close(self):
        self.file.close()
        self.sock.close()


def main():
    a = Client("Smoke A")
    b = Client("Smoke B")

    try:
        a.send("CREATE_ROOM")
        code = a.until("ROOM_STATE")["room"]["code"]

        b.send("JOIN_ROOM", code=code)
        b.until("ROOM_STATE")

        a.send("READY", ready=True)
        b.send("READY", ready=True)

        start_a = a.until("GAME_START")
        start_b = b.until("GAME_START")

        assert start_a["room"] == code
        assert start_b["room"] == code

        # GAME_START luôn chứa dữ liệu tĩnh của bản đồ.
        assert len(start_a["obstacles"]) > 0
        assert len(start_a["bushes"]) > 0
        assert len(start_b["obstacles"]) > 0
        assert len(start_b["bushes"]) > 0

        state = a.until("STATE_UPDATE")

        assert len(state["players"]) == 2

        # Chế độ optimized có thể loại bỏ vật phẩm ở xa người chơi.
        assert isinstance(state["orbs"], list)

        assert state["mapId"] == start_a["mapId"]
        assert state["mapId"] in {
            "meadow",
            "crossroads",
            "crater",
        }
        assert state["arenaSize"] >= 46

        # Baseline gửi lại đá/bụi trong STATE_UPDATE.
        # Optimized chỉ gửi chúng một lần trong GAME_START.
        mode = "baseline" if "bushes" in state else "optimized"

        if mode == "baseline":
            assert state["bushes"] == start_a["bushes"]
            assert state["obstacles"] == start_a["obstacles"]
        else:
            assert "bushes" not in state
            assert "obstacles" not in state

        a.send("MOVE", directionX=1, directionZ=0)
        a.send("CHARGE_START")
        a.send("EXPLODE")

        a.until("PLAYER_EXPLODE")

        print(
            f"PASS ({mode}): room {code}, "
            f"map {start_a['mapId']}, "
            "2 clients, state sync, movement, explosion"
        )

    finally:
        a.close()
        b.close()


if __name__ == "__main__":
    main()
```
