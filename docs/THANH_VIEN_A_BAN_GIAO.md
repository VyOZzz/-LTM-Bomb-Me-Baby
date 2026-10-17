# Bàn giao phần A cho T và B

Ngày 15/10/2026. Dựa trên code đầu vào `dc255a3`, nhánh làm việc `ThuyTien`. Các thay đổi của đợt này nằm trong working tree; tài liệu không khẳng định đã commit/push.

## 1. Trạng thái và phạm vi

- Server: sửa reconnect/disconnect, token không hợp lệ, mốc hết hạn, vòng đời phòng, xử lý đồng thời timeout và spawn 10 người; bổ sung kiểm thử thật.
- Đồng bộ: giữ `baseline` mặc định, giữ `optimized` đã có trong `dc255a3`. Không thêm trường, loại bản tin, mode hành trình hay schema delta mới.
- Test: 38 test qua ở cả hai lần cấu hình baseline và optimized; xem [bằng chứng](THANH_VIEN_A_KIEM_THU.md).
- Unity: chưa sửa, chưa chạy kiểm chứng tích hợp. Không bật optimized cho demo Unity hiện tại trước khi hoàn thiện các mục bên dưới.
- Benchmark: chưa thực hiện đo tải 2/4/8 client. B tiếp tục làm script và số liệu thực nghiệm.

**Trạng thái thống nhất protocol: chưa có xác nhận của T trong phiên làm việc này.** Đây là danh sách để nhóm review, không phải biên bản đã chốt. `docs/PROTOCOL.md` hiện chưa mô tả đầy đủ các thay đổi từ `dc255a3`.

## 2. Những việc cần T review/tích hợp

| Hạng mục đã có phía server | Tình trạng Unity đọc được từ code | Công việc cần thống nhất |
| --- | --- | --- |
| WELCOME khi resume có `lastSeq` | `BombProtocol.cs` chưa khai báo; `BombNetwork.cs` chưa dùng để cập nhật sequence | Bổ sung trường, bảo đảm request mới có seq lớn hơn lastSeq và không làm giảm counter đang cao hơn |
| GAME_START có `obstacles`, `bushes` | `BombGame.cs` chỉ đổi phase/status khi nhận GAME_START | Áp dụng/lưu map từ GAME_START trước snapshot optimized đầu tiên |
| Snapshot optimized thường bỏ `obstacles`, `bushes` | `ApplySnapshot` gọi `ApplyMap`; map mới thiếu trường sẽ dựng thiếu đá/bụi rồi ghi nhận activeMapId | Phân biệt trường vắng với danh sách rỗng; không xóa cache map khi trường vắng; tránh đánh dấu đã dựng map khi chưa nhận đủ dữ liệu |
| Đối thủ `visible=false` có x/z=0 trong optimized thường | Avatar đã dùng visible để quyết định hiển thị | Xác nhận không dùng tọa độ thay thế làm target, hiệu ứng hoặc thông tin radar |
| Orbs/cores được lọc trong 30 đơn vị | `SyncPickups` thay danh sách hiển thị theo snapshot | Xác nhận rời/vào vùng nhìn không bị hiểu là lỗi nhặt đồ; id mới xuất hiện lại phải được dựng lại |
| Resume trong PLAYING nhận full STATE_UPDATE | Server gửi map và mọi pickup, vẫn giữ tọa độ baseline | Kiểm tra client phục hồi map; thống nhất nếu sau này tách “gửi lại geometry” khỏi “bỏ lọc quyền nhìn thấy” |
| Token lạ/hết hạn/đang được dùng trả RESUME_FAILED | Client hiện hiển thị lỗi nhưng vòng reconnect có thể tiếp tục dùng token cũ | Chốt luồng quay về menu hoặc tạo session mới; không tự đổi danh tính trong server khi token sai |
| ACK có thể đi sau ERROR | Client hiện chỉ hiển thị processingMicros từ ACK | Không suy ra hành động thành công từ ACK; script đo cũng phải phân biệt ERROR |

Thay đổi server trong đợt này siết đúng cửa sổ reconnect: `elapsed < 10000 ms`. Không thay giá trị `RECONNECT_MS`. Token không tồn tại dùng mã lỗi `RESUME_FAILED` đã có, không thêm mã lỗi mới.

### Các đề xuất chưa triển khai

- Snapshot khôi phục vẫn nên giữ nguyên luật visibility của optimized nhưng gửi đủ geometry. Cần chốt ý nghĩa, sửa server/client/script/test cùng nhau.
- Nếu muốn delta thật, cần chốt cách gửi update/remove, tick cơ sở, khôi phục khi client mất state. Không gọi selective snapshot hiện tại là delta từng trường.
- Hành trình Ba Mảnh: thứ tự `meadow → crossroads → crater`, điểm/vòng thắng, `mode`, `chapterIndex`, `scores`, sự kiện kết thúc hành trình. Đây là phần sau khi chốt protocol trong ảnh phân công, chưa được thêm vào code.

## 3. B đo thế nào để so sánh công bằng?

Chạy từ thư mục `server/`; dùng cùng phiên bản code cho cả hai chế độ:

```powershell
mvn compile exec:java "-Dexec.args=7777" "-Dsync.mode=baseline"
```

