# Bàn giao phần A cho T và B

Cập nhật 20/10/2026. Code đầu vào ban đầu `dc255a3`; các thay đổi mới nằm trong working tree và chưa được tài liệu này khẳng định là đã commit/push.

## Trạng thái

- Server đã hỗ trợ `QUICK` và `JOURNEY`.
- JOURNEY chạy `meadow → crossroads → crater`, giữ điểm/vòng thắng và phát `JOURNEY_COMPLETE`.
- Novelty selective sync hoạt động ở server; Unity đã dựng/cache geometry từ GAME_START và hiểu tọa độ ẩn.
- Reconnect, sequence trùng, tranh Core và Hành trình có test tự động.
- 45/45 test Java qua ở baseline và optimized; xem [kết quả](THANH_VIEN_A_KIEM_THU.md).
- Chưa chạy Unity Editor/build và chưa đo tải 2/4/8 client trong môi trường này.

## Protocol cần cả nhóm dùng

Đặc tả đầy đủ nằm ở [PROTOCOL.md](PROTOCOL.md). Các thay đổi chính:

| Message | Thay đổi |
| --- | --- |
| `CREATE_ROOM` | Nhận `mode=QUICK` hoặc `mode=JOURNEY`; thiếu mode mặc định QUICK |
| `ROOM_STATE.room` | Thêm `mode`, `chapterIndex`, `scores`, `journeyComplete` |
| `GAME_START` | Có mode/chapter/scores và geometry tĩnh |
| `STATE_UPDATE` | Có mode/chapter/scores; optimized không lặp geometry |
| `GAME_OVER` | Có mode/chapter/scores của vòng vừa kết thúc |
| `JOURNEY_COMPLETE` | Event mới sau crater, có `winnerId`, `winnerIds`, `scores` |
| `WELCOME` khi resume | Có `lastSeq`; Unity đã đưa counter lên giá trị phù hợp |

`scores` là mảng để Unity `JsonUtility` đọc được:

```json
[{"playerId":"...","name":"Bom A","points":4,"roundWins":1}]
```

T cần mở Unity và kiểm tra trực quan:

1. Hai nút tạo phòng Đấu nhanh/Hành trình gửi đúng mode.
2. GAME_START dựng đủ đá/bụi ở optimized.
3. Sau mỗi chương, bảng điểm đúng và READY chuyển map.
4. Sau crater, hiện kết quả cuối và không còn nút READY.
5. Hai người chơi thật reconnect trong trận và giữa hai chương.
6. Lời dẫn, HUD và VFX chương không che gameplay.

## B đo baseline và optimized

Từ thư mục `server/`:

```powershell
mvn compile exec:java "-Dexec.args=7777" "-Dsync.mode=baseline"
```

Đo xong cùng một kịch bản, dừng server rồi chạy:

```powershell
mvn compile exec:java "-Dexec.args=7777" "-Dsync.mode=optimized"
```

`GameMode` (QUICK/JOURNEY) là luật chơi; `SyncMode` (BASELINE/OPTIMIZED) là cách truyền snapshot. Khi so mạng phải giữ cùng game mode, số client, thời lượng, chuỗi input và map. JOURNEY có thứ tự map cố định nên thuận tiện chạy lại ba map; spawn và pickup vẫn ngẫu nhiên, vì vậy phải chạy nhiều lần hoặc kiểm soát fixture.

Các chỉ số nên lưu:

| Chỉ số | Cách hiểu |
| --- | --- |
| RTT | Từ gửi PING đến PONG tương ứng |
| Message/giây | Tổng dòng JSON và riêng STATE_UPDATE |
| Byte/giây | Byte UTF-8 thực đọc/ghi, ghi rõ có tính newline hay không |
| Kích thước snapshot | Tách GAME_START/recovery khỏi snapshot định kỳ |
| processingMicros | Thời gian xử lý request trước ACK, không phải RTT hoặc thời gian snapshot định kỳ |
| ERROR/reconnect | Lưu cả lần thất bại, không lọc mẫu xấu |

Phép đo fixture tự động hiện cho `baseline=1736`, `optimized=754` byte JSON trong một trạng thái cố định. Đây chỉ chứng minh optimized nhỏ hơn ở fixture đó, chưa phải kết quả băng thông trận thật.

## Giới hạn cần ghi đúng

- Selective sync hiện không phải delta encoding từng trường.
- Reconnect optimized gửi lại geometry nhưng vẫn che player vô hình/cull pickup. Event nổ che tọa độ theo viewer và Core spawn xa không được gửi; vẫn không tuyên bố chống gian lận toàn diện vì chưa có xác thực/TLS và event không tọa độ vẫn để lộ một phần diễn biến.
- Socket được ghi đồng bộ khi giữ khóa; client đọc chậm có thể ảnh hưởng phòng/game loop.
- Người vào phòng bị chặn sau khi JOURNEY bắt đầu. Người mất mạng giữa chương có 10 giây reconnect; hết hạn rời roster và phòng cần những người còn lại READY.
- Unity source đã cập nhật nhưng chưa được compile/render bằng Unity Editor trong phiên này.
- `scripts/network_smoke.py` đã đọc geometry từ GAME_START để dùng được với cả baseline và optimized; máy hiện tại chưa có lệnh Python chạy được nên chưa thực thi script này.
- Benchmark 2/4/8 client, CSV thô và kết luận phần trăm vẫn là việc B phải thực hiện.
