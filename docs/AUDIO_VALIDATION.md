# Kiểm tra âm thanh

## Bản sửa ngày 23/10/2026

Lượt tinh chỉnh tiếp theo: người dùng xác nhận âm thanh đã ổn nhưng tiếng bom còn nhẹ. Chỉ xử lý lại ba clip bom: mở dải 65–80 Hz đến 2–2,4 kHz, attack 6 ms, RMS mục tiêu −23 dBFS; gain gameplay 0,64–0,86. Độ lớn khi phát riêng tăng khoảng 3,7–4,6 dB so với lượt trước; giới hạn tổng gain 0,9 vẫn áp dụng khi nhiều tiếng chồng nhau. Hash của toàn bộ file audio/meta ngoài ba tiếng nổ không đổi. Chạy lại audit **142 kiểm tra qua**, build Windows **exit 0**, kiểm tra peak ngoại tuyến và cập nhật WAV nghe thử đều hoàn tất.

- Đổi ba tiếng nổ, bật khiên, khiên đỡ đòn và lọc lại tiếng tăng tốc từ bản gốc Kenney; bỏ ba lớp `mix_explosion_low`, `mix_shield_body`, `mix_boost_body`. Còn 20 clip được dùng. Nguồn và xử lý: [AUDIO_CREDITS.md](AUDIO_CREDITS.md).
- Chạy `scripts/validation/AudioAudit.cs` trong project độc lập `builds/audio-validation` bằng Unity 6000.3.6f1: **exit 0**, `AUDIO_AUDIT_PASS checks=142 clips=20`. Log: `builds/audio-validation.log`.
- Kiểm tra import, nhạc streaming, ánh xạ, stereo/khoảng cách, mute, về sảnh; thêm kiểm tra sáu clip mới nhập PCM, mẫu âm hữu hạn, peak dưới 0,4, biên đầu/cuối nhỏ hơn 0,01, kỹ năng phát một lần và không còn loop động cơ. Đây là kiểm tra Editor, chưa phải nghe trong trận.
- Sáu file sau encode có peak từ **−10,8 đến −8,6 dBFS**; riêng ba tiếng bom là **−9,0 / −9,0 / −10,8 dBFS**. Số đo từng file tại [AUDIO_LEVELS.csv](AUDIO_LEVELS.csv). FFmpeg decode thành công toàn bộ 20 clip.
- Kiểm tra ngoại tuyến cộng các giới hạn peak theo gain trong code, với các slider bằng 1: giới hạn gameplay **0,832**, kết quả **0,727** (mức toàn thang là 1). Kết quả tại `builds/audio-mix-check.json`; không tính overshoot khi đổi pitch/resample hoặc xử lý của thiết bị, không phải bản thu mixer Unity.
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build_client.ps1 -Isolated`: **exit 0**, `BOMB_BUILD_PASS 153727435 bytes`. Bản Windows mới: `builds/windows/BombMeBaby.exe`; log `builds/unity-build.log`. Ở lượt audio đã bỏ thủ công file nghỉ dùng khỏi bản sao do script cũ dùng `/E`. Hiện build/validation dùng chung `sync_unity_project.ps1` với mirror có kiểm tra đường dẫn; không cần thao tác xóa thủ công này. Con số build ở đây thuộc lượt audio; build gameplay mới nhất xem FINAL_VALIDATION.md.
- SHA-256 nhạc sảnh vẫn là `6C6808475E0BF3B2CCA02925EB0048DA3A8CA1E4C7FEBFAC2F6C180D081EA92D`. `git diff --check` qua.
- File nghe thử `builds/audio-preview.wav`: ba tiếng nổ → bật khiên → khiên đỡ đòn → tăng tốc; giữa mỗi tiếng có một giây nghỉ. File này phát mức asset gốc đã xử lý, chưa nhân gain gameplay.
- Người dùng đã xác nhận bản giảm rè ban đầu nghe ổn. Bản tăng lực bom tiếp theo chưa được người dùng nghe xác nhận; agent không nghe trên thiết bị thật hoặc chơi thử 2–4 client. Không suy ra cảm giác nghe chỉ từ peak và kiểm tra tự động. Unity từng có cảnh báo độ dài decode của nhạc `music_meadow` vốn không sửa trong lần này; audit và build đều hoàn tất.

## Lịch sử ngày 22/10/2026

- Unity 6000.3.6f1, project kiểm tra độc lập chỉ gồm `BombAudio.cs`, `BombSettings.cs`, 23 clip được nạp và `scripts/validation/AudioAudit.cs` đặt trong `Assets/Editor/`. Manifest dùng `com.unity.modules.audio: 1.0.0`.
- Chạy Unity `-batchmode -nographics -projectPath <project-kiểm-tra> -executeMethod AudioAudit.Run -logFile <log>`.
- Kết quả: **exit 0**, `AUDIO_AUDIT_PASS checks=94 clips=23`.
- Kiểm tra import/duration/sample rate, streaming nhạc, ánh xạ từng màn và nhạc sảnh, khoảng cách trên mặt đất, stereo trái/phải, nguồn khiên/boost, SFX mute, thắng/thua và dừng loop khi về sảnh. Đây là kiểm tra tự động trong Editor, không thay cho nghe trực tiếp trong trận.
- Build `Assembly-CSharp.csproj`: **0 lỗi**, 1 cảnh báo MSB3277 từ tham chiếu `System.Net.Http` của các package Unity AI.
- 23 tên `Resources.Load` đều có đúng một file và `.meta`; không trùng WAV/OGG cho ba track màn chơi.
- Toàn bộ 22 file mới đã decode lại bằng FFmpeg, không file nào đạt peak 0 dBFS. Chi tiết thời lượng/peak/RMS: [AUDIO_LEVELS.csv](AUDIO_LEVELS.csv).
- SHA-256 của `music_menu.mp3` giữ nguyên: `6C6808475E0BF3B2CCA02925EB0048DA3A8CA1E4C7FEBFAC2F6C180D081EA92D`.
- Chưa chạy nghe thử nhiều client trên thiết bị âm thanh thật. Checklist nghe nằm cuối [AUDIO_CREDITS.md](AUDIO_CREDITS.md).

Project kiểm tra tạm không được đưa vào game; không thêm nút hay chế độ chơi offline.

## Lịch sử dọn audio trước khi giảm từ 23 còn 20 clip

- Xóa 25 clip không dùng (4.081.133 byte): 15 WAV trong `AudioArchive/` và 10 OGG `kenney_*`, kèm `.meta` và thư mục archive rỗng. Bỏ script tổng hợp cũ.
- Trước khi xóa, quét GUID trong `Assets/` và `ProjectSettings/`: không có tham chiếu đến các asset bị xóa ngoài khai báo GUID của chính chúng.
- Sau khi xóa, cả 23 tên clip được `BombAudio` nạp đều có đúng một file và `.meta`. SHA-256 của toàn bộ 23 clip và 23 `.meta` không đổi.
- Script `prepare_game_audio.ps1` lấy ba bản gốc Interface Sounds từ `SourceRoot/interface/Audio/`; kiểm tra cú pháp PowerShell thành công. Không chạy lại xử lý audio hoặc Unity cho lần dọn này.
