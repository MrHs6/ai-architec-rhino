using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Studio;
internal static class Onboarding
{
    internal static async Task Check(string directory)
    {
        var device=BrowserSignIn.CreateDevice();
        var url=new Uri(BrowserSignIn.SiteUrl(device.Fingerprint));
        if(url.Scheme!="https" || url.Host!="ai-arch.ir" || url.AbsolutePath!="/desktop/sign-in" || url.Fragment!="#device="+device.Fingerprint || url.AbsoluteUri.Contains(device.Secret))throw new Exception("Incorrect device sign-in link.");
        using var stop=new CancellationTokenSource();
        try{await BrowserSignIn.Run(stop.Token,_=>stop.Cancel(),_=>{});throw new Exception("Cancelled device flow continued.");}
        catch(OperationCanceledException) when(stop.IsCancellationRequested){}
        Exception? error=null;
        var thread=new Thread(()=>{
            try{
                var app=new Application();
                var welcome=new WelcomeWindow();
                Render((FrameworkElement)welcome.Content,420,485,Path.Combine(directory,"Studio-0.6.1-Sign-in.png"));
                var workspace=new StudioWindow(new StudioMember(Guid.NewGuid(),"Design studio","Licensed workspace"));
                Render((FrameworkElement)workspace.Content,510,680,Path.Combine(directory,"Studio-0.6.1-Connections.png"));
                var targets=(WrapPanel)workspace.FindName("HostTiles");
                Console.WriteLine("Detected apps: "+string.Join(", ",targets.Children.Cast<Button>().Select(t=>t.Tag)));
                var settings=(Expander)workspace.FindName("Settings");
                settings.IsExpanded=true;
                Render((FrameworkElement)workspace.Content,510,680,Path.Combine(directory,"Studio-0.6.1-API.png"));
                if(((FrameworkElement)workspace.FindName("WorkspaceRoot")).IsEnabled || !((PasswordBox)workspace.FindName("Key")).IsEnabled)
                    throw new Exception("Modal settings focus or provider key inaccessible.");
                settings.IsExpanded=false;
                var mode=(ListBox)workspace.FindName("Mode");
                if(mode.SelectedIndex!=1 || mode.Items.Count!=2)throw new Exception("Chat / Build selector failed.");
                workspace.Show();
                Exception? dialogError=null;
                workspace.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,new Action(()=>{
                    try{
                        var account=workspace.OwnedWindows.Cast<Window>().Single();
                        try {
                            if(account.Title!="Studio account")throw new Exception("Account panel failed.");
                            Render((FrameworkElement)account.Content,382,515,Path.Combine(directory,"Studio-0.6.1-Account.png"));
                        }finally{account.Close();}
                    }catch(Exception ex){dialogError=ex; foreach(Window w in workspace.OwnedWindows)w.Close();}
                }));
                ((Button)workspace.FindName("AccountButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if(dialogError!=null)throw dialogError;
                workspace.Close();welcome.Close();
            }catch(Exception ex){error=ex;}
        });
        thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();
        if(error!=null)throw error;
        Console.WriteLine("PASS sign-in artwork, browser handoff, cancelled device authorization and connection selector.");
    }
    static void Render(FrameworkElement view,int w,int h,string path)
    {
        view.Measure(new Size(w,h));view.Arrange(new Rect(0,0,w,h));view.UpdateLayout();
        var bitmap=new RenderTargetBitmap(w,h,96,96,PixelFormats.Pbgra32);
        var background=new DrawingVisual();
        using(var draw=background.RenderOpen()){draw.DrawRectangle(new SolidColorBrush(Color.FromRgb(16,17,21)),null,new Rect(0,0,w,h));}
        bitmap.Render(background);bitmap.Render(view);
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));
        using var file=File.Create(path);png.Save(file);
    }
}
