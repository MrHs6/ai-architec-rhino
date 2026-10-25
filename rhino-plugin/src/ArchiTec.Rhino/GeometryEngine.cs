using System.Text.Json;
using ArchiTec.Core;
using Rhino;
using Rhino.DocObjects;
using Rhino.Display;
using Rhino.Geometry;

namespace ArchiTec.RhinoPlugin;

public static class ContextReader
{
    public static ModelContext Read(RhinoDoc doc, IEnumerable<Guid>? last = null)
    {
        var selected = doc.Objects.GetSelectedObjects(false, false).ToArray();
        var recent = (last ?? []).Select(id => doc.Objects.FindId(id)).Where(o => o is not null && !o.IsDeleted).ToArray();
        // Only selected and panel-created objects are transmitted; never serialize the whole model.
        var scope = selected.Concat(recent!).DistinctBy(o => o.Id).Take(100).ToArray();
        return new ModelContext(doc.RuntimeSerialNumber, doc.ModelUnitSystem.ToString(),
            doc.Layers.CurrentLayer.FullPath, doc.Layers.Where(l => !l.IsDeleted).Take(200).Select(l => l.FullPath).ToArray(),
            doc.Objects.GetObjectList(new ObjectEnumeratorSettings{NormalObjects=true,LockedObjects=true,HiddenObjects=true,ReferenceObjects=true,ObjectTypeFilter=ObjectType.AnyObject}).Count(), scope.Select(o => Info(doc, o)).ToArray(), selected.Take(100).Select(o => o.Id.ToString()).ToArray(),
            recent.Select(o => o!.Id.ToString()).ToArray(), selected.Length + recent.Length > 100);
    }
    public static ObjectInfo Info(RhinoDoc doc, RhinoObject o)
    {
        var b = o.Geometry.GetBoundingBox(true);
        return new ObjectInfo(o.Id.ToString(), o.ObjectType.ToString(), doc.Layers[o.Attributes.LayerIndex].FullPath,
            [b.Min.X,b.Min.Y,b.Min.Z], [b.Max.X,b.Max.Y,b.Max.Z],o.Name,o.Attributes.GetUserString("AIArchiTec.Role"),
            int.TryParse(o.Attributes.GetUserString("AIArchiTec.Floor"),out var floor)?floor:null);
    }
}

public sealed class PreparedPlan : IDisposable
{
    internal readonly uint DocumentId;
    internal readonly UnitSystem Units;
    internal readonly double Tolerance;
    internal readonly Dictionary<Guid, (uint Crc, int Layer)> Fingerprints = [];
    internal readonly List<PreparedAction> Steps = [];
    internal IPreparedGrasshopper? Grasshopper;
    public ActionPlan Plan { get; }
    public IEnumerable<GeometryBase> Preview => Steps.Where(s=>s.Geometry is not null).Select(s=>s.Geometry!);
    internal PreparedPlan(RhinoDoc doc, ActionPlan plan)
    { DocumentId = doc.RuntimeSerialNumber; Units = doc.ModelUnitSystem; Tolerance = doc.ModelAbsoluteTolerance; Plan = plan; }
    public void Dispose() { Grasshopper?.Dispose(); foreach (var s in Steps) s.Geometry?.Dispose(); Steps.Clear(); }
}
internal sealed record PreparedAction(string Tool, JsonElement Parameters, Guid Id, GeometryBase? Geometry, string? Layer,string? Role=null,int? Floor=null);
public sealed record ExecutionReport(List<Guid> Created, List<Guid> Modified, List<ObjectInfo> Properties, uint UndoSerial, object? Grasshopper = null);

