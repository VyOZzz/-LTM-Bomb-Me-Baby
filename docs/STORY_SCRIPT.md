# Kịch bản màn chơi — Hành trình Ba Mảnh

Trạng thái: **kịch bản thiết kế**, viết ngày 15/10/2026. Đây là kịch bản chi tiết cho chế độ **Hành trình Ba Mảnh** đã đề xuất trong `docs/GAME_STORY_MODES.md`. Chế độ này **chưa được triển khai**. Phần mô tả bản đồ dưới đây lấy theo layout có thật trong `server/src/main/java/bombmebaby/GameMap.java`. Các con số luật chơi lấy theo `GameRules.java`.

## 1. Giọng kể và người dẫn chuyện

- **Người dẫn chuyện:** **Bà Giữ Lò**, người trông coi lò trung tâm, nơi Tim Lửa từng cháy. Bà nói ngắn, ấm áp, hơi tinh nghịch và gọi người chơi là "các Bom Bé".
- **Nhân vật người chơi:** các Bom Bé, những quả bom tròn biết lăn, mỗi bé có một dải màu riêng.
- **Quy tắc giọng điệu:**
  - Vui và cạnh tranh nhưng không bạo lực. Người thua bị **loại khỏi vòng**, không "chết".
  - Mỗi đoạn lời dẫn tối đa 2 câu để vừa một khung UI.
  - Lời dẫn không được che màn hình khi trận đang diễn ra. Trong trận chỉ dùng thông báo ngắn ở góc màn hình.

## 2. Luồng của một hành trình

```text
Phòng chờ ─► Mở đầu ─► Chương 1 (meadow) ─► Bảng điểm ─► Chương 2 (crossroads)
         ─► Bảng điểm ─► Chương 3 (crater) ─► Kết hành trình ─► Về phòng chờ
```

- Mỗi chương là **một vòng PvP** theo luật hiện có: 100 HP, không hồi sinh trong vòng, người sống cuối cùng thắng vòng.
- Đầu mỗi vòng, HP, Size, Base Size và cooldown được đặt lại như hiện tại. Điểm hành trình được giữ lại qua các vòng.
- **Tính điểm:** thắng vòng được **3 điểm**, mỗi lần hạ một đối thủ được **1 điểm**.
- **Xếp hạng khi bằng điểm:** xét số vòng thắng, rồi xét người thắng chương 3. Nếu vẫn bằng thì công bố đồng hạng.
- Người bị loại ở vòng trước vẫn chơi tiếp ở vòng sau.

## 3. Mở đầu (màn hình trước chương 1)

**Hình nền:** lò trung tâm tắt lịm, ba vệt sáng bay về ba hướng.

> **Bà Giữ Lò:** "Tim Lửa đã vỡ. Ba mảnh lõi đang làm Đảo Bom Bé mất kiểm soát."
>
> **Bà Giữ Lò:** "Ai mang được ánh sáng trở lại sẽ là người giữ Tim Lửa. Các Bom Bé, sẵn sàng chưa?"

**Nút:** `Sẵn sàng`. Hành trình bắt đầu khi tất cả người trong phòng đã sẵn sàng.

## 4. Chương 1 — Mảnh Mầm (`meadow`, Đồng cỏ rộng)

### Bối cảnh

Mảnh lõi đầu tiên rơi xuống đồng cỏ, nơi Lễ Hội Tia Lửa vừa diễn ra. Cờ lễ hội còn vương trên cỏ, những hòn đá rải rác như bàn ghế bị bỏ lại.

### Bản đồ theo dữ liệu thật

- Kích thước 96×96 (`halfSize` 48).
- 12 hòn đá nhỏ rải đều khắp map.
- 14 bụi cỏ: phần lớn nằm dọc mép map, cộng thêm 3 bụi ở gần giữa, tại khoảng (-17, 11), (1, -15) và (12, 19).
- Map thoáng, nhiều đường đi nên phù hợp để làm quen luật chơi.

### Mục tiêu hiển thị

