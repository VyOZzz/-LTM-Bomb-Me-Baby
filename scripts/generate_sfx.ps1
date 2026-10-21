param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot "..\LTM-Bomb-Me-Baby\Assets\Resources\Audio"),
    [string[]]$Names = @()
)

$ErrorActionPreference = "Stop"
$sampleRate = 44100
$random = [System.Random]::new(20261021)

function Clamp-Sample([double]$value) {
    return [Math]::Max(-1.0, [Math]::Min(1.0, $value))
}

function Sine([double]$frequency, [double]$time, [double]$phase = 0) {
    return [Math]::Sin(2.0 * [Math]::PI * $frequency * $time + $phase)
}

function Noise {
    return $random.NextDouble() * 2.0 - 1.0
}

function Write-Wav([string]$name, [double]$duration, [scriptblock]$sampleGenerator) {
    if ($Names.Count -gt 0 -and $Names -notcontains $name) { return }
    $sampleCount = [Math]::Ceiling($duration * $sampleRate)
    $dataSize = $sampleCount * 2
    $path = Join-Path $OutputDirectory ($name + ".wav")
    $stream = [System.IO.File]::Create($path)
    $writer = [System.IO.BinaryWriter]::new($stream)
    try {
        $writer.Write([Text.Encoding]::ASCII.GetBytes("RIFF"))
        $writer.Write([int](36 + $dataSize))
        $writer.Write([Text.Encoding]::ASCII.GetBytes("WAVE"))
        $writer.Write([Text.Encoding]::ASCII.GetBytes("fmt "))
        $writer.Write([int]16)
        $writer.Write([int16]1)
        $writer.Write([int16]1)
        $writer.Write([int]$sampleRate)
        $writer.Write([int]($sampleRate * 2))
        $writer.Write([int16]2)
        $writer.Write([int16]16)
        $writer.Write([Text.Encoding]::ASCII.GetBytes("data"))
        $writer.Write([int]$dataSize)

        for ($index = 0; $index -lt $sampleCount; $index++) {
            $time = $index / $sampleRate
            $sample = Clamp-Sample (& $sampleGenerator $time $duration)
            $writer.Write([int16]([Math]::Round($sample * 32767)))
        }
    } finally {
        $writer.Dispose()
        $stream.Dispose()
    }
    Write-Host "Generated $path"
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

# A periodic loop. All oscillators complete a whole number of cycles in one second.
Write-Wav "sfx_charge_loop" 1.0 {
    param($t, $duration)
    $pulse = 0.68 + 0.22 * (Sine 3 $t)
    $spark = [Math]::Pow([Math]::Max(0, (Sine 7 $t)), 8)
    return (0.25 * (Sine 96 $t) + 0.10 * (Sine 192 $t) + 0.05 * $spark * (Sine 864 $t)) * $pulse
}

Write-Wav "sfx_explosion" 1.1 {
    param($t, $duration)
    $decay = [Math]::Exp(-4.2 * $t)
    $frequency = 105 - 58 * ($t / $duration)
    $body = 0.58 * (Sine $frequency $t) + 0.18 * (Sine ($frequency * 0.51) $t)
    $crack = (Noise) * [Math]::Exp(-10 * $t)
    $rumble = (Noise) * 0.18 * [Math]::Exp(-3.3 * $t)
    return 0.65 * ($body + 0.75 * $crack + $rumble) * $decay
}

Write-Wav "sfx_energy_pickup" 0.26 {
    param($t, $duration)
    $x = $t / $duration
    $frequency = 540 + 980 * $x
    $envelope = [Math]::Sin([Math]::PI * $x) * (1 - 0.25 * $x)
    return $envelope * (0.48 * (Sine $frequency $t) + 0.16 * (Sine ($frequency * 2) $t))
}

Write-Wav "sfx_core_pickup" 0.58 {
    param($t, $duration)
    $x = $t / $duration
    $frequency = 310 + 560 * $x
    $attack = [Math]::Min(1.0, $t / 0.025)
    $envelope = $attack * [Math]::Pow(1 - $x, 1.4)
    $shimmer = [Math]::Pow([Math]::Max(0, (Sine 9 $t)), 5) * (Sine 1450 $t)
    return $envelope * (0.42 * (Sine $frequency $t) + 0.2 * (Sine ($frequency * 1.5) $t) + 0.1 * $shimmer)
}

Write-Wav "sfx_shield" 0.62 {
    param($t, $duration)
    $x = $t / $duration
    $frequency = 980 - 510 * $x
    $attack = [Math]::Min(1.0, $t / 0.018)
    $envelope = $attack * [Math]::Pow(1 - $x, 1.2)
    return $envelope * (0.34 * (Sine $frequency $t) + 0.22 * (Sine ($frequency * 1.51) $t) + 0.08 * (Sine 1820 $t))
}

Write-Wav "sfx_boost" 0.48 {
    param($t, $duration)
    $x = $t / $duration
    $frequency = 620 - 390 * $x
    $envelope = [Math]::Sin([Math]::PI * $x) * (1 - 0.35 * $x)
    $whoosh = (Noise) * (0.18 + 0.18 * [Math]::Sin([Math]::PI * $x))
    return $envelope * (0.35 * (Sine $frequency $t) + $whoosh)
}

Write-Wav "sfx_damage" 0.38 {
    param($t, $duration)
    $x = $t / $duration
    $envelope = [Math]::Exp(-8 * $t)
    return $envelope * (0.55 * (Sine (150 - 70 * $x) $t) + 0.42 * (Noise))
}

Write-Wav "sfx_match_start" 0.82 {
    param($t, $duration)
    $notes = @(440.0, 554.37, 659.25)
    $slot = [Math]::Min(2, [Math]::Floor($t / 0.25))
    $local = $t - $slot * 0.25
    $envelope = if ($local -lt 0.2) { [Math]::Sin([Math]::PI * $local / 0.2) } else { 0 }
    return 0.48 * $envelope * ((Sine $notes[$slot] $local) + 0.22 * (Sine ($notes[$slot] * 2) $local))
}

Write-Wav "sfx_victory" 1.32 {
    param($t, $duration)
    $notes = @(523.25, 659.25, 783.99, 1046.5)
    $slot = [Math]::Min(3, [Math]::Floor($t / 0.27))
    $local = $t - $slot * 0.27
    $note = $notes[$slot]
    $release = if ($slot -eq 3) { 0.48 } else { 0.24 }
    $envelope = if ($local -lt $release) { [Math]::Sin([Math]::PI * [Math]::Min(1.0, $local / $release)) } else { 0 }
    return 0.45 * $envelope * ((Sine $note $local) + 0.2 * (Sine ($note * 2) $local))
}

Write-Wav "sfx_defeat" 1.08 {
    param($t, $duration)
    $notes = @(392.0, 329.63, 261.63)
    $slot = [Math]::Min(2, [Math]::Floor($t / 0.32))
    $local = $t - $slot * 0.32
    $note = $notes[$slot]
    $envelope = if ($local -lt 0.29) { [Math]::Sin([Math]::PI * $local / 0.29) } else { 0 }
    return 0.4 * $envelope * ((Sine $note $local) + 0.18 * (Sine ($note * 0.5) $local))
}

Write-Wav "sfx_ui_click" 0.09 {
    param($t, $duration)
    $x = $t / $duration
    $envelope = [Math]::Pow(1 - $x, 3)
    return $envelope * (0.48 * (Sine 760 $t) + 0.16 * (Noise))
}
