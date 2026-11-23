using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using ArchiTec.Core;
using ArchiTec.RhinoPlugin;
using Rhino;
using Rhino.PlugIns;

[assembly: Guid("956592df-8071-47ac-9420-5e79e6bace8d")]
namespace Studio.Native;

public sealed class DesktopConnector : PlugIn
{
    readonly CancellationTokenSource lifetime=new();
    readonly McpOperationLedger receipts=new();
    readonly Dictionary<uint,List<Guid>> recent=new();
    static readonly HashSet<string> Supported = ["get_context","get_objects","get_capabilities","execute_plan","get_plan_status","get_grasshopper_context","search_grasshopper_components","get_viewport_image","set_view"];
    public override PlugInLoadTime LoadTime=>PlugInLoadTime.AtStartup;
    protected override LoadReturnCode OnLoad(ref string errorMessage)
    {
        _=Task.Run(()=>Listen(lifetime.Token));
        return LoadReturnCode.Success;
    }
    protected override void OnShutdown()=>lifetime.Cancel();

    async Task Listen(CancellationToken stop)
    {
        string name="AIArchiTec.Studio.Rhino."+Environment.ProcessId;
        while(!stop.IsCancellationRequested){
            try{
                using var pipe=new NamedPipeServerStream(name,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(stop);
                using var deadline=CancellationTokenSource.CreateLinkedTokenSource(stop);
                deadline.CancelAfter(TimeSpan.FromSeconds(85));
                using var reader=new StreamReader(pipe,Encoding.UTF8,false,1024,true);
                using var writer=new StreamWriter(pipe,new UTF8Encoding(false),1024,true){AutoFlush=true};
                using var doc=JsonDocument.Parse(await reader.ReadLineAsync(deadline.Token)??"null");
                string operation=doc.RootElement.GetProperty("operation").GetString()??"";
                var args=doc.RootElement.GetProperty("args").Clone();
                object reply;
                try{
                    object result;
                    if(operation=="catalog")result=McpCatalog.Definitions.EnumerateArray().Where(t=>Supported.Contains(t.GetProperty("name").GetString()!)).ToArray();
                    else{
                        if(!Supported.Contains(operation))throw new ArgumentException("Unsupported native operation.");
                        var task=new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
                        var token=deadline.Token;
                        RhinoApp.InvokeOnUiThread((Action)(()=>{
                            try{token.ThrowIfCancellationRequested();task.TrySetResult(Dispatch(operation,args,token));}
                            catch(Exception ex){task.TrySetException(ex);}
                        }));
                        var native=await task.Task.WaitAsync(token);
                        result=native is McpImage image
                            ? new { content=new object[]{new {type="image",data=image.Data,mimeType=image.MimeType},new {type="text",text=JsonSerializer.Serialize(image.Metadata,Wire.Json)}},isError=false }
                            : new {content=new object[]{new {type="text",text=JsonSerializer.Serialize(native,Wire.Json)}}, isError=false};
                    }
                    reply=new {result};
                }catch(Exception ex){reply=new {error=ex.Message};}
                await writer.WriteLineAsync(JsonSerializer.Serialize(reply));
            }catch(OperationCanceledException) when(stop.IsCancellationRequested){return;}
            catch(Exception ex){Trace.WriteLine("Studio native connector: "+ex.GetType().Name);await Task.Delay(150);}
        }
    }
    object Dispatch(string op,JsonElement args,CancellationToken token)
    {
        if(op=="get_capabilities")return McpCatalog.Capabilities;
        if(op=="get_plan_status")return receipts.Get(args.GetProperty("request_id").GetString()!);
        var doc=RhinoDoc.ActiveDoc??throw new InvalidOperationException("Open a document in Rhino.");
        uint id=doc.RuntimeSerialNumber;
        if(!recent.TryGetValue(id,out var ids))recent[id]=ids=new List<Guid>();
        if(op=="get_context")return ContextReader.Read(doc,ids);
        if(args.GetProperty("document_id").GetUInt32()!=id)throw new InvalidOperationException("Active document changed. Inspect it again.");
        switch(op){
            case "get_objects":return McpDocumentTools.Inspect(doc,args);
            case "get_viewport_image":return McpDocumentTools.Capture(doc);
            case "set_view":return McpDocumentTools.Frame(doc,args,ids);
            case "get_grasshopper_context":return GrasshopperHost.Context(id);
            case "search_grasshopper_components":return GrasshopperHost.Search(args.GetProperty("query").GetString()!,args.TryGetProperty("limit",out var limit)?limit.GetInt32():20);
            case "execute_plan":
                var plan=Wire.Read<ActionPlan>(args.GetProperty("plan").GetRawText());
                string request=args.GetProperty("request_id").GetString()!;
                if(!receipts.Begin(request,id,plan,out var receipt))return receipt;
                try{
                    using var prepared=GeometryEngine.Prepare(doc,plan);
                    var report=GeometryEngine.ApplyCancellable(doc,prepared,token);
                    ids.Clear();ids.AddRange(report.Created.Concat(report.Modified).Distinct());
                    receipt=new {status="applied",request_id=request,document_id=id,report.Created,report.Modified,report.UndoSerial,report.Properties,report.Grasshopper};
                    receipts.Complete(request,receipt);return receipt;
                }catch(Exception ex){receipts.Complete(request,new {status="failed",message=ex.Message});throw;}
            default:throw new ArgumentException("Unsupported native operation.");
        }
    }
}
