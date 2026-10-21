param([string]$DotNet='dotnet',[string]$RhinoSystemDir=(Join-Path $env:ProgramFiles 'Rhino 8\System'),[switch]$UseNuGet)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
& $DotNet build (Join-Path $projectRoot 'AIArchiTec.sln') -c Release -m:1 -p:UseSharedCompilation=false "-p:RhinoSystemDir=$RhinoSystemDir" "-p:BuildWithNuGet=$($UseNuGet.IsPresent.ToString().ToLowerInvariant())" --nologo
if($LASTEXITCODE -ne 0){throw 'Build failed.'}
& $DotNet (Join-Path $projectRoot 'tests\ArchiTec.Tests\bin\Release\net8.0\ArchiTec.Tests.dll')
if($LASTEXITCODE -ne 0){throw 'Tests failed.'}
