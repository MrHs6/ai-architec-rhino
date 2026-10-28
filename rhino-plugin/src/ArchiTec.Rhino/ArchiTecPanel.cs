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
    private bool grasshopperTarget;
    private WorkspaceSessions sessions=>grasshopperTarget?documentState.GrasshopperSessions:documentState.Sessions;
    private List<ChatMessage> history=>sessions[mode].History;
    private List<Guid> lastCreated=>documentState.RecentIds;
    private readonly Dictionary<string,object> mcpResults=[];
    private readonly McpOperationLedger mcpOperations=new();
    private ConnectionSettings settings=new();
    private IPlannerClient? provider;
    private string? apiConnectionError;
    private bool explicitOffline;
    private CancellationTokenSource? requestCancel;
    private PreparedPlan? pending;
    private PlanConduit? conduit;
    private McpServer? mcp;
    private int mcpPort;
    private readonly string mcpOwner=Guid.NewGuid().ToString("N");
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
        apiConnectionError=null;explicitOffline=false;
        view.LoadSettings(value);view.SetApiDisconnected(value.Source);ClearPreview();
        view.SetStatus("Connection settings saved. Open Settings to load models and connect.");
        return true;
    }
    public ArchiTecPanel()
    {
        Current=this;Padding=0;MinimumSize=new Eto.Drawing.Size(350,540);
        view=new StudioView();Content=NativeViewHost.Wrap(view);
        try{settings=SettingsStore.Load();view.LoadProfiles(SettingsStore.LoadProfiles());preferences=SettingsStore.LoadPreferences();}catch{view.SetStatus("Saved settings could not be read. Open API profiles to reconnect.");}
        view.LoadSettings(settings);
        if(settings.ApiKey.Length>0)view.SetApiDisconnected(settings.Source);
        view.LoadPreferences(preferences);view.SetWorkspace(preferences.DefaultWorkspace,false);mode=preferences.DefaultWorkspace;
        view.SendRequested+=()=>_ = SendAsync();view.ApplyRequested+=ApplyPending;
        view.RouteChanged+=()=>{requestCancel?.Cancel();ClearPreview();view.RestoreConversation(view.ChatViaMcp?documentState.McpLog:history);};
        view.TargetChanged+=()=>
        {
            sessions[mode].Prompt=view.Prompt;ClearPreview();grasshopperTarget=view.GrasshopperTarget;
            if(grasshopperTarget){mode="generate";view.SetWorkspace(mode,false);}else view.SetWorkspace(mode,false);
            view.Prompt=sessions[mode].Prompt;view.SetAttachment(sessions[mode].ImageName);view.RestoreConversation(history);
        };
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
        view.OfflineRequested+=()=>{if(busy)return;requestCancel?.Cancel();provider?.Dispose();provider=null;apiConnectionError=null;explicitOffline=true;view.SetOffline();view.CloseConnections();ClearPreview();};
        view.McpStartRequested+=()=>{if(mcp is null){try{var port=view.RequestedMcpPort;StartMcp(port);preferences=preferences with{McpPort=port};SettingsStore.SavePreferences(preferences);}catch(Exception ex){view.McpMessage("MCP: "+ex.Message);}}else StopMcp();};
        view.CopyMcpRequested+=CopyMcpConfiguration;
        view.SetupMcpClientRequested+=()=>_ = SetupMcpClientAsync();
        view.OpenMcpClientRequested+=OpenMcpClient;
        view.CopyMcpTestRequested+=()=>{System.Windows.Clipboard.SetText("Use only the ai-architec-rhino MCP server (not rhino-mcp-router). First call get_context, then use post_message with that document_id to confirm the connection in the Rhino panel. Do not create or modify geometry for this connection test.");view.McpMessage("Connection-test prompt copied. Send it in the client chat with ai-architec-rhino tools enabled.");};
        view.OpenChatRequested+=()=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://chatgpt.com"){UseShellExecute=true});
        contextTimer.Elapsed+=(_,_)=>RefreshContext();
        // Eto unloads hidden/reparented panels. Visibility is not a Stop command:
        // the listener, operation receipts and document targets must survive it.
        Load+=(_,_)=>{contextTimer.Start();RefreshContext();};
        UnLoad+=(_,_)=>contextTimer.Stop();
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
        provider?.Dispose();provider=null;apiConnectionError=null;explicitOffline=false;view.SetApiConnecting(settings.Source);
        busy=true;requestCancel=new CancellationTokenSource(TimeSpan.FromSeconds(30));view.SetBusy(true);view.ApiMessage("Profile saved. Connecting and discovering models…");
        var source=settings.Source;var endpoint=settings.Endpoint;var key=settings.ApiKey;var remember=settings.RememberKey;
        try
        {
            var candidate=ProviderProfiles.Create(source,endpoint,key);
            ModelCatalog catalog;
            try{catalog=await candidate.Models(requestCancel.Token);requestCancel.Token.ThrowIfCancellationRequested();}
            catch{candidate.Dispose();throw;}
            if(disposed){candidate.Dispose();return;}
            if(catalog.Models.Count==0){candidate.Dispose();throw new InvalidOperationException("No models with the required capabilities were found.");}
            provider?.Dispose();provider=candidate;
            settings=new ConnectionSettings(source,endpoint,key,remember,view.DraftSettings().Model);
            view.SetCatalog(catalog.Models,settings.Model,settings.Source);PersistSettings();ClearPreview();
            view.SelectApiRoute();view.ApiMessage($"Connected · {catalog.Models.Count} models. Choose a model, save the profile and return to your workspace.");view.SetStatus("Connected · selected model will be used exactly");
        }
        catch(Exception ex){provider?.Dispose();provider=null;apiConnectionError=ConnectionDiagnostics.Describe(ex);view.SetApiConnectionError(source,apiConnectionError);view.SetStatus("API connection failed · open API profiles for details");}
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
            {provider?.Dispose();provider=null;apiConnectionError=null;explicitOffline=false;view.SetApiDisconnected(draft.Source);ClearPreview();}
            settings=draft;view.ProfileSaved(draft);
            view.ApiMessage(provider is null?"Profile saved, not connected. Choose Connect and discover models; no modeling request has been sent.":"Connected profile saved.");
            return true;
        }
        catch(Exception ex){view.ProfileSaveError(ex.Message);return false;}
    }
    private void RefreshContext()
    {
        if(mcp is not null)view.SetMcpConnection(mcp.Connection.Snapshot());
        var doc=RhinoDoc.ActiveDoc;if(doc is null){view.SetContext("Open a Rhino document to begin");ClearPreview();return;}
        if(!BindDocument(doc))return;
        if(!busy&&!grasshopperTarget&&lastCreated.Count>0)RefreshRecentGeometry(doc);
        view.SetContext($"●  {doc.ModelUnitSystem}   ·   {doc.Objects.GetObjectList(Rhino.DocObjects.ObjectType.AnyObject).Count()} objects   ·   {doc.Objects.GetSelectedObjects(false,false).Count()} selected");
        if(lastCreated.Count>0){var existing=lastCreated.Count(id=>doc.Objects.FindId(id) is {} o&&!o.IsDeleted);view.SetRecentResult(existing>0?$"Recent result · {existing} objects · revision {documentState.Revision}":"Recent result was removed or undone",existing>0);}
        if(pending is not null)try{GeometryEngine.CheckCurrent(doc,pending);}catch{ClearPreview();view.SetStatus("Model changed · generate a fresh preview");}
    }
    private bool BindDocument(RhinoDoc doc)
    {
        if(boundDocumentId==doc.RuntimeSerialNumber)return true;
        if(busy){requestCancel?.Cancel();return false;}
        sessions[mode].Prompt=view.Prompt;ClearPreview();boundDocumentId=doc.RuntimeSerialNumber;documentState=documents.For(boundDocumentId);
        view.Prompt=sessions[mode].Prompt;view.RestoreConversation(view.ChatViaMcp?documentState.McpLog:history);view.SetAttachment(sessions[mode].ImageName);view.SetRecentResult(null);
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
        documentState.LastReport=lead+(actual.Length==0?(documentState.Persian?"نتیجه دیگر در سند وجود ندارد. برای ادامه یک نتیجهٔ تازه بسازید.":"No recent targets remain in this document. Generate a fresh result to continue."):ExecutionFeedback.Describe(documentState.Revision,0,modified,actual.Select(o=>ContextReader.Info(doc,o!)).ToArray(),doc.ModelUnitSystem.ToString(),documentState.Persian,view.ChatViaMcp));
        RememberGeometry(doc);if(view.ChatViaMcp)AddMcpMessage(documentState.LastReport);else AddMessage("assistant",documentState.LastReport);
    }
    private void RefineRecent()
    {
        if(busy)return;var doc=RhinoDoc.ActiveDoc;if(doc is null||!BindDocument(doc))return;
        if(!lastCreated.Any(id=>doc.Objects.FindId(id) is {} o&&!o.IsDeleted)){view.SetStatus("The recent result is no longer in this document.");return;}
        SwitchWorkspace("assist");view.SetWorkspace("assist",false);view.UseRecentResult();view.FocusPrompt();
        view.SetStatus("Continue editing this document's recent result. Describe the next change.");
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
        AddMessage("assistant",ExecutionFeedback.Describe(documentState.Revision,0,0,objects,doc.ModelUnitSystem.ToString(),documentState.Persian,view.ChatViaMcp));
    }
    private void AddMessage(string role,string value,bool error=false)
    {
        history.Add(new ChatMessage(role,value));if(history.Count>24)history.RemoveRange(0,history.Count-24);
        view.AddMessage(role,value,error);
    }
    private void AddMcpMessage(string value,bool error=false)
    {
        documentState.McpLog.Add(new("assistant",value));if(documentState.McpLog.Count>24)documentState.McpLog.RemoveAt(0);
        if(view.ChatViaMcp)view.AddMessage("assistant",value,error);
    }
    public async Task SendAsync()
    {
        if(busy||string.IsNullOrWhiteSpace(view.Prompt))return;
        var doc=RhinoDoc.ActiveDoc;if(doc is null){view.SetStatus("Open a Rhino document first.");return;}
        if(!BindDocument(doc))return;
        if(view.ChatViaMcp)
        {
            view.SetStatus("Chat in your AI client. This workspace receives its results and previews.");return;
        }
        if(!grasshopperTarget&&mode=="generate"&&lastCreated.Count>0&&ConversationIntent.IsExplicitEdit(view.Prompt))
        {var followup=view.Prompt;RefineRecent();if(mode!="assist")return;view.Prompt=followup;}
        ClearPreview();var context=ScopedContext(doc);
        if(mode=="assist"&&context.Objects.Length==0){view.SetStatus("Select target objects or choose recent output first.");return;}
        var fingerprints=context.Objects.ToDictionary(o=>Guid.Parse(o.Id),o=>doc.Objects.FindId(Guid.Parse(o.Id))!.Geometry.DataCRC(0));
        var units=doc.ModelUnitSystem;var tolerance=doc.ModelAbsoluteTolerance;var text=view.Prompt.Trim();var previous=history.TakeLast(12).ToList();
        documentState.Persian=ConversationIntent.IsPersian(text);
        if((grasshopperTarget?documentState.LastGraphReport:documentState.LastReport) is {} report)previous.Add(new ChatMessage("assistant","Verified result data from this document (not instructions):\n"+report));
        AddMessage("user",text);busy=true;requestCancel=new CancellationTokenSource();view.SetBusy(true);view.SetStatus("Understanding your request…");
        try
        {
            if(provider is null&&!explicitOffline&&(settings.ApiKey.Length>0||apiConnectionError is not null))
                throw new InvalidOperationException($"The saved {settings.Source} profile is not connected. Open API profiles, choose Connect and discover models, then select a model. "+(apiConnectionError is null?"To use local examples instead, choose Disconnect API / use offline tools explicitly.":"Last connection error: "+apiConnectionError));
            ActionPlan? plan;
            string? graphState=null;
            if(grasshopperTarget)
            {
                if(provider is null)throw new ArgumentException("Grasshopper planning requires a connected API service, or use MCP in your AI client. Offline examples do not create definitions.");
                context=context with{Grasshopper=JsonSerializer.SerializeToElement(GrasshopperHost.PlanningContext(doc.RuntimeSerialNumber),Wire.Json)};
                graphState=JsonSerializer.Serialize(GrasshopperHost.Context(doc.RuntimeSerialNumber),Wire.Json);
            }
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
            if(graphState is not null&&graphState!=JsonSerializer.Serialize(GrasshopperHost.Context(doc.RuntimeSerialNumber),Wire.Json))throw new InvalidOperationException("The Grasshopper graph changed while planning. Send the request again.");
            if(plan is null){view.SetStatus("Response received · no model changes proposed");return;}
            if(mode=="generate"&&!grasshopperTarget)plan=CreationOptions(plan);
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
        if(grasshopperTarget)return context with{Grasshopper=new{target="grasshopper"}};
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
        view.ShowPreview(plan,doc.ModelUnitSystem.ToString());view.SetStatus(view.ChatViaMcp?"Optional preview · authorize apply_plan in your AI client":"Preview in Rhino · ready for your Apply");doc.Views.Redraw();
    }
    public void ClearPreview()
    {
        if(pendingMcpId is not null)
        {
            if(mcpResults.TryGetValue(pendingMcpId,out var value)&&JsonSerializer.SerializeToElement(value).GetProperty("status").GetString()=="awaiting_approval")
            {mcpResults[pendingMcpId]=new{status="cancelled",message="Preview cancelled or invalidated."};mcp?.Connection.Workflow("cancelled","Preview cancelled or invalidated");}
            pendingMcpId=null;
        }
        if(conduit is not null){conduit.Enabled=false;conduit=null;}pending?.Dispose();pending=null;view.HidePreview();RhinoDoc.ActiveDoc?.Views.Redraw();
    }
    public void ApplyPending()=>ApplyPendingCore(default);
    private void ApplyPendingCore(CancellationToken cancellation)
    {
        if(pending is null)return;
        try
        {
            var doc=RhinoDoc.ActiveDoc??throw new InvalidOperationException("No active document.");var report=GeometryEngine.ApplyCancellable(doc,pending,cancellation);
            if(pendingMcpId is not null)mcpResults[pendingMcpId]=new{status="applied",created=report.Created,modified=report.Modified,properties=report.Properties,undo_serial=report.UndoSerial,grasshopper=report.Grasshopper};
            if(pendingMcpId is not null)mcp?.Connection.Workflow("applied","Confirmed native Rhino execution");
            if(report.Created.Count>0){lastCreated.Clear();lastCreated.AddRange(report.Created);}
            if(lastCreated.Count==0&&report.Modified.Count>0)lastCreated.AddRange(report.Modified);
            var resultObjects=lastCreated.Select(id=>doc.Objects.FindId(id)).Where(o=>o is not null&&!o.IsDeleted).Select(o=>ContextReader.Info(doc,o!)).ToArray();
            documentState.Revision++;
            var resultReport=report.Grasshopper is null?ExecutionFeedback.Describe(documentState.Revision,report.Created.Count,report.Modified.Count,resultObjects,doc.ModelUnitSystem.ToString(),documentState.Persian,view.ChatViaMcp):"CONFIRMED IN GRASSHOPPER\n"+JsonSerializer.Serialize(report.Grasshopper,Wire.Json);
            if(report.Grasshopper is null)documentState.LastReport=resultReport;else documentState.LastGraphReport=resultReport;
            RememberGeometry(doc);
            if(pendingMcpId is not null)AddMcpMessage($"Native execution completed · {report.Created.Count} created · {report.Modified.Count} modified. Continue in your AI client. Rhino Undo is available.");
            else AddMessage("assistant",resultReport+(report.Properties.Count>0?"\n"+JsonSerializer.Serialize(report.Properties,Wire.Json):""));
            ClearPreview();if(!view.ChatViaMcp)view.Prompt="";view.SetStatus(report.Grasshopper is not null?"Definition applied · graph Undo in Grasshopper · continue in your AI conversation":view.ChatViaMcp?"Client-controlled execution complete · Rhino Undo available":"Applied · confirmed by Rhino · describe your next edit");RefreshContext();
        }
        catch(Exception ex){if(pendingMcpId is not null){mcpResults[pendingMcpId]=new{status=cancellation.IsCancellationRequested?"cancelled":"failed",message=ex.Message};mcp?.Connection.Workflow("error","Native execution stopped");AddMcpMessage("Could not apply: "+ex.Message);}else AddMessage("assistant","Could not apply: "+ex.Message,true);ClearPreview();view.SetStatus("Apply failed · review the message");}
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
        if(mcp is not null)return;var candidate=new McpServer(port,HandleMcp);try{candidate.Start();McpSessionFile.Publish(port,candidate.Endpoint,candidate.Token,mcpOwner);mcp=candidate;mcpPort=port;view.SetMcp(true,mcp.Endpoint,mcp.Token);}catch{candidate.Dispose();throw;}
    }
    public void StopMcp(){mcp?.Dispose();mcp=null;if(mcpPort!=0)try{McpSessionFile.Remove(mcpPort,mcpOwner);}catch{}mcpPort=0;if(pendingMcpId is not null)ClearPreview();view.SetMcp(false);}
    private void CopyMcpConfiguration()
    {
        if(mcp is null){view.SetStatus("Start MCP first.");return;}
        var directory=Path.GetDirectoryName(typeof(ArchiTecPanel).Assembly.Location)!;
        System.Windows.Clipboard.SetText(McpClientSetup.Configuration(view.McpClient,Path.Combine(directory,"AIArchiTec.McpBridge.exe"),mcpPort));view.McpMessage("Configuration copied. Add it to your client's MCP settings once; this is not a chat prompt. No session token is included.");
    }
    private async Task SetupMcpClientAsync()
    {
        if(busy)return;var client=view.McpClient;view.SetMcpSetupBusy(true);
        try
        {
            if(mcp is null)StartMcp(view.RequestedMcpPort);
            preferences=preferences with{McpPort=mcpPort};SettingsStore.SavePreferences(preferences);
            if(client=="Other MCP client"){view.McpMessage("Connection enabled. Expand Manual setup and add its configuration to your client once.");return;}
            var directory=Path.GetDirectoryName(typeof(ArchiTecPanel).Assembly.Location)!;
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await McpClientInstaller.Register(client,Path.Combine(directory,"AIArchiTec.McpBridge.exe"),mcpPort,timeout.Token);
            view.McpMessage("Setup saved for "+client+". If it is already open, refresh its MCP tools once (Codex: Settings > Plugins > MCPs > refresh; Claude: quit and reopen). After this setup, switching Rhino MCP off and on reconnects automatically.");
            OpenMcpClient();
        }
        catch(Exception ex){view.McpMessage("Setup not completed. "+ex.Message);}
        finally{view.SetMcpSetupBusy(false);}
    }
    private void OpenMcpClient(){try{McpClientInstaller.Open(view.McpClient);}catch(Exception ex){view.McpMessage(ex.Message);}}
    private async Task<object> HandleMcp(string name,JsonElement args,CancellationToken token)
    {
        var completion=new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);using var registration=token.Register(()=>completion.TrySetCanceled(token));
        RhinoApp.InvokeOnUiThread((Action)(()=>
        {
            if(token.IsCancellationRequested||disposed)return;
            try
            {
                if(name=="get_capabilities"){completion.TrySetResult(McpCatalog.Capabilities);return;}
                if(name=="get_plan_status"){var id=args.GetProperty("request_id").GetString()??"";completion.TrySetResult(mcpOperations.Contains(id)?mcpOperations.Get(id):mcpResults.TryGetValue(id,out var result)?result:new{status="unknown"});return;}
                if(name=="cancel_plan")
                {var id=args.GetProperty("request_id").GetString()??"";if(pendingMcpId==id)ClearPreview();completion.TrySetResult(mcpResults.TryGetValue(id,out var result)?result:mcpOperations.Get(id));return;}
                var doc=RhinoDoc.ActiveDoc??throw new InvalidOperationException("No active Rhino document.");
                if(!BindDocument(doc))throw new InvalidOperationException("A request is active in another document. Retry after it finishes.");
                if(name=="get_context"){completion.TrySetResult(ContextReader.Read(doc,lastCreated));return;}
                if(args.GetProperty("document_id").GetUInt32()!=doc.RuntimeSerialNumber)throw new InvalidOperationException("The document changed. Read get_context again.");
                if(name=="get_objects"){completion.TrySetResult(McpDocumentTools.Inspect(doc,args));return;}
                if(name=="get_grasshopper_context"){completion.TrySetResult(GrasshopperHost.Context(doc.RuntimeSerialNumber));return;}
                if(name=="search_grasshopper_components"){completion.TrySetResult(GrasshopperHost.Search(args.GetProperty("query").GetString()??"",args.TryGetProperty("limit",out var limit)?limit.GetInt32():20));return;}
                if(name=="get_viewport_image"){completion.TrySetResult(McpDocumentTools.Capture(doc));return;}
                if(name=="set_view"){completion.TrySetResult(McpDocumentTools.Frame(doc,args,lastCreated));return;}
                if(name=="apply_plan")
                {
                    var id=args.GetProperty("request_id").GetString()??"";
                    if(pendingMcpId==id){token.ThrowIfCancellationRequested();ApplyPendingCore(token);}
                    completion.TrySetResult(mcpResults.TryGetValue(id,out var receipt)?receipt:new{status="unknown"});return;
                }
                if(name=="execute_plan")
                {
                    if(busy)throw new InvalidOperationException("An API request is active. Stop it before sending an MCP batch.");
                    if(pending is not null)throw new InvalidOperationException("An optional preview is active. Use apply_plan or cancel_plan in this client first.");
                    var id=args.GetProperty("request_id").GetString()??"";
                    var executionPlan=Wire.Read<ActionPlan>(args.GetProperty("plan").GetRawText());
                    if(!mcpOperations.Begin(id,doc.RuntimeSerialNumber,executionPlan,out var receipt)){completion.TrySetResult(receipt);return;}
                    try
                    {
                        token.ThrowIfCancellationRequested();mcp?.Connection.Workflow("working","Executing a client-authorized native batch");
                        using var prepared=GeometryEngine.Prepare(doc,executionPlan);token.ThrowIfCancellationRequested();
                        var report=GeometryEngine.ApplyCancellable(doc,prepared,token);
                        if(report.Created.Count>0){lastCreated.Clear();lastCreated.AddRange(report.Created);}
                        documentState.Revision++;RememberGeometry(doc);
                        receipt=new{request_id=id,status="applied",document_id=doc.RuntimeSerialNumber,created=report.Created,modified=report.Modified,properties=report.Properties,undo_serial=report.UndoSerial,grasshopper=report.Grasshopper,objects=report.Created.Concat(report.Modified).Distinct().Select(v=>doc.Objects.FindId(v)).Where(o=>o is not null&&!o.IsDeleted).Select(o=>ContextReader.Info(doc,o!)).ToArray()};
                        mcpOperations.Complete(id,receipt);mcp?.Connection.Workflow("applied","Native batch complete; continue in the AI client");
                        AddMcpMessage(report.Grasshopper is not null?$"{executionPlan.Summary}\nEditable Grasshopper definition solved · {report.Created.Count} baked objects. Inspect the returned graph receipt and continue in your AI client.":$"{executionPlan.Summary}\n{report.Created.Count} native objects created · {report.Modified.Count} modified · Rhino Undo {report.UndoSerial}. Continue in your AI client.");
                        view.SetStatus(report.Grasshopper is null?"Client-controlled execution complete · Rhino Undo available":"Definition solved · graph Undo in Grasshopper · continue in your AI client");completion.TrySetResult(receipt);
                    }
                    catch(Exception ex)
                    {
                        receipt=new{request_id=id,status=token.IsCancellationRequested?"cancelled":"failed",message=ex.Message};mcpOperations.Complete(id,receipt);
                        mcp?.Connection.Workflow(token.IsCancellationRequested?"cancelled":"error","Batch stopped; inspect its receipt");completion.TrySetResult(receipt);
                    }
                    return;
                }
                if(name=="post_message")
                {
                    if(busy)throw new InvalidOperationException("The panel is busy. Retry after the current request finishes.");
                    if(args.GetProperty("document_id").GetUInt32()!=doc.RuntimeSerialNumber)throw new InvalidOperationException("The document changed. Read get_context again.");
                    var target=args.TryGetProperty("workspace",out var workspace)?workspace.GetString():mode;
                    if(target is not ("generate" or "analyze" or "assist"))throw new ArgumentException("Unknown workspace.");
                    var message=args.GetProperty("message").GetString()??"";if(string.IsNullOrWhiteSpace(message)||message.Length>8000)throw new ArgumentException("Use a message of 1–8000 characters.");
                    AddMcpMessage(message);mcp?.Connection.Workflow("message","Client status delivered to execution log");completion.TrySetResult(new{status="displayed",document_id=doc.RuntimeSerialNumber});return;
                }
                if(busy||pending is not null)throw new InvalidOperationException("A request or optional preview is active. Use apply_plan or cancel_plan from this client first.");
                if(args.GetProperty("document_id").GetUInt32()!=doc.RuntimeSerialNumber)throw new InvalidOperationException("The document changed. Read get_context again.");
                var plan=Wire.Read<ActionPlan>(args.GetProperty("plan").GetRawText());
                PlanValidator.Validate(plan);
                PreparePreview(doc,plan);pendingMcpId=Guid.NewGuid().ToString();
                if(mcpResults.Count>=50)mcpResults.Remove(mcpResults.Keys.First());mcpResults[pendingMcpId]=new{status="awaiting_approval"};
                AddMcpMessage("Optional client preview · "+plan.Summary);
                completion.TrySetResult(new{request_id=pendingMcpId,status="awaiting_approval",message="Optional preview. Use apply_plan or cancel_plan in this AI client; no switch to Rhino is required."});
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
