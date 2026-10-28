"""Measure paired seeded trials, alternating mode order; retain raw CSV and provenance."""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import platform
import socket
import subprocess
import sys
import time

# Also support the Windows embedded Python distribution used by the project.
sys.path.insert(0, str(Path(__file__).resolve().parent))
from load_test import git_commit, server_source_hash


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--modes', nargs='+', choices=['baseline', 'optimized'], default=['optimized', 'baseline'])
    parser.add_argument('--clients', nargs='+', type=int, default=[2, 4, 8])
    parser.add_argument('--repeats', type=int, default=3)
    parser.add_argument('--duration', type=int, default=60)
    parser.add_argument('--seed', type=int, default=1, help='first movement/room seed; increment per repeat')
    parser.add_argument('--out-dir', default='experiments/run-' + time.strftime('%Y%m%d-%H%M%S'))
    parser.add_argument('--port', type=int, default=17777)
    parser.add_argument('--map', choices=['meadow', 'crossroads', 'crater'], default='meadow')
    parser.add_argument('--java', default='java')
    args = parser.parse_args()
    if (args.repeats < 1 or args.duration < 1 or any(n < 2 or n > 10 for n in args.clients)
            or not 1 <= args.port <= 65535 or len(set(args.modes)) != len(args.modes)
            or not -(2**63) <= args.seed <= 2**63 - args.repeats):
        parser.error('Invalid benchmark dimensions, port, seed or duplicate modes')
    root = Path(__file__).resolve().parent.parent
    output = root / args.out_dir
    if output.exists() and any(output.iterdir()):
        parser.error('Output directory is not empty; choose another --out-dir to preserve evidence')
    jar = root / 'server/target/bomb-me-baby-server-1.0.0.jar'
    if not jar.is_file():
        parser.error('Run mvn package first, or use scripts/run_benchmark.ps1')
    output.mkdir(parents=True, exist_ok=True)
    flags = subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0
    properties = subprocess.run([args.java, '-XshowSettings:properties', '-version'],
                                capture_output=True, text=True, check=True, creationflags=flags)
    java_home = next(line.split('=', 1)[1].strip() for line in properties.stderr.splitlines() if 'java.home =' in line)
    java = str(Path(java_home) / 'bin' / ('java.exe' if os.name == 'nt' else 'java'))
    metadata = {
        'started_at': datetime.datetime.now().isoformat(timespec='seconds'),
        'commit': git_commit(), 'server_source_sha256': server_source_hash(),
        'server_jar_sha256': hashlib.sha256(jar.read_bytes()).hexdigest(),
        'python': sys.version, 'platform': platform.platform(),
        'java_version': '\n'.join(properties.stderr.strip().splitlines()[-3:]),
        'arguments': vars(args), 'order': 'alternate each pair; first pair uses --modes order',
        'server_lifecycle': 'fresh JVM for every trial', 'trials': [],
    }
    for count in args.clients:
        for repeat in range(args.repeats):
            pair_index = len(metadata['trials']) // len(args.modes)
            modes = args.modes if pair_index % 2 == 0 else list(reversed(args.modes))
            seed = args.seed + repeat
            for mode in modes:
                if (server_source_hash() != metadata['server_source_sha256']
                        or hashlib.sha256(jar.read_bytes()).hexdigest() != metadata['server_jar_sha256']):
                    raise RuntimeError('Server source/JAR changed during benchmark; start a fresh run')
                with socket.socket() as probe:
                    if probe.connect_ex(('127.0.0.1', args.port)) == 0:
                        raise RuntimeError(f'Port {args.port} already used; choose --port')
                trial = {'mode': mode, 'clients': count, 'seed': seed, 'status': 'running'}
                metadata['trials'].append(trial)
                metadata_path = output / 'metadata.json'
                metadata_path.write_text(json.dumps(metadata, indent=2), encoding='utf-8')
                log_path = output / f'server-{mode}-N{count}-seed{seed}.log'
                with log_path.open('w', encoding='utf-8') as log:
                    server = subprocess.Popen([java, f'-Dsync.mode={mode}', f'-Ddemo.map={args.map}',
                        f'-Dgame.seed={seed}', '-jar', str(jar), str(args.port)],
                        stdout=log, stderr=subprocess.STDOUT, creationflags=flags)
                    try:
                        for _ in range(100):
                            if server.poll() is not None:
                                raise RuntimeError(f'Server failed: {log_path}')
                            if 'server on TCP port' in log_path.read_text(encoding='utf-8'):
                                break
                            time.sleep(.1)
                        else:
                            raise TimeoutError('Server did not become ready')
                        print(f'{mode} N={count} seed={seed}', flush=True)
                        subprocess.run([sys.executable, str(root / 'scripts/load_test.py'), '--port', str(args.port),
                            '--clients', str(count), '--duration', str(args.duration), '--seed', str(seed),
                            '--expect-game-seed', str(seed), '--label', mode, '--out-dir', str(args.out_dir)],
                            cwd=root, check=True, creationflags=flags)
                        trial['status'] = 'passed'
                    except BaseException:
                        trial['status'] = 'failed'
                        raise
                    finally:
                        metadata_path.write_text(json.dumps(metadata, indent=2), encoding='utf-8')
                        server.terminate()
                        try:
                            server.wait(timeout=10)
                        except subprocess.TimeoutExpired:
                            server.kill()
                            server.wait()


if __name__ == '__main__':
    main()
