# Thành viên A — Học và hiểu phần Java server từ đầu

Cập nhật ngày 15/10/2026. Đọc cùng [README](../README.md), [bàn giao cho T và B](THANH_VIEN_A_BAN_GIAO.md) và [kết quả kiểm thử](THANH_VIEN_A_KIEM_THU.md).

Tài liệu này giải thích code thực tế trong thư mục `server/`. Nó thay thế bản hướng dẫn cũ có một số mô tả chưa đúng với code. Không cần hiểu toàn bộ Java trước khi đọc; hãy đi lần lượt từ mục 1 đến mục 6, chạy test, rồi đọc sâu phần khóa và đồng bộ.

## 1. Bạn phụ trách gì?

Bạn A phụ trách chương trình Java chạy trên máy chủ: nhận kết nối, tạo/vào/rời phòng, giữ trạng thái trận, nhận hành động, xử lý mất kết nối và gửi kết quả cho Unity. Bạn cũng viết test chứng minh server xử lý đúng và duy trì hai chế độ đồng bộ để B so sánh.

T phụ trách Unity: đọc bàn phím, vẽ game, phát âm thanh, hiển thị HUD và xử lý bản tin server. B phụ trách map, script đo mạng và số liệu thực nghiệm. Khi đổi tên trường, bỏ trường hay đổi ý nghĩa dữ liệu JSON, cả ba phải thống nhất trước khi tích hợp.

**Kết quả hiện tại:** đã sửa thêm các lỗi vòng đời kết nối, tăng chất lượng kiểm thử và giữ baseline mặc định. Chế độ optimized đã tồn tại từ commit `dc255a3`; Unity hiện còn cần T tích hợp. Chưa có xác nhận T chốt protocol, nên đợt này không thêm loại bản tin hay trường JSON mới. Phần Hành trình Ba Mảnh trong ảnh phân công là công việc sau khi chốt protocol, chưa được triển khai ở đây.

## 2. Những từ cần biết

| Từ | Hiểu đơn giản | Ví dụ trong dự án |
| --- | --- | --- |
| Client | Chương trình người chơi sử dụng | Game Unity trên máy bạn |
| Server | Chương trình giữ kết quả chính thức | `BombServer` chạy bằng Java |
| TCP | Kênh truyền byte có thứ tự, đáng tin cậy khi kết nối còn hoạt động | Socket giữa Unity và Java |
| Socket | Một đầu của kết nối mạng | `Socket socket` trong `Connection` |
| JSON | Văn bản có cặp tên và giá trị | `{"type":"MOVE","seq":5}` |
| Protocol | Quy ước hai phía hiểu dữ liệu như thế nào | `MOVE` cần hướng, không nhận tọa độ tự khai báo |
| Session | Phiên của một người chơi, có thể tồn tại qua nhiều socket | `PlayerState` cùng `resumeToken` |
| Room | Một nhóm người chơi chung trận | `GameRoom` |
| State | Dữ liệu hiện tại | HP, tọa độ, Core, giai đoạn trận |
| Tick | Một lần server tính diễn biến | Mục tiêu mỗi 50 ms |
| Snapshot | Bản chụp dữ liệu tại một thời điểm | `STATE_UPDATE` |
| Thread | Một luồng công việc có thể chạy cùng luồng khác | Luồng đọc socket và luồng game |
| Race condition | Kết quả sai do các luồng xen kẽ ở thời điểm không phù hợp | Hai socket cùng nhận một session |
| Lock/monitor | Quyền vào một vùng xử lý, mỗi lúc một luồng giữ | `synchronized (room)` |
| Baseline | Cách làm dùng làm mốc so sánh | Gửi map tĩnh trong mỗi snapshot |
| Culling | Bỏ bớt dữ liệu không cần gửi cho người nhận | Không gửi Core cách người nhận hơn 30 đơn vị |

Một số cú pháp Java bạn sẽ gặp:

```java
PlayerState p = new PlayerState("An", null);
```

`PlayerState` là kiểu dữ liệu; `p` là tên biến; `new` tạo một đối tượng. `p.hp` là HP của đối tượng đó. `null` nghĩa là chưa có đối tượng được tham chiếu; trong test, người chơi thường không có kết nối thật.

`if` kiểm tra điều kiện, `for` lặp, `return` kết thúc hàm, `!` là phủ định, `&&` yêu cầu cả hai điều kiện đúng. `==` so sánh giá trị nguyên thủy hoặc danh tính đối tượng; khi so sánh nội dung chuỗi Java thường dùng `.equals()`.

