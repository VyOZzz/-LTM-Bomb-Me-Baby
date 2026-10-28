# Số đo lịch sử — 25/10/2026

Lưu lại mô tả phép đo 25/10 trên commit `c35616f`. Đây là số liệu của bản cũ; bản server sau rate limit/seed được đo lại ngày 28/10, xem `experiments/final-2026-10-28/`.

Dữ liệu: [summary.csv](../experiments/final-2026-10-25/summary.csv), 18 file trong [raw](../experiments/final-2026-10-25/raw/) và log server của từng chế độ. **18/18 lượt hoàn tất, tổng ERROR/CONNECTION_LOST = 0.**

## Cấu hình

| Mục | Giá trị thực tế |
| --- | --- |
| Nguồn | commit `c35616f`, không dirty; mọi lượt cùng SHA-256 Java source + pom |
| CPU | 13th Gen Intel Core i9-13900H |
| RAM | 15,6 GB |
| Hệ điều hành | Windows 11 Home Single Language, build 26200 |
| Công cụ | Java 26.0.2 (target Java 17), Maven 3.9.9, Python 3.13.14 |
| Mạng | Loopback 127.0.0.1:17777 |
| Map/mode chơi | meadow cố định, QUICK |
| Ma trận | baseline/optimized × 2/4/8 client × 3 lượt × 30 giây |
| Input | MOVE 10 Hz, PING 1 Hz; seed 1, 2, 3; không nổ bom |
| Phạm vi đo | Sau GAME_START, trước teardown; server restart khi đổi sync mode |
| Thời gian chạy | 25/10/2026, 18:08–18:17 |

Fingerprint nguồn đầy đủ:

```text
f6a56e39675794c4dd6fa6bb740986625c2087e5cb1e37ceb9f32fbb9052b087
```

`load_test.py` băm tên đường dẫn và byte các file Java dưới `server/src/main`, rồi `pom.xml`. Cùng seed input không đồng nghĩa cùng seed spawn/orb; server vẫn sinh cảnh động ngẫu nhiên. Máy vẫn có ứng dụng nền, chưa cô lập tải hệ điều hành.

## Kết quả

Trung bình ba lượt cho mỗi cấu hình. Cột p95 là **trung bình p95 của từng lượt**, không phải p95 gộp mọi mẫu. Byte tính JSON UTF-8 và newline, không gồm overhead TCP/IP.

| Mode | Client | Byte nhận/client/s | Byte/snapshot | RTT TB (ms) | RTT p95 (ms) | processingMicros TB (µs) |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| baseline | 2 | 95 981,8 | 9 511,4 | 0,668 | 1,501 | 160,7 |
| optimized | 2 | 21 316,2 | 2 068,5 | 0,765 | 2,038 | 182,1 |
| baseline | 4 | 102 304,8 | 10 140,6 | 0,402 | 0,905 | 68,8 |
| optimized | 4 | 28 005,9 | 2 735,2 | 0,429 | 0,919 | 65,1 |
| baseline | 8 | 116 644,1 | 11 570,5 | 0,447 | 0,968 | 51,9 |
| optimized | 8 | 44 364,9 | 4 362,3 | 0,414 | 0,939 | 50,7 |

| Client | Giảm byte nhận/client/s | Giảm byte/snapshot |
| --- | ---: | ---: |
| 2 | 77,8% | 78,3% |
| 4 | 72,6% | 73,0% |
| 8 | 62,0% | 62,3% |

Công thức: `(trung bình baseline − trung bình optimized) / trung bình baseline × 100`. Snapshot ở khoảng 10 Hz; cải tiến giảm kích thước message, không giảm tần suất tick.

Kết quả hỗ trợ kết luận **giảm khoảng 62–78% byte nhận/client/s trong kịch bản loopback này**. Không có bằng chứng optimized làm giảm RTT: chênh lệch nhỏ, p95 ở hai client còn cao hơn baseline. processingMicros chịu ảnh hưởng warm-up JVM và thứ tự chạy (baseline chạy trước). Không suy ra FPS Unity từ số liệu mạng.

## So sánh với lần đo 22/10

