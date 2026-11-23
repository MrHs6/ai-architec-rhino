using System.Windows;
namespace Studio;
public partial class WelcomeWindow : Window
{
    internal string VerifiedKey {get; private set;}="";
    public WelcomeWindow(){InitializeComponent(); Loaded+=(_,_)=>Secret.Focus();}
    async void Login(object sender,RoutedEventArgs e)
    {
        string key=Secret.Password.Trim();
        if(key.Length==0){LoginStatus.Text="Enter your OpenRouter API key.";return;}
        LoginButton.IsEnabled=Secret.IsEnabled=false;
        LoginStatus.Text="Verifying with OpenRouter…";
        try {
            await RouterAccess.Verify(key);
            if(!IsVisible)return;
            VerifiedKey=key; Secret.Clear(); DialogResult=true;
        } catch(Exception ex){LoginStatus.Text=ex.Message;}
        finally{LoginButton.IsEnabled=Secret.IsEnabled=true;}
    }
}
