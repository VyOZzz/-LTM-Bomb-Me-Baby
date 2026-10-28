"""Package the working tree, Java server, review documents and optionally a Windows player.
Does not commit, publish or submit. Build first; --source-only omits the Windows player.
"""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
from urllib.parse import quote, unquote, urlsplit, urlunsplit
import zipfile


MARKDOWN_LINK = re.compile(r'(\[[^\]\n]*\]\()([^\s)]+)(\))')


def repair_report_links(report):
    """Keep Report links local when copied; resolve other docs via packaged Source."""
    for document in report.glob('*.md'):
        def repair(match):
            url = urlsplit(match[2])
            if url.scheme or url.netloc or not url.path:
                return match[0]
            if (document.parent / unquote(url.path)).exists():
                return match[0]
            source_target = report.parent / 'Source/docs' / unquote(url.path)
            if not source_target.exists():
                raise RuntimeError(f'Broken Report link in {document.name}: {match[2]}')
            relative = Path(os.path.relpath(source_target, document.parent)).as_posix()
            href = urlunsplit(('', '', quote(relative, safe='/'), url.query, url.fragment))
            return match[1] + href + match[3]
        document.write_text(MARKDOWN_LINK.sub(repair, document.read_text(encoding='utf-8')), encoding='utf-8')
    # Verify the final layout, not just the source documents.
    for document in report.glob('*.md'):
        for match in MARKDOWN_LINK.finditer(document.read_text(encoding='utf-8')):
            url = urlsplit(match[2])
            if not url.scheme and not url.netloc and url.path and not (document.parent / unquote(url.path)).exists():
                raise RuntimeError(f'Broken packaged link in {document.name}: {match[2]}')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--name', default='BombMeBaby-Review-' + datetime.date.today().isoformat())
    parser.add_argument('--experiments-dir', default='experiments/final-2026-10-28')
    parser.add_argument('--source-only', action='store_true', help='include source and server JAR without a Windows player')
    parser.add_argument('--report-docx', type=Path, help='optional reviewed Word report to include')
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
    if not jar.exists() or (not args.source_only and not (player / 'BombMeBaby.exe').exists()):
        parser.error('Build the Windows player and run mvn test package first')
    measurements = (root / args.experiments_dir).resolve()
    if not measurements.is_relative_to((root / 'experiments').resolve()) or not (measurements / 'summary.csv').is_file():
        parser.error('--experiments-dir must name measurements inside experiments/ with summary.csv')
    if args.report_docx and (args.report_docx.suffix.lower() != '.docx' or not args.report_docx.is_file()):
        parser.error('--report-docx must name an existing .docx file')
    files = subprocess.check_output(['git', 'ls-files', '-z', '--cached', '--others', '--exclude-standard'], cwd=root).decode('utf-8').split('\0')
    allowed = ('server/src/', 'scripts/', 'docs/', 'experiments/',
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
    if not args.source_only:
        shutil.copytree(player, output / 'Playable/Client')
    server = output / 'Playable/Server'
    server.mkdir(parents=True)
    shutil.copy2(jar, server / jar.name)
    (server / 'StartServer.cmd').write_text('@echo off\ncd /d "%~dp0"\njava -Dsync.mode=optimized -jar bomb-me-baby-server-1.0.0.jar 7777\npause\n', encoding='ascii')
    if not args.source_only:
        (output / 'Playable/Client/server.txt').write_text('127.0.0.1:7777\n', encoding='ascii')
    report = output / 'Report'
    report.mkdir()
    for filename in ('AUDIO_CREDITS.md', 'ASSET_CREDITS.md'):
        shutil.copy2(root / 'docs' / filename, report / filename)
    shutil.copytree(root / 'docs/validation', report / 'validation')
    shutil.copytree(root / 'docs/images', report / 'images')
    shutil.copytree(measurements, report / 'measurements')
    if args.report_docx:
        shutil.copy2(args.report_docx, report / 'Nhom07_BombMeBaby_Report.docx')
    repair_report_links(report)
    introduction = '''# Bomb Me Baby — bản review để nhóm kiểm tra

'''
    if args.source_only:
        introduction += '''**Gói source + server, chưa kèm Windows player.** Cần build client từ source tương ứng trước khi bàn giao bản chơi hoàn chỉnh.

1. Máy chạy server cần Java 17+. Mở `Playable/Server/StartServer.cmd`.
2. Muốn chơi: kích hoạt Unity Editor 6000.3.6f1 hợp lệ, build từ `Source/` theo `Source/README.md`, rồi dùng cả thư mục client được tạo.
3. Có thể kiểm tra server ngay bằng `python Source/scripts/network_smoke.py 7777` (Python 3.10+).

'''
    else:
        introduction += '''

1. Máy chạy server cần Java 17+. Mở `Playable/Server/StartServer.cmd` và giữ cửa sổ này.
2. Mở `Playable/Client/BombMeBaby.exe`. Chọn chế độ, tạo phòng và gửi mã cho người khác.
3. Người khác nhập IP máy server trong cài đặt mạng (hoặc sửa server.txt cạnh game), rồi nhập mã phòng. Cùng mode tìm trận sẽ được ghép vào phòng riêng của hàng chờ.
4. Điều khiển: WASD, giữ/thả Space nạp/nổ, E khiên, Shift tăng tốc, Q thả mảnh, F3 mạng. Nhạc sảnh giữ nguyên.

`Source/README.md` có lệnh build/test và cách kết nối LAN. Gửi nguyên thư mục Client, không chỉ EXE.

'''
    introduction += '''Report chứa bằng chứng kiểm thử và số đo, chưa có kết quả Compilatio hay kiểm tra trên máy thứ hai. File Word chỉ được kèm khi chỉ định --report-docx; nhóm cần hoàn thiện tỷ lệ đóng góp và xác nhận bản nộp. Gói này chưa được nộp lên dịch vụ nào.

Kiểm tra toàn vẹn sau khi giải nén: `python Source/scripts/verify_package.py .` từ thư mục chứa START_HERE.md. MANIFEST.json ghi SHA-256 của mọi file được đóng gói.
'''
    (output / 'START_HERE.md').write_text(introduction, encoding='utf-8')
    (output / 'PACKAGE_STATUS.json').write_text(json.dumps({
        'created_at': datetime.datetime.now().isoformat(timespec='seconds'),
        'windows_player_included': not args.source_only,
        'report_docx_included': args.report_docx is not None,
        'measurements': measurements.relative_to(root).as_posix(),
        'second_machine_verified': False,
        'compilatio_verified': False,
    }, indent=2), encoding='utf-8')
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
