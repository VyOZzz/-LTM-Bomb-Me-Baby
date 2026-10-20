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
- `scores`: mảng `{playerId,name,points,roundWins}`; rỗng trong QUICK;
- `journeyComplete`: hành trình đã kết thúc hay chưa.

Ba map JOURNEY luôn theo thứ tự `meadow → crossroads → crater`. Người thắng vòng nhận 3 điểm và tăng `roundWins` thêm 1. Mỗi lần trực tiếp loại đối thủ bằng vụ nổ hoặc sát thương phản khiên nhận 1 điểm. Rời trận/disconnect không cho điểm loại đối thủ, nhưng người sống cuối vẫn nhận điểm thắng vòng.

Sau ba vòng, server xếp theo tổng `points`, rồi `roundWins`, rồi ưu tiên người thắng vòng cuối nếu người đó còn nằm trong nhóm hòa. Nếu vẫn hòa, `winnerId` rỗng và `winnerIds` chứa các người đồng hạng.

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

`ENERGY_PICKED`, `CORE_PICKED`, `CORE_SPAWNED`, `SHIELD_ACTIVATED`, `BOOST_ACTIVATED`, `PLAYER_EXPLODE`, `PLAYER_DAMAGED`, `DAMAGE_REFLECTED`, `PLAYER_ELIMINATED`, `PLAYER_DISCONNECTED`, `ACK`, `PONG`, `ERROR` giữ ý nghĩa cũ.

Server là nguồn chính thức: client chỉ gửi hướng/hành động, không tự khai báo tọa độ, HP, sát thương, điểm hoặc người thắng. TCP message từ client giới hạn 2048 ký tự.
