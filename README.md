# Bomb Me Baby

Prototype game PvP arena 2.5D cho tối đa 10 người, xây theo GDD được cung cấp. Unity client hiển thị arena và gửi ý định điều khiển qua TCP; Java server giữ trạng thái chính thức của trận, chọn map, sinh vị trí người chơi, tính va chạm, nhặt vật phẩm, sát thương và người thắng.

## Cấu trúc

- `server/`: Java 17, TCP JSON theo từng dòng, game loop 20 Hz, snapshot 10 Hz, thread pool cho client.
- `LTM-Bomb-Me-Baby/`: project Unity 6.3 LTS (`6000.3.6f1`) với Unity AI Assistant. Arena, camera, ánh sáng, vật cản và spawn point được lưu thành GameObject thật trong scene; player, pickup và VFX được sinh theo trạng thái mạng.
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

## Tài liệu và kiểm thử phần Java server (thành viên A)

- [Học từ đầu và giải thích từng phần code](docs/THANH_VIEN_A_HUONG_DAN.md).
- [Kết quả 38 test và phạm vi đã kiểm chứng](docs/THANH_VIEN_A_KIEM_THU.md).
- [Bàn giao cho T/B: tích hợp JSON và so sánh baseline/optimized](docs/THANH_VIEN_A_BAN_GIAO.md).

Server mặc định dùng baseline. Từ thư mục `server/`, kiểm tra cả hai cấu hình:

```powershell
mvn clean test "-Dsync.mode=baseline"
mvn test "-Dsync.mode=optimized"
```

Chạy server bằng `mvn compile exec:java "-Dexec.args=7777" "-Dsync.mode=baseline"`. Chế độ `optimized` đã có phía server nhưng cần T cập nhật Unity và thống nhất protocol trước khi dùng cho demo. Cải tiến hiện tại là đồng bộ chọn lọc, chưa phải delta từng trường giữa hai snapshot.

## Chạy Unity client

1. Cài Unity `6000.3.6f1` bằng Unity Hub, sau đó mở thư mục `LTM-Bomb-Me-Baby/` và chờ Unity import xong.
2. Mở `Assets/Scenes/Main.unity`. Nếu scene chưa có hierarchy `Bomb Me Baby`, chọn **Tools > Bomb Me Baby > Rebuild Main Scene**.
3. Nhấn Play. Nhập tên, IP server, port rồi chọn **Kết nối**.
4. Client thứ nhất chọn **Tạo phòng**. Các client còn lại nhập mã 6 ký tự và **Vào phòng**.
5. Chọn **Sẵn sàng** để bắt đầu. Hiện tại có thể chơi một mình để kiểm thử; khi có nhiều người, trận bắt đầu khi tất cả cùng sẵn sàng (tối đa 10 người). Để chơi nhiều client trên cùng máy, build một bản Windows/Mac rồi chạy cùng lúc với Unity Editor, hoặc chạy nhiều bản build.

Điều khiển: **WASD/phím mũi tên** di chuyển theo hướng màn hình, **giữ Space** nạp và **thả Space** để nổ, **E** bật Shield, **Shift** Boost. Camera bám sát quả bóng của người chơi và chỉ hiển thị một vùng nhỏ của map. Mỗi vòng dùng một trong ba map server-authoritative rộng 96×96 đến 104×104; vòng kế tiếp trong cùng phòng sẽ đổi sang map khác.

## Bật AI trong Unity Editor

Project đã khai báo package `com.unity.ai.assistant` tương thích với Unity 6.3. Sau lần import đầu tiên:

1. Đăng nhập Unity ID trong Unity Hub và liên kết project với một Unity Cloud project.
2. Nếu nút **AI** chưa hiện, nhấp chuột phải vùng trống trên toolbar rồi chọn **Services > AI**.
3. Mở **AI**, chấp nhận điều khoản và để Unity hoàn tất cài đặt/kích hoạt package.
4. Chọn **Open Assistant** để hỏi, sinh hoặc review code C# ngay trong Editor.

Unity AI Assistant cần kết nối mạng và quyền sử dụng/trial hoặc credits trên tài khoản Unity. File package chỉ chuẩn bị project; không thể thay người dùng đăng nhập hay chấp nhận điều khoản dịch vụ.

## Luật prototype

- 100 HP, không hồi sinh; người cuối cùng sống thắng. Energy tăng Size 0.12 và hồi 3 HP, tối đa 100 HP. Size tối đa 2.6.
- Nạp tối đa 8 giây, sau đó tự nổ. Nhấn nhanh tạo vụ nổ nhỏ; bán kính và sát thương tăng mạnh theo đường cong thời gian nạp, Size và khoảng cách. Người nổ không bị sát thương bởi vụ nổ của chính mình; sau nổ Size về Base Size. Bom Size 1 vẫn gây sát thương ở cự ly gần.
- Shield hoạt động ngay trong 1.5 giây, chặn 100% sát thương nổ và phản 50% về người tấn công; cooldown 9 giây.
- Boost tăng tốc trong 0.9 giây, giảm Size 0.23 và rơi một Energy Orb, cooldown 5 giây.
- Đá là vật cản. Bụi không có collision: người chơi có thể đi xuyên vào để ẩn; đối thủ ở ngoài không nhìn thấy người trong bụi, còn người ở cùng bụi vẫn nhìn thấy nhau.
- Mỗi vòng spawn người chơi ngẫu nhiên, tránh đá/bụi và cách nhau tối thiểu 28 đơn vị. Server chỉ cho thấy đối thủ trong bán kính 26 đơn vị nên không lộ vị trí spawn ở xa.
- Nhân vật được dựng thành quả bóng có dải màu tương phản; phần bóng xoay theo quãng đường di chuyển để chuyển động lăn đọc được rõ ràng.
- Khi bị loại, player rơi Core. Người nhặt Core tăng Base Size 0.1, tối đa 1.4.
- Mất kết nối trong trận có 10 giây để kết nối lại bằng resume token. Quá hạn sẽ bị loại.

Các thông số được đặt ở `server/src/main/java/bombmebaby/GameRules.java`; ba layout map nằm trong `server/src/main/java/bombmebaby/GameMap.java`. Đây là giá trị prototype cụ thể hoá các khoảng gợi ý trong GDD.

## Giao thức và kiểm tra

Xem [docs/PROTOCOL.md](docs/PROTOCOL.md) để biết message JSON. Server không nhận vị trí, HP hoặc sát thương do client tự khai báo; client chỉ gửi hướng và hành động. Mỗi room được khoá riêng khi xử lý input và tick, bảo đảm một Core chỉ được một người nhặt.

Khi server đang chạy:

```sh
python3 scripts/network_smoke.py
```

Unity client hiển thị RTT, số message gửi/nhận và dùng nội suy vị trí giữa các snapshot. `ACK.processingMicros` là thời gian server xử lý một message; các chỉ số hiện chưa được ghi ra file để so sánh benchmark tự động.

## Giới hạn hiện tại

- Cần mở project bằng Unity để Unity tạo scene và kiểm tra build hình ảnh. Phần Unity không thể được chạy trong môi trường không có Unity Editor.
- Chưa có lưu lịch sử trận, bot, skin, matchmaking hay TLS. Baseline gửi snapshot đầy đủ ở 10 Hz; optimized bỏ geometry ở snapshot thường và lọc vật phẩm theo khoảng cách. Unity optimized và số liệu benchmark thực tế còn cần tích hợp/kiểm chứng, xem tài liệu bàn giao phần A.
- Không có cơ chế chống gian lận toàn diện; server đã giới hạn movement và bỏ qua sequence trùng/cũ.
