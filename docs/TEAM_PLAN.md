# Phân công nhóm — Bomb Me Baby

Cập nhật: 20/10/2026, chia lại việc còn lại theo khối lượng đã làm. Bản 15/10 xem trong lịch sử Git của file này. Tài liệu đối chiếu: `final-project/Topics.md`, `Instruction.md`, `Submission.md` và `README.md`.

| Người | Vai trò | Tài khoản Git |
| --- | --- | --- |
| Vỹ | Trưởng nhóm, Unity client | `VyOZzz`, `VyOZ` |
| Tiên | Thành viên A, Java server | `Leona` (nhánh `ThuyTien`) |
| Phương | Thành viên B, map, đo đạc, âm thanh, asset | `Phương` (nhánh `lanphuong`) |

## Đã làm đến 20/10 (theo commit)

| Người | Commit | Đã làm |
| --- | --- | --- |
| Vỹ | `aaed786`, `852d47d`, `29720e0` (12–13/10) | Toàn bộ prototype ban đầu: Java server (TCP JSON, game loop 20 Hz, snapshot 10 Hz, va chạm, nhặt đồ, sát thương), Unity client và scene `Main`, 3 map, hỗ trợ 10 người, test đầu tiên, README. |
| | `0267323`, `f571d91`, `e7e7316` (13–14/10) | Lưu yêu cầu đồ án, kế hoạch phân công đầu tiên, concept arena/HUD, cốt truyện Ba Mảnh Tim Lửa. |
| Tiên | `dc255a3`, `752d67d` (15–17/10) | Novelty selective sync (`SyncMode` baseline/optimized); sửa reconnect, token sai/hết hạn, request trùng, disconnect, dọn phòng; test `ConnectionTest`, `ServerFixesTest`; 3 tài liệu bàn giao. |
| | `4beb0c9` + 4 commit sửa (20/10) | Chế độ Đấu nhanh và Hành trình Ba Mảnh (`GameMode`, điểm, `JOURNEY_COMPLETE`); sửa lộ vị trí trong bụi cho event nổ/Core và snapshot khôi phục; cập nhật `PROTOCOL.md`; 55 dòng Unity để client đọc protocol mới (nút chọn mode, dòng chữ chương/điểm, `lastSeq`, `visible`). 45/45 test qua. |
| Phương | `a84e1d5`, `0199310` (15/10) | Kịch bản Hành trình Ba Mảnh (`STORY_SCRIPT.md`), cập nhật phân công 15/10, `load_test.py` đo RTT/byte/message/`processingMicros` với N client. |
| | `c556012` → `a54d111` (19/10) | `run_benchmark.ps1`, `summarize_experiments.py`, ghi `map_id`/`sync_mode`; 18 lượt đo baseline/optimized 2/4/8 client (`experiments/`, `EXPERIMENTS.md`); sửa `network_smoke.py` cho optimized; mẫu `AUDIO_CREDITS.md`, `PLAYTEST_MAPS.md`; merge nhánh `ThuyTien` vào `lanphuong`. |

Chưa có trên Git: phần Unity giao ngày 15/10 (build Windows, HUD, VFX, âm thanh, lời dẫn, bảng điểm/màn kết hành trình có giao diện), playtest map và âm thanh. Mốc 20/10 "2–4 Unity client chơi trọn trận, HUD/âm thanh/VFX cơ bản" chưa đạt.

## Mốc còn lại

- **22/10:** Unity build Windows chơi trọn Đấu nhanh và Hành trình với 2 client; playtest xong 3 map; có bộ âm thanh đã ghi nguồn.
- **24/10:** bộ asset đồ họa (model, texture, icon, font) đã chọn, ghi nguồn và gửi Vỹ.
- **25/10:** HUD, VFX, âm thanh, lời dẫn, bảng điểm và màn kết hành trình trong Unity; Unity dùng được `optimized`; đo lại trên bản này.
- **28/10:** đủ các phần báo cáo; README build/run/test chạy đúng trên máy sạch; `main` chứa bản cuối.
- **30/10:** báo cáo `.docx` hoàn chỉnh, kiểm tra Compilatio, luyện bảo vệ.
- **31/10, 23:59:** hạn nộp chính thức theo `final-project/Submission.md`.