`Map<String, PlayerState>` giống danh bạ: dùng chuỗi token để tìm người chơi. `List<PlayerState>` là danh sách người trong phòng. `final` ngăn gán lại biến tham chiếu, **không** khiến mọi dữ liệu bên trong đối tượng trở thành bất biến.

## 3. Bản đồ các file

| File | Nên tìm hàm/biến nào? | Vai trò |
| --- | --- | --- |
| [BombServer.java](../server/src/main/java/bombmebaby/BombServer.java) | `main`, `run`, `Connection.run`, `handle`, `resume`, `disconnect`, `withSessionState` | TCP, session, điều phối request, vòng đời kết nối |
| [PlayerState.java](../server/src/main/java/bombmebaby/PlayerState.java) | `connection`, `room`, `lastSeq`, `connected`, `disconnectedAt` | Dữ liệu của một người chơi |
| [GameRoom.java](../server/src/main/java/bombmebaby/GameRoom.java) | `join`, `leave`, `ready`, `start`, `action`, `update`, `pickupCores`, `snapshot` | Phòng, luật diễn biến trận, bản tin trạng thái |
| [GameRules.java](../server/src/main/java/bombmebaby/GameRules.java) | `RECONNECT_MS`, `MIN_SPAWN_DISTANCE`, `damage` | Hằng số và công thức game |
| [SyncMode.java](../server/src/main/java/bombmebaby/SyncMode.java) | `BASELINE`, `OPTIMIZED`, `active` | Chọn cách đồng bộ |
| [ConnectionTest.java](../server/src/test/java/bombmebaby/ConnectionTest.java) | Các hàm có `@Test` | Kiểm tra bằng socket TCP thật trên máy |
| [ServerFixesTest.java](../server/src/test/java/bombmebaby/ServerFixesTest.java) | Core, timeout, snapshot | Kiểm tra trạng thái phòng và hai chế độ |
| [GameRoomTest.java](../server/src/test/java/bombmebaby/GameRoomTest.java) | Movement, shield, spawn, game over | Kiểm tra gameplay, có cả hồi quy spawn 10 người |

`GameMap.java` là dữ liệu ba bản đồ do B phụ trách. Đợt sửa này không đổi layout map hay công thức sát thương.

## 4. Theo dõi một trận từ đầu đến cuối

```mermaid
sequenceDiagram
    participant U as Unity client
    participant S as BombServer.Connection
    participant R as GameRoom
    U->>S: HELLO
    S-->>U: WELCOME (id và token)
    U->>S: CREATE_ROOM hoặc JOIN_ROOM + seq
    S->>R: join(player)
    R-->>U: ROOM_STATE
    U->>S: READY + seq
    S->>R: ready(player, true)
    R-->>U: GAME_START và snapshot đầu
    U->>S: MOVE + seq + hướng
    S->>R: action(...)
    loop Mỗi 50 ms theo lịch
        S->>R: update(now, dt)
        Note over R: Tính di chuyển, nhặt đồ, sát thương
    end
    R-->>U: STATE_UPDATE mỗi 2 tick (~100 ms)
    R-->>U: GAME_OVER khi đủ điều kiện
```

Luồng ví dụ:

1. Unity mở TCP đến IP server, cổng mặc định `7777`.
2. `Connection.run()` đọc từng dòng. `JsonParser` biến dòng chữ thành JSON để Java truy cập.
3. `HELLO` đầu tiên tạo session hoặc yêu cầu nối lại session cũ.
4. `CREATE_ROOM` tạo phòng; `JOIN_ROOM` tìm phòng theo mã. Server trả `ROOM_STATE` để Unity hiển thị danh sách người chơi.
5. Khi mọi người đã sẵn sàng và còn kết nối, `ready()` gọi `start()`.
6. `MOVE` chỉ đặt hướng `dx`, `dz`. `update()` mới dùng thời gian `dt` và tốc độ để tính vị trí, kiểm tra vật cản.
7. `snapshot()` tạo dữ liệu gửi cho từng người. Unity nhận và nội suy hình ảnh cho mượt.
8. Người cuối cùng còn sống thắng. Phòng một người là chế độ thử, không tự kết thúc ngay chỉ vì có một người sống.

