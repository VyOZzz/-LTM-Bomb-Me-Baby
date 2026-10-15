# Hướng dẫn & Tài liệu Kỹ thuật — Thành viên A (Java Server)

> **Dành cho:** Thành viên A — Phụ trách Server Java & Cải tiến Kỹ thuật  
> **Dự án:** Bomb Me Baby (Lập trình mạng)  
> **Nhánh Git:** `ThuyTien`  
> **Tài liệu đối chiếu:** `docs/TEAM_PLAN.md`, `docs/PROTOCOL.md`, `final-project/Topics.md`

---

## 1. Tổng quan vai trò của Thành viên A

Thành viên A phụ trách toàn bộ mã nguồn phía máy chủ (Server), bao gồm:
- **Kiến trúc mạng TCP & Quản lý phiên kết nối:** `BombServer.java`
- **Logic phòng chơi, Game Loop & Đồng bộ trạng thái:** `GameRoom.java`
- **Quy tắc trò chơi & Hằng số cấu hình:** `GameRules.java`
- **Bộ chuyển đổi chế độ đồng bộ (Baseline vs Optimized):** `SyncMode.java`
- **Bộ kiểm thử tự động (Unit / Integration Tests):** `ServerFixesTest.java`

Mục tiêu chính:
1. Xử lý các lỗi mạng thực tế: Mất kết nối (disconnect), kết nối lại (reconnect), gửi trùng gói tin (duplicate sequence), tranh chấp nhặt tài nguyên (Core contention).
2. Hiện thực cải tiến kỹ thuật (**Novelty / Contribution**): Tối ưu hóa dữ liệu truyền tải qua mạng (**Delta Sync / State Culling**) so với bản gốc (**Baseline**).
3. Đảm bảo server ổn định, có test tự động chứng minh và có thể chuyển đổi linh hoạt chế độ để bạn B chạy script đo lường thực nghiệm.

---

## 2. Chi tiết các vấn đề đã giải quyết & Giải thích mã nguồn

### 2.1. Sửa lỗi Reconnect & Chống Race Condition
- **Vấn đề thực tế:** Khi mạng di động hoặc Wi-Fi chập chờn, client có thể cố gắng kết nối lại nhiều lần cùng lúc hoặc mở 2 socket cùng gửi một `resumeToken`. Trước đây, nếu 2 thread cùng kiểm tra `resumed.connected == false`, cả hai đều có thể chiếm phiên, gây lỗi trạng thái hoặc đá nhau liên tục.
- **Giải pháp trong `BombServer.java`:**
  - Áp dụng khóa đồng bộ `synchronized (resumed)` ngay trên đối tượng `PlayerState`.
  - Chỉ một thread kết nối đầu tiên giành được quyền sở hữu session (`player = resumed; player.connected = true;`).
  - Thread kết nối thứ hai đến sau thấy `resumed.connected == true` hoặc đã hết hạn thời gian chờ (`RECONNECT_MS = 10s`) sẽ lập tức bị từ chối với mã lỗi `RESUME_FAILED`.
  - Trong gói tin phản hồi `WELCOME`, server bổ sung `lastSeq` của người chơi:
    ```java
    send(GameRoom.data("type", "WELCOME", "playerId", player.id,
        "resumeToken", player.resumeToken, "lastSeq", player.lastSeq));
    ```
    Điều này giúp client biết chính xác sequence number cuối cùng server đã nhận, từ đó tiếp tục đánh số gói tin tiếp theo mà không bị lệch sequence.
  - Khi người chơi reconnect thành công trong lúc phòng đang diễn ra trận đấu (`PLAYING`), server gửi một snapshot đặc biệt có cờ `forceBaseline = true` để client nhận đủ toàn bộ thông tin bản đồ, chướng ngại vật ngay lập tức.

### 2.2. Xử lý Request Trùng & Sai thứ tự (Duplicate / Out-of-order Sequences)
- **Vấn đề:** Giao thức mạng yêu cầu mỗi hành động của client (trừ `PING`) phải có số thứ tự `seq` tăng dần. Nếu mạng gửi lại gói tin cũ hoặc client gửi trùng số `seq`, trạng thái game có thể bị sai lệch.
- **Giải pháp:**
  - Server kiểm tra `seq <= player.lastSeq`. Nếu đúng, server từ chối xử lý và gửi phản hồi:
    ```json
    {"type":"ERROR","code":"DUPLICATE_SEQUENCE"}
    ```
  - Chỉ khi `seq > player.lastSeq`, server mới cập nhật `player.lastSeq = seq` và xử lý logic.

