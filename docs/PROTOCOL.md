# TCP JSON protocol

Mỗi message là một JSON object trên một dòng UTF-8. Kết nối TCP được giữ xuyên suốt trận. Client gửi `HELLO` đầu tiên. Ngoại trừ `PING`, mỗi action tiếp theo phải có `seq` tăng dần trong cùng session. Server trả `ACK` kèm `seq` và `processingMicros`; message trùng/cũ bị từ chối.

## Client → server

```json
{"type":"HELLO","name":"Bom A","resumeToken":""}
{"type":"CREATE_ROOM","seq":2}
{"type":"JOIN_ROOM","seq":3,"code":"ABC123"}
{"type":"READY","seq":4,"ready":true}
{"type":"MOVE","seq":5,"directionX":0.8,"directionZ":0.3}
{"type":"CHARGE_START","seq":6}
{"type":"EXPLODE","seq":7}
{"type":"USE_SHIELD","seq":8}
{"type":"USE_BOOST","seq":9}
{"type":"LEAVE_ROOM","seq":10}
{"type":"PING","clientTime":1720000000000}
```

Client giữ `resumeToken` từ `WELCOME` trong bộ nhớ. Khi TCP đứt, client kết nối lại và gửi `HELLO` với token trong vòng 10 giây. Nếu kết nối quá hạn, trận đã loại player.

## Server → client

- `WELCOME`: `playerId`, `resumeToken`.
- `ROOM_STATE`: `room` gồm `code`, `phase`, `mapId`, `mapName`, `arenaSize` (nửa chiều rộng map), `maxPlayers` và `players` (id, name, ready, connected).
- `GAME_START` kèm thông tin map; `STATE_UPDATE` gồm `tick`, `phase`, `mapId`, `mapName`, `arenaSize`, `players`, `orbs`, `cores`, `obstacles`, `bushes`.
- `ENERGY_PICKED`, `CORE_PICKED`, `CORE_SPAWNED`, `SHIELD_ACTIVATED`, `BOOST_ACTIVATED`, `PLAYER_EXPLODE`, `PLAYER_DAMAGED`, `DAMAGE_REFLECTED`, `PLAYER_ELIMINATED`, `PLAYER_DISCONNECTED`, `GAME_OVER`.
- `ACK`: `seq`, `processingMicros`; `PONG`: `clientTime`, `serverTime`.
- `ERROR`: `code` như `INVALID_MESSAGE`, `DUPLICATE_SEQUENCE`, `ROOM_UNAVAILABLE`.

`STATE_UPDATE` là trạng thái chính thức. Client nội suy hình ảnh, dựng layout theo dữ liệu map, không tự tính hit hoặc tự cộng HP. Server giới hạn input hướng về vector dài tối đa 1, tự tích phân vị trí, kiểm tra ranh giới/vật cản và quyết định khả năng nhìn thấy đối thủ. Mỗi phòng chứa tối đa 10 người. TCP message từ client giới hạn 2048 ký tự.
