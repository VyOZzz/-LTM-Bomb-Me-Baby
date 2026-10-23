# Phân công nhóm — Bomb Me Baby

## Rà soát bổ sung 24/10/2026

Đã sửa toàn bộ các điểm trong lượt rà lại: ticket có generation và chốt ghép dưới khóa player; người mới vào QUICK FINISHED thấy lobby chờ lượt sau, reconnect không nhận kết quả cũ; audio đối thủ ẩn không dùng gốc bản đồ/vị trí cũ; link Report được trỏ tới Source/docs và kiểm tra trước đóng ZIP. **107 test Java ở mỗi mode, 12 kiểm tra C# và 111 kiểm tra Unity**; chi tiết hồi quy, build và giới hạn tại [FINAL_VALIDATION.md](FINAL_VALIDATION.md).

Các sửa lỗi 23/10 có trong `e1382fd`; thay đổi 24/10 chưa commit/merge. Client/server cần cùng protocol mới có `roundParticipantIds`. Kiểm thử tự động không thay thế playtest người thật/LAN.

## Rà soát kỹ thuật và luồng chơi 23/10/2026

Đã sửa cả 5 lỗi trong đợt rà soát 23/10: Hành trình thiếu người kết thúc sớm có kết quả; không cộng sai thời gian phá ấn khi khựng; phản khiên reset phá ấn; client chết ngừng gửi gameplay input; bản sao build loại asset đã xóa. Bổ sung Đấu nhanh tối đa 3 phút (hết giờ nhiều người thì hòa) và camera/audio tự theo người sống với phạm vi nhìn do server quyết định. **101 test Java thuộc source bàn giao qua ở cả baseline/optimized, 12 kiểm tra C#, 81 kiểm tra Unity và fixture staging qua.** Chi tiết luật, kiểm chứng và giới hạn tại [FINAL_VALIDATION.md](FINAL_VALIDATION.md). Cần dùng client/server cùng phiên bản protocol; trạng thái commit hiện tại xem mục 24/10 ở trên.

## Điều chỉnh âm thanh 23/10/2026

Phản hồi tiếp theo: người dùng xác nhận âm thanh đã ổn, riêng bom còn nhẹ. Tăng riêng tiếng bom: mở dải trầm/độ rõ, attack 6 ms, RMS mục tiêu −23 dBFS và gain phát 0,64–0,86 theo bán kính; giữ giới hạn tổng gain và không thêm lại lớp động cơ/bass. Các file âm thanh khác giữ nguyên. Bản này cần nghe lại để chốt độ mạnh của bom.

Theo phản hồi tiếng bom/xung kích và khiên còn rè: thay ba tiếng nổ `explosionCrunch` bằng mẫu nổ trầm Kenney ngắn hơn, thay tiếng bật/đỡ khiên, lọc lại tiếng tăng tốc; bỏ lớp bass ghép thêm và hai loop động cơ khiên/tăng tốc. Cập nhật script tái tạo, nguồn và số đo tại [AUDIO_CREDITS.md](AUDIO_CREDITS.md), [AUDIO_LEVELS.csv](AUDIO_LEVELS.csv); kết quả kiểm tra tại [AUDIO_VALIDATION.md](AUDIO_VALIDATION.md). Bộ hiện tại dùng file tải về, còn 20 clip; mô tả “11 SFX tự tổng hợp” bên dưới là lịch sử ngày 21/10. Cần nghe lại trên thiết bị thật để chốt cảm giác, không coi số đo peak là bằng chứng đã hết rè khi chơi.

## Bổ sung tích hợp 22/10/2026

Mốc 22/10: đã sửa thứ tự snapshot, phục hồi phòng/kết quả, va chạm tăng size, watchdog đóng nhầm connection, metadata chương trên HUD và vòng đời vật liệu; khi đó 98 test Java qua cả hai mode. Đây là lịch sử, không phải số test hiện tại. Trạng thái 23/10 xem [FINAL_VALIDATION.md](FINAL_VALIDATION.md). Benchmark 22/10 ghi riêng tại [EXPERIMENTS_FINAL.md](EXPERIMENTS_FINAL.md); không thay thế số liệu 19/10 và chưa được đo lại sau thay đổi 23/10.

