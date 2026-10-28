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
    public event Action? TestNetworkRequested,RefineResultRequested,SelectResultRequested,ExplainResultRequested;
    public event Action? CopyMcpTestRequested,SetupMcpClientRequested,OpenMcpClientRequested;
    public event Action? RouteChanged;
    public event Action? TargetChanged;
    public event Action<string>? ModeChanged;
    private readonly Dictionary<string,ConnectionSettings> profiles=[];
    private readonly Dictionary<string,List<ModelDescriptor>> modelLists=[];
    private string activeSource="",editingSource="OpenRouter",mode="generate";
    private bool loading,compact,resultAvailable,isBusy;
    public string Prompt{get=>PromptInput.Text;set=>PromptInput.Text=value;}
    public bool ChatViaMcp=>ChatRouteSelector.SelectedIndex==1;
    public bool GrasshopperTarget=>TargetSelector?.SelectedIndex==1;
    public string McpClient=>McpClientSelector.SelectedIndex switch{1=>"Codex",2=>"Other MCP client",_=>"Claude Desktop"};
    public string Source=>SourceSelector.SelectedItem as string??"OpenRouter";
    public string Endpoint=>ApiUrl.Text.Trim();
    public string ApiCredential=>ApiKey.Password.Trim();
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
        loading=false;SetMcp(false);SetWorkspace("generate",false);UpdateMcpGuide();
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
        ProfileModelSelector.DisplayMemberPath="Name";ProfileModelSelector.ItemsSource=modelLists.TryGetValue(value.Source,out var models)?models:null;
        ProfileModelSelector.Visibility=models is null?Visibility.Collapsed:Visibility.Visible;
        ProfileModelSelector.SelectedItem=models?.FirstOrDefault(m=>m.Id==value.Model);ApiStatus.Text=models is null?"Load this service's models to connect.":$"{models.Count} discovered models in this profile.";
        ProfileSaveStatus.Text=value.ApiKey.Length==0?"No key in this session. Enter a key, then Save or Connect.":value.RememberKey?"Key loaded for this Windows user.":"Session key · not stored on disk. Enable Remember to retain it after restarting Rhino.";
        NetworkStatus.Text="";
    }
    public void ProfileSaved(ConnectionSettings value)
    {
        profiles[value.Source]=value;
        ProfileSaveStatus.Text=$"Profile saved · {value.Source}. "+(value.RememberKey&&value.ApiKey.Length>0?"Key encrypted for this Windows user.":value.ApiKey.Length>0?"Key kept for this Rhino session only; it will be cleared on exit.":"No key stored.");
    }
    public void ProfileSaveError(string message)=>ProfileSaveStatus.Text="Could not save profile: "+message;
    public void NetworkMessage(string message)=>NetworkStatus.Text=message;
    public void SetRecentResult(string? message,bool available=true){resultAvailable=message is not null&&available;RecentResultBar.Visibility=message is null||GrasshopperTarget||ChatViaMcp?Visibility.Collapsed:Visibility.Visible;RecentResultLabel.Text=message??"";RefineResultButton.IsEnabled=SelectResultButton.IsEnabled=ExplainResultButton.IsEnabled=resultAvailable&&!isBusy;}
    public void UseRecentResult(){AssistScope.SelectedIndex=1;AnalysisScope.SelectedIndex=1;}
    public void FocusPrompt()=>PromptInput.Focus();
    public void SetCatalog(List<ModelDescriptor> models,string selected,string source)
    {
        loading=true;activeSource=source;modelLists[source]=models;ModelSelector.DisplayMemberPath="Name";ModelSelector.ItemsSource=models;
        ModelSelector.SelectedItem=models.FirstOrDefault(m=>m.Id==selected);ModelSelector.Visibility=Visibility.Visible;
        if(Source==source){ProfileModelSelector.DisplayMemberPath="Name";ProfileModelSelector.ItemsSource=models;ProfileModelSelector.Visibility=Visibility.Visible;ProfileModelSelector.SelectedItem=ModelSelector.SelectedItem;}
        loading=false;ConnectionLabel.Text="API · "+source;ConnectionLabel.Foreground=Brush("#6ACDAD");DashboardApiStatus.Text=source+" connected\n"+(SelectedModel?.Id??"Choose a model explicitly");
    }
    public void SetOffline(){activeSource="";ModelSelector.Visibility=Visibility.Collapsed;ConnectionLabel.Text="API · not connected · offline tools";ConnectionLabel.Foreground=Brush("#D0B378");DashboardApiStatus.Text="Offline tools · no API connected";}
    public void SetApiDisconnected(string source){activeSource="";ModelSelector.Visibility=Visibility.Collapsed;ConnectionLabel.Text="API · "+source+" · saved, not connected";ConnectionLabel.Foreground=Brush("#D0B378");DashboardApiStatus.Text="Profile saved · connect to use "+source;}
    public void SetApiConnecting(string source){SetApiDisconnected(source);ConnectionLabel.Text="API · "+source+" · connecting…";DashboardApiStatus.Text="Checking credentials and model catalog…";}
    public void SetApiConnectionError(string source,string message){SetApiDisconnected(source);ConnectionLabel.Text="API · "+source+" · connection failed";ConnectionLabel.Foreground=Brush("#F0ABA9");DashboardApiStatus.Text=source+" connection failed · see API profiles";ApiMessage("Connection not established. "+message);}
    public void SetBusy(bool value)
    {
        isBusy=value;
        BusyOverlay.Visibility=value?Visibility.Visible:Visibility.Collapsed;
        foreach(var button in new[]{SendButton,ConnectButton,TestNetworkButton,OfflineButton,GenerateNav,AnalyzeNav,AssistNav,QuickAssistButton,SaveProfileButton,AttachButton,ImportButton,RefineResultButton,SelectResultButton,ExplainResultButton})button.IsEnabled=!value;
        RefineResultButton.IsEnabled=SelectResultButton.IsEnabled=ExplainResultButton.IsEnabled=!value&&resultAvailable;
        ApiCancelButton.Visibility=value?Visibility.Visible:Visibility.Collapsed;
        SourceSelector.IsEnabled=!value;ApiUrl.IsEnabled=!value;ApiKey.IsEnabled=!value;RememberKey.IsEnabled=!value;ModelSelector.IsEnabled=!value;ProfileModelSelector.IsEnabled=!value;
        GenerateOptions.IsEnabled=!value;AnalyzeOptions.IsEnabled=!value;AssistOptions.IsEnabled=!value;
        TargetSelector.IsEnabled=!value;
    }
    public void SetContext(string value)=>ContextLabel.Text=value;
    public void SetStatus(string value)=>StatusLabel.Text=value;
    public void ApiMessage(string value)=>ApiStatus.Text=value;
    public void McpMessage(string value)=>McpSetupStatus.Text=value;
    public void SetAttachment(string? value){AttachmentLabel.Text=value is null?"":"Reference · "+value+" (click clip to remove)";AttachmentLabel.Visibility=value is null?Visibility.Collapsed:Visibility.Visible;}
    public void RestoreConversation(IEnumerable<ChatMessage> messages)
    {
        while(Conversation.Children.Count>1)Conversation.Children.RemoveAt(1);
        Welcome.Visibility=Visibility.Visible;foreach(var message in messages)AddMessage(message.Role,message.Content);
    }
    public void AddMessage(string role,string value,bool error=false)
    {
        Welcome.Visibility=Visibility.Collapsed;var stack=new StackPanel();
        stack.Children.Add(new TextBlock{Text=role=="user"?"YOU":"ARCHI-TEC",FontSize=10,Foreground=Brush(role=="user"?"#AAB1CB":"#B799F4"),Margin=new Thickness(0,0,0,7)});
        stack.Children.Add(new TextBlock{Text=value,FontSize=13,TextWrapping=TextWrapping.Wrap,LineHeight=21,Foreground=Brush(error?"#F0ABA9":"#E5E7F3"),FlowDirection=value.Any(c=>c>='\u0600'&&c<='\u06FF')?FlowDirection.RightToLeft:FlowDirection.LeftToRight});
        Conversation.Children.Add(new Border{Child=stack,Padding=new Thickness(compact?9:13),Margin=new Thickness(role=="user"?14:0,0,role=="user"?0:5,compact?7:12),CornerRadius=new CornerRadius(10),Background=Brush(role=="user"?"#202039":"#14182A"),BorderBrush=Brush(error?"#72434E":"#252B43"),BorderThickness=new Thickness(1)});
        while(Conversation.Children.Count>25)Conversation.Children.RemoveAt(1);ConversationScroll.ScrollToEnd();
    }
    public void ShowPreview(ActionPlan plan,string units){PlanSummary.Text=plan.Summary;ActionCount.Text=$"{plan.Actions.Count} operations";PlanUnits.Text=GrasshopperPlan.IsGrasshopper(plan)?"Definition plan · solves on Apply · Grasshopper Undo":$"{plan.LengthUnit} → {units} · Rhino Undo";PlanDetails.Text=string.Join("\n",plan.Actions.Select((a,i)=>$"{i+1}. {a.Tool.Replace('_',' ')}\n{a.Parameters.GetRawText()}"));PreviewCard.Visibility=Visibility.Visible;ConversationScroll.ScrollToEnd();}
    public void HidePreview()=>PreviewCard.Visibility=Visibility.Collapsed;
    public void SetMcp(bool running,string? url=null,string? token=null)
    {
        if(running)ChatRouteSelector.SelectedIndex=1;
        McpChip.Content=running?"MCP · waiting":"MCP · off";McpStartButton.Content=running?"Stop MCP":"Start MCP";McpStartButton.Visibility=Visibility.Visible;McpStatus.Text=running?"MCP enabled · waiting for your AI client":"Connection disabled";
        McpStage.Visibility=McpLastSeen.Visibility=Visibility.Collapsed;
        McpClientStatus.Text=running?"Stays enabled while Rhino is open, including when this panel is hidden. Stop MCP disables the local connection.":"Click Connect to set up your client, or Start MCP if it is already configured.";McpStage.Text="";McpLastSeen.Text="";McpActivityLog.Text="";
        DashboardMcpStatus.Text=running?"Running\n"+url:"Stopped";McpPort.IsEnabled=!running;McpToken.Password=token??"";if(url is not null)McpUrl.Text=url;
        McpWorkspaceStatus.Text=running?"Waiting for "+McpClient+". Enable its Archi-Tec tools if this is your first connection.":"Connection is off. Enable it to receive requests; your AI client can stay open.";UpdateChatRoute();
    }
    public void SetMcpConnection(McpConnectionSnapshot state)
    {
        var recent=state.LastSeenUtc is {} seen&&DateTimeOffset.UtcNow-seen<TimeSpan.FromSeconds(45);
        var name=state.ClientName?.Contains("codex",StringComparison.OrdinalIgnoreCase)==true?"Codex":state.ClientName?.Contains("claude",StringComparison.OrdinalIgnoreCase)==true?"Claude Desktop":state.ClientName;
        McpStage.Visibility=McpLastSeen.Visibility=Visibility.Visible;
        McpStatus.Text=state.ClientName is null?"MCP enabled · waiting for your AI client":recent?"Connected · "+name:"Session ready · "+name+" · idle";
        McpChip.Content=state.ClientName is null?"MCP · waiting":recent?"MCP · "+name:"MCP · idle";
        McpClientStatus.Text=state.ClientName is null?"No active client session. The local server remains enabled; reconnect from your AI client.":recent?"Client controls planning and execution. Continue in "+name+". Switching apps does not stop MCP.":"No recent contact; the session remains valid. Continue in your AI client without pressing Start again. Last contact does not prove the client is still open.";
        McpStage.Text=state.Stage switch{"initialized"=>"Finishing connection…","tools_ready"=>"Ready · describe your model in the AI client","context_ready"=>"Rhino document shared with your client","ready"=>"Result returned · ready for your next request","working"=>"Processing your client request…","preview"=>"Optional preview · apply or cancel from your AI client","applied"=>"Applied · native geometry confirmed · ready for your next edit","cancelled"=>"Client request stopped · connection stays enabled","error"=>"Request failed; connection stays enabled · review the client response","message"=>"Client status received · ready for your next request","handoff"=>"Client request prepared","disconnected"=>"Client session closed; the server is still running",_=>"Open your client and enable its Archi-Tec tools"};
        McpLastSeen.Text=state.LastSeenUtc is {} at?"Last contact: "+at.ToLocalTime().ToString("HH:mm:ss"):"A running server is not a confirmed ChatGPT or Claude connection.";
        McpActivityLog.Text=string.Join("\n",state.Activity.Select(e=>e.At.ToLocalTime().ToString("HH:mm:ss")+" · "+e.Stage+" · "+e.Detail));
        DashboardMcpStatus.Text=McpClientStatus.Text+"\n"+McpStage.Text;
        McpWorkspaceStatus.Text=state.ClientName is null?"MCP is enabled. Open "+McpClient+" and enable its Archi-Tec tools.":recent?"Connected to "+name+" · keep analyzing and refining the same model in your client.":"Session ready · continue in your AI client. Model IDs and receipts stay available while this Rhino session is open.";
    }
    private void UpdateMcpGuide()
    {
        if(McpSetupGuide is null)return;McpSetupGuide.Text=McpClientSetup.Guide(McpClient);
        CopyMcpSetupButton.Content=McpClient=="Codex"?"Copy Codex configuration (TOML)":"Copy MCP configuration (JSON)";
        SetupMcpClientButton.Content=McpClient=="Other MCP client"?"Enable connection →":"Connect to "+McpClient+" →";
        SetupMcpClientButton.IsEnabled=true;
        McpWorkspaceTitle.Text="Chat in "+McpClient;OpenMcpClientButton.Content="Open "+McpClient+" ↗";OpenMcpClientButton.IsEnabled=McpClient!="Other MCP client";
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
        UpdateChatRoute();
        ImportButton.Visibility=ChatViaMcp||value=="analyze"?Visibility.Collapsed:Visibility.Visible;
        WorkspaceHelp.Text=value switch{"analyze"=>"Analyze never changes geometry. Select objects in Rhino or choose Recent output. Dimensions measures bounds, Health checks geometry validity and closure, and Layers summarizes scoped objects. Run local audit works without an API. Conversational interpretation needs a connected API or an external MCP client.","assist"=>"Assist edits Selection or Recent output. Move uses a distance and axis. Scale uses a factor: 2 doubles size; 0.5 halves it. Extrude uses a height on a suitable curve. Preview first, then Apply. Ctrl+Z undoes the operation. Free-form instructions require an API or external MCP client.",_=>"Generate creates new geometry. With an API connected, chat here. For MCP, send your request in Claude or Codex; replies and proposals return through the MCP tools. Starting MCP alone does not connect this input to an online model. Offline tools support limited geometry examples. Review the preview, then Apply in Rhino."};
        GenerateNav.Background=Brush(value=="generate"?"#38265D":"#101222");AnalyzeNav.Background=Brush(value=="analyze"?"#38265D":"#101222");AssistNav.Background=Brush(value=="assist"?"#38265D":"#101222");
        UpdateChatRoute();
        if(notify)ModeChanged?.Invoke(value);
    }
    public string PlanningOptions=>GrasshopperTarget?"Target: Grasshopper. Return a complete editable grasshopper_definition using catalog component IDs, document units and explicit wires. Include bake_nodes only if requested. For edits use definition_id and the complete revised graph.":mode switch{"analyze"=>$"Analysis focus: {AuditFocus}. Read-only response.","assist"=>$"Model Assist only: existing target scope is {(UseRecent?"recent output":"selection")}. Do not create unrelated objects.",_=>$"Generate new geometry. Concept building facade preference: {Facade}. Output layer prefix: {LayerPrefix}. State assumptions."};
    private static SolidColorBrush Brush(string hex)=>new((Color)ColorConverter.ConvertFromString(hex));
    private void SendClick(object s,RoutedEventArgs e)=>SendRequested?.Invoke();
    private void ApplyClick(object s,RoutedEventArgs e)=>ApplyRequested?.Invoke();
    private void CancelClick(object s,RoutedEventArgs e)=>CancelRequested?.Invoke();
    private void AttachClick(object s,RoutedEventArgs e)=>AttachRequested?.Invoke();
    private void ImportClick(object s,RoutedEventArgs e)=>ImportRequested?.Invoke();
    private void ConnectClick(object s,RoutedEventArgs e)=>ConnectRequested?.Invoke();
    private void TestNetworkClick(object s,RoutedEventArgs e)=>TestNetworkRequested?.Invoke();
    private void RefineResultClick(object s,RoutedEventArgs e)=>RefineResultRequested?.Invoke();
    private void SelectResultClick(object s,RoutedEventArgs e)=>SelectResultRequested?.Invoke();
    private void ExplainResultClick(object s,RoutedEventArgs e)=>ExplainResultRequested?.Invoke();
    private void ProfileEdited(object s,RoutedEventArgs e){if(!loading&&ProfileSaveStatus is not null)ProfileSaveStatus.Text="Unsaved profile edits · Save or Connect to activate this service. Remember controls whether the key survives a restart.";}
    private void OfflineClick(object s,RoutedEventArgs e)=>OfflineRequested?.Invoke();
    private void McpStartClick(object s,RoutedEventArgs e)=>McpStartRequested?.Invoke();
    private void CopyMcpClick(object s,RoutedEventArgs e)=>CopyMcpRequested?.Invoke();
    private void CopyMcpTestClick(object s,RoutedEventArgs e)=>CopyMcpTestRequested?.Invoke();
    private void McpClientChanged(object s,SelectionChangedEventArgs e)=>UpdateMcpGuide();
    private void ChatRouteChanged(object s,SelectionChangedEventArgs e){UpdateChatRoute();RouteChanged?.Invoke();}
    private void TargetSelectionChanged(object s,SelectionChangedEventArgs e){if(SendButton is null)return;UpdateChatRoute();TargetChanged?.Invoke();}
    private void UpdateChatRoute()
    {
        if(SendButton is null)return;
        TargetSelector.Visibility=ChatViaMcp?Visibility.Collapsed:Visibility.Visible;
        RecentResultBar.Visibility=!ChatViaMcp&&!GrasshopperTarget&&RecentResultLabel.Text.Length>0?Visibility.Visible:Visibility.Collapsed;
        ApiConnectionButton.Visibility=ChatViaMcp?Visibility.Collapsed:Visibility.Visible;McpRouteLabel.Visibility=ChatViaMcp?Visibility.Visible:Visibility.Collapsed;
        ImportButton.Visibility=ChatViaMcp||mode=="analyze"?Visibility.Collapsed:Visibility.Visible;
        ComposerCard.Visibility=ChatViaMcp?Visibility.Collapsed:Visibility.Visible;
        McpWorkspaceCard.Visibility=ChatViaMcp?Visibility.Visible:Visibility.Collapsed;
        WorkspaceOptionsCard.Visibility=ChatViaMcp?Visibility.Collapsed:Visibility.Visible;
        GenerateNav.Visibility=AnalyzeNav.Visibility=AssistNav.Visibility=ChatViaMcp?Visibility.Collapsed:Visibility.Visible;
        PreviewCommitControls.Visibility=ChatViaMcp?Visibility.Collapsed:Visibility.Visible;
        ModelSelector.Visibility=!ChatViaMcp&&activeSource.Length>0&&ModelSelector.Items.Count>0?Visibility.Visible:Visibility.Collapsed;
        GenerateOptions.Visibility=!ChatViaMcp&&mode=="generate"?Visibility.Visible:Visibility.Collapsed;
        if(!ChatViaMcp&&GrasshopperTarget)
        {
            GenerateOptions.Visibility=AnalyzeOptions.Visibility=AssistOptions.Visibility=Visibility.Collapsed;
            AnalyzeNav.Visibility=AssistNav.Visibility=Visibility.Collapsed;
            WorkspaceTitle.Text="Grasshopper definition";
            WorkspaceDescription.Text="Create and refine editable components, sliders, data trees and wires. Bake only when requested.";
            WorkspaceHelp.Text="Select your API service and model, describe a parametric definition, review its structured plan, then Apply. The solver runs on Apply. Continue edits here. Graph Undo is in Grasshopper; baked objects use Rhino Undo. Existing non-bridge definitions are read-only. In MCP, stay in your external AI client.";
            ExampleButton.Content="Try a parametric box definition";
            PromptPlaceholder.Text="Describe the Grasshopper definition or its next change…";
        }
        AssistOptions.Visibility=!ChatViaMcp&&!GrasshopperTarget&&mode=="assist"?Visibility.Visible:Visibility.Collapsed;
        ExampleButton.Visibility=ChatViaMcp?Visibility.Collapsed:Visibility.Visible;
        SendButton.Content=mode switch{"analyze"=>"Analyze ↗","assist"=>"Assist ↗",_=>"Generate ↗"};
        ModeLabel.Text=mode=="assist"?"MODEL ASSIST":mode.ToUpperInvariant();
        if(ChatViaMcp){HeroTitle.Text="Rhino execution bridge";HeroDescription.Text="Keep working in your AI client. It sends native modeling operations and receives measured results. This is a status log, not another chatbot.";}
        else{HeroTitle.Text=mode switch{"analyze"=>"Understand your model.","assist"=>"Refine the geometry you have.",_=>"Start a new architectural concept."};HeroDescription.Text="Describe your request here using your connected API service, or try the limited offline tools.";}
        if(!ChatViaMcp&&GrasshopperTarget){HeroTitle.Text="Parametric thinking. Editable definitions.";HeroDescription.Text="Build, solve and refine in Grasshopper using your API account. Geometry stays parametric until you request a bake.";}
    }
    public void SelectApiRoute()=>ChatRouteSelector.SelectedIndex=0;
    private void WorkspaceSelectorWheel(object sender,MouseWheelEventArgs e)
    {
        if(sender is not ComboBox{IsDropDownOpen:false} selector)return;
        // A closed selector should scroll its page, not accidentally change the selected model.
        for(DependencyObject? parent=VisualTreeHelper.GetParent(selector);parent is not null;parent=VisualTreeHelper.GetParent(parent))
            if(parent is ScrollViewer scroll){scroll.ScrollToVerticalOffset(scroll.VerticalOffset-e.Delta);e.Handled=true;return;}
    }
    public void SetMcpSetupBusy(bool value){SetupMcpClientButton.IsEnabled=!value;McpClientSelector.IsEnabled=!value;McpStartButton.IsEnabled=!value;}
    private void SetupMcpClientClick(object s,RoutedEventArgs e)=>SetupMcpClientRequested?.Invoke();
    private void OpenMcpClientClick(object s,RoutedEventArgs e)=>OpenMcpClientRequested?.Invoke();
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
    private void WorkspaceExample(object s,RoutedEventArgs e){Prompt=GrasshopperTarget?"Create an editable Grasshopper box with width, depth and height sliders, initially 2 by 4 by 3 document units. Do not bake it yet.":mode switch{"analyze"=>"Summarize the dimensions and visible geometry issues in this target scope.","assist"=>"Move the scoped objects 2 document units along X, keeping their GUIDs.",_=>"Create a 15 x 20 meter building with 5 floors and 3.2 meter floor heights."};PromptInput.Focus();}
    private void PromptChanged(object s,TextChangedEventArgs e){if(PromptPlaceholder is not null)PromptPlaceholder.Visibility=string.IsNullOrWhiteSpace(PromptInput.Text)?Visibility.Visible:Visibility.Collapsed;if(!loading)InputChanged?.Invoke();}
    private void PromptKey(object s,KeyEventArgs e){if(e.Key==Key.Enter&&(Keyboard.Modifiers&ModifierKeys.Control)!=0){e.Handled=true;SendRequested?.Invoke();}}
    private void ModelChanged(object s,SelectionChangedEventArgs e){if(loading||activeSource.Length==0)return;DashboardApiStatus.Text=activeSource+" connected\n"+(SelectedModel?.Id??"Choose a model explicitly");if(Source==activeSource){loading=true;ProfileModelSelector.SelectedItem=SelectedModel;loading=false;}SelectedModelChanged?.Invoke();}
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
