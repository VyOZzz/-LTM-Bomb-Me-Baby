# TCP JSON protocol

Cập nhật 24/10/2026. Mỗi message là một JSON object trên một dòng UTF-8. TCP được giữ xuyên suốt phiên chơi. Client phải gửi `HELLO` đầu tiên. Server đọc tối đa 2048 ký tự UTF-16 mỗi frame; vượt giới hạn sẽ đóng socket ngay cả khi chưa có newline. Tối đa 64 connection đồng thời.

## Ghép trận, người chờ lượt sau và audio (24/10)

- Yêu cầu tìm trận vẫn có hiệu lực trong khoảng đã được chọn nhưng chưa chốt phòng. `CANCEL_MATCH` có thể hủy ở khoảng này; lượt ghép cũ không được dùng yêu cầu tìm lại, kể cả cùng mode. Nếu server chốt phòng trước, hủy trả `NOT_SEARCHING`; người chơi dùng `LEAVE_ROOM` để rời phòng đã ghép. Không có kết quả vừa `CANCELLED` vừa được ghép từ cùng yêu cầu đó.
- `ROOM_STATE.room.roundParticipantIds` và `GAME_START.roundParticipantIds` chứa ID người tham gia round hiện tại/vừa kết thúc; mảng rỗng trước vòng đầu. Mỗi `GAME_START` thay bằng danh sách vòng mới. QUICK cho người mới vào phòng FINISHED để chờ READY; ID chưa nằm trong danh sách này thấy lobby chờ lượt sau, không thấy kết quả trận cũ. PLAYING vẫn chặn người mới; JOURNEY đã bắt đầu vẫn chặn người thay thế.
- Resume FINISHED của người chỉ vào chờ lượt sau vẫn nhận room/snapshot, nhưng không nhận `GAME_OVER` của vòng chưa chơi. Người đã chơi vẫn nhận lại kết quả như trước.
- Metadata khiên/boost/nhặt đồ/damage vẫn có thể gửi cho cả phòng. Client chỉ phát SFX gắn với player khi có avatar đang hiển thị; sau đó áp khoảng cách nghe. Không dùng `(0,0,0)` hay vị trí cũ của avatar ẩn. Tiếng nổ, Core rơi và objective có tọa độ riêng vẫn dùng chính sách event hiện có.

## Kết thúc sớm, đồng hồ và quan sát (23/10)

- QUICK có giới hạn 180 giây. Còn tối đa một người sống thì kết thúc như trước; hết giờ còn nhiều người sống thì `GAME_OVER.reason="TIME"`, `winnerId=""` (hòa). `STATE_UPDATE.roundTimeLeft` là số giây còn lại do server tính; mỗi vòng mới đặt lại 180 giây. JOURNEY tiếp tục dùng `objective.timeLeft`.
- JOURNEY đã bắt đầu không nhận người thay thế. Khi còn dưới hai người tham gia, trận kết thúc sớm. Người mất kết nối vẫn giữ chỗ trong 10 giây.
- `JOURNEY_COMPLETE.reason` và `ROOM_STATE.room.journeyEndReason` là `COMPLETED` khi trận kết thúc bình thường, `INSUFFICIENT_PLAYERS` khi kết thúc sớm. `journeyComplete=true` cho cả hai; không cho READY tiếp. Resume vẫn phát lại kết quả và lý do.
- `STATE_UPDATE.spectatorTargetId` rỗng khi viewer còn sống; khi đã chết, server chọn người sống đầu tiên trong danh sách phòng, ưu tiên còn kết nối. Tự chọn lại nếu người đó chết/rời phòng/mất kết nối. Camera và điểm nghe âm thanh theo mục tiêu này. Trong OPTIMIZED, tọa độ player, pickup, event nổ/knockback/Core dùng góc nhìn mục tiêu, giữ nguyên luật khoảng cách/bụi. BASELINE vẫn gửi dữ liệu đầy đủ như thiết kế đo so sánh trước đây.
- Client đã chết hoặc chưa có local state không gửi input gameplay; vẫn gửi PING và nhận snapshot. Hiện quan sát tự động, chưa có chọn mục tiêu thủ công.
- “Vào vòng” là nhân vật bước vào vòng tròn quanh bệ Tim Lửa, không phải người mới vào trận. Tiến trình cướp chỉ bắt đầu sau `INTERACT_START`, tối đa 100 ms mỗi tick như giới hạn catch-up của movement. Đồng hồ 30 giây và giới hạn chương vẫn theo thời gian thực, không bị kéo dài bởi server khựng. Damage không reset tương tác.

