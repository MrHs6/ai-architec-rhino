using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
namespace Studio;

public partial class WelcomeWindow : Window
{
    readonly CancellationTokenSource lifetime=new();
    internal StudioMember? Member {get;private set;}
    public WelcomeWindow()
    {
        InitializeComponent();
        Loaded+=async (_,_)=>{
            if(!StudioAccount.HasSession)return;
            LoginButton.IsEnabled=false;
            LoginStatus.Text="Checking your workspace…";
            try { Complete(await StudioAccount.Verify()); }
            catch(Exception ex) { LoginStatus.Text=ex.Message; }
            finally { LoginButton.IsEnabled=true; }
        };
        Closed+=(_,_)=>lifetime.Cancel();
    }
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        StudioWindow.DarkCaption(new WindowInteropHelper(this).Handle);
    }
    static void Browse(string url)=>Process.Start(new ProcessStartInfo(url){UseShellExecute=true});
    void RequestAccess(object sender,RoutedEventArgs e)
    {
        try{Browse("https://ai-arch.ir/request-access");}catch(Exception ex){LoginStatus.Text=ex.Message;}
    }
    void Complete(StudioMember member)
    {
        if(lifetime.IsCancellationRequested)return;
        Member=member; DialogResult=true;
    }
    async void Login(object sender,RoutedEventArgs e)
    {
        LoginButton.IsEnabled=false;
        LoginStatus.Text="Finish signing in in your browser. This window will update automatically.";
        try { Complete(await BrowserSignIn.Run(lifetime.Token,Browse,code=>LoginStatus.Text="Match "+code+" on ai-arch.ir, then approve this device.")); }
        catch(OperationCanceledException) when(lifetime.IsCancellationRequested){}
        catch(TimeoutException){LoginStatus.Text="Sign-in timed out. Open a new browser sign-in.";}
        catch(Exception ex){LoginStatus.Text=ex.Message;}
        finally{LoginButton.IsEnabled=true;}
    }
}
