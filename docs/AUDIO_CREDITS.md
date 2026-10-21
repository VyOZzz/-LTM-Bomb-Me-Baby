# Nguồn và bản quyền âm thanh — Bomb Me Baby

Người phụ trách: Thành viên B. Mỗi file âm thanh đưa vào `LTM-Bomb-Me-Baby/Assets/` phải có một dòng ở đây trước khi ghép nhánh.

## Quy tắc chọn file

- Chỉ dùng file có giấy phép cho phép dùng trong đồ án và chỉnh sửa: ưu tiên CC0 (không cần ghi công), sau đó CC-BY (phải ghi tên tác giả). Không dùng CC-NC nếu chưa chắc đồ án có bị tính là thương mại hay không; không dùng nhạc tải từ YouTube hay game khác.
- Lưu lại link trang gốc của từng file, không chỉ link tải trực tiếp.
- Nếu cắt, đổi tốc độ, trộn hay đổi định dạng, ghi vào cột "Chỉnh sửa".
- File do AI tạo phải ghi rõ công cụ tạo, theo `final-project/Instruction.md`.

Nguồn gợi ý: [Kenney.nl](https://kenney.nl/assets) (CC0), [OpenGameArt.org](https://opengameart.org) (lọc theo giấy phép), [Pixabay Music/Sound Effects](https://pixabay.com/music/) (Pixabay Content License).

## Nhạc theo chương

| Chương / map | Không khí | File trong project | Tên gốc | Tác giả | Link nguồn | Giấy phép | Chỉnh sửa |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 — `meadow` | Vui, nhẹ nhàng | `Assets/Resources/Audio/music_meadow.wav` | Music Meadow | Nhóm Bomb Me Baby | `scripts/generate_sfx.ps1` | Tự tạo cho đồ án | Arpeggio sáng, pad nhẹ; loop 8 giây. |
| 2 — `crossroads` | Cơ khí, nhịp đều | `Assets/Resources/Audio/music_crossroads.wav` | Music Crossroads | Nhóm Bomb Me Baby | `scripts/generate_sfx.ps1` | Tự tạo cho đồ án | Nhịp gate và pulse cơ khí; loop 8 giây. |
| 3 — `crater` | Căng thẳng, dồn dập | `Assets/Resources/Audio/music_crater.wav` | Music Crater | Nhóm Bomb Me Baby | `scripts/generate_sfx.ps1` | Tự tạo cho đồ án | Bass thấp, pulse và rumble tổng hợp; loop 8 giây. |
| Lobby / menu | Êm, dễ nhận diện | `Assets/Resources/Audio/music_menu.wav` | Music Menu | Nhóm Bomb Me Baby | `scripts/generate_sfx.ps1` | Tự tạo cho đồ án | Arpeggio chậm và pad; loop 8 giây. |

## Hiệu ứng âm thanh (SFX)

| Sự kiện trong game | File trong project | Tên gốc | Tác giả | Link nguồn | Giấy phép | Chỉnh sửa |
| --- | --- | --- | --- | --- | --- | --- |
| Nạp nổ (charge) | `Assets/Resources/Audio/sfx_charge_loop.wav` | SFX Charge Loop | Nhóm Bomb Me Baby | `scripts/generate_sfx.ps1` | Tự tạo cho đồ án | Tổng hợp sóng sin tuần hoàn; Unity đổi pitch/volume theo mức nạp. |
| Nổ (`PLAYER_EXPLODE`) | `Assets/Resources/Audio/sfx_explosion.wav` | SFX Explosion | Nhóm Bomb Me Baby | `scripts/generate_sfx.ps1` | Tự tạo cho đồ án | Trộn noise giảm dần và bass sweep; phát 3D theo bán kính nổ. |
| Nhặt orb | `Assets/Resources/Audio/sfx_energy_pickup.wav` | SFX Energy Pickup | Nhóm Bomb Me Baby | `scripts/generate_sfx.ps1` | Tự tạo cho đồ án | Chirp tăng cao độ. |
| Nhặt Core | `Assets/Resources/Audio/sfx_core_pickup.wav` | SFX Core Pickup | Nhóm Bomb Me Baby | `scripts/generate_sfx.ps1` | Tự tạo cho đồ án | Chirp dài hơn, thêm harmonic và shimmer. |
| Bật Shield | `Assets/Resources/Audio/sfx_shield.wav` | SFX Shield | Nhóm Bomb Me Baby | `scripts/generate_sfx.ps1` | Tự tạo cho đồ án | Sweep cao độ giảm với ba lớp harmonic. |
| Boost | `Assets/Resources/Audio/sfx_boost.wav` | SFX Boost | Nhóm Bomb Me Baby | `scripts/generate_sfx.ps1` | Tự tạo cho đồ án | Sweep kết hợp noise dạng whoosh. |
| Bị trúng nổ / mất HP | `Assets/Resources/Audio/sfx_damage.wav` | SFX Damage | Nhóm Bomb Me Baby | `scripts/generate_sfx.ps1` | Tự tạo cho đồ án | Bass impact ngắn kết hợp noise. |
| Bắt đầu trận | `Assets/Resources/Audio/sfx_match_start.wav` | SFX Match Start | Nhóm Bomb Me Baby | `scripts/generate_sfx.ps1` | Tự tạo cho đồ án | Chuỗi ba nốt báo hiệu. |
| Thắng / thua | `Assets/Resources/Audio/sfx_victory.wav`, `sfx_defeat.wav` | SFX Victory / Defeat | Nhóm Bomb Me Baby | `scripts/generate_sfx.ps1` | Tự tạo cho đồ án | Hai chuỗi nốt đi lên/đi xuống. |
| Nút bấm UI | `Assets/Resources/Audio/sfx_ui_click.wav` | SFX UI Click | Nhóm Bomb Me Baby | `scripts/generate_sfx.ps1` | Tự tạo cho đồ án | Click tổng hợp 90 ms. |

Toàn bộ nhạc và SFX trên là PCM mono 44,1 kHz, 16-bit, được tạo từ công thức trong repository và không dùng sample âm thanh bên ngoài. Có thể tái tạo bằng `powershell -ExecutionPolicy Bypass -File scripts/generate_sfx.ps1` từ thư mục gốc.

## Bàn giao cho Trưởng nhóm

- [x] Đủ 11 SFX, có script tái tạo và thông tin nguồn/giấy phép.
- [x] SFX đã đặt tên rõ ràng, import trong Unity và tích hợp bằng `Assets/Scripts/BombAudio.cs`.
- [x] Thanh âm lượng **Hiệu ứng** điều khiển SFX trong game.
- [x] Đủ nhạc cho ba chương và menu; nhạc tự đổi theo map và hạ âm lượng khi có hiệu ứng lớn.
- [ ] Phần "Tài nguyên sử dụng" trong báo cáo đã chép lại bảng hoàn chỉnh.

Trạng thái ngày 21/10/2026: đã có 11 SFX và 4 vòng nhạc menu/map. Cần nghe thử trên bản Windows 2–4 client để chốt cân bằng âm lượng cuối.
