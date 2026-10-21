# TCP JSON protocol

Cập nhật 20/10/2026. Mỗi message là một JSON object trên một dòng UTF-8. TCP được giữ xuyên suốt phiên chơi. Client phải gửi `HELLO` đầu tiên.

## Sequence và reconnect

Ngoại trừ `HELLO` và `PING`, mỗi request client phải có `seq` lớn hơn mọi `seq` server đã chấp nhận trong cùng session. Request trùng/cũ nhận `DUPLICATE_SEQUENCE` và không được thực hiện lại. Sequence không reset khi rời phòng hoặc reconnect.

```json
{"type":"HELLO","name":"Bom A","resumeToken":""}
```

Server trả token cho session mới:

```json
{"type":"WELCOME","playerId":"...","resumeToken":"..."}
```

Khi TCP đứt, client mở socket mới trong vòng 10 giây và gửi HELLO với token. WELCOME khôi phục có thêm `lastSeq`; request tiếp theo phải lớn hơn giá trị này. Token lạ, hết hạn, đang được socket khác giữ nhận `RESUME_FAILED`.

## Client → server

```json
{"type":"CREATE_ROOM","seq":1,"mode":"QUICK"}
{"type":"CREATE_ROOM","seq":1,"mode":"JOURNEY"}
{"type":"JOIN_ROOM","seq":2,"code":"ABC123"}
{"type":"READY","seq":3,"ready":true}
{"type":"MOVE","seq":4,"directionX":0.8,"directionZ":0.3}
{"type":"CHARGE_START","seq":5}
{"type":"EXPLODE","seq":6}
{"type":"USE_SHIELD","seq":7}
{"type":"USE_BOOST","seq":8}
{"type":"LEAVE_ROOM","seq":9}
{"type":"PING","clientTime":1720000000000}
```

`CREATE_ROOM.mode` không phân biệt hoa/thường. Thiếu hoặc rỗng mặc định thành `QUICK` để tương thích client cũ. Giá trị khác QUICK/JOURNEY nhận `INVALID_MODE`. Người vào phòng dùng mode do chủ phòng đã chọn.

Server trả `ACK` sau request không phải PING. ACK xác nhận server đã xử lý message đến điểm phản hồi, không tự khẳng định hành động gameplay thành công; client vẫn phải đọc `ERROR` nếu có.

## Trạng thái phòng và điểm

`ROOM_STATE.room` có:

- `code`, `phase`, `mapId`, `mapName`, `arenaSize`, `maxPlayers`, `players`;
- `mode`: `QUICK` hoặc `JOURNEY`;
- `chapterIndex`: `-1` cho QUICK; `0`, `1`, `2` cho ba chương JOURNEY;
- `scores`: mảng `{playerId,name,points,roundWins,eliminations}`; rỗng trong QUICK. `roundWins` là số mảnh Tim Lửa đã mang về, `eliminations` là số lần loại đối thủ;
- `journeyComplete`: hành trình đã kết thúc hay chưa.

Ba map JOURNEY luôn theo thứ tự `meadow → crossroads → crater`. Người thắng vòng nhận 3 điểm và tăng `roundWins` thêm 1. Mỗi lần trực tiếp loại đối thủ bằng vụ nổ hoặc sát thương phản khiên nhận 1 điểm. Rời trận/disconnect không cho điểm loại đối thủ, nhưng người sống cuối vẫn nhận điểm thắng vòng.

Sau ba vòng, server xếp theo số mảnh (`roundWins`), rồi `eliminations`, rồi ưu tiên người thắng vòng cuối nếu người đó còn nằm trong nhóm hòa. Nếu vẫn hòa, `winnerId` rỗng và `winnerIds` chứa các người đồng hạng. `points` vẫn được gửi để hiển thị nhưng không còn dùng để xếp hạng (thay đổi 20/10/2026).

### Mảnh Tim Lửa và bệ đá (chỉ JOURNEY, cập nhật 21/10/2026)

Mỗi chương có một mảnh và một bệ đá ở tâm map (`ShardObjective.java`):

1. Mảnh xuất hiện sau 10 giây ở một chỗ trống ngẫu nhiên, cách bệ ít nhất 45% nửa cạnh map.
2. Người chạm vào mảnh thì cầm mảnh, nhận 2 nấc Khóa Lửa và 2 giây bảo hộ. Người cầm luôn được mọi viewer nhìn thấy. Một hit chỉ phá một nấc nếu charge ≥ 35%, gây ≥ 10 damage thực tế, không bị Shield chặn và đã hết bảo hộ. Nấc khóa hồi đầy nếu 6 giây không nhận thêm hit hợp lệ.
3. Vỡ đủ hai nấc làm mảnh văng 2,5 unit theo hướng rời tâm nổ. Mảnh khóa nhặt 0,5 giây; carrier cũ bị khóa 1,3 giây. Carrier disconnect sẽ tự thả sau tối đa 2 giây.
4. Người cầm bước vào vòng bệ (bán kính 3) thì đặt mảnh và trở thành owner. Bệ được bảo hộ 5 giây. Sau đó mỗi đối thủ sống/kết nối trong vòng tự phá ấn với progress riêng trong 3 giây. Owner trong vòng chỉ giảm tốc còn 50%, không pause.
5. Damage thực tế reset riêng progress của challenger trúng đòn và stagger 0,45 giây; Shield chặn cả reset lẫn stagger. Rời vòng, chết hoặc disconnect cũng xóa progress riêng.
6. Challenger hoàn thành đầu tiên làm mảnh bật ra gần mép vòng, được ưu tiên nhặt 0,75 giây. Bệ quá nhiệt 4 giây và không nhận lại mảnh. Priority mất ngay nếu người đó chết/disconnect.
5. Hết 3 phút, chủ bệ (nếu chưa đặt thì người đang cầm) thắng chương với `reason="TIME"`. Chỉ còn một người sống thì người đó thắng với `reason="LAST_STANDING"`.

