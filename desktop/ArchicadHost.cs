using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.IO;
namespace Studio;

internal sealed class ArchicadHost : IDisposable
{
    readonly HttpClient http=new(new HttpClientHandler{UseProxy=false,AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(8)};
    readonly Dictionary<string,(int Port,string Token)> sessions=new();
    static readonly string[] Reads=["GetProductInfo","GetSelectedElements","GetElementsByType","GetPropertyValuesOfElements","GetAllPropertyIds"];
    internal static JsonArray Tools()
    {
        var list=new JsonArray(Wire.Node(new{name="get_context",description="Find a running Archicad, read version and selected element IDs.",inputSchema=new{type="object",properties=new{},additionalProperties=false}}));
        foreach(var cmd in Reads.Append("SetPropertyValuesOfElements"))
            list.Add(Wire.Node(new{name=cmd,description="Archicad JSON API "+cmd+". Use official command parameters. For writes use previously inspected element GUIDs.",inputSchema=new{type="object",properties=new{session=new{type="string"},parameters=new{type="object"}},required=new[]{"session","parameters"}}}));
        return list;
    }
    async Task<JsonNode> Post(int port,string cmd,JsonNode args)
    {
        var body=Wire.Node(new{command="API."+cmd,parameters=args});
        using var data=new StringContent(body.ToJsonString(),Encoding.UTF8,"application/json");
        using var reply=await http.PostAsync("http://127.0.0.1:"+port,data);reply.EnsureSuccessStatusCode();
        var json=JsonNode.Parse(await reply.Content.ReadAsStringAsync())!;
        return json["result"]??throw new IOException(json["error"]?.ToJsonString()??"No Archicad result.");
    }
    public async Task<JsonNode> Call(string target,string cmd,JsonNode args)
    {
        if(cmd=="get_context"){
            var matches=await Task.WhenAll(Enumerable.Range(19723,21).Select(async port=>{
                try{var info=await Post(port,"GetProductInfo",new JsonObject());return (port,info);}
                catch(HttpRequestException){return (port,info:(JsonNode?)null);}
                catch(Exception){return (port,info:(JsonNode?)null);}
            }));
            var major=target.Split(" · ").Last();
            var found=matches.Where(x=>x.info!=null && (target=="Archicad"||x.info["version"]?.ToString()==major)).ToArray();
            if(found.Length!=1)throw new IOException("Open one matching Archicad project with its JSON interface available.");
            var hit=found[0];
            var session=sessions.TryGetValue(target,out var saved)&&saved.Port==hit.port?saved:(hit.port,Guid.NewGuid().ToString());
            sessions[target]=session;
            return Wire.Text(Wire.Node(new{session=session.Item2,product=hit.info,selection=await Post(hit.port,"GetSelectedElements",new JsonObject())}).ToJsonString());
        }
        if(!Reads.Contains(cmd)&&cmd!="SetPropertyValuesOfElements")throw new IOException("Unknown Archicad command.");
        if(!sessions.TryGetValue(target,out var current)||current.Token!=(string?)args["session"])throw new IOException("Inspect this Archicad session first.");
        return Wire.Text((await Post(current.Port,cmd,args["parameters"]??new JsonObject())).ToJsonString());
    }
    public void Dispose()=>http.Dispose();
}
