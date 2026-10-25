using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using ArchiTec.Core;
using ArchiTec.RhinoPlugin;
using ArchiTec.RhinoPlugin.Views;
using Rhino;
using Rhino.DocObjects;
using Rhino.FileIO;
using Rhino.Geometry;

namespace ArchiTec.RuntimeTests;

/// <summary>
/// Explicitly scheduled live OpenRouter acceptance in a disposable, empty interactive Rhino document.
/// Uses the real panel connection, conversation, preview and Apply flow. Never substitutes a mock,
/// changes network routing or writes credentials/provider response text to the report.
/// </summary>
public static class LiveProviderAcceptance
{
    private static int running;
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;

    public static void Schedule(string outputDirectory,string modelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        if(string.IsNullOrWhiteSpace(modelId)||modelId.Length>200||modelId.StartsWith("sk-",StringComparison.OrdinalIgnoreCase)||!Regex.IsMatch(modelId,@"^[A-Za-z0-9][A-Za-z0-9_./:+-]*$"))
            throw new ArgumentException("Supply the exact public catalog model ID explicitly.",nameof(modelId));
        if(Interlocked.CompareExchange(ref running,1,0)!=0)throw new InvalidOperationException("A live provider acceptance run is already scheduled.");
        var output=Path.GetFullPath(outputDirectory);
        EventHandler? idle=null;
        idle=async (_,_)=>
        {
            var active=RhinoDoc.ActiveDoc;if(active is null||active.UndoRecordingIsActive)return;
            RhinoApp.Idle-=idle;
            try{await Run(active,output,modelId);}
            finally{Interlocked.Exchange(ref running,0);}
        };
        RhinoApp.Idle+=idle;
    }

