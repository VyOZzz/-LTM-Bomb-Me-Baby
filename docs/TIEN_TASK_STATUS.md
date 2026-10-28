# Tiến độ công việc Tiên — 28/10/2026

Đối chiếu T1–T5 trong ảnh giao việc. Thực hiện trên working tree `353ef09` có thay đổi chưa commit; không tự commit, gửi tin cho Phương hay nộp lên dịch vụ ngoài.

| Việc | Trạng thái | Kết quả / phần còn thiếu |
| --- | --- | --- |
| T1 — 12 test cho Phương | Đã soạn | [TEST_CASES_FOR_REPORT.md](TEST_CASES_FOR_REPORT.md): đúng 12 test, mỗi test có kịch bản, kết quả kỳ vọng và link source. Sẵn để chuyển cho Phương. |
| T2 — rate limit và test | Hoàn thành | 120 frame/kết nối/giây trượt, tính trước parse, đóng riêng client vượt ngưỡng; thêm 6 test về giới hạn và 1 test seed. Tổng 124 test qua trong mỗi lần chạy baseline/optimized. |
| T3 — đo lại | Hoàn thành | 18/18 lượt × 30 giây, 0 lỗi; giảm byte nhận/client/s 79,0% / 74,4% / 61,7% ở 2/4/8 client. Seed RNG phòng và input cùng 1/2/3; đảo thứ tự từng cặp; JVM mới mỗi lượt. [Số liệu và hash](EXPERIMENTS_FINAL.md). |
| T4 — LAN hai máy | Chưa thực hiện | Chưa có máy thứ hai được cung cấp. Đã chuẩn bị [lệnh và quy trình](LAN_VERIFICATION.md), không gắn nhãn LAN cho số đo loopback. |
| T5 — đóng gói và máy khác | Một phần | Đóng gói source + JAR hiện tại, số đo và tài liệu; có manifest SHA-256 và script kiểm tra sau giải nén. Chưa có Windows player mới, báo cáo Word cuối hoặc kiểm tra trên máy thứ hai. |

## Chi tiết T2

`RequestRateLimiter` dùng vòng đệm 120 timestamp, đồng hồ đơn điệu, không dùng khóa chung giữa client. Tại thời điểm đúng một giây, frame cũ hết hạn; không có lỗi cho phép hai đợt 120 frame quanh mốc giây tròn. Giới hạn tính cả HELLO, PING, JSON sai và dòng rỗng, kể cả trước HELLO.

Khi vượt ngưỡng, socket bị đóng trước cập nhật `seq`/action, không tạo thêm ERROR cho mỗi frame flood. Session được dọn theo luồng cũ; người đang chơi vẫn giữ cửa sổ reconnect 10 giây. Giới hạn là theo kết nối, chưa theo IP/tài khoản.

Bằng chứng: [JUnit baseline](validation/server-2026-10-28/baseline/), [JUnit optimized](validation/server-2026-10-28/optimized/). Các fixture chọn mode riêng vẫn giữ mode đó khi chạy cả hai lệnh Maven. Test mạng có client đối chứng tiếp tục nhận PONG và thực thi MOVE sau khi client flood bị đóng.

## Chặn build Windows

Lần thử `scripts/build_client.ps1 -Isolated` đầu bị Unity Package Manager không kết nối IPC trong sandbox. Lần chạy ngoài sandbox kết nối được Package Manager, nhưng Unity kết thúc với mã 198: `No valid Unity Editor license found. Please activate your license.` [Trích log](validation/server-2026-10-28/unity-build-blocker.txt).

DLL `builds/windows/BombMeBaby_Data/Managed/Assembly-CSharp.dll` cũ (25/10) không chứa `protocolVersion`, `ProtocolVersion` hay `VERSION_MISMATCH`; không dùng bản cũ đó để khẳng định tương thích server yêu cầu phiên bản hiện tại. Gói source-only không kèm player này. Cần kích hoạt license Unity hợp lệ trên máy build và chạy build lại từ source.

Không có file Word báo cáo cuối đã được nhóm duyệt tại vị trí đóng gói. Markdown đã được cập nhật; tỷ lệ đóng góp, Word, Compilatio và nộp bài vẫn cần nhóm hoàn thiện.

## Đóng gói / kiểm tra

```powershell
python scripts/package_project.py --name Nhom07_BombMeBaby-SourceServer-2026-10-28 --source-only --experiments-dir experiments/final-2026-10-28
python scripts/verify_package.py builds/Nhom07_BombMeBaby-SourceServer-2026-10-28
```

Output nằm dưới `builds/` (Git bỏ qua), gồm thư mục và ZIP cùng tên. `PACKAGE_STATUS.json` ghi rõ không có Windows player/Word và chưa kiểm tra máy thứ hai. Kiểm tra CRC ZIP, SHA-256 sau giải nén và smoke server cục bộ được lưu riêng sau khi gói tạo xong; không coi các bước này là kiểm thử LAN hoặc máy sạch.

Sau khi có license và Word đã duyệt, build client, đóng gói với tên mới **không có** `--source-only` và thêm `--report-docx <đường-dẫn-báo-cáo.docx>`. Sau đó làm quy trình máy B trong tài liệu LAN.