## Khôi phục và thứ tự message (22/10)

- `WELCOME.hasRoom`: true khi session còn phòng; false hoặc thiếu ở session mới. Client xóa phòng/actor cũ khi false. Resume không còn phòng còn nhận `LEFT_ROOM`; `LEAVE_ROOM` luôn được xác nhận bằng `LEFT_ROOM`, kể cả đã ở menu.
- Phòng đã bắt đầu giữ session mất kết nối tối đa 10 giây, kể cả ở FINISHED hoặc khi Hành trình hoàn tất. Lobby chưa bắt đầu bỏ người mất kết nối; hết hạn giữ chỗ thì server dọn người và phòng rỗng.
- Resume PLAYING: `WELCOME → ROOM_STATE → STATE_UPDATE` có geometry → `PLAYER_RECONNECTED → ACK`.
- Resume FINISHED của người đã tham gia round: `WELCOME → ROOM_STATE → STATE_UPDATE` có geometry → `GAME_OVER → JOURNEY_COMPLETE` (nếu đã hoàn tất) → `PLAYER_RECONNECTED → ACK`. Người mới vào chờ vòng sau bỏ qua `GAME_OVER`.
- `ROOM_STATE.room` có `roundIndex`, `roundWinnerId`, `roundEndReason`, `winnerIds`, `journeyComplete` để dựng lại kết quả độc lập với event đã bỏ lỡ.
- `GAME_START`, `GAME_OVER` có `roundIndex`; `STATE_UPDATE` có `roomCode` và `roundIndex` tăng từ 1 mỗi vòng trong phòng. Client chỉ nhận snapshot khớp phòng/vòng; snapshot PLAYING không được mở lại vòng đã FINISHED.
- Cả hai đầu chỉ gộp các snapshot liền nhau. Event là mốc thứ tự; snapshot recovery có geometry được giữ nếu snapshot sau không có geometry. Inbox client đầy 256 message sẽ reconnect để khôi phục thay vì âm thầm bỏ event; outbound server đầy 128 sẽ đóng connection chậm.
- `winnerIds` gồm những người đồng chiến thắng Hành trình. Danh sách rỗng và `winnerId` rỗng là hòa; người không thuộc nhóm đồng thắng vẫn thua.

Client và server phải được cập nhật cùng phiên bản này.

## Sequence và reconnect

Ngoại trừ `HELLO` và `PING`, mỗi request client phải có `seq` lớn hơn mọi `seq` server đã chấp nhận trong cùng session. Request trùng/cũ nhận `DUPLICATE_SEQUENCE` và không được thực hiện lại. Sequence không reset khi rời phòng hoặc reconnect.

```json
{"type":"HELLO","name":"Bom A","resumeToken":""}
```

Server trả token cho session mới:

```json
{"type":"WELCOME","playerId":"...","resumeToken":"...","syncMode":"OPTIMIZED","demoMap":"meadow"}
```

`syncMode` cho client/debug biết server đang chạy `BASELINE` hay `OPTIMIZED`. `demoMap` rỗng khi QUICK chọn map ngẫu nhiên; có giá trị khi server chạy với `-Ddemo.map=...` hoặc `DEMO_MAP`.

