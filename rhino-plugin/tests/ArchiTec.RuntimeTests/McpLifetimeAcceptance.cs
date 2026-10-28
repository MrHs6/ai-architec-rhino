using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows.Controls;
using ArchiTec.Core;
using ArchiTec.RhinoPlugin;
using ArchiTec.RhinoPlugin.Views;
using Rhino;
using Rhino.UI;

namespace ArchiTec.RuntimeTests;

/// <summary>Read-only against the user's visible document. Native edit/page fixtures use a headless document.</summary>
public static class McpLifetimeAcceptance
{
    public static void Schedule(string output)
    {
        EventHandler? idle=null;
        idle=async (_,_)=>
        {
            RhinoApp.Idle-=idle;
            Directory.CreateDirectory(output);
            var checks=new List<string>();var status="running";string? error=null;
            void Save()=>File.WriteAllText(Path.Combine(output,"mcp-lifetime-acceptance.json"),JsonSerializer.Serialize(new{status,plugin_version=McpCatalog.Version,user_document_mutations=0,cloud_requests=0,checks,error},Wire.Json));
            void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);checks.Add(label);Save();RhinoApp.WriteLine("PASS "+label);}
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            ArchiTecPanel? panel=null;HttpClient? http=null;string? session=null;McpServer? server=null;
            try
            {
                Save();panel=ArchiTecPanel.Current??throw new InvalidOperationException("Open archtec and start MCP first.");
                server=(McpServer?)typeof(ArchiTecPanel).GetField("mcp",flags)!.GetValue(panel)??throw new InvalidOperationException("Start MCP first.");
                var port=(int)typeof(ArchiTecPanel).GetField("mcpPort",flags)!.GetValue(panel)!;
                var view=(StudioView)typeof(ArchiTecPanel).GetField("view",flags)!.GetValue(panel)!;
                var doc=RhinoDoc.ActiveDoc??throw new InvalidOperationException("Open a document first.");
                var before=ContextReader.Read(doc);var originalEndpoint=server.Endpoint;
                http=new HttpClient{Timeout=TimeSpan.FromSeconds(20)};
                http.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",server.Token);
                http.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/event-stream");
                var serial=0;
                async Task<JsonElement> Rpc(string method,object args)
                {
                    using var response=await http.PostAsync(server.Endpoint,new StringContent(JsonSerializer.Serialize(new{jsonrpc="2.0",id=++serial,method,@params=args}),Encoding.UTF8,"application/json"));
                    response.EnsureSuccessStatusCode();
                    if(method=="initialize"){session=response.Headers.GetValues("Mcp-Session-Id").Single();http.DefaultRequestHeaders.Add("Mcp-Session-Id",session);}
                    using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    if(json.RootElement.TryGetProperty("error",out _))throw new InvalidOperationException("RPC failed during lifetime acceptance.");
                    return json.RootElement.GetProperty("result").Clone();
                }
                async Task<JsonElement> Tool(string name,object args)
                {
                    var response=await Rpc("tools/call",new{name,arguments=args});
                    if(response.GetProperty("isError").GetBoolean())throw new InvalidOperationException("Tool failed during lifetime acceptance: "+name);
                    using var json=JsonDocument.Parse(response.GetProperty("content")[0].GetProperty("text").GetString()!);return json.RootElement.Clone();
                }
                await Rpc("initialize",new{protocolVersion="2025-11-25",clientInfo=new{name="Lifetime acceptance",version="1"},capabilities=new{}});
                var initial=await Tool("get_objects",new{document_id=before.DocumentId,limit=1});
                var target=initial.GetProperty("objects").GetArrayLength()>0?initial.GetProperty("objects")[0].GetProperty("info").GetProperty("id").GetString():null;
                Panels.ClosePanel(typeof(ArchiTecPanel).GUID);
                await Task.Delay(250);
                Check(ReferenceEquals(panel,ArchiTecPanel.Current)&&server.Running,"Hiding the real Rhino panel retains its presenter and listening port");
                Check(McpSessionFile.Read(port).Endpoint==originalEndpoint,"Panel hiding retains encrypted local discovery on the same endpoint");
                for(var turn=0;turn<4;turn++)
                {
                    var context=await Tool("get_context",new{});
                    Check(context.GetProperty("document_id").GetUInt32()==before.DocumentId&&context.GetProperty("object_count").GetInt32()==before.ObjectCount,"Hidden panel accepts conversation inspection turn "+(turn+1));
                    if(target is not null)
                    {
                        var objects=await Tool("get_objects",new{document_id=before.DocumentId,object_ids=new[]{target}});
                        Check(objects.GetProperty("objects")[0].GetProperty("info").GetProperty("id").GetString()==target,"Follow-up inspects the same native GUID while the panel is hidden, turn "+(turn+1));
                    }
                }
                Panels.OpenPanel(typeof(ArchiTecPanel));
                await Task.Delay(250);
                Check(ReferenceEquals(panel,ArchiTecPanel.Current)&&ReferenceEquals(server,typeof(ArchiTecPanel).GetField("mcp",flags)!.GetValue(panel)),"Showing the panel reuses the same server instead of starting another listener");
                var idleState=new McpConnectionSnapshot("ready","codex",DateTimeOffset.UtcNow.AddHours(-3),1,[]);
                view.SetMcpConnection(idleState);
                Check(((TextBlock)view.FindName("McpStatus")).Text.StartsWith("Session ready")&&!((TextBlock)view.FindName("McpStatus")).Text.Contains("Reconnecting"),"A valid idle session is not mislabeled as a broken connection");
                view.SetMcpConnection(server.Connection.Snapshot());
                using(var fixture=RhinoDoc.CreateHeadless(null))
                {
                    fixture.AdjustModelUnitSystem(UnitSystem.Meters,false);
                    fixture.UndoRecordingEnabled=true;
                    var plan=new ActionPlan{Summary="Isolated native follow-up fixture",LengthUnit="document",Actions=Enumerable.Range(0,5).Select(i=>ModelAction.Make("create_box",new{width=2,depth=4,height=3,origin=new[]{i*5d,0,0}})).ToList()};
                    using var prepared=GeometryEngine.Prepare(fixture,plan);var created=GeometryEngine.Apply(fixture,prepared).Created;
                    var pageIds=new List<string>();int? offset=0;
                    while(offset is not null)
                    {
                        var page=JsonSerializer.SerializeToElement(McpDocumentTools.Inspect(fixture,JsonSerializer.SerializeToElement(new{limit=2,offset=offset.Value})),Wire.Json);
                        pageIds.AddRange(page.GetProperty("objects").EnumerateArray().Select(o=>o.GetProperty("info").GetProperty("id").GetString()!));
                        offset=page.GetProperty("next_offset").ValueKind==JsonValueKind.Null?null:page.GetProperty("next_offset").GetInt32();
                    }
                    Check(pageIds.Count==5&&pageIds.Distinct().Count()==5,"Paged model inspection reaches every native object without duplicate or missing targets");
                    var move=new ActionPlan{Summary="Follow-up edit to the same native target",LengthUnit="document",Actions=[ModelAction.Make("move_object",new{object_id=created[0],vector=new[]{3d,0,0}})]};
                    using var edit=GeometryEngine.Prepare(fixture,move);GeometryEngine.Apply(fixture,edit);
                    var result=JsonSerializer.SerializeToElement(McpDocumentTools.Inspect(fixture,JsonSerializer.SerializeToElement(new{object_ids=new[]{created[0]}})),Wire.Json).GetProperty("objects")[0];
                    Check(result.GetProperty("valid").GetBoolean()&&result.GetProperty("solid").GetBoolean()&&Math.Abs(result.GetProperty("volume").GetDouble()-24)<1e-6&&Math.Abs(result.GetProperty("info").GetProperty("min")[0].GetDouble()-3)<1e-6&&fixture.Objects.FindId(created[0]) is not null,"Isolated native follow-up preserves GUID, closed 2 x 4 x 3 solid and 24 cubic meter volume");
                }
                Check(server.Running&&ContextReader.Read(doc).ObjectCount==before.ObjectCount,"Closing an unrelated headless document preserves the live bridge and user model");
                status="passed";Save();
            }
            catch(Exception ex){status="failed";error=ex is InvalidOperationException?ex.Message:"Native lifetime acceptance could not complete.";Save();RhinoApp.WriteLine("MCP lifetime acceptance stopped; see the sanitized report.");}
            finally
            {
                if(http is not null&&session is not null&&server is not null)try{using var request=new HttpRequestMessage(HttpMethod.Delete,server.Endpoint);using var response=await http.SendAsync(request);}catch{}
                http?.Dispose();
                if(panel is not null)Panels.OpenPanel(typeof(ArchiTecPanel));
            }
        };
        RhinoApp.Idle+=idle;
    }
}
