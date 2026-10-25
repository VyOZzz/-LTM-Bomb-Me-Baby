param(
    [Parameter(Mandatory=$true)][string]$SourceRoot,
    [Parameter(Mandatory=$true)][string]$Ffmpeg,
    [Parameter(Mandatory=$true)][string]$Ffprobe,
    [string[]]$Clips = @()
)
$ErrorActionPreference = 'Stop'
$culture = [Globalization.CultureInfo]::InvariantCulture
$repo = Split-Path $PSScriptRoot -Parent
$destination = Join-Path $repo 'LTM-Bomb-Me-Baby/Assets/Resources/Audio'
function Read-AudioLog([string]$inputPath, [string]$filter) {
    # Windows PowerShell treats native stderr (including FFmpeg statistics) as errors.
    $ErrorActionPreference = 'Continue'
    $log = (& $Ffmpeg -hide_banner -i $inputPath -af $filter -f null - 2>&1 | Out-String)
    if ($LASTEXITCODE -ne 0) { throw "FFmpeg failed: $inputPath" }
    return $log
}
# Sources and download links are documented in docs/AUDIO_CREDITS.md.
# This processes downloaded recordings; it does not synthesize new sounds.
$customMixes = @{
    'mix_explosion_1' = @{
        Sources = @('scifi/Audio/explosionCrunch_001.ogg', 'scifi/Audio/lowFrequency_explosion_001.ogg')
        Filter = '[0:a]atrim=0:1.25,highpass=f=65,lowpass=f=12000,volume=0.90,afade=t=in:st=0:d=0.004,afade=t=out:st=1.02:d=0.23[a];[1:a]atrim=0:1.25,lowpass=f=280,volume=0.48,afade=t=in:st=0:d=0.004,afade=t=out:st=0.95:d=0.30[b];[a][b]amix=inputs=2:duration=longest:normalize=0,alimiter=limit=0.50:attack=5:release=80,volume=0.70,aresample=44100'
    }
    'mix_explosion_2' = @{
        Sources = @('scifi/Audio/explosionCrunch_003.ogg', 'scifi/Audio/lowFrequency_explosion_000.ogg')
        Filter = '[0:a]atrim=0:1.50,highpass=f=60,lowpass=f=12500,volume=0.88,afade=t=in:st=0:d=0.004,afade=t=out:st=1.22:d=0.28[a];[1:a]atrim=0:1.50,lowpass=f=260,volume=0.44,afade=t=in:st=0:d=0.004,afade=t=out:st=1.15:d=0.35[b];[a][b]amix=inputs=2:duration=longest:normalize=0,alimiter=limit=0.50:attack=5:release=90,volume=0.70,aresample=44100'
    }
    'mix_explosion_3' = @{
        Sources = @('scifi/Audio/explosionCrunch_004.ogg', 'scifi/Audio/lowFrequency_explosion_001.ogg')
        Filter = '[0:a]atempo=1.18,atrim=0:1.55,highpass=f=70,lowpass=f=12000,volume=0.84,afade=t=in:st=0:d=0.004,afade=t=out:st=1.25:d=0.30[a];[1:a]atrim=0:1.55,lowpass=f=300,volume=0.52,afade=t=in:st=0:d=0.004,afade=t=out:st=1.10:d=0.45[b];[a][b]amix=inputs=2:duration=longest:normalize=0,alimiter=limit=0.50:attack=5:release=90,volume=0.70,aresample=44100'
    }
    'mix_victory' = @{
        Sources = @('jingles/Audio/Steel jingles/jingles_STEEL12.ogg', 'jingles/Audio/Hit jingles/jingles_HIT12.ogg', 'jingles/Audio/Pizzicato jingles/jingles_PIZZI12.ogg')
        Filter = '[0:a]volume=0.92[a];[1:a]volume=0.38[b];[2:a]volume=0.24,adelay=35|35[c];[a][b][c]amix=inputs=3:duration=longest:normalize=0,afade=t=in:st=0:d=0.008,afade=t=out:st=1.05:d=0.22,alimiter=limit=0.58:attack=5:release=80,volume=0.60,aresample=44100'
    }
    'mix_ui' = @{
        Sources = @('ui-audio/Audio/click1.ogg')
        Filter = '[0:a]highpass=f=180,lowpass=f=10000,volume=1.4,afade=t=out:st=0.065:d=0.028,alimiter=limit=0.55:attack=0.5:release=20,volume=0.65,aresample=44100'
    }
}
$items = @(
    @('music_meadow', 'meadow.ogg', 'music', ''),
    @('music_crossroads', 'crossroads.ogg', 'music', ''),
    @('music_crater', 'crater.mp3', 'music', ''),
    @('mix_victory', 'jingles/Audio/Steel jingles/jingles_STEEL12.ogg', 'result', ''),
    @('mix_defeat', 'defeat.ogg', 'result', ''),
    @('mix_charge', 'scifi/Audio/spaceEngineSmall_000.ogg', 'loop', 'highpass=f=100,lowpass=f=3500'),
    @('mix_explosion_1', 'scifi/Audio/explosionCrunch_001.ogg', 'soft', ''),
    @('mix_explosion_2', 'scifi/Audio/explosionCrunch_003.ogg', 'soft', ''),
    @('mix_explosion_3', 'scifi/Audio/explosionCrunch_004.ogg', 'soft', ''),
    @('mix_energy', 'interface/Audio/confirmation_003.ogg', 'sfx', 'lowpass=f=6500'),
    @('mix_core', 'interface/Audio/maximize_006.ogg', 'sfx', 'lowpass=f=6500'),
    @('mix_shield', 'interface/Audio/maximize_004.ogg', 'soft', 'highpass=f=180,lowpass=f=3800'),
    @('mix_shield_hit', 'interface/Audio/glass_001.ogg', 'soft', 'highpass=f=180,lowpass=f=3200'),
    @('mix_boost', 'rpg/Audio/knifeSlice.ogg', 'soft', 'highpass=f=220,lowpass=f=3200'),
    @('mix_damage', 'impact/Audio/impactPunch_heavy_000.ogg', 'sfx', 'lowpass=f=5000'),
    @('mix_land_1', 'impact/Audio/impactSoft_heavy_000.ogg', 'sfx', 'lowpass=f=4000'),
    @('mix_land_2', 'impact/Audio/impactSoft_heavy_001.ogg', 'sfx', 'lowpass=f=4000'),
    @('mix_drop', 'rpg/Audio/handleCoins2.ogg', 'sfx', 'lowpass=f=5500'),
    @('mix_ui', 'ui-audio/Audio/click1.ogg', 'sfx', '')
)
$report = @()
$activeNames = @($items | ForEach-Object { $_[0] })
foreach ($requested in $Clips) {
    if ($requested -notin $activeNames) { throw "Unknown clip: $requested" }
}
$reportPath = Join-Path $repo 'docs/AUDIO_LEVELS.csv'
if ($Clips.Count -gt 0 -and (Test-Path -LiteralPath $reportPath)) {
    $report = @(Import-Csv $reportPath | Where-Object { $_.Clip -in $activeNames -and $_.Clip -notin $Clips })
}
foreach ($item in $items) {
    $name, $relative, $kind, $tone, $maxDuration = $item
    if ($Clips.Count -gt 0 -and $name -notin $Clips) { continue }
    $outputPath = Join-Path $destination "$name.ogg"
    if ($customMixes.ContainsKey($name)) {
        $mix = $customMixes[$name]
        $inputArguments = @()
        foreach ($source in $mix.Sources) {
            $sourcePath = Join-Path $SourceRoot $source
            if (-not (Test-Path -LiteralPath $sourcePath)) { throw "Missing source: $sourcePath" }
            $inputArguments += @('-i', $sourcePath)
        }
        & $Ffmpeg -hide_banner -loglevel error -y @inputArguments -filter_complex $mix.Filter -ac 2 -ar 44100 -c:a libvorbis -q:a 5 $outputPath
        if ($LASTEXITCODE -ne 0) { throw "Encoding failed: $name" }
        $durationText = & $Ffprobe -v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 $outputPath
        $duration = [double]::Parse($durationText, $culture)
        $scan = Read-AudioLog $outputPath 'volumedetect'
        $report += [pscustomobject]@{
            Clip = $name; Seconds = [Math]::Round($duration,3)
            PeakDb = [regex]::Match($scan, 'max_volume: ([-\d.]+)').Groups[1].Value
            MeanDb = [regex]::Match($scan, 'mean_volume: ([-\d.]+)').Groups[1].Value
        }
        continue
    }
    $isExplosion = $name.StartsWith('mix_explosion_')
    $inputPath = if ([IO.Path]::IsPathRooted($relative)) { $relative } else { Join-Path $SourceRoot $relative }
    if (-not (Test-Path -LiteralPath $inputPath)) { throw "Missing source: $inputPath" }
    $durationText = & $Ffprobe -v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 $inputPath
    if ($LASTEXITCODE -ne 0) { throw "Cannot decode $inputPath" }
    $duration = [double]::Parse($durationText, $culture)
    if ($maxDuration) {
        $duration = [Math]::Min($duration, [double]$maxDuration)
        $tone += ',atrim=duration=' + $duration.ToString('F6', $culture) + ',asetpts=PTS-STARTPTS'
    }
    if ($kind -eq 'soft') {
        # Measure the finished envelope so a short attack is not normalized then faded away.
        $softFade = [Math]::Min(.12, $duration * .25)
        $softTail = ($duration - $softFade).ToString('F6', $culture)
        $softFadeText = $softFade.ToString('F6', $culture)
        $softAttack = if ($isExplosion) { '0.006' } else { '0.012' }
        $tone += ",afade=t=in:d=${softAttack},afade=t=out:st=${softTail}:d=${softFadeText}"
    }
    $filters = if ($tone) { $tone + ',' } else { '' }
    if ($kind -eq 'music' -or $kind -eq 'result') {
        # Measure first, then use linear loudness normalization to preserve dynamics.
        $measurement = Read-AudioLog $inputPath 'loudnorm=I=-18:TP=-3:LRA=11:print_format=json'
        $json = [regex]::Match($measurement, '(?s)\{\s*"input_i".*?\}').Value | ConvertFrom-Json
        $filters += "loudnorm=I=-18:TP=-3:LRA=11:measured_I=$($json.input_i):measured_TP=$($json.input_tp):measured_LRA=$($json.input_lra):measured_thresh=$($json.input_thresh):offset=$($json.target_offset):linear=true,"
    } else {
        $measureFilter = if ($tone) { $tone + ',volumedetect' } else { 'volumedetect' }
        $measurement = Read-AudioLog $inputPath $measureFilter
        $peak = [double]::Parse([regex]::Match($measurement, 'max_volume: ([-\d.]+)').Groups[1].Value, $culture)
        $mean = [double]::Parse([regex]::Match($measurement, 'mean_volume: ([-\d.]+)').Groups[1].Value, $culture)
        $peakTarget = if ($kind -eq 'soft') { -9 } else { -6 }
        $meanTarget = if ($kind -eq 'soft' -and -not $isExplosion) { -25 } else { -23 }
        $gain = [Math]::Min($peakTarget - $peak, $meanTarget - $mean).ToString('F2', $culture)
        $filters += "volume=${gain}dB,"
    }
    # Tiny fades remove edge clicks without fading out an entire musical phrase.
    $fade = if ($kind -eq 'music' -or $kind -eq 'loop') { .008 } else { .035 }
    $fade = [Math]::Min([double]$fade, $duration * .15)
    $tail = [Math]::Max(0.0, $duration - $fade).ToString('F6', $culture)
    $fadeText = $fade.ToString('F3', $culture)
    if ($kind -ne 'soft') { $filters += "afade=t=in:d=0.004,afade=t=out:st=${tail}:d=${fadeText}" }
    else { $filters = $filters.TrimEnd(',') }
    & $Ffmpeg -hide_banner -loglevel error -y -i $inputPath -af $filters -ar 44100 -c:a libvorbis -q:a 6 $outputPath
    if ($LASTEXITCODE -ne 0) { throw "Encoding failed: $name" }
    $scan = Read-AudioLog $outputPath 'volumedetect'
    $report += [pscustomobject]@{
        Clip = $name; Seconds = [Math]::Round($duration,3)
        PeakDb = [regex]::Match($scan, 'max_volume: ([-\d.]+)').Groups[1].Value
        MeanDb = [regex]::Match($scan, 'mean_volume: ([-\d.]+)').Groups[1].Value
    }
    $metaPath = $outputPath + '.meta'
    if (-not (Test-Path -LiteralPath $metaPath)) {
        $guid = [Guid]::NewGuid().ToString('N')
        $loadType = if ($kind -eq 'music') { 2 } else { 0 }
        $background = if ($kind -eq 'music') { 1 } else { 0 }
        $meta = @"
fileFormatVersion: 2
guid: $guid
AudioImporter:
  externalObjects: {}
  serializedVersion: 8
  defaultSettings:
    serializedVersion: 2
    loadType: $loadType
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 1
    quality: 0.75
    conversionMode: 0
    preloadAudioData: 1
  platformSettingOverrides: {}
  forceToMono: 0
  normalize: 0
  loadInBackground: $background
  ambisonic: 0
  3D: 0
  userData: ''
  assetBundleName: ''
  assetBundleVariant: ''
"@
        [IO.File]::WriteAllText($metaPath, $meta + "`n", (New-Object Text.UTF8Encoding($false)))
    }
    if ($kind -eq 'soft') {
        # Decode to PCM in Unity: these short cues need no second lossy encoding pass.
        $meta = [IO.File]::ReadAllText($metaPath) -replace 'compressionFormat: 1', 'compressionFormat: 0'
        [IO.File]::WriteAllText($metaPath, $meta, (New-Object Text.UTF8Encoding($false)))
    }
}
$report = @($report | Sort-Object { [Array]::IndexOf($activeNames, $_.Clip) })
$csv = $report | ConvertTo-Csv -NoTypeInformation
[IO.File]::WriteAllText($reportPath, ($csv -join "`n") + "`n", (New-Object Text.UTF8Encoding($false)))
$report | Format-Table -AutoSize