Ba giai đoạn phòng là `WAITING` (chờ), `PLAYING` (đang chơi), `FINISHED` (đã kết thúc). Sẵn sàng lại có thể bắt đầu vòng mới.

**Điểm phải nhớ:** game loop là 20 Hz, snapshot định kỳ là 10 Hz. Ngoài lịch định kỳ, server còn gửi snapshot ngay lúc bắt đầu và khi reconnect. Không viết trong báo cáo rằng mọi snapshot được gửi mỗi 50 ms.

## 5. Vì sao client không tự quyết định nhặt Core?

Nếu Unity được tự gửi “tôi có 999 HP” rồi server tin ngay thì người chơi có thể sửa client để gian lận. Ở đây client chỉ gửi ý định như đi sang phải, bắt đầu nạp hoặc nổ. Server tự tính HP, va chạm, ai chết và ai nhặt Core. Đó là mô hình **server authoritative**.

Đọc `pickupCores()` trong `GameRoom.java`:

```java
if (!p.alive || !p.connected) continue;
double distance = GameRules.distance(p.x, p.z, core.x, core.z);
if (distance < 0.55 * p.size + 0.45 && distance < bestDistance) {
    closest = p;
    bestDistance = distance;
}
```

Đoạn này tìm người **gần nhất trong số người đủ điều kiện**, không phải ai gửi request nhanh nhất. Nếu hai người có khoảng cách bằng nhau, người xuất hiện trước trong danh sách phòng giữ ưu tiên vì điều kiện là `<`, không phải `<=`.

Sau khi tìm được người nhận:

```java
cores.remove(i);
closest.baseSize = Math.min(GameRules.MAX_BASE, closest.baseSize + 0.1);
closest.size = Math.max(closest.size, closest.baseSize);
```

Core được xóa và chỉ một người tăng Base Size. Hàm này được gọi trong `update()` đang giữ khóa phòng, nên luồng khác không thể chen vào giữa để cùng nhặt Core. Bản tin thực tế là `CORE_PICKED` với `pickupId`, không phải `coreId`.

Quy tắc chọn người gần nhất và khóa phòng đã có trong code đầu vào. Đợt này bổ sung test cạnh tranh hai tick, khoảng cách bằng nhau, ngoài tầm và người không đủ điều kiện; không nhận phần có sẵn thành thuật toán mới.

## 6. Request trùng và số thứ tự `seq`

Giả sử người chơi gửi:

```json
{"type":"MOVE","seq":5,"directionX":1,"directionZ":0}
```

Server lưu `lastSeq = 5`. Nếu cùng session gửi lại `seq = 5` hoặc gửi `seq = 3`, server trả `DUPLICATE_SEQUENCE` và không thực hiện hành động. `seq = 6` được xét tiếp.

```java
if (seq <= player.lastSeq) { error("DUPLICATE_SEQUENCE"); return; }
player.lastSeq = seq;
```

Sequence không cần liên tục: 5 rồi 8 vẫn được. Nó phải tăng trong toàn bộ session, không reset khi rời phòng hay reconnect. `HELLO` đầu tiên xử lý riêng; `PING` không làm tăng `lastSeq`.

TCP đã tự xử lý mất gói và truyền lại ở tầng giao vận. Ứng dụng thường không nhận các byte trùng chỉ vì TCP truyền lại. `seq` bảo vệ trước việc **ứng dụng gửi lại request**, client lỗi hoặc thử gửi lệnh cũ.

`lastSeq` được cập nhật trước khi kiểm tra mọi điều kiện gameplay. Một request mới nhưng không hợp lệ trong tình trạng hiện tại vẫn có thể tiêu thụ sequence. Client nên luôn tăng số khi gửi request tiếp theo.

**ACK không đồng nghĩa hành động thành công:** server hiện gửi ACK cả sau nhiều trường hợp ERROR. ACK có `seq` và `processingMicros` để phản hồi việc xử lý; phải đọc ERROR để biết hành động bị từ chối. Đợt này giữ nguyên quy ước đó để không tự đổi protocol.

## 7. Reconnect và disconnect được sửa như thế nào?

### 7.1. Phân biệt socket và session

Socket cũ mất đi không có nghĩa session phải mất ngay. `sessions` lưu người chơi theo `resumeToken`. Unity có thể mở socket mới rồi gửi:

```json
{"type":"HELLO","resumeToken":"token-da-nhan-truoc-do"}
```

