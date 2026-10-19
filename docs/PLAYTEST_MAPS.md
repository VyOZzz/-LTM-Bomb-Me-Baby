# Playtest map — Bomb Me Baby

Người phụ trách: Thành viên B. Map định nghĩa trong `server/src/main/java/bombmebaby/GameMap.java`. Mục tiêu theo `docs/TEAM_PLAN.md`: có ít nhất một map chính đã playtest, dễ chơi cho demo 2–4 client.

## Cách chạy

1. Từ thư mục `server/`: `mvn compile exec:java "-Dexec.args=7777"`. Dùng chế độ baseline (mặc định), vì Unity chưa hỗ trợ snapshot optimized.
2. Mở 2 client Unity (Editor + bản build, hoặc 2 bản build), tạo phòng, vào phòng, sẵn sàng.
3. Server chọn map ngẫu nhiên và không lặp lại map vừa chơi; chơi nhiều trận liên tiếp cho đến khi đủ cả 3 map.
4. Mỗi map chơi ít nhất 2 trận, ghi kết quả vào bảng dưới ngay sau trận.

## Kết quả

Ngày playtest: ____ · Người chơi: ____ · Số client: ____ · Commit: ____

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
| Ghi chú khác | | | |

## Đề xuất chỉnh sửa

| Map | Vấn đề | Thay đổi trong `GameMap.java` | Đã kiểm lại sau khi sửa? |
| --- | --- | --- | --- |
| | | | |

## Kết luận

Map chính cho demo: ____ · Lý do: ____
