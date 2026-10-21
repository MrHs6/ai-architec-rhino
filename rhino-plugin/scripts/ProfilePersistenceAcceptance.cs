// Run inside Rhino. Fixture credentials are isolated from all personal settings.
using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.IO;

var a=AppDomain.CurrentDomain.GetAssemblies().First(x=>x.GetName().Name=="AIArchiTec.Studio");
var t=a.GetType("ArchiTec.RhinoPlugin.SettingsStore");var s=a.GetType("ArchiTec.RhinoPlugin.ConnectionSettings");
var root=Path.Combine(Path.GetTempPath(),"AIArchiTec-profile-acceptance-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
var passed=0;Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception(name);passed++;Rhino.RhinoApp.WriteLine("PASS "+name);};
Func<string,string,bool,object> config=(source,key,remember)=>Activator.CreateInstance(s,new object[]{source,source=="OpenAI"?"https://api.openai.com/v1/":"https://openrouter.ai/api/v1/",key,remember,"fixture-model"});
Action<object> save=value=>t.GetMethod("SaveProfile").Invoke(null,new object[]{value,root});
Func<object> load=()=>t.GetMethod("LoadFrom").Invoke(null,new object[]{root});
Func<object,string,string> text=(value,name)=>(string)s.GetProperty(name).GetValue(value);
save(config("OpenRouter","fixture-route",true));save(config("OpenAI","sk-proj-fixture",true));
check(text(load(),"Source")=="OpenAI","Saving a different provider makes it active on reload");
check(text(load(),"ApiKey")=="sk-proj-fixture","Encrypted OpenAI key is available after saving");
check(!File.ReadAllText(Path.Combine(root,"profiles.json")).Contains("sk-proj-fixture")&&!File.ReadAllText(Path.Combine(root,"connection.json")).Contains("sk-proj-fixture"),"Neither settings file contains the plaintext key");
save(config("OpenAI","fixture-session",false));
check(text(load(),"ApiKey")=="fixture-session","Remember-off retains the key in this Rhino session");
using(var active=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"connection.json"))))
using(var profiles=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"profiles.json"))))
{
 check(active.RootElement.GetProperty("api_key").GetString()==""&&profiles.RootElement.GetProperty("OpenAI").GetProperty("api_key").GetString()=="","Remember-off removes persisted key copies");
 check(profiles.RootElement.GetProperty("OpenRouter").GetProperty("api_key").GetString()!="","Updating OpenAI preserves the other provider's encrypted profile");
}
t.GetMethod("ForgetSessionCredentials").Invoke(null,new object[]{root});
check(text(load(),"Source")=="OpenAI"&&text(load(),"ApiKey")=="","Restart simulation retains selected provider but clears session-only key");
check(Directory.GetFiles(root,"*.tmp").Length==0,"Atomic settings writes leave no temporary plaintext or stale file");
var vt=a.GetType("ArchiTec.RhinoPlugin.Views.StudioView");var view=Activator.CreateInstance(vt);
Func<string,object> element=name=>vt.GetMethod("FindName").Invoke(view,new object[]{name});
vt.GetMethod("LoadSettings").Invoke(view,new[]{config("OpenAI","fixture-session",false)});
vt.GetMethod("ProfileSaved").Invoke(view,new[]{config("OpenAI","fixture-session",false)});
vt.GetMethod("ApiMessage").Invoke(view,new object[]{"Connection not established: simulated network failure"});
check(((string)element("ProfileSaveStatus").GetType().GetProperty("Text").GetValue(element("ProfileSaveStatus"))).Contains("session only"),"Network failure is independent from the saved/session profile indicator");
var selector=element("SourceSelector");selector.GetType().GetProperty("SelectedItem").SetValue(selector,"OpenRouter");selector.GetType().GetProperty("SelectedItem").SetValue(selector,"OpenAI");
check((string)element("ApiKey").GetType().GetProperty("Password").GetValue(element("ApiKey"))=="fixture-session","Switching providers retains unsaved session credentials independently");
check(element("TestNetworkButton") is not null&&element("NetworkStatus") is not null,"UI exposes a separate no-key network diagnostic");
Rhino.RhinoApp.WriteLine(passed+" profile synchronization checks passed; personal profiles untouched.");
