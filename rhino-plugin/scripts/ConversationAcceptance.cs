// Acceptance on an empty active document. Uses offline examples, not a live AI account.
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using Rhino;
using Rhino.Geometry;
using Rhino.DocObjects;
var doc=__rhino_doc__;
var evidencePath=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"AIArchiTec-conversation-acceptance-"+doc.RuntimeSerialNumber+".txt");
EventHandler acceptance=null;
acceptance=(sender,args)=>{
if(doc.UndoRecordingIsActive)return;
RhinoApp.Idle-=acceptance;
var evidence=new System.Collections.Generic.List<string>();
try {
if(doc.Objects.Count!=0||doc.Modified)throw new Exception("Use an empty unmodified document for this acceptance test.");
var a=AppDomain.CurrentDomain.GetAssemblies().First(x=>x.GetName().Name=="AIArchiTec.Studio");var t=a.GetType("ArchiTec.RhinoPlugin.ArchiTecPanel");var panel=t.GetProperty("Current").GetValue(null);var view=t.GetField("view",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(panel);var vt=view.GetType();
var passed=0;Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception(name);passed++;evidence.Add("PASS "+name);RhinoApp.WriteLine("PASS "+name);};
Action send=()=>((Task)t.GetMethod("SendAsync").Invoke(panel,null)).GetAwaiter().GetResult();
Action apply=()=>t.GetMethod("ApplyPending",BindingFlags.Public|BindingFlags.Instance).Invoke(panel,null);
vt.GetMethod("SetWorkspace").Invoke(view,new object[]{"generate",true});vt.GetProperty("Prompt").SetValue(view,"Create a 15 x 20 meter building with 5 floors and 3.2 meter floor heights.");send();
check(doc.Objects.Count==0&&t.GetField("pending",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(panel)!=null,"Chat creates a real preview without changing the document");
apply();check(doc.Objects.Count==46,"Chat Apply creates 46 native building parts in the active document");
var ids=doc.Objects.GetObjectList(ObjectType.AnyObject).Select(o=>o.Id).ToArray();var original=ids.ToDictionary(id=>id,id=>doc.Objects.FindId(id).Geometry.GetBoundingBox(true));
var state=t.GetField("documentState",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(panel);var st=state.GetType();
check(((string)st.GetProperty("LastReport").GetValue(state)).Contains("15 × 20 × 16"),"The automatic chat reply measures the actual built result");
vt.GetProperty("Prompt").SetValue(view,"طبقه‌ی پنجم ساختمان را ۲ متر عقب جابه‌جا کن");send();
check((string)t.GetField("mode",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(panel)=="assist"&&doc.Objects.Count==46,"Follow-up automatically enters Assist on the recent result, without a new building");
apply();var moved=0;var untouched=0;
foreach(var id in ids){var obj=doc.Objects.FindId(id);if(obj==null)throw new Exception("An existing GUID disappeared.");var b=obj.Geometry.GetBoundingBox(true);var floor=obj.Attributes.GetUserString("AIArchiTec.Floor");if(floor=="5"){if(Math.Abs(b.Min.Y-original[id].Min.Y-2)>1e-6)throw new Exception("Wrong fifth-floor displacement");moved++;}else{if(b.Min.DistanceTo(original[id].Min)>1e-6)throw new Exception("Unrelated floor moved");untouched++;}}
check(ids.All(id=>doc.Objects.FindId(id)!=null),"All existing GUIDs survive the follow-up edit");
check(moved==10&&untouched==36,"Only the 10 fifth-floor parts move; 36 other components stay unchanged");
check(((string)st.GetProperty("LastReport").GetValue(state)).Contains("نتیجهٔ ثبت‌شده"),"The applied follow-up receives Persian verified feedback");
if(!doc.Undo())throw new Exception("Undo failed");
if(doc.UndoRecordingIsActive)doc.EndUndoRecord(doc.CurrentUndoRecordSerialNumber);
t.GetMethod("RefreshContext",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(panel,null);
check(ids.All(id=>doc.Objects.FindId(id).Geometry.GetBoundingBox(true).Min.DistanceTo(original[id].Min)<1e-6),"Rhino Undo restores exact original positions and GUIDs");
check(((string)st.GetProperty("LastReport").GetValue(state)).Contains("تغییر کرده")&&((string)st.GetProperty("LastReport").GetValue(state)).Contains("15 × 20 × 16"),"Chat detects Undo/manual geometry changes and refreshes measured feedback");
vt.GetMethod("SetWorkspace").Invoke(view,new object[]{"generate",true});doc.Views.ActiveView.ActiveViewport.ZoomExtents();doc.Views.Redraw();
RhinoApp.WriteLine(passed+" workflow acceptance checks passed. Limited offline example remains in the active document.");
System.IO.File.WriteAllLines(evidencePath,evidence.Concat(new[]{passed+" workflow acceptance checks passed."}));
}catch(Exception ex){System.IO.File.WriteAllLines(evidencePath,evidence.Concat(new[]{"FAIL "+ex.ToString()}));RhinoApp.WriteLine(ex.ToString());}
};
RhinoApp.Idle+=acceptance;
RhinoApp.WriteLine("Scheduled conversation acceptance after the current command finishes.");
RhinoApp.WriteLine("Acceptance evidence: "+evidencePath);