"Nhặt Energy để lớn lên — người cuối cùng còn trên đồng cỏ giữ được Mảnh Mầm."

### Lời dẫn trước trận

> **Bà Giữ Lò:** "Ánh xanh cuối cùng dẫn chúng ta đến Đồng cỏ rộng."
>
> **Bà Giữ Lò:** "Ăn thật no năng lượng, nhưng nhớ là càng to thì càng lăn chậm đấy!"

### Gợi ý trong trận (hiện lần lượt, mỗi gợi ý một lần)

| Thời điểm | Gợi ý |
| --- | --- |
| Sau 5 giây | "Nhặt các quả Energy: +0.12 Size và hồi 3 HP." |
| Sau 20 giây | "Giữ Space để nạp, thả ra để nổ. Nạp lâu thì nổ to — quá 8 giây sẽ tự nổ!" |
| Khi người chơi bước vào bụi lần đầu | "Trong bụi, đối thủ ở ngoài không nhìn thấy bạn." |

### Lời kết vòng

- **Người thắng:** "Mảnh Mầm đã sáng lên trong tay bạn. Một khởi đầu xuất sắc!"
- **Người thua:** "Đồng cỏ hôm nay chưa chọn bạn. Còn hai mảnh nữa!"

### Màn chuyển tiếp

> **Bà Giữ Lò:** "Mảnh Mầm vừa ấm lên đã chỉ về phía tây — nơi có những con đường cũ."

## 5. Chương 2 — Mảnh Đường (`crossroads`, Ngã tư bỏ hoang)

### Bối cảnh

Dòng năng lượng chạy dọc những con đường đá của một trạm dẫn năng lượng cũ. Bốn ngả đường gặp nhau ở giữa và mảnh thứ hai đang ở đâu đó quanh đây.

### Bản đồ theo dữ liệu thật

- Kích thước 100×100 (`halfSize` 50).
- 12 hòn đá xếp thành **hai đường chéo hình chữ X**, giống những cột trụ dẫn năng lượng. Ngay tâm map để trống.
- 16 bụi cỏ: phần lớn nằm dọc mép, cộng thêm 4 bụi ở giữa bốn cạnh của chữ X, tại (±25, 0) và (0, ±25).
- Các hàng đá chia map thành bốn khu. Người chơi dễ bị tấn công từ nhiều hướng nên cần Boost và Shield.

### Mục tiêu hiển thị

"Giành quyền kiểm soát ngã tư — người cuối cùng đứng vững giữ được Mảnh Đường."

### Lời dẫn trước trận

> **Bà Giữ Lò:** "Dòng năng lượng chạy qua Ngã tư bỏ hoang. Mảnh thứ hai ở gần đây."
>
> **Bà Giữ Lò:** "Ở đây nguy hiểm có thể đến từ bốn phía. Giữ vỏ chắn sẵn sàng nhé!"

### Gợi ý trong trận

| Thời điểm | Gợi ý |
| --- | --- |
| Sau 5 giây | "Nhấn E: Shield chặn toàn bộ sát thương nổ trong 1.5 giây và phản lại một nửa." |
| Sau 20 giây | "Nhấn Shift: Boost giúp lao nhanh, nhưng bạn sẽ nhỏ đi và đánh rơi một quả Energy." |
| Khi người chơi đứng gần tâm map | "Tâm ngã tư rất trống trải — ai cũng nhìn thấy bạn!" |

### Lời kết vòng

- **Người thắng:** "Ngã tư đã thuộc về bạn. Mảnh Đường rung lên trong tay!"
- **Người thua:** "Bị dồn ở ngã tư rồi. Trận cuối vẫn còn đó!"

### Màn chuyển tiếp

Màn chuyển tiếp hiện sau bảng điểm.

> **Bà Giữ Lò:** "Hai mảnh đã tìm thấy nhau. Giờ chúng chỉ thẳng về miệng núi lửa…"

## 6. Chương 3 — Mảnh Nhiệt (`crater`, Miệng núi lửa)

