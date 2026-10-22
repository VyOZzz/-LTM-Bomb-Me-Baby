param(
    [ValidateRange(1,65535)][int]$Port = 7777,
    [ValidateSet('baseline','optimized')][string]$SyncMode = 'optimized',
    [ValidateSet('','meadow','crossroads','crater')][string]$DemoMap = ''
)
$ErrorActionPreference = 'Stop'
$serverRoot = Join-Path (Split-Path $PSScriptRoot -Parent) 'server'
Push-Location $serverRoot
try {
    mvn -q package '-DskipTests'
    if ($LASTEXITCODE -ne 0) { throw 'Server build failed.' }
    & java "-Dsync.mode=$SyncMode" "-Ddemo.map=$DemoMap" -jar target/bomb-me-baby-server-1.0.0.jar $Port
    if ($LASTEXITCODE -ne 0) { throw 'Server stopped with an error. Check the log above.' }
} finally { Pop-Location }
