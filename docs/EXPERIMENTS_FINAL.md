# Thực nghiệm bản tích hợp — 22/10/2026

Dữ liệu kết quả sau sửa: [summary.csv](../experiments/final-2026-10-22-v2/summary.csv) và 18 file trong [raw](../experiments/final-2026-10-22-v2/raw/). **18/18 lượt hoàn tất, tổng ERROR/CONNECTION_LOST = 0.** Không gộp với số liệu 19/10 hoặc lượt bị lỗi trước sửa.

## Cấu hình

| Mục | Giá trị thực tế |
| --- | --- |
| Nguồn | `d8084b5-dirty`; mọi lượt cùng SHA-256 Java source + pom |
| CPU | Intel Core i7-11800H @ 2.30 GHz |
| RAM | Unity báo 7877 MB bộ nhớ vật lý trên máy này |
| Hệ điều hành | Windows 11, build 22631 |
| Công cụ | Java 21.0.10, Maven 3.9.16, Python portable 3.12.10 |
| Mạng | Loopback 127.0.0.1:17777 |
| Map/mode chơi | meadow cố định, QUICK |
| Ma trận | baseline/optimized × 2/4/8 client × 3 lượt × 30 giây |
| Input | MOVE 10 Hz, PING 1 Hz; seed 1, 2, 3; không nổ bom |
| Phạm vi đo | Sau GAME_START, trước teardown; server restart khi đổi sync mode |

Fingerprint nguồn đầy đủ:

```text
698fbcb72a23e47ef29f1413490ae6dbabe9da9599fcc8d346f6175eb4029ab5
```

`load_test.py` băm tên đường dẫn và byte các file Java dưới `server/src/main`, rồi `pom.xml`. Fingerprint này xác định working tree đã đo, không phải một commit đã được đẩy lên remote. Cùng seed input không đồng nghĩa cùng seed spawn/orb; server vẫn sinh cảnh động ngẫu nhiên. Trong lượt đo sau sửa không chạy build/test Unity đồng thời; máy vẫn có ứng dụng nền, chưa cô lập tải hệ điều hành.

## Kết quả

Trung bình ba lượt cho mỗi cấu hình. Cột p95 là **trung bình p95 của từng lượt**, không phải p95 gộp mọi mẫu. Byte tính JSON UTF-8 và newline, không gồm overhead TCP/IP.

| Mode | Client | Byte nhận/client/s | Byte/snapshot | RTT TB (ms) | RTT p95 (ms) | processingMicros TB (µs) |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| baseline | 2 | 94 850,4 | 9 408,4 | 0,432 | 0,706 | 83,2 |
| optimized | 2 | 20 777,0 | 2 014,5 | 0,449 | 1,067 | 76,4 |
| baseline | 4 | 101 530,6 | 10 063,8 | 0,316 | 0,593 | 26,7 |
| optimized | 4 | 26 110,8 | 2 547,9 | 0,306 | 0,642 | 33,8 |
| baseline | 8 | 115 042,9 | 11 410,1 | 0,313 | 0,695 | 18,1 |
| optimized | 8 | 43 546,6 | 4 283,2 | 0,317 | 0,691 | 17,0 |

| Client | Giảm byte nhận/client/s | Giảm byte/snapshot |
| --- | ---: | ---: |
| 2 | 78,1% | 78,6% |
| 4 | 74,3% | 74,7% |
| 8 | 62,1% | 62,5% |

Công thức: `(trung bình baseline − trung bình optimized) / trung bình baseline × 100`. Số liệu làm tròn sau khi tổng hợp từ CSV. Snapshot ở khoảng 10 Hz; cải tiến giảm kích thước message, không giảm tần suất tick.

Kết quả hỗ trợ kết luận **giảm khoảng 62–78% byte nhận/client/s trong kịch bản loopback này**. Không có bằng chứng chắc chắn optimized làm giảm RTT: chênh lệch nhỏ, p95 ở hai client còn cao hơn. Số processingMicros cũng chịu ảnh hưởng warm-up JVM và thứ tự chạy. Không suy ra FPS Unity từ số liệu mạng.

## Lượt lỗi được giữ lại

[Thư mục phép đo trước sửa](../experiments/final-2026-10-22/) có sáu lượt. Lượt baseline N=4, seed=3 ghi một CONNECTION_LOST tại khoảng 5,8 giây rồi phép đo dừng sau lượt đó. Không xóa/sửa raw và không dùng lượt này để tính bảng kết quả bản cuối.

Điều tra phát hiện watchdog có hai lần đọc volatile `writeStartedAt`: writer có thể chuyển timestamp thành 0 giữa hai lần đọc, khiến watchdog đóng nhầm connection. Test `watchdogDoesNotMisreadAWriteFinishingBetweenVolatileReads` thất bại trên code cũ và qua sau sửa. Raw trace không chứa nguyên nhân đóng socket, nên đây là xác nhận race trong code và kết quả sạch sau sửa, không phải chứng minh pháp y duy nhất cho lần mất kết nối cũ.

## Chạy lại

```powershell
powershell -ExecutionPolicy Bypass -File scripts/run_benchmark.ps1 -Duration 30 -Repeats 3 -Map meadow
python scripts/summarize_experiments.py --summary experiments/final-2026-10-22-v2/summary.csv
```

Runner mặc định tạo thư mục kết quả có timestamp mới, đóng gói JAR trước chạy và chỉ dừng đúng tiến trình server nó tạo. Có thể truyền `-Python`, `-Port`, `-OutDir`. Khi nhận lỗi trong lượt đo, runner dừng và giữ bằng chứng. Script tổng hợp không tự loại các lượt lỗi, vì thế phải chọn đúng tập dữ liệu khi báo cáo.

## Giới hạn

Mỗi lượt 30 giây, chưa warm-up riêng; cố định map nhưng chưa cố định cảnh động. Hai cấu hình chạy theo nhóm baseline rồi optimized, không đảo thứ tự. Cùng máy, không có packet loss/latency nhân tạo; không phải LAN/Internet. Chỉ đo Đấu nhanh bằng client giả lập; Hành trình/reconnect được kiểm thử chức năng bằng JUnit và Unity, chưa benchmark tải riêng. Các giới hạn này cần giữ trong báo cáo thay vì tuyên bố hiệu quả phổ quát.
