# Harvests every file under the win-x64 publish output into a WiX
# ComponentGroup, mirroring the folder layout (locale subfolders etc.)
# under INSTALLDIR. Regenerate after every `dotnet publish` before
# building the MSI - this file is gitignored and not checked in.
#
#   pwsh ./installer/New-AppFilesWxs.ps1 -PublishDir ..\publish\win-x64
param(
  [Parameter(Mandatory = $true)]
  [string]$PublishDir,
  [string]$OutFile = (Join-Path $PSScriptRoot 'AppFiles.generated.wxs')
)

$ErrorActionPreference = 'Stop'
$PublishDir = (Resolve-Path $PublishDir).Path

function ConvertTo-WixId([string]$text) {
  ($text -replace '[^A-Za-z0-9_]', '_')
}

$files = Get-ChildItem -Path $PublishDir -Recurse -File
$components = foreach ($file in $files) {
  $relativePath = $file.FullName.Substring($PublishDir.Length).TrimStart('\')
  $relativeDir = Split-Path $relativePath -Parent
  $idBase = ConvertTo-WixId($relativePath -replace '\\', '_')

  $subdirAttr = ''
  if ($relativeDir) {
    $subdirAttr = " Subdirectory=`"$relativeDir`""
  }

  @"
      <Component Id="comp_$idBase" Guid="*"$subdirAttr>
        <File Id="file_$idBase" Source="$($file.FullName)" />
      </Component>
"@
}

$wxs = @"
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Fragment>
    <ComponentGroup Id="AppFiles" Directory="INSTALLDIR">
$($components -join "`n")
    </ComponentGroup>
  </Fragment>
</Wix>
"@

Set-Content -Path $OutFile -Value $wxs -Encoding UTF8
Write-Host "Wrote $($files.Count) file components to $OutFile" -ForegroundColor Green