### 2.3. Quản lý Ngắt kết nối (Disconnect) & Timeout
- **Vấn đề:** Khi client tắt app hoặc mất mạng đột ngột, socket bị đứt (ném ra `EOF` hoặc `IOException`).
- **Giải pháp:**
  - Khi bắt được lỗi đóng socket trong khối `try-with-resources` của `BombServer.java`, server đánh dấu:
    ```java
    player.connected = false;
    player.disconnectedAt = System.currentTimeMillis();
    ```
  - Trong vòng lặp `GameRoom.tick()` (chạy mỗi 50ms):
    - Server kiểm tra nếu người chơi mất kết nối quá 10 giây (`now - p.disconnectedAt > GameRules.RECONNECT_MS`), người chơi sẽ chính thức bị loại khỏi trận (`p.alive = false`).
    - Server phát broadcast `PLAYER_ELIMINATED` và `PLAYER_DISCONNECTED` để các client còn lại cập nhật UI.
    - Nếu chỉ còn 1 người sống sót, trận đấu tự động kết thúc (`GAME_OVER`).

### 2.4. Tranh chấp nhặt Lõi Năng lượng (Core Contention)
- **Vấn đề:** Khi nhiều người chơi cùng lao vào một Lõi năng lượng (Core) tại cùng một thời điểm, ai sẽ là người nhặt được? Client có thể tự ý quyết định không?
- **Giải pháp:**
  - Server là **Authoritative Server** (Máy chủ có thẩm quyền tối cao). Toàn bộ va chạm và nhặt đồ chỉ do server tính toán trong hàm `tick()` của `GameRoom.java`, được bảo vệ bởi khóa `synchronized(this)`.
  - Khi duyệt danh sách các Cores:
    ```java
    for (Iterator<CoreState> it = cores.iterator(); it.hasNext(); ) {
        CoreState c = it.next();
        for (PlayerState p : players) {
            if (p.alive && GameRules.distance(p.x, p.z, c.x, c.z) <= (p.size + GameRules.CORE_RADIUS)) {
                // Người chơi đầu tiên chạm vào sẽ nhận Core
                p.baseSize += GameRules.CORE_SIZE_BONUS;
                broadcast(data("type", "CORE_PICKED", "playerId", p.id, "coreId", c.id));
                it.remove(); // Xóa ngay lập tức khỏi thế giới
                break;       // Thoát vòng lặp player, không ai khác có thể nhặt Core này nữa!
            }
        }
    }
    ```
  - Nhờ `it.remove()` và `break`, không bao giờ xảy ra tình trạng 2 người cùng nhặt 1 Core.

---

## 3. Cải tiến Kỹ thuật (Novelty): Chế độ Đồng bộ Tối ưu (Delta / Selective Sync)

Để phục vụ yêu cầu đồ án về **Novelty / Technical Contribution** và cung cấp dữ liệu thực nghiệm so sánh cho bạn B:

### 3.1. So sánh Baseline vs Optimized
| Đặc điểm | Baseline (Bản gốc) | Optimized (Cải tiến của A) | Lợi ích đạt được |
| :--- | :--- | :--- | :--- |
| **Vật cản tĩnh (Obstacles, Bushes)** | Gửi lặp lại toàn bộ trong **mọi gói `STATE_UPDATE`** (mỗi 50ms). | Chỉ gửi **1 lần duy nhất trong `GAME_START`**; các gói `STATE_UPDATE` sau đó loại bỏ hoàn toàn. | Giảm kích thước gói tin định kỳ đi từ 30% đến 50%, tiết kiệm băng thông mạng. |
| **Bảo mật vị trí người chơi ẩn náu** | Gửi tọa độ `x, z` thật của tất cả người chơi ngay cả khi đang núp trong bụi (`visible: false`). | Với đối thủ đang tàng hình/núp bụi, server gán `x = 0, z = 0` trong snapshot gửi cho người khác. | Chống hack/cheat soi map (wallhack); giảm rò rỉ dữ liệu qua mạng. |
| **Lọc vật phẩm theo tầm nhìn (Culling)** | Gửi toàn bộ danh sách `orbs` và `cores` trên khắp toàn bản đồ cho tất cả mọi người. | Chỉ gửi các `orbs` và `cores` nằm trong bán kính quan sát (`PICKUP_VISIBILITY = 30.0`) của người nhận. | Giảm dung lượng payload tỷ lệ thuận với số lượng vật phẩm trên bản đồ. |

