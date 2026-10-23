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
            Prune();
            if(clients.Count>=16)clients.Remove(clients.OrderBy(p=>p.Value.Seen).First().Key);
            var id=Guid.NewGuid().ToString("N");clients[id]=new(name,DateTimeOffset.UtcNow);Add("initialized",name);return id;
        }
    }
    public bool Contains(string? id){lock(gate){Prune();return id is not null&&clients.ContainsKey(id);}}
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
    private void Prune(){foreach(var stale in clients.Where(c=>DateTimeOffset.UtcNow-c.Value.Seen>TimeSpan.FromMinutes(2)).Select(c=>c.Key).ToArray())clients.Remove(stale);}
    public McpConnectionSnapshot Snapshot()
    {
        lock(gate)
        {
            Prune();
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
        if(client=="Codex")return "[mcp_servers.ai-architec-rhino]\ncommand = "+JsonSerializer.Serialize(bridge)+"\nstartup_timeout_sec = 20\ntool_timeout_sec = 150\n\n[mcp_servers.ai-architec-rhino.env]\nARCHITEC_MCP_PORT = \""+port+"\"\n";
        return JsonSerializer.Serialize(new{mcpServers=new Dictionary<string,object>{["ai-architec-rhino"]=new{command=bridge,env=new Dictionary<string,string>{["ARCHITEC_MCP_PORT"]=port.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}},new JsonSerializerOptions{WriteIndented=true});
    }
    public static string Guide(string client)=>client switch
    {
        "Codex"=>"1. Click Connect to Codex: enable the local connection and save its MCP entry automatically.\n2. If Codex is already open, refresh once in Settings > Plugins > MCPs. Allow the Archi-Tec tools in the client.\n3. Chat in Codex. Authorize its modeling tools there; no local Apply click is needed. Future Rhino MCP restarts reconnect automatically.\nNo manual copy is needed when automatic setup succeeds.",
        "ChatGPT web"=>"A browser chat requires separate connector setup or Secure MCP Tunnel. This release does not configure a web connector or sign into your account. Use Codex or Claude Desktop here, or API mode for direct chat in Rhino.",
        "Other MCP client"=>"Enable connection, then expand Manual setup. Add the copied JSON configuration to your desktop client's MCP settings once. Chat in that client and allow Archi-Tec tools. The native bridge reconnects after Rhino MCP restarts.",
        _=>"1. Click Connect to Claude Desktop to save the MCP entry for this Windows user. Other server entries are preserved.\n2. Fully quit and reopen Claude once after first-time setup. Allow the Archi-Tec tools.\n3. Chat in Claude. Authorize its modeling tools there; no local Apply click is needed. Future Rhino MCP restarts reconnect automatically."

    };
}