    private static async Task Run(RhinoDoc doc,string output,string modelId)
    {
        var watch=Stopwatch.StartNew();var started=DateTimeOffset.UtcNow;
        var steps=new List<object>();var status="running";var stage="preflight";string? failure=null;
        string secret="";ArchiTecPanel? panel=null;ConnectionSettings? original=null;bool selectedWorkingModel=false,profileSelected=false;
        Directory.CreateDirectory(output);
        void Save()=>AtomicReport(Path.Combine(output,"live-provider-acceptance.json"),new
        {
            status,stage,started_utc=started,elapsed_ms=watch.ElapsedMilliseconds,provider="OpenRouter",
            requested_model_id=modelId,actual_model_id=selectedWorkingModel?modelId:null,plugin_version=McpCatalog.Version,live_api=true,mock_api=false,
            custom_transport=false,raw_provider_messages_saved=false,steps,error=failure
        });
        void Step(string name,object? measurements=null)
        {
            Assert(RhinoDoc.ActiveDoc?.RuntimeSerialNumber==doc.RuntimeSerialNumber,"The active Rhino document changed; acceptance stopped.");
            steps.Add(new{name,elapsed_ms=watch.ElapsedMilliseconds,measurements});Save();RhinoApp.WriteLine("PASS "+name);
        }
        PreparedPlan? Pending()=>panel is null?null:(PreparedPlan?)Field("pending").GetValue(panel);
        try
        {
            Save();
            Assert(!RhinoApp.IsRunningHeadless&&RhinoApp.MainWindowHandle()!=IntPtr.Zero&&doc.Views.ActiveView is not null,"Use an interactive visible Rhino session with an active viewport.");
            Assert(Objects(doc).Length==0,"Live provider acceptance requires an empty disposable Rhino document, including hidden and locked geometry.");
            Assert(doc.ModelUnitSystem is not (UnitSystem.None or UnitSystem.Unset),"Choose real document length units before live acceptance.");
            var scale=RhinoMath.UnitScale(UnitSystem.Meters,doc.ModelUnitSystem);
            Assert(double.IsFinite(scale)&&scale>0,"Document units cannot be converted to meters.");
            var tolerance=Math.Max(doc.ModelAbsoluteTolerance*10,1e-6*scale);
            panel=ArchiTecPanel.Current??throw new InvalidOperationException("Open the installed Archi-Tec panel first.");
            Assert(!(bool)Field("busy").GetValue(panel)!,"The Archi-Tec panel is busy; do not overlap requests.");
            var view=(StudioView)Field("view").GetValue(panel)!;
            original=panel.CurrentSettings;
            var profiles=SettingsStore.LoadProfiles();
            Assert(profiles.TryGetValue("OpenRouter",out var saved)&&saved.RememberKey&&saved.ApiKey.Length>0,"A remembered OpenRouter API profile is required; no credential was written to this report.");
            secret=saved!.ApiKey;
            Assert(panel.TryUpdateSettings(saved with{Model=modelId}),"The saved API profile could not be selected.");
            profileSelected=true;
            view.SelectApiRoute();((ComboBox)view.FindName("TargetSelector")).SelectedIndex=0;
            await Invoke(panel,"SwitchWorkspace","generate");
            Step("Empty interactive document and remembered OpenRouter profile selected",new{units=doc.ModelUnitSystem.ToString(),meters_to_document_scale=scale});

            stage="saved-profile-block";Save();
            Assert(Field("provider").GetValue(panel) is null,"The preconnection check unexpectedly has an active provider.");
            var before=History(panel).Count;
            view.Prompt="سلام، فقط یک پاسخ کوتاه بده و هیچ هندسه‌ای نساز.";
            await panel.SendAsync();
            var blocked=History(panel).Skip(before).LastOrDefault(m=>m.Role=="assistant")?.Content??"";
            Assert(blocked.Contains("saved",StringComparison.OrdinalIgnoreCase)&&blocked.Contains("not connected",StringComparison.OrdinalIgnoreCase),"A saved but disconnected API profile did not report its connection requirement.");
            Assert(Pending() is null&&Objects(doc).Length==0&&Field("provider").GetValue(panel) is null,"The preconnection check must not create a plan, geometry or a provider.");
            Step("Saved API profile refuses silent offline fallback before Connect",new{geometry_count=0,pending_plan=false,cloud_request_sent=false});

            stage="connect";Save();var timer=Stopwatch.StartNew();
            await Invoke(panel,"ConnectAsync");
            var client=Field("provider").GetValue(panel);
            if(client is null)
            {
                var connectionError=(string?)Field("apiConnectionError").GetValue(panel);
                throw new InvalidOperationException(connectionError??"The real API connection was not established.");
            }
            Assert(client.GetType()==typeof(ProviderClient),"The live OpenRouter connection is not using the real provider adapter.");
            var selector=(ComboBox)view.FindName("ModelSelector");
            var exact=selector.Items.Cast<ModelDescriptor>().SingleOrDefault(m=>m.Id==modelId);
            Assert(exact is not null,"The explicitly requested model is absent from the compatible catalog; no replacement was selected.");
            selector.SelectedItem=exact;
            Assert(view.SelectedModel?.Id==modelId&&panel.CurrentSettings.Model==modelId,"The exact requested model was not persisted.");
            selectedWorkingModel=true;
            Step("Real API authentication and model discovery completed",new{latency_ms=timer.ElapsedMilliseconds,compatible_models=selector.Items.Count,selected_model=modelId});

            // Start the connected conversation without the deliberately blocked preconnection turn.
            History(panel).Clear();view.RestoreConversation(History(panel));
            stage="greeting";Save();timer.Restart();
            view.Prompt="سلام. خودت را به فارسی در یک جمله معرفی کن. فقط گفتگو کن؛ هیچ عملیات یا هندسه‌ای پیشنهاد نده.";
            await panel.SendAsync();
            Assert(view.SelectedModel?.Id==modelId,"The selected model changed during the live greeting.");
            Assert(((TextBlock)view.FindName("StatusLabel")).Text.StartsWith("Response received",StringComparison.Ordinal),"The real greeting did not complete as a conversation-only response.");
            var greeting=History(panel).LastOrDefault(m=>m.Role=="assistant")?.Content??"";
            Assert(greeting.Length>0&&ConversationIntent.IsPersian(greeting)&&Pending() is null&&Objects(doc).Length==0,"The Persian greeting must return text without a plan or geometry.");
            Step("Persian live API conversation returns no modeling plan",new{latency_ms=timer.ElapsedMilliseconds,assistant_characters=greeting.Length,persian=true,geometry_count=0,pending_plan=false});

            stage="box-preview";Save();timer.Restart();
            view.Prompt="Create exactly one native solid box using create_box: width 2 meters along X, depth 4 meters along Y, height 3 meters along Z, origin [0,0,0]. No other objects. Return the structured modeling plan.";
            await panel.SendAsync();
            var proposed=Pending();
            Assert(proposed is not null&&proposed.Plan.Actions.Count==1&&proposed.Plan.Actions[0].Tool=="create_box","The live model did not propose the requested single native create_box action.");
            Assert(Objects(doc).Length==0,"The API proposal changed document geometry before Apply.");
            Step("Live API box request produces a preview without committing geometry",new{latency_ms=timer.ElapsedMilliseconds,pending_actions=proposed!.Plan.Actions.Count,geometry_count=0});

            stage="box-apply";Save();timer.Restart();panel.ApplyPending();
            var created=Objects(doc);
            Assert(created.Length==1&&Pending() is null,"Apply did not commit exactly one native box.");
            var originalId=created[0].Id;
            Assert(created[0].Geometry is Brep brep&&brep.IsValid&&brep.IsSolid,"The API result is not a valid closed native Brep.");
            var first=created[0].Geometry.GetBoundingBox(true);
            Assert(Near(first.Min,Point3d.Origin,tolerance)&&Near(first.Max,new Point3d(2*scale,4*scale,3*scale),tolerance),"The created box does not match 2 x 4 x 3 meters at the origin.");
            using(var volume=VolumeMassProperties.Compute((Brep)created[0].Geometry))
                Assert(volume is not null&&Math.Abs(volume.Volume/Math.Pow(scale,3)-24)<1e-4,"The box volume is not 24 cubic meters.");
            Step("Apply creates a valid closed native 2 x 4 x 3 meter solid",new{latency_ms=timer.ElapsedMilliseconds,object_id=originalId,dimensions_m=new[]{2,4,3},volume_m3=24,valid=true,solid=true});

            stage="followup-preview";Save();timer.Restart();
            view.Prompt="Move this same existing box exactly 3 meters in the positive world X direction. Preserve its object ID and dimensions. Use move_object, and do not create a replacement or additional geometry.";
            await panel.SendAsync();proposed=Pending();
            Assert(proposed is not null&&proposed.Plan.Actions.Count==1&&proposed.Plan.Actions[0].Tool=="move_object"&&proposed.Plan.Actions[0].Parameters.GetProperty("object_id").GetString()==originalId.ToString(),"The follow-up did not propose a scoped move of the existing object ID.");
            Assert(Objects(doc).Length==1&&Near(doc.Objects.FindId(originalId)!.Geometry.GetBoundingBox(true).Min,first.Min,tolerance),"The follow-up proposal moved geometry before Apply.");
            Step("Live API follow-up targets the original object and waits for Apply",new{latency_ms=timer.ElapsedMilliseconds,object_id=originalId,geometry_count=1});

            stage="followup-apply";Save();timer.Restart();panel.ApplyPending();
            var moved=Objects(doc);
            Assert(moved.Length==1&&moved[0].Id==originalId&&Pending() is null,"Follow-up Apply replaced or duplicated the original object.");
            var final=moved[0].Geometry.GetBoundingBox(true);
            Assert(Near(final.Min,new Point3d(3*scale,0,0),tolerance)&&Near(final.Max,new Point3d(5*scale,4*scale,3*scale),tolerance),"The follow-up result does not measure a 3 meter positive X translation.");
            Assert(moved[0].Geometry is Brep finalBrep&&finalBrep.IsValid&&finalBrep.IsSolid,"The moved result is not a valid closed solid.");
            Step("Follow-up Apply preserves the native object ID and measured dimensions",new{latency_ms=timer.ElapsedMilliseconds,object_id=originalId,min_m=new[]{3,0,0},max_m=new[]{5,4,3},translation_m=new[]{3,0,0},geometry_count=1});

            stage="fixture";Save();
            var fixture=Path.Combine(output,"Live-API-Box-Moved.3dm");
            using(var options=new FileWriteOptions{SuppressDialogBoxes=true,UpdateDocumentPath=false,WriteSelectedObjectsOnly=false,FileVersion=8})
                Assert(doc.Write3dmFile(fixture,options),"The tested native result could not be saved as a 3DM fixture.");
            using(var reopened=File3dm.Read(fixture))Assert(reopened is not null&&reopened.Objects.Count==1,"The saved 3DM fixture did not reopen with the single tested object.");
            Step("Actual API-created native result saved and reopened",new{file="Live-API-Box-Moved.3dm",geometry_count=1});
            status="passed";stage="complete";Save();doc.Views.Redraw();
        }
        catch(Exception error)
        {
            while(error is TargetInvocationException{InnerException:not null} reflection)error=reflection.InnerException!;
            status="failed";failure=ConnectionDiagnostics.Describe(error);
            if(secret.Length>0)failure=failure.Replace(secret,"[redacted]",StringComparison.Ordinal);
            failure=Regex.Replace(failure,@"sk-[A-Za-z0-9_-]+","[redacted]",RegexOptions.IgnoreCase);
            if(failure.Length>1000)failure="Live API acceptance stopped. Review the last completed stage and the in-Rhino connection status.";
            Save();RhinoApp.WriteLine("Live API acceptance stopped at "+stage+". See the sanitized report.");
            panel?.ClearPreview();
            // Keep the real failed connection and its actionable UI state. Restore only a partial
            // preflight settings change, before a profile was successfully selected or connected.
            if(!profileSelected&&panel is not null&&original is not null&&panel.CurrentSettings!=original)panel.TryUpdateSettings(original);
        }
    }
    private static FieldInfo Field(string name)=>typeof(ArchiTecPanel).GetField(name,Private)??throw new InvalidOperationException("Runtime acceptance field is unavailable: "+name);
    private static List<ChatMessage> History(ArchiTecPanel panel)=>(List<ChatMessage>)(typeof(ArchiTecPanel).GetProperty("history",Private)?.GetValue(panel)??throw new InvalidOperationException("Runtime conversation history is unavailable."));
    private static async Task Invoke(ArchiTecPanel panel,string name,params object[] args)
    {
        var result=(typeof(ArchiTecPanel).GetMethod(name,Private)??throw new InvalidOperationException("Runtime acceptance method is unavailable: "+name)).Invoke(panel,args);
        if(result is Task task)await task;
    }
    private static RhinoObject[] Objects(RhinoDoc doc)=>doc.Objects.GetObjectList(new ObjectEnumeratorSettings{NormalObjects=true,LockedObjects=true,HiddenObjects=true,ReferenceObjects=true,ObjectTypeFilter=ObjectType.AnyObject}).Where(o=>!o.IsDeleted).ToArray();
    private static bool Near(Point3d actual,Point3d expected,double tolerance)=>actual.DistanceTo(expected)<=tolerance;
    private static void Assert(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    private static void AtomicReport(string path,object report)
    {
        var temporary=path+".tmp";File.WriteAllText(temporary,JsonSerializer.Serialize(report,Wire.Json));File.Move(temporary,path,true);
    }
}
