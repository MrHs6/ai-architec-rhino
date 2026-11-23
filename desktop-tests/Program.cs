using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Studio;

if(args.SequenceEqual(new[]{"--connections"})){await ConnectionTests.Check();return;}
if(args.Length==2 && args[0]=="--onboarding"){await Onboarding.Check(args[1]);return;}
if(args.Length==2 && args[0]=="--layout"){WorkspaceLayout.Verify(args[1]);return;}
if(args.SequenceEqual(new[]{"--platform"})){PlatformNavigation.Verify();return;}
// Exercise the exact app loop with a deterministic provider and the live Revit connector.
using var ws = new Workspace(new ProviderFixture());
var history = new JsonArray(Wire.Node(new { role="system",content=Workspace.SystemPrompt }),
    Wire.Node(new { role="user",content="Inspect the current project" }));
var receipts=new List<string>();
var events=new List<string>();
var reply=await ws.Chat("Revit","fixture","local-test",history,true,receipts.Add,_=>{},(name,state)=>events.Add(state+" "+name));
if(reply!="Project inspected"||receipts.Count!=1||!receipts[0].Contains("document_id"))throw new Exception("Live receipt missing");
history.Add(Wire.Node(new {role="user",content="Explain the result"}));
reply=await ws.Chat("Revit","fixture","local-test",history,false,receipts.Add,_=>{});
if(reply!="Context retained"||receipts.Count!=1)throw new Exception("Chat mode produced an unexpected result");
if(!events.Contains("done revit get context"))throw new Exception("Missing completion event");
using(var streaming=new Workspace(new StreamFixture())){
    var streamed=new StringBuilder();
    var answer=await streaming.Chat("Rhino","test","key",new JsonArray(),false,_=>{},_=>{},partial:t=>streamed.Append(t));
    if(answer!="سلام دنیا" || answer!=streamed.ToString())throw new Exception("Streaming chunks did not match the final reply.");
}
Console.WriteLine("PASS streamed chat (UTF-8 fixture).");
Console.WriteLine("PASS provider routing and live operation events (fixture responses).");
Console.WriteLine("PASS API orchestration: model tool request, live Revit read, receipt, follow-up, chat-only isolation (mock provider).");

internal sealed class ProviderFixture : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req,CancellationToken ct)
    {
        var data=JsonNode.Parse(await req.Content!.ReadAsStringAsync(ct))!;
        var messages=data["messages"]!.AsArray();
        bool hasReceipt=messages.Any(m=>(string?)m!["role"]=="tool" && ((string?)m["content"])!.Contains("document_id"));
        object answer;
        if(data["tools"]==null && hasReceipt)answer=new {role="assistant",content="Context retained"};
        else if(hasReceipt)answer=new {role="assistant",content="Project inspected"};
        else if(data["tools"]!.AsArray().Any(t=>(string?)t!["function"]!["name"]=="revit_get_context"))
            answer=new {role="assistant", tool_calls=new[]{new {id="read1",type="function",function=new {name="revit_get_context",arguments="{}"}}}};
        else throw new InvalidOperationException("Tool discovery failed");
        return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(Wire.Node(new {choices=new[]{new{message=answer}}}).ToJsonString(),Encoding.UTF8,"application/json")};
    }
}
internal sealed class StreamFixture : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
    {
        if(request.RequestUri?.Host!="openrouter.ai")throw new Exception("Wrong OpenRouter destination");
        var data="data: {\"choices\":[{\"delta\":{\"content\":\"سلام \"}}]}\n\ndata: {\"choices\":[{\"delta\":{\"content\":\"دنیا\"},\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n";
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(data,Encoding.UTF8,"text/event-stream")});
    }
}