Khi TCP đứt, client mở socket mới trong vòng 10 giây và gửi HELLO với token. WELCOME khôi phục có thêm `lastSeq`; request tiếp theo phải lớn hơn giá trị này. Token lạ, hết hạn, đang được socket khác giữ nhận `RESUME_FAILED`. Unity hiện banner đếm ngược cửa sổ 10 giây và số lần thử kết nối. Server đóng connection không nhận message hợp lệ trong 6 giây; Unity chủ động đóng socket nếu không nhận dữ liệu server trong 7 giây. Hướng di chuyển cũ tự về 0 sau 0,5 giây nhưng không đổi trạng thái kết nối, nên đứng yên/ẩn bụi vẫn hợp lệ vì client tiếp tục gửi `MOVE(0,0)` và `PING`.

## Client → server

```json
{"type":"CREATE_ROOM","seq":1,"mode":"QUICK"}
{"type":"CREATE_ROOM","seq":1,"mode":"JOURNEY"}
{"type":"JOIN_ROOM","seq":2,"code":"ABC123"}
{"type":"READY","seq":3,"ready":true}
{"type":"MOVE","seq":4,"directionX":0.8,"directionZ":0.3}
{"type":"CHARGE_START","seq":5}
{"type":"EXPLODE","seq":6}
{"type":"DROP_SHARD","seq":7}
{"type":"USE_SHIELD","seq":8}
{"type":"USE_BOOST","seq":9}
{"type":"INTERACT_START","seq":10}
{"type":"INTERACT_STOP","seq":11}
{"type":"LEAVE_ROOM","seq":12}
{"type":"FIND_MATCH","seq":13,"mode":"JOURNEY"}
{"type":"CANCEL_MATCH","seq":14}
{"type":"PING","clientTime":1720000000000}
```

`CREATE_ROOM.mode` và `FIND_MATCH.mode` không phân biệt hoa/thường. Thiếu hoặc rỗng mặc định thành `QUICK` để tương thích client cũ. Giá trị khác QUICK/JOURNEY nhận `INVALID_MODE`. Mỗi chương JOURNEY có thời lượng cố định 360 giây trên server.

## Tìm trận (cập nhật 21/10/2026)

Mỗi phòng, kể cả phòng tạo bằng mã, cần ít nhất `minPlayers` = 2 người mới bắt đầu được. Một người bấm `READY` một mình vẫn ở `WAITING`.

`FIND_MATCH` đưa người chơi vào hàng chờ của mode đó; QUICK và JOURNEY không ghép lẫn. Mỗi khi hàng chờ thay đổi, mọi người trong hàng nhận:

```json
{"type":"MATCHMAKING","status":"SEARCHING","mode":"QUICK","waiting":2,"minPlayers":2,"maxPlayers":4}
```

Đủ 2 người cùng mode thì ghép ở lần tick kế tiếp (game loop 50 ms), tối đa 4 người mỗi nhóm đang chờ. Server tạo phòng mới, gửi `{"type":"MATCH_FOUND","room":"ABC123","mode":"QUICK"}` cho từng người, rồi `ROOM_STATE` và `GAME_START`, không cần `READY`. Phòng riêng không được đưa vào tìm trận: bạn bè phải nhập mã. Sau trận, mọi người bấm `READY` để chơi tiếp hoặc `LEAVE_ROOM` để tìm trận khác.

`CANCEL_MATCH` hủy yêu cầu đang chờ hoặc đang ghép nhưng chưa chốt phòng và nhận `{"type":"MATCHMAKING","status":"CANCELLED"}`; không còn yêu cầu có hiệu lực thì nhận `NOT_SEARCHING`. Đang chờ/đang ghép thì `CREATE_ROOM`, `JOIN_ROOM`, `FIND_MATCH` nhận `ALREADY_SEARCHING`; đang ở trong phòng thì `FIND_MATCH` nhận `ALREADY_IN_ROOM`. Mất kết nối khi đang chờ/đang ghép hủy ticket; reconnect không tự vào lại hàng. Nếu chốt phòng đã hoàn tất trước khi disconnect, áp quy tắc giữ chỗ của phòng đã bắt đầu.

