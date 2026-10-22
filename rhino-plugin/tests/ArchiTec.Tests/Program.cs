using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ArchiTec.Core;

int passed=0;
void Test(string name,Action action){action();Console.WriteLine("PASS "+name);passed++;}
void Assert(bool value,string message="Assertion failed"){if(!value)throw new Exception(message);}
void Reject(Action action){try{action();}catch(ArgumentException){return;}throw new Exception("Invalid plan was accepted");}
var id=Guid.NewGuid().ToString();
var context=new ModelContext(1,"Meters","Default",["Default"],1,[],[],[id],false);
Test("Persian digits and explicit meter dimensions",()=> {var p=LocalPlanner.Plan("یک باکس ۱۰ × ۱۵ × ۱۲ متر بساز",context);PlanValidator.Validate(p);Assert(p.LengthUnit=="meters");Assert(p.Actions[0].Parameters.GetProperty("height").GetDouble()==12);});
Test("Follow-up preserves target GUID",()=> {var p=LocalPlanner.Plan("Move the box 2 meters along X",context);Assert(p.Actions[0].Parameters.GetProperty("object_id").GetString()==id);});
Test("Building example produces one validated semantic action",()=>{var p=LocalPlanner.Plan("Create a 15 x 20 meter building with 5 floors and 3.2 meter floor heights",context);Assert(p.Actions[0].Tool=="create_building"&&p.Actions[0].Parameters.GetProperty("floors").GetInt32()==5);});
Test("Reject fractional floor count",()=>Reject(()=>PlanValidator.Validate(new ActionPlan{Summary="bad",Actions=[ModelAction.Make("create_building",new{width=15,depth=20,floors=2.5,floor_height=3.2})]})));
Test("Reject slab taller than a floor",()=>Reject(()=>PlanValidator.Validate(new ActionPlan{Summary="bad",Actions=[ModelAction.Make("create_building",new{width=15,depth=20,floors=5,floor_height=3.2,slab_thickness=4})]})));
Test("Reject duplicate tool parameters",()=>Reject(()=>PlanValidator.Validate(Wire.Read<ActionPlan>("{\"summary\":\"bad\",\"actions\":[{\"tool\":\"create_box\",\"parameters\":{\"width\":1,\"width\":2,\"depth\":2,\"height\":3}}]}"))));
Test("Reject ambiguous targets",()=>Reject(()=>LocalPlanner.Plan("Move box 2 meters along X",context with{LastCreatedIds=[id,Guid.NewGuid().ToString()]})));
Test("Do not invent free-form offline geometry",()=>Reject(()=>LocalPlanner.Plan("Build a courtyard villa",context)));
Test("Reject script tool",()=>Reject(()=>PlanValidator.Validate(new ActionPlan{Summary="bad",Actions=[ModelAction.Make("run_python",new {code="malicious"})]})));
Test("Reject negative dimension",()=>Reject(()=>PlanValidator.Validate(new ActionPlan{Summary="bad",Actions=[ModelAction.Make("create_box",new {width=-1,depth=2,height=3})]})));
Test("Reject unknown parameters",()=>Reject(()=>PlanValidator.Validate(new ActionPlan{Summary="bad",Actions=[ModelAction.Make("create_box",new {width=1,depth=2,height=3,code="anything"})]})));
Test("Reject invalid GUID",()=>Reject(()=>PlanValidator.Validate(new ActionPlan{Summary="bad",Actions=[ModelAction.Make("move_object",new {object_id="fake",vector=new[]{1,2,3}})]})));
Test("Reject oversized coordinate",()=>Reject(()=>PlanValidator.Validate(new ActionPlan{Summary="bad",Actions=[ModelAction.Make("create_line",new {start=new[]{0d,0,0},end=new[]{1e20,0,0}})]})));
Test("Reject nonboolean cap",()=>Reject(()=>PlanValidator.Validate(new ActionPlan{Summary="bad",Actions=[ModelAction.Make("extrude_curve",new {object_id=id,height=1,cap="yes"})]})));
Test("Reject insecure remote API",()=>Reject(()=>new ProviderClient("http://example.com/v1","dummy",false)));
Test("Reject alternate OpenRouter destination",()=>Reject(()=>new ProviderClient("https://example.com/v1","dummy",true)));

