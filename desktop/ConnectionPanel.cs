using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Media;

namespace Studio;
public partial class StudioWindow
{
    readonly IReadOnlyList<HostInstall> installs=HostSetup.Detect();
    HostInstall? SelectedInstall=>HostSetup.Find(installs,AppName,VersionName);
    void BuildTiles()
    {
        HostTiles.Children.Clear();
        foreach(var app in installs.GroupBy(x=>x.Name).Select(g=>g.First()))
        {
            var tile=new Button{ToolTip=app.Name+" "+app.Edition,Padding=new Thickness(5),Width=36,Height=34,Tag=app.Name};
            AutomationProperties.SetName(tile,"Select "+app.Name+" "+app.Edition);
            var icon=app.Icon();
            tile.Content=icon!=null?new Image{Source=icon,Width=23,Height=23}:new TextBlock{Text=ApplicationCatalog.Find(app.Name)?.Mark??app.Name};
            tile.Click+=(_,_)=>{
                if(busy||voice.Recording)return;
                Host.SelectedItem=Host.Items.Cast<ListBoxItem>().First(i=>Equals(i.Tag,app.Name));
                if(Edition.Items.Contains(app.Edition))Edition.SelectedItem=app.Edition;
            };
            HostTiles.Children.Add(tile);
        }
        UpdateSetup();
    }
    void ToggleConnections(object s,RoutedEventArgs e)
    {
        ConnectPanel.Visibility=ConnectPanel.Visibility==Visibility.Visible?Visibility.Collapsed:Visibility.Visible;
        if(ConnectPanel.Visibility==Visibility.Visible)UpdateSetup();
    }
    void UpdateSetup()
    {
        if(SetupHint==null)return;
        var install=SelectedInstall;
        foreach(Button tile in HostTiles.Children)tile.Background=Equals(tile.Tag,AppName)?ColorBrush("#463754"):Brushes.Transparent;
        var route=Connections.Route(Target);
        bool custom=route=="Connector required";
        bool missing=install!=null && HostSetup.NeedsInstall(install);
        InstallBridge.Visibility=missing && !custom?Visibility.Visible:Visibility.Collapsed;
        InstallBridge.Content="Install bridge";
        LaunchHost.Visibility=install==null?Visibility.Collapsed:Visibility.Visible;
        LaunchHost.Content="Open "+install?.Name;
        bool running=false;
        if(install!=null){
            var processes=System.Diagnostics.Process.GetProcessesByName(Path.GetFileNameWithoutExtension(install.Exe));
            try { running=processes.Any(p=>{try{return string.Equals(p.MainModule?.FileName,install.Exe,StringComparison.OrdinalIgnoreCase);}catch(System.ComponentModel.Win32Exception){return false;}catch(InvalidOperationException){return false;}}); }
            finally { foreach(var p in processes)p.Dispose(); }
        }
        Step(BridgeStep,"Bridge",!custom && !missing && install!=null);
        Step(AppStep,"App open",running);
        Step(HandshakeStep,"Verified",false);
        SetupHint.Text=custom?"No native connector is available for this edition.":
            missing?"Install the bridge for "+install!.Name+" "+install.Edition+", then reopen the application.":
            AppName=="Both"?"Open a Rhino model and a Revit project, then check both connections.":
            install==null?"No standard installation found. Open the application manually, then check the connection.":
            AppName is "Rhino" or "Grasshopper"?"Open a model after installing the Studio native connector.":
            AppName is "Blender" or "3ds Max" or "FreeCAD" or "Maya"?"Open the app from Studio to start its scene bridge, then check the connection.":
            "Open a document in "+install.Name+", then check the connection.";
    }
    static void Step(TextBlock label,string title,bool complete)
    {
        label.Text=(complete?"✓  ":"○  ")+title;
        label.Foreground=ColorBrush(complete?"#94D5B3":"#B6AFBF");
    }
    async void InstallBridgeClick(object s,RoutedEventArgs e)
    {
        if(Connections.Route(Target)=="Connector required")return;
        if(SelectedInstall is not {} host)return;
        await Run(async()=>{await HostSetup.Install(host);UpdateSetup();SetupHint.Text="Bridge installed. Reopen "+host.Name+" and check the connection.";});
    }
    void LaunchHostClick(object s,RoutedEventArgs e)
    {
        try{if(SelectedInstall is {} host){HostSetup.Launch(host);SetupHint.Text="Opening "+host.Name+". When the document is ready, check the connection.";}}catch(Exception ex){SetupHint.Text=ex.Message;}
    }
    async void ProbeHostClick(object s,RoutedEventArgs e)
    {
        await Run(async()=>{
            UpdateSetup();SetupHint.Text="Checking "+Target+"…";
            try
            {
                foreach(var host in Target=="Both"?new[]{"Rhino","Revit"}:new[]{Target})
                {
                    var command=host is "Revit" or "Rhino"?host.ToLowerInvariant()+"_get_context":"get_context";
                    var result=await studio.Call(host,command,new System.Text.Json.Nodes.JsonObject());
                    if((bool?)result["isError"]==true)throw new IOException(Wire.Describe(result));
                    if(AppName=="Grasshopper"){
                        var doc=System.Text.Json.Nodes.JsonNode.Parse(Wire.Describe(result))!["document_id"];
                        var graph=await studio.Call(host,"get_grasshopper_context",new System.Text.Json.Nodes.JsonObject{["document_id"]=doc?.DeepClone()});
                        if((bool?)graph["isError"]==true)throw new IOException(Wire.Describe(graph));
                    }
                }
                Step(BridgeStep,"Bridge",true);Step(HandshakeStep,"Verified",true);Step(AppStep,"App open",true);
                SetupHint.Text="✓  "+Target+" responded at "+DateTime.Now.ToShortTimeString()+".";
            }catch(Exception ex){SetupHint.Text="Connection unavailable · "+ex.Message;throw;}
        });
    }
    void ComposerMenu(object s,RoutedEventArgs e)
    {
        var options=new List<(string,Action)> {("Attach reference or render",()=>Attach(s,e)),("Inspect document",()=>Inspect(s,e)),("Export conversation",()=>ExportThread(s,e)),("Choose output folder",ChooseFolder)};
        if(Target is "Both" or "Rhino")options.Insert(0,("Capture viewport",()=>Viewport(s,e)));
        Menu((Button)s,options.ToArray());
    }
}
