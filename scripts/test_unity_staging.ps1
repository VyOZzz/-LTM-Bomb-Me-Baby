$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$fixture = Join-Path $repo ('builds/staging-test-' + [guid]::NewGuid().ToString('N'))
$source = Join-Path $fixture 'source'
$target = Join-Path $fixture 'target'
$sync = Join-Path $PSScriptRoot 'sync_unity_project.ps1'
foreach ($folder in @('Assets', 'Packages', 'ProjectSettings')) {
    New-Item -ItemType Directory -Path (Join-Path $source $folder) -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $source "$folder/kept.txt") -Value 'current'
}
New-Item -ItemType Directory -Path (Join-Path $target 'Library') -Force | Out-Null
Set-Content -LiteralPath (Join-Path $target 'Library/cache.txt') -Value 'keep cache'
& powershell -NoProfile -ExecutionPolicy Bypass -File $sync -SourceProject $source -Destination $target
if ($LASTEXITCODE -ne 0) { throw 'First stage failed' }
foreach ($folder in @('Assets', 'Packages', 'ProjectSettings')) {
    Set-Content -LiteralPath (Join-Path $target "$folder/deleted.txt") -Value 'stale'
}
& powershell -NoProfile -ExecutionPolicy Bypass -File $sync -SourceProject $source -Destination $target
if ($LASTEXITCODE -ne 0) { throw 'Second stage failed' }
foreach ($folder in @('Assets', 'Packages', 'ProjectSettings')) {
    if (Test-Path -LiteralPath (Join-Path $target "$folder/deleted.txt")) { throw "Stale $folder file survived" }
    if (!(Test-Path -LiteralPath (Join-Path $target "$folder/kept.txt"))) { throw "Missing $folder source file" }
}
if (!(Test-Path -LiteralPath (Join-Path $target 'Library/cache.txt'))) { throw 'Cache lost' }
# Reject unsafe paths without touching the real project.
foreach ($invalid in @($repo, (Join-Path $repo 'builds'), $source, (Join-Path $source 'nested'))) {
    $ErrorActionPreference = 'Continue'
    & powershell -NoProfile -ExecutionPolicy Bypass -File $sync -SourceProject $source -Destination $invalid 2>&1 | Out-Null
    $ErrorActionPreference = 'Stop'
    if ($LASTEXITCODE -eq 0) { throw "Unsafe path accepted: $invalid" }
}
Write-Host "STAGING_TEST_PASS: mirror removes stale files, preserves Library, rejects unsafe paths. Fixture: $fixture"
