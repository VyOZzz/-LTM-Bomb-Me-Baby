"""Package the current working tree, Windows player, Java server and review documents.
Does not commit, publish or submit the project. Requires the build scripts to have succeeded.
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import zipfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--name', default='BombMeBaby-Review-2026-10-22')
    args = parser.parse_args()
    if not args.name or any(c not in 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_' for c in args.name):
        parser.error('Use a simple package name containing letters, digits, - or _')
    root = Path(__file__).resolve().parent.parent
    output = root / 'builds' / args.name
    archive = output.with_suffix('.zip')
    if output.exists() or archive.exists():
        parser.error('Output already exists; use another --name to preserve the earlier package')
    player = root / 'builds/windows'
    jar = root / 'server/target/bomb-me-baby-server-1.0.0.jar'
    if not (player / 'BombMeBaby.exe').exists() or not jar.exists():
        parser.error('Build the Windows player and run mvn test package first')
    files = subprocess.check_output(['git', 'ls-files', '-z', '--cached', '--others', '--exclude-standard'], cwd=root).decode('utf-8').split('\0')
    allowed = ('server/src/', 'scripts/', 'docs/', 'experiments/', 'final-project/',
        'LTM-Bomb-Me-Baby/Assets/', 'LTM-Bomb-Me-Baby/Packages/', 'LTM-Bomb-Me-Baby/ProjectSettings/')
    for relative in sorted(set(files)):
        if relative not in ('README.md', '.gitignore', 'server/pom.xml') and not relative.startswith(allowed):
            continue
        path = root / relative
        if not path.is_file():
            continue
        destination = output / 'Source' / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(path, destination)
    shutil.copytree(player, output / 'Playable/Client')
    server = output / 'Playable/Server'
    server.mkdir(parents=True)
    shutil.copy2(jar, server / jar.name)
    (server / 'StartServer.cmd').write_text('@echo off\ncd /d "%~dp0"\njava -Dsync.mode=optimized -jar bomb-me-baby-server-1.0.0.jar 7777\npause\n', encoding='ascii')
    (server / 'StartBots.cmd').write_text('@echo off\npowershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0StartBots.ps1"\npause\n', encoding='ascii')
    (server / 'StartBots.ps1').write_text("$roomCode = Read-Host 'Room code'\nif ($roomCode -notmatch '^[A-Fa-f0-9]{6}$') { throw 'Enter the 6-character room code.' }\n& java -cp (Join-Path $PSScriptRoot 'bomb-me-baby-server-1.0.0.jar') bombmebaby.PracticeBots 127.0.0.1 7777 $roomCode 3\n", encoding='ascii')
    (output / 'Playable/Client/server.txt').write_text('127.0.0.1:7777\n', encoding='ascii')
    report = output / 'Report'
    report.mkdir()
    for filename in ('REPORT_DRAFT.md', 'FINAL_VALIDATION.md', 'EXPERIMENTS_FINAL.md', 'AUDIO_CREDITS.md', 'ASSET_CREDITS.md'):
        shutil.copy2(root / 'docs' / filename, report / filename)
    shutil.copytree(root / 'docs/validation', report / 'validation')
    experiment_doc = report / 'EXPERIMENTS_FINAL.md'
    experiment_doc.write_text(experiment_doc.read_text(encoding='utf-8')
        .replace('../experiments/final-2026-10-22-v2/', 'measurements/')
        .replace('../experiments/final-2026-10-22/', '../Source/experiments/final-2026-10-22/'), encoding='utf-8')
    shutil.copytree(root / 'experiments/final-2026-10-22-v2', report / 'measurements')
    (output / 'START_HERE.md').write_text('''# Bomb Me Baby — bản review để nhóm kiểm tra

1. Máy chạy server cần Java 17+. Mở `Playable/Server/StartServer.cmd` và giữ cửa sổ này.
2. Mở `Playable/Client/BombMeBaby.exe`. Chọn chế độ, tạo phòng và gửi mã cho người khác.
3. Người khác nhập IP máy server trong cài đặt mạng (hoặc sửa server.txt cạnh game), rồi nhập mã phòng. Cùng mode tìm trận sẽ được ghép vào phòng riêng của hàng chờ.
4. Khi chỉ có một người, mở `Playable/Server/StartBots.cmd`, nhập mã trước khi sẵn sàng. Bot là client TCP và vẫn cần server.
5. Điều khiển: WASD, giữ/thả Space nạp/nổ, E khiên, Shift tăng tốc, Q thả mảnh, F3 mạng. Nhạc sảnh giữ nguyên.

`Source/README.md` có lệnh build/test và cách kết nối LAN. Gửi nguyên thư mục Client, không chỉ EXE.

Report chứa nội dung nháp và bằng chứng. Chưa có thông tin đầy đủ trên bìa, file Word đã duyệt, playtest người thật/LAN hoặc kết quả Compilatio. Nhóm cần hoàn thiện những mục này rồi đặt báo cáo đã nộp vào Report; không dùng bộ review để khẳng định đã nộp bài.
''', encoding='utf-8')
    manifest = []
    for path in sorted(output.rglob('*')):
        if path.is_file():
            manifest.append({'path': path.relative_to(output).as_posix(), 'bytes': path.stat().st_size,
                'sha256': hashlib.sha256(path.read_bytes()).hexdigest()})
    (output / 'MANIFEST.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as bundle:
        for path in sorted(output.rglob('*')):
            if path.is_file():
                bundle.write(path, args.name + '/' + path.relative_to(output).as_posix())
    with zipfile.ZipFile(archive) as bundle:
        if bundle.testzip() is not None:
            raise RuntimeError('ZIP verification failed')
    print(f'Package: {output}')
    print(f'ZIP: {archive} ({archive.stat().st_size} bytes)')
    print(f'Manifest: {len(manifest)} files')


if __name__ == '__main__':
    main()
