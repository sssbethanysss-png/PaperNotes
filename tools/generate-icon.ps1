# Regenerate PaperNotes Windows app icon from SVG.
#
# Usage (repo root):
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\generate-icon.ps1
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\generate-icon.ps1 -Svg .\outputs\sticky_note_logo.svg
#
# Prefers ImageMagick / Inkscape when installed.
# Otherwise uses tools/IconRaster (WPF, no NuGet) which reads path data from the SVG.
param(
    [string]$Svg = "",
    [string]$OutIco = ""
)
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)

$assets = Join-Path (Get-Location) 'assets'
New-Item -ItemType Directory -Force -Path $assets | Out-Null

if (-not $Svg) {
    if (Test-Path 'outputs\sticky_note_logo.svg') { $Svg = 'outputs\sticky_note_logo.svg' }
    elseif (Test-Path 'assets\app-icon.svg') { $Svg = 'assets\app-icon.svg' }
    else { throw 'SVG not found. Pass -Svg path\to\file.svg' }
}
$Svg = (Resolve-Path $Svg).Path
Copy-Item -Force $Svg (Join-Path $assets 'app-icon.svg')
Copy-Item -Force $Svg (Join-Path $assets 'sticky_note_logo.svg')

if (-not $OutIco) { $OutIco = Join-Path $assets 'app-icon.ico' }
$also = Join-Path (Get-Location) 'src\Stickies\app.ico'
$masterPng = Join-Path $assets 'app-icon-512.png'

function Test-Cmd($name) { return [bool](Get-Command $name -ErrorAction SilentlyContinue) }

$rendered = $false
$magick = $null
if (Test-Cmd 'magick') { $magick = 'magick' }
elseif ((Test-Cmd 'convert') -and ((Get-Command convert).Source -notmatch '\\System32\\')) { $magick = 'convert' }

if ($magick) {
    & $magick -background none $Svg -density 384 -resize 512x512 $masterPng
    if ($LASTEXITCODE -eq 0 -and (Test-Path $masterPng)) { $rendered = $true; Write-Host 'Rendered with ImageMagick' }
}
if (-not $rendered -and (Test-Cmd 'inkscape')) {
    & inkscape $Svg --export-type=png --export-filename=$masterPng --export-width=512 --export-height=512
    if ($LASTEXITCODE -eq 0 -and (Test-Path $masterPng)) { $rendered = $true; Write-Host 'Rendered with Inkscape' }
}

$env:APPDATA = Join-Path (Get-Location) 'work\build-appdata'
$env:DOTNET_CLI_HOME = Join-Path (Get-Location) 'work\dotnet'
$env:NUGET_PACKAGES = Join-Path (Get-Location) 'work\packages'

if (-not $rendered) {
    Write-Host 'No SVG CLI found - using WPF IconRaster (reads path data from SVG)'
    dotnet run --project tools/IconRaster/IconRaster.csproj -c Release -- (Get-Location) $OutIco $also $Svg
    if ($LASTEXITCODE -ne 0) { throw 'IconRaster failed' }
    Write-Host "Source SVG: $Svg"
    exit 0
}

Add-Type -AssemblyName System.Drawing
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$src = [System.Drawing.Bitmap]::FromFile($masterPng)
try {
    $payloads = New-Object System.Collections.Generic.List[byte[]]
    foreach ($size in $sizes) {
        $frame = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $g = [System.Drawing.Graphics]::FromImage($frame)
        $g.Clear([System.Drawing.Color]::Transparent)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.DrawImage($src, 0, 0, $size, $size)
        $g.Dispose()
        $ms = New-Object System.IO.MemoryStream
        $frame.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $payloads.Add($ms.ToArray())
        $ms.Dispose(); $frame.Dispose()
    }
    $fs = [System.IO.File]::Create($OutIco)
    $bw = New-Object System.IO.BinaryWriter $fs
    $bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $s = $sizes[$i]
        $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
        $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
        $bw.Write([byte]0); $bw.Write([byte]0)
        $bw.Write([uint16]1); $bw.Write([uint16]32)
        $bw.Write([int]$payloads[$i].Length); $bw.Write([int]$offset)
        $offset += $payloads[$i].Length
    }
    foreach ($p in $payloads) { $bw.Write($p) }
    $bw.Dispose(); $fs.Dispose()
    New-Item -ItemType Directory -Force -Path (Split-Path $also) | Out-Null
    Copy-Item -Force $OutIco $also
    $preview = Join-Path $assets 'app-icon-256.png'
    $p256 = New-Object System.Drawing.Bitmap 256, 256, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g2 = [System.Drawing.Graphics]::FromImage($p256)
    $g2.Clear([System.Drawing.Color]::Transparent)
    $g2.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g2.DrawImage($src, 0, 0, 256, 256); $g2.Dispose()
    $p256.Save($preview, [System.Drawing.Imaging.ImageFormat]::Png); $p256.Dispose()
    Write-Host "OK ico=$OutIco"
    Write-Host "OK copy=$also"
} finally { $src.Dispose() }

Write-Host "Source SVG: $Svg"
