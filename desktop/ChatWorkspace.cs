using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Microsoft.Win32;
namespace Studio;

public partial class StudioWindow
{
    CancellationTokenSource? turnCancellation;
    void StopTurn(object sender,RoutedEventArgs e){turnCancellation?.Cancel();Status.Text="Stopping after any active host operation…";}
    string? outputFolder;
    StackPanel? activity;
    TextBlock? currentStep;
    internal void SetRail(bool open)
    {
        bool overlay=ActualWidth>0 && ActualWidth<750;
        RailWidth.Width=new GridLength(open && !overlay?190:0);
        Rail.Visibility=open?Visibility.Visible:Visibility.Collapsed;
        Rail.Width=190;Rail.HorizontalAlignment=HorizontalAlignment.Left;
        Grid.SetColumnSpan(Rail,overlay?2:1);Panel.SetZIndex(Rail,2);
    }
    void ToggleRail(object s,RoutedEventArgs e){ ShowChat(); SetRail(Rail.Visibility!=Visibility.Visible); }
    void ProviderMenu(object s,RoutedEventArgs e){ShowChat();Settings.IsExpanded=!Settings.IsExpanded;}
    void EndpointChanged(object s,TextChangedEventArgs e){if(Key!=null && Service=="Compatible API")Key.Clear();}
    void Menu(Button anchor,params (string title,Action command)[] items)
    {
        var popup=new ContextMenu{Style=(Style)FindResource("PopupMenu"),PlacementTarget=anchor,Placement=PlacementMode.Bottom,Background=ColorBrush("#282331"),Foreground=Brushes.White};
        foreach(var (title,command) in items)
        {
            var icon=title switch {
                "Rhino + Revit"=>"flow", "New conversation"=>"chat", "Attach reference or render"=>"attach",
                "Connector setup" or "External MCP configuration"=>"link", "Developer documentation"=>"book",
                "Capture viewport" or "Inspect document"=>"build", _=>"apps"
            };
            var row=new StackPanel{Orientation=Orientation.Horizontal};
            row.Children.Add(new StudioGlyph{Kind=icon});
            row.Children.Add(new TextBlock{Text=title,Margin=new Thickness(10,0,4,0)});
            var item=new MenuItem{Style=(Style)FindResource("PopupOption"),Header=row};
            System.Windows.Automation.AutomationProperties.SetName(item,title);
            item.Click+=(_,_)=>command();
            popup.Items.Add(item);
        }
        popup.IsOpen=true;
    }
    void WorkspaceMenu(object s,RoutedEventArgs e){ ShowChat(); Menu((Button)s,
        ("Rhino + Revit",()=>{if(!busy && !voice.Recording) Host.SelectedItem=Host.Items.Cast<ListBoxItem>().First(i=>Equals(i.Tag,"Both"));}),("New conversation",()=>Clear(s,e)),("Export conversation…",()=>ExportThread(s,e)),("Choose output folder…",ChooseFolder),
        ("Connector setup",()=>AddHost(s,e)),("External MCP configuration",()=>Config(s,e)),("Developer documentation",()=>DocsClick(s,e))); }
    void StudioMenu(object s,RoutedEventArgs e)=>OpenStudioPage("Studio account");
    void OpenLink(string url){try{Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}catch(Exception ex){Status.Text=ex.Message;}}
    void ChooseFolder(){var dialog=new OpenFolderDialog{Title="Choose the default folder for this session"};if(dialog.ShowDialog(this)==true){outputFolder=dialog.FolderName;Status.Text="Output folder · "+outputFolder;}}
    void SaveText(string filename,string content)
    {
        var dialog=new SaveFileDialog{FileName=filename,DefaultExt=Path.GetExtension(filename),InitialDirectory=outputFolder??Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),OverwritePrompt=true};
        if(dialog.ShowDialog(this)!=true)return;
        try{File.WriteAllText(dialog.FileName,content,new UTF8Encoding(Path.GetExtension(dialog.FileName).Equals(".csv",StringComparison.OrdinalIgnoreCase)));outputFolder=Path.GetDirectoryName(dialog.FileName);Status.Text="Saved · "+dialog.FileName;}catch(Exception ex){Status.Text=ex.Message;}
    }
    void ExportThread(object s,RoutedEventArgs e)
    {
        var doc=new StringBuilder("# "+Target+" conversation\n");
        foreach(var m in history){
            var role=(string?)m?["role"];
            if(role=="system")continue;
            var content=m?["content"] is JsonArray parts?string.Join("\n",parts.Select(p=>(string?)p?["type"]=="text"?(string?)p?["text"]:"[Reference image]")):m?["content"]?.ToString();
            if(!string.IsNullOrEmpty(content))doc.AppendLine("\n## "+role+"\n").AppendLine(content);
        }
        SaveText("conversation.md",doc.ToString());
    }
    void Activity(string text,string state)
    {
        if(activity==null){
            activity=new StackPanel{Margin=new Thickness(10,5,10,5)};
            Messages.Children.Add(new Expander{Header="Activity",IsExpanded=true,Foreground=Brushes.Lavender,Content=activity,Margin=new Thickness(4,4,4,6)});
        }
        if(state=="running" || currentStep==null){currentStep=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,2,0,2)};activity.Children.Add(currentStep);}
        currentStep.Text=(state=="done"?"✓  ":state=="failed"?"!  ":"◌  ")+text;
        currentStep.Foreground=ColorBrush(state=="failed"?"#FFADAC":state=="done"?"#A7D8CB":"#CDB7FF");
        Status.Text=text;
        if(state=="done" || state=="failed")currentStep=null;
        Scroll.ScrollToEnd();
    }
    async void TestModel(object s,RoutedEventArgs e){
        if(busy || voice.Recording)return;
        ModelTestStatus.Text="Contacting "+Service+"…";
        bool verified=false;
        await Run(async()=>{
        if(Service is not ("Compatible API" or CodexModel.Provider) && string.IsNullOrWhiteSpace(Key.Password))throw new IOException("Enter the API key for "+Service+".");
        if(Model.Text.Trim().Length==0)throw new IOException("Enter a model ID.");
        var thread=new JsonArray(Wire.Node(new{role="user",content="Reply with OK."}));
        await studio.Chat(Target,Service,Model.Text.Trim(),Key.Password.Trim(),thread,false,_=>{},_=>{},Activity,Endpoint.Text,probe:true);
        verified=true;
        });
        ModelTestStatus.Text=verified?Service+" replied using "+Model.Text.Trim()+".":Status.Text;
    }
}
