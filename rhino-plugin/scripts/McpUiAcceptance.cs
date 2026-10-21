// Run in the installed Rhino plugin. Reads UI and previews only; no paid API is called.
using System;
using System.Linq;
using System.Reflection;
var a=AppDomain.CurrentDomain.GetAssemblies().First(x=>x.GetName().Name=="AIArchiTec.Studio");
var t=a.GetType("ArchiTec.RhinoPlugin.ArchiTecPanel");var panel=t.GetProperty("Current").GetValue(null);
var view=t.GetField("view",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(panel);var vt=view.GetType();
var passed=0;Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception(name);passed++;Rhino.RhinoApp.WriteLine("PASS "+name);};
Func<string,object> element=name=>vt.GetMethod("FindName").Invoke(view,new object[]{name});
Func<string,string> text=name=>(string)element(name).GetType().GetProperty("Text").GetValue(element(name));
foreach(var mode in new[]{"generate","analyze","assist"}){
 vt.GetMethod("SetWorkspace").Invoke(view,new object[]{mode,true});
 check(text("WorkspaceHelp").Length>80&&!text("WorkspaceHelp").Any(c=>c>='\u0600'&&c<='\u06ff'),mode+" has an English contextual guide");
}
var selector=element("McpClientSelector");selector.GetType().GetProperty("SelectedIndex").SetValue(selector,1);
check(text("McpSetupGuide").Contains("config.toml"),"Codex has its own TOML setup guide");
selector.GetType().GetProperty("SelectedIndex").SetValue(selector,0);
check(text("McpSetupGuide").Contains("claude_desktop_config.json"),"Claude has its own JSON setup guide");
selector.GetType().GetProperty("SelectedIndex").SetValue(selector,2);
check(text("McpSetupGuide").Contains("Secure MCP Tunnel"),"Web guidance describes the separate supported connection route");
selector.GetType().GetProperty("SelectedIndex").SetValue(selector,1);
var route=element("ChatRouteSelector");route.GetType().GetProperty("SelectedIndex").SetValue(route,1);
check(element("SendButton").GetType().GetProperty("Content").GetValue(element("SendButton")).ToString().Contains("Copy for MCP"),"MCP chat button explicitly describes copying rather than automatic online chat");
route.GetType().GetProperty("SelectedIndex").SetValue(route,0);
check(text("ConnectionLabel").StartsWith("API"),"API status is labeled independently from MCP status");
check(element("McpClientStatus")!=null&&element("McpStage")!=null&&element("McpLastSeen")!=null,"Client, workflow stage and last contact have separate displays");
vt.GetMethod("SetWorkspace").Invoke(view,new object[]{"generate",true});
Rhino.RhinoApp.WriteLine(passed+" native MCP UI checks passed. No geometry or personal profiles changed.");
