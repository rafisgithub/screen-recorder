# Packages the self-contained win-x64 publish output as an MSIX for
# Microsoft Store submission. The output is intentionally UNSIGNED - the
# Store signs packages on ingestion. It is not directly installable by
# end users; for local smoke-testing, enable Developer Mode and register
# the loose layout instead:
#
#   Add-AppxPackage -Register installer\bin\msix-layout\AppxManifest.xml
#
#   pwsh ./installer/Build-Msix.ps1 -Version 1.0.2
param(
  [Parameter(Mandatory = $true)]
  [string]$Version,

  # Partner Center identity (Product management > Product identity).
  # The placeholders in AppxManifest.xml work for local builds, but the
  # Store rejects packages whose identity doesn't match the reserved app.
  [string]$IdentityName = $env:MSIX_IDENTITY_NAME,
  [string]$Publisher = $env:MSIX_PUBLISHER,
  [string]$PublisherDisplayName = $env:MSIX_PUBLISHER_DISPLAY_NAME,

  # Reserved app name from Partner Center (Product management > App names).
  # Stamped into Package/Properties/DisplayName; the Store requires this to
  # match one of the app's reserved names or it rejects the package.
  [string]$DisplayName = $env:MSIX_DISPLAY_NAME,

  # Reuse an existing publish\win-x64 output instead of republishing.
  [switch]$SkipPublish
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$PublishDir = Join-Path $RepoRoot 'publish\win-x64'
$OutDir = Join-Path $PSScriptRoot 'bin'
$LayoutDir = Join-Path $OutDir 'msix-layout'
$MsixPath = Join-Path $OutDir 'ScreenRecorder-x64.msix'

$packageVersion = (Select-String -Path "$PSScriptRoot\Package.wxs" -Pattern 'Version="([\d.]+)"').Matches[0].Groups[1].Value
if ($packageVersion -ne $Version) {
  throw "installer/Package.wxs Version is '$packageVersion' but -Version '$Version' was requested. Bump Package.wxs (and Directory.Build.props) first."
}

if (-not $SkipPublish -or -not (Test-Path $PublishDir)) {
  Write-Host "Fetching FFmpeg runtime..." -ForegroundColor Cyan
  & "$RepoRoot\tools\Get-FFmpeg.ps1"

  Write-Host "Publishing win-x64..." -ForegroundColor Cyan
  dotnet publish "$RepoRoot\src\ScreenRecorder.App\ScreenRecorder.App.csproj" /p:PublishProfile=win-x64
  if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }
}

if (-not (Test-Path (Join-Path $PublishDir 'ffmpeg\avcodec-61.dll'))) {
  throw "FFmpeg DLLs missing from publish output - the packaged app would not be able to record."
}

Write-Host "Staging MSIX layout..." -ForegroundColor Cyan
if (Test-Path $LayoutDir) { Remove-Item -Recurse -Force $LayoutDir }
New-Item -ItemType Directory -Force -Path $LayoutDir | Out-Null
Copy-Item -Recurse "$PublishDir\*" $LayoutDir
Copy-Item -Recurse "$PSScriptRoot\msix\Assets" (Join-Path $LayoutDir 'Assets')

# Stamp the manifest: MSIX versions are four-part and the Store requires
# the revision (fourth) part to be 0.
$manifest = [xml](Get-Content -Raw "$PSScriptRoot\msix\AppxManifest.xml")
$identity = $manifest.Package.Identity
$identity.Version = "$Version.0"
if ($IdentityName) { $identity.Name = $IdentityName }
if ($Publisher) { $identity.Publisher = $Publisher }
if ($PublisherDisplayName) { $manifest.Package.Properties.PublisherDisplayName = $PublisherDisplayName }
if ($DisplayName) { $manifest.Package.Properties.DisplayName = $DisplayName }
$manifest.Save((Join-Path $LayoutDir 'AppxManifest.xml'))

# Locate makeappx: prefer an installed Windows SDK, otherwise pull the
# Microsoft.Windows.SDK.BuildTools NuGet package (build machines here only
# have the .NET SDK; GitHub windows runners ship the full Windows SDK).
$makeappx = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\10.*\x64\makeappx.exe" -ErrorAction SilentlyContinue |
  Sort-Object FullName | Select-Object -Last 1 -ExpandProperty FullName
if (-not $makeappx) {
  $toolsDir = Join-Path $RepoRoot '.tools\sdk-buildtools'
  $makeappx = Get-ChildItem "$toolsDir\bin\*\x64\makeappx.exe" -ErrorAction SilentlyContinue |
    Sort-Object FullName | Select-Object -Last 1 -ExpandProperty FullName
  if (-not $makeappx) {
    Write-Host "Downloading Microsoft.Windows.SDK.BuildTools..." -ForegroundColor Cyan
    $nupkg = Join-Path ([System.IO.Path]::GetTempPath()) 'sdk-buildtools.zip'
    Invoke-WebRequest 'https://www.nuget.org/api/v2/package/Microsoft.Windows.SDK.BuildTools/10.0.26100.1742' -OutFile $nupkg
    Expand-Archive $nupkg $toolsDir -Force
    Remove-Item $nupkg
    $makeappx = Get-ChildItem "$toolsDir\bin\*\x64\makeappx.exe" |
      Sort-Object FullName | Select-Object -Last 1 -ExpandProperty FullName
  }
}

Write-Host "Building MSIX..." -ForegroundColor Cyan
& $makeappx pack /d $LayoutDir /p $MsixPath /o
if ($LASTEXITCODE -ne 0) { throw "makeappx pack failed" }

Write-Host "Built $MsixPath (unsigned - upload to Partner Center, which signs it)" -ForegroundColor Green
