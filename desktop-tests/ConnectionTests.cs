using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Studio;

internal static class ConnectionTests
{
    internal static async Task Check()
    {
        using var http=new HttpClient(new RouterFixture());
        await RouterAccess.Verify("fixture",http);
        try {await RouterAccess.Verify("invalid",http);throw new Exception("Invalid key accepted");}
        catch(IOException ex) when(ex.Message.Contains("401")){}
        var models=await RouterAccess.Models("fixture",http);
        if(models.Length!=2 || models[0].Id!="design/new" || !models[0].Tools || models[1].Tools)throw new Exception("Catalog order or tool filter failed.");
        using var ws=new Workspace(new RouterFixture());
        foreach(bool build in new[]{false,true}){
            var answer=await ws.Chat("SketchUp",build?"custom/build":"custom/chat","fixture",new JsonArray(),build,_=>{},_=>{});
            if(answer!="OK")throw new Exception("Manual model selection failed.");
        }
        using var streaming=new Workspace(new StreamFixture());
        var text=new StringBuilder();
        var final=await streaming.Chat("Rhino","vendor/chat","fixture",new JsonArray(),false,_=>{},_=>{},partial:t=>text.Append(t));
        if(final!="سلام دنیا" || final!=text.ToString())throw new Exception("Unicode streaming failed");
        Console.WriteLine("PASS key gate, invalid key, live catalog schema, ordering, tool filter, manual Chat/Build IDs, OpenRouter-only dispatch and streaming (simulated responses).");
    }

    sealed class RouterFixture : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            if(request.RequestUri?.Host!="openrouter.ai")throw new Exception("Unexpected provider");
            if(request.Headers.Authorization?.Parameter!="fixture")return new(HttpStatusCode.Unauthorized);
            object body;
            switch(request.RequestUri.AbsolutePath){
                case "/api/v1/key": body=new {data=new {label="sample"}};break;
                case "/api/v1/models":
                    var architecture=new {output_modalities=new[]{"text"}};
                    body=new {data=new[]{new {id="chat/old",created=1,architecture,supported_parameters=Array.Empty<string>()}, new {id="design/new",created=2,architecture,supported_parameters=new[]{"tools"}}}};break;
                case "/api/v1/chat/completions":
                    var payload=JsonNode.Parse(await request.Content!.ReadAsStringAsync(ct))!;
                    bool build=payload["tools"] is JsonArray;
                    if((string?)payload["model"]!=(build?"custom/build":"custom/chat"))throw new Exception("Wrong model");
                    if(build && (bool?)payload["provider"]?["require_parameters"]!=true)throw new Exception("Build routing missing");
                    body=new {choices=new[]{new {message=new {role="assistant",content="OK"}}}};break;
                default:throw new Exception("Unexpected endpoint");
            }
            return new(HttpStatusCode.OK){Content=new StringContent(Wire.Node(body).ToJsonString(),Encoding.UTF8,"application/json")};
        }
    }
}