Nếu session đã ngắt và chưa hết thời hạn 10 giây, server gắn socket mới vào cùng `PlayerState`: giữ id, phòng, HP và sequence. Server không hồi sinh người đã bị loại chỉ vì họ reconnect.

Thời hạn tính từ lúc **server phát hiện ngắt kết nối** và ghi `disconnectedAt`, không nhất thiết từ lúc Wi-Fi thực sự mất. Chưa có heartbeat timeout để phát hiện mọi trường hợp TCP bị treo im lặng.

### 7.2. Lỗi còn sót trong bản đầu vào

Bản cũ đã khóa lúc nhận token, nhưng disconnect làm hai bước tách rời: đánh dấu session ngắt dưới khóa player, sau đó mới lấy khóa room để xóa hướng đi hoặc rời phòng. Reconnect có thể chen giữa hai bước. Khi kết nối mới đã hoạt động, kết nối cũ vẫn có thể xóa hướng đi hoặc sửa phòng của nó.

### 7.3. Vùng xử lý chung cho vòng đời kết nối

`withSessionState()` lấy khóa theo một thứ tự:

```text
khóa PlayerState → khóa GameRoom (nếu có) → khóa ghi Connection (khi gửi)
```

`disconnect()` và `resume()` cùng đi qua vùng khóa này. Disconnect chỉ tác động nếu nó vẫn sở hữu session:

```java
if (player.connection != this) return;
```

`this` ở đây là đối tượng Connection đang chạy. Nếu session đã có Connection mới, dọn dẹp muộn của Connection cũ không được thay đổi nó.

Ở lobby hoặc phòng đã kết thúc, disconnect rời phòng; phòng rỗng được bỏ khỏi danh sách server. Trong trận, người chơi ở lại chờ reconnect, hướng đi về 0 và đối thủ nhận `PLAYER_DISCONNECTED`.

Khi resume, khóa phòng được giữ đến lúc gửi xong `WELCOME`, `ROOM_STATE` và snapshot khôi phục. Nhờ vậy game loop không gửi snapshot định kỳ trước `WELCOME`. Hai socket tranh cùng token chỉ có một socket thành công.

Token không tồn tại cũng trả `RESUME_FAILED`, thay vì âm thầm tạo người chơi mới dù client đang yêu cầu khôi phục người cũ. Muốn bắt đầu session mới, client gửi HELLO không có token.

Mốc thời gian được thống nhất: **nhỏ hơn 10.000 ms** mới được resume; từ 10.000 ms trở lên là hết hạn. Trước đây resume dùng `<=` trong khi tick dùng `>=`.

### 7.4. Nhận lại sequence và map

WELCOME khi resume có `lastSeq`. Ví dụ server đã xử lý 10, request tiếp theo phải lớn hơn 10. Unity hiện chưa khai báo/áp dụng trường này; T cần cập nhật trước khi khẳng định khôi phục sequence hoạt động đầy đủ ở client mới tạo lại.

Snapshot khôi phục dùng `forceBaseline = true` theo code đã có: có toàn bộ `obstacles`, `bushes`, vật phẩm và tọa độ. Điều này khôi phục map nhưng **cũng giữ hạn chế lộ tọa độ của snapshot baseline**. Muốn chỉ gửi lại map mà vẫn lọc vị trí phải thống nhất ý nghĩa với T; chưa thay ở đợt này.

### 7.5. Hết hạn ngắt kết nối

`GameRoom.update()` giờ xử lý tất cả người hết hạn trước khi xét thắng/thua hoặc nổ tự động. Nếu cả hai người cùng hết hạn, cả hai bị loại, không chọn nhầm một người đã hết hạn làm người thắng. Khi không có người sống, `winnerId` là chuỗi rỗng theo protocol hiện có.

## 8. Hiểu đa luồng và khóa trong code

Server dùng `Executors.newFixedThreadPool(64)` để xử lý Connection; không phải `newCachedThreadPool`. Mỗi Connection đang chạy chiếm một worker đọc socket. Các connection vượt số worker có thể phải chờ trong hàng đợi; đây không phải thiết kế hỗ trợ vô hạn người chơi.

Game loop có một luồng định thời riêng, lần lượt cập nhật các phòng. Trong cùng phòng, `join`, `leave`, `ready`, `action`, `update`, `snapshot` và `roomInfo` dùng `synchronized`.

