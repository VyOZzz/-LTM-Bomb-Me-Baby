# Trạng thái và kiểm chứng — 23/10/2026

Tài liệu này mô tả working tree hiện tại, gồm sửa đổi sau commit `88bf7be`; chưa khẳng định chúng đã commit/merge hoặc phát hành lên remote. Luật và JSON đầy đủ nằm trong [PROTOCOL.md](PROTOCOL.md).

## Chức năng hiện tại

- Hai mode, ba map, phòng mã 2–10 người và tìm trận 2–4 người. Phòng đang PLAYING không nhận người mới; người trong phiên có thể reconnect trong 10 giây.
- Đấu nhanh tối đa 180 giây: người sống cuối cùng thắng; hết giờ còn nhiều người thì hòa. HUD nhận đồng hồ từ server, chơi lại đặt lại thời gian.
- Hành trình chơi `meadow → crossroads → crater`; mỗi chương tối đa 180 giây. Xếp hạng theo số mảnh → số lần hạ → người thắng chương cuối trong nhóm hòa → đồng thắng. Điểm chỉ để thống kê.
- Hành trình thiếu người kết thúc sớm sau thời gian giữ chỗ reconnect, giữ thành tích đã kiếm được, không cộng mảnh của chương chưa chơi. Người đã bị loại nhưng còn trong phòng vẫn được tính là người tham gia chương sau.
- Người chết ngừng gửi input gameplay, vẫn giữ heartbeat và nhận snapshot. Camera/audio tự theo người còn sống; optimized dùng góc nhìn mục tiêu đó và giữ luật bụi/khoảng cách. Chưa có chọn mục tiêu quan sát thủ công.
- Audio hiện có 20 clip: 16 SFX và 4 track nhạc. Nguồn và kiểm chứng riêng ở [AUDIO_CREDITS.md](AUDIO_CREDITS.md), [AUDIO_VALIDATION.md](AUDIO_VALIDATION.md).

## Các lỗi đã sửa và test bảo vệ

| Nhóm lỗi | Hành vi sau sửa / kiểm chứng |
| --- | --- |
| Snapshot vượt event, lobby ma, mất kết quả khi resume | Giữ thứ tự event, chỉ gộp snapshot liền nhau; roomCode/roundIndex chặn dữ liệu cũ; resume phát lại geometry và kết quả, kể cả kết thúc Hành trình sớm. Test C#, TCP và Unity. |
| Tạo/dọn phòng đồng thời | Đăng ký host và cleanup dưới room lock; test chạy cạnh tranh. |
| Tăng size kẹt đá, Boost tạo orb miễn phí | Giải chồng collider sau tăng size; chỉ thả orb khi size thực chi đủ 0,12. Test trên ba map. |
| Timeout giả khi writer vừa xong | Chụp writeStartedAt một lần trước kiểm tra; regression hai luồng. |
| UI xếp hạng/hòa | Xếp theo mảnh và lượt hạ; phân biệt thắng, hòa, đồng thắng; khôi phục popup từ room state. |
| Hành trình kẹt vì chỉ còn một người | Kết thúc sớm có reason INSUFFICIENT_PLAYERS; không mời READY tiếp. Test rời giữa trận/giữa chương, biên reconnect và phát lại kết quả qua TCP. |
| Server khựng cộng sai phá ấn | “Vào vòng” là bước vào vòng tròn quanh bệ Tim Lửa, không phải vào trận giữa chừng. Tick đầu nhận diện người trong vòng chưa được cộng thời gian trước đó; mỗi tick cộng tối đa 100 ms. Deadline chương vẫn theo thời gian thực. Test bước từ ngoài vào sau 4 giây khựng, người đã phá ấn và giới hạn 3/6 giây. |
| Phản khiên không reset phá ấn | Damage trực tiếp/phản khiên dùng chung hậu quả damage thực tế; reset đúng người và áp stagger, giữ miễn stagger liên tiếp. |
| Người chết tiếp tục gửi MOVE | Gọi BombGame.Update thật trong Unity: outbox không có MOVE/EXPLODE, vẫn có PING; thiếu local state cũng không gửi gameplay input. |
| Staging giữ asset đã xóa | Build/validation dùng chung sync_unity_project.ps1; mirror Assets/Packages/ProjectSettings dưới builds, kiểm tra đường dẫn/reparse point, giữ Library. Fixture kiểm tra file thừa, nguồn/cache và đường dẫn không hợp lệ. |
| Tài nguyên đồ họa và frame quá dài | Giải phóng vật liệu riêng theo vòng đời đối tượng; giới hạn frame khi đọc và tối đa 64 connection. Test tích hợp và JUnit. |

