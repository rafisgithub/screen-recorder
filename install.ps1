# Screen Recorder - one-line installer.
#
#   irm https://raw.githubusercontent.com/rafisgithub/screen-recorder/main/install.ps1 | iex
#
# Downloads the latest MSI from GitHub Releases and installs it silently.
# The MSI is per-machine (Program Files), so a UAC prompt appears unless the
# shell is already elevated. Skips the download when the installed version is
# already current (pass -Force to reinstall:
#   & ([scriptblock]::Create((irm <url>))) -Force ).
#
# When piped into iex, failures leave the console open and set $LASTEXITCODE;
# when run as a saved .ps1, failures exit with the msiexec exit code.
param([switch]$Force)

$Repo = 'rafisgithub/screen-recorder'
$msi = Join-Path $env:TEMP 'ScreenRecorderSetup.msi'
$log = Join-Path $env:TEMP 'ScreenRecorderSetup.log'
$code = 0
$rebootNeeded = $false

# Windows PowerShell 5.1 may not offer TLS 1.2 by default, which GitHub requires.
if ($PSVersionTable.PSVersion.Major -lt 6) {
  [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
}

function Test-SRInteractive {
  return [Environment]::UserInteractive -and -not ([Console]::IsInputRedirected)
}

function Get-SRInstalledApp {
  Get-ItemProperty `
      'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*',
      'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*' `
      -ErrorAction SilentlyContinue |
    Where-Object { $_.DisplayName -eq 'Screen Recorder' } |
    Select-Object -First 1
}

function Get-SRLatestTag {
  # The /releases/latest redirect carries the tag; no API rate limits involved.
  try {
    $req = [Net.WebRequest]::CreateHttp("https://github.com/$Repo/releases/latest")
    $req.Method = 'HEAD'
    $req.AllowAutoRedirect = $false
    $req.Timeout = 15000
    $resp = $req.GetResponse()
    try { $location = $resp.Headers['Location'] } finally { $resp.Close() }
    if ($location -match '/tag/v?([0-9][^/]*)$') { return $Matches[1] }
  } catch { }
  return $null
}

$latest = Get-SRLatestTag
$installed = Get-SRInstalledApp

# Skip work when there is nothing to do.
if (-not $Force -and $latest -and $installed -and $installed.DisplayVersion) {
  try {
    $installedVersion = [version]$installed.DisplayVersion
    $latestVersion = [version]$latest
    if ($installedVersion -ge $latestVersion) {
      Write-Host "Screen Recorder v$($installed.DisplayVersion) is already installed and up to date." -ForegroundColor Green
      if ($PSCommandPath) { exit 0 } else { $global:LASTEXITCODE = 0; return }
    }
    Write-Host "Updating Screen Recorder v$($installed.DisplayVersion) -> v$latest..." -ForegroundColor Cyan
  } catch { }
}

# Installing over a running instance forces a reboot to swap files - close it first.
$running = @(Get-Process -Name 'ScreenRecorder', 'YouTubeScreenRecorder' -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
  Write-Host "Screen Recorder is currently running." -ForegroundColor Yellow
  $shouldClose = $false
  if (Test-SRInteractive) {
    Write-Host "If a recording is in progress, stop it first or it will be lost." -ForegroundColor Yellow
    $answer = Read-Host "Close Screen Recorder and continue? [Y/n]"
    if ($answer -eq '' -or $answer -match '^[Yy]') { $shouldClose = $true }
  }
  if ($shouldClose) {
    $running | ForEach-Object { $null = $_.CloseMainWindow() }
    $deadline = (Get-Date).AddSeconds(10)
    while ((Get-Date) -lt $deadline -and (Get-Process -Name 'ScreenRecorder', 'YouTubeScreenRecorder' -ErrorAction SilentlyContinue)) {
      Start-Sleep -Milliseconds 500
    }
  }
  if (Get-Process -Name 'ScreenRecorder', 'YouTubeScreenRecorder' -ErrorAction SilentlyContinue) {
    Write-Host "Please close Screen Recorder and run the installer again." -ForegroundColor Red
    $code = 1
  }
}

if ($code -eq 0) {
  # Prefer the exact versioned asset when the tag is known; /latest otherwise.
  if ($latest) {
    $msiUrl = "https://github.com/$Repo/releases/download/v$latest/ScreenRecorderSetup-x64.msi"
    $what = "Screen Recorder v$latest"
  } else {
    $msiUrl = "https://github.com/$Repo/releases/latest/download/ScreenRecorderSetup-x64.msi"
    $what = "Screen Recorder"
  }

  $sizeNote = ''
  try {
    $head = Invoke-WebRequest -Uri $msiUrl -Method Head -UseBasicParsing -ErrorAction Stop
    $length = $head.Headers['Content-Length']
    if ($length -is [array]) { $length = $length[0] }
    if ($length) { $sizeNote = " (~$([math]::Round([long]$length / 1MB)) MB)" }
  } catch { }

  Write-Host "Downloading $what$sizeNote..." -ForegroundColor Cyan
  # The 5.1 progress bar slows large downloads to a crawl; drop it there.
  $oldProgress = $ProgressPreference
  try {
    if ($PSVersionTable.PSVersion.Major -lt 6) { $ProgressPreference = 'SilentlyContinue' }
    Invoke-WebRequest -Uri $msiUrl -OutFile $msi -UseBasicParsing -ErrorAction Stop
  } catch {
    Write-Host "Download failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Check your connection, or grab the MSI manually: https://github.com/$Repo/releases/latest" -ForegroundColor Yellow
    Remove-Item $msi -ErrorAction SilentlyContinue
    $code = 1
  } finally {
    $ProgressPreference = $oldProgress
  }
}

if ($code -eq 0) {
  $elevated = [Security.Principal.WindowsPrincipal]::new(
    [Security.Principal.WindowsIdentity]::GetCurrent()
  ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

  if (-not $elevated) {
    Write-Host "Installing... a User Account Control prompt will appear - choose Yes." -ForegroundColor Cyan
  } else {
    Write-Host "Installing..." -ForegroundColor Cyan
  }

  $msiArgs = "/i `"$msi`" /qn /norestart /l*v `"$log`""
  try {
    $p = if ($elevated) {
      Start-Process msiexec.exe -ArgumentList $msiArgs -Wait -PassThru -ErrorAction Stop
    } else {
      Start-Process msiexec.exe -ArgumentList $msiArgs -Verb RunAs -Wait -PassThru -ErrorAction Stop
    }
    $code = $p.ExitCode
    if ($code -eq 3010) {
      # Success - Windows just wants a reboot to finish swapping in-use files.
      $code = 0
      $rebootNeeded = $true
    }
    switch ($code) {
      0     { }
      1602  { Write-Host "Installation was cancelled." -ForegroundColor Yellow }
      1618  { Write-Host "Another installation is already in progress. Wait for it to finish and try again." -ForegroundColor Red }
      default { Write-Host "Setup exited with code $code. Install log: $log" -ForegroundColor Red }
    }
  } catch {
    $cancelled = $false
    $inner = $_.Exception
    while ($inner) {
      if ($inner -is [System.ComponentModel.Win32Exception] -and $inner.NativeErrorCode -eq 1223) { $cancelled = $true; break }
      $inner = $inner.InnerException
    }
    if ($cancelled) {
      Write-Host "Installation was cancelled at the elevation prompt." -ForegroundColor Yellow
    } else {
      Write-Host "Could not start the installer: $($_.Exception.Message)" -ForegroundColor Red
    }
    $code = 1602
  }
  Remove-Item $msi -ErrorAction SilentlyContinue
}

if ($code -eq 0) {
  $installedNow = Get-SRInstalledApp
  $version = if ($installedNow -and $installedNow.DisplayVersion) { "v$($installedNow.DisplayVersion) " } else { '' }
  Write-Host "Done. Screen Recorder $($version)is installed - find it in the Start menu." -ForegroundColor Green
  if ($rebootNeeded) {
    Write-Host "A restart is needed to finish replacing files that were in use." -ForegroundColor Yellow
  }
  Write-Host "Uninstall any time:  irm https://raw.githubusercontent.com/$Repo/main/uninstall.ps1 | iex"

  if (-not $rebootNeeded -and (Test-SRInteractive)) {
    $exe = $null
    if ($installedNow -and $installedNow.InstallLocation) {
      $exe = Join-Path $installedNow.InstallLocation 'ScreenRecorder.exe'
    }
    if (-not ($exe -and (Test-Path $exe))) {
      $exe = Join-Path $env:ProgramFiles 'ScreenRecorder\ScreenRecorder.exe'
    }
    if (Test-Path $exe) {
      $answer = Read-Host "Launch Screen Recorder now? [Y/n]"
      if ($answer -eq '' -or $answer -match '^[Yy]') { Start-Process $exe }
    }
  }
} elseif ($PSCommandPath) {
  exit $code
} else {
  $global:LASTEXITCODE = $code
}
