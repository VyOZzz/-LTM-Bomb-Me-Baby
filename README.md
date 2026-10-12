# Bomb Me Baby

Prototype game PvP arena 2.5D cho 2–4 người, xây theo GDD được cung cấp. Unity client hiển thị arena và gửi ý định điều khiển qua TCP; Java server giữ trạng thái chính thức của trận, tính va chạm, nhặt vật phẩm, sát thương và người thắng.

## Cấu trúc

- `server/`: Java 17, TCP JSON theo từng dòng, game loop 20 Hz, snapshot 10 Hz, thread pool cho client.
- `unity/`: project Unity 2022.3 LTS. Cảnh chơi được tạo tự động khi lần đầu mở project; arena và UI được dựng tại runtime từ các primitive 3D.
- `scripts/network_smoke.py`: kiểm tra kết nối thật với 2 client giả lập.
- `docs/GDD Bomb Me Baby.docx`: bản GDD gốc để nhóm đối chiếu trong quá trình phát triển.

## Chạy server

Cần JDK 17+ và Maven 3.9+.

```sh
cd server
mvn test
mvn exec:java -Dexec.args="7777"
```

Server mặc định nghe TCP port `7777`. Mở firewall cho port này nếu người chơi ở máy khác. Giao thức prototype là TCP thuần trong mạng tin cậy; chưa có đăng nhập hay TLS.

## Chạy Unity client

1. Mở thư mục `unity/` bằng Unity Hub với Unity 2022.3 LTS. Nếu Unity đề nghị nâng version hoặc import package, cho phép Unity hoàn thành import.
2. Script Editor sẽ tạo `Assets/Scenes/Main.unity` và thêm scene vào Build Settings. Mở scene này nếu Unity chưa mở tự động.
3. Nhấn Play. Nhập tên, IP server, port rồi chọn **Kết nối**.
4. Client thứ nhất chọn **Tạo phòng**. Các client còn lại nhập mã 6 ký tự và **Vào phòng**.
5. Cả 2–4 người chọn **Sẵn sàng**. Trận bắt đầu tự động khi tất cả cùng sẵn sàng. Để chơi nhiều client trên cùng máy, build một bản Windows/Mac rồi chạy cùng lúc với Unity Editor, hoặc chạy nhiều bản build.

Điều khiển: **WASD** di chuyển, **giữ Space** nạp và **thả Space** để nổ, **E** bật Shield, **Shift** Boost. Sau trận, mọi người chọn **Sẵn sàng** để chơi lại cùng phòng.

## Luật prototype

- 100 HP, không hồi sinh; người cuối cùng sống thắng. Energy tăng Size 0.12 và hồi 3 HP, tối đa 100 HP. Size tối đa 2.6.
- Nạp tối đa 8 giây, sau đó tự nổ. Bán kính và sát thương theo Size, thời gian nạp và khoảng cách. Người nổ không bị sát thương bởi vụ nổ của chính mình; sau nổ Size về Base Size. Bom Size 1 vẫn gây sát thương ở cự ly gần.
- Shield hoạt động ngay trong 1.5 giây, chặn 100% sát thương nổ và phản 50% về người tấn công; cooldown 9 giây.
- Boost tăng tốc trong 0.9 giây, giảm Size 0.23 và rơi một Energy Orb, cooldown 5 giây.
- Khi bị loại, player rơi Core. Người nhặt Core tăng Base Size 0.1, tối đa 1.4.
- Mất kết nối trong trận có 10 giây để kết nối lại bằng resume token. Quá hạn sẽ bị loại.

Các thông số được đặt ở `server/src/main/java/bombmebaby/GameRules.java`; đây là giá trị prototype cụ thể hoá các khoảng gợi ý trong GDD.

## Giao thức và kiểm tra

Xem [docs/PROTOCOL.md](docs/PROTOCOL.md) để biết message JSON. Server không nhận vị trí, HP hoặc sát thương do client tự khai báo; client chỉ gửi hướng và hành động. Mỗi room được khoá riêng khi xử lý input và tick, bảo đảm một Core chỉ được một người nhặt.

Khi server đang chạy:

```sh
python3 scripts/network_smoke.py
```

Unity client hiển thị RTT, số message gửi/nhận và dùng nội suy vị trí giữa các snapshot. `ACK.processingMicros` là thời gian server xử lý một message; các chỉ số hiện chưa được ghi ra file để so sánh benchmark tự động.

## Giới hạn hiện tại

- Cần mở project bằng Unity để Unity tạo scene và kiểm tra build hình ảnh. Phần Unity không thể được chạy trong môi trường không có Unity Editor.
- Chưa có lưu lịch sử trận, bot, skin, matchmaking, TLS, hoặc tối ưu state sync theo khoảng cách. Snapshot hiện gửi toàn bộ trạng thái ở 10 Hz để làm baseline cho thí nghiệm mạng.
- Không có cơ chế chống gian lận toàn diện; server đã giới hạn movement và bỏ qua sequence trùng/cũ.