Việc nhóm cần chốt: thông tin trang bìa, playtest người thật/LAN, kiểm tra Compilatio và bàn giao bản Report đã nộp. Các số 71 test/11 SFX ở phần lịch sử không mô tả bản hiện tại.

Thông tin nhóm và lịch sử đóng góp bên dưới giữ theo mốc 21/10/2026. Bản 15/10 và 20/10 xem trong lịch sử Git của file này. Tài liệu đối chiếu: `final-project/Topics.md`, `Instruction.md`, `Submission.md` và `README.md`.

| Người | Vai trò | Tài khoản Git |
| --- | --- | --- |
| Vỹ | Trưởng nhóm, Unity client | `VyOZzz`, `VyOZ` |
| Tiên | Thành viên A, Java server | `Leona` (nhánh `ThuyTien`) |
| Phương | Thành viên B, map, đo đạc, âm thanh, đóng gói | `Phương` (nhánh `lanphuong`) |

## Đã làm đến 21/10 (theo commit và kiểm tra thực tế)

| Người | Commit | Đã làm |
| --- | --- | --- |
| Vỹ | `aaed786`, `852d47d`, `29720e0` (12–13/10) | Toàn bộ prototype ban đầu: Java server (TCP JSON, game loop 20 Hz, snapshot 10 Hz, va chạm, nhặt đồ, sát thương), Unity client và scene `Main`, 3 map, hỗ trợ 10 người, test đầu tiên, README. |
| | `0267323`, `f571d91`, `e7e7316` (13–14/10) | Lưu yêu cầu đồ án, kế hoạch phân công đầu tiên, concept arena/HUD, cốt truyện Ba Mảnh Tim Lửa. |
| | `05721b1`, `9b15950`, `301dfec` (20–21/10) | Menu chọn mode, HUD, minimap, mảnh Tim Lửa, lời dẫn từ JSON, cooldown bom, màn mở đầu bằng video/ảnh dự phòng, hình ảnh menu, bảng điểm/kết quả và cập nhật luật Hành trình. |
| Tiên | `dc255a3`, `752d67d` (15–17/10) | Novelty selective sync (`SyncMode` baseline/optimized); sửa reconnect, token sai/hết hạn, request trùng, disconnect, dọn phòng; test `ConnectionTest`, `ServerFixesTest`; 3 tài liệu bàn giao. |
| | `4beb0c9` + 4 commit sửa (20/10) | Chế độ Đấu nhanh và Hành trình Ba Mảnh (`GameMode`, điểm, `JOURNEY_COMPLETE`); sửa lộ vị trí trong bụi cho event nổ/Core và snapshot khôi phục; cập nhật `PROTOCOL.md`; 55 dòng Unity để client đọc protocol mới (nút chọn mode, dòng chữ chương/điểm, `lastSeq`, `visible`). Tại mốc này 45/45 test qua; đến mốc 21/10 tăng lên 71 test; bộ hiện tại xem phần đầu tài liệu. |
| | `d5aaa69`, `806c4d9` (21/10, hỗ trợ tích hợp) | Thêm 11 SFX tự tổng hợp, `BombAudio`, âm thanh UI/gameplay, script tái tạo và sửa cảnh báo tên biến. Phần đóng góp âm thanh cần được ghi đúng theo người thực hiện/thực sự phối hợp trong PR và báo cáo. |
| Phương | `a84e1d5`, `0199310` (15/10) | Kịch bản Hành trình Ba Mảnh (`STORY_SCRIPT.md`), cập nhật phân công 15/10, `load_test.py` đo RTT/byte/message/`processingMicros` với N client. |
| | `c556012` → `a54d111` (19/10) | `run_benchmark.ps1`, `summarize_experiments.py`, ghi `map_id`/`sync_mode`; 18 lượt đo baseline/optimized 2/4/8 client (`experiments/`, `EXPERIMENTS.md`); sửa `network_smoke.py` cho optimized; mẫu `AUDIO_CREDITS.md`, `PLAYTEST_MAPS.md`; merge nhánh `ThuyTien` vào `lanphuong`. |

