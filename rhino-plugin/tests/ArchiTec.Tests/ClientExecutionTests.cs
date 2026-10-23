using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ArchiTec.Core;

static class ClientExecutionTests
{
    public static async Task<int> Run()
    {
        int count=0;void Check(bool value,string name){if(!value)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
        Check(!McpClientInstaller.SafeSetupDiagnostic("Error: cannot save\napi_key = private-value\nBearer private-access\nCause: file unavailable").Contains("private"),"Client setup diagnostics suppress credential-like and assignment lines");
        Check(McpClientInstaller.SafeSetupDiagnostic("Error: file unavailable").Contains("file unavailable"),"Client setup retains actionable non-credential error detail");
        void Reject(Action action,string name){try{action();}catch(Exception ex)when(ex is ArgumentException or InvalidOperationException){Check(true,name);return;}throw new Exception(name);}
        var ledger=new McpOperationLedger();var id=Guid.NewGuid().ToString();
        var plan=new ActionPlan{Summary="client box",Actions=[ModelAction.Make("create_box",new{width=1,depth=2,height=3})]};
        Check(ledger.Begin(id,1,plan,out _),"A new client operation is registered before execution");
        ledger.Complete(id,new{status="applied",created=new[]{"fixture"}});
        Check(!ledger.Begin(id,1,plan,out var receipt)&&JsonSerializer.Serialize(receipt).Contains("applied"),"Retry with the same UUID returns its receipt without a second execution");
        Check(!ledger.Begin(Guid.Parse(id).ToString("N").ToUpperInvariant(),1,plan,out _),"Alternate casing and UUID formatting cannot duplicate an operation");
        Reject(()=>ledger.Begin(id,2,plan,out _),"The same operation ID cannot be reused in another document");
        Reject(()=>ledger.Begin(id,1,plan with{Summary="different"},out _),"The same operation ID cannot silently execute a different plan");
        Reject(()=>ledger.Begin("invalid",1,plan,out _),"An operation requires a stable valid UUID");
        Reject(()=>PlanValidator.Validate(plan with{Actions=[ModelAction.Make("create_mesh",new{vertices=new[]{new[]{0,0,0},new[]{1,0,0},new[]{0,1,0},new[]{0,0,1}},faces=new[]{new[]{0,1,9}}})]}),"Mesh references outside the vertex array are rejected");
        Reject(()=>PlanValidator.Validate(plan with{Actions=[ModelAction.Make("create_mesh",new{vertices=new[]{new[]{0,0,0},new[]{1,0,0},new[]{0,1,0},new[]{0,0,1}},faces=new[]{new[]{0,0,1}}})]}),"Repeated face indices are rejected");
        Reject(()=>PlanValidator.Validate(plan with{Actions=[ModelAction.Make("create_loft",new{profiles=new[]{new[]{new[]{0,0,0},new[]{1,0,0},new[]{0,1,0}}}})]}),"A loft requires at least two bounded profiles");
        Reject(()=>PlanValidator.Validate(plan with{Actions=[ModelAction.Make("set_visibility",new{object_id=id,visible="false"})]}),"Visibility is a typed operation, not a script parameter");
        Reject(()=>PlanValidator.Validate(plan with{Actions=[ModelAction.Make("delete_objects",new{object_ids=Array.Empty<string>()})]}),"Deletion cannot target an implicit whole document");
        Reject(()=>PlanValidator.Validate(plan with{Actions=[ModelAction.Make("delete_objects",new{object_ids=new[]{"C:/private/file.3dm"}})]}),"Document deletion accepts object UUIDs, never filesystem paths");
        var tools=McpCatalog.Definitions.EnumerateArray().ToDictionary(t=>t.GetProperty("name").GetString()!);
        Check(tools["execute_plan"].GetProperty("annotations").GetProperty("destructiveHint").GetBoolean()&&tools.ContainsKey("apply_plan")&&tools.ContainsKey("cancel_plan"),"Native write tools request client authorization and manage previews without a local click");
        Check(tools["get_objects"].GetProperty("annotations").GetProperty("readOnlyHint").GetBoolean()&&!tools.ContainsKey("run_python")&&!tools.ContainsKey("run_command"),"Broad native geometry access does not expose arbitrary scripts or system commands");
        var probes=tools["get_objects"].GetProperty("inputSchema").GetProperty("properties").GetProperty("probe_points");
        Check(probes.GetProperty("maxItems").GetInt32()==100&&probes.GetProperty("items").GetProperty("minItems").GetInt32()==3,"Containment inspection advertises a bounded three-dimensional point contract");
        var listener=new System.Net.Sockets.TcpListener(IPAddress.Loopback,0);listener.Start();var port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
        var started=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var stopped=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var server=new McpServer(port,async(name,args,cancel)=>
        {
            if(name=="execute_plan")
            {started.TrySetResult();try{await Task.Delay(10000,cancel);}catch(OperationCanceledException){stopped.TrySetResult();throw;}}
            return new{ok=true};
        });server.Start();
        using var client=new HttpClient();client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/event-stream");client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",server.Token);
        async Task<HttpResponseMessage> Post(object body)=>await client.PostAsync(server.Endpoint,new StringContent(JsonSerializer.Serialize(body),Encoding.UTF8,"application/json"));
        using(var hello=await Post(new{jsonrpc="2.0",id=1,method="initialize",@params=new{protocolVersion="2025-11-25",clientInfo=new{name="cancel fixture"}}}))client.DefaultRequestHeaders.Add("Mcp-Session-Id",hello.Headers.GetValues("Mcp-Session-Id").Single());
        var active=Post(new{jsonrpc="2.0",id=2,method="tools/call",@params=new{name="execute_plan",arguments=new{}}});
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using(var cancelled=await Post(new{jsonrpc="2.0",method="notifications/cancelled",@params=new{requestId=2}}))Check(cancelled.StatusCode==HttpStatusCode.Accepted,"Client Stop notifications are accepted while another request is running");
        await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5));using(var response=await active)Check(response.IsSuccessStatusCode,"Cancellation reaches the active operation token without killing the listener");
        using(var next=await Post(new{jsonrpc="2.0",id=3,method="tools/call",@params=new{name="get_context",arguments=new{}}}))
        {using var json=JsonDocument.Parse(await next.Content.ReadAsStringAsync());Check(!json.RootElement.GetProperty("result").GetProperty("isError").GetBoolean(),"A new request works immediately after client cancellation");}
        return count;
    }
}
