# T4/T5 — đo LAN hai máy và kiểm tra gói

**Trạng thái 28/10/2026: chưa đo trên hai máy.** Kết quả loopback không được ghi thành kết quả LAN. Các lệnh dưới đây là quy trình để nhóm chạy khi có máy B, không phải bằng chứng đã chạy.

## Chuẩn bị

- Máy A chạy JAR hiện tại bằng Java 17+, máy B chạy Python 3.10+ và bản source tương ứng. Cả hai cùng LAN; máy B đặt IP của máy A trong `--host`.
- Từ thư mục source trên máy A: `mvn -f server/pom.xml package`.
- Ghi model CPU/RAM, Windows, Java, Python của cả hai máy, loại mạng Wi-Fi/Ethernet, thời điểm đo. Ghi SHA-256 JAR bằng `Get-FileHash server/target/bomb-me-baby-server-1.0.0.jar -Algorithm SHA256` và đối chiếu với `metadata.json` của lần đo loopback nếu dùng đúng JAR đã đóng gói.
- Máy B dùng `Test-NetConnection <IP-máy-A> -Port 17777` khi server đã chạy. Nếu firewall chặn, người quản lý máy A cho phép cổng TCP này trên mạng riêng. Không cần mở cổng ra Internet.

`load_test.py` băm mã nguồn **trên máy chạy script**; khi đo LAN, hash đó không tự chứng minh JAR trên máy A. Phải giữ hash JAR và log máy A cùng dữ liệu máy B.

## Bốn lượt ngắn, 4 client mỗi lượt

Thứ tự: optimized seed 1 → baseline seed 1 → baseline seed 2 → optimized seed 2. Mỗi lượt khởi động lại server, dùng cùng map, seed phòng và seed input trong một cặp. Dùng thư mục kết quả mới để tránh trộn số liệu.

Trên máy A, chạy từ thư mục source, lần đầu:

```powershell
java '-Dsync.mode=optimized' '-Ddemo.map=meadow' '-Dgame.seed=1' -jar server/target/bomb-me-baby-server-1.0.0.jar 17777
```

Trên máy B, chạy từ thư mục source sau khi server báo sẵn sàng:

```powershell
# Đổi thành IPv4 LAN thực tế của máy A.
$ServerIp = '192.168.1.20'
python scripts/load_test.py --host $ServerIp --port 17777 --clients 4 --duration 30 --seed 1 --expect-game-seed 1 --label optimized-lan --out-dir experiments/lan-2026-10-28
```

Sau mỗi lượt, dừng server bằng Ctrl+C rồi chạy lại theo bảng:

| Lượt | Máy A: sync.mode | Máy A: game.seed | Máy B: --seed và --expect-game-seed | Máy B: --label |
| --- | --- | --- | --- | --- |
| 1 | optimized | 1 | 1 | optimized-lan |
| 2 | baseline | 1 | 1 | baseline-lan |
| 3 | baseline | 2 | 2 | baseline-lan |
| 4 | optimized | 2 | 2 | optimized-lan |

Giữ nguyên `--host`, `--clients 4`, `--duration 30` và thư mục output. Chụp/lưu log server riêng từng lượt. Script dừng với mã lỗi khi có ERROR/CONNECTION_LOST; giữ cả raw lỗi, không xóa để làm đẹp kết quả.

```powershell
python scripts/summarize_experiments.py --summary experiments/lan-2026-10-28/summary.csv
```

Chỉ cập nhật báo cáo thành “đã đo LAN” khi đủ summary/raw và thông tin hai máy; ghi rõ RTT, số lỗi, giảm byte và giới hạn chỉ 1–2 lượt/mode. Không gộp trung bình LAN vào bảng loopback.

## Kiểm tra gói trên máy B

1. Chép ZIP sang máy B và đối chiếu SHA-256 của **ZIP** trước/sau khi chép (`Get-FileHash <file.zip>`).
2. Giải nén vào thư mục mới; mở PowerShell tại thư mục chứa `START_HERE.md`.
3. Chạy `python Source/scripts/verify_package.py .`: phải PASS toàn bộ file, kích thước, SHA-256 và link Markdown trong Report. Chạy bước này trước khi Maven tạo thêm `target/` trong gói.
4. Mở `Playable/Server/StartServer.cmd`. Ở terminal khác chạy `python Source/scripts/network_smoke.py 7777`: phải PASS.
5. Từ `Source/server`, chạy `mvn test "-Dsync.mode=baseline"` và `mvn test "-Dsync.mode=optimized"`: hiện kỳ vọng 124 test/lần.
6. Nếu `PACKAGE_STATUS.json` ghi `windows_player_included=false`, gói **chưa có bản chơi Windows**. Máy có license Unity hợp lệ cần build `Source/scripts/build_client.ps1 -Isolated`, rồi kiểm tra hai client tạo/vào phòng, sẵn sàng, chơi và reconnect. Đóng gói lại không có `--source-only` sau khi build thành công.
7. Kiểm tra có bản Word cuối đúng tên, tỷ lệ đóng góp và kết quả Compilatio. Chỉ ghi đạt những mục đã kiểm tra thực tế.

Lưu ngày kiểm tra, máy kiểm tra, hash ZIP, output verifier/Maven/smoke và ảnh hai client vào thư mục bằng chứng mới. Việc giải nén và chạy server trên chính máy A chỉ chứng minh kiểm tra cục bộ, không thay thế các bước trên máy B.
