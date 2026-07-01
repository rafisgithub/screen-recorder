# Screen Recorder — one-line installer.
#
#   irm https://raw.githubusercontent.com/rafisgithub/screen-recorder/main/install.ps1 | iex
#
# Downloads the latest signed MSI from GitHub Releases and installs it silently.
$ErrorActionPreference = 'Stop'
$Repo = 'rafisgithub/screen-recorder'
$msi = Join-Path $env:TEMP 'ScreenRecorderSetup.msi'

Write-Host "Downloading Screen Recorder..." -ForegroundColor Cyan
Invoke-WebRequest -Uri "https://github.com/$Repo/releases/latest/download/ScreenRecorderSetup-x64.msi" -OutFile $msi

Write-Host "Installing..." -ForegroundColor Cyan
$p = Start-Process msiexec.exe -ArgumentList "/i `"$msi`" /qn /norestart" -Wait -PassThru
Remove-Item $msi -ErrorAction SilentlyContinue

if ($p.ExitCode -ne 0) {
  Write-Host "Setup exited with code $($p.ExitCode)." -ForegroundColor Red
  exit $p.ExitCode
}

Write-Host "Done. Screen Recorder is installed." -ForegroundColor Green
Write-Host "Uninstall any time:  irm https://raw.githubusercontent.com/$Repo/main/uninstall.ps1 | iex"