```java
synchronized void update(long now, double dt) { ... }
```

Tương đương lấy khóa `this`, mà `this` ở đây là phòng hiện tại. Luồng MOVE và luồng tick muốn sửa cùng phòng phải chờ nhau. Hai phòng có khóa khác nhau, nhưng vẫn dùng chung một luồng tick của server.

`volatile` trên `connection`, `room`, `connected` giúp luồng khác thấy giá trị được công bố. Nó **không thay thế khóa** cho thao tác gồm nhiều bước. Ví dụ kiểm tra session rảnh rồi nhận session vẫn phải làm trong vùng khóa chung.

Để tránh deadlock, không thêm đoạn lấy khóa player bên trong phương thức GameRoom đang giữ khóa room. Việc đảo thứ tự room → player sẽ xung đột với player → room của vòng đời kết nối.

Giới hạn hiện tại: ghi socket vẫn đồng bộ, có thể diễn ra trong khi giữ khóa phòng/session. Client đọc chậm có thể làm chậm phòng và game loop. Test hiện chưa chứng minh chịu được client chậm hoặc tải lớn; hàng đợi gửi riêng là hướng cải tiến sau này.

## 9. Cải tiến đồng bộ: nói đúng tên, đo đúng thứ

| Nội dung | Baseline | Optimized hiện có |
| --- | --- | --- |
| Tần suất tính game | 20 Hz | 20 Hz |
| Snapshot định kỳ | 10 Hz | 10 Hz |
| Đá và bụi trong snapshot | Có mỗi lần | Bỏ ở snapshot thường; có trong GAME_START và snapshot khôi phục |
| Đối thủ không nhìn thấy | `visible=false` nhưng vẫn có tọa độ thật | `visible=false`, `x=0`, `z=0` trong snapshot thường |
| Orb/Core | Gửi tất cả | Gửi vật phẩm cách viewer tối đa 30 đơn vị |
| Players và các trường động | Gửi lại mỗi snapshot | Vẫn gửi lại mỗi snapshot |

Đây là **selective state synchronization**: tách dữ liệu tĩnh và lọc theo người nhận. Chưa phải delta encoding đầy đủ vì chưa so snapshot mới/cũ, chưa gửi riêng các trường thay đổi hay danh sách id bị xóa giữa hai tick.

`0,0` là tọa độ thay thế khi `visible=false`, không phải vị trí thật của đối thủ. Culling chỉ thay danh sách gửi đi, không xóa đồ vật trong thế giới server. Khi người chơi tiến lại gần, vật phẩm xuất hiện lại trong snapshot nếu nó còn tồn tại.

Trong `snapshot()`:

```java
boolean optimized = !forceBaseline && syncMode == SyncMode.OPTIMIZED;
```

`forceBaseline` ghi đè chế độ khi cần khôi phục đầy đủ. Chế độ mặc định của room lấy từ cấu hình server. Constructor nhận `SyncMode` giúp test tạo cả hai chế độ trong cùng lần chạy, không cần sửa biến toàn cục bằng reflection.

Đợt này còn sửa `List.copyOf(orbs)` và `List.copyOf(cores)`: snapshot giữ bản sao danh sách vật phẩm. Nếu tick sau xóa đồ vật, snapshot đã tạo không bị đổi theo trong lúc chuẩn bị ghi JSON.

Không nên tuyên bố đã chống mọi hack map: snapshot khôi phục vẫn đầy đủ; các sự kiện như `CORE_SPAWNED` và `PLAYER_EXPLODE` vẫn có tọa độ và broadcast. Phạm vi hiện có chỉ là giảm dữ liệu và giảm lộ vị trí trong **snapshot optimized thông thường**.

## 10. Thay đổi mới so với commit dc255a3

