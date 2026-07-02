<#
.SYNOPSIS
    Downloads the FFmpeg 7.1 win64 shared libraries the app needs at runtime.

.DESCRIPTION
    FFmpeg.AutoGen is a bindings-only package; the native DLLs (avcodec-61,
    avformat-61, avutil-59, swscale-8, swresample-5, ...) must be provided
    separately. This script fetches the BtbN GPL shared build (GPL because the
    software fallback encoders libx264/libx265 are GPL-only) and drops the DLLs
    into <repo>\ffmpeg\, which is gitignored. The App project copies that folder
    next to the built executable, where FFmpegInterop resolves it, and the MSI
    build bundles it under <install dir>\ffmpeg.

    The download is pinned to an exact dated BtbN autobuild and verified
    against a SHA-256 hash so release builds are reproducible and tamper-
    evident. To move to a newer build, update $Release and $ReleaseSha256
    together (Get-FileHash <zip> after a manual download).

.PARAMETER Destination
    Where to place the DLLs. Defaults to <repo root>\ffmpeg.

.PARAMETER Force
    Re-download even if avcodec-61.dll is already present.
#>
[CmdletBinding()]
param(
    [string]$Destination = (Join-Path (Split-Path -Parent $PSScriptRoot) 'ffmpeg'),
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

# FFmpeg 7.1 => avcodec major 61, matching FFmpeg.AutoGen 7.0.0 bindings.
$Release = 'https://github.com/BtbN/FFmpeg-Builds/releases/download/autobuild-2026-07-02-16-09/ffmpeg-n7.1.5-1-g7d0e842004-win64-gpl-shared-7.1.zip'
$ReleaseSha256 = 'CFF3F0028C4777025B640C67F8176BDEAD4793E919621455B63A0AC0D243E716'

if (-not $Force -and (Test-Path (Join-Path $Destination 'avcodec-61.dll'))) {
    Write-Host "FFmpeg DLLs already present in '$Destination' (use -Force to re-download)."
    return
}

$staging = Join-Path ([IO.Path]::GetTempPath()) "ffmpeg-fetch-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $staging | Out-Null
$zip = Join-Path $staging 'ffmpeg.zip'

try {
    Write-Host "Downloading $Release ..."
    # curl.exe is much faster than Invoke-WebRequest for large binaries;
    # -sS keeps CI logs free of progress spam while still surfacing errors.
    & curl.exe -fsSL --retry 3 -o $zip $Release
    if ($LASTEXITCODE -ne 0) { throw "Download failed with curl exit code $LASTEXITCODE." }

    $actualSha256 = (Get-FileHash $zip -Algorithm SHA256).Hash
    if ($actualSha256 -ne $ReleaseSha256) {
        throw "SHA-256 mismatch for downloaded FFmpeg build: expected $ReleaseSha256, got $actualSha256. Refusing to use it."
    }

    Write-Host 'Extracting ...'
    Expand-Archive -Path $zip -DestinationPath $staging

    $root = Get-ChildItem -Directory $staging -Filter 'ffmpeg-*' | Select-Object -First 1
    $bin = if ($root) { Join-Path $root.FullName 'bin' }
    if (-not ($bin -and (Test-Path $bin))) { throw "Archive layout unexpected: no ffmpeg-*/bin folder found." }

    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    Copy-Item (Join-Path $bin '*.dll') -Destination $Destination -Force

    # GPL compliance: ship the license text alongside the binaries.
    $license = Join-Path $root.FullName 'LICENSE.txt'
    if (Test-Path $license) {
        Copy-Item $license -Destination (Join-Path $Destination 'FFMPEG-LICENSE.txt') -Force
    }

    $dlls = Get-ChildItem $Destination -Filter '*.dll'
    Write-Host "Placed $($dlls.Count) DLLs in '$Destination':"
    $dlls | ForEach-Object { Write-Host "  $($_.Name)" }
}
finally {
    Remove-Item -Recurse -Force $staging -ErrorAction SilentlyContinue
}
