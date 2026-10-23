using System.Text;
using System.Text.Json;
using ArchiTec.Core;

Console.InputEncoding = new UTF8Encoding(false);
Console.OutputEncoding = new UTF8Encoding(false);
var port = int.TryParse(Environment.GetEnvironmentVariable("ARCHITEC_MCP_PORT"), out var p) ? p : 5188;
if (port is < 1024 or > 65535) { Console.Error.WriteLine("Invalid Archi-Tec MCP port."); return 1; }
await using var bridge = new ReconnectingBridge(port);
var writes=new SemaphoreSlim(1);var active=new List<Task>();
async Task Dispatch(JsonElement root)
{
    try
    {
        var reply=await bridge.Handle(root);
        if(reply is not null){await writes.WaitAsync();try{Console.WriteLine(JsonSerializer.Serialize(reply));}finally{writes.Release();}}
    }
    catch{Console.Error.WriteLine("Archi-Tec bridge request failed; no request was replayed.");}
}
while (await Console.In.ReadLineAsync() is { } line)
{
    JsonElement id = default;
    try
    {
        if (line.Length > 2_000_000) throw new JsonException();
        using var json = JsonDocument.Parse(line);
        var root = json.RootElement;
        if (root.TryGetProperty("id", out var value)) id = value.Clone();
        if (root.GetProperty("jsonrpc").GetString() != "2.0") throw new JsonException();
        active.RemoveAll(t=>t.IsCompleted);
        if(active.Count>=64&&root.TryGetProperty("id",out _))
        {await writes.WaitAsync();try{Console.WriteLine(JsonSerializer.Serialize(new{jsonrpc="2.0",id,error=new{code=-32000,message="Too many pending requests."}}));}finally{writes.Release();}continue;}
        active.Add(Dispatch(root.Clone()));
    }
    catch
    {
        if (id.ValueKind != JsonValueKind.Undefined)
        {await writes.WaitAsync();try{Console.WriteLine(JsonSerializer.Serialize(new { jsonrpc = "2.0", id, error = new { code = -32600, message = "Invalid MCP request." } }));}finally{writes.Release();}}
    }
}
bridge.Stop();await Task.WhenAll(active);writes.Dispose();
return 0;
