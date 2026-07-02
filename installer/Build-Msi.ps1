# Builds the self-contained win-x64 app and packages it into the MSI
# that install.ps1 downloads from GitHub Releases. Used by both the
# release workflow and local ad-hoc builds.
#
#   pwsh ./installer/Build-Msi.ps1 -Version 1.0.1
param(
  [Parameter(Mandatory = $true)]
  [string]$Version
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$PublishDir = Join-Path $RepoRoot 'publish\win-x64'
$OutDir = Join-Path $PSScriptRoot 'bin'
$MsiPath = Join-Path $OutDir 'ScreenRecorderSetup-x64.msi'

$packageVersion = (Select-String -Path "$PSScriptRoot\Package.wxs" -Pattern 'Version="([\d.]+)"').Matches[0].Groups[1].Value
if ($packageVersion -ne $Version) {
  throw "installer/Package.wxs Version is '$packageVersion' but -Version '$Version' was requested. Bump Package.wxs (and Directory.Build.props) first."
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

Write-Host "Publishing win-x64..." -ForegroundColor Cyan
dotnet publish "$RepoRoot\src\ScreenRecorder.App\ScreenRecorder.App.csproj" /p:PublishProfile=win-x64
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

Write-Host "Harvesting app files..." -ForegroundColor Cyan
& "$PSScriptRoot\New-AppFilesWxs.ps1" -PublishDir $PublishDir
if ($LASTEXITCODE -ne 0) { throw "Harvest failed" }

Write-Host "Building MSI..." -ForegroundColor Cyan
wix build "$PSScriptRoot\Package.wxs" "$PSScriptRoot\AppFiles.generated.wxs" `
  -arch x64 `
  -ext WixToolset.UI.wixext `
  -out $MsiPath
if ($LASTEXITCODE -ne 0) { throw "wix build failed" }

Write-Host "Built $MsiPath" -ForegroundColor Green
