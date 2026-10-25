# Bomb Me Baby

Prototype game PvP arena 2.5D cho tối đa 10 người, xây theo GDD được cung cấp. Unity client hiển thị arena và gửi ý định điều khiển qua TCP; Java server giữ trạng thái chính thức của trận, chọn map, sinh vị trí người chơi, tính va chạm, nhặt vật phẩm, sát thương và người thắng.

## Cấu trúc

- `server/`: Java 17, TCP JSON theo từng dòng, game loop 20 Hz, snapshot 10 Hz, thread pool cho client.
- `LTM-Bomb-Me-Baby/`: project Unity 6.3 LTS (`6000.3.6f1`). Arena, camera, ánh sáng, vật cản và spawn point được lưu thành GameObject thật trong scene; player, pickup và VFX được sinh theo trạng thái mạng. Unity AI Assistant là công cụ Editor tùy chọn, không cần để chạy hoặc build game.
- `scripts/network_smoke.py`: kiểm tra kết nối thật với 2 client giả lập.
- `docs/GDD Bomb Me Baby.docx`: bản GDD gốc để nhóm đối chiếu trong quá trình phát triển.

## Kiến trúc và luồng mạng

```text
Unity clients (2–4 khi demo, tối đa 10)
        │  TCP lâu dài, JSON theo từng dòng
        │  HELLO / room action / MOVE / EXPLODE / PING
        ▼
Java authoritative server
  ├─ thread pool nhận và kiểm tra request
  ├─ khóa riêng theo room khi đổi trạng thái
  ├─ game loop 20 Hz
  └─ hàng đợi ghi riêng/client; gộp snapshot liền nhau, giữ thứ tự event gameplay
        │
        └─ STATE_UPDATE / ACK / PONG / GAME_OVER / JOURNEY_COMPLETE
```

Client chỉ gửi ý định điều khiển. Server kiểm tra sequence, tính movement, va chạm, khả năng nhìn thấy, sát thương, objective và người thắng. `baseline` gửi snapshot đầy đủ; `optimized` gửi geometry lúc bắt đầu/reconnect rồi lọc trạng thái động theo từng viewer.

## Chạy server

Cần JDK 17+ và Maven 3.9+.

```sh
cd server
mvn test
mvn exec:java -Dexec.args="7777"
```

Server mặc định nghe TCP port `7777`. Mở firewall cho port này nếu người chơi ở máy khác. Giao thức prototype là TCP thuần trong mạng tin cậy; chưa có đăng nhập hay TLS.

