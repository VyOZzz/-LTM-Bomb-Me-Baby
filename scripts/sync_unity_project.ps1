param(
    [string]$SourceProject = (Join-Path (Split-Path $PSScriptRoot -Parent) 'LTM-Bomb-Me-Baby'),
    [string]$Destination = (Join-Path (Split-Path $PSScriptRoot -Parent) 'builds/unity-validation')
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$buildRoot = [IO.Path]::GetFullPath((Join-Path $repo 'builds'))
$sourceRoot = [IO.Path]::GetFullPath($SourceProject).TrimEnd('\', '/')
$targetRoot = [IO.Path]::GetFullPath($Destination).TrimEnd('\', '/')
if (!$targetRoot.StartsWith($buildRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Unity staging destination must be a child of this repository builds directory.'
}
if ($targetRoot.Equals($sourceRoot, [StringComparison]::OrdinalIgnoreCase) -or
    $targetRoot.StartsWith($sourceRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or
    $sourceRoot.StartsWith($targetRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Source and staging trees must not overlap.'
}
function Assert-NoReparsePoint([string]$Path, [bool]$WalkTree) {
    if (!(Test-Path -LiteralPath $Path)) { return }
    $item = Get-Item -LiteralPath $Path -Force
    if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Unsafe staging link: $Path" }
    if ($WalkTree -and $item.PSIsContainer) {
        foreach ($child in Get-ChildItem -LiteralPath $Path -Force) {
            Assert-NoReparsePoint $child.FullName $true
        }
    }
}
# Validate ancestors and every mirrored subtree before any deletion or copy.
foreach ($root in @($sourceRoot, $targetRoot)) {
    $ancestor = $root
    while ($ancestor) {
        Assert-NoReparsePoint $ancestor $false
        $ancestor = Split-Path $ancestor -Parent
    }
}
$folders = @('Assets', 'Packages', 'ProjectSettings')
foreach ($folder in $folders) {
    $source = Join-Path $sourceRoot $folder
    if (!(Test-Path -LiteralPath $source -PathType Container)) { throw "Missing source folder: $source" }
    Assert-NoReparsePoint $source $true
    Assert-NoReparsePoint (Join-Path $targetRoot $folder) $true
}
foreach ($folder in $folders) {
    # Only these verified staging subtrees are mirrored. Library remains a reusable cache.
    & robocopy (Join-Path $sourceRoot $folder) (Join-Path $targetRoot $folder) /MIR /XJ /R:0 /W:0 /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "Failed to stage $folder" }
}
exit 0
