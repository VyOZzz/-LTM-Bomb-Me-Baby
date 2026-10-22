param(
    [Parameter(Mandatory=$true)][string]$SourceRoot,
    [Parameter(Mandatory=$true)][string]$Ffmpeg,
    [Parameter(Mandatory=$true)][string]$Ffprobe
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
$items = @(
    @('music_meadow', 'meadow.ogg', 'music', ''),
    @('music_crossroads', 'crossroads.ogg', 'music', ''),
    @('music_crater', 'crater.mp3', 'music', ''),
    @('mix_victory', 'victory.ogg', 'result', ''),
    @('mix_defeat', 'defeat.ogg', 'result', ''),
    @('mix_charge', 'scifi/Audio/spaceEngineSmall_000.ogg', 'loop', 'highpass=f=100,lowpass=f=3500'),
    @('mix_explosion_1', 'scifi/Audio/explosionCrunch_004.ogg', 'sfx', 'highpass=f=55,lowpass=f=6500'),
    @('mix_explosion_2', 'scifi/Audio/explosionCrunch_002.ogg', 'sfx', 'highpass=f=55,lowpass=f=6500'),
    @('mix_explosion_3', 'scifi/Audio/explosionCrunch_001.ogg', 'sfx', 'highpass=f=55,lowpass=f=6500'),
    @('mix_explosion_low', 'scifi/Audio/lowFrequency_explosion_000.ogg', 'sfx', 'highpass=f=40,lowpass=f=700'),
    @('mix_energy', 'interface/Audio/confirmation_003.ogg', 'sfx', 'lowpass=f=6500'),
    @('mix_core', 'interface/Audio/maximize_006.ogg', 'sfx', 'lowpass=f=6500'),
    @('mix_shield', 'scifi/Audio/forceField_003.ogg', 'sfx', 'highpass=f=100,lowpass=f=5000'),
    @('mix_shield_body', 'scifi/Audio/engineCircular_002.ogg', 'loop', 'highpass=f=100,lowpass=f=2400'),
    @('mix_shield_hit', 'impact/Audio/impactGlass_medium_002.ogg', 'sfx', 'lowpass=f=5000'),
    @('mix_boost', 'rpg/Audio/knifeSlice.ogg', 'sfx', 'highpass=f=150,lowpass=f=6000'),
    @('mix_boost_body', 'scifi/Audio/thrusterFire_002.ogg', 'loop', 'highpass=f=100,lowpass=f=3200'),
    @('mix_damage', 'impact/Audio/impactPunch_heavy_000.ogg', 'sfx', 'lowpass=f=5000'),
    @('mix_land_1', 'impact/Audio/impactSoft_heavy_000.ogg', 'sfx', 'lowpass=f=4000'),
    @('mix_land_2', 'impact/Audio/impactSoft_heavy_001.ogg', 'sfx', 'lowpass=f=4000'),
    @('mix_drop', 'rpg/Audio/handleCoins2.ogg', 'sfx', 'lowpass=f=5500'),
    @('mix_ui', 'interface/Audio/click_003.ogg', 'sfx', 'lowpass=f=5000')
)
$report = @()
foreach ($item in $items) {
    $name, $relative, $kind, $tone = $item
    $inputPath = if ([IO.Path]::IsPathRooted($relative)) { $relative } else { Join-Path $SourceRoot $relative }
    if (-not (Test-Path -LiteralPath $inputPath)) { throw "Missing source: $inputPath" }
    $durationText = & $Ffprobe -v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 $inputPath
    if ($LASTEXITCODE -ne 0) { throw "Cannot decode $inputPath" }
    $duration = [double]::Parse($durationText, $culture)
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
        $gain = [Math]::Min(-6 - $peak, -23 - $mean).ToString('F2', $culture)
        $filters += "volume=${gain}dB,"
    }
    # Tiny fades remove edge clicks without fading out an entire musical phrase.
    $fade = if ($kind -eq 'music' -or $kind -eq 'loop') { .008 } else { .035 }
    $fade = [Math]::Min([double]$fade, $duration * .15)
    $tail = [Math]::Max(0.0, $duration - $fade).ToString('F6', $culture)
    $fadeText = $fade.ToString('F3', $culture)
    $filters += "afade=t=in:d=0.004,afade=t=out:st=${tail}:d=${fadeText}"
    $outputPath = Join-Path $destination "$name.ogg"
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
}
$report | Export-Csv (Join-Path $repo 'docs/AUDIO_LEVELS.csv') -NoTypeInformation -Encoding UTF8
$report | Format-Table -AutoSize