| Phần | Đã có trong commit đầu vào | Bổ sung/sửa trong đợt này |
| --- | --- | --- |
| Reconnect | Khóa lúc nhận token, WELCOME.lastSeq, full snapshot | Khóa chung toàn vòng đời, ngăn cleanup cũ tác động phiên mới, thứ tự bản tin khôi phục |
| Token và timeout | Có resume token, cửa sổ 10 giây | Token lạ bị từ chối; thống nhất điều kiện hết hạn |
| Room | Tạo/vào/rời phòng | `putIfAbsent` tránh ghi đè mã phòng trùng; join không vào phòng vừa bị xóa; dọn lobby rỗng khi disconnect |
| Kết thúc trận | Loại người hết hạn | Loại toàn bộ người hết hạn trước khi chọn thắng/thua |
| Core | Người gần nhất, khóa room | Test cạnh tranh và các điều kiện nhận Core |
| Đồng bộ | BASELINE/OPTIMIZED và culling | Chọn mode tường minh cho test; snapshot không dùng chung danh sách pickup có thể thay đổi |
| Spawn | Chọn vị trí bằng greedy farthest-point | Thử lại tối đa 32 điểm bắt đầu nếu bố trí bị kẹt; giữ khoảng cách tối thiểu 28 |
| Test | 27 test; vài test chỉ tự lặp điều kiện | 38 test, gồm 12 test TCP thật; kiểm tra cả hai mode |
| Hướng dẫn | Có mô tả chưa khớp code | Viết lại theo code, thêm cách học, lệnh chạy, bằng chứng và giới hạn |

Lỗi spawn xuất hiện khi chạy test: `Cannot separate 10 spawns on map meadow`. Một lựa chọn đầu tiên không tốt có thể làm thuật toán tham lam không xếp đủ người dù map còn cách bố trí hợp lệ. Thử lại giúp xử lý trường hợp đó mà không hạ khoảng cách; test kiểm tra 20 seed × 3 map. Đây là kiểm tra hữu hạn, không phải chứng minh toán học cho mọi seed.

## 11. Chạy trên máy bạn

Mở Terminal trong IntelliJ hoặc PowerShell ở thư mục dự án. Chỉ chạy `cd server` một lần nếu bạn đang đứng ở thư mục gốc:

```powershell
cd server
java -version
mvn -version
mvn clean test
```

`mvn` là Maven, công cụ tải thư viện, biên dịch và chạy test. Dự án biên dịch với Java release 17; máy kiểm tra dùng JDK 21.0.10 và Maven 3.9.16. Lần đầu Maven có thể cần mạng để tải thư viện.

Kết quả đúng ở bản hiện tại là `Tests run: 38, Failures: 0, Errors: 0, Skipped: 0` rồi `BUILD SUCCESS`.

Chạy từng nhóm để học:

```powershell
mvn test "-Dtest=ConnectionTest"
mvn test "-Dtest=ServerFixesTest"
mvn test "-Dtest=GameRoomTest"
```

Chạy server baseline cho Unity hiện tại và cho B đo:

```powershell
mvn compile exec:java "-Dexec.args=7777" "-Dsync.mode=baseline"
```

Terminal này sẽ tiếp tục chạy server. Giữ nó mở, dùng terminal khác hoặc Unity để kết nối. Dừng server bằng Ctrl+C.

Kiểm tra mode optimized bằng test, chưa cần Unity:

```powershell
mvn test "-Dsync.mode=optimized"
```

Sau khi T tích hợp và xác nhận protocol, khởi động lại server bằng:

```powershell
mvn compile exec:java "-Dexec.args=7777" "-Dsync.mode=optimized"
```

`-Dsync.mode=...` là **Java system property**, không phải biến môi trường. Nó được đọc khi lớp SyncMode khởi tạo; muốn đổi mode thì khởi động lại tiến trình. Xem log `[SyncMode] active = ...` để xác nhận. Giá trị không nhận ra hiện quay về baseline.

Nếu `mvn` không được nhận diện, kiểm tra PATH trong terminal và cấu hình Maven/JDK của IntelliJ. Nếu báo cổng đã được dùng, dừng server cũ hoặc đổi cả cổng server và client. Nếu lỗi khi chạy optimized trên Unity, xem phần bàn giao cho T trước; test Java pass không chứng minh Unity đã tương thích.

## 12. Đọc test như đọc một thí nghiệm

Mỗi test có ba phần: dựng tình huống → gọi code thật → so kết quả mong đợi. `assertEquals` yêu cầu bằng nhau, `assertTrue` yêu cầu đúng, `assertFalse` yêu cầu sai. Khi một điều kiện không đúng, Maven báo test thất bại để mình tìm nguyên nhân.

Ví dụ `simultaneousResumeAllowsExactlyOneTcpConnection` tạo hai socket, dùng `CountDownLatch` cho hai luồng cùng gửi HELLO, rồi đọc phản hồi: một WELCOME, một RESUME_FAILED. Nó đi qua chính `Connection.run()` và `handle()` của sản phẩm. Không chỉ tự viết lại biểu thức kiểm tra token trong test.

