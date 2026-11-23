using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace Studio;

public partial class StudioWindow
{
    void SettingsOpened(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource != Settings) return;
        TopActions.IsEnabled = WorkspaceRoot.IsEnabled = false;
        ChatModel.Focus();
    }
    void CloseSettings(object sender, RoutedEventArgs e) => Settings.IsExpanded = false;
    void SettingsClosed(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource != Settings) return;
        TopActions.IsEnabled = WorkspaceRoot.IsEnabled = true;
        Prompt.Focus();
    }
    void WorkspaceKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape && Settings.IsExpanded)
        {
            Settings.IsExpanded = false;
            e.Handled = true;
        }
    }

    static Button PanelAction(string icon, string title, Action action)
    {
        var line = new StackPanel { Orientation = Orientation.Horizontal };
        line.Children.Add(new StudioGlyph { Kind = icon });
        line.Children.Add(new TextBlock { Text=title, Margin=new Thickness(12,0,0,0), VerticalAlignment=VerticalAlignment.Center });
        var button = new Button {Content=line, HorizontalContentAlignment=HorizontalAlignment.Left, Padding=new Thickness(12,9,12,9)};
        System.Windows.Automation.AutomationProperties.SetName(button,title);
        button.Click += (_,_) => action();
        return button;
    }

    void OpenStudioPage(string page)
    {
        var dialog=new Window {Owner=this, Title=page, Width=398, MaxHeight=SystemParameters.WorkArea.Height-40,
            SizeToContent=SizeToContent.Height, ResizeMode=ResizeMode.NoResize, ShowInTaskbar=false, Icon=Icon,
            WindowStartupLocation=WindowStartupLocation.CenterOwner, Background=ColorBrush("#16161D"), Foreground=Foreground,
            FontFamily=FontFamily, FontSize=13, Resources=new ResourceDictionary {Source=new Uri("/ArchiTecStudio;component/StudioTheme.xaml",UriKind.Relative)}};
        dialog.SourceInitialized += (_,_) => DarkCaption(new WindowInteropHelper(dialog).Handle);
        dialog.PreviewKeyDown += (_,e) => { if(e.Key==System.Windows.Input.Key.Escape) dialog.Close(); };
        var body=new StackPanel {Margin=new Thickness(22)};
        dialog.Content=new ScrollViewer {Content=body, VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        var artwork=new Border {Height=74, CornerRadius=new CornerRadius(9), Background=new ImageBrush(new System.Windows.Media.Imaging.BitmapImage(
            new Uri("pack://application:,,,/ArchiTecStudio;component/Assets/hero-wave.jpg"))) {Stretch=Stretch.UniformToFill}};
        body.Children.Add(artwork);
        body.Children.Add(new TextBlock {Text=page, FontWeight=FontWeights.SemiBold, FontSize=24, Margin=new Thickness(2,16,0,5)});
        void Detail(string text) => body.Children.Add(new TextBlock {Text=text, TextWrapping=TextWrapping.Wrap, Foreground=ColorBrush("#C1BBCD"), Margin=new Thickness(2,0,0,12)});
        void ActionRow(string icon,string label,Action action) =>
            body.Children.Add(PanelAction(icon,label,()=>{dialog.Close();action();}));
        switch(page)
        {
            case "Studio account":
                Detail("OpenRouter · Verified session");
                Detail("Studio version "+typeof(StudioWindow).Assembly.GetName().Version?.ToString(3));
                ActionRow("link","Application connections",()=>{ShowChat();ConnectPanel.Visibility=Visibility.Visible;UpdateSetup();});
                ActionRow("settings","Model & API settings",()=>{ShowChat();Settings.IsExpanded=true;});
                ActionRow("apps","Open web workspace",()=>PlatformClick(this,new RoutedEventArgs()));
                ActionRow("book","About the studio",()=>OpenStudioPage("About the studio"));
                ActionRow("mail","Contact the developer",()=>OpenStudioPage("Contact"));
                ActionRow("exit","End session",Close);
                break;
            case "About the studio":
                Detail("Hasan Sharif Askari · Architect and developer");
                Detail("I built AI Archi-Tec to connect architectural design conversations to editable documents. Studio brings the project, its modeling tools and the AI conversation into one workspace.");
                ActionRow("book","Explore AI Archi-Tec",()=>OpenLink("https://ai-arch.ir/home"));
                ActionRow("mail","Contact",()=>OpenStudioPage("Contact"));
                break;
            case "Contact":
                Detail("Contact Hasan about AI Archi-Tec Studio or its application connectors.");
                var email=new TextBox {Text="contact.archtech@gmail.com", IsReadOnly=true, Margin=new Thickness(0,2,0,8)};
                body.Children.Add(email);
                ActionRow("mail","Write an email",()=>OpenLink("mailto:contact.archtech@gmail.com"));
                break;
        }
        dialog.ShowDialog();
    }
}
