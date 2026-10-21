using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ArchiTec.Core;

namespace ArchiTec.RhinoPlugin.Views;

public partial class StudioView : UserControl
{
    public event Action? SendRequested,ApplyRequested,CancelRequested,AttachRequested,ImportRequested,ConnectRequested,OfflineRequested,McpStartRequested,CopyMcpRequested,OpenChatRequested;
    public event Action? InputChanged,SelectedModelChanged;
    public event Action<string>? ModeChanged;
    public string Prompt {get=>PromptInput.Text;set=>PromptInput.Text=value;}
    public string Source=>SourceSelector.SelectedIndex switch {1=>"OpenAI",2=>"OpenAI-compatible / local",_=>"OpenRouter"};
    public string Endpoint=>ApiUrl.Text.Trim();
    public string ApiCredential=>ApiKey.Password;
    public bool Remember=>RememberKey.IsChecked==true;
    public ModelDescriptor? SelectedModel=>ModelSelector.SelectedItem as ModelDescriptor;
    private bool loading;
    private bool mcpRunning;
    public StudioView()
    {
        InitializeComponent();
        using var stream=typeof(StudioView).Assembly.GetManifestResourceStream("AIArchiTec.Assets.logo.png");
        var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();RailLogo.Source=bitmap;
    }
    public void LoadSettings(ConnectionSettings value)
    {
        loading=true;SourceSelector.SelectedIndex=value.Source switch {"OpenAI"=>1,"OpenAI-compatible / local"=>2,_=>0};
        ApiUrl.Text=value.Endpoint;ApiKey.Password=value.ApiKey;RememberKey.IsChecked=value.RememberKey;loading=false;
    }
    public void SetCatalog(List<ModelDescriptor> models,string selected,string source)
    {
        loading=true;ModelSelector.ItemsSource=models;ModelSelector.DisplayMemberPath="Name";
        ModelSelector.SelectedItem=models.FirstOrDefault(m=>m.Id==selected)??models.FirstOrDefault();
        ModelSelector.Visibility=Visibility.Visible;loading=false;
        ConnectionLabel.Text="●  "+source+" connected";ConnectionLabel.Foreground=Brush("#6ACDAD");
    }
    public void SetOffline(){ModelSelector.Visibility=Visibility.Collapsed;ConnectionLabel.Text="●  Offline examples";ConnectionLabel.Foreground=Brush("#D0B378");}
    public void SetBusy(bool value)
    {BusyOverlay.Visibility=value?Visibility.Visible:Visibility.Collapsed;SendButton.IsEnabled=!value;ConnectButton.IsEnabled=!value;
        GenerateNav.IsEnabled=!value;AnalyzeNav.IsEnabled=!value;AssistNav.IsEnabled=!value;SourceSelector.IsEnabled=!value;ApiUrl.IsEnabled=!value;ApiKey.IsEnabled=!value;RememberKey.IsEnabled=!value;}
    public void SetContext(string value)=>ContextLabel.Text=value;
    public void SetStatus(string value)=>StatusLabel.Text=value;
    public void ApiMessage(string value)=>ApiStatus.Text=value;
    public void SetAttachment(string? value)
    {AttachmentLabel.Text=value is null?"":"↗  "+value+" · click attachment to remove";AttachmentLabel.Visibility=value is null?Visibility.Collapsed:Visibility.Visible;}
    public void AddMessage(string role,string value,bool error=false)
    {
        Welcome.Visibility=Visibility.Collapsed;
        var stack=new StackPanel();
        stack.Children.Add(new TextBlock{Text=role=="user"?"YOU":"ARCHI-TEC",FontSize=9,FontWeight=FontWeights.SemiBold,Foreground=Brush(role=="user"?"#AAB1CB":"#B799F4"),Margin=new Thickness(0,0,0,8)});
        stack.Children.Add(new TextBlock{Text=value,TextWrapping=TextWrapping.Wrap,LineHeight=20,Foreground=Brush(error?"#F0ABA9":"#E5E7F3"),
            FlowDirection=value.Any(c=>c>='\u0600'&&c<='\u06FF')?FlowDirection.RightToLeft:FlowDirection.LeftToRight});
        Conversation.Children.Add(new Border{Child=stack,Padding=new Thickness(13),Margin=new Thickness(role=="user"?22:0,0,role=="user"?0:10,12),CornerRadius=new CornerRadius(11),
            Background=Brush(role=="user"?"#202039":"#14182A"),BorderBrush=Brush(error?"#72434E":"#252B43"),BorderThickness=new Thickness(1)});
        while(Conversation.Children.Count>25)Conversation.Children.RemoveAt(1);
        ConversationScroll.ScrollToEnd();
    }
    public void ShowPreview(ActionPlan plan,string documentUnits)
    {
        PlanSummary.Text=plan.Summary;ActionCount.Text=$"{plan.Actions.Count} operations";
        PlanUnits.Text=$"{plan.LengthUnit} → {documentUnits}  ·  Rhino Undo supported";
        PlanDetails.Text=string.Join("\n",plan.Actions.Select((a,i)=>$"{i+1}. {a.Tool.Replace('_',' ')}"));PreviewCard.Visibility=Visibility.Visible;
    }
    public void HidePreview()=>PreviewCard.Visibility=Visibility.Collapsed;
    public void SetMcp(bool running,string? url=null,string? token=null)
    {mcpRunning=running;McpChip.Content=running?"MCP ●":"MCP ↗";McpStartButton.Content=running?"Stop MCP":"Start localhost MCP  →";
        McpStatus.Text=running?"Connected · local access protected by token":"Not connected";McpToken.Password=token??"";if(url is not null)McpUrl.Text=url;}
    public void CloseConnections()=>SettingsOverlay.Visibility=Visibility.Collapsed;
    public void ShowApi(){SettingsOverlay.Visibility=Visibility.Visible;ApiSettings.Visibility=Visibility.Visible;McpSettings.Visibility=Visibility.Collapsed;}
    private static SolidColorBrush Brush(string hex)=>new((Color)ColorConverter.ConvertFromString(hex));
    private void SetMode(string value)
    {ModeLabel.Text=value=="assist"?"MODEL ASSIST":value.ToUpperInvariant();SendButton.Content=(value=="analyze"?"Analyze":value=="assist"?"Assist":"Generate")+"  ↗";
        GenerateNav.Background=Brush(value=="generate"?"#38265D":"#00101222");AnalyzeNav.Background=Brush(value=="analyze"?"#38265D":"#00101222");AssistNav.Background=Brush(value=="assist"?"#38265D":"#00101222");ModeChanged?.Invoke(value);}
    private void SendClick(object sender,RoutedEventArgs e)=>SendRequested?.Invoke();
    private void ApplyClick(object sender,RoutedEventArgs e)=>ApplyRequested?.Invoke();
    private void CancelClick(object sender,RoutedEventArgs e)=>CancelRequested?.Invoke();
    private void AttachClick(object sender,RoutedEventArgs e)=>AttachRequested?.Invoke();
    private void ImportClick(object sender,RoutedEventArgs e)=>ImportRequested?.Invoke();
    private void ConnectClick(object sender,RoutedEventArgs e)=>ConnectRequested?.Invoke();
    private void OfflineClick(object sender,RoutedEventArgs e)=>OfflineRequested?.Invoke();
    private void McpStartClick(object sender,RoutedEventArgs e)=>McpStartRequested?.Invoke();
    private void CopyMcpClick(object sender,RoutedEventArgs e)=>CopyMcpRequested?.Invoke();
    private void OpenChatClick(object sender,RoutedEventArgs e)=>OpenChatRequested?.Invoke();
    private void SettingsClick(object sender,RoutedEventArgs e)=>ShowApi();
    private void CloseSettings(object sender,RoutedEventArgs e)=>CloseConnections();
    private void ApiTab(object sender,RoutedEventArgs e)=>ShowApi();
    private void McpClick(object sender,RoutedEventArgs e){if(!mcpRunning)McpStartRequested?.Invoke();ShowMcp();}
    private void McpTab(object sender,RoutedEventArgs e)=>ShowMcp();
    private void ShowMcp(){SettingsOverlay.Visibility=Visibility.Visible;ApiSettings.Visibility=Visibility.Collapsed;McpSettings.Visibility=Visibility.Visible;}
    private void GenerateMode(object sender,RoutedEventArgs e)=>SetMode("generate");
    private void AnalyzeMode(object sender,RoutedEventArgs e)=>SetMode("analyze");
    private void AssistMode(object sender,RoutedEventArgs e)=>SetMode("assist");
    private void BoxExample(object sender,RoutedEventArgs e){Prompt="Create a box 10 x 15 x 12 meters on layer AI_Building.";PromptInput.Focus();}
    private void BuildingExample(object sender,RoutedEventArgs e){Prompt="Create a 15 x 20 meter building with 5 floors, 3.2 meter floor heights and a glazed facade.";PromptInput.Focus();}
    private void PromptChanged(object sender,TextChangedEventArgs e)
    {if(PromptPlaceholder is not null)PromptPlaceholder.Visibility=string.IsNullOrWhiteSpace(PromptInput.Text)?Visibility.Visible:Visibility.Collapsed;InputChanged?.Invoke();}
    private void PromptKey(object sender,KeyEventArgs e){if(e.Key==Key.Enter&&(Keyboard.Modifiers&ModifierKeys.Control)!=0){e.Handled=true;SendRequested?.Invoke();}}
    private void ModelChanged(object sender,SelectionChangedEventArgs e){if(!loading)SelectedModelChanged?.Invoke();}
    private void SourceChanged(object sender,SelectionChangedEventArgs e)
    {if(!loading&&ApiUrl is not null){ApiUrl.Text=SourceSelector.SelectedIndex switch {1=>"https://api.openai.com/v1/",2=>"http://127.0.0.1:1234/v1/",_=>"https://openrouter.ai/api/v1/"};ApiKey.Password="";}}
}