### Bối cảnh

Đây là nơi Tim Lửa phát nổ. Mảnh lớn nhất nằm trong lòng miệng núi lửa, xung quanh là vành đá nóng.

### Bản đồ theo dữ liệu thật

- Map lớn nhất: 104×104 (`halfSize` 52).
- 6 hòn đá lớn (bán kính 2.0–2.4) tạo thành **vành miệng núi lửa** quanh tâm, cách tâm khoảng 13–17 đơn vị. Giữa các hòn đá có khe hở để lọt vào trong.
- 6 hòn đá nhỏ hơn rải ở vùng ngoài.
- 15 bụi cỏ chia thành hai vòng: một vòng dọc mép map, một vòng ở giữa cách tâm khoảng 25–32 đơn vị.
- Lối chơi chính: tranh giành lòng chảo ở giữa và phục kích qua các khe đá.

### Mục tiêu hiển thị

"Trận cuối — người còn đứng vững trong miệng núi lửa sẽ mang Mảnh Nhiệt về lò."

### Lời dẫn trước trận

> **Bà Giữ Lò:** "Miệng núi lửa vẫn còn nóng. Trận cuối sẽ quyết định người giữ Tim Lửa."
>
> **Bà Giữ Lò:** "Bảng điểm chưa khép lại đâu — mỗi lần hạ đối thủ vẫn đáng giá!"

### Gợi ý trong trận

| Thời điểm | Gợi ý |
| --- | --- |
| Sau 5 giây | "Nhặt Core của người bị loại để tăng Base Size đến hết vòng này." |
| Khi người chơi ở trong vành đá | "Lòng chảo kín đáo nhưng chỉ có vài khe để thoát. Cẩn thận bị vây!" |

### Lời kết vòng

- **Người thắng:** "Mảnh Nhiệt đã nằm yên trong tay bạn!"
- **Người thua:** "Núi lửa thật khắc nghiệt — nhưng hãy xem bảng điểm cuối cùng!"

**Lưu ý thiết kế:** hiệu ứng môi trường của map này, như khói hay dung nham, chỉ để trang trí. Hiệu ứng không được che người chơi hay gây sát thương, vì server chưa có luật tương ứng.

## 7. Kết hành trình

**Hình nền:** lò trung tâm bùng sáng trở lại, ba mảnh lõi ghép thành Tim Lửa.

> **Bà Giữ Lò:** "Ba mảnh lõi đã trở về. Đảo Bom Bé lại sáng — và một nhà vô địch mới đã xuất hiện."

Sau lời dẫn là lời công bố, chọn theo kết quả:

| Kết quả | Lời công bố |
| --- | --- |
| Có một người dẫn đầu | "**{tên}** là Người Giữ Tim Lửa với **{điểm}** điểm!" |
| Thắng nhờ xét số vòng thắng | "Bằng điểm, nhưng **{tên}** thắng nhiều vòng hơn. Tim Lửa chọn bạn!" |
| Thắng nhờ thắng chương 3 | "Bằng điểm, bằng số vòng thắng — **{tên}** giành chiến thắng ở miệng núi lửa!" |
| Đồng hạng | "Tim Lửa sáng rực cho cả **{tên 1}** và **{tên 2}**. Đồng vô địch!" |

**Nút:** `Hành trình mới` hoặc `Về phòng chờ`.

## 8. Bảng điểm giữa các chương

```text
┌─────────────────── SAU CHƯƠNG 1 — MẢNH MẦM ───────────────────┐
│  Hạng  Tên        Thắng vòng   Hạ đối thủ   Điểm               │
│   1    Bom A          1             2         5                │
│   2    Bom B          0             1         1                │
│   3    Bom C          0             0         0                │
├────────────────────────────────────────────────────────────────┤
│  Tiếp theo: Chương 2 — Ngã tư bỏ hoang          [ Sẵn sàng ]   │
└────────────────────────────────────────────────────────────────┘
```

Số liệu trong khung là ví dụ minh họa, không phải kết quả chơi thật.