Trạng thái kiểm chứng ngày 21/10: Unity đã có HUD, VFX cơ bản, lời dẫn, bảng điểm/kết quả, 11 SFX và 4 track nhạc menu/map; source Unity compile thành công với `6000.3.6f1`. Client có writer nền, inbox giới hạn/gộp snapshot, heartbeat, banner reconnect/hết hạn, trạng thái người mất kết nối và bảng F3. Server có writer queue riêng từng connection, timeout client im lặng/write treo, dừng input cũ, dọn phòng offline và **71 test** đều qua ở `baseline` và `optimized`; smoke TCP toàn tiến trình đã qua ở optimized. Benchmark cũ có 18 lượt 2/4/8 client giả lập. Chưa điền playtest ba map, chưa đo lại bằng 2–4 Unity client thật/LAN và các thay đổi trải nghiệm demo chưa nằm trên `origin/main`.

## Mốc bàn giao còn lại (cập nhật 24/10)

- **Đã có bản build 24/10:** Windows và server JAR từ source đã sửa; kiểm tra tự động qua. Còn review/commit các thay đổi hiện tại, playtest 2–4 client và điền kết quả ba map.
- **24/10:** nghe thử/cân bằng nhạc và SFX trên build 2–4 client; chốt runbook demo và cấu hình server/map.
- **25/10:** kiểm chứng HUD, VFX, âm thanh, lời dẫn, bảng điểm và màn kết trên build; chạy `optimized` end-to-end; deploy server thử nghiệm và đo lại trên bản cuối.
- **28/10:** đủ các phần báo cáo; README build/run/test chạy đúng trên máy sạch; `main` chứa bản cuối.
- **30/10:** báo cáo `.docx` hoàn chỉnh, kiểm tra Compilatio, luyện bảo vệ.
- **31/10, 23:59:** hạn nộp chính thức theo `final-project/Submission.md`.

## Phân công bàn giao (giữ vai trò từ 20/10, cập nhật tiến độ 23/10)

Nguyên tắc: Vỹ làm toàn bộ Unity. Tiên đã làm xong phần lớn server nên phần còn lại nghiêng về deploy và tài liệu. Phương tập trung playtest, âm thanh, đo lại, ghép báo cáo và đóng gói bài nộp. Không mở thêm hạng mục hoàn thiện hình ảnh map trong phạm vi còn lại.

| Người | Việc | Bàn giao kiểm chứng được |
| --- | --- | --- |
| **Vỹ** (Unity, tích hợp) | Review và merge các thay đổi vào `main`; tạo build Windows từ commit cuối; kiểm tra 6 mục Unity trong `THANH_VIEN_A_BAN_GIAO.md`, gồm `optimized`, reconnect, hai mode, bảng điểm và màn kết. Chụp ảnh game chạy thật và sửa lỗi tích hợp phát sinh. | Bản build 2–4 client chơi từ lobby đến kết quả ở cả hai mode; ảnh chụp game chạy thật; `main` và tag phát hành chứa bản đã kiểm chứng. |
| **Tiên** (server) | Sửa lỗi server phát sinh khi kiểm thử Unity; giữ bộ 107 test hiện tại qua ở cả hai sync mode. Writer queue, heartbeat, input expiry và dọn phòng offline đã có; phần còn lại là chuẩn bị cách chạy server deploy được (port cấu hình, JAR/Docker hoặc nền tảng tương đương), cập nhật bàn giao và hỗ trợ smoke test public/LAN. | Test sạch sau mỗi sửa; client kết nối được tới server tách riêng; tài liệu bàn giao khớp code và lệnh triển khai. |
| **Phương** (map, đo, âm thanh, đóng gói) | Playtest 3 map và điền kết quả thật; nghe thử/cân bằng bộ nhạc và SFX trên 2–4 client. Đo lại baseline/optimized trên commit cuối, ưu tiên thêm LAN hai máy. Ghép báo cáo `.docx`, chạy Compilatio, chuẩn bị `Source/` và `Report/`. | `PLAYTEST_MAPS.md` đã điền; âm thanh có nguồn; `EXPERIMENTS.md` khớp bản cuối; báo cáo đúng tên và có kết quả Compilatio. |

