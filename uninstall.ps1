# Screen Recorder — uninstaller.
#
#   irm https://raw.githubusercontent.com/rafisgithub/screen-recorder/main/uninstall.ps1 | iex
#
# When piped into iex, failures leave the console open and set $LASTEXITCODE;
# when run as a saved .ps1, failures exit with the msiexec exit code.
$code = 0

# Find the MSI product code from the machine uninstall registry.
$app = Get-ItemProperty `
    'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*',
    'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*' `
    -ErrorAction SilentlyContinue |
  Where-Object { $_.DisplayName -eq 'Screen Recorder' } |
  Select-Object -First 1

if (-not $app) {
  Write-Host "Screen Recorder is not installed (or was already removed)." -ForegroundColor Yellow
  return
}

$elevated = [Security.Principal.WindowsPrincipal]::new(
  [Security.Principal.WindowsIdentity]::GetCurrent()
).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

Write-Host "Uninstalling Screen Recorder..." -ForegroundColor Cyan
$msiArgs = "/x `"$($app.PSChildName)`" /qn /norestart"
try {
  $p = if ($elevated) {
    Start-Process msiexec.exe -ArgumentList $msiArgs -Wait -PassThru -ErrorAction Stop
  } else {
    Start-Process msiexec.exe -ArgumentList $msiArgs -Verb RunAs -Wait -PassThru -ErrorAction Stop
  }
  $code = $p.ExitCode
  if ($code -ne 0) {
    Write-Host "Uninstall exited with code $code." -ForegroundColor Red
  }
} catch {
  Write-Host "Uninstall was cancelled at the elevation prompt." -ForegroundColor Red
  $code = 1602
}

if ($code -eq 0) {
  Write-Host "Removed." -ForegroundColor Green
} elseif ($PSCommandPath) {
  exit $code
} else {
  $global:LASTEXITCODE = $code
}