**Câu nhận xét ngắn dưới bảng điểm**, chọn một câu theo tình huống:

- Có người dẫn đầu cách biệt từ 4 điểm trở lên: "**{tên}** đang bỏ xa cả nhóm. Ai sẽ cản bước?"
- Hai người đầu chỉ cách nhau 0–1 điểm: "Cuộc đua sít sao! Chỉ một vụ nổ có thể đổi ngôi."
- Trước chương 3: "Thắng vòng cuối được 3 điểm — mọi thứ vẫn có thể lật ngược!"

## 9. Thông báo sự kiện trong trận (dùng chung cho cả ba chương)

Mỗi thông báo hiện ngắn ở góc màn hình, khoảng 2 giây. Cột thứ nhất ghi event server đang gửi thật (xem `docs/PROTOCOL.md`).

| Event server | Thông báo |
| --- | --- |
| `GAME_START` | "Chương {n} bắt đầu! Tìm năng lượng đi nào." |
| `PLAYER_ELIMINATED` (reason `explosion`) | "**{tên}** đã bị loại khỏi vòng!" |
| `PLAYER_ELIMINATED` (reason `reflection`) | "**{tên}** dính đòn phản Shield và bị loại!" |
| `PLAYER_ELIMINATED` (reason `disconnect`) | "**{tên}** mất kết nối quá lâu và bị loại." |
| `PLAYER_ELIMINATED` (reason `left`) | "**{tên}** đã rời trận." |
| `CORE_SPAWNED` | "Một Core vừa rơi ra — nhanh tay lên!" |
| `CORE_PICKED` | "**{tên}** đã nhặt Core và lớn mạnh hơn!" |
| `DAMAGE_REFLECTED` | "Phản đòn! Shield của **{tên}** dội ngược vụ nổ." |
| `PLAYER_DISCONNECTED` | "**{tên}** mất kết nối… còn 10 giây để quay lại." |
| Chỉ còn 2 người sống (client tự đếm từ `STATE_UPDATE`) | "Chỉ còn hai Bom Bé! Trận đấu quyết định!" |
| `GAME_OVER` có `winnerId` | "**{tên}** thắng chương {n}!" |
| `GAME_OVER` với `winnerId` rỗng | "Không ai trụ lại — chương này không có người thắng." |

## 10. Ghi chú triển khai

- **Làm được ngay trên client, không cần sửa server:**
  - Màn mở đầu và lời dẫn từng chương, dựa trên `mapId` trong `GAME_START`.
  - Gợi ý trong trận.
  - Thông báo sự kiện ở mục 9.
- **Cần server hỗ trợ thêm:**
  - Cố định thứ tự map `meadow → crossroads → crater`. Hiện `GameMap.random` chọn map ngẫu nhiên, chỉ tránh lặp lại map vừa chơi.
  - Lưu điểm hành trình và số vòng thắng.
  - Gửi `mode`, `chapterIndex` và `scores` cho client.
  - Thêm event kết thúc hành trình.
  - Tên và định dạng message phải được cả nhóm thống nhất giữa server, Unity và script đo trước khi sửa, như `docs/TEAM_PLAN.md` đã quy định.
- **Số lần hạ đối thủ** lấy từ trường `kills` mà server đã gửi trong `STATE_UPDATE`.
- **Lời thoại nên để trong một file dữ liệu** (ví dụ JSON trong `Assets/`) thay vì viết cứng trong `BombGame.cs`, để sửa lời mà không phải sửa code.

## Câu hỏi còn mở

- Theo code hiện tại, khi kẻ tấn công bị loại vì đòn phản Shield thì người bật Shield **không** được cộng `kills`. Có tính trường hợp này là 1 lần hạ đối thủ không?
- Nếu một chương kết thúc mà không ai thắng (`winnerId` rỗng), có chơi lại chương đó hay chuyển sang chương sau?
- Người chơi vào phòng giữa hành trình (giữa hai chương) có được tham gia với 0 điểm không?
