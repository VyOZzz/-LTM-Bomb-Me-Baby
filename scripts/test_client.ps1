$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$testRoot = Join-Path $repoRoot 'server/target/client-regression'
New-Item -ItemType Directory -Force -Path $testRoot | Out-Null
$env:DOTNET_CLI_HOME = Join-Path $env:TEMP 'bombmebaby-dotnet-cli'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$sources = @('scripts/validation/NetworkRegression.cs', 'LTM-Bomb-Me-Baby/Assets/Scripts/BombNetwork.cs', 'LTM-Bomb-Me-Baby/Assets/Scripts/BombProtocol.cs')
$includes = ($sources | ForEach-Object { '<Compile Include="' + [System.Security.SecurityElement]::Escape((Join-Path $repoRoot $_)) + '" />' }) -join "`n"
$project = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup>
  <ItemGroup>$includes</ItemGroup>
</Project>
"@
$projectPath = Join-Path $testRoot 'ClientRegression.csproj'
[IO.File]::WriteAllText($projectPath, $project)
dotnet run --project $projectPath --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw 'Client regression checks failed.' }
