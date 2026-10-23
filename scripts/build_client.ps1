param(
    [string]$Unity = 'C:/Program Files/Unity/Hub/Editor/6000.3.6f1/Editor/Unity.exe',
    [switch]$Isolated
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$project = Join-Path $repoRoot 'LTM-Bomb-Me-Baby'
$output = Join-Path $repoRoot 'builds/windows/BombMeBaby.exe'
$log = Join-Path $repoRoot 'builds/unity-build.log'
New-Item -ItemType Directory -Force -Path (Split-Path $log -Parent) | Out-Null
if ($Isolated) {
    $copy = Join-Path $repoRoot 'builds/unity-validation'
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'sync_unity_project.ps1') -SourceProject $project -Destination $copy
    if ($LASTEXITCODE -ne 0) { throw 'Unity staging failed' }
    $project = $copy
}
$arguments = @('-batchmode', '-nographics', '-quit', '-projectPath', ('"' + $project + '"'), '-buildTarget', 'Win64',
    '-executeMethod', 'BombBuild.Windows', '-bombBuildOutput', ('"' + $output + '"'), '-logFile', ('"' + $log + '"'))
$buildProcess = Start-Process -FilePath $Unity -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
if ($buildProcess.ExitCode -ne 0) { throw "Unity build failed; see $log" }
Write-Host "Windows build: $output"
