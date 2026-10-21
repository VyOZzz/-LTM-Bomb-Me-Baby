# Bàn giao phần A cho T và B

Cập nhật 21/10/2026, đối chiếu với source đã commit trên nhánh hiện tại và lượt kiểm tra sạch cùng ngày.

## Trạng thái

- Server đã hỗ trợ `QUICK` và `JOURNEY`.
- JOURNEY chạy `meadow → crossroads → crater`, giữ điểm/vòng thắng và phát `JOURNEY_COMPLETE`.
- Novelty selective sync hoạt động ở server; Unity đã dựng/cache geometry từ GAME_START và hiểu tọa độ ẩn.
- Reconnect, sequence trùng, tranh Core và Hành trình có test tự động.
- 68/68 test Java qua ở baseline và optimized; xem [kết quả](THANH_VIEN_A_KIEM_THU.md).
- Unity `6000.3.6f1` đã batch compile thành công; 11 SFX import được. Đã có 18 lượt benchmark loopback 2/4/8 client giả lập trong `experiments/`.
- Chưa phát hành lại Windows build và chưa chơi end-to-end 2–4 client thật trên cùng commit cuối.

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
| `WELCOME` | Có `syncMode`, `demoMap`; khi resume có thêm `lastSeq`. Unity hiện cấu hình ở F3 và đưa counter lên giá trị phù hợp |

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

Phép đo fixture tự động hiện cho `baseline=1799`, `optimized=821` byte JSON trong một trạng thái cố định. Benchmark trận giả lập cho thấy giảm khoảng 66–79% byte nhận/client/s tùy số client; xem `EXPERIMENTS.md` để biết điều kiện và giới hạn.

## Giới hạn cần ghi đúng

- Selective sync hiện không phải delta encoding từng trường.
- Reconnect optimized gửi lại geometry nhưng vẫn che player vô hình/cull pickup. Event nổ che tọa độ theo viewer và Core spawn xa không được gửi; vẫn không tuyên bố chống gian lận toàn diện vì chưa có xác thực/TLS và event không tọa độ vẫn để lộ một phần diễn biến.
- Socket được ghi đồng bộ khi giữ khóa; client đọc chậm có thể ảnh hưởng phòng/game loop.
- Người vào phòng bị chặn sau khi JOURNEY bắt đầu. Người mất mạng giữa chương có 10 giây reconnect; hết hạn rời roster và phòng cần những người còn lại READY.
- Unity source đã compile nhưng chưa có bằng chứng playtest cuối bằng 2–4 Windows client thật ở cả QUICK/JOURNEY.
- `scripts/network_smoke.py` đã đọc geometry từ GAME_START để dùng được với cả baseline và optimized; cần lưu lại kết quả chạy trên commit/tag phát hành.
- Benchmark 2/4/8 client và CSV thô đã có; B cần đo lại trên bản cuối, ưu tiên cố định map/seed và bổ sung LAN hai máy.
