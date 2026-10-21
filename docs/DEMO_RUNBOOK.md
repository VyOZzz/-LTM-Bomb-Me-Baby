# Runbook demo — Bomb Me Baby

Cập nhật và kiểm thử trên Windows ngày 13/10/2026.

## Chuẩn bị

1. Chạy `mvn test` trong `server/`.
2. Chạy server bằng `mvn exec:java` (TCP `7777`).
3. Đặt `server.txt` cạnh file game `.exe`; dòng đầu là `HOST:PORT`.
4. Khởi động 2–4 bản game. Client thứ nhất tạo phòng; các client còn lại nhập mã phòng rồi vào phòng.
5. Chỉ bấm **Sẵn sàng** sau khi đủ client vì phòng đang `PLAYING` sẽ từ chối người join mới bằng `ROOM_UNAVAILABLE`.

## Kịch bản demo ngắn

- Xác nhận mọi client thấy cùng mã phòng, map và danh sách người chơi.
- Ready tất cả client và xác nhận phase chuyển `WAITING → PLAYING`.
- Di chuyển bằng WASD; kiểm tra camera bám local player và snapshot vẫn đến.
- Giữ/thả Space; kiểm tra VFX nổ và HUD hiển thị cooldown bom 2.5 giây.
- Dùng E và Shift; kiểm tra cooldown Shield/Boost.
- Loại một player, nhặt Core và chơi tới popup kết quả.
- Chọn chơi lại; kiểm tra tất cả cùng ready và vòng mới đổi map.

## Kết quả đã xác minh 13/10/2026

- `mvn test`: 12 test, 0 failure, 0 error.
- `scripts/network_smoke.py`: PASS với 2 client giả lập, create/join/ready/snapshot/move/explode.
- Build Windows khởi động được, đọc `server.txt`, kết nối localhost và bắt đầu trận solo.
- Hai Windows client thật cùng join phòng, cùng ready và nhận phase `PLAYING`; RTT, message count và byte count cập nhật trên cả hai.

Đây là kiểm thử chức năng, chưa phải số liệu baseline cho báo cáo. Bảng baseline phải được sinh từ script đo lặp lại, giữ nguyên map/kịch bản/cấu hình máy và lưu dữ liệu thô.
