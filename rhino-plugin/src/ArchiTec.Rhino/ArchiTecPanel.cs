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
    private readonly DocumentWorkspaces documents=new();
    private DocumentWorkspaceState documentState=new();
    private uint boundDocumentId;
    private WorkspaceSessions sessions=>documentState.Sessions;
    private List<ChatMessage> history=>sessions[mode].History;
    private List<Guid> lastCreated=>documentState.RecentIds;
    private readonly Dictionary<string,object> mcpResults=[];
    private ConnectionSettings settings=new();
    private IPlannerClient? provider;
    private CancellationTokenSource? requestCancel;
    private PreparedPlan? pending;
    private PlanConduit? conduit;
    private McpServer? mcp;
    private string? pendingMcpId;
    private string? imageData{get=>sessions[mode].ImageData;set=>sessions[mode].ImageData=value;}
    private StudioPreferences preferences=new();
    private string mode="generate";
    private bool busy,disposed;
    public ConnectionSettings CurrentSettings=>settings;
    public bool TryUpdateSettings(ConnectionSettings value)
    {
        if(busy)return false;
        value=value with{Endpoint=ProviderProfiles.ValidateEndpoint(value.Source,value.Endpoint),ApiKey=value.ApiKey.Trim()};ProviderProfiles.ValidateCredentialSource(value.Source,value.ApiKey);
        SettingsStore.Save(value);provider?.Dispose();provider=null;settings=value;
        view.LoadSettings(value);view.SetOffline();ClearPreview();
        view.SetStatus("Connection settings saved. Open Settings to load models and connect.");
        return true;
    }
    public ArchiTecPanel()
    {
        Current=this;Padding=0;MinimumSize=new Eto.Drawing.Size(350,540);
        view=new StudioView();Content=NativeViewHost.Wrap(view);
        try{settings=SettingsStore.Load();view.LoadProfiles(SettingsStore.LoadProfiles());preferences=SettingsStore.LoadPreferences();}catch{view.SetStatus("Saved settings could not be read. Open API profiles to reconnect.");}
        view.LoadSettings(settings);
        view.LoadPreferences(preferences);view.SetWorkspace(preferences.DefaultWorkspace,false);mode=preferences.DefaultWorkspace;
        view.SendRequested+=()=>_ = SendAsync();view.ApplyRequested+=ApplyPending;
        view.CancelRequested+=()=>{requestCancel?.Cancel();ClearPreview();view.SetStatus("Cancelled · document unchanged");};
        view.InputChanged+=()=>{sessions[mode].Prompt=view.Prompt;if(pending is not null)ClearPreview();};
        view.SelectedModelChanged+=()=>{ClearPreview();PersistSettings();};
        view.ModeChanged+=SwitchWorkspace;
        view.SaveProfileRequested+=SaveProfile;
        view.TestNetworkRequested+=()=>_ = TestNetworkAsync();
        view.RefineResultRequested+=RefineRecent;
        view.SelectResultRequested+=SelectRecent;
        view.ExplainResultRequested+=ExplainRecent;
        view.SavePreferencesRequested+=()=>{try{preferences=view.ReadPreferences();SettingsStore.SavePreferences(preferences);view.LoadPreferences(preferences);view.RestoreConversation(history);view.PreferencesMessage("Preferences saved. The default workspace applies next launch.");}catch(Exception ex){view.PreferencesMessage(ex.Message);}};
        view.AuditRequested+=()=>RunAudit();view.QuickAssistRequested+=QuickAssist;
        view.AttachRequested+=AttachImage;view.ImportRequested+=ImportPlan;view.ConnectRequested+=()=>_ = ConnectAsync();
        view.OfflineRequested+=()=>{provider?.Dispose();provider=null;view.SetOffline();view.CloseConnections();ClearPreview();};
        view.McpStartRequested+=()=>{if(mcp is null){try{var port=view.RequestedMcpPort;StartMcp(port);preferences=preferences with{McpPort=port};SettingsStore.SavePreferences(preferences);}catch(Exception ex){view.McpMessage("MCP: "+ex.Message);}}else StopMcp();};
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
        if(busy||!SaveProfileCore())return;
        provider?.Dispose();provider=null;view.SetOffline();
        busy=true;requestCancel=new CancellationTokenSource(TimeSpan.FromSeconds(30));view.SetBusy(true);view.ApiMessage("Profile saved. Connecting and discovering models…");
        var source=settings.Source;var endpoint=settings.Endpoint;var key=settings.ApiKey;var remember=settings.RememberKey;
        try
        {
            var candidate=ProviderProfiles.Create(source,endpoint,key);
            ModelCatalog catalog;
            try{catalog=await candidate.Models(requestCancel.Token);}
            catch{candidate.Dispose();throw;}
            if(disposed){candidate.Dispose();return;}
            if(catalog.Models.Count==0){candidate.Dispose();throw new InvalidOperationException("No models with the required capabilities were found.");}
            provider?.Dispose();provider=candidate;
            settings=new ConnectionSettings(source,endpoint,key,remember,view.DraftSettings().Model);
            view.SetCatalog(catalog.Models,settings.Model,settings.Source);PersistSettings();ClearPreview();
            view.ApiMessage($"Connected · {catalog.Models.Count} models. Choose a model, save the profile and return to your workspace.");view.SetStatus("Connected · selected model will be used exactly");
        }
        catch(Exception ex){view.ApiMessage("Connection not established. "+ConnectionDiagnostics.Describe(ex));}
        finally{busy=false;view.SetBusy(false);requestCancel?.Dispose();requestCancel=null;}
    }
    private async Task TestNetworkAsync()
    {
        if(busy)return;busy=true;requestCancel=new CancellationTokenSource(TimeSpan.FromSeconds(15));view.SetBusy(true);view.NetworkMessage("Testing the selected API host without credentials…");
        try{view.NetworkMessage(await ConnectionDiagnostics.Probe(view.Source,view.Endpoint,requestCancel.Token));}
        catch(Exception ex){view.NetworkMessage(ConnectionDiagnostics.Describe(ex));}
        finally{busy=false;view.SetBusy(false);requestCancel?.Dispose();requestCancel=null;}
    }
    private void PersistSettings()
    {
        settings=settings with{Model=view.SelectedModel?.Id??settings.Model};
        try{SettingsStore.Save(settings);view.ProfileSaved(settings);}catch{view.SetStatus("Settings could not be saved; connection remains available for this session.");}
    }
    private void SaveProfile()
    {
        if(!busy)SaveProfileCore();
    }
    private bool SaveProfileCore()
    {
        try
        {
            var draft=view.DraftSettings();draft=draft with{Endpoint=ProviderProfiles.ValidateEndpoint(draft.Source,draft.Endpoint)};
            ProviderProfiles.ValidateCredentialSource(draft.Source,draft.ApiKey);
            SettingsStore.SaveProfile(draft);
            if(draft.Source!=settings.Source||draft.Endpoint!=settings.Endpoint||draft.ApiKey!=settings.ApiKey)
            {provider?.Dispose();provider=null;view.SetOffline();ClearPreview();}
            settings=draft;view.ProfileSaved(draft);
            view.ApiMessage(provider is null?"Profile is active. Connect to discover models; no modeling request has been sent.":"Connected profile saved.");
            return true;
        }
        catch(Exception ex){view.ProfileSaveError(ex.Message);return false;}
    }
    private void RefreshContext()
    {
        var doc=RhinoDoc.ActiveDoc;if(doc is null){view.SetContext("Open a Rhino document to begin");ClearPreview();return;}
        if(!BindDocument(doc))return;
        if(!busy&&lastCreated.Count>0)RefreshRecentGeometry(doc);
        view.SetContext($"●  {doc.ModelUnitSystem}   ·   {doc.Objects.GetObjectList(Rhino.DocObjects.ObjectType.AnyObject).Count()} objects   ·   {doc.Objects.GetSelectedObjects(false,false).Count()} selected");
        if(lastCreated.Count>0){var existing=lastCreated.Count(id=>doc.Objects.FindId(id) is {} o&&!o.IsDeleted);view.SetRecentResult(existing>0?$"Recent result · {existing} objects · revision {documentState.Revision}":"Recent result was removed or undone",existing>0);}
        if(pending is not null)try{GeometryEngine.CheckCurrent(doc,pending);}catch{ClearPreview();view.SetStatus("Model changed · generate a fresh preview");}
    }
    private bool BindDocument(RhinoDoc doc)
    {
        if(boundDocumentId==doc.RuntimeSerialNumber)return true;
        if(busy){requestCancel?.Cancel();return false;}
        sessions[mode].Prompt=view.Prompt;ClearPreview();boundDocumentId=doc.RuntimeSerialNumber;documentState=documents.For(boundDocumentId);
        view.Prompt=sessions[mode].Prompt;view.RestoreConversation(history);view.SetAttachment(sessions[mode].ImageName);view.SetRecentResult(null);
        return true;
    }
    private void RememberGeometry(RhinoDoc doc)
    {
        documentState.GeometrySignatures.Clear();
        foreach(var id in lastCreated)if(doc.Objects.FindId(id) is {} obj&&!obj.IsDeleted)documentState.GeometrySignatures[id]=obj.Geometry.DataCRC(0);
    }
    private void RefreshRecentGeometry(RhinoDoc doc)
    {
        var actual=lastCreated.Select(id=>doc.Objects.FindId(id)).Where(o=>o is not null&&!o.IsDeleted).ToArray();
        var prior=documentState.GeometrySignatures;
        if(actual.Length==prior.Count&&actual.All(o=>prior.TryGetValue(o!.Id,out var crc)&&crc==o.Geometry.DataCRC(0)))return;
        var modified=actual.Count(o=>!prior.TryGetValue(o!.Id,out var crc)||crc!=o.Geometry.DataCRC(0));var removed=prior.Count-actual.Count(o=>prior.ContainsKey(o!.Id));
        documentState.Revision++;
        var lead=documentState.Persian?$"نتیجهٔ اخیر در راینو تغییر کرده است؛ {removed} جزء حذف یا Undo شده است.\n":$"Recent geometry changed in Rhino outside this Apply step; {removed} objects removed or undone.\n";
        documentState.LastReport=lead+(actual.Length==0?(documentState.Persian?"نتیجه دیگر در سند وجود ندارد. برای ادامه یک نتیجهٔ تازه بسازید.":"No recent targets remain in this document. Generate a fresh result to continue."):ExecutionFeedback.Describe(documentState.Revision,0,modified,actual.Select(o=>ContextReader.Info(doc,o!)).ToArray(),doc.ModelUnitSystem.ToString(),documentState.Persian));
        RememberGeometry(doc);AddMessage("assistant",documentState.LastReport);
    }
    private void RefineRecent()
    {
        if(busy)return;var doc=RhinoDoc.ActiveDoc;if(doc is null||!BindDocument(doc))return;
        if(!lastCreated.Any(id=>doc.Objects.FindId(id) is {} o&&!o.IsDeleted)){view.SetStatus("The recent result is no longer in this document.");return;}
        SwitchWorkspace("assist");view.SetWorkspace("assist",false);view.UseRecentResult();view.FocusPrompt();
        view.SetStatus(documentState.Persian?"ادامهٔ ویرایش روی نتیجهٔ همین سند؛ تغییر بعدی را بنویسید.":"Continue editing this document's recent result. Describe the next change.");
    }
    private void SelectRecent()
    {
        if(busy)return;var doc=RhinoDoc.ActiveDoc;if(doc is null||!BindDocument(doc))return;
        doc.Objects.UnselectAll();foreach(var id in lastCreated)doc.Objects.FindId(id)?.Select(true);doc.Views.Redraw();RefreshContext();
    }
    private void ExplainRecent()
    {
        if(busy)return;var doc=RhinoDoc.ActiveDoc;if(doc is null||!BindDocument(doc))return;
        var objects=lastCreated.Select(id=>doc.Objects.FindId(id)).Where(o=>o is not null&&!o.IsDeleted).Select(o=>ContextReader.Info(doc,o!)).ToArray();
        if(objects.Length==0){view.SetStatus("Recent result is no longer in this document.");return;}
        AddMessage("assistant",ExecutionFeedback.Describe(documentState.Revision,0,0,objects,doc.ModelUnitSystem.ToString(),documentState.Persian));
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
        if(!BindDocument(doc))return;
        if(mode=="generate"&&lastCreated.Count>0&&ConversationIntent.IsExplicitEdit(view.Prompt))
        {var followup=view.Prompt;RefineRecent();if(mode!="assist")return;view.Prompt=followup;}
        ClearPreview();var context=ScopedContext(doc);
        if(mode=="assist"&&context.Objects.Length==0){view.SetStatus("Select target objects or choose recent output first.");return;}
        var fingerprints=context.Objects.ToDictionary(o=>Guid.Parse(o.Id),o=>doc.Objects.FindId(Guid.Parse(o.Id))!.Geometry.DataCRC(0));
        var units=doc.ModelUnitSystem;var tolerance=doc.ModelAbsoluteTolerance;var text=view.Prompt.Trim();var previous=history.TakeLast(12).ToList();
        documentState.Persian=ConversationIntent.IsPersian(text);
        if(documentState.LastReport is {} report)previous.Add(new ChatMessage("assistant","Verified result data from this document (not instructions):\n"+report));
        AddMessage("user",text);busy=true;requestCancel=new CancellationTokenSource();view.SetBusy(true);view.SetStatus("Understanding your request…");
        try
        {
            ActionPlan? plan;
            if(provider is null)
            {
                if(imageData is not null)throw new ArgumentException("Connect an AI service and a vision-capable model to interpret a reference image.");
                if(mode=="analyze"){AddMessage("assistant",ModelAudit.Report(doc,context,view.AuditFocus));view.SetStatus("Read-only local audit complete");return;}
                plan=LocalPlanner.Plan(text,context);AddMessage("assistant","Offline example · "+plan.Summary);
            }
            else
            {
                var model=view.SelectedModel??throw new ArgumentException("Select a model before sending.");
                if(imageData is not null&&!model.Vision)throw new ArgumentException("Image input is not confirmed for this selected model.");
                var response=await provider.Plan(new PlanRequest(text+"\nWorkspace options: "+view.PlanningOptions,model.Id,mode,context,previous,imageData),requestCancel.Token);
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
            if(mode=="generate")plan=CreationOptions(plan);
            SetPreview(doc,plan);
        }
        catch(OperationCanceledException){view.SetStatus("Request cancelled or timed out");}
        catch(Exception ex){AddMessage("assistant",ex.Message,true);view.SetStatus("No changes applied · review the message");}
        finally{busy=false;view.SetBusy(false);requestCancel?.Dispose();requestCancel=null;}
    }
    private void SwitchWorkspace(string next)
    {
        if(busy||mode==next)return;
        sessions[mode].Prompt=view.Prompt;ClearPreview();mode=next;
        view.Prompt=sessions[mode].Prompt;view.SetAttachment(sessions[mode].ImageName);view.RestoreConversation(history);
        view.SetStatus(next=="analyze"?"Read-only workspace · choose an audit focus":"Independent workspace · ready");
    }
    private ModelContext ScopedContext(RhinoDoc doc)
    {
        var context=ContextReader.Read(doc,lastCreated);
        if(mode=="generate")return context with{Objects=[],SelectedIds=[],LastCreatedIds=[]};
        var ids=(view.UseRecent?context.LastCreatedIds:context.SelectedIds).ToHashSet();
        return context with{Objects=context.Objects.Where(o=>ids.Contains(o.Id)).ToArray(),SelectedIds=view.UseRecent?[]:context.SelectedIds,LastCreatedIds=view.UseRecent?context.LastCreatedIds:[]};
    }
    private ActionPlan CreationOptions(ActionPlan plan)
    {
        if(string.IsNullOrWhiteSpace(view.LayerPrefix))throw new ArgumentException("Choose an output layer prefix.");
        return plan with{Actions=plan.Actions.Select(a=>
        {
            if(!a.Tool.StartsWith("create_",StringComparison.Ordinal)||a.Tool=="create_layer")return a;
            var parameters=System.Text.Json.Nodes.JsonNode.Parse(a.Parameters.GetRawText())!.AsObject();parameters["layer"]=view.LayerPrefix;
            if(a.Tool=="create_building")parameters["facade"]=view.Facade;
            return a with{Parameters=JsonSerializer.SerializeToElement(parameters)};
        }).ToList(),Summary=plan.Summary+$"\nWorkspace output: {view.LayerPrefix}; building facade: {view.Facade}."};
    }
    private void RunAudit()
    {
        if(busy||mode!="analyze")return;
        try{var doc=RhinoDoc.ActiveDoc??throw new InvalidOperationException("Open a document first.");AddMessage("assistant",ModelAudit.Report(doc,ScopedContext(doc),view.AuditFocus));view.SetStatus("Local audit complete · no geometry changed");}
        catch(Exception ex){AddMessage("assistant",ex.Message,true);}
    }
    private void QuickAssist()
    {
        if(busy||mode!="assist")return;
        try
        {
            var doc=RhinoDoc.ActiveDoc??throw new InvalidOperationException("Open a document first.");var context=ScopedContext(doc);
            if(context.Truncated||context.Objects.Length is <1 or >100)throw new ArgumentException("Choose between 1 and 100 complete targets in this scope.");
            var amount=view.Amount;var tool=view.AssistTool;var bounds=Rhino.Geometry.BoundingBox.Empty;
            foreach(var target in context.Objects){bounds.Union(new Rhino.Geometry.Point3d(target.Min[0],target.Min[1],target.Min[2]));bounds.Union(new Rhino.Geometry.Point3d(target.Max[0],target.Max[1],target.Max[2]));}
            var vector=new double[3];vector[view.Axis]=amount;
            var actions=context.Objects.Select(o=>tool switch{
                "move_object"=>ModelAction.Make(tool,new{object_id=o.Id,vector}),
                "scale_object"=>ModelAction.Make(tool,new{object_id=o.Id,factor=amount,center=new[]{bounds.Center.X,bounds.Center.Y,bounds.Center.Z}}),
                _=>ModelAction.Make(tool,new{object_id=o.Id,height=amount,cap=true})}).ToList();
            SetPreview(doc,new ActionPlan{Summary=$"{tool.Replace('_',' ')} · {actions.Count} scoped targets · value {amount} {(tool=="scale_object"?"(factor)":doc.ModelUnitSystem)}",LengthUnit="document",Actions=actions});
        }
        catch(Exception ex){AddMessage("assistant",ex.Message,true);view.SetStatus("Review the target scope and operation");}
    }
    public void SetPreview(RhinoDoc doc,ActionPlan plan)
    {
        WorkspacePolicy.Validate(mode,plan,ScopedContext(doc));
        PreparePreview(doc,plan);
    }
    private void PreparePreview(RhinoDoc doc,ActionPlan plan)
    {
        ClearPreview();pending=GeometryEngine.Prepare(doc,plan);conduit=new PlanConduit(doc,pending.Preview){Enabled=true};
        if(preferences.ZoomPreview&&pending.Preview.Any())
        {var bounds=Rhino.Geometry.BoundingBox.Empty;foreach(var geometry in pending.Preview)bounds.Union(geometry.GetBoundingBox(true));if(bounds.IsValid)doc.Views.ActiveView?.ActiveViewport.ZoomBoundingBox(bounds);}
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
            if(lastCreated.Count==0&&report.Modified.Count>0)lastCreated.AddRange(report.Modified);
            var resultObjects=lastCreated.Select(id=>doc.Objects.FindId(id)).Where(o=>o is not null&&!o.IsDeleted).Select(o=>ContextReader.Info(doc,o!)).ToArray();
            documentState.Revision++;documentState.LastReport=ExecutionFeedback.Describe(documentState.Revision,report.Created.Count,report.Modified.Count,resultObjects,doc.ModelUnitSystem.ToString(),documentState.Persian);
            RememberGeometry(doc);
            AddMessage("assistant",documentState.LastReport+(report.Properties.Count>0?"\n"+JsonSerializer.Serialize(report.Properties,Wire.Json):""));
            ClearPreview();view.Prompt="";view.SetStatus("Applied · confirmed by Rhino · describe your next edit");RefreshContext();
        }
        catch(Exception ex){if(pendingMcpId is not null)mcpResults[pendingMcpId]=new{status="failed",message=ex.Message};AddMessage("assistant","Could not apply: "+ex.Message,true);ClearPreview();view.SetStatus("Apply failed · review the message");}
    }
    private void AttachImage()
    {
        if(busy)return;if(imageData is not null){imageData=null;sessions[mode].ImageName=null;view.SetAttachment(null);ClearPreview();return;}
        using var dialog=new OpenFileDialog{Title="Add an architectural reference"};dialog.Filters.Add(new FileFilter("Reference images",".png",".jpg",".jpeg"));
        if(dialog.ShowDialog(this)!=DialogResult.Ok)return;
        var file=new FileInfo(dialog.FileName);if(file.Length>4_000_000){view.SetStatus("Reference must be smaller than 4 MB.");return;}
        var mime=file.Extension.ToLowerInvariant() is ".jpg" or ".jpeg"?"image/jpeg":"image/png";
        imageData=$"data:{mime};base64,"+Convert.ToBase64String(File.ReadAllBytes(file.FullName));sessions[mode].ImageName=file.Name;view.SetAttachment(file.Name);ClearPreview();
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
                var plan=Wire.Read<ActionPlan>(args.GetProperty("plan").GetRawText());
                var targetMode=plan.Actions.Any(a=>a.Tool is "move_object" or "scale_object" or "select_objects" or "get_object_properties")?"assist":"generate";
                WorkspacePolicy.Validate(targetMode,plan,ContextReader.Read(doc,lastCreated));
                SwitchWorkspace(targetMode);view.SetWorkspace(targetMode,false);PreparePreview(doc,plan);pendingMcpId=Guid.NewGuid().ToString();
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
