# Kịch bản và luồng Hành trình Ba Mảnh

Cập nhật 23/10/2026 theo `GameRoom.java`, `ShardObjective.java`, `BombGame.cs` và `Assets/StreamingAssets/story.json`. Đây là mô tả luồng hiện tại; lời hiển thị thực tế được đọc từ JSON hoặc dựng trong client theo trạng thái server.

## Cốt truyện

Đảo Bom Bé sống nhờ Tim Lửa. Khi Tim Lửa vỡ, ba mảnh rơi xuống Đồng cỏ rộng, Ngã tư bỏ hoang và Miệng núi lửa. Các Bom Bé tranh giành mảnh, đưa về bệ trung tâm và giữ đến hết chương. Energy làm nhân vật lớn hơn nhưng chậm hơn; nổ tiêu hao Size, Shield phản sát thương và Boost đổi năng lượng lấy tốc độ.

Tên Mảnh Mầm, Mảnh Đường, Mảnh Nhiệt và nhân vật Bà Giữ Lò thuộc ý tưởng nội dung ban đầu. Không coi mọi câu thoại trong bản thiết kế cũ là lời đã được phát trong game. Kết quả hiện tại phân biệt người giữ nhiều mảnh nhất với trường hợp một người mang về đủ cả ba mảnh (“Tim Lửa Trọn Vẹn”).

## Luồng hiện tại

```text
Chọn Hành trình → Tìm trận hoặc tạo/vào phòng riêng
→ Chương 1: meadow → Kết quả / Ready
→ Chương 2: crossroads → Kết quả / Ready
→ Chương 3: crater → Kết quả chung cuộc → Rời phòng
```

- Phòng riêng cần ít nhất hai người và tất cả sẵn sàng; tìm trận đủ người được server bắt đầu tự động.
- Lần đầu vào Hành trình trong phiên, client phát phim mở đầu; Esc/Space bỏ qua. Nếu không giải mã được video, có ba ảnh dự phòng.
- Mỗi chương bắt đầu với 100 HP, Size/Base Size và cooldown đặt lại; không hồi sinh trong chương. Người bị loại trở lại ở chương sau nếu vẫn ở trong phòng.
- Giữa chương có bảng điểm và Ready. Sau khi kết thúc Hành trình, rời phòng để tạo/tìm hành trình mới; không có vòng thứ tư.
- Phòng PLAYING không nhận người mới. Hành trình đã bắt đầu cũng không nhận người thay thế giữa các chương; reconnect chỉ khôi phục người thuộc phiên cũ trong 10 giây.
- Còn dưới hai người tham gia sau thời gian giữ chỗ thì kết thúc sớm, giữ thành tích đã đạt. Người đã bị loại nhưng vẫn kết nối được tính là người tham gia chương sau. Không cộng mảnh của chương chưa diễn ra.
- Người bị loại ngừng gửi input chiến đấu; camera/audio tự theo người còn sống. Dữ liệu optimized giữ phạm vi nhìn và luật bụi của mục tiêu quan sát.

## Ba chương

| Chương | Map | Mục tiêu và gợi ý |
| --- | --- | --- |
| 1 | `meadow` — Đồng cỏ rộng | Tìm Energy, làm quen bụi che khuất và mang mảnh về bệ. |
| 2 | `crossroads` — Ngã tư bỏ hoang | Cẩn thận nhiều hướng tiếp cận, giữ Shield khi qua chỗ trống. |
| 3 | `crater` — Miệng núi lửa | Tranh mảnh cuối; Boost giúp thoát tầm nổ nhưng tiêu hao Size. |

Layout và kích thước thực tế nằm trong `GameMap.java`. Hiệu ứng môi trường không gây sát thương riêng.

## Mảnh Tim Lửa và phá ấn

- Mảnh xuất hiện sau 10 giây ở xa bệ. Chạm để nhặt, mang về vòng bệ bán kính 3 đơn vị để đặt.
- Người cầm mảnh được nhìn thấy công khai, không thể nạp/nổ bom; Q chủ động thả. Hai nấc Khóa Lửa bảo vệ mảnh; chi tiết ngưỡng damage/charge, bảo hộ và hồi khóa theo [PROTOCOL.md](PROTOCOL.md).
- Sau khi đặt, bệ được bảo hộ 5 giây. Đối thủ đứng trong vòng tròn quanh bệ để phá ấn trong 3 giây, hoặc 6 giây khi chủ mảnh đang ở trong vòng. Mỗi người có tiến độ riêng.
- “Vừa vào vòng” chỉ là nhân vật vừa bước vào vùng bệ, không phải mới tham gia trận. Không cộng thời gian trước khi vào; tối đa 100 ms tiến độ mỗi tick để server khựng không làm vỡ ấn ngay.
- Damage thực tế, kể cả phản khiên, reset tiến độ người bị thương và có thể gây stagger. Rời vòng, bị loại hoặc mất kết nối cũng xóa tiến độ riêng. Khiên chặn damage trực tiếp thì không bị reset bởi hit đó.
- Phá ấn thành công làm mảnh bật khỏi bệ, người phá có ưu tiên nhặt ngắn; bệ quá nhiệt 4 giây.
- Chương tối đa 180 giây theo thời gian thực. Chủ bệ, hoặc người cầm nếu mảnh chưa đặt, thắng khi hết giờ. Không ai giữ thì hòa chương. Còn một người sống thì người đó thắng sớm.

## Điểm và kết quả

- Thắng chương: một mảnh (`roundWins + 1`) và 3 điểm. Hạ đối thủ bằng nổ hoặc phản khiên: `eliminations + 1`, thêm 1 điểm. Rời phòng/hết hạn reconnect không cho điểm hạ đối thủ.
- Xếp hạng: số mảnh → số lần hạ → người thắng chương 3 nếu thuộc nhóm hòa → đồng thắng. Tổng điểm là thống kê, không quyết định thứ hạng.
- Kết thúc sớm không dùng ưu tiên thắng chương 3 nếu chương đó chưa diễn ra; thành tích đã kiếm được của người rời phòng vẫn nằm trong bảng tổng.
- Chương hòa vẫn chuyển sang chương sau khi đủ người Ready; không chơi lại chương đó.
- `JOURNEY_COMPLETE.reason` phân biệt `COMPLETED` và `INSUFFICIENT_PLAYERS`. UI nói rõ kết thúc sớm và không mời Ready tiếp.

## Nguồn nội dung và kiểm chứng

`story.json` chứa lời dẫn map và event như nhặt Energy/Core, nhận damage, phản khiên, bật khiên/Boost và bị loại. `BombGame.cs` dựng thông báo objective, trạng thái phòng và kết quả; `BombStoryCinematic.cs` xử lý phần mở đầu. Số lần hạ riêng vòng nằm trong `PlayerData.kills`; tổng Hành trình nằm trong `ScoreData.eliminations`.

Kiểm chứng tự động và giới hạn tại [FINAL_VALIDATION.md](FINAL_VALIDATION.md). Dùng [PLAYTEST_MAPS.md](PLAYTEST_MAPS.md) để ghi trải nghiệm thật; không điền kết quả dự kiến thành kết quả đã chơi.
