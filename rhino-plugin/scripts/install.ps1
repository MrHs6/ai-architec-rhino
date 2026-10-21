[CmdletBinding(SupportsShouldProcess=$true)]
param(
    [string]$PackagePath='',
    [uri]$PackageUrl,
    [string]$Sha256='',
    [string]$RhinoSystemDir=(Join-Path $env:ProgramFiles 'Rhino 8\System'),
    [switch]$CheckOnly
)
$ErrorActionPreference='Stop'
if($PackageUrl -and $PackagePath){throw 'Specify PackagePath or PackageUrl, not both.'}
if($PackageUrl){
    if($PackageUrl.Scheme -ne 'https' -or $PackageUrl.UserInfo){throw 'PackageUrl must be an HTTPS URL without credentials.'}
    if($Sha256 -notmatch '^[a-fA-F0-9]{64}$'){throw 'Remote installation requires the release SHA256 value.'}
}
$yak=Join-Path $RhinoSystemDir 'Yak.exe'
$rhino=Join-Path $RhinoSystemDir 'Rhino.exe'
if(!(Test-Path -LiteralPath $yak) -or !(Test-Path -LiteralPath $rhino)){throw 'Rhino 8 was not found. Supply -RhinoSystemDir for a custom installation.'}
$rhinoVersion=[Diagnostics.FileVersionInfo]::GetVersionInfo($rhino)
if($rhinoVersion.FileMajorPart -ne 8 -or $rhinoVersion.FileMinorPart -lt 31){throw 'This package requires Rhino 8.31 or later within Rhino 8.'}
$downloadDirectory=$null
try{
    if($PackageUrl){
        if(!$PSCmdlet.ShouldProcess($PackageUrl.AbsoluteUri,'Download and verify Rhino package')){return}
        $downloadDirectory=Join-Path ([IO.Path]::GetTempPath()) ('AIArchiTec-'+[guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $downloadDirectory | Out-Null
        $leaf=[IO.Path]::GetFileName($PackageUrl.AbsolutePath)
        if($leaf -notmatch '^ai-architec-rhino-[A-Za-z0-9.-]+-rh8_31-win\.yak$'){throw 'The URL must point directly to the versioned AI Archi-Tec .yak asset.'}
        $PackagePath=Join-Path $downloadDirectory $leaf
        Invoke-WebRequest -Uri $PackageUrl -OutFile $PackagePath -UseBasicParsing
    }elseif(!$PackagePath){
        $candidates=@(Get-ChildItem -LiteralPath $PSScriptRoot -Filter 'ai-architec-rhino-*-rh8_31-win.yak' -File)
        if($candidates.Count -ne 1){throw 'Supply -PackagePath, or extract the installer ZIP containing exactly one .yak next to this script.'}
        $PackagePath=$candidates[0].FullName
    }
    $PackagePath=(Resolve-Path -LiteralPath $PackagePath).Path
    if([IO.Path]::GetExtension($PackagePath) -ne '.yak'){throw 'PackagePath must be a .yak file.'}
    if(!$Sha256){
        $checksumPath=Join-Path (Split-Path -Parent $PackagePath) 'SHA256SUMS.txt'
        if(Test-Path -LiteralPath $checksumPath){
            foreach($line in Get-Content -LiteralPath $checksumPath){
                $parts=$line -split '\s+',2
                if($parts.Count -eq 2 -and $parts[1] -eq [IO.Path]::GetFileName($PackagePath)){$Sha256=$parts[0];break}
            }
        }
    }
    if($Sha256){
        if($Sha256 -notmatch '^[a-fA-F0-9]{64}$' -or (Get-FileHash -LiteralPath $PackagePath -Algorithm SHA256).Hash -ne $Sha256){throw 'Package checksum mismatch.'}
    }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive=[IO.Compression.ZipFile]::OpenRead($PackagePath)
    try{
        $entry=$archive.GetEntry('manifest.yml')
        if(!$entry -or !$archive.GetEntry('AIArchiTec.rhp')){throw 'Required plugin payload is missing.'}
        $reader=[IO.StreamReader]::new($entry.Open())
        try{$manifest=$reader.ReadToEnd()}finally{$reader.Dispose()}
        if($manifest -notmatch '(?m)^name:\s*ai-architec-rhino\s*$'){throw 'Unexpected package identity.'}
    }finally{$archive.Dispose()}
    if($CheckOnly){Write-Output 'Installation prerequisites, package identity and supplied checksum verified.';return}
    if($PSCmdlet.ShouldProcess($PackagePath,'Install AI Archi-Tec for the current Windows user')){
        & $yak install $PackagePath
        if($LASTEXITCODE -ne 0){throw "Yak installation failed with exit code $LASTEXITCODE."}
        Write-Output 'Installed. Save your work, restart Rhino, then run AIArchitect. Configure your own API key or localhost MCP in Settings.'
    }
}finally{
    # Remove only the single download created here, never a user-provided file.
    if($downloadDirectory){
        if($PackagePath -and (Test-Path -LiteralPath $PackagePath) -and [IO.Path]::GetDirectoryName($PackagePath) -eq $downloadDirectory){Remove-Item -LiteralPath $PackagePath}
        if(Test-Path -LiteralPath $downloadDirectory){Remove-Item -LiteralPath $downloadDirectory}
    }
}
