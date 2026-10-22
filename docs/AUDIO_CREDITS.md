# Nguồn và xử lý âm thanh — Bomb Me Baby

Cập nhật 22/10/2026. Nhạc/SFX đang dùng là bản ghi tải từ các tác giả dưới đây; không dùng âm tổng hợp trong gameplay. Codex hỗ trợ tìm nguồn, xử lý file và tích hợp code. Tất cả đường dẫn audio trong bảng tính từ `LTM-Bomb-Me-Baby/Assets/Resources/Audio/`.

## Nhạc theo màn

| Map | Bài nhạc / tác giả / nguồn | File gốc → file sử dụng | Không khí | Giấy phép |
| --- | --- | --- | --- | --- |
| `meadow` | [A New Day — SpiderDave](https://opengameart.org/content/a-new-day) | `49_0.ogg` → `music_meadow.ogg` (88,6 giây) | Phiêu lưu vui, sáng, phù hợp màn mở đầu đồng cỏ | CC0 |
| `crossroads` | [Assembly Not Required — AndyGun11](https://opengameart.org/content/assembly-not-required-dark-synthwave-electronic-body-music) | `assembly_not_required.ogg` → `music_crossroads.ogg` (38,4 giây) | Synthwave/EBM, nhịp cơ khí cho màn giao tranh giữa hành trình | CC-BY 4.0 |
| `crater` | [JRPG Epic Rock Battle Theme #1 — HydroGene](https://opengameart.org/content/jrpg-epic-rock-battle-theme-1) | `jrpg_battle_loop.mp3` → `music_crater.ogg` (113,7 giây) | Rock chiến đấu dồn dập cho màn cuối | CC0 |
| Sảnh/menu | File do người dùng cung cấp | `ssstik.io_1792657165746_1.mp3` → `music_menu.mp3` | Giữ nguyên bài nhạc và mức phát sảnh | Tác giả/giấy phép chưa xác định |

Ba track màn chơi được cân theo mục tiêu **−18 LUFS / true peak tối đa −3 dBTP** bằng đo hai lượt, chuyển OGG 44,1 kHz. Làm mềm biên 8 ms để giảm click tại chỗ lặp. Unity phát streaming; chuyển giữa hai nguồn nhạc trong 1,2 giây. File sảnh không qua xử lý.

SHA-256 nhạc sảnh trước/sau: `6C6808475E0BF3B2CCA02925EB0048DA3A8CA1E4C7FEBFAC2F6C180D081EA92D`.

## Hiệu ứng

| Sự kiện | File sử dụng | Bản ghi gốc | Nguồn / tác giả / giấy phép |
| --- | --- | --- | --- |
| Nạp bom | `mix_charge.ogg` | `spaceEngineSmall_000.ogg` | [Sci-fi Sounds — Kenney, CC0](https://kenney.nl/assets/sci-fi-sounds) |
| Bom nổ: ba biến thể | `mix_explosion_1/2/3.ogg` | `explosionCrunch_004/002/001.ogg` | Sci-fi Sounds — Kenney, CC0 |
| Thân âm trầm khi nổ | `mix_explosion_low.ogg` | `lowFrequency_explosion_000.ogg` | Sci-fi Sounds — Kenney, CC0 |
| Nhặt năng lượng | `mix_energy.ogg` | `confirmation_003.ogg` | [Interface Sounds — Kenney, CC0](https://kenney.nl/assets/interface-sounds) |
| Nhặt Core / Tim Lửa | `mix_core.ogg` | `maximize_006.ogg` | Interface Sounds — Kenney, CC0 |
| Bật khiên | `mix_shield.ogg` | `forceField_003.ogg` | Sci-fi Sounds — Kenney, CC0 |
| Lớp điện duy trì khiên | `mix_shield_body.ogg` | `engineCircular_002.ogg` | Sci-fi Sounds — Kenney, CC0 |
| Khiên đỡ đòn | `mix_shield_hit.ogg` | `impactGlass_medium_002.ogg` | [Impact Sounds — Kenney, CC0](https://kenney.nl/assets/impact-sounds) |
| Lướt/tăng tốc | `mix_boost.ogg` | `knifeSlice.ogg` | [RPG Audio — Kenney, CC0](https://kenney.nl/assets/rpg-audio) |
| Lớp khí đẩy khi tăng tốc | `mix_boost_body.ogg` | `thrusterFire_002.ogg` | Sci-fi Sounds — Kenney, CC0 |
| Bị đánh / phản sát thương | `mix_damage.ogg` | `impactPunch_heavy_000.ogg` | Impact Sounds — Kenney, CC0 |
| Tiếp đất sau khi bị hất | `mix_land_1/2.ogg` | `impactSoft_heavy_000/001.ogg` | Impact Sounds — Kenney, CC0 |
| Core xuất hiện; Tim Lửa xuất hiện / rơi / đặt lên bệ | `mix_drop.ogg` | `handleCoins2.ogg` | RPG Audio — Kenney, CC0 |
| Chiến thắng | `mix_victory.ogg` | `VICTORY_0.ogg` (3,9 giây) | [Fanfares — Spring/Spring Enterprises, CC-BY 4.0](https://opengameart.org/content/fanfares) |
| Thua cuộc | `mix_defeat.ogg` | `LOSE_0.ogg` (3,2 giây) | Fanfares — Spring/Spring Enterprises, CC-BY 4.0 |
| Nút UI | `mix_ui.ogg` | `click_003.ogg` | Interface Sounds — Kenney, CC0 |

SFX được lọc bớt âm cao chói/hạ âm siêu trầm, cân RMS mục tiêu −23 dBFS và peak mục tiêu tối đa −6 dBFS trước encode; mức thực sau encode nằm trong [AUDIO_LEVELS.csv](AUDIO_LEVELS.csv). Đầu/cuối file có fade ngắn; khiên/boost có đường cong vào và tắt riêng. Pitch tiếng nổ/nhặt/lướt/tiếp đất biến thiên nhẹ giữa lần phát. Nhạc kết quả dùng cân loudness như nhạc màn chơi.

Bộ trộn dùng tối đa 12 voice gần/người chơi và 8 voice xa, giới hạn tổng gain SFX để chừa khoảng cho nhạc và loop, chặn sự kiện trùng quá sát nhau. Tiếng xa giảm theo khoảng cách trên mặt đất, pan theo chiều ngang màn hình. Âm nhặt năng lượng không làm nhạc lên/xuống liên tục; nổ bom, sát thương và kết quả mới ưu tiên hạ nhạc. SFX slider cập nhật cả âm đang phát. Không có âm báo bắt đầu trận.

Ghi công khi đưa vào báo cáo/build: **Assembly Not Required by AndyGun11**; **Fanfares by Spring/Spring Enterprises**; **A New Day by SpiderDave**; **JRPG Epic Rock Battle Theme #1 by HydroGene**; **Sound effects by Kenney (kenney.nl)**. [CC-BY 4.0](https://creativecommons.org/licenses/by/4.0/) / [CC0](https://creativecommons.org/publicdomain/zero/1.0/). Các bản sử dụng đã được cân âm, lọc và đổi định dạng như mô tả trên. Giấy phép Kenney có tại `docs/licenses/KENNEY_CC0.txt`.

## Tái tạo bản audio đã xử lý

Script `scripts/prepare_game_audio.ps1` dùng FFmpeg và ffprobe. Chuẩn bị một thư mục nguồn:

- `meadow.ogg`: https://opengameart.org/sites/default/files/49_0.ogg
- `crossroads.ogg`: https://opengameart.org/sites/default/files/assembly_not_required.ogg
- `crater.mp3`: https://opengameart.org/sites/default/files/jrpg_battle_loop.mp3
- `victory.ogg`: https://opengameart.org/sites/default/files/VICTORY_0.ogg
- `defeat.ogg`: https://opengameart.org/sites/default/files/LOSE_0.ogg
- `scifi/`, `impact/`, `rpg/`, `interface/`: giải nén các gói từ trang Kenney tương ứng ở bảng trên, giữ thư mục `Audio/`.
- Đặt các bản gốc trong thư mục nguồn riêng ngoài `Assets/`; repository chỉ giữ bản audio đã xử lý dùng trong game.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/prepare_game_audio.ps1 -SourceRoot C:\audio-sources -Ffmpeg C:\ffmpeg\bin\ffmpeg.exe -Ffprobe C:\ffmpeg\bin\ffprobe.exe
```

Đã xóa 15 WAV tự tổng hợp cũ trong `Assets/AudioArchive/`, 10 OGG `kenney_*` trước xử lý và script tổng hợp cũ. Thư mục `Resources/Audio/` còn đúng 23 clip đang dùng: 19 `mix_*`, ba `music_*.ogg` và `music_menu.mp3`. Các file `.meta` của clip đang dùng được giữ nguyên.

## Kiểm chứng nghe trong trận

Đã lưu số đo từng clip; cần nghe trên thiết bị thật với 2–4 client để chốt cảm giác nghe. Kiểm tra: chuyển ba chương không giật nhạc; nổ đồng thời không át nhạc kết quả; nhặt nhiều orb không dồn tiếng quá to; khiên và lướt kết thúc êm; tiếp đất và rơi Tim Lửa chỉ phát khi có sự kiện; về sảnh trở lại đúng MP3 người dùng chọn. Không thêm chế độ chơi offline.
