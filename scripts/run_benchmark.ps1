# Run load_test.py against a fresh server for each sync mode, client count and repeat.
#
# Example (from the repo root, after `mvn compile` in server/):
#   powershell -ExecutionPolicy Bypass -File scripts/run_benchmark.ps1
#   powershell -ExecutionPolicy Bypass -File scripts/run_benchmark.ps1 -Clients 2 -Repeats 1 -Duration 5 -OutDir experiments-smoke
#
# Results: <OutDir>/summary.csv and <OutDir>/raw/. Server logs: %TEMP%\bomb-server-<mode>.log
param(
    [string[]]$Modes = @("baseline", "optimized"),
    [int[]]$Clients = @(2, 4, 8),
    [int]$Repeats = 3,
    [int]$Duration = 60,
    [string]$OutDir = "experiments",
    [string]$Label = ""
)
$ErrorActionPreference = "Stop"
$repo = Split-Path $PSScriptRoot -Parent
Set-Location $repo

$cpFile = "$repo\server\target\cp.txt"
if (-not (Test-Path $cpFile)) {
    Push-Location "$repo\server"
    mvn -q -B compile dependency:build-classpath "-Dmdep.outputFile=target/cp.txt"
    Pop-Location
}
$cp = "$repo\server\target\classes;" + (Get-Content $cpFile -Raw).Trim()

# The `java` on PATH may be a launcher shim that spawns the real JVM and exits, so
# stopping it would leave the server running. Resolve the real java.exe instead.
# (Through cmd: PowerShell 5.1 turns native stderr into terminating errors under "Stop".)
$javaHome = (cmd /c "java -XshowSettings:properties -version 2>&1" | Select-String "java.home").ToString().Split("=")[1].Trim()
$java = Join-Path $javaHome "bin\java.exe"

function Get-PortOwner {
    Get-NetTCPConnection -LocalPort 7777 -State Listen -ErrorAction SilentlyContinue
}

foreach ($mode in $Modes) {
    if (Get-PortOwner) { throw "port 7777 is already in use; stop that server first" }
    $log = Join-Path $env:TEMP "bomb-server-$mode.log"
    $server = Start-Process $java -ArgumentList "-Dsync.mode=$mode", "-cp", "`"$cp`"", "bombmebaby.BombServer", "7777" `
        -RedirectStandardOutput $log -RedirectStandardError "$log.err" -PassThru -WindowStyle Hidden
    try {
        $ready = $false
        for ($i = 0; $i -lt 50 -and -not $ready; $i++) {
            Start-Sleep -Milliseconds 200
            $ready = (Select-String -Path $log -Pattern "server on TCP port" -Quiet -ErrorAction SilentlyContinue)
        }
        if (-not $ready) { throw "server ($mode) did not start; see $log and $log.err" }
        if (-not (Select-String -Path $log -Pattern "active = $mode" -Quiet)) { throw "server is not in $mode mode" }
        Write-Host "== server up: $((Get-Content $log) -join ' | ')"
        $name = if ($Label) { $Label } else { $mode }
        foreach ($n in $Clients) {
            foreach ($r in 1..$Repeats) {
                Write-Host "== $mode N=$n run $r"
                python scripts/load_test.py --clients $n --duration $Duration --seed $r --label $name --out-dir $OutDir
                if ($LASTEXITCODE -ne 0) { Write-Host "!! run failed (exit $LASTEXITCODE)" }
            }
        }
    } finally {
        Stop-Process -Id $server.Id -Force -ErrorAction SilentlyContinue
        Get-PortOwner | ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }
        Start-Sleep -Seconds 1
    }
}
