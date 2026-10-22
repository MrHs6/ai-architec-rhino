using System.Diagnostics;
using System.Net;
using System.Text.Json;
using ArchiTec.Core;

static class BridgeReconnectTests
{
    public static async Task<int> Run()
    {
        var passed=0;void Check(bool ok,string name){if(!ok)throw new Exception(name);passed++;Console.WriteLine("PASS "+name);}
        var merged=McpClientInstaller.MergeClaude("""{"theme":"dark","mcpServers":{"another":{"command":"keep-me","env":{"KEY":"fixture"}}}}""",@"C:\Apps\ArchiTec\bridge.exe",5188);
        using(var json=JsonDocument.Parse(merged))Check(json.RootElement.GetProperty("theme").GetString()=="dark"&&json.RootElement.GetProperty("mcpServers").GetProperty("another").GetProperty("command").GetString()=="keep-me"&&!json.RootElement.GetProperty("mcpServers").GetProperty("ai-architec-rhino").GetRawText().Contains("TOKEN"),"Automatic Claude setup preserves unrelated settings and exports no session token");
        try{McpClientInstaller.MergeClaude("[]","bridge",5188);throw new Exception("Invalid Claude config accepted");}catch(ArgumentException){Check(true,"Invalid existing Claude configuration is rejected without overwriting it");}
        if(!OperatingSystem.IsWindows())return passed;
        var probe=new System.Net.Sockets.TcpListener(IPAddress.Loopback,0);probe.Start();var port=((IPEndPoint)probe.LocalEndpoint).Port;probe.Stop();
        var owner=Guid.NewGuid().ToString("N");var messages=0;var rejectedPlans=0;McpServer? server=null;
        var bridge=Path.GetFullPath("../../../../../src/ArchiTec.McpBridge/bin/Release/net8.0/AIArchiTec.McpBridge.exe",AppContext.BaseDirectory);
        var start=new ProcessStartInfo(bridge){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
        start.Environment.Remove("ARCHITEC_MCP_TOKEN");start.Environment.Remove("ARCHITEC_MCP_URL");start.Environment["ARCHITEC_MCP_PORT"]=port.ToString();
        using var process=Process.Start(start)??throw new Exception("Bridge did not start");
        var stderr=process.StandardError.ReadToEndAsync();var serial=0;
        async Task<JsonElement> Send(string method,object args)
        {
            var id=++serial;await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new{jsonrpc="2.0",id,method,@params=args}));await process.StandardInput.FlushAsync();
            var line=await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(12));
            using var reply=JsonDocument.Parse(line??throw new Exception("Bridge closed stdout"));
            Check(reply.RootElement.GetProperty("id").GetInt32()==id,"Bridge keeps request correlation across reconnects");
            return reply.RootElement.GetProperty("result").Clone();
        }
        async Task<JsonElement> Tool(string name,object args)=>await Send("tools/call",new{name,arguments=args});
        void Start()
        {
            server=new McpServer(port,(name,args,cancel)=>
            {
                if(name=="propose_plan"){rejectedPlans++;throw new InvalidOperationException("Fixture plan rejected");}
                if(name=="post_message")messages++;
                return Task.FromResult<object>(new{ok=true,message=args.TryGetProperty("message",out var text)?text.GetString():"context"});
            });server.Start();McpSessionFile.Publish(port,server.Endpoint,server.Token,owner);
        }
        void Stop(){McpSessionFile.Remove(port,owner);server?.Dispose();server=null;}
        try
        {
            var hello=await Send("initialize",new{protocolVersion="2025-11-25",capabilities=new{},clientInfo=new{name="reconnect fixture",version="1"}});
            Check(hello.GetProperty("serverInfo").GetProperty("name").GetString()=="ai-architec-rhino","A client can initialize its stable bridge before Rhino MCP starts");
            var catalog=await Send("tools/list",new{});Check(catalog.GetProperty("tools").GetArrayLength()==4,"Tools remain discoverable while Rhino MCP is off");
            var offline=await Tool("get_context",new{});Check(offline.GetProperty("isError").GetBoolean()&&!process.HasExited,"Offline requests return actionable tool errors without killing the bridge");
            for(var cycle=0;cycle<3;cycle++)
            {
                Start();
                if(cycle==0)
                {
                    var until=DateTime.UtcNow.AddSeconds(8);while(server!.Connection.Snapshot().Clients==0&&DateTime.UtcNow<until)await Task.Delay(100);
                    Check(server.Connection.Snapshot().ClientName=="reconnect fixture","An already-open client reconnects passively after the server starts");
                }
                var context=await Tool("get_context",new{});Check(!context.GetProperty("isError").GetBoolean(),"The same bridge reaches the new authenticated server after restart "+(cycle+1));
                var message=await Tool("post_message",new{message="سلام ×",document_id=1});
                using(var body=JsonDocument.Parse(message.GetProperty("content")[0].GetProperty("text").GetString()!))Check(body.RootElement.GetProperty("message").GetString()=="سلام ×"&&messages==cycle+1,"UTF-8 messages are delivered exactly once after reconnect "+(cycle+1));
                if(cycle<2){Stop();var stopped=await Tool("get_context",new{});Check(stopped.GetProperty("isError").GetBoolean(),"Stopping MCP produces an honest offline error without a client reload");}
            }
            var rejected=await Tool("propose_plan",new{});Check(rejected.GetProperty("isError").GetBoolean()&&rejectedPlans==1,"A rejected modeling request is never replayed automatically");
            process.StandardInput.Close();await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Check(process.ExitCode==0&&server!.Connection.Snapshot().Clients==0,"The reconnecting bridge closes its last authenticated session on EOF");
        }
        finally{if(!process.HasExited)process.Kill();Stop();}
        return passed;
    }
}
