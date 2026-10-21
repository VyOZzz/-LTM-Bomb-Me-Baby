# Đề xuất cốt truyện và chế độ chơi — Bomb Me Baby

Trạng thái: cập nhật 21/10/2026. **Hành trình Ba Mảnh đã được triển khai ở Java server, JSON protocol và Unity**, gồm objective, ba chương, điểm, lời dẫn, phim/ảnh mở đầu, HUD/VFX và SFX. Nhạc riêng theo chương và kiểm thử end-to-end 2–4 Windows client trên build cuối vẫn chưa hoàn tất. Đội 2v2 vẫn chỉ là đề xuất.

## Ý tưởng chính

**Tên tuyến truyện:** Ba Mảnh Tim Lửa.

Đảo Bom Bé sống nhờ **Tim Lửa**, nguồn năng lượng giữ cho mọi vùng đất sáng và ổn định. Trong Lễ Hội Tia Lửa, Tim Lửa quá tải và vỡ thành ba mảnh, rơi xuống Đồng cỏ rộng, Ngã tư bỏ hoang và Miệng núi lửa. Những Bom Bé tranh tài để chứng minh ai điều khiển năng lượng giỏi nhất và đủ sức mang các mảnh lõi về lò trung tâm.

Energy làm cơ thể lớn và mạnh hơn nhưng chậm hơn. Charge tích năng lượng; Explosion giải phóng nó nên Bom Bé trở về Base Size. Shield là vỏ bảo vệ có thể phản lại luồng nổ. Boost đốt một phần năng lượng để lao nhanh. Khi một Bom Bé bị loại khỏi cuộc thi, Core của họ rơi ra để người còn lại tranh giành. Người thắng cả hành trình khôi phục Tim Lửa.

Giọng điệu: vui, cạnh tranh, hơi bí ẩn; nhân vật bị **loại khỏi trận**, không chết. Cốt truyện kể bằng 1–2 câu trước/sau mỗi chương và hình nền/UI ngắn, không cần cutscene dài.

## Ba chương dùng ba map đang có

| Chương | Map hiện có | Vai trò trong chuyện | Nhịp chơi và hình/tiếng |
| --- | --- | --- | --- |
| 1 — Mảnh Mầm | `meadow` — Đồng cỏ rộng | Phát hiện mảnh lõi đầu tiên giữa tàn tích lễ hội. | Ánh sáng ấm, cỏ xanh, nhạc tò mò/vui; không gian mở để làm quen Energy, Charge và bụi ẩn nấp. |
| 2 — Mảnh Đường | `crossroads` — Ngã tư bỏ hoang | Dấu vết dẫn đến trạm dẫn năng lượng cũ. | Chạng vạng tím/xanh, âm thanh cơ khí nhẹ; nhiều hướng tiếp cận, khuyến khích Boost và Shield đúng lúc. |
| 3 — Mảnh Nhiệt | `crater` — Miệng núi lửa | Mảnh lớn nhất nằm sát nơi Tim Lửa nổ vỡ. | Ánh cam/đỏ và âm nhạc căng hơn; hiệu ứng môi trường không được che người chơi hoặc gây sát thương khi server chưa có luật tương ứng. |

**Lời dẫn ngắn có thể dùng trong UI:**

- Mở đầu: “Tim Lửa đã vỡ. Ba mảnh lõi đang làm Đảo Bom Bé mất kiểm soát. Ai sẽ đưa ánh sáng trở lại?”
- Chương 1: “Ánh xanh cuối cùng dẫn chúng ta đến Đồng cỏ rộng.”
- Chương 2: “Dòng năng lượng chạy qua Ngã tư bỏ hoang. Mảnh thứ hai ở gần đây.”
- Chương 3: “Miệng núi lửa vẫn còn nóng. Trận cuối sẽ quyết định người giữ Tim Lửa.”
- Kết: “Ba mảnh lõi đã trở về. Đảo Bom Bé lại sáng — và một nhà vô địch mới đã xuất hiện.”

## Chế độ chơi

### 1. Đấu nhanh — ưu tiên hoàn thiện trước 20/10

- 2–4 người chơi thật cho demo; server hiện cho phép tối đa 10.
- Một trận PvP tự do trên một map; người sống cuối cùng thắng.
- Dùng luật và luồng phòng hiện tại: tạo/vào phòng → Ready → trận → kết quả.
- HUD, lời dẫn, nhạc/SFX, VFX và banner reconnect đã có; cần build Windows và kiểm thử end-to-end 2–4 client thật.

### 2. Hành trình Ba Mảnh — đã có logic server và UI cơ bản

- 2–4 người trong cùng phòng chơi **ba vòng theo thứ tự** `meadow → crossroads → crater`.
- Sau mỗi vòng, người bị loại trở lại ở vòng sau. Mỗi người thắng vòng được **3 điểm**, mỗi lần loại đối thủ được **1 điểm**. Hết ba vòng, tổng điểm cao nhất thắng hành trình; hòa điểm thì xét số vòng thắng, sau đó là người thắng vòng cuối. Nếu vẫn bằng nhau, công bố đồng hạng.
- Giữa các vòng: màn kể chuyện ngắn, bảng điểm và nút Ready. Vòng mới đặt lại HP/Size/cooldown như hiện tại; **điểm hành trình** được giữ trên server.
- Server chọn map, tính điểm và phát kết quả; client chỉ hiển thị. `mode`, `chapterIndex`, `scores` đã có trong trạng thái và `JOURNEY_COMPLETE` kết thúc hành trình. Xem `docs/PROTOCOL.md`.

### 3. Đội 2v2 — chỉ làm sau hai chế độ trên

- Hai đội tranh tài trên một map; đội còn người sống cuối cùng thắng.
- Cần thêm team assignment, luật tránh gây sát thương đồng đội, trạng thái đội và test. Đây là tính năng mới đáng kể, không đưa vào demo nếu chưa kiểm chứng trọn vẹn.

## Vào game và trạng thái online

Hiện Unity gửi `HELLO` với tên và `resumeToken`; server tạo/khôi phục session rồi trả `WELCOME`. Đây là **vào game bằng tên/guest session**, chưa phải tài khoản có mật khẩu. Sau khi nhận `WELCOME`, client có thể hiện “Đã online”; trong phòng, `ROOM_STATE` đã cho biết ai kết nối.

Nếu muốn danh sách online toàn hệ thống, bổ sung `ONLINE_LIST` khi vào game và `PLAYER_ONLINE`/`PLAYER_OFFLINE` khi trạng thái đổi. Server là nguồn trạng thái chính thức; khi mất kết nối cần áp dụng thời gian reconnect 10 giây trước khi báo offline thật. Nếu muốn đăng nhập tài khoản lâu dài, cần thiết kế xác thực và lưu dữ liệu riêng; việc đó nằm ngoài hai chế độ ưu tiên ở trên.

## Ranh giới kỹ thuật và lịch

- Đến **20/10:** hoàn thiện Đấu nhanh, chọn map/hiển thị chuyện mở đầu đơn giản, có ít nhất một trận 2–4 Unity client chạy thật.
- Hành trình đã có thứ tự map, điểm server, event/UI cơ bản và test nhiều vòng. Việc tiếp theo là playtest 2–4 Unity client, hoàn thiện lời dẫn/VFX và sửa lỗi tích hợp nếu có.
- Cốt truyện, art và chế độ chơi giúp phần chức năng/demo. Phần **Novelty / Contributions** của môn Lập trình mạng vẫn cần cải tiến kỹ thuật riêng và số liệu thực nghiệm, như kế hoạch trong `docs/TEAM_PLAN.md`.