Từ PowerShell ở thư mục gốc, có thể dùng:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/run_server.ps1
```

Hoặc đóng gói một lần rồi chạy chỉ với Java 17+ (máy chạy không cần Maven):

```powershell
cd server
mvn test package
java -Dsync.mode=optimized -jar target/bomb-me-baby-server-1.0.0.jar 7777
```

## Tài liệu và kiểm thử phần Java server (thành viên A)

- [Học từ đầu và giải thích từng phần code](docs/THANH_VIEN_A_HUONG_DAN.md).
- [Kết quả kiểm chứng mới nhất](docs/FINAL_VALIDATION.md), [mốc kiểm thử trước đây](docs/THANH_VIEN_A_KIEM_THU.md).
- [Bàn giao cho T/B: tích hợp JSON và so sánh baseline/optimized](docs/THANH_VIEN_A_BAN_GIAO.md).

Server mặc định dùng baseline. Từ thư mục `server/`, kiểm tra cả hai cấu hình:

```powershell
mvn clean test "-Dsync.mode=baseline"
mvn test "-Dsync.mode=optimized"
```

Chạy server bằng `mvn compile exec:java "-Dexec.args=7777" "-Dsync.mode=baseline"`. Unity có nút tạo phòng Đấu nhanh và Chiếm Mảnh Tim Lửa. Chế độ `optimized` gửi geometry một lần ở GAME_START và lọc snapshot theo viewer; đây là đồng bộ chọn lọc, chưa phải delta từng trường giữa hai snapshot.

Để khóa map cho buổi demo Đấu nhanh, thêm `"-Ddemo.map=meadow"` (hoặc `crossroads`, `crater`). Khi deploy có thể dùng biến môi trường `DEMO_MAP`. Server gửi `syncMode` và `demoMap` trong `WELCOME`; nhấn **F3** trong trận để xem endpoint, cấu hình đồng bộ và map demo đang dùng.

> Bản Unity build chỉ là **client**, không chứa Java server. `127.0.0.1` luôn là máy đang chạy game: nếu gửi build cho người khác mà giữ IP này, game sẽ tìm server trên máy của họ và kết nối thất bại.

Để máy khác kết nối:

- Cùng Wi-Fi/LAN: chạy server trên một máy, nhập IPv4 LAN của máy đó (ví dụ `192.168.1.20`) trong game và cho phép TCP `7777` qua firewall.
- Khác mạng Internet: chạy server trên VPS có IP/domain public, hoặc cấu hình port forwarding TCP `7777` trên router tới máy chạy server. Không dùng IP dạng `192.168.x.x` qua Internet.
- Có thể đặt một file `server.txt` cạnh file `.exe`, dòng đầu là `HOST:PORT` (ví dụ `game.example.com:7777`), hoặc chạy `BombMeBaby.exe --server=HOST:PORT`. Client cũng ghi nhớ địa chỉ đã nhập.

Kiểm tra từ máy người chơi trước khi mở game (PowerShell):

```powershell
Test-NetConnection HOST -Port 7777
```

`TcpTestSucceeded: False` nghĩa là lỗi nằm ở việc server chưa chạy, IP/port sai, firewall hoặc port forwarding; build Unity chưa thể tự khắc phục các phần mạng này.

## Chạy Unity client

1. Cài Unity `6000.3.6f1` bằng Unity Hub, sau đó mở thư mục `LTM-Bomb-Me-Baby/` và chờ Unity import xong.
2. Mở `Assets/Scenes/Main.unity`. Nếu scene chưa có hierarchy `Bomb Me Baby`, chọn **Tools > Bomb Me Baby > Rebuild Main Scene**.
3. Nhấn Play, nhập tên rồi chọn **Chơi**. Client dùng server đã lưu hoặc được cấu hình bằng `server.txt`; IP/port chỉ nằm trong **Cài đặt mạng nâng cao** dành cho chủ phòng và kiểm thử.
4. Chọn chế độ rồi bấm **Tìm trận**: server ghép ngẫu nhiên những người cùng tìm chế độ đó (2–4 người) và vào trận ngay. Muốn chơi riêng với bạn bè thì một người chọn **Tạo phòng riêng**, những người còn lại nhập mã 6 ký tự và **Vào phòng**.
5. Trong phòng riêng, chọn **Sẵn sàng**; trận bắt đầu khi có ít nhất 2 người và tất cả cùng sẵn sàng (tối đa 10 người). Để chơi nhiều client trên cùng máy, build một bản Windows/Mac rồi chạy cùng lúc với Unity Editor, hoặc chạy nhiều bản build.

Khi người chơi tạo hoặc vào phòng **Chiếm Mảnh Tim Lửa** lần đầu trong phiên, client phát phim mở đầu `Assets/StreamingAssets/story_intro.mp4` với phụ đề tiếng Việt; nhấn **Esc** hoặc **Space** để bỏ qua. Nếu máy không giải mã được MP4, client tự chuyển sang ba khung hình minh hoạ trong `Assets/Resources/Menu/`. Có thể thay video bằng bản dựng H.264 1920×1080 khác, giữ nguyên tên file để không phải sửa code.

Điều khiển: **WASD/phím mũi tên** di chuyển theo hướng màn hình, **giữ Space** nạp và **thả Space** để nổ, **E** bật Shield, **Shift** Boost. Trong Hành Trình Tim Lửa, người đang cầm mảnh không thể nạp/nổ bom và nhấn **Q** để chủ động thả; đối thủ đứng trong bệ và **giữ F** để cướp. Camera bám sát quả bóng của người chơi và chỉ hiển thị một vùng nhỏ của map; sau khi chết tự theo người còn sống, dùng góc nhìn của người đó. Đấu nhanh tối đa 3 phút. Hành trình gồm 3 chương/map; mỗi chương cố định tối đa 6 phút. Người đặt mảnh lên bệ và giữ không bị cướp trong 30 giây thắng chương ngay.

Game có nhạc riêng cho menu và ba map. Nhạc tự hạ khi có tiếng nổ/kết quả; dùng pool 20 giọng SFX với âm lượng/kênh trái phải theo vị trí người chơi để hạn chế chồng âm. Khi mất kết nối trong trận, client hiện cửa sổ reconnect 10 giây và số lần thử lại. Người còn trong phòng thấy tên người mất kết nối và thời gian giữ chỗ; vị trí đang ẩn trong bụi không bị lộ.

**Phòng riêng và tìm nhanh:** người khác bấm Tìm trận **không vào phòng riêng bạn đã tạo**. Gửi mã phòng cho họ; hoặc cả hai cùng bấm Tìm trận ở cùng mode. Server ghép ở tick kế tiếp khi có ít nhất 2 người đang tìm.

## Build Windows

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build_client.ps1 -Isolated
```

