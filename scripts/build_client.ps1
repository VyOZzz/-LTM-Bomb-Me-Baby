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
    foreach ($folder in @('Assets', 'Packages', 'ProjectSettings')) {
        & robocopy (Join-Path $project $folder) (Join-Path $copy $folder) /E /R:0 /W:0 /NFL /NDL /NJH /NJS /NP | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "Failed to copy $folder" }
    }
    $project = $copy
}
$arguments = @('-batchmode', '-nographics', '-quit', '-projectPath', ('"' + $project + '"'), '-buildTarget', 'Win64',
    '-executeMethod', 'BombBuild.Windows', '-bombBuildOutput', ('"' + $output + '"'), '-logFile', ('"' + $log + '"'))
$buildProcess = Start-Process -FilePath $Unity -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
if ($buildProcess.ExitCode -ne 0) { throw "Unity build failed; see $log" }
Write-Host "Windows build: $output"
