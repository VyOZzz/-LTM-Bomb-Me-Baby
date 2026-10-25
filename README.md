# 💣 Bomb Me Baby

**Game đấu trường PvP 2.5D nhiều người chơi qua mạng TCP** — bài tập lớn môn **Lập trình mạng**, năm học 2026–2027.

Người chơi điều khiển một quả bóng lăn trên bản đồ, nhặt Energy để lớn lên, gồng bom rồi kích nổ để hạ đối thủ. Toàn bộ luật chơi do **Java server** quyết định (server-authoritative); **Unity client** chỉ gửi ý định điều khiển và hiển thị trạng thái server gửi về.

| | |
| --- | --- |
| **Chủ đề** | Chủ đề 2 – Phát triển game mạng (`final-project/Topics.md`) |
| **Giảng viên** | Thầy Ngô Văn Quyền |
| **Repository** | https://github.com/VyOZzz/-LTM-Bomb-Me-Baby |

### Thành viên nhóm

| STT | MSSV | Họ tên | Email | Phụ trách chính | Đóng góp |
| --- | --- | --- | --- | --- | ---: |
| 1 | ... | Vỹ (trưởng nhóm) | ... | Prototype ban đầu, Unity client, gameplay Hành trình, bản cuối | ...% |
| 2 | ... | Tiên | ... | Java server, selective sync, reconnect/timeout, kiểm thử, âm thanh | ...% |
| 3 | ... | Phương | ... | Ghép trận, công cụ đo và thực nghiệm, bản đồ và giao diện, bot test | ...% |
| | | | | **Tổng** | **100%** |

