param([Parameter(Mandatory=$true)][string]$PackagePath)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $PackagePath).Path)
try{
    $required=@('manifest.yml','AIArchiTec.rhp','AIArchiTec.Contracts.dll','AIArchiTec.McpBridge.exe','AIArchiTec.McpBridge.dll','AIArchiTec.McpBridge.deps.json','AIArchiTec.McpBridge.runtimeconfig.json','README.md','PRIVACY.md','logo.png')
    $actual=@($archive.Entries | ForEach-Object FullName)
    foreach($name in $required){if($actual -notcontains $name){throw "Package is missing $name"}}
    foreach($name in $actual){if($required -notcontains $name){throw "Unexpected package entry: $name"}}
    if($actual.Count -ne $required.Count){throw 'Duplicate package entries.'}
    foreach($entry in $archive.Entries){if($entry.Length -eq 0){throw "Empty package entry: $($entry.FullName)"}}
    $reader=[IO.StreamReader]::new($archive.GetEntry('manifest.yml').Open())
    try{$manifest=$reader.ReadToEnd()}finally{$reader.Dispose()}
    $version=[regex]::Match($manifest,'(?m)^version:\s*(\S+)\s*$').Groups[1].Value
    if([IO.Path]::GetFileName($PackagePath) -ne "ai-architec-rhino-$version-rh8_31-win.yak"){throw 'Filename, version and Rhino distribution tag do not match.'}
    Write-Output "Verified package $version (10 expected files; no host SDK or user settings)."
}finally{$archive.Dispose()}
