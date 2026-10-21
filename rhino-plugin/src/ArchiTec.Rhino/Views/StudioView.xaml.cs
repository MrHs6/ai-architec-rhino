using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ArchiTec.Core;

namespace ArchiTec.RhinoPlugin.Views;

public partial class StudioView:UserControl
{
    public event Action? SendRequested,ApplyRequested,CancelRequested,AttachRequested,ImportRequested,ConnectRequested,OfflineRequested,McpStartRequested,CopyMcpRequested,OpenChatRequested;
    public event Action? InputChanged,SelectedModelChanged,SaveProfileRequested,SavePreferencesRequested,AuditRequested,QuickAssistRequested;
    public event Action<string>? ModeChanged;
    private readonly Dictionary<string,ConnectionSettings> profiles=[];
    private readonly Dictionary<string,List<ModelDescriptor>> modelLists=[];
    private string activeSource="",editingSource="OpenRouter",mode="generate";
    private bool loading,compact;
    public string Prompt{get=>PromptInput.Text;set=>PromptInput.Text=value;}
    public string Source=>SourceSelector.SelectedItem as string??"OpenRouter";
    public string Endpoint=>ApiUrl.Text.Trim();
    public string ApiCredential=>ApiKey.Password;
    public bool Remember=>RememberKey.IsChecked==true;
    public ModelDescriptor? SelectedModel=>ModelSelector.SelectedItem as ModelDescriptor;
    public string Facade=>FacadeSelector.SelectedIndex switch{1=>"solid",2=>"none",_=>"glazed"};
    public string LayerPrefix=>OutputLayer.Text.Trim();
    public string AuditFocus=>AnalysisFocus.SelectedIndex switch{1=>"health",2=>"layers",_=>"dimensions"};
    public bool UseRecent=>mode=="assist"?AssistScope.SelectedIndex==1:AnalysisScope.SelectedIndex==1;
    public string AssistTool=>AssistOperation.SelectedIndex switch{1=>"scale_object",2=>"extrude_curve",_=>"move_object"};
    public int Axis=>AssistAxis.SelectedIndex;
    public double Amount=>double.TryParse(AssistAmount.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out var value)&&double.IsFinite(value)&&value>0?value:throw new ArgumentException("Enter a positive numeric distance, height or scale factor.");
    public int RequestedMcpPort=>int.TryParse(McpPort.Text,out var port)&&port is >=1024 and <=65535?port:throw new ArgumentException("Choose a localhost port from 1024 to 65535.");
    public StudioView()
    {
        InitializeComponent();loading=true;
        SourceSelector.ItemsSource=ProviderProfiles.All.Select(p=>p.Name).ToArray();SourceSelector.SelectedIndex=0;
        using var stream=typeof(StudioView).Assembly.GetManifestResourceStream("AIArchiTec.Assets.logo.png");
        var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();
        RailLogoBrush.ImageSource=bitmap;BrandLogoBrush.ImageSource=bitmap;AboutLogoBrush.ImageSource=bitmap;
        AboutVersion.Text="Version "+(typeof(StudioView).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]??"beta");
        AboutInstallPath.Text=typeof(StudioView).Assembly.Location;
        loading=false;SetWorkspace("generate",false);
    }
    public void LoadProfiles(Dictionary<string,ConnectionSettings> values){foreach(var p in values)profiles[p.Key]=p.Value;}
    public ConnectionSettings DraftSettings()=>new(Source,Endpoint,ApiCredential,Remember,(ProfileModelSelector.SelectedItem as ModelDescriptor)?.Id??(profiles.TryGetValue(Source,out var draft)?draft.Model:""));
    public void LoadSettings(ConnectionSettings value)
    {
        profiles[value.Source]=value;loading=true;SourceSelector.SelectedItem=value.Source;editingSource=value.Source;FillProfile(value);loading=false;
    }
    private void FillProfile(ConnectionSettings value)
    {
        ApiUrl.Text=value.Endpoint;ApiKey.Password=value.ApiKey;RememberKey.IsChecked=value.RememberKey;
        var preset=ProviderProfiles.Get(value.Source);ProviderNotes.Text=preset.Notes;ProviderProtocol.Text="Protocol · "+preset.Protocol;
        ApiUrl.IsReadOnly=value.Source!="OpenAI-compatible / local";
        ProfileModelSelector.ItemsSource=modelLists.TryGetValue(value.Source,out var models)?models:null;ProfileModelSelector.DisplayMemberPath="Name";
        ProfileModelSelector.Visibility=models is null?Visibility.Collapsed:Visibility.Visible;
        ProfileModelSelector.SelectedItem=models?.FirstOrDefault(m=>m.Id==value.Model);ApiStatus.Text=models is null?"Load this service's models to connect.":$"{models.Count} discovered models in this profile.";
    }
    public void SetCatalog(List<ModelDescriptor> models,string selected,string source)
    {
        loading=true;activeSource=source;modelLists[source]=models;ModelSelector.ItemsSource=models;ModelSelector.DisplayMemberPath="Name";
        ModelSelector.SelectedItem=models.FirstOrDefault(m=>m.Id==selected)??models.FirstOrDefault();ModelSelector.Visibility=Visibility.Visible;
        if(Source==source){ProfileModelSelector.ItemsSource=models;ProfileModelSelector.Visibility=Visibility.Visible;ProfileModelSelector.DisplayMemberPath="Name";ProfileModelSelector.SelectedItem=ModelSelector.SelectedItem;}
        loading=false;ConnectionLabel.Text="●  "+source;ConnectionLabel.Foreground=Brush("#6ACDAD");DashboardApiStatus.Text=source+" connected\n"+SelectedModel?.Id;
    }
    public void SetOffline(){activeSource="";ModelSelector.Visibility=Visibility.Collapsed;ConnectionLabel.Text="●  Offline tools";ConnectionLabel.Foreground=Brush("#D0B378");DashboardApiStatus.Text="Offline tools · no API connected";}
    public void SetBusy(bool value)
    {
        BusyOverlay.Visibility=value?Visibility.Visible:Visibility.Collapsed;
        foreach(var button in new[]{SendButton,ConnectButton,GenerateNav,AnalyzeNav,AssistNav,QuickAssistButton,SaveProfileButton,AttachButton,ImportButton})button.IsEnabled=!value;
        SourceSelector.IsEnabled=!value;ApiUrl.IsEnabled=!value;ApiKey.IsEnabled=!value;RememberKey.IsEnabled=!value;ModelSelector.IsEnabled=!value;ProfileModelSelector.IsEnabled=!value;
        GenerateOptions.IsEnabled=!value;AnalyzeOptions.IsEnabled=!value;AssistOptions.IsEnabled=!value;
    }
    public void SetContext(string value)=>ContextLabel.Text=value;
    public void SetStatus(string value)=>StatusLabel.Text=value;
    public void ApiMessage(string value)=>ApiStatus.Text=value;
    public void McpMessage(string value)=>McpStatus.Text=value;
    public void SetAttachment(string? value){AttachmentLabel.Text=value is null?"":"Reference · "+value+" (click clip to remove)";AttachmentLabel.Visibility=value is null?Visibility.Collapsed:Visibility.Visible;}
    public void RestoreConversation(IEnumerable<ChatMessage> messages)
    {
        while(Conversation.Children.Count>1)Conversation.Children.RemoveAt(1);
        Welcome.Visibility=Visibility.Visible;foreach(var message in messages)AddMessage(message.Role,message.Content);
    }
    public void AddMessage(string role,string value,bool error=false)
    {
        Welcome.Visibility=Visibility.Collapsed;var stack=new StackPanel();
        stack.Children.Add(new TextBlock{Text=role=="user"?"YOU":"ARCHI-TEC",FontSize=9,Foreground=Brush(role=="user"?"#AAB1CB":"#B799F4"),Margin=new Thickness(0,0,0,7)});
        stack.Children.Add(new TextBlock{Text=value,TextWrapping=TextWrapping.Wrap,LineHeight=19,Foreground=Brush(error?"#F0ABA9":"#E5E7F3"),FlowDirection=value.Any(c=>c>='\u0600'&&c<='\u06FF')?FlowDirection.RightToLeft:FlowDirection.LeftToRight});
        Conversation.Children.Add(new Border{Child=stack,Padding=new Thickness(compact?9:13),Margin=new Thickness(role=="user"?14:0,0,role=="user"?0:5,compact?7:12),CornerRadius=new CornerRadius(10),Background=Brush(role=="user"?"#202039":"#14182A"),BorderBrush=Brush(error?"#72434E":"#252B43"),BorderThickness=new Thickness(1)});
        while(Conversation.Children.Count>25)Conversation.Children.RemoveAt(1);ConversationScroll.ScrollToEnd();
    }
    public void ShowPreview(ActionPlan plan,string units){PlanSummary.Text=plan.Summary;ActionCount.Text=$"{plan.Actions.Count} operations";PlanUnits.Text=$"{plan.LengthUnit} → {units} · Rhino Undo";PlanDetails.Text=string.Join("\n",plan.Actions.Select((a,i)=>$"{i+1}. {a.Tool.Replace('_',' ')}\n{a.Parameters.GetRawText()}"));PreviewCard.Visibility=Visibility.Visible;}
    public void HidePreview()=>PreviewCard.Visibility=Visibility.Collapsed;
    public void SetMcp(bool running,string? url=null,string? token=null)
    {
        McpChip.Content=running?"MCP ●":"MCP ↗";McpStartButton.Content=running?"Stop MCP":"Start localhost MCP →";McpStatus.Text=running?"Running · session token required":"Stopped";
        DashboardMcpStatus.Text=running?"Running\n"+url:"Stopped";McpPort.IsEnabled=!running;McpToken.Password=token??"";if(url is not null)McpUrl.Text=url;
    }
    public void LoadPreferences(StudioPreferences value){DefaultWorkspace.SelectedIndex=value.DefaultWorkspace switch{"analyze"=>1,"assist"=>2,_=>0};ZoomPreview.IsChecked=value.ZoomPreview;CompactMode.IsChecked=value.Compact;McpPort.Text=value.McpPort.ToString();compact=value.Compact;}
    public StudioPreferences ReadPreferences()=>new(DefaultWorkspace.SelectedIndex switch{1=>"analyze",2=>"assist",_=>"generate"},ZoomPreview.IsChecked==true,CompactMode.IsChecked==true,RequestedMcpPort);
    public void PreferencesMessage(string value)=>PreferencesStatus.Text=value;
    public void CloseConnections()=>PageOverlay.Visibility=Visibility.Collapsed;
    public void ShowApi()=>ShowPage("API profiles",ApiSettings);
    public void ShowMcp()=>ShowPage("MCP connection",McpSettings);
    public void ShowAbout()=>ShowPage("About the creator",AboutSettings);
    public void ShowGeneral()=>ShowPage("Preferences",GeneralSettings);
    public void ShowDashboard()=>ShowPage("Connections",ConnectionSettings);
    private void ShowPage(string title,StackPanel page){foreach(var p in new[]{ApiSettings,McpSettings,GeneralSettings,AboutSettings,ConnectionSettings})p.Visibility=p==page?Visibility.Visible:Visibility.Collapsed;PageTitle.Text=title;PageOverlay.Visibility=Visibility.Visible;}
    public void SetWorkspace(string value,bool notify=true)
    {
        mode=value;CloseConnections();GenerateOptions.Visibility=value=="generate"?Visibility.Visible:Visibility.Collapsed;AnalyzeOptions.Visibility=value=="analyze"?Visibility.Visible:Visibility.Collapsed;AssistOptions.Visibility=value=="assist"?Visibility.Visible:Visibility.Collapsed;
        WorkspaceTitle.Text=value switch{"analyze"=>"Analyze model","assist"=>"Model Assist",_=>"Generate architecture"};
        WorkspaceDescription.Text=value switch{"analyze"=>"Read-only reports from selected objects or recent output.","assist"=>"Edit existing targets. Distances use document units; scale is a factor.",_=>"Create new geometry; set the concept facade and output layers."};
        HeroTitle.Text=value switch{"analyze"=>"Understand your model.","assist"=>"Refine the geometry you have.",_=>"Start a new architectural concept."};
        HeroDescription.Text=value switch{"analyze"=>"Run a local audit of dimensions, geometry health or layers. Connect an AI service for focused interpretation.","assist"=>"Select objects in Rhino or use recent output, choose an operation and preview it. This workspace keeps its own conversation.",_=>"Describe dimensions and intent, or attach a reference. Your creation prompts and references stay in this workspace."};
        ExampleButton.Content=value switch{"analyze"=>"Show a focused analysis prompt","assist"=>"Try a target-specific editing prompt",_=>"Try a five-floor building example"};
        PromptPlaceholder.Text=value switch{"analyze"=>"Ask about dimensions, geometry health or layers…","assist"=>"Describe how to edit your selected or recent targets…",_=>"Describe the new space or building you want to create…"};
        SendButton.Content=value switch{"analyze"=>"Analyze ↗","assist"=>"Assist ↗",_=>"Generate ↗"};ModeLabel.Text=value=="assist"?"MODEL ASSIST":value.ToUpperInvariant();
        ImportButton.Visibility=value=="analyze"?Visibility.Collapsed:Visibility.Visible;
        GenerateNav.Background=Brush(value=="generate"?"#38265D":"#101222");AnalyzeNav.Background=Brush(value=="analyze"?"#38265D":"#101222");AssistNav.Background=Brush(value=="assist"?"#38265D":"#101222");
        if(notify)ModeChanged?.Invoke(value);
    }
    public string PlanningOptions=>mode switch{"analyze"=>$"Analysis focus: {AuditFocus}. Read-only response.","assist"=>$"Model Assist only: existing target scope is {(UseRecent?"recent output":"selection")}. Do not create unrelated objects.",_=>$"Generate new geometry. Concept building facade preference: {Facade}. Output layer prefix: {LayerPrefix}. State assumptions."};
    private static SolidColorBrush Brush(string hex)=>new((Color)ColorConverter.ConvertFromString(hex));
    private void SendClick(object s,RoutedEventArgs e)=>SendRequested?.Invoke();
    private void ApplyClick(object s,RoutedEventArgs e)=>ApplyRequested?.Invoke();
    private void CancelClick(object s,RoutedEventArgs e)=>CancelRequested?.Invoke();
    private void AttachClick(object s,RoutedEventArgs e)=>AttachRequested?.Invoke();
    private void ImportClick(object s,RoutedEventArgs e)=>ImportRequested?.Invoke();
    private void ConnectClick(object s,RoutedEventArgs e)=>ConnectRequested?.Invoke();
    private void OfflineClick(object s,RoutedEventArgs e)=>OfflineRequested?.Invoke();
    private void McpStartClick(object s,RoutedEventArgs e)=>McpStartRequested?.Invoke();
    private void CopyMcpClick(object s,RoutedEventArgs e)=>CopyMcpRequested?.Invoke();
    private void OpenChatClick(object s,RoutedEventArgs e)=>OpenChatRequested?.Invoke();
    private void SettingsClick(object s,RoutedEventArgs e)=>ShowApi();
    private void McpClick(object s,RoutedEventArgs e)=>ShowMcp();
    private void GeneralClick(object s,RoutedEventArgs e)=>ShowGeneral();
    private void AboutClick(object s,RoutedEventArgs e)=>ShowAbout();
    private void DashboardClick(object s,RoutedEventArgs e)=>ShowDashboard();
    private void CloseSettings(object s,RoutedEventArgs e)=>CloseConnections();
    private void SaveProfileClick(object s,RoutedEventArgs e)=>SaveProfileRequested?.Invoke();
    private void SavePreferencesClick(object s,RoutedEventArgs e)=>SavePreferencesRequested?.Invoke();
    private void AuditClick(object s,RoutedEventArgs e)=>AuditRequested?.Invoke();
    private void QuickAssistClick(object s,RoutedEventArgs e)=>QuickAssistRequested?.Invoke();
    private void GenerateMode(object s,RoutedEventArgs e)=>SetWorkspace("generate");
    private void AnalyzeMode(object s,RoutedEventArgs e)=>SetWorkspace("analyze");
    private void AssistMode(object s,RoutedEventArgs e)=>SetWorkspace("assist");
    private void WebsiteClick(object s,RoutedEventArgs e)=>OpenUrl("https://ai-arch.ir");
    private void GithubClick(object s,RoutedEventArgs e)=>OpenUrl("https://github.com/MrHs6/ai-architec-rhino");
    private static void OpenUrl(string url)=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url){UseShellExecute=true});
    private void WorkspaceExample(object s,RoutedEventArgs e){Prompt=mode switch{"analyze"=>"Summarize the dimensions and visible geometry issues in this target scope.","assist"=>"Move the scoped objects 2 document units along X, keeping their GUIDs.",_=>"Create a 15 x 20 meter building with 5 floors and 3.2 meter floor heights."};PromptInput.Focus();}
    private void PromptChanged(object s,TextChangedEventArgs e){if(PromptPlaceholder is not null)PromptPlaceholder.Visibility=string.IsNullOrWhiteSpace(PromptInput.Text)?Visibility.Visible:Visibility.Collapsed;if(!loading)InputChanged?.Invoke();}
    private void PromptKey(object s,KeyEventArgs e){if(e.Key==Key.Enter&&(Keyboard.Modifiers&ModifierKeys.Control)!=0){e.Handled=true;SendRequested?.Invoke();}}
    private void ModelChanged(object s,SelectionChangedEventArgs e){if(loading)return;DashboardApiStatus.Text=activeSource+" connected\n"+SelectedModel?.Id;if(Source==activeSource){loading=true;ProfileModelSelector.SelectedItem=SelectedModel;loading=false;}SelectedModelChanged?.Invoke();}
    private void ProfileModelChanged(object s,SelectionChangedEventArgs e){if(!loading&&Source==activeSource)ModelSelector.SelectedItem=ProfileModelSelector.SelectedItem;}
    private void SourceChanged(object s,SelectionChangedEventArgs e)
    {
        if(loading||ApiUrl is null)return;
        profiles[editingSource]=new(editingSource,ApiUrl.Text,ApiKey.Password,Remember,(ProfileModelSelector.SelectedItem as ModelDescriptor)?.Id??(profiles.TryGetValue(editingSource,out var old)?old.Model:""));
        editingSource=Source;var preset=ProviderProfiles.Get(Source);var next=profiles.TryGetValue(Source,out var saved)?saved:new ConnectionSettings(Source,preset.Endpoint);
        loading=true;FillProfile(next);loading=false;
    }
    private void OptionChanged(object s,SelectionChangedEventArgs e){if(AmountLabel is not null&&AssistAxis is not null){AmountLabel.Text=AssistOperation.SelectedIndex==1?"Scale factor":"Distance / height (document units)";AssistAxis.IsEnabled=AssistOperation.SelectedIndex==0;}if(!loading)InputChanged?.Invoke();}
    private void OptionTextChanged(object s,TextChangedEventArgs e){if(!loading)InputChanged?.Invoke();}
}
