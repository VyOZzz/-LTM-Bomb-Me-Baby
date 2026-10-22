# Kiểm tra bản audio ngày 22/10/2026

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

## Kiểm tra sau khi dọn audio thừa

- Xóa 25 clip không dùng (4.081.133 byte): 15 WAV trong `AudioArchive/` và 10 OGG `kenney_*`, kèm `.meta` và thư mục archive rỗng. Bỏ script tổng hợp cũ.
- Trước khi xóa, quét GUID trong `Assets/` và `ProjectSettings/`: không có tham chiếu đến các asset bị xóa ngoài khai báo GUID của chính chúng.
- Sau khi xóa, cả 23 tên clip được `BombAudio` nạp đều có đúng một file và `.meta`. SHA-256 của toàn bộ 23 clip và 23 `.meta` không đổi.
- Script `prepare_game_audio.ps1` lấy ba bản gốc Interface Sounds từ `SourceRoot/interface/Audio/`; kiểm tra cú pháp PowerShell thành công. Không chạy lại xử lý audio hoặc Unity cho lần dọn này.