Công việc cụ thể của từng người: [mục 13](#13-phân-công-công-việc). Báo cáo đầy đủ: [docs/REPORT.md](docs/REPORT.md).

---

## Mục lục

1. [Giới thiệu](#1-giới-thiệu)
2. [Tính năng và luật chơi](#2-tính-năng-và-luật-chơi)
3. [Kiến trúc hệ thống](#3-kiến-trúc-hệ-thống)
4. [Giao thức mạng](#4-giao-thức-mạng)
5. [Điểm mới và kết quả thực nghiệm](#5-điểm-mới-novelty-và-kết-quả-thực-nghiệm)
6. [Công nghệ sử dụng](#6-công-nghệ-sử-dụng)
7. [Cấu trúc thư mục](#7-cấu-trúc-thư-mục)
8. [Yêu cầu hệ thống](#8-yêu-cầu-hệ-thống)
9. [Hướng dẫn cài đặt và chạy](#9-hướng-dẫn-cài-đặt-và-chạy)
10. [Kiểm thử và đo đạc](#10-kiểm-thử-và-đo-đạc)
11. [Xử lý sự cố](#11-xử-lý-sự-cố)
12. [Giới hạn và hướng phát triển](#12-giới-hạn-và-hướng-phát-triển)
13. [Phân công công việc](#13-phân-công-công-việc)
14. [Tài liệu](#14-tài-liệu)

---

## 1. Giới thiệu

### Bài toán

Game hành động nhiều người cần mọi người chơi thấy **cùng một trận đấu**, trong khi mỗi máy có độ trễ khác nhau và có thể mất kết nối bất kỳ lúc nào. Nếu client tự tính vị trí, máu, sát thương thì dễ lệch trạng thái và dễ gian lận. Ngoài ra, nếu server gửi toàn bộ trạng thái cho mọi người ở mỗi tick thì lượng dữ liệu tăng nhanh theo số người chơi, và client còn biết được vị trí của đối thủ đang ẩn.

### Mục tiêu

1. Xây dựng server TCP giữ trạng thái chính thức của trận, phục vụ nhiều phòng và nhiều client đồng thời.
2. Thiết kế protocol JSON rõ ràng, có sequence chống request trùng, heartbeat, timeout và reconnect.
3. Đề xuất cơ chế **đồng bộ trạng thái chọn lọc** (selective sync) và đo mức giảm băng thông so với baseline gửi toàn bộ.

### Phạm vi

- **Có:** 2 chế độ chơi, 3 bản đồ, phòng riêng bằng mã 6 ký tự, ghép trận ngẫu nhiên, tối đa 10 người/phòng, reconnect trong 10 giây, Unity client cho Windows.
- **Chưa có:** đăng nhập/tài khoản, mã hóa TLS, lưu lịch sử trận, chống gian lận toàn diện.

---

## 2. Tính năng và luật chơi

### Chế độ chơi

| Chế độ | Mô tả |
| --- | --- |
| **Đấu nhanh** | Tối đa 3 phút. Người sống cuối cùng thắng. Hết giờ mà còn nhiều người thì xếp hạng: mỗi lần hạ +3 điểm → tổng sát thương → còn sống → HP. 60 giây cuối, vị trí mọi người hiện trên minimap. |
| **Hành trình Tim Lửa** | 3 chương liên tiếp trên 3 bản đồ (`meadow` → `crossroads` → `crater`), mỗi chương tối đa 6 phút. Mang Mảnh Tim Lửa đặt lên bệ và giữ 30 giây không bị cướp để thắng chương. |

### Vào trận

- **Tìm trận:** server ghép ngẫu nhiên 2–4 người cùng chọn một chế độ và vào trận ngay.
- **Phòng riêng:** một người tạo phòng, gửi mã 6 ký tự cho bạn bè; trận bắt đầu khi có ít nhất 2 người và tất cả bấm **Sẵn sàng**.

### Điều khiển

| Phím | Hành động |
| --- | --- |
| `W A S D` / phím mũi tên | Di chuyển |
| Giữ `Space` → thả | Gồng bom → kích nổ (tự nổ sau 4 giây) |
| `E` | Khiên: chặn 100% sát thương trong 1,5 giây, phản 50% về người tấn công (hồi 9 giây) |
| `Shift` | Tăng tốc 0,9 giây, tốn một ít Size (hồi 5 giây) |
| `Q` | Thả Mảnh Tim Lửa (Hành trình) |
| Giữ `F` | Cướp mảnh khi đứng trong bệ (Hành trình) |
| `F3` | Bảng debug: server, chế độ đồng bộ, RTT |
| `Esc` / `Space` | Bỏ qua phim mở đầu |

### Luật chính

- 100 HP, không hồi sinh trong vòng. Bom Size 1 gồng đầy gây 80 sát thương ở tâm nổ, giảm dần theo khoảng cách; hồi bom 2,5 giây.
- Nhặt Energy: +0,12 Size (tối đa 2,6), +1 HP, tăng tốc nhẹ. Nổ xong Size về Base Size, phần Size đã tiêu rơi ra thành Energy.
- Người bị loại rơi Core; nhặt Core tăng Base Size thêm 0,1 (tối đa 1,4).
- Đá là vật cản. Bụi cho phép ẩn nấp: đối thủ ở ngoài không thấy người trong bụi. Chỉ thấy đối thủ trong bán kính 26 đơn vị.
- Mất kết nối có 10 giây để vào lại bằng resume token, quá hạn thì bị loại.

Thông số nằm trong `server/src/main/java/bombmebaby/GameRules.java` (chiến đấu), `GameRoom.java` (Đấu nhanh), `ShardObjective.java` (Hành trình) và `GameMap.java` (bản đồ).

---

## 3. Kiến trúc hệ thống

```text
 ┌──────────────┐ ┌──────────────┐       ┌──────────────┐
 │ Unity client │ │ Unity client │  ...  │ Unity client │   (2–10 người / phòng)
 └──────┬───────┘ └──────┬───────┘       └──────┬───────┘
        │   TCP lâu dài, JSON mỗi dòng (port 7777)
        ▼                ▼                      ▼
 ┌──────────────────────────────────────────────────────────┐
 │                 Java authoritative server                 │
 │  ServerSocket ── thread pool (64) ── Connection/client    │
 │                     │                  └─ writer queue    │
 │       ┌─────────────┼──────────────┐                     │
 │   Matchmaker    GameRoom (lock)  Sessions (resume token)  │
 │                     │                                     │
 │         Game loop 20 Hz ── snapshot 10 Hz ── SyncMode     │
 └──────────────────────────────────────────────────────────┘
```

| Thành phần | Nhiệm vụ |
| --- | --- |
| `BombServer` | Nhận kết nối, đọc frame JSON, kiểm tra `seq`, điều phối request, chạy game loop, dọn session hết hạn |
| `Connection` | Một luồng đọc + một luồng ghi riêng cho mỗi client; hàng đợi ghi tối đa 128 message, gộp snapshot liền nhau |
| `GameRoom` | Trạng thái một phòng: di chuyển, va chạm, nổ bom, nhặt đồ, thắng thua. Mọi thay đổi chạy dưới khóa của phòng |
| `Matchmaker` | Hàng đợi tìm trận theo chế độ, ghép 2–4 người |
| `SyncMode` | `BASELINE` gửi đầy đủ / `OPTIMIZED` lọc theo từng người xem |
| `ShardObjective` | Luật Mảnh Tim Lửa của chế độ Hành trình |
| Unity `BombNetwork` | Socket TCP, luồng ghi nền, inbox giới hạn, heartbeat, tự reconnect |
| Unity `BombGame` | Xử lý message, nội suy vị trí giữa hai snapshot, HUD, camera, âm thanh |

**Concurrency:** mỗi client có luồng đọc lấy từ thread pool và luồng ghi riêng; game loop chạy trên một luồng riêng. Thứ tự khóa luôn là `player → room → hàng đợi ghi` để tránh deadlock; ghép trận khóa các player theo thứ tự id. Client chậm làm đầy hàng đợi ghi sẽ bị đóng kết nối, không làm chậm cả phòng.

---

## 4. Giao thức mạng

- **Giao thức:** TCP, kết nối giữ suốt phiên chơi. Mỗi message là một JSON object trên một dòng UTF-8, tối đa 2048 ký tự.
- **Luồng chính:**

```text
Client                                   Server
  |------ HELLO {name, resumeToken} ------->|
  |<----- WELCOME {playerId, resumeToken} --|
  |------ FIND_MATCH / CREATE_ROOM -------->|
  |<----- MATCH_FOUND / ROOM_STATE ---------|
  |<----- GAME_START {map, obstacles} ------|
  |------ MOVE / CHARGE_START / EXPLODE --->|   (mỗi request có seq tăng dần)
  |<----- ACK {seq, processingMicros} ------|
  |<----- STATE_UPDATE (10 Hz) -------------|
  |------ PING ---------------------------->|
  |<----- PONG -----------------------------|
  |<----- GAME_OVER {winnerId, leaderboard}-|
  |------ LEAVE_ROOM / đóng socket -------->|
```

- **Ví dụ message:**

```json
{"type":"HELLO","name":"Bom A","resumeToken":""}
{"type":"MOVE","seq":4,"directionX":0.8,"directionZ":0.3}
{"type":"ERROR","code":"DUPLICATE_SEQUENCE"}
```

- **Xử lý lỗi:**

| Tình huống | Cách xử lý |
| --- | --- |
| JSON sai / thiếu trường | Trả `ERROR INVALID_MESSAGE`, giữ kết nối |
| Request trùng hoặc cũ | `seq` ≤ seq đã nhận → `DUPLICATE_SEQUENCE`, không thực hiện lại |
| Hành động sai trạng thái | `NOT_IN_ROOM`, `ACTION_UNAVAILABLE`, `ROOM_UNAVAILABLE`, `BOMB_COOLDOWN`… |
| Frame quá lớn | Đóng socket khi vượt 2048 ký tự kể cả chưa có newline |
| Client im lặng | Server đóng sau 6 giây không nhận message hợp lệ; client tự đóng sau 7 giây |
| Ghi bị treo | Đóng kết nối nếu một lần ghi kéo dài quá 3 giây hoặc hàng đợi đầy 128 |
| Mất kết nối | Giữ chỗ 10 giây; client gửi lại `HELLO` kèm `resumeToken` để khôi phục phòng và trạng thái |
| Quá tải | Tối đa 64 kết nối đồng thời |

Đặc tả đầy đủ các message: [docs/PROTOCOL.md](docs/PROTOCOL.md).

---

## 5. Điểm mới (Novelty) và kết quả thực nghiệm

### Selective state synchronization

| Khía cạnh | Baseline | Proposed (`OPTIMIZED`) |
| --- | --- | --- |
| Hình học bản đồ (đá, bụi) | Gửi lại trong mọi snapshot | Gửi một lần ở `GAME_START` hoặc khi reconnect |
| Người chơi ở xa / trong bụi | Gửi đủ tọa độ cho mọi người | Ẩn tọa độ với người không được phép thấy |
| Energy / Core | Gửi toàn bộ | Chỉ gửi vật phẩm trong tầm nhìn |
| Sự kiện nổ | Gửi tọa độ cho cả phòng | Không gửi tọa độ cho người không thấy nguồn nổ |
| Tác dụng phụ | Client biết vị trí đối thủ đang ẩn | Chống "map hack" ngay từ dữ liệu gửi đi |

Chuyển chế độ bằng `-Dsync.mode=baseline|optimized` khi chạy server.

### Kết quả đo

Đo ngày 22/10/2026: loopback, map `meadow`, 2/4/8 client giả lập, mỗi cấu hình 3 lượt × 30 giây, **18/18 lượt hoàn tất, 0 lỗi kết nối**.

| Client | Byte nhận/client/s (baseline) | Byte nhận/client/s (optimized) | Giảm |
| ---: | ---: | ---: | ---: |
| 2 | 94 850 | 20 777 | **78,1%** |
| 4 | 101 531 | 26 111 | **74,3%** |
| 8 | 115 043 | 43 547 | **62,1%** |

RTT trung bình ở cả hai chế độ đều khoảng 0,3–0,45 ms trên loopback; kết quả **không** chứng minh được optimized giảm RTT. Số liệu đo trên bản code ngày 22/10, trước các thay đổi gameplay 23–25/10. Cấu hình máy, dữ liệu thô và giới hạn của phép đo: [docs/EXPERIMENTS_FINAL.md](docs/EXPERIMENTS_FINAL.md), [experiments/final-2026-10-22-v2/](experiments/final-2026-10-22-v2/).

---

## 6. Công nghệ sử dụng

| Hạng mục | Công nghệ |
| --- | --- |
| Server | Java 17, `java.net.ServerSocket`/`Socket`, `ExecutorService` |
| Client | Unity 6.3 LTS (`6000.3.6f1`), C#, `System.Net.Sockets.TcpClient` |
| Giao thức | TCP, JSON theo dòng (protocol tự thiết kế) |
| Thư viện | Gson 2.11 (server), Newtonsoft.Json (client), JUnit 5 (test) |
| Build | Maven 3.9 (server), Unity batch build (client) |
| Script đo/test | Python 3 (chỉ dùng thư viện chuẩn), PowerShell |
| Tài nguyên | Asset CC0 của Kenney, âm thanh ghi nguồn trong `docs/ASSET_CREDITS.md`, `docs/AUDIO_CREDITS.md` |

---

## 7. Cấu trúc thư mục

```text
-LTM-Bomb-Me-Baby/
├── README.md                  ← file này
├── server/                    ← Java server (Maven)
│   ├── pom.xml
│   └── src/
│       ├── main/java/bombmebaby/   BombServer, GameRoom, Matchmaker, SyncMode, ...
│       └── test/java/bombmebaby/   test JUnit
├── LTM-Bomb-Me-Baby/          ← project Unity (client)
│   ├── Assets/Scenes/Main.unity
│   ├── Assets/Scripts/        BombNetwork, BombGame, BombProtocol, HUD, map, âm thanh...
│   ├── Assets/Editor/         công cụ build, kiểm tra, dựng scene
│   └── Assets/StreamingAssets/ phim mở đầu, lời dẫn
├── scripts/                   ← chạy server, build client, bot, đo đạc, đóng gói
├── experiments/               ← dữ liệu đo thô (CSV) và bảng tổng hợp
├── docs/                      ← protocol, thực nghiệm, báo cáo nháp, runbook demo, GDD
├── final-project/             ← yêu cầu đồ án của giảng viên
└── builds/                    ← bản build (bị Git bỏ qua, tạo bằng script)
```

---

## 8. Yêu cầu hệ thống

| | Tối thiểu | Máy nhóm đã dùng để đo |
| --- | --- | --- |
| Hệ điều hành | Windows 10/11 64-bit | Windows 11 |
| CPU | 4 nhân | Intel Core i7-11800H |
| RAM | 8 GB (16 GB nếu mở Unity Editor) | 8 GB |
| Ổ đĩa | ~1 GB cho bản build; ~10 GB nếu cài Unity Editor | |
| Mạng | Cùng LAN, hoặc server có IP public/port forwarding | Loopback |

| Phần mềm | Phiên bản | Cần khi |
| --- | --- | --- |
| JDK | 17 trở lên | Chạy server |
| Maven | 3.9 trở lên | Build/test server từ source |
| Unity Hub + Unity Editor | `6000.3.6f1` | Mở/sửa/build client từ source |
| Python | 3.10 trở lên | Chạy bot, smoke test, benchmark (không cần cài thêm thư viện) |
| Git | bất kỳ | Clone source |

> Chỉ chơi thì **không cần** cài Unity: chỉ cần bản build Windows và một máy chạy server có Java 17.

---

## 9. Hướng dẫn cài đặt và chạy

### Bước 1 — Lấy source code

```powershell
git clone https://github.com/VyOZzz/-LTM-Bomb-Me-Baby.git
cd -LTM-Bomb-Me-Baby
```

Hoặc giải nén thư mục `Source/` trong gói nộp bài.

Kiểm tra công cụ đã cài:

```powershell
java -version      # phải là 17 trở lên
mvn -v
python --version
```

### Bước 2 — Chạy server

Server **không cần database**; mọi trạng thái nằm trong bộ nhớ. Chọn một trong các cách sau.

**Cách A — script có sẵn (khuyên dùng):**

```powershell
powershell -ExecutionPolicy Bypass -File scripts/run_server.ps1
# Tùy chọn: -Port 7777  -SyncMode optimized|baseline  -DemoMap meadow|crossroads|crater
```

Script tự build JAR rồi chạy server ở chế độ `optimized`.

**Cách B — Maven:**

```powershell
cd server
mvn test                                                     # chạy toàn bộ test
mvn compile exec:java "-Dexec.args=7777" "-Dsync.mode=optimized"
```

**Cách C — đóng gói JAR, máy chạy chỉ cần Java:**

```powershell
cd server
mvn package
java -Dsync.mode=optimized -jar target/bomb-me-baby-server-1.0.0.jar 7777
```

Khi thấy dòng `Bomb Me Baby server on TCP port 7777` là server đã sẵn sàng.

**Cấu hình server:**

| Tham số | Mặc định | Ý nghĩa |
| --- | --- | --- |
| Đối số đầu tiên | `7777` | Cổng TCP |
| `-Dsync.mode` | `baseline` (cách B, C) / `optimized` (cách A) | Chế độ đồng bộ |
| `-Ddemo.map` hoặc biến môi trường `DEMO_MAP` | rỗng (ngẫu nhiên) | Khóa bản đồ Đấu nhanh khi demo |

### Bước 3 — Chạy client

**Cách A — dùng bản build Windows có sẵn:**

1. Mở thư mục bản build đi kèm bài nộp (ví dụ `builds/windows-final/`) và chạy `Bomb Me Baby.exe`. Khi gửi cho người khác, phải gửi **cả thư mục**, không chỉ file `.exe`.
2. Chỉ định server bằng một trong ba cách:
   - Tạo file `server.txt` cạnh file `.exe`, dòng đầu là `HOST:PORT`, ví dụ `192.168.1.20:7777`;
   - Chạy kèm tham số: `"Bomb Me Baby.exe" --server=192.168.1.20:7777`;
   - Nhập trong game ở mục **Cài đặt mạng nâng cao**. Client sẽ nhớ địa chỉ đã nhập.
3. Nhập tên → **Chơi** → chọn chế độ → **Tìm trận** hoặc **Tạo phòng riêng**.

**Cách B — chạy từ Unity Editor:**

1. Mở Unity Hub → **Add** → chọn thư mục `LTM-Bomb-Me-Baby/`. Hub sẽ yêu cầu cài Unity `6000.3.6f1` nếu máy chưa có.
2. Chờ import lần đầu (có thể mất vài phút), mở `Assets/Scenes/Main.unity`.
3. Nếu scene trống, chọn menu **Tools › Bomb Me Baby › Rebuild Main Scene**.
4. Nhấn **Play**. Mặc định client kết nối `127.0.0.1:7777`.

**Tự build bản Windows:**

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build_client.ps1 -Isolated
# Nếu Unity không nằm ở đường dẫn mặc định: thêm -Unity "D:/.../Unity.exe"
```

Kết quả nằm tại `builds/windows/BombMeBaby.exe`. Có thể build trong Editor qua **Tools › Bomb Me Baby › Build Windows**. Cờ `-Isolated` build trên một bản sao nên vẫn mở được Editor song song.

### Bước 4 — Chơi nhiều người

| Tình huống | Cách làm |
| --- | --- |
| Cùng một máy | Chạy server, rồi mở nhiều cửa sổ bản build (hoặc bản build + Unity Editor), tất cả dùng `127.0.0.1:7777` |
| Cùng Wi-Fi / LAN | Chạy server trên một máy, lấy IPv4 LAN bằng `ipconfig` (ví dụ `192.168.1.20`), mở firewall cho TCP `7777`; các máy khác nhập địa chỉ này |
| Khác mạng Internet | Chạy server trên VPS có IP public, hoặc mở port forwarding TCP `7777` trên router. Không dùng IP `192.168.x.x` qua Internet |
| Thiếu người để test | Thêm bot: `python scripts/bots.py --find QUICK --count 2` hoặc `python scripts/bots.py --room ABC123 --count 3` |

Mở firewall Windows cho server (PowerShell quyền Administrator):

```powershell
New-NetFirewallRule -DisplayName "Bomb Me Baby" -Direction Inbound -Protocol TCP -LocalPort 7777 -Action Allow
```

Kiểm tra từ máy người chơi:

```powershell
Test-NetConnection 192.168.1.20 -Port 7777     # TcpTestSucceeded phải là True
```

---

## 10. Kiểm thử và đo đạc

| Lệnh | Mục đích |
| --- | --- |
| `cd server; mvn test "-Dsync.mode=baseline"` | 115 test JUnit: kết nối, reconnect, sequence trùng, ghép trận, luật chơi, Hành trình |
| `cd server; mvn test "-Dsync.mode=optimized"` | Chạy lại toàn bộ test ở chế độ optimized |
| `python scripts/network_smoke.py [port]` | Smoke test qua TCP thật với 2 client giả lập (server phải đang chạy) |
| `python scripts/load_test.py --clients 4 --duration 30 --label optimized` | Đo RTT, byte, message, thời gian xử lý với N client |
| `powershell -ExecutionPolicy Bypass -File scripts/run_benchmark.ps1 -Duration 30 -Repeats 3 -Map meadow` | Chạy toàn bộ ma trận baseline/optimized × 2/4/8 client, ghi CSV vào `experiments/run-<thời gian>/` |
| `python scripts/summarize_experiments.py --summary <thư mục>/summary.csv` | Tổng hợp bảng kết quả từ CSV |

Kiểm tra Unity (Play Mode, build, staging): [docs/FINAL_VALIDATION.md](docs/FINAL_VALIDATION.md). Kịch bản demo trước giảng viên: [docs/DEMO_RUNBOOK.md](docs/DEMO_RUNBOOK.md).

---

## 11. Xử lý sự cố

| Hiện tượng | Nguyên nhân / cách khắc phục |
| --- | --- |
| `mvn` hoặc `java` không nhận lệnh | Chưa cài hoặc chưa thêm vào `PATH`; kiểm tra `JAVA_HOME` trỏ tới JDK 17+ |
| `Address already in use` khi chạy server | Cổng 7777 đang bị chiếm; tắt server cũ hoặc chạy cổng khác (`-Port 7778`) |
| Client báo không kết nối được | Server chưa chạy, sai IP/cổng, hoặc firewall chặn. Kiểm tra bằng `Test-NetConnection` |
| Gửi bản build cho bạn mà họ không vào được | `127.0.0.1` là máy của chính họ; phải nhập IP của máy chạy server |
| Bấm Tìm trận mà không vào phòng bạn tạo | Tìm trận chỉ ghép người đang tìm; muốn vào phòng riêng phải nhập mã phòng |
| Không vào được phòng đang chơi | Phòng `PLAYING` từ chối người mới (`ROOM_UNAVAILABLE`); chờ ván sau |
| Script `.ps1` bị chặn | Chạy kèm `powershell -ExecutionPolicy Bypass -File ...` |
| `python3` không có trên Windows | Dùng `python` hoặc `py` |
| Phim mở đầu không phát | Máy không giải mã được MP4; client tự chuyển sang ảnh minh họa |

---

## 12. Giới hạn và hướng phát triển

**Giới hạn hiện tại**

1. Benchmark chạy trên loopback với client giả lập, chưa đo trên LAN/Internet thật, chưa đo FPS của client.
2. Số liệu đo là của bản code ngày 22/10, chưa đo lại sau các thay đổi gameplay 23–25/10.
3. TCP thuần, chưa có TLS, đăng nhập hay lưu lịch sử trận; chống gian lận mới ở mức server bỏ qua input bất thường và sequence trùng.
4. Đồng bộ chọn lọc lọc theo người xem, chưa nén delta từng trường giữa hai snapshot.

**Hướng phát triển**

1. Gửi delta giữa hai snapshot, kết hợp UDP cho dữ liệu vị trí.
2. Đo trên LAN/Internet có độ trễ và mất gói nhân tạo; thử nhiều client hơn.
3. Thêm tài khoản, lưu kết quả trận vào database, bảng xếp hạng.
4. Thêm TLS và client-side prediction để giảm cảm giác trễ.

---

## 13. Phân công công việc

Tổng hợp từ lịch sử commit và [docs/TEAM_PLAN.md](docs/TEAM_PLAN.md).

### Vỹ — trưởng nhóm, Unity client, gameplay

| Thời gian | Công việc |
| --- | --- |
| 12–13/10 | Dựng prototype ban đầu: Java server TCP JSON, game loop 20 Hz, snapshot 10 Hz, va chạm, nhặt đồ, sát thương; Unity client và scene `Main`; 3 bản đồ; hỗ trợ 10 người/phòng |
| 13–14/10 | Lưu yêu cầu đồ án, kế hoạch phân công đầu tiên, concept arena và HUD, cốt truyện Ba Mảnh Tim Lửa |
| 20–21/10 | Server: mảnh Tim Lửa và bệ đá, luật mang/cướp/phá ấn mảnh (`ShardObjective.java`, `ShardStealMechanicsTest`), `JOIN_ROOM` kiểm tra chế độ phòng |
| 20–21/10 | Unity: menu chọn chế độ, HUD, minimap, lời dẫn từ `story.json`, cooldown bom, màn mở đầu bằng video, bảng điểm và màn kết quả |
| 25/10 | Hoàn thiện gameplay và bố cục bản đồ (`BombMapLayouts.cs`), chế độ thử cục bộ, bảng xếp hạng Đấu nhanh khi hết giờ, dọn asset, viết lại README |

### Tiên — Java server, đồng bộ chọn lọc, kết nối, kiểm thử

| Thời gian | Công việc |
| --- | --- |
| 15–17/10 | **Novelty:** selective sync (`SyncMode` baseline/optimized); sửa reconnect, token sai/hết hạn, request trùng, dọn phòng; test `ConnectionTest`, `ServerFixesTest`; tài liệu bàn giao `THANH_VIEN_A_*` |
| 20/10 | `GameMode` Đấu nhanh và Hành trình, tính điểm, `JOURNEY_COMPLETE`; sửa lộ vị trí trong bụi qua event nổ/Core/snapshot khôi phục; cập nhật `PROTOCOL.md` |
| 21–22/10 | Heartbeat và timeout hai phía, hàng đợi ghi, đếm ngược reconnect trên Unity; harness kiểm tra Unity (`BombValidation.cs`), script build |
| 21–23/10 | Âm thanh: `BombAudio`, SFX, nhạc sảnh và 3 bản đồ, chỉnh tiếng bom và khiên |
| 22–24/10 | Sửa luồng trận, phá ấn, quan sát sau khi bị loại; ổn định ghép trận (ticket có generation); `package_project.py`; tổng hợp `EXPERIMENTS_FINAL.md`, `FINAL_VALIDATION.md` |

### Phương — ghép trận, đo đạc thực nghiệm, bản đồ và giao diện

| Thời gian | Công việc |
| --- | --- |
| 15/10 | Kịch bản Hành trình Ba Mảnh (`STORY_SCRIPT.md`); `load_test.py` đo RTT, byte, message và thời gian xử lý với N client |
| 19/10 | `run_benchmark.ps1`, `summarize_experiments.py`; sửa `network_smoke.py` cho chế độ optimized; chạy 18 lượt đo baseline/optimized với 2/4/8 client (`EXPERIMENTS.md`); mẫu `AUDIO_CREDITS.md`, `PLAYTEST_MAPS.md` |
| 21/10 | Ghép trận ngẫu nhiên `FIND_MATCH`/`CANCEL_MATCH` trên server (`Matchmaker.java`) kèm test; nút Tìm trận và trạng thái hàng chờ trên Unity |
| 22/10 | Nhập asset CC0 của Kenney; dựng lại 3 bản đồ bằng asset; làm mới HUD, menu, overlay, VFX |
| 23/10 | Khóa Lửa dạng thanh máu (`ShardObjective.java`) kèm test; bot thêm người chơi (`scripts/bots.py`, `BombDevBots.cs`); công cụ test trong Editor |

---

## 14. Tài liệu

| Tài liệu | Nội dung |
| --- | --- |
| [docs/REPORT.md](docs/REPORT.md) | Báo cáo dự án theo cấu trúc paper (`final-project/Instruction.md`, mục 8) |
| [docs/PROTOCOL.md](docs/PROTOCOL.md) | Đặc tả đầy đủ protocol TCP JSON |
| [docs/EXPERIMENTS_FINAL.md](docs/EXPERIMENTS_FINAL.md) | Thiết lập, kết quả, giới hạn thực nghiệm |
| [docs/REPORT_DRAFT.md](docs/REPORT_DRAFT.md) | Bản nháp báo cáo cũ (24/10) |
| [docs/FINAL_VALIDATION.md](docs/FINAL_VALIDATION.md) | Kết quả kiểm thử server và Unity |
| [docs/DEMO_RUNBOOK.md](docs/DEMO_RUNBOOK.md) | Kịch bản demo |
| [docs/THANH_VIEN_A_HUONG_DAN.md](docs/THANH_VIEN_A_HUONG_DAN.md) | Giải thích code server từng phần |
| [docs/STORY_SCRIPT.md](docs/STORY_SCRIPT.md) | Kịch bản Hành trình Tim Lửa |
| [docs/GDD Bomb Me Baby.docx](docs/GDD%20Bomb%20Me%20Baby.docx) | Game Design Document |
| [docs/ASSET_CREDITS.md](docs/ASSET_CREDITS.md), [docs/AUDIO_CREDITS.md](docs/AUDIO_CREDITS.md) | Nguồn và giấy phép hình ảnh, âm thanh |

**Tham khảo**

1. W. Eddy (ed.), *Transmission Control Protocol (TCP)*, RFC 9293, 2022. https://www.rfc-editor.org/rfc/rfc9293.html
2. Oracle, *Java SE 17 API – java.net*. https://docs.oracle.com/en/java/javase/17/docs/api/java.base/java/net/package-summary.html
3. Unity Technologies, *Unity 6.3 Manual*. https://docs.unity3d.com/Manual/index.html
4. G. Fiedler, *Snapshot Interpolation*, 2014. https://gafferongames.com/post/snapshot_interpolation/
5. Google, *Gson*. https://github.com/google/gson
6. *JUnit 5 User Guide*. https://junit.org/junit5/
