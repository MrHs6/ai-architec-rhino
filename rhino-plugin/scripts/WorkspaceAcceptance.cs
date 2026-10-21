// Run inside Rhino's C# editor after loading AI Archi-Tec. No personal settings or active geometry are changed.
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using Rhino;
using Rhino.Geometry;

var assembly=AppDomain.CurrentDomain.GetAssemblies().Last(a=>a.GetName().Name.StartsWith("AIArchiTec.")&&a.GetType("ArchiTec.RhinoPlugin.ModelAudit")!=null);
var coreName=assembly.GetReferencedAssemblies().First(a=>a.Name.StartsWith("AIArchiTec.Contracts"));
var core=AppDomain.CurrentDomain.GetAssemblies().First(a=>a.FullName==coreName.FullName);
var viewType=assembly.GetType("ArchiTec.RhinoPlugin.Views.StudioView");var view=Activator.CreateInstance(viewType);
Func<string,object> element=name=>viewType.GetMethod("FindName").Invoke(view,new object[]{name});
var passed=0;Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception(name);passed++;RhinoApp.WriteLine("PASS "+name);};
foreach(var mode in new[]{"generate","analyze","assist"})
{
 viewType.GetMethod("SetWorkspace").Invoke(view,new object[]{mode,false});
 var name=mode=="generate"?"GenerateOptions":mode=="analyze"?"AnalyzeOptions":"AssistOptions";
 check(element(name).GetType().GetProperty("Visibility").GetValue(element(name)).ToString()=="Visible",mode+" options are independent");
}
foreach(var name in new[]{"Api","Mcp","General","About","Dashboard"})
{
 viewType.GetMethod("Show"+name).Invoke(view,null);
 var visible=new[]{"ApiSettings","McpSettings","GeneralSettings","AboutSettings","ConnectionSettings"}.Count(p=>element(p).GetType().GetProperty("Visibility").GetValue(element(p)).ToString()=="Visible");
 check(visible==1,name+" has a dedicated page");
}
var source=element("SourceSelector");source.GetType().GetProperty("SelectedItem").SetValue(source,"xAI / Grok");element("ApiKey").GetType().GetProperty("Password").SetValue(element("ApiKey"),"fixture-grok");
source.GetType().GetProperty("SelectedItem").SetValue(source,"Google Gemini");element("ApiKey").GetType().GetProperty("Password").SetValue(element("ApiKey"),"fixture-gemini");
source.GetType().GetProperty("SelectedItem").SetValue(source,"xAI / Grok");
check((string)element("ApiKey").GetType().GetProperty("Password").GetValue(element("ApiKey"))=="fixture-grok","Provider key drafts remain independent");
check((string)element("ApiUrl").GetType().GetProperty("Text").GetValue(element("ApiUrl"))=="https://api.x.ai/v1/","Provider endpoint restores with its profile");
check((int)source.GetType().GetProperty("Items").GetValue(source).GetType().GetProperty("Count").GetValue(source.GetType().GetProperty("Items").GetValue(source))==6,"Six providers are available");
var brush=viewType.GetField("BrandLogoBrush",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(view);
var bitmap=brush.GetType().GetProperty("ImageSource").GetValue(brush);check((int)bitmap.GetType().GetProperty("PixelWidth").GetValue(bitmap)>1000,"Original full-resolution logo is embedded");
using(var doc=RhinoDoc.CreateHeadless(null))
{
 doc.ModelUnitSystem=UnitSystem.Meters;var id=doc.Objects.AddBrep(new Box(new BoundingBox(0,0,0,1,2,3)).ToBrep());
 var context=assembly.GetType("ArchiTec.RhinoPlugin.ContextReader").GetMethod("Read").Invoke(null,new object[]{doc,new[]{id}});
 var report=assembly.GetType("ArchiTec.RhinoPlugin.ModelAudit").GetMethod("Report");
 check(((string)report.Invoke(null,new object[]{doc,context,"dimensions"})).Contains("1 × 2 × 3"),"Dimensions audit measures native geometry");
 check(((string)report.Invoke(null,new object[]{doc,context,"health"})).Contains("closed solid"),"Health audit reports native closure");
 check(((string)report.Invoke(null,new object[]{doc,context,"layers"})).Contains("Default: 1"),"Layer audit uses scoped objects");
}
var store=assembly.GetType("ArchiTec.RhinoPlugin.SettingsStore");var settings=assembly.GetType("ArchiTec.RhinoPlugin.ConnectionSettings");
var directory=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"AIArchiTec-profile-test-"+Guid.NewGuid().ToString("N"));System.IO.Directory.CreateDirectory(directory);
Func<string,string,bool,string,object> config=(name,key,remember,model)=>Activator.CreateInstance(settings,new object[]{name,"https://example.invalid/",key,remember,model});
var first=config("Google Gemini","fixture-A",true,"model-A");var second=config("Anthropic / Claude","fixture-B",true,"model-B");
var options=(JsonSerializerOptions)core.GetType("ArchiTec.Core.Wire").GetField("Json").GetValue(null);
var encoded=store.GetMethod("Encode",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new[]{first});
check((string)settings.GetProperty("ApiKey").GetValue(encoded)!="fixture-A","DPAPI protects profile credentials");
var decoded=store.GetMethod("Decode",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new[]{encoded});
check((string)settings.GetProperty("ApiKey").GetValue(decoded)=="fixture-A","DPAPI roundtrip succeeds for this Windows user");
System.IO.File.WriteAllText(System.IO.Path.Combine(directory,"connection.json"),JsonSerializer.Serialize(encoded,options));
store.GetMethod("SaveProfile").Invoke(null,new object[]{first,directory});store.GetMethod("SaveProfile").Invoke(null,new object[]{second,directory});store.GetMethod("SaveProfile").Invoke(null,new object[]{config("Google Gemini","fixture-A",false,"model-A"),directory});
using(var active=JsonDocument.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(directory,"connection.json"))))
using(var profiles=JsonDocument.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(directory,"profiles.json"))))
{
 check(active.RootElement.GetProperty("api_key").GetString()==""&&profiles.RootElement.GetProperty("Google Gemini").GetProperty("api_key").GetString()=="","Remember-off removes both stored key copies");
 check(profiles.RootElement.GetProperty("Anthropic / Claude").GetProperty("model").GetString()=="model-B"&&profiles.RootElement.GetProperty("Anthropic / Claude").GetProperty("api_key").GetString()!="fixture-B","Saving one provider preserves the other encrypted profile");
}
RhinoApp.WriteLine(passed+" workspace/profile/audit checks passed; personal settings and active geometry were not changed.");
