using Eto.Drawing;
using Eto.Forms;
using Rhino.UI;
using ArchiTec.Core;

namespace ArchiTec.RhinoPlugin;

/// <summary>Per-user connection preferences integrated into Rhino's native Options tree.</summary>
public sealed class ArchiTecOptionsPage:OptionsDialogPage
{
    private Panel? control;
    private readonly DropDown source=new();
    private readonly TextBox endpoint=new();
    private readonly PasswordBox key=new();
    private readonly CheckBox remember=new(){Text="Remember my API key securely on this Windows account"};
    private readonly Label status=new(){TextColor=Colors.Gray,Wrap=WrapMode.Word};
    private ConnectionSettings initial=new();
    public ArchiTecOptionsPage():base("AI Archi-Tec"){}
    public override object PageControl=>control??=CreateControl();
    private Panel CreateControl()
    {
        foreach(var preset in ProviderProfiles.All)source.Items.Add(preset.Name);
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
        source.SelectedIndexChanged+=(_,_)=>
        {
            if(source.SelectedIndex<0)return;var preset=ProviderProfiles.All[source.SelectedIndex];
            var profiles=SettingsStore.LoadProfiles();var profile=profiles.TryGetValue(preset.Name,out var value)?value:new ConnectionSettings(preset.Name,preset.Endpoint);
            endpoint.Text=profile.Endpoint;endpoint.ReadOnly=preset.Name!="OpenAI-compatible / local";key.Text=profile.ApiKey;remember.Checked=profile.RememberKey;
        };
        return new Panel{Content=layout};
    }
    public override bool OnActivate(bool active)
    {
        if(active)
        {
            _=PageControl;
            try{
                initial=ArchiTecPanel.Current?.CurrentSettings??SettingsStore.Load();
                source.SelectedIndex=Array.FindIndex(ProviderProfiles.All,p=>p.Name==initial.Source);
                endpoint.Text=initial.Endpoint;key.Text=initial.ApiKey;remember.Checked=initial.RememberKey;status.Text="";
            }catch{status.Text="Saved settings could not be read. Enter your connection details again.";}
        }
        return true;
    }
    public override bool OnApply()
    {
        if(control is null)return true;
        if(!Uri.TryCreate(endpoint.Text.Trim(),UriKind.Absolute,out var uri)||uri.UserInfo.Length>0||!(uri.Scheme=="https"||(uri.Scheme=="http"&&uri.IsLoopback))){status.Text="Use an HTTPS API URL or a localhost HTTP endpoint, without credentials in the URL.";return false;}
        var selected=ProviderProfiles.All[source.SelectedIndex].Name;
        var profiles=SettingsStore.LoadProfiles();var model=profiles.TryGetValue(selected,out var profile)?profile.Model:"";
        var value=new ConnectionSettings(selected,endpoint.Text.Trim(),key.Text,remember.Checked==true,model);
        if(value==initial)return true;
        try{
            if(ArchiTecPanel.Current is {} panel){if(!panel.TryUpdateSettings(value)){status.Text="Wait for the current Studio request to finish before changing settings.";return false;}}
            else SettingsStore.Save(value);
            initial=value;status.Text="Saved. Open Studio > Settings to connect and choose a model.";return true;
        }catch{status.Text="Preferences could not be saved for this Windows user.";return false;}
    }
    public override void OnCancel(){key.Text="";}
}
