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

# Short, seamless-enough music beds for the demo. Every loop fades to zero at the
# boundary and uses note slots shorter than the beat, avoiding clicks when Unity loops it.
$menuNotes = @(261.63, 329.63, 392.00, 523.25, 220.00, 329.63, 440.00, 493.88,
               196.00, 293.66, 392.00, 493.88, 220.00, 329.63, 440.00, 659.25)
$meadowNotes = @(261.63, 329.63, 392.00, 659.25, 349.23, 440.00, 523.25, 698.46,
                 392.00, 493.88, 587.33, 783.99, 261.63, 392.00, 523.25, 783.99)
$crossroadsNotes = @(293.66, 349.23, 440.00, 587.33, 261.63, 329.63, 392.00, 523.25,
                     233.08, 293.66, 349.23, 466.16, 261.63, 329.63, 440.00, 523.25)
$craterNotes = @(220.00, 261.63, 329.63, 440.00, 196.00, 246.94, 293.66, 392.00,
                 174.61, 220.00, 261.63, 349.23, 196.00, 246.94, 329.63, 392.00)

Write-Wav "music_menu" 8.0 {
    param($t, $duration)
    $beat = 0.5; $step = [Math]::Floor($t / $beat); $local = $t - $step * $beat
    $note = $menuNotes[$step % $menuNotes.Count]
    $attack = [Math]::Min(1.0, $local / 0.025)
    $pluck = $attack * [Math]::Exp(-5.2 * $local)
    $root = @(130.81, 110.00, 98.00, 110.00)[[Math]::Floor($t / 2) % 4]
    $edge = [Math]::Min(1.0, [Math]::Min($t / 0.12, ($duration - $t) / 0.12))
    return $edge * (0.20 * $pluck * ((Sine $note $t) + 0.18 * (Sine ($note * 2) $t)) +
        0.07 * (Sine $root $t) + 0.035 * (Sine ($root * 1.5) $t))
}

Write-Wav "music_meadow" 8.0 {
    param($t, $duration)
    $beat = 0.5; $step = [Math]::Floor($t / $beat); $local = $t - $step * $beat
    $note = $meadowNotes[$step % $meadowNotes.Count]
    $pluck = [Math]::Min(1.0, $local / 0.018) * [Math]::Exp(-6.0 * $local)
    $root = @(130.81, 174.61, 196.00, 130.81)[[Math]::Floor($t / 2) % 4]
    $breeze = 0.5 + 0.5 * (Sine 0.25 $t)
    $edge = [Math]::Min(1.0, [Math]::Min($t / 0.12, ($duration - $t) / 0.12))
    return $edge * (0.22 * $pluck * ((Sine $note $t) + 0.14 * (Sine ($note * 2) $t)) +
        0.065 * (Sine $root $t) + 0.025 * $breeze * (Sine 1046.5 $t))
}

Write-Wav "music_crossroads" 8.0 {
    param($t, $duration)
    $beat = 0.5; $step = [Math]::Floor($t / $beat); $local = $t - $step * $beat
    $note = $crossroadsNotes[$step % $crossroadsNotes.Count]
    $gate = if ($local -lt 0.30) { [Math]::Pow([Math]::Sin([Math]::PI * $local / 0.30), 1.4) } else { 0 }
    $root = @(146.83, 130.81, 116.54, 130.81)[[Math]::Floor($t / 2) % 4]
    $mechanical = [Math]::Pow([Math]::Max(0, (Sine 4 $t)), 10)
    $edge = [Math]::Min(1.0, [Math]::Min($t / 0.12, ($duration - $t) / 0.12))
    return $edge * (0.18 * $gate * ((Sine $note $t) + 0.22 * (Sine ($note * 2) $t)) +
        0.08 * (Sine $root $t) + 0.045 * $mechanical * (Sine 880 $t))
}

Write-Wav "music_crater" 8.0 {
    param($t, $duration)
    $beat = 0.5; $step = [Math]::Floor($t / $beat); $local = $t - $step * $beat
    $note = $craterNotes[$step % $craterNotes.Count]
    $pulse = if ($local -lt 0.34) { [Math]::Sin([Math]::PI * $local / 0.34) } else { 0 }
    $root = @(110.00, 98.00, 87.31, 98.00)[[Math]::Floor($t / 2) % 4]
    $rumble = (Noise) * 0.018 * (0.5 + 0.5 * (Sine 2 $t))
    $edge = [Math]::Min(1.0, [Math]::Min($t / 0.12, ($duration - $t) / 0.12))
    return $edge * (0.19 * $pulse * ((Sine $note $t) + 0.20 * (Sine ($note * 0.5) $t)) +
        0.10 * (Sine $root $t) + $rumble)
}
