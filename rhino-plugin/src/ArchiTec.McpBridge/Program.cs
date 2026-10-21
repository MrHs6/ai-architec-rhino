using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ArchiTec.Core;

// Only JSON-RPC replies go to stdout. Discovery credentials stay on this Windows account.
var port=int.TryParse(Environment.GetEnvironmentVariable("ARCHITEC_MCP_PORT"),out var configuredPort)?configuredPort:5188;
var endpoint=Environment.GetEnvironmentVariable("ARCHITEC_MCP_URL");
var token=Environment.GetEnvironmentVariable("ARCHITEC_MCP_TOKEN");
try{if(string.IsNullOrWhiteSpace(token)){var local=McpSessionFile.Read(port);endpoint=local.Endpoint;token=local.Token;}}
catch{Console.Error.WriteLine("Start MCP in the Rhino Archi-Tec panel on the configured port, then reload this client connection.");return 1;}
if(!Uri.TryCreate(endpoint,UriKind.Absolute,out var uri)||uri.Scheme!="http"||!uri.IsLoopback||uri.UserInfo.Length>0||uri.Query.Length>0||uri.Fragment.Length>0||uri.AbsolutePath!="/mcp"||string.IsNullOrWhiteSpace(token))
{Console.Error.WriteLine("Use the local connection configuration from the Rhino Archi-Tec MCP page.");return 1;}
using var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(35)};
client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",token);
client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/event-stream");
client.DefaultRequestHeaders.Add("MCP-Protocol-Version","2025-11-25");
string? session=null;
using var lifetime=new CancellationTokenSource();
var heartbeat=Task.Run(async()=>
{
    try{while(!lifetime.IsCancellationRequested){await Task.Delay(TimeSpan.FromSeconds(15),lifetime.Token);var current=Volatile.Read(ref session);if(current is null)continue;using var ping=new HttpRequestMessage(HttpMethod.Post,uri){Content=new StringContent("{\"jsonrpc\":\"2.0\",\"id\":\"bridge-heartbeat\",\"method\":\"ping\"}",Encoding.UTF8,"application/json")};ping.Headers.Add("Mcp-Session-Id",current);using var response=await client.SendAsync(ping,lifetime.Token);}}
    catch(OperationCanceledException){}catch{Console.Error.WriteLine("Rhino MCP heartbeat ended. Reload the connection after starting MCP in Rhino.");}
});
while(await Console.In.ReadLineAsync() is { } line)
{
    JsonElement id=default;
    try
    {
        if(line.Length>300_000)throw new InvalidOperationException();
        using var document=JsonDocument.Parse(line);
        if(document.RootElement.TryGetProperty("id",out var requestId))id=requestId.Clone();
        using var outgoing=new HttpRequestMessage(HttpMethod.Post,uri){Content=new StringContent(line,Encoding.UTF8,"application/json")};
        if(session is not null)outgoing.Headers.Add("Mcp-Session-Id",session);
        using var response=await client.SendAsync(outgoing);
        if(response.Headers.TryGetValues("Mcp-Session-Id",out var values))session=values.First();
        if(response.StatusCode==System.Net.HttpStatusCode.Accepted)continue;
        response.EnsureSuccessStatusCode();
        using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Console.WriteLine(JsonSerializer.Serialize(json.RootElement));
    }
    catch(Exception)
    {
        if(id.ValueKind!=JsonValueKind.Undefined)Console.WriteLine(JsonSerializer.Serialize(new{jsonrpc="2.0",id,error=new{code=-32603,message="Rhino MCP is unavailable or rejected this request. Start MCP in Rhino and reload the client connection. Legacy token configurations must be copied again after a restart."}}));
        else Console.Error.WriteLine("MCP notification could not be delivered to Rhino.");
    }
}
 lifetime.Cancel();await heartbeat;
if(session is not null)try{using var goodbye=new HttpRequestMessage(HttpMethod.Delete,uri);goodbye.Headers.Add("Mcp-Session-Id",session);using var response=await client.SendAsync(goodbye);}catch{}
return 0;