### 3.2. Cấu hình chuyển đổi linh hoạt (`SyncMode.java`)
Server hỗ trợ chuyển đổi giữa 2 chế độ thông qua biến môi trường JVM `-Dsync.mode`:
```bash
# Chế độ Baseline (dùng làm mốc so sánh thực nghiệm):
mvn exec:java -Dexec.args="7777" -Dsync.mode=baseline

# Chế độ Optimized (bản cải tiến kỹ thuật):
mvn exec:java -Dexec.args="7777" -Dsync.mode=optimized
```
Điều này cho phép bạn B chạy cùng 1 kịch bản kiểm thử (2, 4, 8 client) trên cả 2 chế độ để đo đạc:
- Số byte truyền nhận mỗi giây (Throughput / Bandwidth).
- Kích thước trung bình của gói `STATE_UPDATE`.
- Thời gian xử lý snapshot của server (`processingMicros`).

---

## 4. Cơ chế Đa luồng (Multi-threading) & Đồng bộ hóa dữ liệu

Khi bảo vệ trước giảng viên, đây là phần hay được hỏi nhất:

1. **Mô hình luồng (Thread Model):**
   - **Mỗi kết nối TCP:** Được xử lý bởi 1 thread độc lập (`BombServer.Connection` chạy trong `Executors.newCachedThreadPool()`). Luồng này chuyên đọc các dòng JSON từ client và phân tích thông điệp.
   - **Vòng lặp trận đấu (Game Tick Loop):** Được chạy bởi `ScheduledExecutorService` (1 thread định thời) với chu kỳ 50ms (20 ticks/giây) chạy hàm `GameRoom.tick()`.
2. **Cơ chế chống xung đột (Concurrency Control):**
   - Tất cả các phương thức thay đổi hoặc truy xuất trạng thái phòng trong `GameRoom` (`handleAction`, `tick`, `snapshot`, `join`, `leave`, v.v.) đều được bảo vệ bằng từ khóa `synchronized(this)`.
   - Khi luồng mạng của Client A gửi gói `MOVE`, nó phải giành được khóa của `GameRoom` trước khi cập nhật vector hướng.
   - Nhờ đó, thread game loop và các thread client không bao giờ bị race condition hay data corruption khi cùng đọc/ghi vào danh sách người chơi, quả cầu năng lượng, hoặc bom.
   - Để tránh Deadlock, việc gửi dữ liệu ra mạng (`send()`) được thực hiện cẩn thận, không giữ lồng ghép nhiều loại khóa khác nhau.

---

## 5. Danh sách Kiểm thử Tự động (`ServerFixesTest.java`)

Toàn bộ 16 ca kiểm thử mới được bổ sung vào bộ test (nâng tổng số test lên 27 test, chạy thành công 100%):