var handler=new FakeHandler();
using(var provider=new ProviderClient("https://openrouter.ai/api/v1/","test-key",true,handler))
{
    var models=await provider.Models(default);
    Test("Filter unsupported models and preserve exact ID",()=>Assert(models.Models.Count==1 && models.Models[0].Id=="vendor/test-exact"));
    var request=new PlanRequest("make box","vendor/test-exact","generate",context,[]);
    var result=await provider.Plan(request,default);
    Test("Parse structured response and validate tool parameters",()=>Assert(result.Plan!.Actions[0].Parameters.GetProperty("width").GetDouble()==10));
    Test("No model fallback in provider payload",()=> {using var j=JsonDocument.Parse(handler.LastBody);Assert(j.RootElement.GetProperty("model").GetString()==request.Model);Assert(!j.RootElement.GetProperty("provider").GetProperty("allow_fallbacks").GetBoolean());});
    try{await provider.Plan(request with{Model="missing/model"},default);throw new Exception("Missing model accepted");}catch(ArgumentException){passed++;Console.WriteLine("PASS Reject unavailable model");}
    try{await provider.Plan(request with{Mode="analyze"},default);throw new Exception("Analyze accepted changes");}catch(InvalidOperationException){passed++;Console.WriteLine("PASS Analyze cannot return geometry plan");}
    handler.Fail=true;
    try{await provider.Plan(request,default);throw new Exception("HTTP error accepted");}catch(InvalidOperationException ex){Assert(ex.Message.Contains("401")&&!ex.Message.Contains("test-key"));passed++;Console.WriteLine("PASS API errors redact credentials");}
}

var calls=0;
var probe=new System.Net.Sockets.TcpListener(IPAddress.Loopback,0);probe.Start();var port=((IPEndPoint)probe.LocalEndpoint).Port;probe.Stop();
using(var server=new McpServer(port,(name,args,token)=>{calls++;return Task.FromResult<object>(new {ok=true});}))
{
    server.Start();
    using var client=new HttpClient();client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/event-stream");
    async Task<HttpResponseMessage> Post(string payload)=>await client.PostAsync(server.Endpoint,new StringContent(payload,Encoding.UTF8,"application/json"));
    var init="""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"tests","version":"1"}}}""";
    using(var r=await Post(init))Test("MCP requires token",()=>Assert(r.StatusCode==HttpStatusCode.Unauthorized));
    client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",server.Token);
    using(var r=await Post(init)){using var j=JsonDocument.Parse(await r.Content.ReadAsStringAsync());Test("MCP initialize negotiates supported version",()=>Assert(j.RootElement.GetProperty("result").GetProperty("protocolVersion").GetString()=="2025-11-25"));}
    client.DefaultRequestHeaders.Add("Origin","https://untrusted.example");
    using(var r=await Post(init))Test("MCP rejects browser origins",()=>Assert(r.StatusCode==HttpStatusCode.Forbidden));
    client.DefaultRequestHeaders.Remove("Origin");
    using(var r=await Post("""{"jsonrpc":"2.0","id":2,"method":"tools/list"}""")){using var j=JsonDocument.Parse(await r.Content.ReadAsStringAsync());Test("MCP tools contain no execution bypass",()=>Assert(j.RootElement.GetProperty("result").GetProperty("tools").GetArrayLength()==4));}
    using(var r=await Post("""{"jsonrpc":"2.0","method":"notifications/initialized"}"""))Test("MCP notifications use 202",()=>Assert(r.StatusCode==HttpStatusCode.Accepted));
    using(var r=await Post("""{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"get_context","arguments":{}}}""")){using var j=JsonDocument.Parse(await r.Content.ReadAsStringAsync());Test("MCP tool dispatch",()=>Assert(calls==1 && !j.RootElement.GetProperty("result").GetProperty("isError").GetBoolean()));}
    using(var r=await Post("""{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"execute_code","arguments":{}}}""")){using var j=JsonDocument.Parse(await r.Content.ReadAsStringAsync());Test("MCP rejects arbitrary code tools",()=>Assert(j.RootElement.GetProperty("error").GetProperty("code").GetInt32()==-32602));}
    client.DefaultRequestHeaders.Add("MCP-Protocol-Version","unsupported");
    using(var r=await Post(init))Test("MCP rejects unknown protocol headers",()=>Assert(r.StatusCode==HttpStatusCode.BadRequest));
}
passed+=await ProviderProtocolTests.Run();
passed+=await ConnectionWorkflowTests.Run();
passed+=await McpConnectionTests.Run();
passed+=await BridgeReconnectTests.Run();
Console.WriteLine($"{passed} tests passed.");
return 0;

sealed class FakeHandler:HttpMessageHandler
{
    public string LastBody="";public bool Fail;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
    {
        if(Fail)return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        if(request.Method==HttpMethod.Get)return Json("""{"data":[{"id":"vendor/test-exact","name":"Test","supported_parameters":["tools","structured_outputs"],"architecture":{"input_modalities":["text","image"],"output_modalities":["text"]}},{"id":"unsupported","supported_parameters":[]}]}""");
        LastBody=await request.Content!.ReadAsStringAsync(cancellationToken);
        var reply=new {message="Preview",plan=new {version=1,summary="Create box",length_unit="meters",actions=new[]{new {tool="create_box",parameters_json="{\"width\":10,\"depth\":15,\"height\":12}"}}}};
        return Json(JsonSerializer.Serialize(new {choices=new[]{new {finish_reason="stop",message=new {content=JsonSerializer.Serialize(reply)}}}}));
    }
    private static HttpResponseMessage Json(string body)=>new(HttpStatusCode.OK){Content=new StringContent(body,Encoding.UTF8,"application/json")};
}
