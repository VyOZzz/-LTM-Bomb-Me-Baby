"""Launch the packaged server, measure both sync modes on one fixed map, retain raw CSV."""
import argparse
import os
from pathlib import Path
import socket
import subprocess
import sys
import time


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--modes', nargs='+', choices=['baseline', 'optimized'], default=['baseline', 'optimized'])
    parser.add_argument('--clients', nargs='+', type=int, default=[2, 4, 8])
    parser.add_argument('--repeats', type=int, default=3)
    parser.add_argument('--duration', type=int, default=60)
    parser.add_argument('--out-dir', default='experiments/run-' + time.strftime('%Y%m%d-%H%M%S'))
    parser.add_argument('--port', type=int, default=17777)
    parser.add_argument('--map', choices=['meadow', 'crossroads', 'crater'], default='meadow')
    parser.add_argument('--java', default='java')
    args = parser.parse_args()
    if args.repeats < 1 or args.duration < 1 or any(n < 2 or n > 10 for n in args.clients):
        parser.error('Invalid benchmark dimensions')
    root = Path(__file__).resolve().parent.parent
    output = root / args.out_dir
    output.mkdir(parents=True, exist_ok=True)
    flags = subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0
    # Resolve the actual JVM: PATH can point to a launcher that exits before its child.
    properties = subprocess.run([args.java, '-XshowSettings:properties', '-version'], capture_output=True, text=True, check=True)
    java_home = next(line.split('=', 1)[1].strip() for line in properties.stderr.splitlines() if 'java.home =' in line)
    java = str(Path(java_home) / 'bin' / ('java.exe' if os.name == 'nt' else 'java'))
    for mode in args.modes:
        with socket.socket() as probe:
            if probe.connect_ex(('127.0.0.1', args.port)) == 0:
                raise RuntimeError(f'Port {args.port} already used; choose --port')
        log_path = output / f'server-{mode}.log'
        with log_path.open('w', encoding='utf-8') as log:
            server = subprocess.Popen([java, f'-Dsync.mode={mode}', f'-Ddemo.map={args.map}', '-jar',
                str(root / 'server/target/bomb-me-baby-server-1.0.0.jar'), str(args.port)],
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
                for count in args.clients:
                    for repeat in range(1, args.repeats + 1):
                        print(f'{mode} N={count} repeat={repeat}', flush=True)
                        subprocess.run([sys.executable, str(root / 'scripts/load_test.py'), '--port', str(args.port),
                            '--clients', str(count), '--duration', str(args.duration), '--seed', str(repeat),
                            '--label', mode, '--out-dir', args.out_dir], cwd=root, check=True)
            finally:
                server.terminate()
                try:
                    server.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    server.kill()
                    server.wait()


if __name__ == '__main__':
    main()
