# Screen Recorder — uninstaller.
#
#   irm https://raw.githubusercontent.com/rafisgithub/screen-recorder/main/uninstall.ps1 | iex
#
$ErrorActionPreference = 'SilentlyContinue'

Write-Host "Uninstalling Screen Recorder..." -ForegroundColor Cyan
$pkg = Get-Package -Name "Screen Recorder" -ErrorAction SilentlyContinue
if ($pkg) {
  $pkg | Uninstall-Package -Force | Out-Null
  Write-Host "Removed." -ForegroundColor Green
} else {
  Write-Host "Screen Recorder is not installed (or was already removed)." -ForegroundColor Yellow
}
