using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Studio;

internal static class Wire
{
    public static JsonNode Node(object value) => JsonSerializer.SerializeToNode(value)!;
    public static JsonNode Text(string value, bool error = false) => Node(new { content = new[] { new { type = "text", text = value } }, isError = error });
    public static string Describe(JsonNode result) => string.Join("\n", result["content"]!.AsArray().Where(i => (string?)i?["type"] == "text").Select(i => (string?)i!["text"]));
}

internal sealed class Connections : IDisposable
{
    private ArchicadHost archicad=new();
    private static bool RhinoFamily(string h) => h.Split(" · ")[0] is "Rhino" or "Grasshopper";
    public static string Route(string h) => RhinoFamily(h)?"Rhino bridge":h is "Revit" or "Both"?"Studio bridge":SceneHost.Supports(h)?"Scene connector":h.Split(" · ")[0]=="Archicad"?"Archicad JSON":WindowsAutomation.Supports(h)?"Windows automation":"Connector required";
    public async Task<JsonArray> Tools(string host)
    {
        if(Route(host)=="Scene connector")return SceneHost.Tools(host);
        if(Route(host)=="Archicad JSON")return ArchicadHost.Tools();
        if(Route(host)=="Windows automation")return WindowsAutomation.Tools(host);
        if (RhinoFamily(host)) return (await RhinoNative.Request(host,"catalog",new JsonObject())).AsArray();
        if(host!="Revit")throw new IOException("No native connector is available for this edition.");
        var tools = new JsonArray();
        void Add(string name, string description, params string[] fields)
        {
            var props = new JsonObject();
            foreach (var key in fields) props[key] = Wire.Node(new { type = key.EndsWith("_m") ? "number" : "string" });
            tools.Add(Wire.Node(new { name, description, inputSchema = new { type = "object", properties = props, required = fields, additionalProperties = false } }));
        }
        Add("get_context","Read active project, levels, view and selection.");
        Add("create_model_line","Create a native horizontal model line on a level. Coordinates in metres.","document_id","request_id","level_id","x1_m","y1_m","x2_m","y2_m");
        Add("save_project","Save the active named Revit document in place when requested.","document_id","request_id");
        Add("inspect_wall","Inspect height and axis endpoints in metres.","document_id","wall_id");
        Add("create_wall","Create a straight native wall. Supply a fresh UUID request_id.","document_id","request_id","level_id","x1_m","y1_m","x2_m","y2_m","height_m");
        Add("set_wall_height","Edit the same unconnected wall. Supply a fresh UUID request_id.","document_id","request_id","wall_id","height_m");
        return tools;
    }

    public async Task<JsonNode> Call(string host, string name, JsonNode args)
    {
        if(Route(host)=="Scene connector")return await SceneHost.Call(host,name,args);
        if(Route(host)=="Archicad JSON")return await archicad.Call(host,name,args);
        if(Route(host)=="Windows automation")return WindowsAutomation.Call(host,name,args);
        if(RhinoFamily(host))return await RhinoNative.Request(host,name,args);
        if(host!="Revit")throw new IOException("No native connector is available for this edition.");
        var processes = Process.GetProcessesByName("Revit");
        try
        {
            var sessions=processes.Where(p=>{
                try{return Path.GetFileName(Path.GetDirectoryName(p.MainModule?.FileName))?.Equals("Revit 2025",StringComparison.OrdinalIgnoreCase)==true;}
                catch(Exception ex) when(ex is System.ComponentModel.Win32Exception or InvalidOperationException){return false;}
            }).ToArray();
            if (sessions.Length != 1) throw new IOException("Open one Revit 2025 session with the Studio connector and a project.");
            using var pipe = new NamedPipeClientStream(".", "AIArchiTec.Revit." + sessions[0].Id, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(2500);
            using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, true) { AutoFlush = true };
            using var reader = new StreamReader(pipe, Encoding.UTF8, false, 1024, true);
            await writer.WriteLineAsync(JsonSerializer.Serialize(new { name, arguments = args }));
            var reply = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(45)) ?? throw new IOException("Revit did not return a receipt.");
            return Wire.Text(reply, JsonNode.Parse(reply)?["error"] != null);
        }
        finally { foreach (var p in processes) p.Dispose(); }
    }

    public void Dispose()
    {
archicad.Dispose();archicad=new();
    }
}
