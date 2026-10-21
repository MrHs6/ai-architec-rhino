using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ArchiTec.Core;

static class McpConnectionTests
{
    public static async Task<int> Run()
    {
        var passed=0;void Check(bool ok,string name){if(!ok)throw new Exception(name);passed++;Console.WriteLine("PASS "+name);}
        var socket=new System.Net.Sockets.TcpListener(IPAddress.Loopback,0);socket.Start();var port=((IPEndPoint)socket.LocalEndpoint).Port;socket.Stop();
        var dispatched="";
        using var server=new McpServer(port,(name,args,cancel)=>{dispatched=name;return Task.FromResult<object>(new{ok=true});});server.Start();
        using var client=new HttpClient();client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/event-stream");
        async Task<HttpResponseMessage> Post(string body)=>await client.PostAsync(server.Endpoint,new StringContent(body,Encoding.UTF8,"application/json"));
        var initialize="""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","clientInfo":{"name":"ArchiTec protocol acceptance","version":"1"},"capabilities":{}}}""";
        using(var rejected=await Post(initialize))Check(rejected.StatusCode==HttpStatusCode.Unauthorized&&server.Connection.Snapshot().ClientName is null,"Unauthenticated traffic cannot announce a connected client");
        client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",server.Token);
        string session;
        using(var response=await Post(initialize)){session=response.Headers.GetValues("Mcp-Session-Id").Single();Check(server.Connection.Snapshot().ClientName=="ArchiTec protocol acceptance"&&session.Length==32,"Authenticated initialization identifies the announced client and creates a bounded session");}
        client.DefaultRequestHeaders.Add("Mcp-Session-Id",session);
        using(var response=await Post("""{"jsonrpc":"2.0","id":2,"method":"tools/list"}"""))Check(server.Connection.Snapshot().Stage=="tools_ready","Tool discovery advances the visible connection stage");
        using(var response=await Post("""{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"post_message","arguments":{"document_id":1,"message":"Hello","workspace":"analyze"}}}"""))Check(dispatched=="post_message","Conversational replies have a dedicated MCP tool without geometry execution");
        server.Connection.Workflow("preview","awaiting Apply");
        using(var response=await Post("""{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"get_plan_status","arguments":{"request_id":"fixture"}}}"""))Check(server.Connection.Snapshot().Stage=="preview","Polling execution status does not overwrite the preview workflow stage");
        using(var request=new HttpRequestMessage(HttpMethod.Delete,server.Endpoint))using(var response=await client.SendAsync(request))Check(response.StatusCode==HttpStatusCode.NoContent&&server.Connection.Snapshot().Clients==0,"Client shutdown clears its actual session");
        using(var response=await Post("""{"jsonrpc":"2.0","id":5,"method":"ping"}"""))Check(response.StatusCode==HttpStatusCode.NotFound,"An expired session cannot masquerade as connected");
        var claude=McpClientSetup.Configuration("Claude Desktop",@"C:\Program Files\ArchiTec\AIArchiTec.McpBridge.exe",port);
        using(var json=JsonDocument.Parse(claude))Check(json.RootElement.GetProperty("mcpServers").GetProperty("ai-architec-rhino").GetProperty("env").GetProperty("ARCHITEC_MCP_PORT").GetString()==port.ToString()&&!claude.Contains("TOKEN"),"Claude JSON contains the correct bridge port and no copied secret");
        var codex=McpClientSetup.Configuration("Codex",@"C:\Program Files\ArchiTec\AIArchiTec.McpBridge.exe",port);
        Check(codex.Contains("[mcp_servers.ai-architec-rhino]")&&codex.Contains("C:\\\\Program Files")&&!codex.Contains("TOKEN"),"Codex receives TOML with escaped Windows paths rather than Claude JSON");
        Check(McpClientSetup.Guide("ChatGPT web").Contains("Secure MCP Tunnel")&&McpClientSetup.Guide("ChatGPT web").Contains("does not configure"),"Web setup explains the separate connector requirement");
        if(OperatingSystem.IsWindows())
        {
            var owner=Guid.NewGuid().ToString("N");
            try
            {
                McpSessionFile.Publish(port,server.Endpoint,server.Token,owner);
                Check(!File.ReadAllText(McpSessionFile.PathFor(port)).Contains(server.Token)&&McpSessionFile.Read(port).Token==server.Token,"Windows discovery protects the session token and restores it for the same account");
                McpSessionFile.Remove(port,"another-session");Check(File.Exists(McpSessionFile.PathFor(port)),"Session cleanup cannot delete another Rhino owner's descriptor");
                var bridge=Path.GetFullPath("../../../../../src/ArchiTec.McpBridge/bin/Release/net8.0/AIArchiTec.McpBridge.exe",AppContext.BaseDirectory);
                var start=new ProcessStartInfo(bridge){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
                start.Environment.Remove("ARCHITEC_MCP_TOKEN");start.Environment.Remove("ARCHITEC_MCP_URL");start.Environment["ARCHITEC_MCP_PORT"]=port.ToString();
                using var process=Process.Start(start)??throw new Exception("Bridge did not start.");
                try
                {
                    await process.StandardInput.WriteLineAsync(initialize);await process.StandardInput.FlushAsync();
                    var line=await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10));
                    using var reply=JsonDocument.Parse(line??"");
                    Check(reply.RootElement.TryGetProperty("result",out _)&&server.Connection.Snapshot().Clients==1&&!line!.Contains(server.Token),"The packaged stdio bridge performs a real handshake using encrypted discovery without printing a token");
                    process.StandardInput.Close();await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
                    Check(process.ExitCode==0&&server.Connection.Snapshot().Clients==0,"Closing bridge stdin ends the server's tracked client session");
                }
                finally{if(!process.HasExited)process.Kill();}
            }
            finally{McpSessionFile.Remove(port,owner);}
        }
        return passed;
    }
}
