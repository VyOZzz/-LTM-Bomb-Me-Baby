# Hoàn thiện và kiểm chứng — 22/10/2026

Phiên bản nguồn: working tree sau `d8084b5`, chưa commit. Không dùng hash commit gốc để khẳng định các sửa đổi đã nằm trên Git remote. Benchmark mới ghi thêm SHA-256 toàn bộ Java source và `pom.xml`.

## Lỗi đã sửa

| Mục | Kết quả |
| --- | --- |
| R1: snapshot vượt qua event | Giữ thứ tự TCP, chỉ gộp snapshot liền nhau; giữ geometry recovery; kiểm tra room/vòng ở Unity; overflow khởi động lại kết nối |
| R2: lobby ma sau resume | WELCOME báo có/không còn phòng; LEFT_ROOM luôn được xác nhận; client xóa state cũ |
| R3: create/cleanup race | Đăng ký phòng và thêm host dưới cùng room lock; cleanup kiểm tra/xóa dưới room lock |
| R4: tăng size kẹt đá | Giải quyết chồng collider sau nhặt orb/Core, tìm vị trí trống gần nhất nếu cụm đá gây dao động |
| R5: mất kết quả khi reconnect | Lưu người thắng/lý do trong room; resume phát lại snapshot đầy đủ và kết quả; giữ session FINISHED 10 giây |
| R6: luật UI sai | Xếp theo mảnh → số lần hạ → người thắng chương cuối → đồng thắng; điểm chỉ thống kê |
| R7: hòa bị báo thua | Phân biệt thắng, thua, hòa, đồng chiến thắng; không phát nhạc thua cho hòa/đồng thắng |
| R8: Boost tạo orb miễn phí | Chỉ thả orb nếu size thực sự tiêu hao đủ 0,12; vẫn được tăng tốc ở base size |
| R9: timeout giả khi đang ghi | Chụp `writeStartedAt` một lần trước kiểm tra; tránh hai lần đọc volatile thấy giá trị khác nhau và đóng nhầm kết nối khỏe |
| HUD Hành trình | GAME_START/snapshot cập nhật chương, map, điểm ngay, không chờ ROOM_STATE tiếp theo |
| Vật liệu đồ họa | Giải phóng vật liệu riêng khi actor/pickup/objective/map bị xóa; giải phóng fallback nền trước khi thay texture |
| Khung mạng quá dài | Giới hạn ngay khi đọc, không đợi cấp phát toàn bộ dòng; connection mới bị chặn khi đủ 64 |
| Trải nghiệm phòng | Nút tạo phòng riêng rõ hơn; mô tả vào bằng mã; tìm trận ghép hàng chờ riêng |

## Kết quả tự động

- **98 test Java** qua ở cả baseline và optimized, 0 failure/error. Bao gồm TCP thật, resume lobby/đang chơi/giữa chương/finale, giới hạn frame, khóa room, va chạm tăng size trên mọi đá của cả ba map, bot và cuộc đua timeout/ghi socket.
- **12 kiểm tra C#** qua trên chính `BombNetwork.cs` và `BombProtocol.cs`, chạy bằng `scripts/test_client.ps1`.
- **69 kiểm tra Unity Play Mode** qua trong Unity 6000.3.6f1: dựng ba map ba lượt bằng snapshot từ Java, cập nhật actor/kết quả, audio import, kiểm tra vật liệu và một Unity client kết nối server JAR thật cùng một peer TCP. Đây là kiểm thử tự động headless, không phải hai người chơi hoặc hai Unity client.
- Vật liệu được gắn nhãn do game tạo: **11 → 11 → 11** sau mỗi chu kỳ ba map trong harness. Phép đo có giữ map hiện tại và đối tượng chờ cuối frame; chứng minh không tăng trong kịch bản này, không phải tuyên bố toàn bộ bộ nhớ/FPS đã tối ưu.
- Windows player build thành công bằng `BombBuild.Windows`; output `builds/windows/BombMeBaby.exe`. Bộ nguồn chứa script build lại bằng một lệnh, không phụ thuộc file `.csproj` sinh tự động.
- Windows player đã chạy nền 8 giây, không tự thoát và không có các lỗi runtime được kiểm tra (NullReference/MissingMethod/TypeLoad/Missing audio/Fatal); tiến trình được harness dừng chủ động. Đây là smoke khởi động, không phải playtest.
- Chạy lại 69 kiểm tra Unity với thiết bị đồ họa cũng qua; ba ảnh render từ camera bằng snapshot Java đã được xem: [meadow](validation/meadow.png), [crossroads](validation/crossroads.png), [crater](validation/crater.png). Ảnh không gồm HUD và không được gọi là ảnh chụp một trận người thật. Marker kiểm thử lưu ở [unity-checks.txt](validation/unity-checks.txt).
- Benchmark sau sửa watchdog: **18 lượt × 30 giây, 0 lỗi**, giảm khoảng **62–78% byte nhận/client/s** ở optimized. Xem [EXPERIMENTS_FINAL.md](EXPERIMENTS_FINAL.md) cùng dữ liệu lỗi trước sửa được giữ nguyên.
- Server được đóng gói JAR kèm Gson: `server/target/bomb-me-baby-server-1.0.0.jar`.
- Giữ nguyên nhạc sảnh MP3; không thêm lại chế độ offline hoặc âm thanh mở trận.

Lệnh lặp lại (từ thư mục gốc):

```powershell
cd server
mvn test "-Dsync.mode=baseline"
mvn test package "-Dsync.mode=optimized"
cd ..
powershell -ExecutionPolicy Bypass -File scripts/test_client.ps1
python scripts/validate_unity.py
powershell -ExecutionPolicy Bypass -File scripts/build_client.ps1 -Isolated
```

`test_client.ps1` cần .NET 10 SDK. `validate_unity.py` cần Python 3.10+, Java 17+ và Unity đúng phiên bản; tùy máy truyền `--java` và `--unity`. Validation dùng bản sao trong `builds/unity-validation`, server cổng 17778 và không tác động phiên Editor đang mở.

## Phạm vi còn cần người thật

Lượt benchmark đầu `experiments/final-2026-10-22` dừng ở lượt thứ 6 khi có một CONNECTION_LOST. Regression test của watchdog thất bại trên code cũ và qua sau khi chụp biến volatile một lần. Đây là race đã được xác nhận; trace mạng cũ không đủ để chứng minh duy nhất nguyên nhân của lần disconnect đó. Dữ liệu cũ được giữ nguyên, phép đo sau sửa nằm ở thư mục `final-2026-10-22-v2` và được trình bày riêng.

Chưa có phép đo FPS/GC trong Windows player có đồ họa, nghe âm thanh bằng tai, playtest hai máy LAN, kiểm tra trên máy sạch hoặc Compilatio. Không điền số liệu trải nghiệm chủ quan vào `PLAYTEST_MAPS.md`. Đấu nhanh vẫn kết thúc theo người sống cuối cùng; không tự thêm đồng hồ/thu vòng làm đổi luật.

Trước nộp: bổ sung đúng thông tin nhóm, chơi checklist trên build, kiểm tra similarity theo yêu cầu của thầy và dùng đúng báo cáo đã nộp vào thư mục Report. Những bước này cần thông tin/kết quả thực tế từ nhóm.
