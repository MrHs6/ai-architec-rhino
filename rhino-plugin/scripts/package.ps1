param([string]$OutputDirectory='')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
if(!$OutputDirectory){$OutputDirectory=Join-Path $projectRoot 'artifacts\releases'}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
$manifest=Get-Content -LiteralPath (Join-Path $projectRoot 'manifest.yml') -Raw
$version=[regex]::Match($manifest,'(?m)^version:\s*(\S+)\s*$').Groups[1].Value
if($version -notmatch '^\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?$'){throw 'Invalid manifest version.'}
$projectVersion=([xml](Get-Content -LiteralPath (Join-Path $projectRoot 'src\ArchiTec.Rhino\ArchiTec.Rhino.csproj') -Raw)).Project.PropertyGroup.Version
if($projectVersion -ne $version){throw 'Project and manifest versions must match.'}
$stage=Join-Path $projectRoot ('artifacts\staging\'+[guid]::NewGuid().ToString('N'))
$payload=Join-Path $stage 'payload'
$bundle=Join-Path $stage 'installer'
New-Item -ItemType Directory -Force -Path $payload,$bundle,$OutputDirectory | Out-Null
$pluginBin=Join-Path $projectRoot 'src\ArchiTec.Rhino\bin\Release\net8.0-windows'
$bridgeBin=Join-Path $projectRoot 'src\ArchiTec.McpBridge\bin\Release\net8.0'
foreach($fileName in @('AIArchiTec.rhp','AIArchiTec.Contracts.dll')){Copy-Item -LiteralPath (Join-Path $pluginBin $fileName) -Destination $payload}
foreach($fileName in @('AIArchiTec.McpBridge.exe','AIArchiTec.McpBridge.dll','AIArchiTec.McpBridge.deps.json','AIArchiTec.McpBridge.runtimeconfig.json')){Copy-Item -LiteralPath (Join-Path $bridgeBin $fileName) -Destination $payload}
foreach($fileName in @('manifest.yml','PRIVACY.md')){Copy-Item -LiteralPath (Join-Path $projectRoot $fileName) -Destination $payload}
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\INSTALLER-README.md') -Destination (Join-Path $payload 'README.md')
Copy-Item -LiteralPath (Join-Path $projectRoot 'src\ArchiTec.Rhino\Assets\logo.png') -Destination (Join-Path $payload 'logo.png')
Add-Type -AssemblyName System.IO.Compression.FileSystem
$yakPath=Join-Path $OutputDirectory "ai-architec-rhino-$version-rh8_31-win.yak"
# Yak's documented format is a ZIP archive with a manifest at its root.
# Hosted CI does not need or redistribute McNeel's Yak executable.
function Write-Archive([string]$Source,[string]$Destination){
    $stream=[IO.File]::Open($Destination,[IO.FileMode]::Create,[IO.FileAccess]::Write,[IO.FileShare]::None)
    $zip=[IO.Compression.ZipArchive]::new($stream,[IO.Compression.ZipArchiveMode]::Create)
    try{
        foreach($file in Get-ChildItem -LiteralPath $Source -File | Sort-Object Name){
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$file.FullName,$file.Name,[IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    }finally{$zip.Dispose();$stream.Dispose()}
}
Write-Archive $payload $yakPath
& (Join-Path $PSScriptRoot 'verify-package.ps1') -PackagePath $yakPath
Copy-Item -LiteralPath $yakPath -Destination $bundle
foreach($fileName in @('install.ps1','install.cmd')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $fileName) -Destination $bundle}
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\INSTALLER-README.md') -Destination (Join-Path $bundle 'README.md')
Copy-Item -LiteralPath (Join-Path $projectRoot 'PRIVACY.md') -Destination $bundle
Get-ChildItem -LiteralPath $bundle -File | Sort-Object Name | ForEach-Object {
    '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(),$_.Name
} | Set-Content -LiteralPath (Join-Path $bundle 'SHA256SUMS.txt') -Encoding ascii
$bundlePath=Join-Path $OutputDirectory "ai-architec-rhino-$version-windows-installer.zip"
Write-Archive $bundle $bundlePath
@($yakPath,$bundlePath) | ForEach-Object {
    '{0}  {1}' -f (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant(),[IO.Path]::GetFileName($_)
} | Set-Content -LiteralPath (Join-Path $OutputDirectory 'SHA256SUMS.txt') -Encoding ascii
Write-Output $yakPath
Write-Output $bundlePath