Server trả `ACK` sau request không phải PING. ACK xác nhận server đã xử lý message đến điểm phản hồi, không tự khẳng định hành động gameplay thành công; client vẫn phải đọc `ERROR` nếu có.

## Trạng thái phòng và điểm

`ROOM_STATE.room` có:

- `code`, `phase`, `mapId`, `mapName`, `arenaSize`, `minPlayers`, `maxPlayers`, `players`;
- `mode`: `QUICK` hoặc `JOURNEY`;
- `roundIndex`, `roundParticipantIds`, `roundWinnerId`, `roundEndReason`: định danh vòng, người đã tham gia và kết quả; không coi mọi người đang có mặt trong phòng đều đã chơi vòng vừa kết thúc;
- `chapterIndex`: `-1` cho QUICK; `0`, `1`, `2` tương ứng Đồng cỏ, Ngã tư và Núi lửa trong JOURNEY;
- `scores`: mảng `{playerId,name,points,roundWins,eliminations}`; rỗng trong QUICK. `roundWins` là số mảnh Tim Lửa đã mang về, `eliminations` là số lần loại đối thủ;
- `journeyComplete`: hành trình đã kết thúc hay chưa.

JOURNEY gồm ba chương lần lượt trên `meadow`, `crossroads`, `crater`. Sau mỗi chương, mọi người sẵn sàng để chuyển sang map kế tiếp. Người chiếm được mảnh hoặc sống cuối nhận 3 điểm và tăng `roundWins` thêm 1. Mỗi lần trực tiếp loại đối thủ bằng vụ nổ hoặc sát thương phản khiên nhận 1 điểm.

### Mảnh Tim Lửa và bệ đá (chỉ JOURNEY, cập nhật 25/10/2026)

Mỗi chương có một mảnh và một bệ đá ở tâm map (`ShardObjective.java`):

1. Mảnh xuất hiện sau 10 giây ở một chỗ trống ngẫu nhiên, cách bệ ít nhất 45% nửa cạnh map.
2. Người chạm vào mảnh thì cầm mảnh. Người cầm luôn được mọi viewer nhìn thấy, không thể nạp/nổ bom và có thể gửi `DROP_SHARD` (phím Q). Damage thực tế bào Khóa Lửa 40 HP; về 0 thì mảnh rơi. Khóa hồi sau 3 giây không trúng đòn.
3. Người cầm bước vào vòng bệ (bán kính 3) thì đặt mảnh và trở thành owner. Server mở đếm ngược 30 giây; hết thời gian, owner thắng ngay với `reason="CLAIMED"`.
4. Một đối thủ sống, kết nối, đứng yên trong vòng gửi `INTERACT_START` (giữ F) để cướp trong 3 giây. Chỉ một người có thể cướp tại một thời điểm.
5. Di chuyển chủ động, thả F (`INTERACT_STOP`), dùng bom, khiên, boost, kỹ năng khác, chết hoặc mất kết nối sẽ hủy tiến trình. Damage, bom trúng và knockback không reset tiến trình nếu người cướp còn sống. Khiên bật trước đó tiếp tục hoạt động.
6. Hoàn thành tương tác chuyển mảnh trực tiếp sang trạng thái `CARRIED` của người cướp; bệ quá nhiệt 4 giây để họ có thể chạy khỏi bệ.
7. Hết thời lượng chương (mặc định 6 phút) mà chưa ai chiếm thành công thì chương đó hòa với `reason="TIME"`. Chỉ còn một người sống thì người đó thắng với `reason="LAST_STANDING"`.

Mọi `STATE_UPDATE` của JOURNEY có thêm object công khai, không lọc theo viewer kể cả ở OPTIMIZED:

