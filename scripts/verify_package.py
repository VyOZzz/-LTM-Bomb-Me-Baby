"""Verify all manifest hashes and local Report Markdown links in an extracted package."""
import argparse
import hashlib
import json
from pathlib import Path
import re
from urllib.parse import unquote, urlsplit


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('package', type=Path, help='extracted directory containing MANIFEST.json')
    args = parser.parse_args()
    root = args.package.resolve()
    entries = json.loads((root / 'MANIFEST.json').read_text(encoding='utf-8'))
    seen = set()
    for entry in entries:
        relative = entry['path']
        path = (root / relative).resolve()
        if not path.is_relative_to(root) or relative in seen:
            raise RuntimeError(f'Unsafe or duplicate manifest entry: {relative}')
        seen.add(relative)
        if not path.is_file() or path.stat().st_size != entry['bytes']:
            raise RuntimeError(f'Missing or wrong-sized file: {relative}')
        if hashlib.sha256(path.read_bytes()).hexdigest() != entry['sha256']:
            raise RuntimeError(f'SHA-256 mismatch: {relative}')
    actual = {p.relative_to(root).as_posix() for p in root.rglob('*') if p.is_file()}
    if actual != seen | {'MANIFEST.json'}:
        raise RuntimeError(f'Unexpected/missing files: {actual ^ (seen | {"MANIFEST.json"})}')
    link_count = 0
    for document in (root / 'Report').glob('*.md'):
        for target in re.findall(r'\[[^\]\n]*\]\(([^\s)]+)\)', document.read_text(encoding='utf-8')):
            url = urlsplit(target)
            if not url.scheme and not url.netloc and url.path:
                resolved = (document.parent / unquote(url.path)).resolve()
                if not resolved.is_relative_to(root) or not resolved.exists():
                    raise RuntimeError(f'Broken/outside link in {document.name}: {target}')
                link_count += 1
    for required in ('Source/README.md', 'Source/server/pom.xml', 'Source/scripts/network_smoke.py',
                     'Playable/Server/bomb-me-baby-server-1.0.0.jar', 'Report/measurements/summary.csv'):
        if required not in seen:
            raise RuntimeError(f'Required file absent: {required}')
    status = json.loads((root / 'PACKAGE_STATUS.json').read_text(encoding='utf-8'))
    if status['windows_player_included']:
        for required in ('Playable/Client/BombMeBaby.exe', 'Playable/Client/UnityPlayer.dll',
                         'Playable/Client/BombMeBaby_Data/Managed/Assembly-CSharp.dll'):
            if required not in seen:
                raise RuntimeError(f'Incomplete Windows player: {required}')
    print(json.dumps({'result': 'PASS', 'files_checked': len(entries), 'local_links_checked': link_count,
                      'package_status': status}, indent=2))


if __name__ == '__main__':
    main()
