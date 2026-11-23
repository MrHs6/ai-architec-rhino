param([string]$App = (Join-Path $PSScriptRoot '..\outputs\Studio-0.1\App\ArchiTecStudio.exe'))
$ErrorActionPreference = 'Stop'
$process = [Diagnostics.Process]::new()
$process.StartInfo.FileName = $App
$process.StartInfo.Arguments = '--mcp Both'
$process.StartInfo.UseShellExecute = $false
$process.StartInfo.RedirectStandardInput = $true
$process.StartInfo.RedirectStandardOutput = $true
$process.StartInfo.CreateNoWindow = $true
[void]$process.Start()
$sequence = 0
function Invoke-Studio($method,$params) {
    $script:sequence++
    $line = @{jsonrpc='2.0';id=$sequence;method=$method;params=$params} | ConvertTo-Json -Compress -Depth 20
    $process.StandardInput.WriteLine($line)
    $read = $process.StandardOutput.ReadLineAsync()
    if (!$read.Wait(25000)) { throw "Timeout on $method" }
    $reply = $read.Result | ConvertFrom-Json
    if ($reply.id -ne $sequence -or $null -ne $reply.error) { throw "Unexpected reply to $method" }
    $reply.result
}
try {
    $hello = Invoke-Studio initialize @{protocolVersion='2025-03-26';capabilities=@{};clientInfo=@{name='test';version='1'}}
    $process.StandardInput.WriteLine('{"jsonrpc":"2.0","method":"notifications/initialized"}')
    $catalog = Invoke-Studio 'tools/list' @{}
    if ($catalog.tools.name -notcontains 'rhino_execute_plan' -or $catalog.tools.name -notcontains 'revit_create_wall') { throw 'Missing tool' }
    Write-Output ("MCP handshake + catalog: " + $catalog.tools.Count + " tools")
    foreach ($hostName in 'rhino','revit') {
        $r = Invoke-Studio 'tools/call' @{name=$hostName+'_get_context';arguments=@{}}
        if ($r.isError) { Write-Output ($hostName+' unavailable: '+$r.content[0].text) }
        else { Write-Output ($hostName+' live: '+$r.content[0].text.Substring(0,[Math]::Min(320,$r.content[0].text.Length))) }
    }
} finally { $process.StandardInput.Close(); if (!$process.WaitForExit(3000)) { $process.Kill() }; $process.Dispose() }