Lưu số liệu baseline, dừng tiến trình bằng Ctrl+C, rồi:

```powershell
mvn compile exec:java "-Dexec.args=7777" "-Dsync.mode=optimized"
```

Phân biệt hai mốc: baseline trong phiên bản hiện tại để so với optimized cùng bản; prototype lịch sử `834ae6f` nếu nhóm cần so tiến trình phát triển. Baseline hiện tại đã có các sửa lỗi và GAME_START có geometry từ `dc255a3`, nên không phải bản sao byte-for-byte của prototype cũ.

| Chỉ số | Đo gì? | Lưu ý |
| --- | --- | --- |
| RTT | Thời gian từ gửi PING đến nhận PONG tương ứng | Dùng đồng hồ đơn điệu tại client để tính khoảng thời gian nếu script hỗ trợ |
| Message/giây | Số dòng JSON trong cửa sổ đo | Báo riêng STATE_UPDATE và tổng message nếu có thể |
| Byte/giây | Số byte UTF-8 thực sự đọc/ghi trên stream | Đếm cả dấu xuống dòng; không dùng số ký tự chuỗi |
| Kích thước snapshot | Byte của STATE_UPDATE | Tách snapshot khởi tạo/khôi phục khỏi snapshot định kỳ và vẫn báo tổng lưu lượng |
| processingMicros | Giá trị trong ACK | Không phải thời gian tạo snapshot định kỳ hay toàn bộ RTT |
| Lỗi và reconnect | Số ERROR, lần disconnect/resume, kết quả | Không bỏ các lần thất bại để chỉ giữ mẫu đẹp |

Server `PrintWriter.println` dùng dấu xuống dòng của hệ điều hành: trên Windows thường CRLF, khác LF trên hệ khác. Nếu tính từ JSON sau khi bỏ dấu xuống dòng thì phải ghi rõ đây là JSON payload, không phải toàn bộ byte trên stream. Đếm payload cũng chưa gồm TCP/IP overhead.

Mỗi kịch bản 2/4/8 client cần cùng map, số người, thời lượng, chuỗi input, phân bố vị trí/vật phẩm, máy chạy và điều kiện mạng. Server hiện chọn map/spawn ngẫu nhiên; chỉ chạy hai lệnh trên chưa tự bảo đảm hai lượt giống nhau. B cần ghi mapId, kiểm soát fixture hoặc phân nhóm kết quả và chạy lặp. Không có cờ seed production được thêm trong đợt này.

Công thức khi đã có hai phép đo tương đương:

```text
tiết kiệm (%) = (byte_baseline - byte_optimized) / byte_baseline × 100
```

Chưa có kết luận “tiết kiệm 30–50%” hay “giảm latency” cho game thực tế. Test fixture trong báo cáo chỉ xác nhận JSON của cùng trạng thái nhỏ hơn.

`scripts/network_smoke.py` hiện yêu cầu `bushes` trong STATE_UPDATE, nên chỉ dùng nguyên bản với baseline. Khi chốt optimized, B cập nhật script để lấy geometry từ GAME_START và chấp nhận geometry vắng ở snapshot thường.

## 4. Kịch bản tích hợp A + T + B

1. Chạy baseline, hai Unity client tạo/vào phòng, sẵn sàng, di chuyển, nổ, shield/boost và kết thúc trận.
2. Ngắt một client trong trận; đối thủ thấy trạng thái mất kết nối. Nối lại trước hạn: giữ id, phòng, HP và sequence, có map đầy đủ.
3. Nối lại quá hạn hoặc dùng token không tồn tại: hiện lỗi rõ ràng; luồng tạo session mới do client chủ động thực hiện.
4. Gửi request cùng seq hai lần qua client giả lập: một lần tác động, lần sau ERROR; seq cao hơn tiếp tục dùng được.
5. Cho hai người tranh Core: chỉ một người tăng Base Size; không phát thêm lần nhặt Core đó ở tick sau.
6. Sau khi T tích hợp optimized: kiểm tra map lúc bắt đầu, đổi map vòng mới và reconnect; kiểm tra bụi và vật phẩm vào/ra vùng nhìn.
7. B chạy cùng bộ kịch bản đo cho hai mode, lưu CSV thô và cấu hình. T review thay đổi PROTOCOL trước khi ghép nhánh.

## 5. Giới hạn cần giữ trong báo cáo

- Chưa chạy Unity Editor/build ở phiên làm việc này; không dùng ảnh concept làm ảnh game đã chạy.
- Chưa đo tải, mạng LAN thực tế, client chậm hoặc kết nối treo không báo EOF.
- Gửi socket còn đồng bộ trong khóa; một client chậm có thể ảnh hưởng tick.
- Snapshot recovery và một số event còn có tọa độ; không tuyên bố chống lộ vị trí toàn diện.
- Phòng kết thúc khi toàn bộ người chơi đã offline có thể còn giữ trong `rooms`; đợt này mới dọn chắc chắn lobby/phòng rỗng khi rời hoặc disconnect ngoài trận. Cần chính sách dọn phòng kết thúc cho server chạy lâu.
- Không tự commit/push, liên hệ T/B hay ghi nhận đã được nhóm phê duyệt.
