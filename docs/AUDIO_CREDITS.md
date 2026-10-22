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
| Lobby / menu | Nhạc do người dùng chọn | `Assets/Resources/Audio/music_menu.mp3` | `ssstik.io_1792657165746_1.mp3` | Chưa xác định | Tệp do người dùng cung cấp | Quyền sử dụng do người dùng cung cấp | Đổi tên để giữ nguyên đường dẫn `Resources/Audio/music_menu`; phát streaming và lặp trong sảnh. Bản nhạc tự tạo cũ được lưu tại `Assets/AudioArchive/music_menu_generated.wav`. |

## Hiệu ứng âm thanh (SFX)

| Sự kiện trong game | File trong project | Tên gốc | Tác giả | Link nguồn | Giấy phép | Chỉnh sửa |
| --- | --- | --- | --- | --- | --- | --- |
| Nạp nổ (charge) | `Assets/Resources/Audio/sfx_charge_loop.wav` | SFX Charge Loop | Nhóm Bomb Me Baby | `scripts/generate_sfx.ps1` | Tự tạo cho đồ án | Tổng hợp sóng sin tuần hoàn; Unity đổi pitch/volume theo mức nạp. |
| Nổ (`PLAYER_EXPLODE`) | `Assets/Resources/Audio/kenney_explosion_crunch.ogg`, `kenney_explosion_low.ogg` | `explosionCrunch_004`, `lowFrequency_explosion_000` | Kenney | [Sci-fi Sounds](https://kenney.nl/assets/sci-fi-sounds) | CC0 | Phát hai lớp crunch + low body; nổ của bản thân phát 2D, nổ từ xa phát 3D theo bán kính. |
| Nhặt orb | `Assets/Resources/Audio/kenney_energy_pickup.ogg` | `confirmation_003` | Kenney | [Interface Sounds](https://kenney.nl/assets/interface-sounds) | CC0 | Đổi tên; duck nhạc ngắn để tiếng nhặt rõ. |
| Nhặt Core | `Assets/Resources/Audio/kenney_core_pickup.ogg` | `maximize_006` | Kenney | [Interface Sounds](https://kenney.nl/assets/interface-sounds) | CC0 | Đổi tên; tăng mức phát và duck nhạc. |
| Bật Shield | `Assets/Resources/Audio/kenney_shield.ogg`, `kenney_shield_body.ogg` | `forceField_003`, `engineCircular_002` | Kenney | [Sci-fi Sounds](https://kenney.nl/assets/sci-fi-sounds) | CC0 | Ghép tiếng bật với hum điện; attack 0,1 giây, rung pitch nhẹ và fade-out 0,28 giây. Âm từ xa vẫn phát 3D. |
| Boost | `Assets/Resources/Audio/kenney_boost.ogg`, `kenney_boost_body.ogg` | `laserLarge_002`, `thrusterFire_002` | Kenney | [Sci-fi Sounds](https://kenney.nl/assets/sci-fi-sounds) | CC0 | Ghép transient với thruster tăng pitch 0,82–1,28; attack 0,05 giây và fade-out 0,22 giây. Âm từ xa vẫn phát 3D. |
| Bị trúng nổ / mất HP | `Assets/Resources/Audio/kenney_damage.ogg` | `impactPunch_heavy_000` | Kenney | [Impact Sounds](https://kenney.nl/assets/impact-sounds) | CC0 | Đổi tên; tăng ưu tiên cho người bị trúng và duck nhạc. |
| Thắng / thua | `Assets/Resources/Audio/sfx_victory.wav`, `sfx_defeat.wav` | SFX Victory / Defeat | Nhóm Bomb Me Baby | `scripts/generate_sfx.ps1` | Tự tạo cho đồ án | Hai chuỗi nốt đi lên/đi xuống. |
| Nút bấm UI | `Assets/Resources/Audio/kenney_ui_click.ogg` | `click_003` | Kenney | [Interface Sounds](https://kenney.nl/assets/interface-sounds) | CC0 | Đổi tên, phát 2D. |

Nhạc, charge loop, tín hiệu bắt đầu trận và thắng/thua vẫn là PCM mono 44,1 kHz, 16-bit do nhóm tạo bằng `scripts/generate_sfx.ps1`. Các SFX Kenney là OGG từ các gói gốc CC0; bản giấy phép lưu tại `docs/licenses/KENNEY_CC0.txt`. Các WAV tự tạo cũ vẫn được giữ trong repository làm tài nguyên dự phòng nhưng không còn được `BombAudio` nạp cho những sự kiện đã thay ở bảng trên.

## Bàn giao cho Trưởng nhóm

- [x] Đủ 11 SFX, có script tái tạo và thông tin nguồn/giấy phép.
- [x] SFX đã đặt tên rõ ràng, import trong Unity và tích hợp bằng `Assets/Scripts/BombAudio.cs`.
- [x] Thanh âm lượng **Hiệu ứng** điều khiển SFX trong game.
- [x] Đủ nhạc cho ba chương và menu; nhạc tự đổi theo map và hạ âm lượng khi có hiệu ứng lớn.
- [ ] Phần "Tài nguyên sử dụng" trong báo cáo đã chép lại bảng hoàn chỉnh.

Trạng thái ngày 21/10/2026: đã có 11 SFX và 4 vòng nhạc menu/map. Cần nghe thử trên bản Windows 2–4 client để chốt cân bằng âm lượng cuối.
