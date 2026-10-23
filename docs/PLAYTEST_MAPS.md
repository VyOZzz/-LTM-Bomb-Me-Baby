# Playtest map — Bomb Me Baby

Kiểm tra tự động ngày 24/10 đã qua 111 kiểm tra Unity, gồm ba map × ba chu kỳ, snapshot Java → geometry/actor → kết quả → rời phòng, không tăng số vật liệu qua chu kỳ; input khi chết, quan sát, kết thúc Hành trình sớm, người mới vào chờ lượt sau và audio đối thủ ẩn. Một Unity client cùng một peer TCP đã tạo/join/ready/kết thúc trên server thật. Chi tiết ở [FINAL_VALIDATION.md](FINAL_VALIDATION.md). Đây **không phải playtest người thật**, nên bảng cảm nhận/thời lượng trận bên dưới vẫn để trống.

Người phụ trách: Thành viên B. Map định nghĩa trong `server/src/main/java/bombmebaby/GameMap.java`. Mục tiêu theo `docs/TEAM_PLAN.md`: kiểm tra đủ ba map bằng 2–4 client thật và chọn một map/kịch bản ổn định cho demo.

## Cách chạy

1. Chạy `mvn test "-Dsync.mode=optimized"`, sau đó từ `server/` chạy `mvn compile exec:java "-Dexec.args=7777" "-Dsync.mode=optimized"`. Unity hiện đã hỗ trợ geometry từ `GAME_START` và snapshot optimized.
2. Mở 2 client Unity (Editor + bản build, hoặc 2 bản build), tạo phòng, vào phòng, sẵn sàng.
3. Chơi Đấu nhanh nhiều vòng để kiểm tra đổi map; chơi một lượt Hành trình để kiểm tra đúng thứ tự `meadow → crossroads → crater`.
4. Mỗi map chơi ít nhất 2 trận. Ghi commit, build, sync mode, số client và kết quả ngay sau trận; không điền theo trí nhớ.
5. Sau khi sửa map, chơi lại đúng map/kịch bản đã lỗi và ghi kết quả hồi quy.
6. Kiểm tra Đấu nhanh hòa khi hết 3 phút còn nhiều người; Hành trình hai người kết thúc sớm khi một người rời/hết hạn reconnect. Với ít nhất 3 người, kiểm tra camera/audio người bị loại tự theo người sống và giữ luật bụi.

## Kết quả

Ngày playtest: ____ · Người chơi: ____ · Số client: ____ · Commit/build: ____ · Mạng: localhost/LAN/Internet ____ · Sync mode: ____

| Tiêu chí | `meadow` (48) | `crater` (52) | `crossroads` (50) |
| --- | --- | --- | --- |
| Số trận đã chơi | | | |
| Thời lượng trận trung bình | | | |
| Có chỗ bị kẹt giữa đá? (ghi tọa độ gần đúng) | | | |
| Bụi quá dày / quá thưa? Trốn trong bụi có hợp lý? | | | |
| Điểm xuất phát công bằng? Có ai bị dồn góc? | | | |
| Orb/Core phân bố đều? | | | |
| Dễ hiểu cho người mới (1–5) | | | |
| Vui (1–5) | | | |
| HUD/VFX/nhạc/SFX có giúp đọc tình huống? | | | |
| Có lệch trạng thái giữa các client? | | | |
| Ghi chú khác | | | |

## Đề xuất chỉnh sửa

| Map | Vấn đề | Thay đổi trong `GameMap.java` | Đã kiểm lại sau khi sửa? |
| --- | --- | --- | --- |
| | | | |

## Kết luận

Map chính cho demo: ____ · Lý do: ____
