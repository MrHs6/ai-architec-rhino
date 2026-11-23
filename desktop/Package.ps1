param([string]$Sdk='dotnet',[string]$Wix='wix')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$stage=Join-Path $root 'outputs/Studio-0.7.0'
$app=Join-Path $stage 'App'

New-Item -ItemType Directory -Path "$app/Bridges" -Force | Out-Null
& $Sdk publish "$PSScriptRoot/Studio.csproj" -r win-x64 --self-contained true -c Release -o $app -p:NuGetAudit=false -p:DebugType=None -p:DebugSymbols=false
if ($LASTEXITCODE) {throw 'Desktop build failed.'}
& $Sdk build "$root/revit-plugin/ArchiTec.Revit.csproj" -c Release -p:NuGetAudit=false
if ($LASTEXITCODE) {throw 'Revit build failed.'}
Copy-Item "$root/revit-plugin/bin/Release/net8.0-windows/ArchiTec.Revit.dll","$PSScriptRoot/Studio.Revit2025.addin" "$app/Bridges" -Force
foreach ($major in 8,9) {
    $hostArgs = if ($major -eq 8) { @('-p:BuildWithNuGet=true') } else { @("-p:RhinoSystemDir=$env:ProgramFiles/Rhino 9/System") }
    $dest = "$app/Bridges/Rhino$major"
    $compiled = "$stage/Native$major"
    & $Sdk build "$root/studio-rhino-native/StudioRhino.csproj" -c Release $hostArgs -p:NuGetAudit=false -o $compiled
    if ($LASTEXITCODE) {throw "Rhino $major connector build failed."}
    New-Item -ItemType Directory -Path $dest -Force | Out-Null
    Copy-Item "$compiled/ArchiTecStudioNative.rhp" $dest -Force
    Set-Content "$dest/manifest.yml" "name: architec-studio-native`nversion: 0.7.0`nauthors: [AI Archi-Tec]`ndescription: Native document connector for Archi-Tec Studio"
}
Copy-Item "$PSScriptRoot/Quickstart.md" $app -Force
Add-Type -AssemblyName System.Drawing
$photo=[Drawing.Image]::FromFile("$PSScriptRoot/Assets/studio-entry.jpg")
foreach ($art in @(@('Dialog',493,312,162),@('Banner',493,58,110))) {
    $bmp=[Drawing.Bitmap]::new($art[1],$art[2])
    $g=[Drawing.Graphics]::FromImage($bmp)
    $g.Clear([Drawing.Color]::White)
    $x=if ($art[0] -eq 'Banner') {$art[1]-$art[3]} else {0}
    $dest=[Drawing.Rectangle]::new([int]$x,0,[int]$art[3],[int]$art[2])
    $crop=[Math]::Min($photo.Width,[int]($photo.Height*$art[3]/$art[2]))
    $g.DrawImage($photo,$dest,([Drawing.Rectangle]::new([int](($photo.Width-$crop)/2),0,$crop,$photo.Height)),[Drawing.GraphicsUnit]::Pixel)
    $bmp.Save("$stage/$($art[0]).bmp",[Drawing.Imaging.ImageFormat]::Bmp)
    $g.Dispose(); $bmp.Dispose()
}
$photo.Dispose()
& $Wix build "$PSScriptRoot/StudioSetup.wxs" -arch x64 -ext WixToolset.UI.wixext -d "Payload=$app" -d "BrandIcon=$PSScriptRoot/Assets/Studio.ico" -d "DialogArt=$stage/Dialog.bmp" -d "BannerArt=$stage/Banner.bmp" -o "$root/outputs/AI-Archi-Tec-Studio-0.7.0-x64.msi"
if ($LASTEXITCODE) {throw 'MSI build failed.'}
