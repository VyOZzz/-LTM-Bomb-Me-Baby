# Thực nghiệm bản hiện tại — 28/10/2026

Đo lại sau khi thêm rate limit và seed server. **18/18 lượt hoàn tất, tổng ERROR/CONNECTION_LOST = 0.** Optimized giảm **61,7–79,0% byte nhận/client/s** trong kịch bản này. Đây là dữ liệu của working tree có fingerprint dưới đây, không dùng số liệu 25/10 để đại diện cho server vừa sửa.

Dữ liệu: [summary.csv](../experiments/final-2026-10-28/summary.csv), [raw](../experiments/final-2026-10-28/raw/), [metadata và thứ tự chạy](../experiments/final-2026-10-28/metadata.json), [môi trường](../experiments/final-2026-10-28/environment.json). Log server được lưu riêng từng lượt trong cùng thư mục.

## Cấu hình

| Mục | Giá trị thực tế |
| --- | --- |
| CPU / RAM | Intel Core i7-11800H @ 2.30 GHz / 7,69 GiB RAM vật lý khả dụng cho Windows |
| Hệ điều hành | Windows 11, build 22631 |
| Công cụ | Java 21.0.10 (target Java 17), Maven 3.9.16, Python portable 3.12.10 |
| Nguồn | Working tree `353ef09-dirty`, đo ngày 28/10/2026; SHA-256 xác định bản server, gồm file mới chưa commit |
| Mạng | Loopback `127.0.0.1:17777` |
| Map / mode chơi | `meadow`, QUICK |
| Ma trận | 2 chế độ × 2/4/8 client × 3 lượt × 30 giây = 18 lượt |
| Input | MOVE 10 Hz, PING 1 Hz, không nổ bom; seed input 1/2/3 |
| Seed server | `-Dgame.seed=1`, `2`, `3` tương ứng seed input; mỗi phòng có RNG riêng |
| Thứ tự | Theo cặp cùng số client/seed, đảo thứ tự mỗi cặp; cặp đầu optimized → baseline |
| JVM | Khởi động mới mỗi lượt, không warm-up riêng |
| Phạm vi đo | Sau GAME_START, trước teardown; JSON UTF-8 + newline, không gồm overhead TCP/IP |

Fingerprint SHA-256 nguồn Java dưới `server/src/main` + `pom.xml` (bao gồm tên đường dẫn và byte file):

```text
c285bc7317034bd4924bd63129e2f58ab6cd45d4515e16cd28402085975e1247
```

SHA-256 JAR thực sự được runner khởi động:

```text
fa2bb1c1340a75e268003f371cbd4f0d1965a5e89bf6cbe5b62bea17e5384671
```

## Kết quả

Trung bình ba lượt mỗi cấu hình. p95 là trung bình p95 từng lượt, không phải p95 gộp. Không có lượt lỗi bị loại khỏi bảng.

| Mode | Client | Byte nhận/client/s | Byte/snapshot | RTT TB (ms) | RTT p95 (ms) | processingMicros TB (µs) |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| baseline | 2 | 95 851,1 | 9 497,8 | 0,679 | 1,213 | 170,4 |
| optimized | 2 | 20 093,6 | 1 946,9 | 0,625 | 1,349 | 157,2 |
| baseline | 4 | 102 519,8 | 10 162,5 | 0,817 | 1,693 | 119,7 |
| optimized | 4 | 26 267,4 | 2 562,0 | 0,589 | 1,498 | 101,3 |
| baseline | 8 | 115 693,0 | 11 460,3 | 1,036 | 4,243 | 150,3 |
| optimized | 8 | 44 331,2 | 4 354,7 | 0,559 | 1,252 | 91,5 |

| Client | Giảm byte nhận/client/s | Giảm byte/snapshot |
| --- | ---: | ---: |
| 2 | 79,0% | 79,5% |
| 4 | 74,4% | 74,8% |
| 8 | 61,7% | 62,0% |

Công thức: `(baseline − optimized) / baseline × 100`. Tần suất snapshot vẫn khoảng 10 Hz; cải tiến chủ yếu giảm byte/message.

## Kiểm soát phép đo và giới hạn

Seed cố định làm vị trí spawn và vật phẩm ban đầu giống nhau giữa hai chế độ; JUnit đối chiếu trực tiếp với 2/4/8 người và seed 1/2/3. Lịch luồng, thời điểm input và tick vẫn khác nên không tuyên bố toàn bộ quỹ đạo giống hệt. Player ID, resume token và mã phòng vẫn ngẫu nhiên.

Đã đảo thứ tự mỗi cặp: 5 cặp optimized trước, 4 cặp baseline trước. Mỗi lượt dùng JVM mới để tránh nối tiếp trạng thái warm-up; chưa có warm-up riêng, chưa đo CPU/GC, không cô lập ứng dụng nền. Chưa đo LAN/Internet, packet loss, FPS Unity hoặc tải Hành trình. RTT loopback không chứng minh cải thiện độ trễ trên mạng thật.

Hai chế độ đều bật rate limit 120 frame/kết nối/giây trượt. Tải benchmark 10 MOVE/s + 1 PING/s nằm dưới ngưỡng; số liệu này đo đồng bộ trạng thái, không phải benchmark chống flood.

## Chạy lại

```powershell
# Chọn thư mục mới; runner từ chối ghi đè bộ số liệu đã tồn tại.
powershell -ExecutionPolicy Bypass -File scripts/run_benchmark.ps1 -Duration 30 -Repeats 3 -Seed 1 -Map meadow -OutDir experiments/repeat-2026-10-28
python scripts/summarize_experiments.py --summary experiments/repeat-2026-10-28/summary.csv
```

Máy hiện tại dùng Python portable nên lệnh đo thực tế là `builds/tools/python/python.exe scripts/run_benchmark.py --clients 2 4 8 --repeats 3 --duration 30 --out-dir experiments/final-2026-10-28`, sau `mvn test package "-Dsync.mode=optimized"`. Tham số còn lại lưu trong metadata. Runner giữ hash nguồn/JAR, kiểm tra seed server qua WELCOME và dừng nếu một lượt có lỗi. Đo LAN theo [LAN_VERIFICATION.md](LAN_VERIFICATION.md).

## Số đo lịch sử

Phép đo 25/10 và 22/10 được giữ nguyên: [mô tả lịch sử](EXPERIMENTS_2026-10-25.md), [raw 25/10](../experiments/final-2026-10-25/raw/), [raw 22/10](../experiments/final-2026-10-22-v2/raw/). Các lần trước chưa cố định seed cảnh và chạy theo nhóm baseline trước; không gộp với dữ liệu hiện tại.