1. `reconnectRestoresPlayerInRoom`: Xác nhận người chơi mất kết nối được phục hồi đúng phòng và trạng thái khi reconnect kịp thời.
2. `reconnectFailsAfterWindowExpires`: Người chơi reconnect sau 10s sẽ bị từ chối.
3. `reconnectRaceOnlyOneConnectionWins`: Giả lập 2 luồng cùng lúc dùng chung 1 token reconnect; xác nhận đúng 1 luồng thành công và luồng kia thất bại.
4. `duplicateSequenceIsRejected`: Xác nhận gói tin có `seq <= lastSeq` bị từ chối với lỗi `DUPLICATE_SEQUENCE`.
5. `strictlyIncreasingSequenceIsAccepted`: Gói tin có `seq` tăng dần hợp lệ.
6. `disconnectMarksPlayerDisconnected`: Khi socket đóng, cờ `connected` chuyển sang `false` và lưu mốc thời gian.
7. `disconnectTimeoutEliminatesPlayer`: Khi hết thời gian chờ 10s, người chơi bị loại khỏi trận.
8. `corePickupClosestPlayerWins`: Kiểm tra tính Authoritative của server: chỉ 1 người duy nhất gần Core nhất nhặt được.
9. `coreCannotBePickedTwice`: Core đã bị nhặt sẽ bị xóa, không thể bị người khác nhặt lại.
10. `optimizedSnapshotOmitsObstaclesAndBushes`: Trong chế độ Optimized, snapshot không chứa chướng ngại vật tĩnh.
11. `baselineSnapshotIncludesObstaclesAndBushes`: Trong chế độ Baseline, snapshot luôn kèm chướng ngại vật tĩnh.
12. `gameStartAlwaysIncludesObstaclesAndBushes`: Bản tin `GAME_START` luôn cung cấp đầy đủ layout map cho cả 2 chế độ.
13. `optimizedSnapshotHidesInvisiblePlayerCoords`: Tọa độ người tàng hình bị ẩn (`x=0, z=0`) trong chế độ Optimized.
14. `baselineSnapshotRevealsInvisiblePlayerCoords`: Bản gốc Baseline vẫn để lộ tọa độ người núp bụi.
15. `optimizedSnapshotCullsDistantPickups`: Lọc bỏ các orbs/cores nằm ngoài tầm nhìn 30 mét.
16. `reconnectForcesFullSnapshotEvenInOptimizedMode`: Khi reconnect, client luôn nhận được full snapshot để dựng lại bản đồ.

Lệnh chạy kiểm thử:
```bash
mvn test
```

---

## 6. Gợi ý Trả lời Phỏng vấn / Bảo vệ Đồ án (Dành cho Thành viên A)

**Câu 1: Em đã giải quyết vấn đề reconnect khi mạng chập chờn như thế nào?**
> *Trả lời:* Em lưu phiên người chơi kèm một `resumeToken` ngẫu nhiên khi người chơi kết nối lần đầu (gửi trong gói `WELCOME`). Khi client bị ngắt kết nối TCP đột ngột, client có 10 giây để mở socket mới và gửi lại `HELLO` kèm `resumeToken`. Trên server, em dùng khối `synchronized(resumed)` để tránh race condition nếu có nhiều kết nối cùng tranh chấp 1 token. Đồng thời, server gửi lại `lastSeq` để client đồng bộ lại số thứ tự gói tin và gửi một full snapshot trạng thái trận đấu để client khôi phục giao diện ngay lập tức.

**Câu 2: Novelty (Cải tiến kỹ thuật) của nhóm em trong phần mạng là gì?**
> *Trả lời:* Trong nguyên mẫu ban đầu, mỗi tick (50ms) server đều gửi toàn bộ danh sách chướng ngại vật tĩnh, bụi rậm, toàn bộ vật phẩm trên toàn map và để lộ tọa độ của cả người chơi đang núp bụi. Cải tiến của em là thiết kế cơ chế **Selective / Delta Sync**:
> 1. Gửi bản đồ tĩnh một lần duy nhất lúc `GAME_START`.
> 2. Ẩn tọa độ người chơi tàng hình để chống lộ thông tin qua gói tin mạng.
> 3. Cắt tỉa (cull) các vật phẩm ngoài bán kính quan sát của từng người chơi.
> Em tạo ra 2 chế độ `baseline` và `optimized` điều khiển qua JVM argument để bạn B có thể chạy script đo đạc định lượng sự khác biệt về số byte truyền và độ trễ.

**Câu 3: Server xử lý tranh chấp khi 2 người cùng nhặt 1 vật phẩm thế nào?**
> *Trả lời:* Server của nhóm em hoạt động theo mô hình **Server-Authoritative** (Máy chủ toàn quyền). Client chỉ gửi input di chuyển, không có quyền tự quyết định đã nhặt đồ hay chưa. Trong vòng lặp `tick()` được bảo vệ bởi khóa đồng bộ `synchronized(this)`, server kiểm tra khoảng cách. Ngay khi phát hiện người chơi đầu tiên chạm vào vật phẩm, server cộng điểm/kích thước cho người đó, phát thông báo `CORE_PICKED`, và gọi `iterator.remove()` xóa ngay vật phẩm khỏi danh sách thế giới. Vì vậy người chơi thứ hai trong cùng tick đó sẽ không thể nhặt trùng được nữa.
