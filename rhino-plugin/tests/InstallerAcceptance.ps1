param([string]$PackageDirectory)
$ErrorActionPreference='Stop'
$scripts=Join-Path $PSScriptRoot '..\scripts'
foreach($file in Get-ChildItem $scripts -Filter '*.ps1'){
    $tokens=$null;$errors=$null
    [System.Management.Automation.Language.Parser]::ParseFile($file.FullName,[ref]$tokens,[ref]$errors) | Out-Null
    if($errors.Count){throw "PowerShell parse failure: $($file.Name)"}
}
$package=Get-ChildItem $PackageDirectory -Filter '*-rh9-win.yak' | Select-Object -First 1
& (Join-Path $scripts 'install.ps1') -RhinoMajor 9 -CheckOnly -PackagePath $package.FullName
$wrong=Get-ChildItem $PackageDirectory -Filter '*-rh8_31-win.yak' | Select-Object -First 1
$rejected=$false
try{& (Join-Path $scripts 'install.ps1') -RhinoMajor 9 -CheckOnly -PackagePath $wrong.FullName}catch{
    if($_.Exception.Message -notlike '*distribution does not match*'){throw}
    $rejected=$true
}
if(!$rejected){throw 'Rhino 9 accepted a Rhino 8 distribution.'}
Write-Output 'PASS script syntax, Rhino 9 package/checksum prerequisites and cross-version rejection.'