Output: `builds/windows/BombMeBaby.exe`. Build và validation dùng chung `scripts/sync_unity_project.ps1`: đồng bộ cả việc xóa file thừa trong Assets/Packages/ProjectSettings của bản sao, giữ cache Library. Không chạy hai tác vụ đồng thời trên cùng bản sao. `-Isolated` build trong bản sao để bạn có thể giữ Editor đang mở; bỏ cờ nếu build trực tiếp project đã đóng. Truyền `-Unity` nếu Editor nằm ở đường dẫn khác. Trong Editor có **Tools > Bomb Me Baby > Build Windows**. Gửi cả thư mục Windows, không chỉ `.exe`.

## Bật AI trong Unity Editor (tùy chọn)

Project đang khai báo package preview `com.unity.ai.assistant`. Package này chỉ phục vụ hỗ trợ trong Editor và không thuộc luồng chạy game. Nếu cần dùng:

1. Đăng nhập Unity ID trong Unity Hub và liên kết project với một Unity Cloud project.
2. Nếu nút **AI** chưa hiện, nhấp chuột phải vùng trống trên toolbar rồi chọn **Services > AI**.
3. Mở **AI**, chấp nhận điều khoản và để Unity hoàn tất cài đặt/kích hoạt package.
4. Chọn **Open Assistant** để hỏi, sinh hoặc review code C# ngay trong Editor.

Unity AI Assistant cần kết nối mạng và quyền sử dụng/trial hoặc credits trên tài khoản Unity. Người kiểm tra source không cần kích hoạt AI để mở scene, chạy game hoặc build player.

## Luật prototype

- 100 HP, không hồi sinh trong vòng; người cuối cùng sống thắng sớm. Đấu nhanh hòa nếu hết 180 giây còn nhiều người. Chiếm Mảnh kết thúc sau 300 giây nếu chưa ai giữ bệ đủ 30 giây. Energy tăng Size 0.12, hồi 1 HP và cho boost tốc độ 12% trong 0.65 giây. Size tối đa 2.6.
- Nạp tối đa 4 giây, sau đó tự nổ. Sau mỗi lần nổ, bom có cooldown 2.5 giây do server kiểm soát để không thể spam. Bom Size 1 charge đầy gây đúng 80 damage ở tâm nổ; damage nhân theo Size rồi giảm theo khoảng cách. Người nổ không bị sát thương bởi vụ nổ của chính mình; sau nổ Size về Base Size và phần Size đã tiêu hao rơi thành các mảnh Energy quanh vị trí nổ.
- Shield hoạt động ngay trong 1.5 giây, chặn 100% sát thương nổ và phản 50% về người tấn công; cooldown 9 giây.
- Boost tăng tốc trong 0.9 giây, tiêu hao tối đa 0.23 Size nhưng không thấp hơn Base Size; chỉ rơi một Energy Orb nếu tiêu hao ít nhất 0.12. Cooldown 5 giây; ở Base Size vẫn tăng tốc được.
- Đá là vật cản. Bụi không có collision: người chơi có thể đi xuyên vào để ẩn; đối thủ ở ngoài không nhìn thấy người trong bụi, còn người ở cùng bụi vẫn nhìn thấy nhau.
- Mỗi vòng spawn người chơi ngẫu nhiên, tránh đá/bụi và cách nhau tối thiểu 28 đơn vị. Server chỉ cho thấy đối thủ trong bán kính 26 đơn vị nên không lộ vị trí spawn ở xa.
- Nhân vật được dựng thành quả bóng có dải màu tương phản; phần bóng xoay theo quãng đường di chuyển để chuyển động lăn đọc được rõ ràng.
- Khi bị loại, player rơi Core. Người nhặt Core tăng Base Size 0.1, tối đa 1.4.
- Mất kết nối trong trận có 10 giây để kết nối lại bằng resume token. Quá hạn sẽ bị loại.

Thông số chiến đấu ở `server/src/main/java/bombmebaby/GameRules.java`, thời hạn Đấu nhanh ở `GameRoom.java`, luật Tim Lửa ở `ShardObjective.java`; ba layout map nằm trong `server/src/main/java/bombmebaby/GameMap.java`. Đây là giá trị prototype cụ thể hoá các khoảng gợi ý trong GDD.

