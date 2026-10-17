# Kết quả kiểm thử phần A

Ngày kiểm tra: 15/10/2026, Windows 11, JDK 21.0.10, Maven 3.9.16. POM giữ `maven.compiler.release=17`, Gson 2.11.0 và JUnit 5.11.4. Code đầu vào: `dc255a3`; kết quả cuối áp dụng cho các thay đổi working tree cùng tài liệu này.

## 1. Các lần chạy thực tế

| Lần chạy | Kết quả | Ý nghĩa |
| --- | --- | --- |
| `mvn test` trước khi sửa | 27/27 pass | Xác nhận trạng thái đầu vào; chưa chứng minh các test mô phỏng kiểm tra được production connection |
| `mvn test "-Dsync.mode=optimized"` trong quá trình sửa | Một lần lỗi `Cannot separate 10 spawns on map meadow` | Phát hiện lỗi ngẫu nhiên trong chọn spawn 10 người; đã bổ sung thử lại và hồi quy 60 cấu hình |
| `mvn test "-Dsync.mode=optimized"` sau sửa spawn | 38 test, 0 failure, 0 error, 0 skipped; BUILD SUCCESS | Kiểm tra toàn bộ suite khi property production chọn optimized |
| `mvn clean test "-Dsync.mode=baseline"` sau cùng | 38 test, 0 failure, 0 error, 0 skipped; BUILD SUCCESS | Biên dịch lại sạch và kiểm tra cấu hình baseline |

Các lệnh chạy từ thư mục `server/`. Báo cáo Maven nằm trong `server/target/surefire-reports/`; `mvn clean` xóa kết quả lần trước. Đây là hai lần chạy cùng 38 test, không phải 76 test khác nhau.

## 2. Nội dung 38 test

### ConnectionTest — 12 test, socket TCP thật

| Tên test | Điều được xác nhận |
| --- | --- |
| `helloRequiredBeforeRoomActions` | Request phòng trước HELLO bị từ chối |
| `unknownTokenFailsWithoutCreatingNewSession` | Token lạ không âm thầm tạo player mới |
| `duplicateAndOlderRequestsDoNotChangeRoomOrMovement` | CREATE_ROOM/MOVE trùng hoặc cũ không tác động lại; seq tăng vẫn được xử lý |
| `pingDoesNotAdvanceActionSequence` | PING phản hồi đúng và không chiếm seq của action |
| `disconnectInLobbyRemovesPlayerAndEmptyRoom` | Socket đóng dọn kết nối, membership và lobby rỗng |
| `resumeRestoresIdentitySequenceRoomAndFullMap` | Giữ id/phòng/lastSeq; WELCOME đi trước state; map phục hồi đủ; seq cũ bị từ chối; cleanup cũ không phá kết nối mới |
| `simultaneousResumeAllowsExactlyOneTcpConnection` | Hai socket tranh token: một WELCOME, một RESUME_FAILED; đóng socket thua không ngắt người thắng |
| `activeSessionCannotBeClaimedAgain` | Không chiếm session đang có kết nối |
| `resumeAtOrAfterDeadlineIsRejected` | Hết cửa sổ reconnect thì từ chối |
| `gameStartCarriesMapAndFollowingOptimizedSnapshotOmitsIt` | Kiểm tra JSON thực sự truyền qua socket, không chỉ kiểm tra biến map trong room |
| `disconnectNotifiesOpponentThenTimeoutAwardsCorrectWinner` | Có sự kiện disconnect, còn sống trước hạn, bị loại khi hết hạn và chọn đúng người thắng |
| `leaveRoomClearsMembershipAndAllowsCreatingAnotherRoom` | Rời phòng rồi tạo phòng mới không giữ membership cũ |

Fixture mở socket trên loopback/cổng tự cấp, chạy chính `BombServer.Connection.run()` bằng executor. Không khởi chạy `BombServer.run()` và scheduler production; các tick cần kiểm tra được test gọi chủ động. Vì vậy đây là integration test lớp kết nối, không thay thế smoke test toàn tiến trình hoặc demo Unity. Fixture ConnectionTest chủ động chọn optimized để kiểm tra recovery ngay cả khi Maven property là baseline.

### ServerFixesTest — 14 test trạng thái phòng

| Nhóm | Các trường hợp |
| --- | --- |
| Timeout (3) | Trước hạn còn sống; đúng hạn bị loại và chỉ rơi một Core; mọi người cùng hết hạn đều bị loại |
| Core (4) | Người gần nhất dù đứng sau trong danh sách; bằng khoảng cách với hai tick chạy đồng thời; người chết/offline không nhặt; ngoài tầm không nhặt |
| Snapshot (7) | Baseline giữ map/vật phẩm; optimized culling đúng biên 30; ẩn người trong bụi nhưng giữ self/cùng bụi; ẩn người xa; full recovery giữ contract cũ; snapshot không dùng chung list mutable; so byte cùng trạng thái |

Cả BASELINE và OPTIMIZED được tạo tường minh trong test, nên không có chuyện tất cả test optimization vô tình chạy theo mặc định baseline.

### GameRoomTest — 12 test gameplay

11 test nền về sẵn sàng, chế độ solo, damage, shield, boost, movement, bụi, map, spawn và kết thúc trận được giữ lại. Test đọc geometry yêu cầu snapshot đầy đủ tường minh để chạy được ở cả hai property.

Test mới `tenPlayersCanStartAcrossMapsAndSeeds` thử 3 map × 20 seed = 60 cấu hình, mỗi cấu hình có 10 người. Kiểm tra trận bắt đầu và mọi cặp spawn cách nhau tối thiểu 28 đơn vị. Đây là một test chạy nhiều dữ liệu, không tính thành 60 test độc lập.

## 3. Phép kiểm tra kích thước JSON

Test `optimizedSnapshotUsesFewerUtf8BytesForIdenticalState` dùng một room cố định map meadow, seed 42, hai player, cùng thời điểm/viewer và một Orb ở xa. Nó serialize cùng trạng thái một lần có forceBaseline và một lần optimized.

Kết quả quan sát:

```text
[Snapshot fixture] baseline=1691 optimized=709 UTF-8 bytes (JSON only)
```

Đây là byte **JSON**, chưa cộng dấu xuống dòng hay TCP/IP overhead. Nó chứng minh trường hợp kiểm thử này có payload nhỏ hơn. Không suy diễn thành số liệu byte/giây, latency hoặc phần trăm tiết kiệm cho mọi trận; chưa có benchmark 2/4/8 client. UUID có độ dài cố định; map/seed và dữ liệu fixture được giữ cố định cho phép kiểm tra này.

## 4. Chưa xác minh

- Unity client tương thích optimized, reconnect khi tạo lại đối tượng network và dựng map sau reconnect.
- Smoke script Python: `python` trên máy hiện trỏ tới Windows Store alias, chưa có trình Python chạy được qua lệnh đó; chưa chạy `scripts/network_smoke.py` trong đợt này.
- Tải 2/4/8 client, RTT, băng thông trên mạng thật, client chậm và TCP treo không báo mất kết nối.
- Mọi interleaving đa luồng hay mọi seed spawn có thể xảy ra. Test qua cung cấp bằng chứng hồi quy, không phải chứng minh không còn bất kỳ lỗi nào.

Hướng dẫn học code: [THANH_VIEN_A_HUONG_DAN.md](THANH_VIEN_A_HUONG_DAN.md). Checklist tích hợp và đo: [THANH_VIEN_A_BAN_GIAO.md](THANH_VIEN_A_BAN_GIAO.md).