Mọi `STATE_UPDATE` của JOURNEY có thêm object công khai, không lọc theo viewer kể cả ở OPTIMIZED:

```json
"objective": {
  "state":"PLACED", "x":0, "z":0,
  "carrierId":"", "ownerId":"...", "challengerId":"...", "capture":0.4,
  "fireLock":2, "fireLockMax":2, "carrierProtection":0,
  "pedestalProtection":0, "pedestalOverheat":0,
  "pickupPriorityId":"...", "pickupPriority":0.5, "pickupUnlock":0,
  "sealBreakers":[
    {"playerId":"...","progress":0.4,"stagger":0,"resetFlash":0}
  ],
  "pedestalX":0, "pedestalZ":0, "pedestalRadius":3,
  "timeLeft":95.2, "spawnIn":0
}
```

`state` là `WAITING`, `GROUND`, `CARRIED` hoặc `PLACED`. `sealBreakers` là progress 0..1, thời gian stagger và cửa sổ feedback reset của từng challenger. `challengerId`/`capture` vẫn được giữ tương thích và trỏ tới người đang dẫn đầu. Các thời gian còn lại dùng giây. QUICK không có trường `objective`. Client so snapshot objective để hiện thông báo/VFX; mọi quyền nhặt, damage và progress vẫn do server quyết định.

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

Sau chương 0 hoặc 1, người chơi bấm READY lại để sang chương tiếp theo. Server reset HP/Size/cooldown và trạng thái vòng, giữ điểm hành trình. Sau chương 2, server gửi thêm:

```json
{
  "type":"JOURNEY_COMPLETE",
  "room":"ABC123",
  "mode":"JOURNEY",
  "chapterIndex":2,
  "winnerId":"...",
  "winnerIds":["..."],
  "scores":[{"playerId":"...","name":"Bom A","points":12,"roundWins":3}]
}
```

Không thể READY để tạo vòng thứ tư; server trả `JOURNEY_COMPLETE` dưới dạng mã ERROR.

## Các event khác

`ENERGY_PICKED`, `CORE_PICKED`, `CORE_SPAWNED`, `SHIELD_ACTIVATED`, `BOOST_ACTIVATED`, `PLAYER_EXPLODE`, `PLAYER_DAMAGED`, `DAMAGE_REFLECTED`, `PLAYER_ELIMINATED`, `PLAYER_DISCONNECTED`, `ACK`, `PONG`, `ERROR` giữ ý nghĩa cũ. `PLAYER_KNOCKED_BACK` là event mới mô tả impulse đã được server áp dụng.

Server là nguồn chính thức: client chỉ gửi hướng/hành động, không tự khai báo tọa độ, HP, sát thương, điểm hoặc người thắng. TCP message từ client giới hạn 2048 ký tự.

## Luồng input và snapshot

`STATE_UPDATE` là trạng thái chính thức. Client nội suy hình ảnh, dựng layout theo dữ liệu map, không tự tính hit hoặc tự cộng HP. Server giới hạn input hướng về vector dài tối đa 1, tự tích phân vị trí, kiểm tra ranh giới/vật cản và quyết định khả năng nhìn thấy đối thủ. Mỗi phòng chứa tối đa 10 người. TCP message từ client giới hạn 2048 ký tự.

## Luồng input → server → snapshot

1. `BombGame.Update()` đọc WASD/Space/E/Shift và chỉ tạo **ý định** hành động.
2. `BombNetwork.Send()` gắn `seq` và `clientTime`, serialize JSON rồi ghi một dòng UTF-8 lên TCP.
3. Một `BombServer.Connection` đọc dòng JSON trên thread của client, kiểm tra session và sequence, sau đó gọi `GameRoom.action()`.
4. Mọi thay đổi trong một phòng đi qua monitor `synchronized` của `GameRoom`; game loop 20 Hz cũng dùng cùng monitor khi gọi `update()`. Vì vậy input, tick và tranh nhặt Core không sửa đồng thời cùng một room.
5. Server tích phân movement, collision, pickup, cooldown và damage. Mỗi hai tick, server gửi `STATE_UPDATE` — baseline hiện tại là full snapshot 10 Hz.
6. Thread đọc của `BombNetwork` chỉ đưa JSON vào queue. `BombGame.Update()` trên Unity main thread lấy message, áp snapshot, nội suy transform và cập nhật HUD/VFX.

```text
Input Unity → BombNetwork (TCP JSON) → BombServer.Connection
            → GameRoom.action/update → STATE_UPDATE
            → BombNetwork inbox → BombGame.ApplySnapshot → scene/HUD/VFX
```

Không được cập nhật Unity `GameObject` từ network thread. Không được để client tự quyết định vị trí, HP, damage, pickup hoặc người thắng.
