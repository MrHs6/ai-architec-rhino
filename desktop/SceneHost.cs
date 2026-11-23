using System.Diagnostics;
using System.IO;
using System.Text.Json.Nodes;

namespace Studio;
internal static class SceneHost
{
    internal static bool Supports(string host)=>host.Split(" · ")[0] is "Blender" or "SketchUp" or "3ds Max" or "FreeCAD" or "Maya";
    internal static JsonArray Tools(string host)
    {
        var point=new{type="array",minItems=3,maxItems=3,items=new{type="number"}};
        var tools=new JsonArray(Wire.Node(new{name="get_context",description="Read scene, units, session and selection.",inputSchema=new{type="object",properties=new{},additionalProperties=false}}),
            Wire.Node(new{name="create_mesh",description="Create one editable mesh from indexed triangles in host units. Supply a stable UUID for retries.",inputSchema=new{type="object",properties=new{document_id=new{type="string"},request_id=new{type="string"},name=new{type="string"},vertices=new{type="array",items=point},faces=new{type="array",items=new{type="array",minItems=3,maxItems=3,items=new{type="integer"}}}},required=new[]{"document_id","request_id","name","vertices","faces"}}}));
        if(host.Split(" · ")[0] is "3ds Max" or "SketchUp"){
            foreach(var action in new[]{"save_copy","export_obj","render_png"}){
                tools.Add(Wire.Node(new{name=action,description=action=="render_png"?"Write a PNG of the active view (3ds Max renders; SketchUp captures viewport).":"Write a new project copy or OBJ file. Returns its absolute path.",inputSchema=new{type="object",properties=new{document_id=new{type="string"},request_id=new{type="string",format="uuid"}},required=new[]{"document_id","request_id"},additionalProperties=false}}));
            }
        }
        return tools;
    }
    internal static async Task<JsonNode> Call(string target,string command,JsonNode args)
    {
        if(!Tools(target).Any(tool=>(string?)tool!["name"]==command))throw new IOException("Unknown scene tool.");
        var parts=target.Split(" · ");
        var root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AIArchiTec","Sessions",parts[0]);
        var dirs=Directory.Exists(root)?Directory.GetDirectories(root):[];
        var live=new List<string>();
        foreach(var dir in dirs){
            try{
                var meta=JsonNode.Parse(File.ReadAllText(Path.Combine(dir,"host.json"))) as JsonObject;
                if(meta?["version"] is not JsonValue versionValue || meta["pid"] is not JsonValue pidValue || !pidValue.TryGetValue<int>(out int pid))continue;
                var version=versionValue.ToString();
                if(parts.Length>1 && version.Split('.')[0]!=parts[1].Replace(".x",""))continue;
                using var process=Process.GetProcessById(pid);
                if(!process.HasExited && (DateTime.UtcNow-File.GetLastWriteTimeUtc(Path.Combine(dir,"heartbeat"))).TotalSeconds<12)live.Add(dir);
            }catch(Exception e) when(e is IOException or ArgumentException or InvalidOperationException or System.Text.Json.JsonException or FormatException){}
        }
        if(live.Count!=1)throw new IOException("Load the bundled Studio connector in one matching application session. See the connector folder.");
        var path=Path.Combine(live[0],Guid.NewGuid().ToString("N"));
        var request=Wire.Node(new{command,args,expires=DateTimeOffset.UtcNow.AddSeconds(20).ToUnixTimeSeconds()}).ToJsonString();
        if(request.Length>2_000_000)throw new IOException("Mesh request exceeds 2 MB.");
        await File.WriteAllTextAsync(path+".tmp",request);
        File.Move(path+".tmp",path+".req");
        try{
            for(int i=0;i<(command=="render_png"?1500:120);i++){
                if(File.Exists(path+".out")){
                    var result=JsonNode.Parse(await File.ReadAllTextAsync(path+".out"))!;
                    return Wire.Text(result.ToJsonString(),result["error"]!=null);
                }
                await Task.Delay(200);
            }
            throw new IOException("Host timed out. Inspect the scene before retrying.");
        }finally{File.Delete(path+".req");File.Delete(path+".out");}
    }
}
