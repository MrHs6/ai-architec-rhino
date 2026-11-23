using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Studio;

internal sealed record HostProfile(string Name, string Command, string[] Args)
{
    public static string ConfigPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AIArchiTec","Studio","connections.json");
    public static HostProfile[] Load() => File.Exists(ConfigPath) ? JsonSerializer.Deserialize<HostProfile[]>(File.ReadAllText(ConfigPath)) ?? [] : [];
    public static void Save(HostProfile profile)
    {
        var profiles = Load().Where(p => p.Name != profile.Name).Append(profile).ToArray();
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        string temp=ConfigPath+"."+Guid.NewGuid().ToString("N")+".tmp";
        File.WriteAllText(temp,JsonSerializer.Serialize(profiles));
        File.Move(temp,ConfigPath,true);
    }
}

internal sealed class StdioHost(HostProfile profile, int? rhinoPort = null) : IDisposable
{
    Process? child;
    int sequence;
    readonly SemaphoreSlim requests=new(1,1);
    async Task<JsonNode> Request(string method,object args)
    {
        int id=++sequence;
        await child!.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new{jsonrpc="2.0",id,method,@params=args}));
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(120));
        while(true)
        {
            var line=await child.StandardOutput.ReadLineAsync(timeout.Token) ?? throw new IOException("The MCP server exited.");
            var reply=JsonNode.Parse(line)!;
            if(reply["id"]==null)continue;
            if(reply["method"]!=null){
                await child.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new{jsonrpc="2.0",id=reply["id"],error=new{code=-32601,message="Client service unavailable"}}));
                continue;
            }
            if((int?)reply["id"]!=id)throw new IOException("Unexpected reply ID.");
            return reply["result"] ?? throw new IOException(reply["error"]?["message"]?.ToString() ?? "Server rejected request.");
        }
    }
    public async Task<JsonNode> Call(string method,object args)
    {
        await requests.WaitAsync();
        try
        {
            if(child==null || child.HasExited){
                Dispose();
                if(!Path.IsPathFullyQualified(profile.Command)||!File.Exists(profile.Command))throw new IOException("MCP executable is unavailable.");
                var start=new ProcessStartInfo(profile.Command){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,StandardInputEncoding=new UTF8Encoding(false),StandardOutputEncoding=Encoding.UTF8};
                if (rhinoPort.HasValue) start.Environment["ARCHITEC_MCP_PORT"] = rhinoPort.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                foreach(var arg in profile.Args)start.ArgumentList.Add(arg);
                child=Process.Start(start)!;
                await Request("initialize",new{protocolVersion="2025-03-26",capabilities=new{},clientInfo=new{name="AI Archi-Tec Studio",version=typeof(StdioHost).Assembly.GetName().Version!.ToString(3)}});
                await child.StandardInput.WriteLineAsync("{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}");
            }
            return await Request(method,args);
        }
        catch { Dispose(); throw; }
        finally { requests.Release(); }
    }
    public async Task<JsonArray> Tools()
    {
        var all=new JsonArray(); string? cursor=null;
        var seen=new HashSet<string>();
        do {
            var page=await Call("tools/list",cursor==null?new JsonObject():Wire.Node(new{cursor}));
            foreach(var t in page["tools"]!.AsArray())all.Add(t!.DeepClone());
            cursor=(string?)page["nextCursor"];
            if(cursor!=null && !seen.Add(cursor))throw new IOException("Repeated tools cursor.");
        }while(cursor!=null);
        return all;
    }
    public void Dispose(){ if(child!=null){try{if(!child.HasExited)child.Kill();}finally{child.Dispose();child=null;}} }
}
