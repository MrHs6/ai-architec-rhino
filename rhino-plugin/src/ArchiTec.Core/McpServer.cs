using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ArchiTec.Core;

/// <summary>Stateless MCP Streamable HTTP, protocol 2025-11-25 / 2025-06-18 / 2025-03-26.</summary>
public sealed class McpServer : IDisposable
{
    private readonly HttpListener listener=new();
    private readonly CancellationTokenSource lifetime=new();
    private readonly SemaphoreSlim connections=new(8);
    private readonly Func<string,JsonElement,CancellationToken,Task<object>> call;
    public string Token {get;}=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public string Endpoint {get;}
    public McpConnectionMonitor Connection {get;}=new();
    public bool Running=>listener.IsListening;
    public McpServer(int port,Func<string,JsonElement,CancellationToken,Task<object>> handler)
    {
        if(port is <1024 or >65535)throw new ArgumentException("Use a port between 1024 and 65535.");
        Endpoint=$"http://127.0.0.1:{port}/mcp";
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        call=handler;
    }
    public void Start(){listener.Start();_ = Listen();}
    private async Task Listen()
    {
        while(!lifetime.IsCancellationRequested)
        {
            try
            {
                var ctx=await listener.GetContextAsync().WaitAsync(lifetime.Token);
                if(!await connections.WaitAsync(0,lifetime.Token)){ctx.Response.StatusCode=503;ctx.Response.Close();continue;}
                _=HandleBounded(ctx);
            }
            catch(Exception) when(lifetime.IsCancellationRequested || !listener.IsListening){break;}
        }
    }
    private async Task HandleBounded(HttpListenerContext ctx)
    {
        try{await Handle(ctx);}catch{try{ctx.Response.StatusCode=500;ctx.Response.Close();}catch{}}
        finally{connections.Release();}
    }
    private async Task Handle(HttpListenerContext ctx)
    {
        var request=ctx.Request;var response=ctx.Response;
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);deadline.CancelAfter(TimeSpan.FromSeconds(30));
        if(request.Url?.AbsolutePath!="/mcp"){response.StatusCode=404;response.Close();return;}
        // No browser origins are trusted; desktop MCP clients and the stdio bridge omit Origin.
        if(request.Headers["Origin"] is not null){response.StatusCode=403;response.Close();return;}
        var credential=request.Headers["Authorization"];
        var expected="Bearer "+Token;
        if(credential is null || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(credential),Encoding.UTF8.GetBytes(expected)))
        {response.StatusCode=401;response.Close();return;}
        var session=request.Headers["Mcp-Session-Id"];
        if(session is not null&&!Connection.Contains(session)){response.StatusCode=404;response.Close();return;}
        if(request.HttpMethod=="DELETE"&&session is not null){Connection.Disconnect(session);response.StatusCode=204;response.Close();return;}
        if(request.HttpMethod!="POST"){response.StatusCode=405;response.Headers["Allow"]="POST, DELETE";response.Close();return;}
        var protocol=request.Headers["MCP-Protocol-Version"];
        if(protocol is not null && protocol is not ("2025-11-25" or "2025-06-18" or "2025-03-26"))
        {response.StatusCode=400;response.Close();return;}
        if(request.ContentType?.StartsWith("application/json",StringComparison.OrdinalIgnoreCase)!=true)
        {response.StatusCode=415;response.Close();return;}
        var accept=request.Headers["Accept"]??"";
        if(!accept.Contains("application/json") || !accept.Contains("text/event-stream"))
        {response.StatusCode=406;response.Close();return;}
        const int max=300_000;
        if(request.ContentLength64>max){response.StatusCode=413;response.Close();return;}
        using var output=new MemoryStream();var buffer=new byte[8192];int size;
        while((size=await request.InputStream.ReadAsync(buffer,deadline.Token))>0)
        {if(output.Length+size>max){response.StatusCode=413;response.Close();return;}await output.WriteAsync(buffer.AsMemory(0,size),deadline.Token);}
        object result;JsonElement id=default;
        try
        {
            using var json=JsonDocument.Parse(output.ToArray(),new JsonDocumentOptions{MaxDepth=32});
            var root=json.RootElement;
            if(root.ValueKind!=JsonValueKind.Object || !root.TryGetProperty("jsonrpc",out var rpc) || rpc.GetString()!="2.0")throw new RpcError(-32600,"Invalid JSON-RPC request.");
            if(root.TryGetProperty("id",out var requestId))id=requestId.Clone();
            if(id.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.String or JsonValueKind.Number))throw new RpcError(-32600,"Invalid JSON-RPC ID.");
            var method=root.TryGetProperty("method",out var methodValue) ? methodValue.GetString() : null;
            if(id.ValueKind==JsonValueKind.Undefined)
            {
                if(method is null || method.StartsWith("notifications/")){Connection.Touch(session,"heartbeat","");response.StatusCode=202;response.Close();return;}
                throw new RpcError(-32600,"A request ID is required.");
            }
            var args=root.TryGetProperty("params",out var p) ? p.Clone() : JsonSerializer.SerializeToElement(new {});
            if(method=="initialize"){session=Connection.Initialize(args);response.Headers["Mcp-Session-Id"]=session;}
            else Connection.Touch(session,method=="tools/list"?"tools_ready":"heartbeat",method??"request");
            result=method switch
            {
                "initialize"=>InitializationResult(args),
                "ping"=>new {},
                "tools/list"=>new {tools=ToolDefinitions},
                "tools/call"=>await CallTool(args,session,deadline.Token),
                _=>throw new RpcError(-32601,"Method not found.")
            };
            result=new {jsonrpc="2.0",id,result};
        }
        catch(JsonException){result=Error(id,-32700,"Invalid JSON.");}
        catch(RpcError ex){result=Error(id,ex.Code,ex.Message);}
        catch(OperationCanceledException){result=Error(id,-32603,"Request cancelled or timed out.");}
        catch{result=Error(id,-32603,"Internal error.");}
        var bytes=JsonSerializer.SerializeToUtf8Bytes(result);
        response.ContentType="application/json";response.ContentEncoding=Encoding.UTF8;response.ContentLength64=bytes.Length;
        await response.OutputStream.WriteAsync(bytes,deadline.Token);response.Close();
    }
    private static object Error(JsonElement id,int code,string message)=>new {jsonrpc="2.0",id=id.ValueKind==JsonValueKind.Undefined ? (object?)null : id,error=new {code,message}};
    private async Task<object> CallTool(JsonElement p,string? session,CancellationToken cancel)
    {
        if(!p.TryGetProperty("name",out var n))throw new RpcError(-32602,"Tool name is required.");
        var name=n.GetString()??"";
        if(name is not ("get_context" or "propose_plan" or "get_plan_status" or "post_message"))throw new RpcError(-32602,"Unknown tool.");
        try
        {
            Connection.Touch(session,name=="get_plan_status"?"heartbeat":"working",name);
            var args=p.TryGetProperty("arguments",out var a) ? a.Clone() : JsonSerializer.SerializeToElement(new {});
            var result=await call(name,args,cancel);
            if(name=="get_context")Connection.Touch(session,"context_ready",name);
            else if(name=="propose_plan")Connection.Touch(session,"preview","Waiting for Apply in Rhino");
            return new {content=new[]{new {type="text",text=JsonSerializer.Serialize(result,Wire.Json)}},isError=false};
        }
        catch(Exception ex) when(ex is ArgumentException or InvalidOperationException)
        {Connection.Touch(session,"error","Tool request failed; review the client response");return new {content=new[]{new {type="text",text=ex.Message}},isError=true};}
    }
    public static object InitializationResult(JsonElement args)=>new {protocolVersion=Negotiated(args),capabilities=new {tools=new {listChanged=false}},serverInfo=new {name="ai-architec-rhino",version="0.4.2-beta.1"},
        instructions="Use this ai-architec-rhino server, not another Rhino router. Read get_context first. If Rhino MCP is off, enable it in the Rhino panel and retry get_context; the bridge reconnects automatically. For replies use post_message with the current document_id. propose_plan only previews changes; Apply in Rhino confirms execution. Poll get_plan_status before claiming success or repeating a request. create_box parameters: width, depth, height, optional origin/layer. create_building: width, depth, floors, floor_height, optional origin/layer/facade. move_object: object_id, vector [x,y,z]. scale_object: object_id, factor, optional center. Never execute scripts or bypass Apply."};
    private static string Negotiated(JsonElement p)
    {
        var v=p.TryGetProperty("protocolVersion",out var version) ? version.GetString() : "";
        return v is "2025-11-25" or "2025-06-18" or "2025-03-26" ? v : "2025-11-25";
    }
    private sealed class RpcError(int code,string message):Exception(message){public int Code=>code;}
    public static readonly JsonElement ToolDefinitions=JsonDocument.Parse("""
    [
      {"name":"get_context","description":"Read active Rhino units, document ID, layers, selected objects and recently created objects. No geometry changes.","inputSchema":{"type":"object","properties":{},"additionalProperties":false},"annotations":{"readOnlyHint":true}},
      {"name":"post_message","description":"Display your conversational response or read-only analysis in the active Rhino Archi-Tec chat. Reads no API account and changes no geometry. Use the document_id from get_context; this does not Apply a plan.","inputSchema":{"type":"object","required":["document_id","message"],"additionalProperties":false,"properties":{"document_id":{"type":"integer"},"message":{"type":"string","minLength":1,"maxLength":8000},"workspace":{"type":"string","enum":["generate","analyze","assist"]}}},"annotations":{"readOnlyHint":false,"destructiveHint":false}},
      {"name":"propose_plan","description":"Validate and preview a plan in the Rhino panel. Requires the current document_id from get_context. A human must click Apply. Never assume execution; poll get_plan_status. Parameters use native plan actions (tool + parameters object), not scripts.","inputSchema":{"type":"object","required":["document_id","plan"],"additionalProperties":false,"properties":{"document_id":{"type":"integer"},"plan":{"type":"object","required":["version","summary","length_unit","actions"],"additionalProperties":false,"properties":{"version":{"type":"integer","enum":[1]},"summary":{"type":"string"},"length_unit":{"type":"string","enum":["meters","millimeters","centimeters","feet","inches","document"]},"actions":{"type":"array","maxItems":100,"items":{"type":"object","required":["tool","parameters"],"additionalProperties":false,"properties":{"tool":{"type":"string","enum":["create_building","create_box","create_line","create_polyline","create_curve","extrude_curve","move_object","scale_object","create_layer","select_objects","get_object_properties"]},"parameters":{"type":"object"}}}}}}}},"annotations":{"readOnlyHint":false,"destructiveHint":false}},
      {"name":"get_plan_status","description":"Read whether a proposed plan is awaiting_approval, applied, cancelled or failed, and the confirmed created/modified object IDs.","inputSchema":{"type":"object","required":["request_id"],"additionalProperties":false,"properties":{"request_id":{"type":"string"}}},"annotations":{"readOnlyHint":true}}
    ]
    """).RootElement.Clone();
    public void Dispose(){lifetime.Cancel();listener.Close();}
}
