using System.Net;
using System.Text;
using System.Text.Json;
using ArchiTec.Core;

internal static class ProviderProtocolTests
{
    public static async Task<int> Run()
    {
        var count=0;
        void Check(string name,bool condition){if(!condition)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
        var id=Guid.NewGuid().ToString();var info=new ObjectInfo(id,"Brep","Default",[0,0,0],[1,1,1]);
        var context=new ModelContext(1,"Meters","Default",["Default"],1,[info],[id],[id],false);
        foreach(var source in new[]{"Google Gemini","Anthropic / Claude","xAI / Grok"})
        {
            using var handler=new NativeFixture(source);var preset=ProviderProfiles.Get(source);
            using var client=ProviderProfiles.Create(source,preset.Endpoint,"fixture-key",handler);
            var catalog=await client.Models(default);var model=catalog.Models.Single();
            Check(source+" filters unsupported models and discovers vision",model.Vision);
            Check(source+" uses the exact catalog ID",model.Id==handler.ModelId);
            var result=await client.Plan(new PlanRequest("box",model.Id,"generate",context,[],"data:image/png;base64,AQID"),default);
            Check(source+" parses native structured output",result.Plan?.Actions[0].Tool=="create_box");
            Check(source+" keeps API keys out of URLs",!handler.Paths.Any(p=>p.Contains("fixture-key")));
            using(var payload=JsonDocument.Parse(handler.LastBody))
            {
                var root=payload.RootElement;
                if(source=="Google Gemini")
                {
                    Check("Gemini native authentication and GenerateContent path",handler.LastHeaders.Contains("x-goog-api-key")&&!handler.LastHeaders.Contains("Authorization")&&handler.Paths.Last().EndsWith("models/gemini-fixture:generateContent"));
                    Check("Gemini native image and structured generation config",root.GetProperty("contents")[0].GetProperty("parts")[1].GetProperty("inlineData").GetProperty("data").GetString()=="AQID"&&root.GetProperty("generationConfig").GetProperty("responseFormat").GetProperty("text").GetProperty("schema").GetProperty("type").GetString()=="object");
                    Check("Gemini paginates the model catalog",handler.Paths.Any(p=>p.Contains("pageToken=next")));
                }
                else if(source=="Anthropic / Claude")
                {
                    Check("Anthropic key and version headers",handler.LastHeaders.Contains("x-api-key")&&handler.LastHeaders.Contains("anthropic-version")&&!handler.LastHeaders.Contains("Authorization"));
                    Check("Anthropic native image and output_config",root.GetProperty("messages")[0].GetProperty("content")[1].GetProperty("source").GetProperty("media_type").GetString()=="image/png"&&root.GetProperty("output_config").GetProperty("format").GetProperty("type").GetString()=="json_schema");
                    Check("Anthropic paginates the model catalog",handler.Paths.Any(p=>p.Contains("after_id=next")));
                }
                else Check("xAI language catalog and JSON schema protocol",handler.Paths.Any(p=>p.EndsWith("language-models"))&&root.GetProperty("response_format").GetProperty("type").GetString()=="json_schema"&&handler.LastHeaders.Contains("Authorization"));
            }
            handler.Incomplete=true;
            try{await client.Plan(new PlanRequest("box",model.Id,"generate",context,[]),default);throw new Exception("Truncation accepted");}catch(InvalidOperationException){Check(source+" rejects incomplete output",true);}
            handler.Incomplete=false;handler.Unauthorized=true;
            try{await client.Plan(new PlanRequest("box",model.Id,"generate",context,[]),default);throw new Exception("Unauthorized accepted");}catch(InvalidOperationException ex){Check(source+" redacts API errors",ex.Message.Contains("401")&&!ex.Message.Contains("fixture-key"));}
        }
        foreach(var name in new[]{"Google Gemini","Anthropic / Claude","xAI / Grok"})
        {try{using var c=ProviderProfiles.Create(name,"https://other.example/v1/","fixture");throw new Exception("Alternate host accepted");}catch(ArgumentException){Check(name+" rejects wrong credential destination",true);}}
        var sessions=new WorkspaceSessions();sessions["generate"].Prompt="generation draft";sessions["generate"].History.Add(new("user","generate-only"));sessions["generate"].ImageData="image-reference";
        Check("Workspace drafts, images and histories are isolated",sessions["analyze"].Prompt==""&&sessions["assist"].History.Count==0&&sessions["analyze"].ImageData is null);
        sessions["assist"].Prompt="edit";Check("Workspace switching preserves draft",sessions["generate"].Prompt=="generation draft");
        var move=new ActionPlan{Summary="move",Actions=[ModelAction.Make("move_object",new{object_id=id,vector=new[]{1,0,0}})]};
        var create=new ActionPlan{Summary="create",Actions=[ModelAction.Make("create_box",new{width=1,depth=2,height=3})]};
        foreach(var sample in new[]{("analyze",move,context),("generate",move,context),("assist",create,context),("assist",move,context with{Objects=[]}),("assist",move,context with{Objects=[info with{Id=Guid.NewGuid().ToString()}]})})
        {try{WorkspacePolicy.Validate(sample.Item1,sample.Item2,sample.Item3);throw new Exception("Policy accepted invalid operation");}catch(InvalidOperationException){Check("Workspace rejects "+sample.Item1+" action outside its purpose/scope",true);}}
        WorkspacePolicy.Validate("assist",move,context);Check("Model Assist accepts scoped edit",true);
        WorkspacePolicy.Validate("generate",create,context);Check("Generate accepts creation",true);
        return count;
    }
    private sealed class NativeFixture(string source):HttpMessageHandler
    {
        public string ModelId=>source=="Google Gemini"?"models/gemini-fixture":source=="Anthropic / Claude"?"claude-fixture":"grok-fixture";
        public List<string> Paths=[];public string LastBody="";public HashSet<string> LastHeaders=[];public bool Incomplete,Unauthorized;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            Paths.Add(request.RequestUri!.ToString());LastHeaders=request.Headers.Select(h=>h.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if(Unauthorized)return new(HttpStatusCode.Unauthorized);
            if(request.Method==HttpMethod.Get)
            {
                if(source=="Google Gemini")return Json(Paths.Last().Contains("pageToken=")?new{models=Array.Empty<object>()}:new{models=new object[]{new{name=ModelId,displayName="Gemini",supportedGenerationMethods=new[]{"generateContent"}},new{name="models/gemini-image",supportedGenerationMethods=new[]{"generateContent"}}},nextPageToken="next"});
                if(source=="Anthropic / Claude")return Json(Paths.Last().Contains("after_id=")?new{data=Array.Empty<object>(),has_more=false}:new{data=new object[]{new{id=ModelId,display_name="Claude",capabilities=new{structured_outputs=new{supported=true},image_input=new{supported=true}}},new{id="unsupported",capabilities=new{structured_outputs=new{supported=false}}}},has_more=true,last_id="next"});
                return Json(new{models=new object[]{new{id=ModelId,input_modalities=new[]{"text","image"},output_modalities=new[]{"text"}},new{id="image-only",input_modalities=new[]{"text"},output_modalities=new[]{"image"}}}});
            }
            LastBody=await request.Content!.ReadAsStringAsync(token);
            var reply=JsonSerializer.Serialize(new{message="Preview",plan=new{version=1,summary="Box",length_unit="meters",actions=new[]{new{tool="create_box",parameters_json="{\"width\":1,\"depth\":2,\"height\":3}"}}}});
            if(source=="Google Gemini")return Json(new{candidates=new[]{new{finishReason=Incomplete?"MAX_TOKENS":"STOP",content=new{parts=new object[]{new{thought=true,text="not output"},new{text=reply}}}}}});
            if(source=="Anthropic / Claude")return Json(new{model=ModelId,stop_reason=Incomplete?"max_tokens":"end_turn",content=new[]{new{type="text",text=reply}}});
            return Json(new{choices=new[]{new{finish_reason=Incomplete?"length":"stop",message=new{content=reply}}}});
        }
        private static HttpResponseMessage Json(object body)=>new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(body),Encoding.UTF8,"application/json")};
    }
}
