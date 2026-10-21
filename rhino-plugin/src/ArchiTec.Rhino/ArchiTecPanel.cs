using System.Runtime.InteropServices;
using System.Text.Json;
using ArchiTec.Core;
using ArchiTec.RhinoPlugin.Views;
using Eto.Forms;
using Rhino;

namespace ArchiTec.RhinoPlugin;

/// <summary>Native Rhino panel and workflow presenter. Geometry, API transport and view styling are separate.</summary>
[Guid("a0d2a671-ec0b-4f76-af38-3baea6fd8c64")]
public sealed class ArchiTecPanel:Panel
{
    public static ArchiTecPanel? Current {get;private set;}
    private readonly StudioView view;
    private readonly UITimer contextTimer=new(){Interval=1};
    private readonly List<ChatMessage> history=[];
    private readonly List<Guid> lastCreated=[];
    private readonly Dictionary<string,object> mcpResults=[];
    private ConnectionSettings settings=new();
    private IPlannerClient? provider;
    private CancellationTokenSource? requestCancel;
    private PreparedPlan? pending;
    private PlanConduit? conduit;
    private McpServer? mcp;
    private string? pendingMcpId,imageData;
    private string mode="generate";
    private bool busy,disposed;
    public ConnectionSettings CurrentSettings=>settings;
    public bool TryUpdateSettings(ConnectionSettings value)
    {
        if(busy)return false;
        SettingsStore.Save(value);provider?.Dispose();provider=null;settings=value;
        view.LoadSettings(value);view.SetOffline();ClearPreview();
        view.SetStatus("Connection settings saved. Open Settings to load models and connect.");
        return true;
    }
    public ArchiTecPanel()
    {
        Current=this;Padding=0;MinimumSize=new Eto.Drawing.Size(350,540);
        view=new StudioView();Content=NativeViewHost.Wrap(view);
        try{settings=SettingsStore.Load();}catch{view.SetStatus("Saved connection could not be read. Open Settings to reconnect.");}
        view.LoadSettings(settings);
        view.SendRequested+=()=>_ = SendAsync();view.ApplyRequested+=ApplyPending;
        view.CancelRequested+=()=>{requestCancel?.Cancel();ClearPreview();view.SetStatus("Cancelled · document unchanged");};
        view.InputChanged+=()=>{if(pending is not null)ClearPreview();};
        view.SelectedModelChanged+=()=>{ClearPreview();PersistSettings();};
        view.ModeChanged+=value=>{if(busy)return;mode=value;ClearPreview();};
        view.AttachRequested+=AttachImage;view.ImportRequested+=ImportPlan;view.ConnectRequested+=()=>_ = ConnectAsync();
        view.OfflineRequested+=()=>{provider?.Dispose();provider=null;view.SetOffline();view.CloseConnections();ClearPreview();};
        view.McpStartRequested+=()=>{if(mcp is null){try{StartMcp();}catch(Exception ex){view.SetStatus("MCP: "+ex.Message);}}else StopMcp();};
        view.CopyMcpRequested+=CopyMcpConfiguration;
        view.OpenChatRequested+=()=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://chatgpt.com"){UseShellExecute=true});
        contextTimer.Elapsed+=(_,_)=>RefreshContext();
        Load+=(_,_)=>contextTimer.Start();UnLoad+=(_,_)=>{contextTimer.Stop();requestCancel?.Cancel();StopMcp();ClearPreview();};
        contextTimer.Start();RefreshContext();
    }
    public static void SizeFloatingWindow()=>Application.Instance.AsyncInvoke(()=>
    {
        var window=Current?.ParentWindow;if(window is null||!window.GetType().Name.Contains("Floating"))return;
        var area=Screen.PrimaryScreen.WorkingArea;
        window.ClientSize=new Eto.Drawing.Size(560,Math.Min(830,(int)area.Height-85));
        window.Location=new Eto.Drawing.Point(Math.Max((int)area.Left,(int)area.Right-590),(int)area.Top+25);
    });
    private async Task ConnectAsync()
    {
        if(busy)return;busy=true;view.SetBusy(true);view.ApiMessage("Connecting and discovering models…");
        var source=view.Source;var endpoint=view.Endpoint;var key=view.ApiCredential;var remember=view.Remember;
        try
        {
            var candidate=new ProviderClient(endpoint,key,source=="OpenRouter");
            ModelCatalog catalog;
            try{using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));catalog=await candidate.Models(timeout.Token);}
            catch{candidate.Dispose();throw;}
            if(disposed){candidate.Dispose();return;}
            if(catalog.Models.Count==0){candidate.Dispose();throw new InvalidOperationException("No models with the required capabilities were found.");}
            provider?.Dispose();provider=candidate;
            settings=new ConnectionSettings(source,endpoint,key,remember,settings.Model);
            view.SetCatalog(catalog.Models,settings.Model,settings.Source);PersistSettings();ClearPreview();
            view.ApiMessage($"Connected · {catalog.Models.Count} models available");view.CloseConnections();view.SetStatus("Connected · selected model will be used exactly");
        }
        catch(Exception ex){view.ApiMessage("Connection failed: "+ex.Message);}
        finally{busy=false;view.SetBusy(false);}
    }
    private void PersistSettings()
    {
        settings=settings with{Model=view.SelectedModel?.Id??settings.Model};
        try{SettingsStore.Save(settings);}catch{view.SetStatus("Settings could not be saved; connection remains available for this session.");}
    }
    private void RefreshContext()
    {
        var doc=RhinoDoc.ActiveDoc;if(doc is null){view.SetContext("Open a Rhino document to begin");ClearPreview();return;}
        view.SetContext($"●  {doc.ModelUnitSystem}   ·   {doc.Objects.GetObjectList(Rhino.DocObjects.ObjectType.AnyObject).Count()} objects   ·   {doc.Objects.GetSelectedObjects(false,false).Count()} selected");
        if(pending is not null)try{GeometryEngine.CheckCurrent(doc,pending);}catch{ClearPreview();view.SetStatus("Model changed · generate a fresh preview");}
    }
    private void AddMessage(string role,string value,bool error=false)
    {
        history.Add(new ChatMessage(role,value));if(history.Count>24)history.RemoveRange(0,history.Count-24);
        view.AddMessage(role,value,error);
    }
    public async Task SendAsync()
    {
        if(busy||string.IsNullOrWhiteSpace(view.Prompt))return;
        var doc=RhinoDoc.ActiveDoc;if(doc is null){view.SetStatus("Open a Rhino document first.");return;}
        ClearPreview();var context=ContextReader.Read(doc,lastCreated);
        var fingerprints=context.Objects.ToDictionary(o=>Guid.Parse(o.Id),o=>doc.Objects.FindId(Guid.Parse(o.Id))!.Geometry.DataCRC(0));
        var units=doc.ModelUnitSystem;var tolerance=doc.ModelAbsoluteTolerance;var text=view.Prompt.Trim();var previous=history.TakeLast(12).ToList();
        AddMessage("user",text);busy=true;requestCancel=new CancellationTokenSource();view.SetBusy(true);view.SetStatus("Understanding your request…");
        try
        {
            ActionPlan? plan;
            if(provider is null)
            {
                if(imageData is not null)throw new ArgumentException("Connect an AI service and a vision-capable model to interpret a reference image.");
                if(mode=="analyze"){AddMessage("assistant",ContextSummary(context));view.SetStatus("Rhino model context read");return;}
                plan=LocalPlanner.Plan(text,context);AddMessage("assistant","Offline example · "+plan.Summary);
            }
            else
            {
                var model=view.SelectedModel??throw new ArgumentException("Select a model before sending.");
                if(imageData is not null&&!model.Vision)throw new ArgumentException("Image input is not confirmed for this model. Select a vision-capable OpenRouter model.");
                var response=await provider.Plan(new PlanRequest(text,model.Id,mode,context,previous,imageData),requestCancel.Token);
                requestCancel.Token.ThrowIfCancellationRequested();if(disposed)return;
                if(response.Model!=model.Id)throw new InvalidOperationException("The response used a different model and was rejected.");
                plan=response.Plan;if(mode=="analyze"&&plan is not null)throw new InvalidOperationException("Analyze mode cannot change geometry.");
                AddMessage("assistant",response.Message);
            }
            if(RhinoDoc.ActiveDoc?.RuntimeSerialNumber!=doc.RuntimeSerialNumber||doc.ModelUnitSystem!=units||doc.ModelAbsoluteTolerance!=tolerance)
                throw new InvalidOperationException("The active document changed while planning. Send the request again.");
            foreach(var pair in fingerprints)
                if(doc.Objects.FindId(pair.Key) is not {} obj||obj.Geometry.DataCRC(0)!=pair.Value)throw new InvalidOperationException("A target changed while planning. Generate a fresh plan.");
            if(plan is null){view.SetStatus("Response received · no model changes proposed");return;}
            SetPreview(doc,plan);
        }
        catch(OperationCanceledException){view.SetStatus("Request cancelled or timed out");}
        catch(Exception ex){AddMessage("assistant",ex.Message,true);view.SetStatus("No changes applied · review the message");}
        finally{busy=false;view.SetBusy(false);requestCancel?.Dispose();requestCancel=null;}
    }
    private static string ContextSummary(ModelContext c)=>$"Document units: {c.Units}\nActive layer: {c.ActiveLayer}\nObjects: {c.ObjectCount}\nSelected: {c.SelectedIds.Length}\n"+
        (c.Objects.Length==0?"Select model objects for a closer analysis.":string.Join("\n",c.Objects.Select(o=>$"{o.Name??o.Type} · {o.Layer} · bounds {string.Join(", ",o.Min)} → {string.Join(", ",o.Max)}")));
    public void SetPreview(RhinoDoc doc,ActionPlan plan)
    {
        ClearPreview();pending=GeometryEngine.Prepare(doc,plan);conduit=new PlanConduit(doc,pending.Preview){Enabled=true};
        view.ShowPreview(plan,doc.ModelUnitSystem.ToString());view.SetStatus("Preview in Rhino · ready for your Apply");doc.Views.Redraw();
    }
    public void ClearPreview()
    {
        if(pendingMcpId is not null)
        {
            if(mcpResults.TryGetValue(pendingMcpId,out var value)&&JsonSerializer.SerializeToElement(value).GetProperty("status").GetString()=="awaiting_approval")
                mcpResults[pendingMcpId]=new{status="cancelled",message="Preview cancelled or invalidated."};
            pendingMcpId=null;
        }
        if(conduit is not null){conduit.Enabled=false;conduit=null;}pending?.Dispose();pending=null;view.HidePreview();RhinoDoc.ActiveDoc?.Views.Redraw();
    }
    public void ApplyPending()
    {
        if(pending is null)return;
        try
        {
            var doc=RhinoDoc.ActiveDoc??throw new InvalidOperationException("No active document.");var report=GeometryEngine.Apply(doc,pending);
            if(pendingMcpId is not null)mcpResults[pendingMcpId]=new{status="applied",created=report.Created,modified=report.Modified,properties=report.Properties,undo_serial=report.UndoSerial};
            if(report.Created.Count>0){lastCreated.Clear();lastCreated.AddRange(report.Created);}
            AddMessage("assistant",$"Built in your current Rhino document.\n{report.Created.Count} native objects created · {report.Modified.Count} modified.\nUse Rhino Ctrl+Z to undo this operation."+
                (report.Properties.Count>0?"\n"+JsonSerializer.Serialize(report.Properties,Wire.Json):""));
            ClearPreview();view.SetStatus("Applied · confirmed by Rhino");RefreshContext();
        }
        catch(Exception ex){if(pendingMcpId is not null)mcpResults[pendingMcpId]=new{status="failed",message=ex.Message};AddMessage("assistant","Could not apply: "+ex.Message,true);ClearPreview();view.SetStatus("Apply failed · review the message");}
    }
    private void AttachImage()
    {
        if(busy)return;if(imageData is not null){imageData=null;view.SetAttachment(null);ClearPreview();return;}
        using var dialog=new OpenFileDialog{Title="Add an architectural reference"};dialog.Filters.Add(new FileFilter("Reference images",".png",".jpg",".jpeg"));
        if(dialog.ShowDialog(this)!=DialogResult.Ok)return;
        var file=new FileInfo(dialog.FileName);if(file.Length>4_000_000){view.SetStatus("Reference must be smaller than 4 MB.");return;}
        var mime=file.Extension.ToLowerInvariant() is ".jpg" or ".jpeg"?"image/jpeg":"image/png";
        imageData=$"data:{mime};base64,"+Convert.ToBase64String(File.ReadAllBytes(file.FullName));view.SetAttachment(file.Name);ClearPreview();
    }
    private void ImportPlan()
    {
        if(busy)return;using var dialog=new OpenFileDialog{Title="Preview a structured action plan"};dialog.Filters.Add(new FileFilter("Action plans",".json"));
        if(dialog.ShowDialog(this)!=DialogResult.Ok)return;
        try{if(new FileInfo(dialog.FileName).Length>250_000)throw new ArgumentException("Plan is too large.");SetPreview(RhinoDoc.ActiveDoc??throw new InvalidOperationException("No document."),Wire.Read<ActionPlan>(File.ReadAllText(dialog.FileName)));}
        catch(Exception ex){ClearPreview();view.SetStatus(ex.Message);}
    }
    public void StartMcp(int port=5188)
    {
        if(mcp is not null)return;var candidate=new McpServer(port,HandleMcp);try{candidate.Start();mcp=candidate;view.SetMcp(true,mcp.Endpoint,mcp.Token);}catch{candidate.Dispose();throw;}
    }
    public void StopMcp(){mcp?.Dispose();mcp=null;if(pendingMcpId is not null)ClearPreview();view.SetMcp(false);}
    private void CopyMcpConfiguration()
    {
        if(mcp is null){view.SetStatus("Start MCP first.");return;}
        var directory=Path.GetDirectoryName(typeof(ArchiTecPanel).Assembly.Location)!;
        var config=new{mcpServers=new Dictionary<string,object>{["ai-architec-rhino"]=new{command=Path.Combine(directory,"AIArchiTec.McpBridge.exe"),env=new Dictionary<string,string>{["ARCHITEC_MCP_URL"]=mcp.Endpoint,["ARCHITEC_MCP_TOKEN"]=mcp.Token}}}};
        System.Windows.Clipboard.SetText(JsonSerializer.Serialize(config,new JsonSerializerOptions{WriteIndented=true}));view.SetStatus("Desktop setup copied · keep the local token private");
    }
    private async Task<object> HandleMcp(string name,JsonElement args,CancellationToken token)
    {
        var completion=new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);using var registration=token.Register(()=>completion.TrySetCanceled(token));
        RhinoApp.InvokeOnUiThread((Action)(()=>
        {
            if(token.IsCancellationRequested||disposed)return;
            try
            {
                if(name=="get_plan_status"){var id=args.GetProperty("request_id").GetString()??"";completion.TrySetResult(mcpResults.TryGetValue(id,out var result)?result:new{status="unknown"});return;}
                var doc=RhinoDoc.ActiveDoc??throw new InvalidOperationException("No active Rhino document.");
                if(name=="get_context"){completion.TrySetResult(ContextReader.Read(doc,lastCreated));return;}
                if(busy||pending is not null)throw new InvalidOperationException("A request or preview is active. Ask the user to Apply or Cancel first.");
                if(args.GetProperty("document_id").GetUInt32()!=doc.RuntimeSerialNumber)throw new InvalidOperationException("The document changed. Read get_context again.");
                var plan=Wire.Read<ActionPlan>(args.GetProperty("plan").GetRawText());SetPreview(doc,plan);pendingMcpId=Guid.NewGuid().ToString();
                if(mcpResults.Count>=50)mcpResults.Remove(mcpResults.Keys.First());mcpResults[pendingMcpId]=new{status="awaiting_approval"};
                AddMessage("assistant","MCP proposal · "+plan.Summary);
                completion.TrySetResult(new{request_id=pendingMcpId,status="awaiting_approval",message="Preview only. The user must click Apply in Rhino."});
            }
            catch(Exception ex){completion.TrySetException(new InvalidOperationException(ex.Message));}
        }));return await completion.Task;
    }
    protected override void Dispose(bool disposing)
    {
        if(disposing){disposed=true;contextTimer.Dispose();requestCancel?.Cancel();provider?.Dispose();StopMcp();ClearPreview();if(Current==this)Current=null;}
        base.Dispose(disposing);
    }
}
