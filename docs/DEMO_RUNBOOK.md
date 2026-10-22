# Runbook demo — Bomb Me Baby

Cập nhật 22/10/2026. Bản Windows và server JAR đã build; [FINAL_VALIDATION.md](FINAL_VALIDATION.md) ghi phạm vi kiểm tra tự động. Cần chạy checklist bằng người thật trên build này trước nộp; mốc 13/10 bên dưới chỉ là lịch sử.

## Chuẩn bị

1. Chạy `mvn test "-Dsync.mode=baseline"` và `mvn test package "-Dsync.mode=optimized"` trong `server/`; cả hai phải qua 98 test.
2. Chạy server bằng `mvn compile exec:java "-Dexec.args=7777" "-Dsync.mode=optimized"` (TCP `7777`).
3. Đặt `server.txt` cạnh file game `.exe`; dòng đầu là `HOST:PORT`.
4. Khởi động 2–4 bản game. Client thứ nhất tạo phòng; các client còn lại nhập mã phòng rồi vào phòng.
5. Chỉ bấm **Sẵn sàng** sau khi đủ client vì phòng đang `PLAYING` sẽ từ chối người join mới bằng `ROOM_UNAVAILABLE`.

Muốn demo lặp lại một map Đấu nhanh, thêm `"-Ddemo.map=meadow"` vào lệnh server. Nhấn **F3** trong game để xác nhận endpoint, `OPTIMIZED` và map demo. Bỏ tham số hoặc để `DEMO_MAP` rỗng để quay lại chọn map ngẫu nhiên.

## Kịch bản demo ngắn

- Xác nhận mọi client thấy cùng mã phòng, map và danh sách người chơi.
- Ready tất cả client và xác nhận phase chuyển `WAITING → PLAYING`.
- Di chuyển bằng WASD; kiểm tra camera bám local player và snapshot vẫn đến.
- Giữ/thả Space; kiểm tra VFX nổ và HUD hiển thị cooldown bom 2.5 giây.
- Dùng E và Shift; kiểm tra cooldown Shield/Boost.
- Loại một player, nhặt Core và chơi tới popup kết quả.
- Chọn chơi lại; kiểm tra tất cả cùng ready và vòng mới đổi map.
- Tạo phòng **Hành trình Ba Mảnh**, kiểm tra `meadow → crossroads → crater`, điểm giữa chương và màn kết cuối.
- Ngắt mạng một client dưới 10 giây rồi kết nối lại; kiểm tra giữ danh tính, phòng và geometry.
- Xác nhận lời dẫn, VFX, SFX và popup kết quả không che thao tác chính.
- Nghe thử nhạc menu và đủ ba track map; kiểm tra nhạc hạ khi nổ và thanh **Nhạc/Hiệu ứng** hoạt động.

## Trạng thái xác minh

### Source hiện tại — 22/10/2026

- Java: 98 test, 0 failure/error ở cả baseline và optimized; JAR kèm dependency chạy bằng Java 17+.
- Unity `6000.3.6f1`: Windows build thành công; 12 kiểm tra C# và 69 kiểm tra Play Mode tự động qua. Audio hiện có 19 SFX và 4 track nhạc; nhạc sảnh giữ nguyên.
- Chưa đánh dấu hoàn thành demo cuối cho đến khi chạy lại 2–4 Windows client trên cùng commit/tag phát hành.

### Mốc client thật — 13/10/2026

- `mvn test`: 12 test, 0 failure, 0 error tại commit ngày 13/10.
- `scripts/network_smoke.py`: PASS với 2 client giả lập, create/join/ready/snapshot/move/explode.
- Build Windows khởi động được, đọc `server.txt`, kết nối localhost và bắt đầu trận solo (trước 21/10; nay mỗi trận cần ít nhất 2 người).
- Hai Windows client thật cùng join phòng, cùng ready và nhận phase `PLAYING`; RTT, message count và byte count cập nhật trên cả hai.

Đây là kiểm thử chức năng, chưa thay thế số liệu thực nghiệm cho báo cáo. Benchmark phải được sinh từ script đo lặp lại, giữ nguyên map/kịch bản/cấu hình máy và lưu dữ liệu thô. Kết quả 13/10 không được dùng để khẳng định build 21/10 đã chơi trọn hai mode.