Phần viết báo cáo, mỗi người viết phần mình làm:

| Phần báo cáo | Người viết |
| --- | --- |
| Giới thiệu, gameplay, kiến trúc tổng thể, Unity client, kết quả demo | Vỹ |
| Giao thức JSON, server, thread/khóa room, reconnect, novelty selective sync, kiểm thử tự động | Tiên |
| Thiết kế map, thực nghiệm và đánh giá, tài nguyên sử dụng (âm thanh, AI), ghép và định dạng báo cáo | Phương |

Phụ thuộc: nhạc/SFX đã có và cần được nghe thử trên build. Phép đo lại chỉ chạy sau khi Vỹ tạo build từ commit cuối và Tiên chốt server/config deploy. Mọi thay đổi protocol tiếp theo do Tiên đề xuất và Vỹ duyệt trước khi ghép nhánh.

Cả ba cùng đọc kiến trúc, giao thức và code quan trọng trước buổi bảo vệ; ai cũng phải giải thích được luồng input → server → snapshot.

## Định hướng kỹ thuật

- Prototype tại commit `834ae6f` là mốc ban đầu; báo cáo cần phân biệt phần đã có với phần nhóm phát triển tiếp.
- Ứng viên cho **Novelty / Contributions** là giảm dữ liệu đồng bộ so với snapshot đầy đủ hiện tại. Chỉ trình bày là cải tiến đã đạt được sau khi có code, test và số liệu đo lặp lại được.
- Mọi thay đổi JSON protocol phải được thống nhất giữa server, Unity client và script đo trước khi ghép nhánh.
- UI/đồ họa/âm thanh phục vụ trải nghiệm và demo; không dùng riêng chúng làm bằng chứng cho novelty kỹ thuật.
- GDD gốc nhắm 2–4 người; code hiện hỗ trợ tối đa 10. Demo chính dùng 2–4 client Unity; thí nghiệm có thể dùng 8 client giả lập. Báo cáo ghi rõ sự khác biệt này.

## Quy tắc làm việc và nộp bài

- Mỗi người dùng nhánh và tài khoản Git của mình; commit theo thay đổi thực tế, mô tả rõ đã làm gì. Không sửa lịch sử để tạo đóng góp giả. PR ghi cách chạy và kết quả kiểm thử thực tế.
- AI có thể hỗ trợ tìm hiểu, giải thích, gợi ý và debug theo `final-project/Instruction.md`. Người nộp chịu trách nhiệm về code, kiến trúc, số liệu, nguồn tham khảo và báo cáo; phải hiểu và bảo vệ được phần mình làm. Ghi trung thực tài nguyên AI tạo ra, gồm concept hình ảnh trong `docs/concepts/`.
- Không tự tạo số liệu, không gọi ảnh concept là ảnh chụp game chạy thật. Báo cáo phân biệt kết quả đã đo với kế hoạch chưa thực hiện.
- Bộ nộp cần source chạy được, README build/run/test, báo cáo `NhomXX_TenProject_Report.docx`, tài liệu thực nghiệm và kết quả kiểm tra Compilatio theo quy định trong `final-project/`.
