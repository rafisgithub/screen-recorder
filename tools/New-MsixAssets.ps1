<#
.SYNOPSIS
    Renders the MSIX logo PNGs in installer\msix\Assets from the app icon.

.DESCRIPTION
    One-off generator: run it after changing src\ScreenRecorder.App\Assets\app.ico
    and commit the resulting PNGs. Tile logos get padding per the Windows tile
    design guidance; app-list logos (Square44x44, StoreLogo) are full-bleed.

    pwsh ./tools/New-MsixAssets.ps1
#>
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$IcoPath = Join-Path $RepoRoot 'src\ScreenRecorder.App\Assets\app.ico'
$AssetsDir = Join-Path $RepoRoot 'installer\msix\Assets'
New-Item -ItemType Directory -Force -Path $AssetsDir | Out-Null

# Pull the largest frame the .ico contains (closest match to 256x256).
$icon = New-Object System.Drawing.Icon($IcoPath, 256, 256)
$source = $icon.ToBitmap()

function New-Logo {
    param([int]$Width, [int]$Height, [int]$IconSize, [string]$Name)

    $bmp = New-Object System.Drawing.Bitmap($Width, $Height)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $x = [int](($Width - $IconSize) / 2)
    $y = [int](($Height - $IconSize) / 2)
    $g.DrawImage($source, $x, $y, $IconSize, $IconSize)
    $g.Dispose()

    $out = Join-Path $AssetsDir $Name
    $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "Wrote $out"
}

New-Logo -Width 44  -Height 44  -IconSize 44  -Name 'Square44x44Logo.png'
New-Logo -Width 50  -Height 50  -IconSize 50  -Name 'StoreLogo.png'
New-Logo -Width 150 -Height 150 -IconSize 100 -Name 'Square150x150Logo.png'
New-Logo -Width 310 -Height 150 -IconSize 96  -Name 'Wide310x150Logo.png'

$source.Dispose()
$icon.Dispose()
