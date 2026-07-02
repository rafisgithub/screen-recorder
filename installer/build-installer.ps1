<#
.SYNOPSIS
    Publishes the app and builds the Windows MSI installer.

.DESCRIPTION
    1. dotnet publish  → publish\win-x64\
    2. Generate AppFiles.generated.wxs from the publish output
    3. wix build       → installer\bin\YouTubeScreenRecorder-<Version>-x64.msi

.PARAMETER Version
    MSI version string (e.g. 1.0.0). Defaults to 1.0.0.

.EXAMPLE
    .\build-installer.ps1
    .\build-installer.ps1 -Version 1.2.3
#>
param([string]$Version = "1.0.0")

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root       = Split-Path $PSScriptRoot -Parent
$publishDir = Join-Path $root "publish\win-x64"
$outDir     = Join-Path $PSScriptRoot "bin"
$appProject = Join-Path $root "src\ScreenRecorder.App\ScreenRecorder.App.csproj"
$pkgWxs     = Join-Path $PSScriptRoot "Package.wxs"
$genWxs     = Join-Path $PSScriptRoot "AppFiles.generated.wxs"
$msiOut     = Join-Path $outDir "YouTubeScreenRecorder-$Version-x64.msi"

Write-Host ""
Write-Host "=== YouTube Screen Recorder — Installer Build ===" -ForegroundColor Cyan
Write-Host "  Version : $Version"
Write-Host "  MSI  -> : $msiOut"
Write-Host ""

# ── 1. Publish ───────────────────────────────────────────────────────────────
Write-Host "Step 1/4  dotnet publish (win-x64, self-contained)..." -ForegroundColor Yellow
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

dotnet publish $appProject `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishReadyToRun=true `
    -p:ExcludeXmlAssemblyFiles=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    --output $publishDir `
    --nologo

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)" }
Write-Host "  done — $((Get-ChildItem $publishDir -Recurse -File).Count) files published" -ForegroundColor Green

# ── 2. Generate file-harvest WXS ─────────────────────────────────────────────
Write-Host ""
Write-Host "Step 2/4  Generating AppFiles.generated.wxs..." -ForegroundColor Yellow

$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine('<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">')
[void]$sb.AppendLine('  <Fragment>')

# Directory structure (subdirectories become nested <DirectoryRef> + <Directory>)
$subDirs = Get-ChildItem $publishDir -Recurse -Directory | Sort-Object FullName
if ($subDirs) {
    [void]$sb.AppendLine('    <DirectoryRef Id="INSTALLDIR">')
    foreach ($dir in $subDirs) {
        $rel  = $dir.FullName.Substring($publishDir.TrimEnd('\').Length + 1)
        $parts = $rel -split '\\'
        $indent = '      ' + ('  ' * ($parts.Count - 1))
        [void]$sb.AppendLine("$indent<Directory Id=""dir_$($rel -replace '[\\.\-]','_')"" Name=""$($dir.Name)"" />")
    }
    [void]$sb.AppendLine('    </DirectoryRef>')
}

# Components — one per file
[void]$sb.AppendLine('    <ComponentGroup Id="AppFiles" Directory="INSTALLDIR">')

$files = Get-ChildItem $publishDir -Recurse -File | Sort-Object FullName
$idx   = 0
foreach ($file in $files) {
    $rel      = $file.FullName.Substring($publishDir.TrimEnd('\').Length + 1)
    $compId   = "comp_$($rel -replace '[\\.\-\s]','_')"
    $fileId   = "file_$($rel -replace '[\\.\-\s]','_')"
    $subPath  = if ($file.DirectoryName -ne $publishDir) {
                    $file.DirectoryName.Substring($publishDir.TrimEnd('\').Length + 1)
                } else { $null }

    [void]$sb.AppendLine("      <Component Id=""$compId"" Guid=""*"" $(if ($subPath) { "Subdirectory=""$subPath""" })>")
    [void]$sb.AppendLine("        <File Id=""$fileId"" Source=""$($file.FullName)"" $(if ($idx -eq 0) { 'KeyPath=""yes""' }) />")
    [void]$sb.AppendLine("      </Component>")
    $idx++
}

[void]$sb.AppendLine('    </ComponentGroup>')
[void]$sb.AppendLine('  </Fragment>')
[void]$sb.AppendLine('</Wix>')

[System.IO.File]::WriteAllText($genWxs, $sb.ToString(), [System.Text.Encoding]::UTF8)
Write-Host "  done — $idx components written to AppFiles.generated.wxs" -ForegroundColor Green

# ── 3. Ensure WiX UI extension ───────────────────────────────────────────────
Write-Host ""
Write-Host "Step 3/4  Checking WiX UI extension..." -ForegroundColor Yellow
$extList = wix extension list --global 2>&1
if ($extList -notmatch "WixToolset.UI.wixext") {
    Write-Host "  Installing WixToolset.UI.wixext/4.0.5..." -ForegroundColor DarkYellow
    wix extension add WixToolset.UI.wixext/4.0.5 --global
    if ($LASTEXITCODE -ne 0) { throw "Failed to install WiX UI extension" }
}
Write-Host "  ready" -ForegroundColor Green

# ── 4. Build MSI ─────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "Step 4/4  wix build..." -ForegroundColor Yellow
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory $outDir | Out-Null }

wix build $pkgWxs $genWxs `
    -arch x64 `
    -d "Version=$Version" `
    -ext WixToolset.UI.wixext `
    -out $msiOut

if ($LASTEXITCODE -ne 0) { throw "wix build failed (exit $LASTEXITCODE)" }

$sizeMb = [math]::Round((Get-Item $msiOut).Length / 1MB, 1)
Write-Host ""
Write-Host "=== BUILD SUCCEEDED ===" -ForegroundColor Green
Write-Host "  $msiOut  ($sizeMb MB)" -ForegroundColor Green
Write-Host ""
Write-Host "NOTE: FFmpeg native DLLs (avcodec-61.dll etc.) are NOT bundled." -ForegroundColor DarkYellow
Write-Host "      Provide them by setting the FFMPEG_ROOT environment variable" -ForegroundColor DarkYellow
Write-Host "      or placing the DLLs beside YouTubeScreenRecorder.exe." -ForegroundColor DarkYellow
Write-Host ""