```json
"objective": {
  "state":"PLACED", "x":0, "z":0,
  "carrierId":"", "ownerId":"...", "challengerId":"...", "capture":0.4,
  "claimTimeLeft":18.0, "claimDuration":30.0, "claimProgress":0.4,
  "fireLock":40, "fireLockMax":40,
  "pedestalProtection":0, "pedestalOverheat":0,
  "pickupPriorityId":"...", "pickupPriority":0.5, "pickupUnlock":0,
  "sealBreakers":[
    {"playerId":"...","progress":0.4,"stagger":0,"resetFlash":0}
  ],
  "pedestalX":0, "pedestalZ":0, "pedestalRadius":3,
  "timeLeft":95.2, "spawnIn":0
}
```

`state` là `WAITING`, `GROUND`, `CARRIED` hoặc `PLACED`. `sealBreakers` chứa tối đa một tương tác giữ F với progress 0..1. `claimTimeLeft` và `claimProgress` là đồng hồ chiếm bệ công khai cho mọi client. Các thời gian còn lại dùng giây. QUICK không có trường `objective`. Server quyết định vị trí, trạng thái sống, tiến trình, người mang/owner và thời điểm thắng.

Mọi mục tiêu nằm trong vụ nổ nhận horizontal impulse server-authoritative. Snapshot vị trí vẫn là nguồn chính thức; event sau chỉ dùng để chạy bounce Y/squash thuần hình ảnh:

```json
{"type":"PLAYER_KNOCKED_BACK","playerId":"...","directionX":0.8,"directionZ":0.6,"strength":12.4,"shielded":false}
```

Trong `OPTIMIZED`, event chỉ được gửi tới viewer nhìn thấy target. Shield giảm impulse còn 18%. `PlayerData.stagger` cho biết số giây khóa movement input còn lại; knockback vẫn được server tích phân và collision-check trong lúc stagger.

## Bắt đầu và đồng bộ trận

`GAME_START` có `room`, map, `mode`, `chapterIndex`, `scores`, `obstacles`, `bushes`. Geometry tĩnh luôn có trong GAME_START.

`STATE_UPDATE` có `tick`, `phase`, map, `mode`, `chapterIndex`, `scores`, `players`, `orbs`, `cores`. Cấu trúc phụ thuộc chế độ sync của server:

- BASELINE: mỗi snapshot tiếp tục có `obstacles`, `bushes`, toàn bộ pickup và tọa độ người chơi.
- OPTIMIZED: snapshot định kỳ bỏ `obstacles`/`bushes`, lọc pickup ngoài 30 đơn vị; player không được viewer nhìn thấy có `visible=false`, `x=0`, `z=0`.

Unity phải dựng/cache geometry từ GAME_START và không xóa map khi hai trường geometry vắng trong snapshot optimized. Khi reconnect giữa trận, server gửi lại geometry nhưng vẫn che player vô hình và cull pickup xa.

Trong optimized, `PLAYER_EXPLODE` có thêm `visible`; viewer không nhìn thấy nguồn nổ nhận `visible=false`, `x=0`, `z=0`. `CORE_SPAWNED` chỉ được gửi cho viewer cách Core tối đa 30 đơn vị. Baseline vẫn phát đầy đủ để làm mốc đo.

## Kết thúc vòng và hành trình

Mỗi vòng kết thúc bằng:

```json
{
  "type":"GAME_OVER",
  "winnerId":"...",
  "room":"ABC123",
  "reason":"TIME",
  "mode":"JOURNEY",
  "chapterIndex":0,
  "scores":[{"playerId":"...","name":"Bom A","points":4,"roundWins":1}]
}
```

Ngay sau khi trận JOURNEY kết thúc, server gửi thêm:

```json
{
  "type":"JOURNEY_COMPLETE",
  "reason":"COMPLETED",
  "room":"ABC123",
  "mode":"JOURNEY",
  "chapterIndex":0,
  "winnerId":"...",
  "winnerIds":["..."],
  "scores":[{"playerId":"...","name":"Bom A","points":3,"roundWins":1}]
}
```

