# Screen Recorder - uninstaller.
#
#   irm https://raw.githubusercontent.com/rafisgithub/screen-recorder/main/uninstall.ps1 | iex
#
# Removes the app. Recordings, settings, and logs are kept.
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

# Close a running instance first so files are not in use during removal.
$running = @(Get-Process -Name 'ScreenRecorder', 'YouTubeScreenRecorder' -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
  Write-Host "Closing Screen Recorder..." -ForegroundColor Cyan
  $running | ForEach-Object { $null = $_.CloseMainWindow() }
  $deadline = (Get-Date).AddSeconds(10)
  while ((Get-Date) -lt $deadline -and (Get-Process -Name 'ScreenRecorder', 'YouTubeScreenRecorder' -ErrorAction SilentlyContinue)) {
    Start-Sleep -Milliseconds 500
  }
  if (Get-Process -Name 'ScreenRecorder', 'YouTubeScreenRecorder' -ErrorAction SilentlyContinue) {
    Write-Host "Please close Screen Recorder (stop any recording first) and run this again." -ForegroundColor Red
    if ($PSCommandPath) { exit 1 } else { $global:LASTEXITCODE = 1; return }
  }
}

$elevated = [Security.Principal.WindowsPrincipal]::new(
  [Security.Principal.WindowsIdentity]::GetCurrent()
).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $elevated) {
  Write-Host "Uninstalling... a User Account Control prompt will appear - choose Yes." -ForegroundColor Cyan
} else {
  Write-Host "Uninstalling Screen Recorder..." -ForegroundColor Cyan
}

$rebootNeeded = $false
$msiArgs = "/x `"$($app.PSChildName)`" /qn /norestart"
try {
  $p = if ($elevated) {
    Start-Process msiexec.exe -ArgumentList $msiArgs -Wait -PassThru -ErrorAction Stop
  } else {
    Start-Process msiexec.exe -ArgumentList $msiArgs -Verb RunAs -Wait -PassThru -ErrorAction Stop
  }
  $code = $p.ExitCode
  if ($code -eq 3010) { $code = 0; $rebootNeeded = $true }
  switch ($code) {
    0     { }
    1602  { Write-Host "Uninstall was cancelled." -ForegroundColor Yellow }
    1618  { Write-Host "Another installation is already in progress. Wait for it to finish and try again." -ForegroundColor Red }
    default { Write-Host "Uninstall exited with code $code." -ForegroundColor Red }
  }
} catch {
  $cancelled = $false
  $inner = $_.Exception
  while ($inner) {
    if ($inner -is [System.ComponentModel.Win32Exception] -and $inner.NativeErrorCode -eq 1223) { $cancelled = $true; break }
    $inner = $inner.InnerException
  }
  if ($cancelled) {
    Write-Host "Uninstall was cancelled at the elevation prompt." -ForegroundColor Yellow
  } else {
    Write-Host "Could not start the uninstaller: $($_.Exception.Message)" -ForegroundColor Red
  }
  $code = 1602
}

if ($code -eq 0) {
  Write-Host "Removed. Your recordings, settings, and logs were kept." -ForegroundColor Green
  if ($rebootNeeded) {
    Write-Host "A restart is needed to finish removing files that were in use." -ForegroundColor Yellow
  }
} elseif ($PSCommandPath) {
  exit $code
} else {
  $global:LASTEXITCODE = $code
}
