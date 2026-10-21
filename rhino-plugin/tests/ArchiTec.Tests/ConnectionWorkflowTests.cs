using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using ArchiTec.Core;

internal static class ConnectionWorkflowTests
{
    public static async Task<int> Run()
    {
        var passed=0;void Check(bool ok,string name){if(!ok)throw new Exception(name);passed++;Console.WriteLine("PASS "+name);}
        var id=Guid.NewGuid().ToString();var other=Guid.NewGuid().ToString();
        var objects=new[]{new ObjectInfo(id,"Brep","AI",[0,0,0],[15,20,16],Role:"glazing",Floor:5),new ObjectInfo(other,"Brep","AI",[0,0,0],[15,20,3.2],Role:"column",Floor:1)};
        var context=new ModelContext(1,"Meters","Default",["Default","AI"],2,objects,[],[id,other],false);
        var move=LocalPlanner.Plan("Move the fifth floor of the building 2 meters back",context);
        Check(move.Actions.Count==1&&move.Actions[0].Parameters.GetProperty("object_id").GetString()==id&&move.Actions[0].Parameters.GetProperty("vector")[1].GetDouble()==2,"Floor follow-up targets the existing fifth floor, not a replacement building");
        var persian=LocalPlanner.Plan("طبقه‌ی پنجم ساختمان را ۲ متر عقب جابه‌جا کن",context);
        Check(persian.Actions.Count==1&&persian.Actions[0].Parameters.GetProperty("object_id").GetString()==id,"Persian floor follow-up preserves exact target GUID");
        try{LocalPlanner.Plan("Move floor eleven 2 meters along Y",context);throw new Exception("Ambiguous floor accepted");}catch(ArgumentException){Check(true,"Unrecognized floor cannot silently move the whole building");}
        Check(ConversationIntent.IsExplicitEdit("Move it 2 meters along X")&&ConversationIntent.IsExplicitEdit("همین مدل را تغییر بده")&&!ConversationIntent.IsExplicitEdit("Create a new building"),"Conversation distinguishes a follow-up edit from a new concept");
        var documents=new DocumentWorkspaces();documents.For(1).RecentIds.Add(Guid.Parse(id));documents.For(1).Sessions["assist"].Prompt="edit A";
        Check(documents.For(2).RecentIds.Count==0&&documents.For(2).Sessions["assist"].Prompt==""&&documents.For(1).Sessions["assist"].Prompt=="edit A","Document histories and recent target IDs stay isolated");
        var receipt=ExecutionFeedback.Describe(2,2,0,objects,"Meters",true);
        Check(receipt.Contains("15 × 20 × 16")&&receipt.Contains("مرحلهٔ 2")&&!receipt.Contains("thermal"),"Result feedback uses measured bounds and the user's language");
        try{ProviderProfiles.ValidateCredentialSource("OpenRouter","sk-proj-fixture");throw new Exception("Wrong key destination accepted");}catch(ArgumentException){Check(true,"Recognized OpenAI project keys are not sent to OpenRouter");}
        Check(ConnectionDiagnostics.Describe(new HttpRequestException("fixture-secret",new SocketException(10013))).Contains("10013")&&!ConnectionDiagnostics.Describe(new HttpRequestException("fixture-secret",new SocketException(10013))).Contains("fixture-secret"),"Socket-access errors distinguish network policy from key validity without leaking secrets");
        var probe=new FixtureHandler();var probeResult=await ConnectionDiagnostics.Probe("OpenAI","https://api.openai.com/v1/",default,probe);
        Check(!probe.HasAuthorization&&probeResult.Contains("HTTP 401")&&probeResult.Contains("not key validity"),"Network probe sends no key and treats HTTP 401 as host reachability");
        var handler=new FixtureHandler{Authorized=true};
        using var client=ProviderProfiles.Create("OpenAI","https://api.openai.com/v1/","fixture-key",handler);
        var catalog=await client.Models(default);
        Check(catalog.Models.Count==1&&catalog.Models[0].Id=="gpt-fixture-exact","OpenAI discovery excludes image/audio/embedding-only catalog entries");
        var request=new PlanRequest("explain","gpt-fixture-exact","analyze",context,[new("assistant","verified result")]);
        var response=await client.Plan(request,default);using var body=JsonDocument.Parse(handler.Body);
        Check(handler.LastPath.EndsWith("/responses")&&!body.RootElement.GetProperty("store").GetBoolean()&&body.RootElement.GetProperty("model").GetString()==request.Model,"OpenAI uses stateless Responses with the exact selected model");
        Check(body.RootElement.GetProperty("text").GetProperty("format").GetProperty("type").GetString()=="json_schema"&&response.Plan is null&&response.Message=="Read-only explanation","OpenAI structured response is parsed and validated");
        handler.Incomplete=true;
        try{await client.Plan(request,default);throw new Exception("Incomplete result accepted");}catch(InvalidOperationException){Check(true,"Incomplete OpenAI output cannot apply geometry");}
        return passed;
    }
    private sealed class FixtureHandler:HttpMessageHandler
    {
        public bool HasAuthorization,Authorized,Incomplete;public string Body="",LastPath="";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            HasAuthorization=request.Headers.Authorization is not null;LastPath=request.RequestUri!.AbsolutePath;
            if(!Authorized)return new(HttpStatusCode.Unauthorized);
            if(request.Method==HttpMethod.Get)return Json("""{"data":[{"id":"gpt-fixture-exact"},{"id":"gpt-image-fixture"},{"id":"text-embedding-fixture"},{"id":"gpt-realtime-fixture"}]}""");
            Body=await request.Content!.ReadAsStringAsync(cancellationToken);
            var text=JsonSerializer.Serialize(new{message="Read-only explanation",plan=(object?)null});
            return Json(JsonSerializer.Serialize(new{status=Incomplete?"incomplete":"completed",output=new[]{new{type="message",content=new[]{new{type="output_text",text}}}}}));
        }
        private static HttpResponseMessage Json(string text)=>new(HttpStatusCode.OK){Content=new StringContent(text,Encoding.UTF8,"application/json")};
    }
}
