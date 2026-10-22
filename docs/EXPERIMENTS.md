# Thực nghiệm đồng bộ trạng thái — Bomb Me Baby

Đây là mốc lịch sử 19/10. Phép đo bản tích hợp 22/10, map cố định, dữ liệu lỗi trước sửa và 18 lượt sau sửa được ghi riêng tại [EXPERIMENTS_FINAL.md](EXPERIMENTS_FINAL.md).

Người phụ trách: Thành viên B. Đo ngày 19/10/2026. Dữ liệu: `experiments/summary.csv` (mỗi dòng một lượt đo) và `experiments/raw/` (mỗi dòng một message gửi hoặc nhận).

## Câu hỏi

Chế độ `optimized` của server (A, nhánh `ThuyTien`, `SyncMode.java`) giảm bao nhiêu dữ liệu đồng bộ so với `baseline`, và có ảnh hưởng đến độ trễ hay thời gian xử lý không?

- `baseline`: mỗi `STATE_UPDATE` gửi đủ đá, bụi, mọi người chơi (kể cả tọa độ người đang khuất) và mọi orb/Core.
- `optimized`: đá và bụi chỉ gửi một lần trong `GAME_START`; người chơi không nhìn thấy có x/z = 0; orb/Core xa hơn 30 đơn vị bị lọc. Đây là gửi chọn lọc, không phải delta từng trường.

## Điều kiện đo

| Mục | Giá trị |
| --- | --- |
| Code server | merge `d641945` (nhánh `lanphuong`, đã gồm `ThuyTien`); cùng một bản build cho cả hai chế độ |
| Máy | Intel Core i9-13900H (14 nhân / 20 luồng), 15,6 GB RAM, Windows 11 |
| Phần mềm | Java 26.0.2, Maven 3.9.9, Python 3.13.14 |
| Mạng | Server và client cùng máy, `127.0.0.1:7777` (loopback) |
| Kịch bản | N client giả lập vào một phòng; mỗi client gửi `MOVE` 10 Hz (đổi hướng ngẫu nhiên, seed = số lượt), `PING` 1 Hz; không ai nổ nên trận không kết thúc |
| Ma trận | 2 chế độ × N = 2/4/8 × 3 lượt lặp, mỗi lượt 60 giây; server khởi động lại khi đổi chế độ |
| Phần được đo | Chỉ từ sau `GAME_START` đến hết 60 giây; traffic vào phòng và rời phòng không tính |

Cột `commit` trong `summary.csv`: hai lượt đầu ghi `d641945`, các lượt sau ghi `c556012` vì commit script được tạo trong lúc đo. `c556012` chỉ đổi script Python, không đổi server; nội dung `load_test.py` giống nhau ở mọi lượt.

## Cách chạy lại

```powershell
cd server; mvn compile; cd ..
powershell -ExecutionPolicy Bypass -File scripts/run_benchmark.ps1
python scripts/summarize_experiments.py
```

`run_benchmark.ps1` tự bật server ở từng chế độ, kiểm tra log server đúng chế độ rồi chạy `load_test.py`; `load_test.py` tự nhận chế độ từ snapshot (có `obstacles` là baseline) và ghi vào cột `sync_mode`.

## Kết quả

Trung bình 3 lượt cho mỗi ô. 18/18 lượt không có `ERROR` hay mất kết nối.

| Chế độ | Client | Map đã rơi vào | Byte nhận/client/s | Byte TB/STATE_UPDATE | Message nhận/client/s | RTT TB (ms) | RTT p95 (ms) | processingMicros TB (µs) |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| baseline | 2 | crater, crossroads, meadow | 97 630 | 9 691 | 21,13 | 0,63 | 1,44 | 172,8 |
| optimized | 2 | crater, meadow | 21 229 | 2 064 | 21,12 | 0,60 | 1,57 | 129,1 |
| baseline | 4 | crater, crossroads, meadow | 103 320 | 10 258 | 21,32 | 0,35 | 0,66 | 61,7 |
| optimized | 4 | crossroads, meadow | 26 697 | 2 608 | 21,31 | 0,33 | 0,77 | 56,0 |
| baseline | 8 | crater, meadow | 114 758 | 11 398 | 21,58 | 0,35 | 0,73 | 40,8 |
| optimized | 8 | crater, meadow | 39 184 | 3 854 | 21,43 | 0,30 | 0,74 | 42,4 |

| Client | Giảm byte nhận/client/s | Giảm byte/STATE_UPDATE |
| --- | --- | --- |
| 2 | 78,3% | 78,7% |
| 4 | 74,2% | 74,6% |
| 8 | 65,9% | 66,2% |

Công thức: `(baseline − optimized) / baseline × 100`, tính trên giá trị trung bình.

## Nhận xét

- **Băng thông:** `optimized` giảm 66–79% byte server gửi cho mỗi client. Phần lớn đến từ việc bỏ đá/bụi khỏi mỗi snapshot; khi N tăng, danh sách người chơi chiếm tỉ lệ lớn hơn nên mức giảm nhỏ dần (79% → 66%).
- **Số message không đổi** (~21/client/s: 10 `STATE_UPDATE` + 10 `ACK` cho `MOVE` + 1 `PONG`, cộng vài event như `ENERGY_PICKED` khi client đi qua orb). Cải tiến làm nhỏ message, không làm thưa message.
- **RTT và processingMicros:** trên loopback đều dưới 2 ms và chênh lệch giữa hai chế độ nhỏ hơn dao động giữa các lượt. Không kết luận được `optimized` làm giảm độ trễ. processingMicros của N = 2 cao hơn vì đó là các lượt chạy đầu tiên sau khi bật server (JVM chưa JIT xong).

## Giới hạn

- **Map ngẫu nhiên:** server tự chọn map và chưa có cờ cố định seed, nên hai chế độ không rơi vào cùng bộ map (xem cột "Map đã rơi vào"). Các map có số đá/bụi khác nhau (12 đá; 14–16 bụi), nên mức giảm từng ô có sai số theo map.
- **Byte đo được** là JSON cộng ký tự xuống dòng (server dùng `println`, trên Windows là CRLF) đọc từ stream; chưa gồm header TCP/IP.
- **Loopback** không có độ trễ hay mất gói thật; chưa đo trên LAN/Wi-Fi.
- **Client giả lập** không nổ, không nhặt Core, không reconnect; snapshot khôi phục khi reconnect luôn là baseline nên không nằm trong phép đo này.
- **8 client** là giả lập; demo chính dùng 2–4 client Unity theo GDD.

## Việc tiếp theo

- Cố định map khi đo (cần A thêm cờ chọn map hoặc seed cho server), rồi đo lại để so từng map.
- Đo trên hai máy qua LAN.
- Đo lại bằng cùng kịch bản sau khi có chế độ Hành trình Ba Mảnh, đổi `--label`.