Lần đo 22/10 (mục dưới) chạy trên máy khác (i7-11800H, Java 21) và bản code trước các thay đổi gameplay 23–25/10. Tỉ lệ giảm byte nhận/client/s hai lần gần như trùng:

| Client | 22/10 | 25/10 |
| --- | ---: | ---: |
| 2 | 78,1% | 77,8% |
| 4 | 74,3% | 72,6% |
| 8 | 62,1% | 62,0% |

Byte/snapshot tuyệt đối của bản cuối lớn hơn khoảng 1% (ví dụ baseline 2 client: 9 408 → 9 511 byte), nhiều khả năng do các thay đổi protocol 23–25/10; nhóm chưa phân tích chi tiết từng trường. RTT và processingMicros tuyệt đối khác nhau giữa hai máy nên không so sánh trực tiếp.

## Chạy lại

```powershell
powershell -ExecutionPolicy Bypass -File scripts/run_benchmark.ps1 -Duration 30 -Repeats 3 -Map meadow -OutDir experiments/final-2026-10-25
python scripts/summarize_experiments.py --summary experiments/final-2026-10-25/summary.csv
```

Runner đóng gói JAR trước khi chạy và chỉ dừng đúng tiến trình server nó tạo. Có thể truyền `-Python`, `-Port`, `-OutDir`. Khi nhận lỗi trong lượt đo, runner dừng và giữ bằng chứng. Script tổng hợp không tự loại các lượt lỗi, vì thế phải chọn đúng tập dữ liệu khi báo cáo.

## Giới hạn

Mỗi lượt 30 giây, chưa warm-up riêng; cố định map nhưng chưa cố định cảnh động. Hai cấu hình chạy theo nhóm baseline rồi optimized, không đảo thứ tự. Cùng máy, không có packet loss/latency nhân tạo; không phải LAN/Internet. Chỉ đo Đấu nhanh bằng client giả lập; Hành trình/reconnect được kiểm thử chức năng bằng JUnit và Unity, chưa benchmark tải riêng. Các giới hạn này cần giữ trong báo cáo thay vì tuyên bố hiệu quả phổ quát.

---

# Lần đo trước — 22/10/2026

Giữ lại để đối chứng. Đo trên working tree `d8084b5-dirty`, trước các thay đổi gameplay/protocol 23–25/10.

Dữ liệu: [summary.csv](../experiments/final-2026-10-22-v2/summary.csv) và 18 file trong [raw](../experiments/final-2026-10-22-v2/raw/). **18/18 lượt hoàn tất, tổng ERROR/CONNECTION_LOST = 0.**

| Mục | Giá trị thực tế |
| --- | --- |
| CPU | Intel Core i7-11800H @ 2.30 GHz |
| RAM | Unity báo 7877 MB bộ nhớ vật lý |
| Hệ điều hành | Windows 11, build 22631 |
| Công cụ | Java 21.0.10, Maven 3.9.16, Python portable 3.12.10 |
| Fingerprint nguồn | `698fbcb72a23e47ef29f1413490ae6dbabe9da9599fcc8d346f6175eb4029ab5` |

Ma trận, input và phạm vi đo giống lần 25/10.

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

## Lượt lỗi được giữ lại

[Thư mục phép đo trước sửa](../experiments/final-2026-10-22/) có sáu lượt. Lượt baseline N=4, seed=3 ghi một CONNECTION_LOST tại khoảng 5,8 giây rồi phép đo dừng sau lượt đó. Không xóa/sửa raw và không dùng lượt này để tính bảng kết quả.

Điều tra phát hiện watchdog có hai lần đọc volatile `writeStartedAt`: writer có thể chuyển timestamp thành 0 giữa hai lần đọc, khiến watchdog đóng nhầm connection. Test `watchdogDoesNotMisreadAWriteFinishingBetweenVolatileReads` thất bại trên code cũ và qua sau sửa. Raw trace không chứa nguyên nhân đóng socket, nên đây là xác nhận race trong code và kết quả sạch sau sửa, không phải chứng minh pháp y duy nhất cho lần mất kết nối cũ.
