using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using Rhino;
using Rhino.Geometry;
using Rhino.DocObjects;

var assembly = AppDomain.CurrentDomain.GetAssemblies().First(a=>a.GetName().Name=="AIArchiTec.Studio");
var contracts = AppDomain.CurrentDomain.GetAssemblies().First(a=>a.GetName().Name=="AIArchiTec.Contracts");
var engine = assembly.GetType("ArchiTec.RhinoPlugin.GeometryEngine");
var planType = contracts.GetType("ArchiTec.Core.ActionPlan");
var wire = (JsonSerializerOptions)contracts.GetType("ArchiTec.Core.Wire").GetField("Json").GetValue(null);
var passes = new List<string>();
Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception("FAIL "+name);passes.Add(name);RhinoApp.WriteLine("PASS "+name);};
Func<RhinoDoc,string,object> prepare=(d,json)=>engine.GetMethod("Prepare").Invoke(null,new[]{(object)d,JsonSerializer.Deserialize(json,planType,wire)});
Func<RhinoDoc,object,object> apply=(d,p)=>{return engine.GetMethod("Apply").Invoke(null,new[]{(object)d,p});};
Func<object,List<Guid>> created=r=>(List<Guid>)r.GetType().GetProperty("Created").GetValue(r);
Action<Action,string> rejected=(action,name)=>{bool failed=false;try{action();}catch(TargetInvocationException){failed=true;}check(failed,name);};
Func<string,string> box=unit=>"{\"version\":1,\"summary\":\"Acceptance box\",\"length_unit\":\""+unit+"\",\"actions\":[{\"tool\":\"create_box\",\"parameters\":{\"width\":10,\"depth\":15,\"height\":12,\"layer\":\"AI_Building\"}}]}";
Func<Guid,string> move=id=>"{\"version\":1,\"summary\":\"Move two meters along X\",\"length_unit\":\"meters\",\"actions\":[{\"tool\":\"move_object\",\"parameters\":{\"object_id\":\""+id+"\",\"vector\":[2,0,0]}}]}";
using(var doc=RhinoDoc.CreateHeadless(null))
{
    doc.ModelUnitSystem=UnitSystem.Meters;doc.UndoRecordingEnabled=true;
    var setupUndo=doc.BeginUndoRecord("Setup sentinel");var sentinel=doc.Objects.AddPoint(new Point3d(-100,-100,-100));doc.EndUndoRecord(setupUndo);
    var sentinelCrc=doc.Objects.FindId(sentinel).Geometry.DataCRC(0);
    using(var pending=(IDisposable)prepare(doc,box("meters")))
    {
        check(doc.Objects.GetObjectList(ObjectType.AnyObject).Count()==1,"Preview does not add document objects");
        var report=apply(doc,pending);var id=created(report).Single();
        var obj=doc.Objects.FindId(id);var bounds=obj.Geometry.GetBoundingBox(true);
        check(bounds.Diagonal.EpsilonEquals(new Vector3d(10,15,12),1e-7),"Native box has exact 10 x 15 x 12 meter dimensions");
        check(Math.Abs(VolumeMassProperties.Compute((Brep)obj.Geometry).Volume-1800)<1e-6,"Native box is solid with volume 1800 m3");
        check(doc.Layers[obj.Attributes.LayerIndex].Name=="AI_Building","Box belongs to AI_Building layer");
        using(var movement=(IDisposable)prepare(doc,move(id)))apply(doc,movement);
        check(doc.Objects.FindId(id)!=null && Math.Abs(doc.Objects.FindId(id).Geometry.GetBoundingBox(true).Min.X-2)<1e-7,"Move preserves object GUID and offsets X by 2 meters");
        check(doc.Objects.FindId(sentinel).Geometry.DataCRC(0)==sentinelCrc,"Unrelated geometry remains unchanged");
        check(doc.Undo(),"Rhino undo succeeds for movement");if(doc.UndoRecordingIsActive)doc.EndUndoRecord(doc.CurrentUndoRecordSerialNumber);
        check(Math.Abs(doc.Objects.FindId(id).Geometry.GetBoundingBox(true).Min.X)<1e-7,"Undo restores original position");
        using(var stale=(IDisposable)prepare(doc,move(id)))
        {
            var changeUndo=doc.BeginUndoRecord("Fixture change");doc.Objects.Transform(id,Transform.Translation(1,0,0),true);doc.EndUndoRecord(changeUndo);
            rejected(()=>apply(doc,stale),"Reject stale preview after target geometry changes");
        }
        var curvePlan="{\"version\":1,\"summary\":\"Closed floor outline\",\"length_unit\":\"meters\",\"actions\":[{\"tool\":\"create_polyline\",\"parameters\":{\"points\":[[0,0,0],[4,0,0],[4,5,0],[0,5,0]],\"closed\":true,\"layer\":\"AI_Floor\"}}]}";
        Guid curveId;using(var curvePending=(IDisposable)prepare(doc,curvePlan))curveId=created(apply(doc,curvePending)).Single();
        var extrudePlan="{\"version\":1,\"summary\":\"Extrude floor outline\",\"length_unit\":\"meters\",\"actions\":[{\"tool\":\"extrude_curve\",\"parameters\":{\"object_id\":\""+curveId+"\",\"height\":3,\"cap\":true}}]}";
        using(var extrusion=(IDisposable)prepare(doc,extrudePlan))
        {var extruded=doc.Objects.FindId(created(apply(doc,extrusion)).Single());check(extruded.Geometry is Brep b && b.IsSolid,"Closed planar curve extrudes into capped solid");check(doc.Objects.FindId(curveId)!=null,"Extrusion preserves source curve");}
        var count=doc.Objects.GetObjectList(ObjectType.AnyObject).Count();
        var invalidBatch="{\"version\":1,\"summary\":\"Invalid batch\",\"length_unit\":\"meters\",\"actions\":[{\"tool\":\"create_box\",\"parameters\":{\"width\":1,\"depth\":1,\"height\":1}},{\"tool\":\"move_object\",\"parameters\":{\"object_id\":\""+Guid.NewGuid()+"\",\"vector\":[1,0,0]}}]}";
        rejected(()=>prepare(doc,invalidBatch),"Invalid later action rejects whole batch before execution");
        check(doc.Objects.GetObjectList(ObjectType.AnyObject).Count()==count,"Rejected batch leaves object count unchanged");
        var lockUndo=doc.BeginUndoRecord("Fixture lock");doc.Objects.Lock(curveId,true);doc.EndUndoRecord(lockUndo);
        rejected(()=>prepare(doc,move(curveId)),"Locked geometry cannot be modified");
        var unlockUndo=doc.BeginUndoRecord("Fixture unlock");doc.Objects.Unlock(curveId,true);doc.EndUndoRecord(unlockUndo);
        using(var changedUnits=(IDisposable)prepare(doc,move(curveId)))
        {doc.ModelUnitSystem=UnitSystem.Millimeters;rejected(()=>apply(doc,changedUnits),"Unit change invalidates pending preview");doc.ModelUnitSystem=UnitSystem.Meters;}
        var threePoint="{\"version\":1,\"summary\":\"Curve\",\"length_unit\":\"meters\",\"actions\":[{\"tool\":\"create_curve\",\"parameters\":{\"points\":[[0,0,0],[2,3,0],[4,0,0]]}}]}";
        using(var curvePending=(IDisposable)prepare(doc,threePoint))check(created(apply(doc,curvePending)).Count==1,"Three-point curve produces native editable geometry");
    }
}
using(var millimeters=RhinoDoc.CreateHeadless(null))
{
    millimeters.ModelUnitSystem=UnitSystem.Millimeters;millimeters.UndoRecordingEnabled=true;
    using(var pending=(IDisposable)prepare(millimeters,box("meters")))
    {var id=created(apply(millimeters,pending)).Single();check(Math.Abs(millimeters.Objects.FindId(id).Geometry.GetBoundingBox(true).Max.X-10000)<1e-5,"Meter instructions convert correctly into millimeter document");check(millimeters.Undo(),"Undo succeeds for box creation");check(millimeters.Objects.FindId(id)==null,"Undo removes created geometry");}
}
RhinoApp.WriteLine(passes.Count+" Rhino integration tests passed.");
