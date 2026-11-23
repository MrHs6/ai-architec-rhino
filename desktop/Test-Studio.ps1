param([string]$Sdk="dotnet")
& $Sdk run --project "$PSScriptRoot/../desktop-tests/Studio.Acceptance.csproj" -- --connections
exit $LASTEXITCODE