## Giao thức và kiểm tra

Xem [docs/PROTOCOL.md](docs/PROTOCOL.md) để biết message JSON. Server không nhận vị trí, HP hoặc sát thương do client tự khai báo; client chỉ gửi hướng và hành động. Mỗi room được khoá riêng khi xử lý input và tick, bảo đảm một Core chỉ được một người nhặt.

Khi server đang chạy:

```sh
python3 scripts/network_smoke.py
```

Unity client hiển thị RTT, số message gửi/nhận và dùng nội suy vị trí giữa các snapshot. `ACK.processingMicros` là thời gian server xử lý một message. `scripts/load_test.py` và `scripts/run_benchmark.ps1` ghi dữ liệu CSV để so sánh tự động; kết quả và giới hạn phép đo nằm trong [docs/EXPERIMENTS.md](docs/EXPERIMENTS.md).

Lệnh chạy toàn bộ kiểm tra C#/Unity, staging và build được ghi tại [FINAL_VALIDATION.md](docs/FINAL_VALIDATION.md).

## Điểm cải tiến và kết quả thực nghiệm

Contribution kỹ thuật chính là **selective state synchronization**:

- geometry tĩnh được gửi một lần ở `GAME_START` hoặc khi reconnect, thay vì lặp lại trong mọi snapshot;
- player trong bụi/ở xa và pickup ngoài phạm vi được lọc theo từng viewer;
- event nổ không gửi tọa độ nguồn cho viewer không được phép nhìn thấy.

Phép đo mới ngày 22/10 gồm 18 lượt × 30 giây, cố định map meadow, 2/4/8 client giả lập: `optimized` giảm khoảng **62–78% byte nhận/client/s**, không có lỗi kết nối. Xem [EXPERIMENTS_FINAL.md](docs/EXPERIMENTS_FINAL.md). Đây là bản đo ngày 22/10 theo fingerprint trong báo cáo, không phải phép đo lại source 23/10. Kết quả chưa chứng minh giảm RTT hoặc hiệu năng LAN/Internet. Mốc cũ 19/10 (66–79%, map ngẫu nhiên) vẫn được giữ riêng trong [EXPERIMENTS.md](docs/EXPERIMENTS.md).

Trạng thái kiểm tra ngày 24/10/2026:

- Windows player đã build bằng Unity `6000.3.6f1`; 16 clip SFX và 4 track nhạc đang dùng, nhạc sảnh giữ nguyên;
- 12 kiểm tra C# và 111 kiểm tra Unity Play Mode đã qua, gồm một client Unity kết nối Java thật cùng một peer TCP;
- test Java, lỗi đã sửa và phạm vi kiểm chứng xem [FINAL_VALIDATION.md](docs/FINAL_VALIDATION.md);
- 107 test Java đã qua ở cả hai chế độ; kiểm tra staging qua. Đã sửa hủy/tìm lại lúc ghép trận, người mới vào chờ lượt sau và audio đối thủ ẩn. Benchmark 18 lượt thuộc bản ngày 22/10, chưa đo lại sau thay đổi gameplay/protocol 23–24/10.

## Giới hạn hiện tại

- `builds/` bị Git ignore; clone source cần chạy script build. Vẫn cần kiểm tra máy sạch và nghe/chơi bằng người thật trước khi nộp.
- Có matchmaking. Chưa có lưu lịch sử trận, skin hoặc TLS; server dùng TCP thuần cho demo/mạng tin cậy.
- Benchmark mới vẫn dùng loopback và client giả lập; không thay thế đo LAN, FPS/GC hoặc đánh giá trải nghiệm người chơi.
- Không có cơ chế chống gian lận toàn diện; server đã giới hạn movement và bỏ qua sequence trùng/cũ.

## Tài liệu và nguồn tham khảo

- [Giao thức TCP JSON](docs/PROTOCOL.md)
- [Thiết lập và kết quả thực nghiệm](docs/EXPERIMENTS.md)
- [Kết quả kiểm thử server](docs/THANH_VIEN_A_KIEM_THU.md)
- [Kịch bản Chiếm Mảnh Tim Lửa](docs/STORY_SCRIPT.md)
- [Nguồn và giấy phép âm thanh](docs/AUDIO_CREDITS.md)
- [Unity Manual](https://docs.unity3d.com/Manual/index.html), [Java networking API](https://docs.oracle.com/en/java/javase/17/docs/api/java.base/java/net/package-summary.html), [Gson](https://github.com/google/gson) và [JUnit 5](https://junit.org/junit5/)
