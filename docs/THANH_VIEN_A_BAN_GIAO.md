# Bàn giao phần A cho T và B

Cập nhật 24/10/2026, đối chiếu working tree hiện tại; các sửa đổi mới chưa commit/merge. Dùng client/server cùng phiên bản [PROTOCOL.md](PROTOCOL.md).

## Trạng thái

- Server đã hỗ trợ `QUICK` và `JOURNEY`.
- JOURNEY chạy `meadow → crossroads → crater`, giữ điểm/vòng thắng và phát `JOURNEY_COMPLETE`.
- Novelty selective sync hoạt động ở server; Unity đã dựng/cache geometry từ GAME_START và hiểu tọa độ ẩn.
- Reconnect, sequence trùng, tranh Core và Hành trình có test tự động.
- 107 test Java qua ở mỗi lần chạy baseline/optimized; xem [kết quả hiện tại](FINAL_VALIDATION.md), [mốc lịch sử 21/10](THANH_VIEN_A_KIEM_THU.md).
- Unity `6000.3.6f1`: 111 kiểm tra Play Mode và 12 kiểm tra C# qua; 20 clip (16 SFX, 4 nhạc). Benchmark 22/10 có 18 lượt loopback, chưa đo lại sau sửa 23–24/10.
- Ghép trận giữ ticket đến khi chốt phòng; hủy/tìm lại không dùng lượt ghép cũ. `roundParticipantIds` trong ROOM_STATE/GAME_START giúp người mới vào QUICK FINISHED thấy lobby chờ lượt sau; resume không phát GAME_OVER của vòng chưa chơi. SFX player ẩn không phát ở vị trí cũ/mặc định.
- Đã build Windows và server JAR; chưa playtest end-to-end 2–4 Windows client trên commit/tag phát hành.

## Protocol cần cả nhóm dùng

Đặc tả đầy đủ nằm ở [PROTOCOL.md](PROTOCOL.md). Các thay đổi chính:

| Message | Thay đổi |
| --- | --- |
| `CREATE_ROOM` | Nhận `mode=QUICK` hoặc `mode=JOURNEY`; thiếu mode mặc định QUICK |
| `ROOM_STATE.room` | Có mode/chapter/scores, roundIndex, roundWinnerId, roundEndReason, winnerIds, journeyComplete, journeyEndReason |
| `GAME_START` | Có mode/chapter/scores và geometry tĩnh |
| `STATE_UPDATE` | Có roomCode/roundIndex, mode/chapter/scores, roundTimeLeft, spectatorTargetId; optimized không lặp geometry |
| `GAME_OVER` | Có mode/chapter/scores của vòng vừa kết thúc |
| `JOURNEY_COMPLETE` | Sau crater hoặc kết thúc sớm, có winnerId/winnerIds/scores/reason |
| `WELCOME` | Có `hasRoom`, `syncMode`, `demoMap`; khi resume có thêm `lastSeq`. Unity hiện cấu hình ở F3 và đưa counter lên giá trị phù hợp |

`scores` là mảng để Unity `JsonUtility` đọc được:

```json
[{"playerId":"...","name":"Bom A","points":4,"roundWins":1}]
```

T cần mở Unity và kiểm tra trực quan:

1. Hai nút tạo phòng Đấu nhanh/Hành trình gửi đúng mode.
2. GAME_START dựng đủ đá/bụi ở optimized.
3. Sau chương chưa cuối và còn đủ người, bảng điểm đúng và READY chuyển map; thiếu người thì kết thúc sớm, không chờ READY 1/1.
4. Sau crater, hiện kết quả cuối và không còn nút READY.
5. Hai người chơi thật reconnect trong trận và giữa hai chương.
6. Lời dẫn, HUD và VFX chương không che gameplay; Đấu nhanh hòa khi hết 3 phút; camera/audio người chết tự theo người sống, không lộ đối thủ trong bụi ở optimized.

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

Fixture snapshot so hai nhánh trên cùng trạng thái và kiểm tra optimized nhỏ hơn; kích thước thay đổi khi DTO thay đổi. Benchmark ngày 22/10 giảm khoảng 62–78% byte nhận/client/s; xem [EXPERIMENTS_FINAL.md](EXPERIMENTS_FINAL.md) và fingerprint của bản đã đo. Không gán số đo cũ cho source hiện tại.

## Giới hạn cần ghi đúng

- Selective sync hiện không phải delta encoding từng trường.
- Reconnect optimized gửi lại geometry nhưng vẫn che player vô hình/cull pickup. Event nổ che tọa độ theo viewer và Core spawn xa không được gửi; vẫn không tuyên bố chống gian lận toàn diện vì chưa có xác thực/TLS và event không tọa độ vẫn để lộ một phần diễn biến.
- Socket được ghi bằng writer riêng cho từng connection; snapshot chờ được gộp về bản mới nhất. Client im lặng 6 giây, write treo 3 giây hoặc queue control đầy sẽ bị đóng riêng, không giữ khóa room/game loop.
- Người vào phòng bị chặn sau khi JOURNEY bắt đầu. Người mất mạng giữa chương có 10 giây reconnect; hết hạn bị loại/dọn khỏi roster. Còn dưới hai người tham gia thì Hành trình kết thúc sớm; đủ người mới READY tiếp.
- Unity source đã compile nhưng chưa có bằng chứng playtest cuối bằng 2–4 Windows client thật ở cả QUICK/JOURNEY.
- `scripts/network_smoke.py` đã đọc geometry từ GAME_START để dùng được với cả baseline và optimized; cần lưu lại kết quả chạy trên commit/tag phát hành.
- Benchmark 2/4/8 client và CSV thô đã có; B cần đo lại trên bản cuối, ưu tiên cố định map/seed và bổ sung LAN hai máy.
