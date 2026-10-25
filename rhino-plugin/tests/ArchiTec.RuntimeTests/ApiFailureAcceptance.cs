using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using ArchiTec.Core;
using ArchiTec.RhinoPlugin;
using ArchiTec.RhinoPlugin.Views;
using Rhino;
using Rhino.DocObjects;

namespace ArchiTec.RuntimeTests;

/// <summary>Exercises a real, already-failed API connection without sending any new cloud requests.</summary>
public static class ApiFailureAcceptance
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static int scheduled;

    public static void Schedule(string outputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        var output=Path.GetFullPath(outputDirectory);
        if(Interlocked.CompareExchange(ref scheduled,1,0)!=0)throw new InvalidOperationException("API failure acceptance is already scheduled.");
        EventHandler? idle=null;
        idle=async (_,_)=>
        {
            var doc=RhinoDoc.ActiveDoc;if(doc is null||doc.UndoRecordingIsActive)return;
            RhinoApp.Idle-=idle;
            try{await Run(doc,output);}
            finally{Interlocked.Exchange(ref scheduled,0);}
        };
        RhinoApp.Idle+=idle;
    }

    private static async Task Run(RhinoDoc doc,string output)
    {
        var timer=Stopwatch.StartNew();var steps=new List<object>();
        var status="running";var stage="preflight";string? error=null;
        ArchiTecPanel? panel=null;StudioView? view=null;string? originalFailure=null;string originalPrompt="";string secret="";
        bool restoreFailure=false,originalExplicitOffline=false;
        Directory.CreateDirectory(output);
        void Save()=>Atomic(Path.Combine(output,"api-failure-acceptance.json"),new
        {
            status,stage,plugin_version=McpCatalog.Version,elapsed_ms=timer.ElapsedMilliseconds,
            cloud_requests_sent_by_fixture=0,existing_failure_used=true,applied_geometry=false,steps,error
        });
        void Check(bool ok,string name,object? data=null)
        {
            if(!ok)throw new InvalidOperationException(name);
            AssertSameDocument(doc);steps.Add(new{name,elapsed_ms=timer.ElapsedMilliseconds,data});Save();RhinoApp.WriteLine("PASS "+name);
        }
        try
        {
            Save();
            if(RhinoApp.IsRunningHeadless||RhinoApp.MainWindowHandle()==IntPtr.Zero||doc.Views.ActiveView is null||Objects(doc).Length!=0)
                throw new FixtureUnavailableException("Use an empty disposable interactive Rhino document, including no hidden or locked geometry.");
            panel=ArchiTecPanel.Current??throw new FixtureUnavailableException("Open the installed Archi-Tec panel first.");
            view=(StudioView)Field("view").GetValue(panel)!;
            if((bool)Field("busy").GetValue(panel)!||Field("provider").GetValue(panel) is not null)
                throw new FixtureUnavailableException("Wait for an actual failed Connect with no active provider; this fixture does not force a failure.");
            originalFailure=(string?)Field("apiConnectionError").GetValue(panel);
            if(string.IsNullOrWhiteSpace(originalFailure))throw new FixtureUnavailableException("No actual connection error is available; run a real Connect first. This fixture sends no cloud request.");
            originalExplicitOffline=(bool)Field("explicitOffline").GetValue(panel)!;
            if(originalExplicitOffline)throw new FixtureUnavailableException("A real failed API connection is required before explicitly selecting offline tools.");
            originalPrompt=view.Prompt;secret=panel.CurrentSettings.ApiKey;restoreFailure=true;
            Check(true,"Real failed API connection is available",new{provider=panel.CurrentSettings.Source,active_provider=false,geometry_count=0});

            stage="view-states";Save();
            // A detached view exercises real UI handlers without changing the panel's catalog/profile.
            var isolated=new StudioView();
            isolated.SetCatalog([new("fixture/current","Fixture model",false)],"fixture/removed","OpenRouter");
            Check(isolated.SelectedModel is null,"Missing remembered model requires explicit selection instead of substituting the first catalog model");
            isolated.SetCatalog([new("fixture/current","Fixture model",false),new("fixture/other","Other fixture model",false)],"fixture/current","OpenRouter");
            isolated.SetApiConnectionError("OpenRouter","Fixture connection failure.");
            ((ComboBox)isolated.FindName("ChatRouteSelector")).SelectedIndex=1;isolated.SelectApiRoute();
            Check(((ComboBox)isolated.FindName("ModelSelector")).Visibility==Visibility.Collapsed,"Switching MCP to API cannot expose a stale model selector after connection failure");
            ((ComboBox)isolated.FindName("ModelSelector")).SelectedIndex=1;
            Check(!((TextBlock)isolated.FindName("DashboardApiStatus")).Text.Contains(" connected",StringComparison.OrdinalIgnoreCase)&&((TextBlock)isolated.FindName("ConnectionLabel")).Text.Contains("failed",StringComparison.OrdinalIgnoreCase),"A stale model selection cannot relabel a disconnected API as connected");
            isolated.SetApiDisconnected("OpenRouter");isolated.SetWorkspace("analyze",false);
            Check(((ComboBox)isolated.FindName("ModelSelector")).Visibility==Visibility.Collapsed,"Changing workspace keeps saved but disconnected model selection hidden");

            stage="wrong-service-key";Save();var settingsBefore=panel.CurrentSettings;bool rejected=false;
            try{panel.TryUpdateSettings(new ConnectionSettings("Anthropic / Claude",ProviderProfiles.Get("Anthropic / Claude").Endpoint,"sk-or-v1-runtime-fixture-wrong-service",false));}
            catch(ArgumentException){rejected=true;}
            Check(rejected&&panel.CurrentSettings==settingsBefore&&Field("provider").GetValue(panel) is null&&(string?)Field("apiConnectionError").GetValue(panel)==originalFailure,"Wrong-service credential is rejected locally without replacing the saved profile or real failure");

            view.SelectApiRoute();((ComboBox)view.FindName("TargetSelector")).SelectedIndex=0;
            Invoke(panel,"SwitchWorkspace","generate");
            const string box="Create a box 2 x 4 x 3 meters";
            stage="failed-api-send";Save();var before=History(panel).Count;
            view.Prompt=box;await panel.SendAsync();
            var reply=History(panel).Skip(before).LastOrDefault(m=>m.Role=="assistant")?.Content??"";
            Check(reply.Contains("saved",StringComparison.OrdinalIgnoreCase)&&reply.Contains("not connected",StringComparison.OrdinalIgnoreCase)&&reply.Contains("Last connection error:",StringComparison.Ordinal),"Actual Send after failed Connect shows the connection error instead of silently planning offline");
            Check(Field("pending").GetValue(panel) is null&&Objects(doc).Length==0&&Field("provider").GetValue(panel) is null&&(string?)Field("apiConnectionError").GetValue(panel)==originalFailure,"Failed API Send retains the real failure and leaves geometry unchanged",new{geometry_count=0,pending_plan=false});

            stage="explicit-offline";Save();
            var offline=typeof(StudioView).GetMethod("OfflineClick",Private)??throw new InvalidOperationException("The actual offline button handler is unavailable.");
            offline.Invoke(view,[view,new RoutedEventArgs()]);
            Check((bool)Field("explicitOffline").GetValue(panel)!&&Field("provider").GetValue(panel) is null&&Field("apiConnectionError").GetValue(panel) is null,"The actual explicit Offline handler selects local tools");
            view.Prompt=box;await panel.SendAsync();
            var pending=(PreparedPlan?)Field("pending").GetValue(panel);
            Check(pending is not null&&pending.Plan.Actions.Count==1&&pending.Plan.Actions[0].Tool=="create_box"&&Objects(doc).Length==0,"Explicit offline mode allows the same box request as a local preview without Apply",new{geometry_count=0,pending_actions=pending?.Plan.Actions.Count,cloud_requests_sent=0});
            panel.ClearPreview();Check(Objects(doc).Length==0,"Clearing the offline preview leaves the empty document unchanged");
            status="passed";stage="complete";Save();
        }
        catch(Exception failure)
        {
            while(failure is TargetInvocationException{InnerException:not null} reflected)failure=reflected.InnerException!;
            status=failure is FixtureUnavailableException?"unavailable":"failed";
            error=ConnectionDiagnostics.Describe(failure);
            if(secret.Length>0)error=error.Replace(secret,"[redacted]",StringComparison.Ordinal);
            error=Regex.Replace(error,@"sk-[A-Za-z0-9_-]+","[redacted]",RegexOptions.IgnoreCase);
            if(error.Length>1000)error="API failure acceptance stopped; review its last completed stage.";
            Save();RhinoApp.WriteLine("API failure acceptance "+status+" at "+stage+". See the sanitized report.");
        }
        finally
        {
            // Restore the real, actionable failed API state, not a generic disconnected label.
            if(restoreFailure&&panel is not null&&view is not null&&originalFailure is not null)
            {
                panel.ClearPreview();Field("apiConnectionError").SetValue(panel,originalFailure);Field("explicitOffline").SetValue(panel,originalExplicitOffline);
                view.Prompt=originalPrompt;view.SetApiConnectionError(panel.CurrentSettings.Source,originalFailure);
                view.SetStatus("API connection failed · open API profiles for details");
            }
        }
    }
    private sealed class FixtureUnavailableException(string message):InvalidOperationException(message);
    private static FieldInfo Field(string name)=>typeof(ArchiTecPanel).GetField(name,Private)??throw new InvalidOperationException("Runtime acceptance field is unavailable: "+name);
    private static List<ChatMessage> History(ArchiTecPanel panel)=>(List<ChatMessage>)(typeof(ArchiTecPanel).GetProperty("history",Private)?.GetValue(panel)??throw new InvalidOperationException("Runtime conversation history is unavailable."));
    private static void Invoke(ArchiTecPanel panel,string name,params object[] args)=>(typeof(ArchiTecPanel).GetMethod(name,Private)??throw new InvalidOperationException("Runtime acceptance method is unavailable: "+name)).Invoke(panel,args);
    private static RhinoObject[] Objects(RhinoDoc doc)=>doc.Objects.GetObjectList(new ObjectEnumeratorSettings{NormalObjects=true,LockedObjects=true,HiddenObjects=true,ReferenceObjects=true,ObjectTypeFilter=ObjectType.AnyObject}).Where(o=>!o.IsDeleted).ToArray();
    private static void AssertSameDocument(RhinoDoc doc){if(RhinoDoc.ActiveDoc?.RuntimeSerialNumber!=doc.RuntimeSerialNumber)throw new InvalidOperationException("The active Rhino document changed; acceptance stopped.");}
    private static void Atomic(string path,object value){var temporary=path+".tmp";File.WriteAllText(temporary,JsonSerializer.Serialize(value,Wire.Json));File.Move(temporary,path,true);}
}