public static class GeometryEngine
{
    public static PreparedPlan Prepare(RhinoDoc doc, ActionPlan plan)
    {
        PlanValidator.Validate(plan);
        if (doc.ModelUnitSystem is UnitSystem.None or UnitSystem.CustomUnits or UnitSystem.Unset)
            throw new ArgumentException("Set standard Rhino document units before planning geometry.");
        var factor = RhinoMath.UnitScale(Unit(plan.LengthUnit, doc), doc.ModelUnitSystem);
        var prepared = new PreparedPlan(doc, plan);
        if(GrasshopperPlan.IsGrasshopper(plan))
        {
            try{GrasshopperHost.Prepare(doc,prepared);return prepared;}catch{prepared.Dispose();throw;}
        }
        var shadow = new Dictionary<Guid, GeometryBase>();
        try
        {
            foreach (var a in plan.Actions)
            {
                var p = a.Parameters;
                Guid id = p.TryGetProperty("object_id",out _) ? PlanValidator.Id(p,"object_id") : Guid.Empty;
                GeometryBase? geometry = null;
                var layer = p.TryGetProperty("layer", out var l) ? l.GetString() : null;
                RhinoObject? source = null;
                if (id != Guid.Empty)
                {
                    source = Reference(doc, id, MutatesTarget(a.Tool));
                    if(a.Tool=="set_visibility"&&(source.IsLocked||source.IsReference||doc.Layers[source.Attributes.LayerIndex].IsLocked))throw new ArgumentException("Visibility targets must be editable.");
                    prepared.Fingerprints.TryAdd(id, (source.Geometry.DataCRC(0), source.Attributes.LayerIndex));
                    if (!shadow.ContainsKey(id)) shadow[id] = source.Geometry.Duplicate();
                }
                switch (a.Tool)
                {
                    case "create_building":
                        var components=ArchitectureBuilder.Create(p,factor,doc);
                        prepared.Steps.AddRange(components);
                        if(prepared.Steps.Count>1500)throw new ArgumentException("A plan may create at most 1500 component operations.");
                        foreach(var part in components)
                        {
                            var targetLayer=doc.Layers.FindName(part.Layer!);
                            if(targetLayer is not null&&(targetLayer.IsLocked||!targetLayer.IsVisible))throw new ArgumentException("A building layer is hidden or locked.");
                        }
                        continue;
                    case "create_box":
                        var origin = p.TryGetProperty("origin",out var o) ? Pt(o,factor) : Point3d.Origin;
                        geometry = new Box(new Plane(origin,Vector3d.ZAxis),new Interval(0,PlanValidator.Number(p,"width")*factor),
                            new Interval(0,PlanValidator.Number(p,"depth")*factor),new Interval(0,PlanValidator.Number(p,"height")*factor)).ToBrep();
                        break;
                    case "create_line":
                        geometry = new LineCurve(Pt(p.GetProperty("start"),factor),Pt(p.GetProperty("end"),factor)); break;
                    case "create_loft": case "create_mesh": case "create_extrusion":
                        geometry=SurfaceTools.Create(a.Tool,p,factor,doc.ModelAbsoluteTolerance);break;
                    case "create_boolean":case "create_sweep":case "create_revolve":case "create_pipe":case "create_offset":case "create_geometry":
                        geometry=SolidOperations.Create(a.Tool,p,factor,doc.ModelAbsoluteTolerance,reference=>
                        {
                            var obj=Reference(doc,reference,false);prepared.Fingerprints.TryAdd(reference,(obj.Geometry.DataCRC(0),obj.Attributes.LayerIndex));
                            return shadow.TryGetValue(reference,out var current)?current:obj.Geometry;
                        });break;
                    case "create_polyline": case "create_curve":
                        var points = p.GetProperty("points").EnumerateArray().Select(v=>Pt(v,factor)).ToList();
                        if (p.TryGetProperty("closed",out var closed) && closed.GetBoolean() && points[0] != points[^1]) points.Add(points[0]);
                        geometry = a.Tool == "create_polyline" ? new PolylineCurve(points) : Curve.CreateInterpolatedCurve(points, points.Count>=4 ? 3 : 1);
                        break;
                    case "extrude_curve":
                        if (shadow[id] is not Curve curve || !curve.IsPlanar(doc.ModelAbsoluteTolerance))
                            throw new ArgumentException("extrude_curve requires a planar curve.");
                        var surface = Surface.CreateExtrusion(curve,new Vector3d(0,0,PlanValidator.Number(p,"height")*factor))
                            ?? throw new ArgumentException("Extrusion failed.");
                        var brep = surface.ToBrep(); surface.Dispose();
                        var cap = !p.TryGetProperty("cap",out var capValue) || capValue.GetBoolean();
                        if (cap)
                        {
                            if (!curve.IsClosed) { brep.Dispose(); throw new ArgumentException("Capped extrusion requires a closed planar curve."); }
                            var capped = brep.CapPlanarHoles(doc.ModelAbsoluteTolerance);
                            brep.Dispose(); brep = capped ?? throw new ArgumentException("Could not cap extrusion.");
                            if (!brep.IsSolid) { brep.Dispose(); throw new ArgumentException("Capped extrusion is not solid."); }
                        }
                        geometry = brep;
                        layer ??= doc.Layers[source!.Attributes.LayerIndex].FullPath;
                        break;
                    case "move_object":case "copy_object":
                        var v = PlanValidator.Point(p,"vector");
                        geometry = shadow[id].Duplicate();
                        if (!geometry.Transform(Transform.Translation(v[0]*factor,v[1]*factor,v[2]*factor))) throw new ArgumentException("Move failed.");
                        if(a.Tool!="copy_object"){shadow[id].Dispose(); shadow[id] = geometry.Duplicate();} break;
                    case "rotate_object":
                        var axis=PlanValidator.Point(p,"axis_direction");var direction=new Vector3d(axis[0],axis[1],axis[2]);if(!direction.Unitize())throw new ArgumentException("Rotation axis cannot be zero.");
                        geometry=shadow[id].Duplicate();if(!geometry.Transform(Transform.Rotation(PlanValidator.Number(p,"angle_degrees")*Math.PI/180,direction,Pt(p.GetProperty("axis_origin"),factor))))throw new ArgumentException("Rotation failed.");
                        shadow[id].Dispose();shadow[id]=geometry.Duplicate();break;
                    case "scale_object":
                        geometry = shadow[id].Duplicate();
                        if (!geometry.Transform(Transform.Scale(Pt(p.GetProperty("center"),factor),PlanValidator.Number(p,"factor")))) throw new ArgumentException("Scale failed.");
                        shadow[id].Dispose(); shadow[id] = geometry.Duplicate(); break;
                    case "create_layer": layer = PlanValidator.Text(p,"name"); break;
                    case "select_objects":case "delete_objects":
                        foreach (var elem in p.GetProperty("object_ids").EnumerateArray())
                        {
                            var obj = Reference(doc,Guid.Parse(elem.GetString()!),a.Tool!="delete_objects");
                            if(a.Tool=="delete_objects"&&(obj.IsLocked||obj.IsReference||doc.Layers[obj.Attributes.LayerIndex].IsLocked))throw new ArgumentException("Deletion targets must be editable.");
                            prepared.Fingerprints.TryAdd(obj.Id,(obj.Geometry.DataCRC(0),obj.Attributes.LayerIndex));
                        }
                        break;
                }
                if (layer is not null)
                {
                    if (layer.Contains("::") || !ModelComponent.IsValidComponentName(layer)) throw new ArgumentException("Use a valid single-level layer name.");
                    var existing = doc.Layers.FindName(layer);
                    if (existing is not null && (!existing.IsVisible || existing.IsLocked)) throw new ArgumentException($"Layer '{layer}' is hidden or locked.");
                }
                if (geometry is not null)
                {
                    var b = geometry.GetBoundingBox(true);
                    if (!geometry.IsValid || !b.IsValid || b.Diagonal.Length < doc.ModelAbsoluteTolerance || b.Diagonal.Length / RhinoMath.UnitScale(UnitSystem.Meters, doc.ModelUnitSystem) > 200000)
                    { geometry.Dispose(); throw new ArgumentException("Invalid, sub-tolerance or excessively large geometry."); }
                }
                prepared.Steps.Add(new PreparedAction(a.Tool,p.Clone(),id,geometry,layer));
            }
            return prepared;
        }
        catch { prepared.Dispose(); throw; }
        finally { foreach (var g in shadow.Values) g.Dispose(); }
    }
    public static ExecutionReport Apply(RhinoDoc doc,PreparedPlan prepared)=>ApplyCancellable(doc,prepared,default);
    public static ExecutionReport ApplyCancellable(RhinoDoc doc, PreparedPlan prepared,CancellationToken cancellation)
    {
        CheckCurrent(doc,prepared);
        if(prepared.Grasshopper is not null)return prepared.Grasshopper.Apply(doc,cancellation);
        if (doc.CurrentUndoRecordSerialNumber != 0)
            throw new InvalidOperationException($"Rhino is recording another operation (Undo {doc.CurrentUndoRecordSerialNumber}). Finish the active command before applying this plan.");
        // Validate layers again, including locks changed since preview.
        foreach (var s in prepared.Steps.Where(s=>s.Layer is not null))
        {
            var l = doc.Layers.FindName(s.Layer!);
            if (l is not null && (l.IsLocked || !l.IsVisible)) throw new InvalidOperationException("A destination layer changed. Generate a new preview.");
        }
        var serial = doc.BeginUndoRecord("AI Archi-Tec: " + prepared.Plan.Summary);
        if (serial == 0) throw new InvalidOperationException("Rhino could not create an undo record. Finish any active command and ensure document Undo is enabled.");
        var created = new List<Guid>(); var modified = new List<Guid>(); var properties = new List<ObjectInfo>();
        var layerCountBefore=doc.Layers.Count;var materialCountBefore=doc.Materials.Count;
        Exception? failure = null;
        try
        {
            foreach (var s in prepared.Steps)
            {
                cancellation.ThrowIfCancellationRequested();
                if (s.Tool.StartsWith("create_") && s.Tool != "create_layer" || s.Tool is "extrude_curve" or "copy_object")
                {
                    var attributes = new ObjectAttributes { LayerIndex = s.Layer is null ? doc.Layers.CurrentLayerIndex : EnsureLayer(doc,s.Layer),
                        Name = s.Parameters.TryGetProperty("name",out var n) ? n.GetString() : "AI Archi-Tec" };
                    attributes.SetUserString("AIArchiTec", "0.1.0");
                    if(s.Role is not null)attributes.SetUserString("AIArchiTec.Role",s.Role);
                    if(s.Floor is not null)attributes.SetUserString("AIArchiTec.Floor",s.Floor.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    var id = doc.Objects.Add(s.Geometry!, attributes);
                    if (id == Guid.Empty) throw new InvalidOperationException("Rhino rejected the new geometry.");
                    created.Add(id);
                }
                else switch (s.Tool)
                {
                    case "create_layer":
                        var layerIndex=EnsureLayer(doc,s.Layer!);var layer=doc.Layers[layerIndex];
                        if(s.Parameters.TryGetProperty("color",out var rgb)){var c=PlanValidator.Coordinates(rgb);layer.Color=System.Drawing.Color.FromArgb((int)c[0],(int)c[1],(int)c[2]);}
                        if(s.Parameters.TryGetProperty("color",out _)||s.Parameters.TryGetProperty("transparency",out _))
                        {
                            var mat=new Material{Name=s.Layer+" material",DiffuseColor=layer.Color,Transparency=s.Parameters.TryGetProperty("transparency",out var t)?t.GetDouble():0};
                            layer.RenderMaterialIndex=doc.Materials.Add(mat);doc.Layers.Modify(layer,layerIndex,true);
                        }
                        break;
                    case "set_visibility":
                        var visible=s.Parameters.GetProperty("visible").GetBoolean();
                        var target=Reference(doc,s.Id,false);
                        if(target.IsLocked||target.IsReference||doc.Layers[target.Attributes.LayerIndex].IsLocked)throw new InvalidOperationException("Visibility target became read-only.");
                        if(target.IsHidden==visible)
                        {
                            if(visible?!doc.Objects.Show(s.Id,true):!doc.Objects.Hide(s.Id,true))throw new InvalidOperationException("Rhino could not change visibility.");
                            modified.Add(s.Id);
                        }
                        break;
                    case "move_object": case "scale_object":case "rotate_object":
                        if (!doc.Objects.Replace(s.Id,s.Geometry!,false)) throw new InvalidOperationException("Rhino rejected the geometry replacement.");
                        modified.Add(s.Id); break;
                    case "get_object_properties": properties.Add(ContextReader.Info(doc,Reference(doc,s.Id,false))); break;
                    case "delete_objects":
                        foreach(var e in s.Parameters.GetProperty("object_ids").EnumerateArray())
                        {
                            var deletedId=Guid.Parse(e.GetString()!);
                            var deleted=Reference(doc,deletedId,false);
                            if(deleted.IsLocked||deleted.IsReference||doc.Layers[deleted.Attributes.LayerIndex].IsLocked)throw new InvalidOperationException("Deletion target became read-only.");
                            // Hidden construction is still an explicitly addressed editable target.
                            // The Guid overload respects hidden mode and silently refuses deletion.
                            // Locks and reference objects are rejected above, before ignoring modes.
                            if(!doc.Objects.Delete(deleted,true,true))throw new InvalidOperationException("Rhino could not delete the explicitly requested target.");
                            modified.Add(deletedId);
                        }
                        break;
                    case "select_objects":
                        foreach(var e in s.Parameters.GetProperty("object_ids").EnumerateArray())
                            if (!doc.Objects.Select(Guid.Parse(e.GetString()!))) throw new InvalidOperationException("Rhino could not select the object.");
                        break;
                }
            }
        }
        catch (Exception ex) { failure = ex; }
        finally { doc.EndUndoRecord(serial); }
        if (failure is not null)
        {
            if(created.Count==0&&modified.Count==0&&doc.Layers.Count==layerCountBefore&&doc.Materials.Count==materialCountBefore)
                throw new InvalidOperationException("Execution failed before any geometry or layer changes. "+failure.Message,failure);
            // Modeless Undo opens a redo-recording scope in Rhino 8. Close only
            // the scope created by this synchronous rollback: there was no active
            // record after EndUndoRecord above, and this runs on the UI thread.
            if (!doc.Undo()) throw new InvalidOperationException("Execution failed and rollback failed. Inspect the document before continuing.",failure);
            var rollbackRecord=doc.CurrentUndoRecordSerialNumber;
            if(rollbackRecord!=0&&!doc.EndUndoRecord(rollbackRecord))
                throw new InvalidOperationException("Geometry rollback completed, but Rhino could not finish its Undo scope. Save and reopen the document before continuing.",failure);
            doc.Views.Redraw();
            throw new InvalidOperationException("Execution failed; the action batch was undone. " + failure.Message,failure);
        }
        doc.Views.Redraw();
        return new ExecutionReport(created,modified.Distinct().ToList(),properties,serial);
    }
    public static void CheckCurrent(RhinoDoc doc, PreparedPlan prepared)
    {
        if (doc.RuntimeSerialNumber != prepared.DocumentId || doc.ModelUnitSystem != prepared.Units || doc.ModelAbsoluteTolerance != prepared.Tolerance)
            throw new InvalidOperationException("The active document, units or tolerance changed. Generate a new preview.");
        foreach (var pair in prepared.Fingerprints)
        {
            var obj = Reference(doc,pair.Key,prepared.Steps.Any(s=>s.Id==pair.Key&&MutatesTarget(s.Tool)));
            if (obj.Geometry.DataCRC(0) != pair.Value.Crc || obj.Attributes.LayerIndex != pair.Value.Layer)
                throw new InvalidOperationException("A referenced object changed after preview. Generate a new preview.");
        }
    }
    private static bool MutatesTarget(string tool)=>tool is "move_object" or "scale_object" or "rotate_object";
    private static RhinoObject Reference(RhinoDoc doc, Guid id, bool editable)
    {
        var obj = doc.Objects.FindId(id);
        if (obj is null || obj.IsDeleted) throw new ArgumentException($"Object {id} no longer exists.");
        var layer = doc.Layers[obj.Attributes.LayerIndex];
        if (editable && (obj.IsLocked || obj.IsHidden || layer.IsLocked || !layer.IsVisible || obj.IsReference))
            throw new ArgumentException("Referenced object is locked, hidden or read-only.");
        return obj;
    }
    private static int EnsureLayer(RhinoDoc doc, string name)
    {
        var existing = doc.Layers.FindName(name);
        if (existing is not null) return existing.Index;
        var color=name.EndsWith("_Glazing")?System.Drawing.Color.FromArgb(105,177,198):name.EndsWith("_Slabs")?System.Drawing.Color.FromArgb(202,205,218):name.EndsWith("_Structure")?System.Drawing.Color.FromArgb(112,120,145):System.Drawing.Color.FromArgb(143,112,244);
        int index = doc.Layers.Add(name,color);
        if(index>=0&&name.EndsWith("_Glazing"))
        {
            var material=new Material{Name="ArchiTec Glass",DiffuseColor=color,Transparency=.72,Shine=130};
            var materialIndex=doc.Materials.Add(material);doc.Layers[index].RenderMaterialIndex=materialIndex;
        }
        return index >= 0 ? index : throw new InvalidOperationException("Could not create layer: " + name);
    }
    private static Point3d Pt(JsonElement e,double factor) { var p = PlanValidator.Coordinates(e); return new Point3d(p[0]*factor,p[1]*factor,p[2]*factor); }
    private static UnitSystem Unit(string unit,RhinoDoc doc) => unit switch
    { "meters"=>UnitSystem.Meters,"millimeters"=>UnitSystem.Millimeters,"centimeters"=>UnitSystem.Centimeters,"feet"=>UnitSystem.Feet,"inches"=>UnitSystem.Inches,_=>doc.ModelUnitSystem };
}

public sealed class PlanConduit : DisplayConduit
{
    private readonly uint documentId;
    private readonly List<GeometryBase> geometry;
    public PlanConduit(RhinoDoc doc, IEnumerable<GeometryBase> items) { documentId = doc.RuntimeSerialNumber; geometry = items.ToList(); }
    protected override void CalculateBoundingBox(CalculateBoundingBoxEventArgs e)
    { if (e.RhinoDoc.RuntimeSerialNumber == documentId) foreach(var g in geometry) e.IncludeBoundingBox(g.GetBoundingBox(true)); }
    protected override void PostDrawObjects(DrawEventArgs e)
    {
        if (e.RhinoDoc.RuntimeSerialNumber != documentId) return;
        var color = System.Drawing.Color.FromArgb(170,132,255);
        foreach (var g in geometry)
            if (g is Brep b) e.Display.DrawBrepWires(b,color,2);
            else if (g is Curve c) e.Display.DrawCurve(c,color,2);
    }
}
