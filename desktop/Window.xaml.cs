using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Studio;

public partial class StudioWindow : Window
{
    readonly Workspace studio = new();
    StudioMember? member;
    internal StudioWindow(StudioMember account):this(){member=account; BuildTiles(); AccountButton.ToolTip=account.Email+" · "+account.Access; Width=540;Height=735;}
    JsonArray history = History();
    string? attachment;
    bool busy;
    string? selectedProvider;
    readonly Dictionary<string,(string Model,string Key)> accounts=new();
    private sealed record Conversation(JsonArray History, UIElement[] Items, string Draft, string? Attachment);
    readonly Dictionary<string,Conversation> conversations=new();
    string? previousTarget;
    static JsonArray History() => new(Wire.Node(new { role="system", content=Workspace.SystemPrompt }));
    string Target => VersionName=="Automatic" || (AppName=="Revit" && VersionName=="2025") ? AppName : AppName+" · "+VersionName;
    string Service => ((ComboBoxItem)Provider.SelectedItem).Content.ToString()!;
    public StudioWindow()
    {
        InitializeComponent();
        foreach(var app in ApplicationCatalog.All) Row(app.Name,app.Mark,app.Color);
        foreach(var profile in HostProfile.Load().Where(p=>ApplicationCatalog.Find(p.Name)==null && !p.Name.Contains(" · "))) Row(profile.Name,"↗","#A59CE1");
        Provider.SelectedIndex = 0;
        var revitSessions=System.Diagnostics.Process.GetProcessesByName("Revit");
        Host.SelectedIndex=revitSessions.Length==1 ? 1 : 0;
        foreach(var process in revitSessions)process.Dispose();
        Loaded+=(_,_)=>SetRail(ActualWidth>=950);
        SizeChanged+=(_,_)=>SetRail(Rail.Visibility==Visibility.Visible);
        Closed += (_,_) => { closingStudio=true;platform?.Dispose();voice.Dispose();studio.Dispose(); };
    }
    void ProviderChanged(object s, SelectionChangedEventArgs e)
    {
        if (Key == null) return;
        if(selectedProvider!=null && selectedProvider!="Compatible API")accounts[selectedProvider]=(Model.Text,Key.Password);
        selectedProvider=Service;Key.Clear();EndpointPanel.Visibility=Service=="Compatible API"?Visibility.Visible:Visibility.Collapsed;
        CodexLogin.Visibility=Service==CodexModel.Provider?Visibility.Visible:Visibility.Collapsed;
        Key.IsEnabled=Model.IsEnabled=Service!=CodexModel.Provider;
        ModelTestStatus.Text=Service==CodexModel.Provider?"Uses the signed-in Codex CLI on this PC. Run codex login for first-time setup.":"";
        Model.Text = Service switch { CodexModel.Provider=>"Codex default", "Compatible API"=>"","Gemini"=>"gemini-2.5-flash","OpenAI"=>"gpt-4.1-mini","Anthropic"=>"claude-sonnet-4-6",_=>"openai/gpt-4.1-mini" };
        if(accounts.TryGetValue(Service,out var profile)){Model.Text=profile.Model;Key.Password=profile.Key;}
    }
    void TargetChanged(object s, SelectionChangedEventArgs e)
    {
        if(Messages==null || Host.SelectedItem==null)return;
        SelectEdition();if(ActualWidth>0 && ActualWidth<750)SetRail(false);
    }
    void RefreshTarget()
    {
        UpdateSetup();
        HostTitle.Text=AppName=="Both"?"Rhino + Revit":AppName;
        HostDescription.Text=(ApplicationCatalog.Find(AppName)?.Interface ?? "Custom MCP server")+"   ·   "+Connections.Route(Target);
        ViewButton.Visibility=AppName is "Rhino" or "Both" ? Visibility.Visible : Visibility.Collapsed;
        if(previousTarget!=null) conversations[previousTarget]=new(history,Messages.Children.Cast<UIElement>().ToArray(),Prompt.Text,attachment);
        Messages.Children.Clear();
        previousTarget=Target;
        if(!conversations.TryGetValue(Target,out var thread)){Reset();return;}
        history=thread.History;attachment=thread.Attachment;Prompt.Text=thread.Draft;
        foreach(var item in thread.Items) Messages.Children.Add(item);
        DisplayAttachment(); Status.Text=Target+"  ·  Conversation restored";Scroll.ScrollToEnd();
    }
    void PinClick(object s, RoutedEventArgs e) => Topmost = Pin.IsChecked == true;
    void Reset()
    {
        history = History(); Messages.Children.Clear(); attachment=null; DisplayAttachment(); Prompt.Clear();
        var welcome=new StackPanel{Margin=new Thickness(10,24,10,12),Tag="welcome"};
        welcome.Children.Add(new TextBlock{Text="What are you working on?",FontWeight=FontWeights.SemiBold,FontSize=26,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,6)});
        welcome.Children.Add(new TextBlock{Foreground=ColorBrush("#B8B1C6"),MaxWidth=550,HorizontalAlignment=HorizontalAlignment.Left,TextWrapping=TextWrapping.Wrap,Text=Target switch { "Revit"=>"Inspect the open project to see its levels and selection. Use Build to create a wall or change its height.", "Both"=>"Inspect both documents. In Build you can ask to copy a straight Revit wall axis into Rhino using the same world coordinates.", "Rhino"=>"Open a Rhino document and enable the Archi-Tec MCP connection. Inspect document to read its units and selection.", _=>Connections.Route(Target)=="Connector required"?"Configure a local MCP server for this application and edition to enable document tools. Chat and reference images are available immediately.":"Inspect your active document or discover its editing tools." }});Messages.Children.Add(welcome);
        Shortcuts(); Status.Text = Target + "  ·  Ready";
    }
    void Clear(object s, RoutedEventArgs e) { if (!busy) Reset(); }
    void Message(string label,string body)
    {
        foreach(var empty in Messages.Children.OfType<FrameworkElement>().Where(c=>Equals(c.Tag,"welcome")).ToArray())Messages.Children.Remove(empty);
        var stack = new StackPanel { Margin=new Thickness(2,6,4,8) };
        stack.Children.Add(new TextBlock { Text=label, Foreground=ColorBrush("#BFAAD9"), FontSize=12, FontWeight=FontWeights.SemiBold });
        if(label=="Archi-Tec"){
            ChatOutput.Render(stack,body,SaveText);
            var save=new Button{Content="Save response…",HorizontalAlignment=HorizontalAlignment.Left};
            save.Click+=(_,_)=>SaveText("response.md",body);
            var actions=new WrapPanel();
            var copy=new Button{Content="Copy",ToolTip="Copy the complete response"};
            copy.Click+=(_,_)=>{try{Clipboard.SetText(body);Status.Text="Response copied";}catch(System.Runtime.InteropServices.ExternalException){Status.Text="Clipboard is busy. Try again."; }};
            actions.Children.Add(copy);actions.Children.Add(save);stack.Children.Add(actions);
        }else if(label=="Tool receipt")stack.Children.Add(new Expander{Header="Execution details",Foreground=Brushes.Lavender,Content=ChatOutput.Text(body,true)});
        else stack.Children.Add(ChatOutput.Text(body));
        stack.Margin=new Thickness(14,10,14,10);
        Messages.Children.Add(new Border {CornerRadius=new CornerRadius(10),Margin=label=="You"?new Thickness(48,3,0,7):new Thickness(0,3,22,7),Background=ColorBrush(label=="You"?"#292331":"#15161B"),Child=stack}); Scroll.ScrollToEnd();
    }
    void Preview(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption=BitmapCacheOption.OnLoad; bitmap.StreamSource=stream; bitmap.EndInit(); bitmap.Freeze();
        var card=new StackPanel();card.Children.Add(new Image { Source=bitmap,MaxHeight=320,Stretch=Stretch.Uniform });
        var save=new Button{Content="Save image…",HorizontalAlignment=HorizontalAlignment.Left};
        save.Click+=(_,_)=>{
            var dialog=new SaveFileDialog{FileName="image.png",Filter="PNG image|*.png",InitialDirectory=outputFolder};
            if(dialog.ShowDialog(this)!=true)return;
            try{var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var output=File.Create(dialog.FileName);encoder.Save(output);Status.Text="Saved · "+dialog.FileName;}catch(Exception ex){Status.Text=ex.Message;}
        };card.Children.Add(save);Messages.Children.Add(card); Scroll.ScrollToEnd();
    }
    async Task Run(Func<Task> action)
    {
        if (busy || voice.Recording) return;
        activity=null;currentStep=null;busy=true; Host.IsEnabled=Edition.IsEnabled=Settings.IsEnabled=Mode.IsEnabled=SendButton.IsEnabled=false;
        Status.Text="Working with "+Target+"…";
        try { if(member!=null && (await StudioAccount.Verify()).Id!=member.Id)throw new UnauthorizedAccessException("The signed-in account changed. Reopen Studio."); await action(); Status.Text=Target+"  ·  Ready"; }
        catch(OperationCanceledException){Activity("Stopped · review any completed host operations","failed");}
        catch(Exception ex) { Activity(ex.Message,"failed");Status.Text=ex.Message; }
        finally { busy=false; Host.IsEnabled=Edition.IsEnabled=Settings.IsEnabled=Mode.IsEnabled=SendButton.IsEnabled=true; }
    }
    async void Inspect(object s,RoutedEventArgs e) => await Run(async () => {
        if(Connections.Route(Target) is "Windows automation" or "Scene connector" or "Archicad JSON"){Message(Target,Wire.Describe(await studio.Call(Target,"get_context",new JsonObject())));return;}
        if(AppName=="Grasshopper" && Connections.Route(Target)=="Rhino bridge"){
            var ctx=await studio.Call(Target,"get_context",new JsonObject());
            if((bool?)ctx["isError"]==true)throw new IOException(Wire.Describe(ctx));
            var id=JsonNode.Parse(Wire.Describe(ctx))!["document_id"];
            Message(AppName,Wire.Describe(await studio.Call(Target,"get_grasshopper_context",new JsonObject{["document_id"]=id!.DeepClone()})));return;
        }
        if(AppName=="Rhino" && Target!="Rhino" && Connections.Route(Target)=="Rhino bridge"){
            var result=await studio.Call(Target,"get_context",new JsonObject());
            if ((bool?)result["isError"]==true) throw new IOException(Wire.Describe(result));
            Message(Target,Wire.Describe(result));return;
        }
        if(Target is not ("Both" or "Rhino" or "Revit")){
            var catalog=await studio.Tools(Target); Message(Target,string.Join("\n",catalog.Select(t=>(string?)t!["name"]+" · "+(string?)t["description"])));return;
        }
        foreach(var h in Target=="Both"?new[]{"Rhino","Revit"}:new[]{Target})
        {
            Activity("Reading document · "+h,"running");
            try { var r=await studio.Call(h,h.ToLowerInvariant()+"_get_context",new JsonObject()); Message(h,Wire.Describe(r));Activity(h+" document inspection",(bool?)r["isError"]==true?"failed":"done"); }
            catch(Exception ex){Activity(h+" · "+ex.Message,"failed");Message(h,ex.Message);}
        }
    });
    async void Viewport(object s,RoutedEventArgs e) => await Run(async () => {
        if(AppName is not ("Both" or "Rhino")) throw new InvalidOperationException("Select Rhino to capture its viewport.");
        var host=Target=="Both"?"Rhino":Target;
        var prefix=host=="Rhino"?"rhino_":"";
        var context=await studio.Call(host,prefix+"get_context",new JsonObject());
        if ((bool?)context["isError"]==true) throw new IOException(Wire.Describe(context));
        var doc=JsonNode.Parse(Wire.Describe(context))!["document_id"] ?? throw new IOException("Rhino returned no document ID.");
        var r=await studio.Call(host,prefix+"get_viewport_image",new JsonObject{["document_id"]=doc.DeepClone()});
        var image=r["content"]!.AsArray().FirstOrDefault(x=>(string?)x?["type"]=="image");
        if(image==null) throw new IOException(Wire.Describe(r));
        Preview(Convert.FromBase64String((string)image["data"]!));
    });
    async void SignInCodex(object s,RoutedEventArgs e){
        if(busy)return;
        ModelTestStatus.Text="Finish signing in to Codex in your browser.";
        bool signedIn=false;
        await Run(async()=>{await CodexModel.Login();signedIn=true;});
        ModelTestStatus.Text=signedIn?"Signed in. Use Test connection to verify the model.":Status.Text;
    }
    async void ConnectCodex(object s,RoutedEventArgs e) => await Run(async()=>{
        await CodexModel.Register(Target);
        Message("Codex MCP",Target+" is registered. Open a new Codex chat and ask it to inspect the active document. Keep the design application and its bridge open.");
    });
    void Config(object s,RoutedEventArgs e)
    {
        var config=JsonSerializer.Serialize(new { mcpServers=new { architec=new {command=Environment.ProcessPath,args=new[]{"--mcp",Target}} } },new JsonSerializerOptions{WriteIndented=true});
        var codex="codex mcp add architec -- \""+Environment.ProcessPath+"\" --mcp \""+Target+"\"";
        try { Clipboard.SetText(config); } catch(System.Runtime.InteropServices.ExternalException) { /* Both configurations are also selectable below. */ }
        Message("Claude Desktop / Gemini CLI · merge into mcpServers settings",config); Message("Codex · terminal command",codex);
    }
    void Attach(object s,RoutedEventArgs e)
    {
        if(busy)return;
        var dialog=new OpenFileDialog{Filter="References|*.png;*.jpg;*.jpeg;*.txt;*.md;*.csv;*.json"};
        if(dialog.ShowDialog()==true) { if(!ValidReference(new FileInfo(dialog.FileName))){Status.Text="Choose an image under 6 MB or a text reference under 96 KB.";return;} attachment=dialog.FileName; DisplayAttachment(); if(attachment!=null)Status.Text="Reference attached · "+Path.GetFileName(attachment); }
    }
    void AddHost(object s,RoutedEventArgs e)
    {
        if(busy || voice.Recording)return;
        var existing=HostProfile.Load().FirstOrDefault(p=>p.Name==Target);
        var name=new TextBox{Text=Connections.Route(Target) is "Connector required" or "Configured MCP"?Target:""};
        var executable=new TextBox{Text=existing?.Command??"",IsReadOnly=true};
        var args=new TextBox{Text=JsonSerializer.Serialize(existing?.Args??Array.Empty<string>())};
        var form=new StackPanel{Margin=new Thickness(22)};
        var dialog=new Window{Title="Application connector",Owner=this,Icon=Icon,Content=form,Width=412,SizeToContent=SizeToContent.Height,
            ResizeMode=ResizeMode.NoResize,Background=ColorBrush("#191920"),Foreground=Foreground,WindowStartupLocation=WindowStartupLocation.CenterOwner,
            Resources=new ResourceDictionary {Source=new Uri("/ArchiTecStudio;component/StudioTheme.xaml",UriKind.Relative)}};
        dialog.SourceInitialized+=(_,_)=>DarkCaption(new System.Windows.Interop.WindowInteropHelper(dialog).Handle);
        form.Children.Add(new TextBlock{Text="Application connector",FontSize=22,FontWeight=FontWeights.SemiBold});
        form.Children.Add(new TextBlock{Text="Select a local MCP server that connects Studio to your design application.",TextWrapping=TextWrapping.Wrap,Foreground=ColorBrush("#B9B2C7"),Margin=new Thickness(0,4,0,12)});
        void Label(string text)=>form.Children.Add(new TextBlock{Text=text,Margin=new Thickness(0,8,0,4)});
        Label("Application name");form.Children.Add(name);
        Label("MCP server executable");form.Children.Add(executable);
        form.Children.Add(PanelAction("link","Choose executable…",()=>{
            var file=new OpenFileDialog {Title="Choose the local MCP server",Filter="Windows MCP server|*.exe"};
            if(file.ShowDialog(dialog)!=true)return;
            executable.Text=file.FileName;
            if(string.IsNullOrWhiteSpace(name.Text))name.Text=Path.GetFileNameWithoutExtension(file.FileName);
        }));
        Label("Server arguments · JSON array");form.Children.Add(args);
        var save=new Button{Content="Add connection",Margin=new Thickness(0,10,0,0)};form.Children.Add(save);
        save.Click+=(_,_)=>{
            try {
                var label=name.Text.Trim();
                if(label.Length==0 || label is "Rhino" or "Revit" or "Both")throw new ArgumentException("Choose a unique application name.");
                var parameters=JsonSerializer.Deserialize<string[]>(args.Text)??throw new ArgumentException("Enter an array of strings.");
                if(parameters.Any(p=>p==null))throw new ArgumentException("Arguments must be strings.");
                if(!File.Exists(executable.Text) || !Path.GetExtension(executable.Text).Equals(".exe",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Select an existing MCP server executable.");
                if(label.Contains(" · ") && label!=Target)throw new ArgumentException("Select a catalog edition before adding its connector.");
                HostProfile.Save(new(label,executable.Text,parameters));
                studio.ResetConnections();
                if(label!=Target && !Host.Items.Cast<ListBoxItem>().Any(i=>(string)i.Tag==label)) Row(label,"↗","#BDACF8");
                var pick=Host.Items.Cast<ListBoxItem>().FirstOrDefault(i=>(string)i.Tag==label);
                if(pick!=null)Host.SelectedItem=pick;
                HostDescription.Text=(ApplicationCatalog.Find(AppName)?.Interface??"MCP")+"   ·   "+Connections.Route(Target);
                UpdateSetup();
                dialog.DialogResult=true;
            }catch(Exception ex){MessageBox.Show(dialog,ex.Message);}
        };
        dialog.ShowDialog();
    }
    void OnKey(object s,KeyEventArgs e) { if(e.Key==System.Windows.Input.Key.Enter && Keyboard.Modifiers!=ModifierKeys.Shift && !e.ImeProcessedKey.Equals(System.Windows.Input.Key.Enter)){ e.Handled=true; Send(s,e); } }
    async void Send(object s,RoutedEventArgs e)
    {
        if(busy || voice.Recording || (string.IsNullOrWhiteSpace(Prompt.Text) && attachment==null))return;
        if((Service is not ("Compatible API" or CodexModel.Provider) && string.IsNullOrWhiteSpace(Key.Password))||string.IsNullOrWhiteSpace(Model.Text)){Settings.IsExpanded=true; Status.Text="Enter a provider API key and model.";return;}
        await Run(async()=>{
            var content=new JsonArray(Wire.Node(new{type="text",text=Prompt.Text}));
            if(attachment!=null && IsTextReference(attachment)){
                if(!ValidReference(new FileInfo(attachment)))throw new IOException("The attached reference changed. Attach it again.");
                var reference=await File.ReadAllTextAsync(attachment,new UTF8Encoding(false,true));
                content.Add(Wire.Node(new{type="text",text="Attached reference data ("+Path.GetFileName(attachment)+"):\n"+reference}));
            }else if(attachment!=null){
                if(new FileInfo(attachment).Length>6_000_000)throw new IOException("Choose an image under 6 MB.");
                var bytes=await File.ReadAllBytesAsync(attachment);
                string mime=Path.GetExtension(attachment).Equals(".png",StringComparison.OrdinalIgnoreCase)?"image/png":"image/jpeg";
                content.Add(Wire.Node(new{type="image_url",image_url=new{url="data:"+mime+";base64,"+Convert.ToBase64String(bytes)}}));
                Preview(bytes);
            }
            var turn=(JsonArray)history.DeepClone();turn.Add(Wire.Node(new{role="user",content}));
            Message("You",Prompt.Text);
            string reply;
            using var cancellation=new CancellationTokenSource();turnCancellation=cancellation;StopButton.Visibility=Visibility.Visible;
            TextBox? live=null;
            void AppendText(string token){if(live==null){live=ChatOutput.Text("");Messages.Children.Add(live);}live.AppendText(token);Scroll.ScrollToEnd();}
            try { reply=await studio.Chat(Target,Service,Model.Text.Trim(),Key.Password.Trim(),turn,Mode.SelectedIndex==0,x=>Message("Tool receipt",x),Preview,Activity,Endpoint.Text,cancellation.Token,AppendText); }
            catch when(turn.Skip(history.Count+1).Any(n=>(string?)n?["role"]=="tool"))
            {
                history=turn;Prompt.Clear();attachment=null;
                Message("Execution interrupted","The tool receipts above are retained. Inspect the document before resuming.");
                throw;
            }
            finally{turnCancellation=null;StopButton.Visibility=Visibility.Collapsed;}
            if(live!=null)Messages.Children.Remove(live);
            history=turn; Prompt.Clear(); attachment=null; DisplayAttachment(); Message("Archi-Tec",reply);
        });
    }
}

internal static class Program
{
    [STAThread] static int Main(string[] args)
    {
        if(args.Length==2 && args[0]=="--mcp")
        {
            Console.SetIn(new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false)));
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush=true });
            Server.Run(args[1]).GetAwaiter().GetResult();return 0;
        }
        var app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
        var signIn=new WelcomeWindow();
        if(signIn.ShowDialog()!=true || signIn.Member==null){app.Shutdown();return 0;}
        var window=new StudioWindow(signIn.Member);
        app.MainWindow=window;
        app.ShutdownMode=ShutdownMode.OnMainWindowClose;
        return app.Run(window);
    }
}
