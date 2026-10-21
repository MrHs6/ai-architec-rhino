[CmdletBinding(SupportsShouldProcess=$true)]
param(
    [string]$PackagePath='',
    [string]$InstalledPluginPath='',
    [switch]$CheckOnly
)
$ErrorActionPreference='Stop'
$pluginId='0ab9a769-52f9-4378-85cb-cf8177ed5166'
$registration='Software\McNeel\Rhinoceros\8.0\Plug-Ins\'+$pluginId
if($PackagePath -and $InstalledPluginPath){throw 'Specify PackagePath or InstalledPluginPath, not both.'}
if($PackagePath){
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive=[IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $PackagePath).Path)
    try{
        $entry=$archive.GetEntry('manifest.yml')
        if(!$entry){throw 'Package manifest is missing.'}
        $reader=[IO.StreamReader]::new($entry.Open())
        try{$manifest=$reader.ReadToEnd()}finally{$reader.Dispose()}
        if($manifest -notmatch '(?m)^name:\s*ai-architec-rhino\s*$'){throw 'Unexpected package identity.'}
        $version=[regex]::Match($manifest,'(?m)^version:\s*(\S+)\s*$').Groups[1].Value
        if($version -notmatch '^\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?$'){throw 'Invalid package version.'}
        $InstalledPluginPath=Join-Path $env:APPDATA "McNeel\Rhinoceros\packages\8.0\ai-architec-rhino\$version\AIArchiTec.rhp"
    }finally{$archive.Dispose()}
}
if(!$InstalledPluginPath){throw 'Supply the installed .rhp path or the .yak used for installation.'}
$InstalledPluginPath=(Resolve-Path -LiteralPath $InstalledPluginPath).Path
if([IO.Path]::GetFileName($InstalledPluginPath) -ine 'AIArchiTec.rhp'){throw 'Unexpected plugin filename.'}
$installedDirectory=Split-Path -Parent $InstalledPluginPath
$installedManifest=Get-Content -LiteralPath (Join-Path $installedDirectory 'manifest.yml') -Raw
if($installedManifest -notmatch '(?m)^name:\s*ai-architec-rhino\s*$' -or !(Test-Path -LiteralPath (Join-Path $installedDirectory 'AIArchiTec.Contracts.dll'))){throw 'The installed package is incomplete or has another identity.'}
$info=[Diagnostics.FileVersionInfo]::GetVersionInfo($InstalledPluginPath)
if($info.ProductName -notin @('ai-architec-rhino','AIArchiTec.Studio') -or ([version]$info.FileVersion) -lt [version]'0.3.0.0'){throw 'The target is not a supported AI Archi-Tec Studio build.'}

$key=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($registration)
if(!$key){Write-Output 'No legacy registration found. Rhino will discover the Yak package on startup.';return}
$modified=$false
try{
    $payload=$key.OpenSubKey('PlugIn')
    try{$previous=if($payload){[string]$payload.GetValue('FileName','')}else{''}}finally{if($payload){$payload.Dispose()}}
    $commands=$key.OpenSubKey('CommandList')
    try{$hasCommand=$commands -and $commands.GetValue('archtec',$null)}finally{if($commands){$commands.Dispose()}}
}finally{$key.Dispose()}
if($previous -ieq $InstalledPluginPath -and $hasCommand){Write-Output 'Plugin registration already targets this Studio package.';return}
Write-Output "Registered plugin: $previous"
Write-Output "Installed Studio: $InstalledPluginPath"
if($CheckOnly){Write-Output 'A registration repair is required. No changes made.';return}
if(!$WhatIfPreference -and (Get-Process -Name Rhino -ErrorAction SilentlyContinue)){throw 'Close every Rhino window before repairing registration; a running prototype can overwrite it at shutdown.'}
if(!$PSCmdlet.ShouldProcess('HKCU\'+$registration,'Back up this plugin registration and point it to the installed Studio package')){return}

# Rhino cannot replace a loaded .NET assembly; this repair takes effect on the next launch.
# Touch only this plugin GUID. Preserve load protection, other plugins, documents and API settings.
$backupDirectory=Join-Path $env:LOCALAPPDATA 'AIArchiTec\InstallationBackups'
New-Item -ItemType Directory -Path $backupDirectory -Force | Out-Null
$backup=Join-Path $backupDirectory ('Rhino8-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N')+'.reg')
& "$env:SystemRoot\System32\reg.exe" export ('HKCU\'+$registration) $backup /y | Out-Null
if($LASTEXITCODE -ne 0){throw 'Registration backup failed. No registration changes were made.'}
try{
    $key=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($registration,$true)
    if(!$key){throw 'Plugin registration disappeared; restart the installer.'}
    try{
        $payload=$key.CreateSubKey('PlugIn')
        try{
            if([string]$payload.GetValue('FileName','') -cne $previous){throw 'Registration changed during installation; restart the installer.'}
            $payload.SetValue('FileName',$InstalledPluginPath,[Microsoft.Win32.RegistryValueKind]::String)
            $modified=$true
        }finally{$payload.Dispose()}
        $commands=$key.CreateSubKey('CommandList')
        try{$commands.SetValue('archtec','2;archtec',[Microsoft.Win32.RegistryValueKind]::String)}finally{$commands.Dispose()}
    }finally{$key.Dispose()}
}catch{
    if($modified){
        & "$env:SystemRoot\System32\reg.exe" import $backup | Out-Null
        throw "Registration repair failed; rollback attempted. Backup: $backup. $($_.Exception.Message)"
    }
    throw
}
Write-Output "Registration repaired. Backup: $backup"
Write-Output 'Save your work and restart Rhino. Run archtec; About shows the loaded version and file path.'
