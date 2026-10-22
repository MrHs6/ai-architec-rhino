using System.Text;
using System.Text.Json;
using ArchiTec.Core;

Console.InputEncoding = new UTF8Encoding(false);
Console.OutputEncoding = new UTF8Encoding(false);
var port = int.TryParse(Environment.GetEnvironmentVariable("ARCHITEC_MCP_PORT"), out var p) ? p : 5188;
if (port is < 1024 or > 65535) { Console.Error.WriteLine("Invalid Archi-Tec MCP port."); return 1; }
await using var bridge = new ReconnectingBridge(port);
while (await Console.In.ReadLineAsync() is { } line)
{
    JsonElement id = default;
    try
    {
        if (line.Length > 300_000) throw new JsonException();
        using var json = JsonDocument.Parse(line);
        var root = json.RootElement;
        if (root.TryGetProperty("id", out var value)) id = value.Clone();
        if (root.GetProperty("jsonrpc").GetString() != "2.0") throw new JsonException();
        var reply = await bridge.Handle(root);
        if (reply is not null) Console.WriteLine(JsonSerializer.Serialize(reply));
    }
    catch
    {
        if (id.ValueKind != JsonValueKind.Undefined)
            Console.WriteLine(JsonSerializer.Serialize(new { jsonrpc = "2.0", id, error = new { code = -32600, message = "Invalid MCP request." } }));
    }
}
return 0;