Các harness tái hiện lỗi cũ và báo cáo review trùng đã được bỏ sau khi gộp nội dung cần thiết vào đây. Test hồi quy được giữ trong `server/src/test`, `scripts/validation`, `scripts/test_unity_staging.ps1` và `Assets/Editor/BombValidation.cs`.

## Kết quả mới nhất

| Kiểm tra | Kết quả ngày 23/10 |
| --- | --- |
| Java/JUnit | 107 test qua ở mỗi lần chạy baseline/optimized, không failure/error. Cùng bộ test chạy hai cấu hình; một số fixture chủ động chọn sync mode để kiểm tra từng nhánh. |
| C# ngoài Unity | 12 kiểm tra hàng đợi/kết quả qua trên BombNetwork/BombProtocol thật. |
| Unity Play Mode | 81 kiểm tra headless qua: ba map × ba chu kỳ, actor/kết quả, import audio, vật liệu, input chết/quan sát, một Unity client + một peer TCP trên server JAR thật. |
| Staging | Fixture mirror, cache Library và giới hạn đường dẫn qua. |
| Build Windows | exit 0, BOMB_BUILD_PASS; `builds/windows/BombMeBaby.exe`. Gửi cả thư mục Windows. |
| Server JAR | `server/target/bomb-me-baby-server-1.0.0.jar`, kèm Gson, chạy bằng Java 17+. |

Bằng chứng: [Java](validation/review-fixes-2026-10-23/java-tests.json), [Unity](validation/review-fixes-2026-10-23/unity-checks.txt). Log đầy đủ ở `server/target/surefire-reports/`, `builds/unity-validation.log`, `builds/unity-build.log`; build/target không được Git theo dõi. Lượt Unity đầu gặp lỗi thứ tự khởi tạo transport trong fixture; đã sửa fixture và chạy lại đầy đủ thành công, không sửa production network để làm test qua.

## Chạy lại

Từ thư mục gốc trên Windows, cần JDK 17+, Maven 3.9+, .NET 10 SDK, Python 3.10+ và Unity 6000.3.6f1:

```powershell
cd server
mvn test "-Dsync.mode=baseline"
mvn test package "-Dsync.mode=optimized"
cd ..
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test_client.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test_unity_staging.ps1
python scripts/validate_unity.py
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build_client.ps1 -Isolated
```

Validation dùng bản sao `builds/unity-validation` và server TCP 17778; có `--java`, `--unity` để đổi đường dẫn. Build dùng `-Unity` để đổi Editor. Không chạy build và validation đồng thời vì dùng chung bản sao. Cả hai đồng bộ xóa file thừa chỉ trong ba thư mục nguồn của bản sao, giữ cache Library và không đóng Editor của project gốc.

## Bằng chứng lịch sử và giới hạn

- Benchmark ngày 22/10 vẫn là **18 lượt × 30 giây**, 0 lỗi ở đợt sau sửa watchdog, giảm khoảng **62–78% byte nhận/client/s**. Fingerprint và raw CSV trong [EXPERIMENTS_FINAL.md](EXPERIMENTS_FINAL.md) chỉ xác định bản đã đo ngày đó; chưa benchmark lại sau thay đổi gameplay/protocol 23/10. Không gán số liệu này cho source hiện tại.
- Các ảnh [meadow](validation/meadow.png), [crossroads](validation/crossroads.png), [crater](validation/crater.png) là render camera từ harness ngày 22/10, không có HUD, không phải trận người thật. Smoke Windows khởi động 8 giây cũng thuộc ngày 22/10; lượt 23/10 xác nhận build và kiểm thử Editor, chưa thay thế playtest Windows.
- Bộ test 21/10 được ghi riêng trong [THANH_VIEN_A_KIEM_THU.md](THANH_VIEN_A_KIEM_THU.md); không sửa số liệu lịch sử thành số test mới.
- Chưa có playtest cuối 2–4 Windows client/LAN, đo FPS/GC, kiểm tra máy sạch hay Compilatio. Không điền kết quả chủ quan vào [PLAYTEST_MAPS.md](PLAYTEST_MAPS.md). Các ca server khựng dùng thời gian đầu vào điều khiển được, không phải phép đo tần suất khựng thực tế.
