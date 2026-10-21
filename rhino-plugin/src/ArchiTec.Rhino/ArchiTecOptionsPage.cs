using Eto.Drawing;
using Eto.Forms;
using Rhino.UI;

namespace ArchiTec.RhinoPlugin;

/// <summary>Per-user connection preferences integrated into Rhino's native Options tree.</summary>
public sealed class ArchiTecOptionsPage:OptionsDialogPage
{
    private Panel? control;
    private readonly DropDown source=new(){Items={"OpenRouter","OpenAI","OpenAI-compatible / local"}};
    private readonly TextBox endpoint=new();
    private readonly PasswordBox key=new();
    private readonly CheckBox remember=new(){Text="Remember my API key securely on this Windows account"};
    private readonly Label status=new(){TextColor=Colors.Gray,Wrap=WrapMode.Word};
    private ConnectionSettings initial=new();
    public ArchiTecOptionsPage():base("AI Archi-Tec"){}
    public override object PageControl=>control??=CreateControl();
    private Panel CreateControl()
    {
        var layout=new DynamicLayout{Padding=20,Spacing=new Size(10,12)};
        layout.AddRow(new Label{Text="AI Archi-Tec",Font=new Font(SystemFont.Bold,18)});
        layout.AddRow(new Label{Text="Type archtec in Rhino's command line to open Studio.",Wrap=WrapMode.Word});
        layout.AddRow(new Label{Text="AI source"},source);
        layout.AddRow(new Label{Text="API base URL"},endpoint);
        layout.AddRow(new Label{Text="Personal API key"},key);
        layout.AddRow(remember);
        layout.AddRow(new Label{Text="Apply saves your preferences. Load compatible models and connect inside Studio > Settings. Keys and settings belong to the current Windows user.",Wrap=WrapMode.Word});
        layout.AddRow(new Label{Text="MCP: open Studio > MCP to start the authenticated localhost server and copy the desktop client configuration. MCP tokens are created per session.",Wrap=WrapMode.Word});
        layout.AddRow(status);
        layout.AddSpace();
        source.SelectedIndexChanged+=(_,_)=>endpoint.Text=source.SelectedIndex switch {0=>"https://openrouter.ai/api/v1/",1=>"https://api.openai.com/v1/",_=>"http://127.0.0.1:1234/v1/"};
        return new Panel{Content=layout};
    }
    public override bool OnActivate(bool active)
    {
        if(active)
        {
            _=PageControl;
            try{
                initial=ArchiTecPanel.Current?.CurrentSettings??SettingsStore.Load();
                source.SelectedIndex=initial.Source switch {"OpenAI"=>1,"OpenAI-compatible / local"=>2,_=>0};
                endpoint.Text=initial.Endpoint;key.Text=initial.ApiKey;remember.Checked=initial.RememberKey;status.Text="";
            }catch{status.Text="Saved settings could not be read. Enter your connection details again.";}
        }
        return true;
    }
    public override bool OnApply()
    {
        if(control is null)return true;
        if(!Uri.TryCreate(endpoint.Text.Trim(),UriKind.Absolute,out var uri)||uri.UserInfo.Length>0||!(uri.Scheme=="https"||(uri.Scheme=="http"&&uri.IsLoopback))){status.Text="Use an HTTPS API URL or a localhost HTTP endpoint, without credentials in the URL.";return false;}
        var value=new ConnectionSettings(source.SelectedIndex switch {1=>"OpenAI",2=>"OpenAI-compatible / local",_=>"OpenRouter"},endpoint.Text.Trim(),key.Text,remember.Checked==true,initial.Model);
        if(value==initial)return true;
        try{
            if(ArchiTecPanel.Current is {} panel){if(!panel.TryUpdateSettings(value)){status.Text="Wait for the current Studio request to finish before changing settings.";return false;}}
            else SettingsStore.Save(value);
            initial=value;status.Text="Saved. Open Studio > Settings to connect and choose a model.";return true;
        }catch{status.Text="Preferences could not be saved for this Windows user.";return false;}
    }
    public override void OnCancel(){key.Text="";}
}
