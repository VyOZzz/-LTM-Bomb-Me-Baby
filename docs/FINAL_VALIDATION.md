# Trạng thái và kiểm chứng — cập nhật 24/10/2026

Các sửa lỗi bổ sung 24/10 nằm trong working tree trên HEAD `e1382fd`, chưa tạo commit hoặc phát hành remote. Luật và JSON đầy đủ nằm trong [PROTOCOL.md](PROTOCOL.md).

## Sửa lỗi sau rà soát 24/10

Đã sửa ba lỗi runtime và lỗi link Report phát hiện trong lượt rà soát. Java hiện có **107 test qua ở mỗi mode baseline/optimized**, gồm **36 test Connection**; C# ngoài Unity có **12 kiểm tra qua**. Bộ Unity mở rộng lên **111 kiểm tra**, gồm packet người mới vào chờ từ server thật và 23 kiểm tra audio chưa thấy/ẩn/gần/xa/local. Các test mới được giữ trong bộ hồi quy chính.

| Nhóm lỗi | Cách sửa | Kiểm chứng |
| --- | --- | --- |
| Hủy/tìm lại bị lượt ghép cũ kéo vào sai mode | Ticket có generation, vẫn được giữ khi đã chọn nhưng chưa chốt phòng. Chốt ghép khóa tối đa bốn player theo ID rồi kiểm tra ticket còn hiệu lực. Hủy/disconnect dùng khóa player; thiếu người thì trả ticket hợp lệ về đầu hàng, không tạo phòng tạm. | Test TCP hủy/tìm khác mode, tìm lại cùng mode, hủy cả yêu cầu mới, disconnect, FIFO khi thiếu người; 12 lần tranh giữa hủy và chốt ghép trong test đồng thời, không có cả CANCELLED lẫn MATCH_FOUND cho cùng yêu cầu. |
| Người mới vào QUICK FINISHED bị báo thua | `roundParticipantIds` phân biệt người chơi vòng vừa qua với người chỉ vào chờ vòng sau. Unity hiện lobby/READY cho người mới; resume không phát GAME_OVER của vòng chưa chơi. | TCP join → resume → READY sang vòng mới; Unity đọc packet thật, kiểm tra lobby, bỏ âm thua cũ và vẫn nhận kết quả vòng đã tham gia. |
| Audio đối thủ ẩn phát tại gốc bản đồ/vị trí cũ | SFX gắn player chỉ phát khi avatar đang hiển thị, sau đó áp khoảng cách nghe. Không dùng tọa độ mặc định; giữ audio local và event có tọa độ riêng. | Bảy loại event × ba trường hợp chưa thấy/đã ẩn/ở gần, thêm âm xa và local trong Unity. Không thay file audio hoặc mức mix. |
| Link Report thiếu file đích | Link còn thiếu trong Report được trỏ tới file tương ứng trong Source/docs của gói. Script kiểm tra lại đích local trước khi tạo manifest/ZIP. | Kiểm tra layout gói bàn giao; không cần copy trùng toàn bộ tài liệu sang Report. |

**Bàn giao bằng Git:** Cần commit các thay đổi hiện tại để clone có đầy đủ bản đã kiểm thử; không gán kết quả working tree này cho HEAD cũ.

Trong lúc chạy lại, test cũ `ShardCarrierActionTest` có lần thất bại do fixture gán `charging=true` nhưng để `chargeSince=0`: bom tự nổ trước lúc nhặt mảnh và có thể đánh trúng người khác tùy vị trí ngẫu nhiên. Fixture đã dùng action CHARGE_START thật, seed cố định và bỏ orb không liên quan; không đổi luật chiến đấu để làm test qua. Log/tổng Java ở `builds/audit-2026-10-24/public-*.json`, log Unity ở `builds/unity-validation.log`, packet TCP người mới ở `server/target/newcomer-fixtures.jsonl` (Git bỏ qua). Chưa thực hiện playtest người thật/LAN, đo FPS/GC hoặc độ trễ thực tế; không suy ra “không còn lỗi” từ kết quả tự động.

Harness Unity cũng được sửa để mỗi bước coroutine chờ một frame Play Mode thật: callback `EditorApplication.update` có thể chạy nhiều lần trong một frame, khiến kiểm tra vật liệu trước đây đôi khi đếm trước khi `Destroy` được xử lý. Sau sửa, ba chu kỳ đều còn đúng hai vật liệu ground của map đang giữ ở menu; 111 kiểm tra qua trên JAR mới. Không nới điều kiện kiểm tra vật liệu hoặc sửa production renderer để bỏ qua lỗi test.

Build 24/10: `builds/windows/BombMeBaby.exe` tạo thành công (`BOMB_BUILD_PASS`, exit 0) bằng Unity 6000.3.6f1; server tại `server/target/bomb-me-baby-server-1.0.0.jar`. Cần gửi cả thư mục Windows. Lệnh đóng gói bản này: `python scripts/package_project.py --name BombMeBaby-Review-2026-10-24-Public`; script kiểm tra link Report, ghi manifest SHA-256 và kiểm tra CRC ZIP trước khi báo thành công. Build/ZIP là bản working tree để review, chưa phải commit/tag phát hành.

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

## Kết quả kiểm chứng 23/10

| Kiểm tra | Kết quả ngày 23/10 |
| --- | --- |
| Java/JUnit | 101 test trong source bàn giao qua ở mỗi lần chạy baseline/optimized, không failure/error. Cùng bộ test chạy hai cấu hình; một số fixture chủ động chọn sync mode để kiểm tra từng nhánh. |
| C# ngoài Unity | 12 kiểm tra hàng đợi/kết quả qua trên BombNetwork/BombProtocol thật. |
| Unity Play Mode | 81 kiểm tra headless qua: ba map × ba chu kỳ, actor/kết quả, import audio, vật liệu, input chết/quan sát, một Unity client + một peer TCP trên server JAR thật. |
| Staging | Fixture mirror, cache Library và giới hạn đường dẫn qua. |
| Build Windows | exit 0, BOMB_BUILD_PASS; `builds/windows/BombMeBaby.exe`. Gửi cả thư mục Windows. |
| Server JAR | `server/target/bomb-me-baby-server-1.0.0.jar`, kèm Gson, chạy bằng Java 17+. |

Bằng chứng Java lịch sử dưới đây chỉ trích các suite thuộc source bàn giao; kết quả từng suite giữ nguyên từ lượt chạy 23/10. Bằng chứng: [Java](validation/review-fixes-2026-10-23/java-tests.json), [Unity](validation/review-fixes-2026-10-23/unity-checks.txt). Log đầy đủ ở `server/target/surefire-reports/`, `builds/unity-validation.log`, `builds/unity-build.log`; build/target không được Git theo dõi. Lượt Unity đầu gặp lỗi thứ tự khởi tạo transport trong fixture; đã sửa fixture và chạy lại đầy đủ thành công, không sửa production network để làm test qua.

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