## Phân công từ 20/10

Nguyên tắc: Vỹ làm toàn bộ Unity. Tiên đã làm xong phần lớn server nên phần còn lại nhẹ hơn và nghiêng về tài liệu. Phương nhận thêm asset cho game, việc ghép báo cáo và đóng gói bài nộp.

| Người | Việc | Bàn giao kiểm chứng được |
| --- | --- | --- |
| **Vỹ** (Unity, tích hợp) | Build Windows; HUD (HP, Size, thanh nạp, cooldown Shield/Boost); VFX nổ/Shield; gắn nhạc và SFX do Phương gửi. Màn mở đầu, lời dẫn theo `mapId`, gợi ý và thông báo sự kiện theo `STORY_SCRIPT.md` (lời thoại trong file JSON). Thay phần chữ tạm của Tiên bằng bảng điểm giữa chương và màn kết hành trình có giao diện. Kiểm tra 6 mục Unity trong `THANH_VIEN_A_BAN_GIAO.md`, gồm chạy được `optimized`. Review và merge `ThuyTien`, `lanphuong` vào `main`. | Bản build 2–4 client chơi từ lobby đến kết quả ở cả hai mode; ảnh chụp game chạy thật cho báo cáo; `main` là bản nộp. |
| **Tiên** (server) | Sửa lỗi server phát sinh khi Vỹ tích hợp Unity. Cập nhật `THANH_VIEN_A_BAN_GIAO.md` (đã có benchmark và smoke test). Nếu kịp: chính sách dọn phòng đã kết thúc khi mọi người offline. | Test vẫn qua sau mỗi sửa; tài liệu bàn giao khớp code. |
| **Phương** (map, đo, âm thanh, asset, đóng gói) | Playtest 3 map, chỉnh `GameMap.java` nếu cần. Chọn nhạc theo chương và SFX, ghi `AUDIO_CREDITS.md`, gửi file cho Vỹ. Tìm và chuẩn bị asset đồ họa cho game: model/texture cho đá, bụi cây, sàn và tường theo từng map; skybox hoặc nền; icon HUD (HP, Shield, Boost, Core); font tiếng Việt; hiệu ứng hạt cho nổ/Shield nếu có. Đưa vào `LTM-Bomb-Me-Baby/Assets/` theo thư mục Vỹ quy định, ghi nguồn và giấy phép như âm thanh. Đo lại baseline/optimized trên bản cuối, thêm đo qua LAN hai máy nếu được. Ghép báo cáo `.docx` từ phần của cả nhóm, chạy Compilatio, chuẩn bị thư mục `Source/` và `Report/`. | `PLAYTEST_MAPS.md` đã điền; bộ âm thanh và asset có nguồn, mở được trong Unity 6000.3.6f1; `experiments/` và `EXPERIMENTS.md` theo bản cuối; báo cáo và kết quả Compilatio. |

Phần viết báo cáo, mỗi người viết phần mình làm:

| Phần báo cáo | Người viết |
| --- | --- |
| Giới thiệu, gameplay, kiến trúc tổng thể, Unity client, kết quả demo | Vỹ |
| Giao thức JSON, server, thread/khóa room, reconnect, novelty selective sync, kiểm thử tự động | Tiên |
| Thiết kế map, thực nghiệm và đánh giá, tài nguyên sử dụng (âm thanh, asset đồ họa, AI), ghép và định dạng báo cáo | Phương |

Phụ thuộc: Unity cần bộ âm thanh và asset của Phương trước 24/10; Vỹ thống nhất với Phương phong cách đồ họa và thư mục đặt asset trước khi Phương tải về; phép đo lại cần Unity chạy được `optimized` (Vỹ) và server ổn định (Tiên). Mọi thay đổi protocol tiếp theo do Tiên đề xuất và Vỹ duyệt trước khi ghép nhánh.

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
