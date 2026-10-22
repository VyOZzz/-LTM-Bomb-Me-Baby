"""Run opt-in Editor integration checks in an isolated project against the packaged server.
Prerequisites: mvn test package in server/, Unity 6000.3.6f1, Java 17+ and Python 3.10+.
This does not modify or close the developer's open Unity project.
"""
import argparse
import os
from pathlib import Path
import shutil
import subprocess
import time


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--unity", default="C:/Program Files/Unity/Hub/Editor/6000.3.6f1/Editor/Unity.exe")
    parser.add_argument("--java", default="java")
    parser.add_argument("--port", type=int, default=17778)
    parser.add_argument("--graphics", action="store_true", help="Render map screenshots with a graphics device")
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    build = root / "builds"
    project = build / "unity-validation"
    fixtures = root / "server/target/unity-fixtures.jsonl"
    if not fixtures.exists():
        parser.error("Run mvn test package in server/ first")
    for folder in ("Assets", "Packages", "ProjectSettings"):
        shutil.copytree(root / "LTM-Bomb-Me-Baby" / folder, project / folder, dirs_exist_ok=True)
    flags = subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0
    log_path = build / "unity-validation.log"
    with (build / "unity-test-server.log").open("w", encoding="utf-8") as server_log:
        server = subprocess.Popen([args.java, "-Dsync.mode=optimized", "-jar",
            str(root / "server/target/bomb-me-baby-server-1.0.0.jar"), str(args.port)],
            stdout=server_log, stderr=subprocess.STDOUT, creationflags=flags)
        try:
            for _ in range(50):
                if server.poll() is not None:
                    raise RuntimeError("Test server failed; see builds/unity-test-server.log")
                if "server on TCP port" in (build / "unity-test-server.log").read_text(encoding="utf-8"):
                    break
                time.sleep(.1)
            command = [args.unity, "-batchmode", "-projectPath", str(project),
                "-executeMethod", "BombValidation.Run", "-bombFixtures", str(fixtures),
                "-bombTestPort", str(args.port), "-logFile", str(log_path)]
            if args.graphics:
                command += ["-bombScreenshots", str(build / "map-screenshots")]
            else:
                command.append("-nographics")
            result = subprocess.run(command, timeout=600, creationflags=flags)
            text = log_path.read_text(encoding="utf-8", errors="replace")
            for line in text.splitlines():
                if "BOMB_" in line:
                    print(line)
            if result.returncode or "BOMB_VALIDATION_PASS" not in text:
                raise RuntimeError(f"Unity validation failed; see {log_path}")
        finally:
            server.terminate()
            try:
                server.wait(timeout=10)
            except subprocess.TimeoutExpired:
                server.kill()
                server.wait()


if __name__ == "__main__":
    main()
