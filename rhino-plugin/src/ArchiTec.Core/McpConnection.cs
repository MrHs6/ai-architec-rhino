using System.Text.Json;

namespace ArchiTec.Core;

public sealed record McpActivity(DateTimeOffset At,string Stage,string Detail);
public sealed record McpConnectionSnapshot(string Stage,string? ClientName,DateTimeOffset? LastSeenUtc,int Clients,IReadOnlyList<McpActivity> Activity);

/// <summary>Protocol observations, not proof of a user's identity or paid account.</summary>
public sealed class McpConnectionMonitor
{
    private sealed record Client(string Name,DateTimeOffset Seen);
    private readonly object gate=new();
    private readonly Dictionary<string,Client> clients=[];
    private readonly List<McpActivity> activity=[];
    private string stage="waiting";
    public string Initialize(JsonElement args)
    {
        var name=args.TryGetProperty("clientInfo",out var info)&&info.TryGetProperty("name",out var n)&&n.ValueKind==JsonValueKind.String?n.GetString():null;
        name=new string((name??"MCP client").Where(c=>!char.IsControl(c)).Take(64).ToArray());
        lock(gate)
        {
            if(clients.Count>=16)clients.Remove(clients.OrderBy(p=>p.Value.Seen).First().Key);
            var id=Guid.NewGuid().ToString("N");clients[id]=new(name,DateTimeOffset.UtcNow);Add("initialized",name);return id;
        }
    }
    public bool Contains(string? id){lock(gate)return id is not null&&clients.ContainsKey(id);}
    public void Touch(string? id,string next,string detail)
    {
        lock(gate)
        {
            if(id is not null&&clients.TryGetValue(id,out var client))clients[id]=client with{Seen=DateTimeOffset.UtcNow};
            if(next=="heartbeat")return;
            Add(next,detail);
        }
    }
    public void Disconnect(string id){lock(gate){if(clients.Remove(id))Add("disconnected","Client session closed");}}
    public void Workflow(string next,string detail){lock(gate)Add(next,detail);}
    private void Add(string next,string detail)
    {
        stage=next;activity.Add(new(DateTimeOffset.UtcNow,next,detail));if(activity.Count>12)activity.RemoveAt(0);
    }
    public McpConnectionSnapshot Snapshot()
    {
        lock(gate)
        {
            var latest=clients.Values.OrderByDescending(c=>c.Seen).FirstOrDefault();
            return new(stage,latest?.Name,latest?.Seen,clients.Count,activity.ToArray());
        }
    }
}

public static class McpClientSetup
{
    public static string Configuration(string client,string bridge,int port)
    {
        if(port is <1024 or >65535)throw new ArgumentException("Invalid MCP port.");
        if(client=="Codex")return "[mcp_servers.ai-architec-rhino]\ncommand = "+JsonSerializer.Serialize(bridge)+"\nstartup_timeout_sec = 20\ntool_timeout_sec = 45\n\n[mcp_servers.ai-architec-rhino.env]\nARCHITEC_MCP_PORT = \""+port+"\"\n";
        return JsonSerializer.Serialize(new{mcpServers=new Dictionary<string,object>{["ai-architec-rhino"]=new{command=bridge,env=new Dictionary<string,string>{["ARCHITEC_MCP_PORT"]=port.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}},new JsonSerializerOptions{WriteIndented=true});
    }
    public static string Guide(string client)=>client switch
    {
        "Codex"=>"1. In the client's MCP settings, add a STDIO server named ai-architec-rhino, or merge the copied TOML into config.toml.\n2. Use the bridge command and ARCHITEC_MCP_PORT from the copied setup. Keep your other server entries.\n3. Reload the connection. In the CLI, codex mcp list shows configuration and /mcp shows active tools.\n4. Ask the client to call get_context through ai-architec-rhino. Its announced name appears here after the handshake.\nPasting setup text into a chat does not register the server.",
        "ChatGPT web"=>"This button only opens the website. Localhost and a STDIO configuration alone do not connect web chat. A remote connection or Secure MCP Tunnel, account setup and supported account capabilities are required. This version does not configure a tunnel or sign into your account. Use a compatible local Claude Desktop or Codex client for this connection, or connect API profiles to chat directly in this panel.",
        _=>"1. In Claude Desktop, open Settings → Developer → Edit Config.\n2. Merge the ai-architec-rhino entry from the copied JSON into mcpServers in claude_desktop_config.json. Keep other entries.\n3. Fully quit and reopen Claude Desktop. Keep Rhino and MCP running.\n4. Enable the ai-architec-rhino tools in Claude and ask it to call get_context first.\n5. This panel shows the announced client name after the handshake. Geometry proposals appear as previews for your Apply."
    };
}
