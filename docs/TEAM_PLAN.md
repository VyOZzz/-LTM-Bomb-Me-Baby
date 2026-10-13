# Phân công nhóm — Bomb Me Baby

Cập nhật: 13/10/2026. Tài liệu đối chiếu: `final-project/Topics.md`, `Instruction.md`, `Submission.md` và `README.md`.

## Mục tiêu và mốc bàn giao

- **20/10:** mốc nội bộ: 2–4 Unity client chơi trọn trận, một map chính dễ chơi, HUD/âm thanh/VFX cơ bản và số liệu baseline đầu tiên.
- **21–26/10:** hoàn thiện cải tiến đồng bộ trạng thái, đo lại bằng cùng kịch bản và sửa lỗi tích hợp.
- **27–30/10:** hoàn thiện báo cáo, README, bộ source chạy được, luyện bảo vệ và kiểm tra nộp bài.
- **31/10, 23:59:** hạn nộp chính thức theo `final-project/Submission.md`.

## Phần việc và bằng chứng bàn giao

| Người | Sở hữu chính | Bàn giao cần kiểm chứng |
| --- | --- | --- |
| Trưởng nhóm | Unity client: `LTM-Bomb-Me-Baby/Assets/Scripts/`, scene, HUD, ánh sáng, VFX, tích hợp âm thanh | Bản build/demo 2–4 client chơi từ lobby đến kết quả; giải thích luồng input → server → snapshot; review và tích hợp các nhánh. |
| Thành viên A | Java server: `BombServer.java`, `GameRoom.java`, `GameRules.java` và test liên quan | Sửa lỗi reconnect, request trùng, disconnect, tranh Core; test tự động; cải tiến đồng bộ có thể so với baseline và giải thích cơ chế thread/khóa room. |
| Thành viên B | `GameMap.java`, `scripts/`, dữ liệu thực nghiệm và tài nguyên âm thanh | Một map chính được playtest; script tạo 2/4/8 client; dữ liệu thô, cấu hình/lệnh chạy, bảng đo latency, message rate, byte truyền và thời gian xử lý; ghi nguồn/quyền sử dụng âm thanh. |

Mỗi người viết phần tương ứng trong báo cáo. Cả ba cùng đọc kiến trúc, giao thức và code quan trọng trước buổi bảo vệ.

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