Kết thúc sớm dùng cùng event trên với reason INSUFFICIENT_PLAYERS. Không thể READY khi `journeyComplete=true`; server trả `JOURNEY_COMPLETE` dưới dạng mã ERROR.

## Các event khác

`ENERGY_PICKED`, `CORE_PICKED`, `CORE_SPAWNED`, `SHIELD_ACTIVATED`, `BOOST_ACTIVATED`, `PLAYER_EXPLODE`, `PLAYER_DAMAGED`, `DAMAGE_REFLECTED`, `PLAYER_ELIMINATED`, `ACK`, `PONG`, `ERROR` giữ ý nghĩa cũ. `PLAYER_DISCONNECTED` có thêm `reconnectMs` và `reconnectDeadline`; `PLAYER_RECONNECTED` báo session đã quay lại. `PLAYER_KNOCKED_BACK` mô tả impulse đã được server áp dụng.

Server là nguồn chính thức: client chỉ gửi hướng/hành động, không tự khai báo tọa độ, HP, sát thương, điểm hoặc người thắng. TCP message từ client giới hạn 2048 ký tự.

## Luồng input và snapshot

`STATE_UPDATE` là trạng thái chính thức. Client dự đoán hình ảnh ngắn hạn cho người chơi local, ngoại suy tối đa 120 ms cho người chơi khác và hiệu chỉnh mềm về snapshot; client không tự tính hit hoặc tự cộng HP. Server giới hạn input hướng về vector dài tối đa 1, tự tích phân vị trí, kiểm tra ranh giới/vật cản và quyết định khả năng nhìn thấy đối thủ. Mỗi phòng chứa tối đa 10 người. TCP message từ client giới hạn 2048 ký tự.

## Luồng input → server → snapshot

1. `BombGame.Update()` đọc WASD/Space/Q/E/Shift và chỉ tạo **ý định** hành động.
2. `BombNetwork.Send()` gắn `seq` và `clientTime`, serialize JSON rồi đưa vào hàng đợi giới hạn; thread ghi riêng phát một dòng UTF-8 lên TCP để Unity main thread không bị chặn.
3. Một `BombServer.Connection` đọc dòng JSON trên thread của client, kiểm tra session và sequence, sau đó gọi `GameRoom.action()`.
4. Mọi thay đổi trong một phòng đi qua monitor `synchronized` của `GameRoom`; game loop 20 Hz cũng dùng cùng monitor khi gọi `update()`. Vì vậy input, tick và tranh nhặt Core không sửa đồng thời cùng một room.
5. Server tích phân movement, collision, pickup, cooldown và damage. Mỗi hai tick tạo `STATE_UPDATE`; hàng đợi ghi chỉ thay snapshot liền trước bằng bản mới hơn nếu không làm mất geometry recovery. Client đọc chậm không chặn khóa room/game loop; write quá 3 giây hoặc queue đầy sẽ đóng riêng connection đó. Watchdog chụp timestamp ghi một lần trước khi kiểm tra để tránh race với writer vừa hoàn tất.
6. Thread đọc của `BombNetwork` đưa tất cả message vào cùng queue có thứ tự, gộp snapshot liền nhau theo cùng quy tắc geometry. Khi đầy, reconnect để lấy state đầy đủ. `BombGame.Update()` kiểm tra phòng/vòng, áp snapshot, làm mượt transform và cập nhật HUD/VFX.

```text
Input Unity → BombNetwork (TCP JSON) → BombServer.Connection
            → GameRoom.action/update → STATE_UPDATE
            → BombNetwork inbox → BombGame.ApplySnapshot → scene/HUD/VFX
```

Không được cập nhật Unity `GameObject` từ network thread. Không được để client tự quyết định vị trí, HP, damage, pickup hoặc người thắng.