`equalDistanceCoreIsAwardedOnceAcrossConcurrentTicks` cho hai luồng cùng gọi `update()` và kiểm tra chỉ một người tăng Base Size. `Future.get()` có timeout giúp test thất bại nếu luồng bị kẹt, đồng thời đưa ngoại lệ từ luồng phụ về luồng test.

Các test timeout truyền mốc thời gian cho room, hoặc đặt thời điểm ngắt trong fixture; không cần ngồi chờ đủ 10 giây. Fixture là dữ liệu/đối tượng dựng riêng cho test, không phải dữ liệu giả để báo cáo benchmark.

## 13. Lộ trình tự học và thực hành

1. Đọc mục 2–4, tự kể lại đường đi của MOVE từ Unity đến snapshot.
2. Mở `PlayerState`, tìm trường ứng với tọa độ, HP, token và sequence.
3. Chạy `ConnectionTest`; đọc test reconnect và request trùng cùng code `handle()`.
4. Đọc `update()` và `pickupCores()`; giải thích vì sao chỉ một người nhận Core.
5. Chạy `ServerFixesTest`; xem test byte và so hai snapshot của cùng một room.
6. Mở Unity baseline với T, chơi thử hai client. Test Java là bước chuẩn bị, vẫn cần demo thực tế.
7. Thống nhất danh sách trong tài liệu bàn giao, sau đó B đo 2/4/8 client bằng cùng kịch bản.

Trong IntelliJ, bạn có thể đặt breakpoint ở `Connection.handle`, `resume`, `disconnect` hoặc `pickupCores`, rồi chạy test bằng Debug. Quan sát `player.lastSeq`, `player.connection`, `room.phase`, `cores.size()`. Dừng debugger lâu sẽ ảnh hưởng timeout theo đồng hồ thật; đây không phải lỗi game.

## 14. Câu hỏi bảo vệ và cách trả lời

**Em làm phần nào?** Em phụ trách server Java, kết nối/session, phòng và trạng thái trận; sửa vòng đời reconnect/disconnect, kiểm thử request trùng và tranh Core; duy trì hai chế độ đồng bộ để so sánh. Em phân biệt phần đã có của prototype với thay đổi của đợt phát triển trong bảng mục 10.

**Vì sao TCP rồi còn cần seq?** TCP đảm bảo luồng byte theo thứ tự. Seq ngăn ứng dụng thực hiện lại request mà client tự gửi trùng hoặc gửi cũ.

**Vì sao synchronized trên player chưa đủ?** Vì disconnect còn sửa dữ liệu room. Phải ngăn reconnect chen giữa bước đổi session và bước dọn trạng thái room, đồng thời thống nhất thứ tự lấy khóa.

**Ai nhặt Core khi hai người chạm cùng lúc?** Server chọn người gần nhất đủ điều kiện trong tick. Bằng khoảng cách thì thứ tự danh sách quyết định. Xóa Core và cộng Base Size trong vùng khóa room nên không nhặt hai lần.

**Cải tiến có phải delta sync không?** Hiện tại là selective sync: bỏ map tĩnh khỏi snapshot thường và lọc dữ liệu theo viewer. Chưa có delta từng trường dựa trên snapshot trước.

**Tiết kiệm bao nhiêu phần trăm?** Test cố định chứng minh snapshot optimized nhỏ hơn với cùng trạng thái. Chưa dùng kết quả một fixture để kết luận hiệu quả mạng thực tế; B cần đo lặp lại với 2/4/8 client và báo cấu hình cụ thể.

**processingMicros là gì?** Thời gian trong luồng xử lý request đến trước khi gửi ACK; có thể gồm thời gian chờ khóa và gửi phản hồi trước đó. Nó không phải RTT, cũng không phải số đo riêng cho serialize snapshot định kỳ.

**Đã hoàn thành toàn bộ game chưa?** Phần server và test trong phạm vi này đã được kiểm tra. Unity optimized, benchmark mạng và chế độ Hành trình còn phụ thuộc phối hợp với T/B; không trình bày chúng như tính năng đã tích hợp.

Hãy tập trả lời bằng cách chỉ vào hàm và chạy test minh họa, thay vì học thuộc nguyên đoạn. Tài liệu có AI hỗ trợ biên soạn; người nộp cần tự kiểm chứng và trình bày đúng phần mình hiểu.
