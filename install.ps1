# Screen Recorder — one-line installer.
#
#   irm https://raw.githubusercontent.com/rafisgithub/screen-recorder/main/install.ps1 | iex
#
# Downloads the latest signed MSI from GitHub Releases and installs it silently.
# The MSI is per-machine (Program Files), so a UAC prompt appears unless the
# shell is already elevated.
#
# When piped into iex, failures leave the console open and set $LASTEXITCODE;
# when run as a saved .ps1, failures exit with the msiexec exit code.
$Repo = 'rafisgithub/screen-recorder'
$msi = Join-Path $env:TEMP 'ScreenRecorderSetup.msi'
$log = Join-Path $env:TEMP 'ScreenRecorderSetup.log'
$code = 0

# Windows PowerShell 5.1 may not offer TLS 1.2 by default, which GitHub requires.
if ($PSVersionTable.PSVersion.Major -lt 6) {
  [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
}

Write-Host "Downloading Screen Recorder..." -ForegroundColor Cyan
try {
  Invoke-WebRequest -Uri "https://github.com/$Repo/releases/latest/download/ScreenRecorderSetup-x64.msi" -OutFile $msi -UseBasicParsing -ErrorAction Stop
} catch {
  Write-Host "Download failed: $($_.Exception.Message)" -ForegroundColor Red
  Remove-Item $msi -ErrorAction SilentlyContinue
  $code = 1
}

if ($code -eq 0) {
  $elevated = [Security.Principal.WindowsPrincipal]::new(
    [Security.Principal.WindowsIdentity]::GetCurrent()
  ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

  Write-Host "Installing..." -ForegroundColor Cyan
  $msiArgs = "/i `"$msi`" /qn /norestart /l*v `"$log`""
  try {
    $p = if ($elevated) {
      Start-Process msiexec.exe -ArgumentList $msiArgs -Wait -PassThru -ErrorAction Stop
    } else {
      Start-Process msiexec.exe -ArgumentList $msiArgs -Verb RunAs -Wait -PassThru -ErrorAction Stop
    }
    $code = $p.ExitCode
    if ($code -ne 0) {
      Write-Host "Setup exited with code $code. Install log: $log" -ForegroundColor Red
    }
  } catch {
    Write-Host "Installation was cancelled at the elevation prompt." -ForegroundColor Red
    $code = 1602
  }
  Remove-Item $msi -ErrorAction SilentlyContinue
}

if ($code -eq 0) {
  Write-Host "Done. Screen Recorder is installed." -ForegroundColor Green
  Write-Host "Uninstall any time:  irm https://raw.githubusercontent.com/$Repo/main/uninstall.ps1 | iex"
} elseif ($PSCommandPath) {
  exit $code
} else {
  $global:LASTEXITCODE = $code
}
