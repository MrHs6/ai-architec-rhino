using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

// Stdout carries only MCP messages. Secrets are never printed.
var endpoint=Environment.GetEnvironmentVariable("ARCHITEC_MCP_URL")??"http://127.0.0.1:5188/mcp";
var token=Environment.GetEnvironmentVariable("ARCHITEC_MCP_TOKEN");
if(!Uri.TryCreate(endpoint,UriKind.Absolute,out var uri) || uri.Scheme!="http" || !uri.IsLoopback || string.IsNullOrWhiteSpace(token))
{Console.Error.WriteLine("Configure the local MCP URL and token using the Rhino panel's Copy configuration button.");return 1;}
using var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(35)};
client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",token);
client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/event-stream");
client.DefaultRequestHeaders.Add("MCP-Protocol-Version","2025-11-25");
while(await Console.In.ReadLineAsync() is { } line)
{
    JsonElement id=default;
    try
    {
        using var document=JsonDocument.Parse(line);
        if(document.RootElement.TryGetProperty("id",out var requestId))id=requestId.Clone();
        using var body=new StringContent(line,Encoding.UTF8,"application/json");
        using var response=await client.PostAsync(uri,body);
        if(response.StatusCode==System.Net.HttpStatusCode.Accepted)continue;
        response.EnsureSuccessStatusCode();
        var answer=await response.Content.ReadAsStringAsync();
        using var json=JsonDocument.Parse(answer);
        Console.WriteLine(JsonSerializer.Serialize(json.RootElement));
    }
    catch(Exception)
    {
        if(id.ValueKind!=JsonValueKind.Undefined)
            Console.WriteLine(JsonSerializer.Serialize(new {jsonrpc="2.0",id,error=new {code=-32603,message="Rhino MCP is unavailable or rejected this request. Open the panel, start MCP and copy its current configuration."}}));
        else Console.Error.WriteLine("MCP notification could not be delivered to Rhino.");
    }
}
return 0;
