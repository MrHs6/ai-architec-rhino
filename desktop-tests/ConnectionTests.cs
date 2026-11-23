using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Studio;

internal static class ConnectionTests
{
    internal static void FixtureCodex(string[] args)
    {
        var stdin=Console.In.ReadToEnd();
        string Param(string flag)=>args[Array.IndexOf(args,flag)+1];
        if(!args.Contains("--ignore-user-config") || Param("--sandbox")!="read-only" || !File.Exists(Param("--image"))
            || !stdin.Contains("Image attached:") || stdin.Contains("base64") || !stdin.Contains("height"))
            throw new Exception("Wrong Codex invocation or image handling");
        var schema=JsonNode.Parse(File.ReadAllText(Param("--output-schema")))!;
        if(!schema["properties"]!["tool"]!["enum"]!.AsArray().Any(x=>x!.ToString()=="height"))
            throw new Exception("Missing tool constraint");
        File.WriteAllText(Param("--output-last-message"),"{\"tool\":\"height\",\"arguments\":\"{}\",\"text\":\"\"}");
    }

    internal static void FixtureHost()
    {
        Console.InputEncoding=new UTF8Encoding(false);
        Console.OutputEncoding=new UTF8Encoding(false);
        int height=0;
        while(Console.ReadLine() is string line)
        {
            var message=JsonNode.Parse(line)!;
            if(message["id"]==null) continue;
            var args=message["params"]!;
            object result=message["method"]!.ToString() switch{
                "initialize"=>new{protocolVersion="2025-03-26",capabilities=new{tools=new{}},serverInfo=new{name="Fixture",version="1"}},
                "tools/list"=>new{tools=new[]{new{name="height",description="Read or change the example height",inputSchema=new{type="object",properties=new{value=new{type="integer"}}}}}},
                "tools/call" when args["name"]!.ToString()=="height"=>Wire.Text("ارتفاع "+(height=(int?)args["arguments"]?["value"]??height)),
                _=>throw new Exception("Unexpected method")
            };
            Console.WriteLine(JsonSerializer.Serialize(new{jsonrpc="2.0",id=message["id"],result}));
        }
    }

    internal static async Task Check()
    {
        string folder=Directory.CreateTempSubdirectory("studio-resolver-").FullName;
        void Session(int port,int pid)=>File.WriteAllText(Path.Combine(folder,$"mcp-session-{port}.json"),JsonSerializer.Serialize(new{Endpoint=$"http://127.0.0.1:{port}/mcp",ProcessId=pid}));
        try{
            Session(5190,9);Session(5192,8);Session(5188,1);
            int? Major(int pid)=>pid is 8 or 9?pid:null;
            if(RhinoSessions.Resolve("Rhino · 9",folder,Major)?.Port!=5190 || RhinoSessions.Resolve("Grasshopper · 8",folder,Major)?.Port!=5192)
                throw new Exception("Port or edition mismatch");
            try{RhinoSessions.Resolve("Rhino",folder,Major);throw new Exception("Ambiguous target accepted");}catch(IOException){}
            File.Delete(Path.Combine(folder,"mcp-session-5192.json"));
            if(RhinoSessions.Resolve("Rhino",folder,Major)?.Port!=5190)throw new Exception("Stale port retained");
            Console.WriteLine("PASS alternate ports, session expiry, automatic and explicit Rhino/Grasshopper editions");
        }finally{foreach(var file in Directory.GetFiles(folder))File.Delete(file);Directory.Delete(folder);}

        var profile=new HostProfile("Fixture",Environment.ProcessPath!,["--fixture-host"]);
        using(var server=new StdioHost(profile)){
            var replies=await Task.WhenAll(Enumerable.Range(0,8).Select(_=>server.Tools()));
            if(replies.Any(page=>page.Count!=1))throw new Exception("Concurrent MCP calls lost replies.");
            Console.WriteLine("PASS simultaneous MCP discovery requests");
        }
        using var ws=new Workspace(new ModelFixture(),new Connections(()=>[profile]));
        var messages=new JsonArray(Wire.Node(new{role="user",content=new object[]{
            new{type="text",text="ارتفاع را تنظیم کن"},new{type="image_url",image_url=new{url="data:image/png;base64,AA=="}}}}));
        var descriptor=Wire.Node(new{function=new{name="height"}});
        var ai=await CodexModel.Reply(messages,new JsonArray(descriptor),CancellationToken.None,Environment.ProcessPath);
        if(ai["tool_calls"]?[0]?["function"]?["name"]?.ToString()!="height" || messages[0]!["content"]![1]!["type"]!.ToString()!="image_url")throw new Exception("Codex response or original reference damaged.");
        Console.WriteLine("PASS simulated Codex subprocess: isolated arguments, photo handoff, schema-constrained tool selection");
        var receipts=new List<string>();
        var answer=await ws.Chat("Fixture","OpenRouter","fixture","test",messages,true,receipts.Add,_=>{});
        if(answer!="done" || receipts.Count!=1 || !receipts[0].Contains("ارتفاع 4"))throw new Exception("Missing first edit");
        messages.Add(Wire.Node(new{role="user",content="Read the same object"}));
        answer=await ws.Chat("Fixture","OpenRouter","fixture","test",messages,true,receipts.Add,_=>{});
        if(answer!="done" || receipts.Count!=2 || !receipts[1].Contains("ارتفاع 4"))throw new Exception("MCP child or document state lost");
        foreach(var service in new[]{"OpenAI","Gemini","Anthropic","Compatible API","OpenRouter"}){
            var probe=new JsonArray(Wire.Node(new{role="user",content="OK?"}));
            if(await ws.Chat("Fixture",service,"fixture","test",probe,false,_=>{},_=>{},customEndpoint:"http://localhost:10000/v1/chat/completions",probe:true)!="OK")
                throw new Exception("Provider probe failed: "+service);
        }
        using var streaming=new Workspace(new StreamFixture());
        var chunks=new StringBuilder();
        var streamed=await streaming.Chat("Fixture","Gemini","fixture","test",new JsonArray(),false,_=>{},_=>{},partial:t=>chunks.Append(t));
        if(streamed!="سلام دنیا" || chunks.ToString()!=streamed)throw new Exception("Unicode stream failed");
        Console.WriteLine("PASS API fixtures, image and Persian text payload, streamed Unicode, edit then inspect via the same child MCP process");
    }

    private sealed class ModelFixture : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            var data=JsonNode.Parse(await request.Content!.ReadAsStringAsync(token))!;
            if(request.Headers.Authorization?.Parameter!="test")throw new Exception("Auth header missing");
            object answer;
            if(data["tools"]==null){
                if((int?)(data["max_tokens"]??data["max_completion_tokens"])!=64)throw new Exception("Incorrect probe budget");
                answer=new{role="assistant",content="OK"};
            }else{
                var history=data["messages"]!.AsArray();
                if(history[0]!["content"]![1]!["type"]!.ToString()!="image_url")throw new Exception("Reference lost");
                if(history.Last()!["role"]!.ToString()=="tool")answer=new{role="assistant",content="done"};
                else answer=new{role="assistant",tool_calls=new[]{new{id=Guid.NewGuid().ToString(),type="function",function=new{name="height",arguments=history.Count==1?"{\"value\":4}":"{}"}}}};
            }
            return new(HttpStatusCode.OK){Content=new StringContent(Wire.Node(new{choices=new[]{new{message=answer}}}).ToJsonString(),Encoding.UTF8,"application/json")};
        }
    }
}
