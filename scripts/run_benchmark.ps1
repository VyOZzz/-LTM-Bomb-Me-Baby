# Fixed-map loopback measurements; each sync mode starts its own packaged server.
param(
    [string[]]$Modes = @("optimized", "baseline"),
    [int[]]$Clients = @(2, 4, 8),
    [int]$Repeats = 3,
    [int]$Duration = 60,
    [long]$Seed = 1,
    [string]$OutDir = "",
    [string]$Python = "python",
    [ValidateRange(1,65535)][int]$Port = 17777,
    [ValidateSet('meadow','crossroads','crater')][string]$Map = 'meadow'
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
if (-not $OutDir) { $OutDir = 'experiments/run-' + (Get-Date -Format 'yyyyMMdd-HHmmss') }
Push-Location (Join-Path $repoRoot 'server')
try {
    mvn -q -B package '-DskipTests'
    if ($LASTEXITCODE -ne 0) { throw 'Server package failed' }
} finally { Pop-Location }
$arguments = @((Join-Path $PSScriptRoot 'run_benchmark.py'), '--modes') + $Modes +
    @('--clients') + $Clients + @('--repeats', $Repeats, '--duration', $Duration,
    '--out-dir', $OutDir, '--port', $Port, '--map', $Map, '--seed', $Seed)
& $Python @arguments
if ($LASTEXITCODE -ne 0) { throw 'Benchmark failed; inspect the server logs in the output folder.' }
