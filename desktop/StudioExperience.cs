using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
namespace Studio;

public partial class StudioWindow
{
    readonly VoiceInput voice=new();
    string AppName=>(Host.SelectedItem as ListBoxItem)?.Tag as string ?? "Rhino";
    string VersionName=>Edition.SelectedItem as string ?? "Automatic";
    static Brush ColorBrush(string hex)=>(Brush)new BrushConverter().ConvertFromString(hex)!;
    void Row(string name,string mark,string color)
    {
        var panel=new StackPanel{Orientation=Orientation.Horizontal};
        var icon=HostSetup.Find(installs,name,"Automatic")?.Icon();
        if(icon!=null)panel.Children.Add(new Image{Source=icon,Width=31,Height=27,Stretch=Stretch.Uniform});
        else panel.Children.Add(HostMark.Draw(name,mark,color));
        panel.Children.Add(new TextBlock{Text=name=="Both"?"Rhino + Revit":name,Margin=new Thickness(9,0,0,0),VerticalAlignment=VerticalAlignment.Center});
        Host.Items.Add(new ListBoxItem{Tag=name,Content=panel});
    }
    bool switching;
    void SelectEdition()
    {
        switching=true;
        Edition.ItemsSource=ApplicationCatalog.Find(AppName)?.Editions??["Automatic"];
        Edition.SelectedIndex=0; switching=false;
        RefreshTarget();
    }
    void VersionChanged(object sender,SelectionChangedEventArgs e){if(!switching && Messages!=null && Edition.SelectedItem!=null)RefreshTarget();}
    void DocsClick(object s,RoutedEventArgs e)
    {
        var uri=ApplicationCatalog.Find(AppName)?.Docs;
        if(uri!=null)Process.Start(new ProcessStartInfo(uri){UseShellExecute=true});
    }
    static bool IsImage(string path)=>Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg";
    static bool IsTextReference(string path)=>Path.GetExtension(path).ToLowerInvariant() is ".txt" or ".md" or ".csv" or ".json";
    static bool ValidReference(FileInfo file)=>file.Exists && (IsImage(file.Name) && file.Length<=6_000_000 || IsTextReference(file.Name) && file.Length<=96_000);
    void ImageDrag(object sender,DragEventArgs e)
    {
        e.Effects=!busy && e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;e.Handled=true;
    }
    void ImageDrop(object sender,DragEventArgs e)
    {
        e.Handled=true;
        if(busy || e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length!=1)return;
        var f=new FileInfo(files[0]);
        if(!ValidReference(f)){Status.Text="Choose an image under 6 MB or a text reference under 96 KB.";return;}
        attachment=f.FullName;DisplayAttachment();
    }
    void DisplayAttachment()
    {
        AttachmentBar.Visibility=Visibility.Collapsed;Thumb.Source=null;
        if(attachment==null)return;
        if(IsTextReference(attachment)){FileLabel.Text=Path.GetFileName(attachment);Thumb.Visibility=Visibility.Collapsed;AttachmentBar.Visibility=Visibility.Visible;return;}
        Thumb.Visibility=Visibility.Visible;
        try{
            var image=new BitmapImage();
            using var stream=File.OpenRead(attachment);image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=stream;image.DecodePixelWidth=140;image.EndInit();image.Freeze();
            Thumb.Source=image;FileLabel.Text=Path.GetFileName(attachment);AttachmentBar.Visibility=Visibility.Visible;
        }catch{attachment=null;Status.Text="Image could not be opened.";}
    }
    void Shortcuts()
    {
        var panel=new WrapPanel{Tag="welcome",Margin=new Thickness(8,3,0,12)};
        void Draft(string title,string prompt){
            var button=new Button{Content=title+"   →",Background=ColorBrush("#212127"),ToolTip=prompt};
            button.Click+=(_,_)=>{if(!busy){Mode.SelectedIndex=0;Prompt.Text=prompt;Prompt.Focus();Prompt.CaretIndex=prompt.Length;}};
            panel.Children.Add(button);
        }
        Draft("Inspect selection","Inspect my active document and describe the selected elements.");
        if(AppName=="Revit")Draft("Model a wall","Create a 5 m straight wall on the first available level, 3 m high.");
        else if(AppName=="Rhino")Draft("Design a mass","Create a stepped architectural mass with three editable solid volumes.");
        else if(SceneHost.Supports(Target))Draft("Model a pavilion","Create an editable triangular mesh for a folded pavilion roof. Inspect scene units first.");
        else if(WindowsAutomation.Supports(Target))Draft("Create a layer","Inspect my active document and create a layer named Design review.");
        Messages.Children.Add(panel);
    }
    void OpenConnectorFolder(object s,RoutedEventArgs e)
    {
        var folder=Path.Combine(AppContext.BaseDirectory,"Connectors");
        if(Directory.Exists(folder))Process.Start(new ProcessStartInfo(folder){UseShellExecute=true});
    }
    void RemoveAttachment(object s,RoutedEventArgs e){if(!busy){attachment=null;DisplayAttachment();}}
    string? VoiceKey()
    {
        var key=SpeechKey.Password.Trim();
        if(key.Length==0 && Service is ("OpenAI" or "OpenRouter"))key=Key.Password.Trim();
        if(key.Length!=0)return key;
        Settings.IsExpanded=true;
        Status.Text=ModelTestStatus.Text="Enter your OpenRouter or OpenAI provider key for integrated recording, or an OpenAI dictation key.";
        return null;
    }
    async Task Dictate(string path,string key)
    {
        var text=await VoiceInput.Transcribe(path,key,Service=="OpenRouter" && string.IsNullOrWhiteSpace(SpeechKey.Password));
        Prompt.AppendText((Prompt.Text.Length>0?" ":"")+text);Prompt.CaretIndex=Prompt.Text.Length;Prompt.Focus();
    }
    async void VoiceClick(object s,RoutedEventArgs e)
    {
        if(busy)return;
        var key=VoiceKey();if(key==null)return;
        try{
            if(!voice.Recording){voice.Start();Mic.Content=new StudioGlyph{Kind="stop"};Status.Text="Recording in Studio · click Stop to transcribe";Host.IsEnabled=Edition.IsEnabled=Settings.IsEnabled=SendButton.IsEnabled=false;return;}
            string path=voice.Stop();Mic.Content=new StudioGlyph{Kind="mic"};
            try{await Run(()=>Dictate(path,key));}finally{File.Delete(path);}
        }catch(Exception ex){Status.Text=ex.Message;}
        finally{if(!voice.Recording){Mic.Content=new StudioGlyph{Kind="mic"};Host.IsEnabled=Edition.IsEnabled=Settings.IsEnabled=SendButton.IsEnabled=true;}}
    }
}
