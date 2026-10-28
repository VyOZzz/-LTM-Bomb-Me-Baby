# 12 test tiêu biểu để Phương đưa vào báo cáo

Cập nhật 28/10/2026. Danh sách phục vụ T1/D1; tên phương thức khớp mã nguồn. Toàn bộ bộ test hiện có **124 test**, qua khi chạy với cả `-Dsync.mode=baseline` và `-Dsync.mode=optimized`. Các fixture chỉ định chế độ riêng vẫn giữ chế độ đó; không diễn giải hai lệnh này là mọi test đều đổi chế độ nội bộ.

| # | Lớp / test | Kịch bản | Kết quả kỳ vọng |
| --- | --- | --- | --- |
| 1 | `ConnectionTest.helloRequiredBeforeRoomActions` | Client gửi `CREATE_ROOM` trước `HELLO` qua TCP thật. | Trả `HELLO_REQUIRED`, không tạo phòng. |
| 2 | `ConnectionTest.duplicateAndOlderRequestsDoNotChangeRoomOrMovement` | Gửi lại `CREATE_ROOM`, rồi gửi `MOVE` có `seq` trùng hoặc cũ. | Trả `DUPLICATE_SEQUENCE`; không tạo thêm phòng hoặc đổi hướng; `seq` mới vẫn được chấp nhận. |
| 3 | `ConnectionTest.resumeRestoresIdentitySequenceRoomAndFullMap` | Ngắt kết nối trong trận rồi dùng resume token nối lại trong thời hạn. | Giữ player ID, phòng, `lastSeq`; gửi lại geometry; từ chối seq cũ; cleanup của socket cũ không ngắt socket mới. |
| 4 | `ConnectionTest.heartbeatWatchdogClosesASilentConnection` | Cho thời điểm nhận cuối vượt timeout rồi chạy watchdog. | Kết nối đóng, session chuyển sang offline, con trỏ connection được dọn. |
| 5 | `ConnectionTest.oversizedUnterminatedFrameClosesWithoutWaitingForNewline` | Gửi frame vượt 2048 ký tự mà chưa có newline. | Server đóng kết nối ngay khi quá giới hạn, không đợi newline. |
| 6 | `ConnectionTest.rateLimitClosesOnlyFloodingPlayerBeforeApplyingExtraAction` | Một người trong trận gửi đủ 120 frame trong một giây, sau đó gửi thêm `MOVE`; người kia tiếp tục chơi. | Đóng riêng socket vượt ngưỡng; không cập nhật `seq` của request bị chặn; giữ quyền reconnect; người kia vẫn nhận `PONG` và thực hiện `MOVE`. |
| 7 | `ServerFixesTest.disconnectAtDeadlineEliminatesAndDropsOnlyOneCore` | Tick trận tại đúng hạn reconnect rồi tick thêm lần nữa. | Người mất kết nối bị loại, chỉ rơi một Core, trận kết thúc. |
| 8 | `ServerFixesTest.equalDistanceCoreIsAwardedOnceAcrossConcurrentTicks` | Hai luồng cùng cập nhật trận có hai người cách Core bằng nhau. | Core chỉ được trao một lần, chỉ một người tăng base size, không còn Core trùng. |
| 9 | `ServerFixesTest.optimizedHidesBushOpponentButShowsSelfAndSameBush` | Đối thủ đứng trong bụi; người xem ban đầu ở ngoài rồi đi vào cùng bụi. | Ban đầu `visible=false`, tọa độ đối thủ bằng 0; vẫn thấy chính mình; cùng bụi thì nhận lại tọa độ thật. |
| 10 | `ServerFixesTest.optimizedSnapshotUsesFewerUtf8BytesForIdenticalState` | Serialize cùng phòng, tick, người xem và trạng thái theo cách đầy đủ và chọn lọc. | Snapshot chọn lọc có ít byte UTF-8 hơn; đây là fixture chức năng, không thay thế benchmark tải. |
| 11 | `ReviewFixesTest.leavingJourneyConcludesWithoutInventingFutureShards` | Một trong hai người rời Hành trình đã bắt đầu. | Kết thúc vì thiếu người, chỉ tính chương thực sự thắng; không tự cộng các mảnh của chương tương lai, không nhận người thay thế. |
| 12 | `ReviewFixesTest.quickTimeoutRanksKillsThenDamageAndCrownsTheLeader` | Một người hạ đối thủ, người khác gây nhiều sát thương hơn; cho trận Đấu nhanh hết giờ. | Điểm hạ ưu tiên hơn sát thương, người dẫn đầu thắng; thứ hạng và sát thương trong bảng đúng. |

Nguồn: [ConnectionTest](../server/src/test/java/bombmebaby/ConnectionTest.java), [ServerFixesTest](../server/src/test/java/bombmebaby/ServerFixesTest.java), [ReviewFixesTest](../server/src/test/java/bombmebaby/ReviewFixesTest.java).

Chạy ba lớp trên:

```powershell
cd server
mvn test "-Dtest=ConnectionTest,ServerFixesTest,ReviewFixesTest"
```

Chạy toàn bộ và xem bằng chứng:

```powershell
mvn test "-Dsync.mode=baseline"
mvn test "-Dsync.mode=optimized"
```

XML kết quả được lưu tại [baseline](validation/server-2026-10-28/baseline/) và [optimized](validation/server-2026-10-28/optimized/). Ngoài 12 test đại diện, `RequestRateLimiterTest` kiểm tra biên đúng 1 giây, cửa sổ trượt và vòng đệm/đồng hồ tràn; `ConnectionTest` kiểm tra flood JSON lỗi trước HELLO và lưu lượng bình thường; `ServerFixesTest.benchmarkSeedReproducesSpawnsAndOrbsAcrossSyncModes` kiểm tra cùng seed sinh cùng vị trí/vật phẩm ở 2/4/8 người.
