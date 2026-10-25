using System.Text.Json;
using System.Drawing;
using ArchiTec.Core;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Special;
using Grasshopper.Kernel.Undo;
using GH_IO.Serialization;
using Rhino;
using Rhino.Geometry;
using Rhino.DocObjects;

namespace ArchiTec.RhinoPlugin;

/// <summary>UI-thread graph execution through installed native component libraries.</summary>
public static class GrasshopperBridge
{
    private static readonly Dictionary<ulong,(uint RhinoId,GH_Document Document,GrasshopperDefinition Recipe)> Managed = [];
    private static readonly HashSet<string> Libraries = new(StringComparer.OrdinalIgnoreCase)
    { "CurveComponents.gha","SurfaceComponents.gha","VectorComponents.gha","MathComponents.gha","XformComponents.gha","TriangulationComponents.gha","FieldComponents.gha" };
    private static IEnumerable<IGH_ObjectProxy> Catalog => Instances.ComponentServer.ObjectProxies.Where(Allowed);
    private static bool Allowed(IGH_ObjectProxy proxy)
    {
        var root=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(GH_Document).Assembly.Location)!,"Components"))+Path.DirectorySeparatorChar;
        return !proxy.Obsolete && typeof(IGH_Component).IsAssignableFrom(proxy.Type)
            && Path.GetFullPath(proxy.Location).StartsWith(root,StringComparison.OrdinalIgnoreCase) && Libraries.Contains(Path.GetFileName(proxy.Location));
    }
    private static object Describe(IGH_ObjectProxy proxy)
    {
        var c=(IGH_Component)Instances.ComponentServer.EmitObject(proxy.Guid);
        return new { component_id=proxy.Guid,name=c.Name,description=c.Description,category=c.Category,subcategory=c.SubCategory,
            inputs=c.Params.Input.Select((p,i)=>new {index=i,name=p.Name,type=p.TypeName,access=p.Access.ToString(),optional=p.Optional}),
            outputs=c.Params.Output.Select((p,i)=>new {index=i,name=p.Name,type=p.TypeName}) };
    }
    public static object Search(string query,int limit)
    {
        if(query.Length>120 || limit is <1 or >80)throw new ArgumentException("Use a query of at most 120 characters and limit 1–80.");
        var matches=Catalog.Where(p=>string.IsNullOrWhiteSpace(query) || (p.Desc.Name+" "+p.Desc.NickName+" "+p.Desc.Category+" "+p.Desc.Description).Contains(query,StringComparison.OrdinalIgnoreCase)).OrderBy(p=>p.Desc.Name).ToArray();
        return new {components=matches.Take(limit).Select(Describe).ToArray(),total=matches.Length,truncated=matches.Length>limit,policy="Built-in geometry/math libraries only. No scripts, clusters, file/network IO or third-party components."};
    }
    public static object PlanningCatalog()
    {
        string[] names=["Domain Box","Center Box","Box 2Pt","Rectangle","Circle","Construct Point","Construct Plane","World XY","XY Plane","Unit X","Unit Y","Unit Z","Extrude","Cap Holes","Loft","Boundary Surfaces","Move","Rotate","Scale","Series","Range","Construct Domain","Deconstruct Brep","Addition","Multiplication","Division","Line","Line SDL","Polygon","Surface From Points","Divide Curve","Amplitude","Merge","Flatten Tree","Graft Tree"];
        return Catalog.Where(p=>names.Contains(p.Desc.Name,StringComparer.OrdinalIgnoreCase)).Take(80).Select(Describe).ToArray();
    }
    private static object Ports(IGH_DocumentObject n)
    {
        var inputs=n is IGH_Component c?c.Params.Input.ToArray():Array.Empty<IGH_Param>();
        var outputs=n is IGH_Component comp?comp.Params.Output.ToArray():n is IGH_Param param?[param]:Array.Empty<IGH_Param>();
        return new { inputs=inputs.Select((p,i)=>new {index=i,name=p.Name,type=p.TypeName,sources=p.Sources.Select(s=>new {node_id=s.Attributes.GetTopLevel.DocObject.InstanceGuid,param_id=s.InstanceGuid}),branches=p.VolatileData.PathCount,count=p.VolatileDataCount}),
            outputs=outputs.Select((p,i)=>new {index=i,name=p.Name,type=p.TypeName,branches=p.VolatileData.PathCount,count=p.VolatileDataCount,samples=p.VolatileData.AllData(true).Take(5).Select(v=>v?.ToString())}) };
    }
    public static object Context(uint id)
    {
        foreach(var key in Managed.Where(p=>!Instances.DocumentServer.Any(d=>d==p.Value.Document)).Select(p=>p.Key).ToArray())Managed.Remove(key);
        var active=Instances.ActiveCanvas?.Document;
        return new {active_definition_id=active?.RuntimeID,definitions=Instances.DocumentServer.Select(d=>new {definition_id=d.RuntimeID,name=d.DisplayName,editable=Managed.TryGetValue(d.RuntimeID,out var m)&&m.RhinoId==id,node_count=d.ObjectCount}).ToArray(),
            active=active is null?null:new {definition_id=active.RuntimeID,enabled=active.Enabled,last_recipe=Managed.TryGetValue(active.RuntimeID,out var managed)&&managed.RhinoId==id?managed.Recipe:null,nodes=active.Objects.Take(200).Select(n=>new {node_id=n.InstanceGuid,component_id=n.ComponentGuid,name=n.Name,nickname=n.NickName,x=n.Attributes.Pivot.X,y=n.Attributes.Pivot.Y,slider=n is GH_NumberSlider slider?new {value=slider.CurrentValue,minimum=slider.Slider.Minimum,maximum=slider.Slider.Maximum}:null,ports=Ports(n),messages=Messages(n)}).ToArray(),truncated=active.ObjectCount>200},
            workflow="Build/update with grasshopper_definition. Existing non-bridge definitions are inspected only. Use the receipt's definition_id to update a bridge-created graph. Save .gh through Grasshopper normally."};
    }
    private static object Messages(IGH_DocumentObject n)=>n is IGH_ActiveObject a?new {errors=a.RuntimeMessages(GH_RuntimeMessageLevel.Error).ToArray(),warnings=a.RuntimeMessages(GH_RuntimeMessageLevel.Warning).ToArray()}:new {errors=Array.Empty<string>(),warnings=Array.Empty<string>()};
    internal static GrasshopperPrepared Prepare(RhinoDoc rhino,ActionPlan plan)
    {
        var recipe=GrasshopperPlan.Read(plan.Actions.Single().Parameters);GH_Document? target=null;
        if(recipe.DefinitionId is {} id)
        {
            if(!Managed.TryGetValue(id,out var entry)||entry.RhinoId!=rhino.RuntimeSerialNumber || !Instances.DocumentServer.Any(d=>d==entry.Document))throw new ArgumentException("Definition is not an open bridge-created graph in this Rhino document. Read get_grasshopper_context.");
            target=entry.Document;
            if(target.SolutionDepth>0 || target.ScheduleDelay>=0)throw new InvalidOperationException("The target definition is solving or scheduled. Stop it before replacing the graph.");
        }
        var candidate=new GH_Document();var nodes=new Dictionary<string,IGH_DocumentObject>();
        try
        {
            foreach(var node in recipe.Nodes)
            {
                IGH_DocumentObject obj;
                if(node.Kind=="slider")
                {
                    var s=new GH_NumberSlider();s.CreateAttributes();s.Slider.Minimum=node.Minimum;s.Slider.Maximum=node.Maximum;s.Slider.DecimalPlaces=6;s.SetSliderValue(node.Value);obj=s;
                }
                else
                {
                    var proxy=Catalog.FirstOrDefault(p=>p.Guid==node.ComponentId)??throw new ArgumentException($"Component {node.ComponentId} is unavailable or unsupported. Search installed components first.");
                    obj=Instances.ComponentServer.EmitObject(proxy.Guid)??throw new ArgumentException("Component could not be instantiated.");obj.CreateAttributes();var c=(IGH_Component)obj;
                    foreach(var group in node.Inputs.GroupBy(i=>i.Index))
                    {
                        if(group.Key>=c.Params.Input.Count)throw new ArgumentException($"Input {group.Key} does not exist on {obj.Name}.");
                        SetData(c.Params.Input[group.Key],group);
                    }
                }
                obj.Attributes.Pivot=new PointF((float)node.X,(float)node.Y);if(node.Nickname is not null)obj.NickName=node.Nickname;
                if(!candidate.AddObject(obj,false))throw new InvalidOperationException("Could not add a graph node.");nodes.Add(node.Id,obj);
            }
            foreach(var wire in recipe.Wires)
            {
                var source=Output(nodes[wire.From],wire.Output);
                if(nodes[wire.To] is not IGH_Component dest || wire.Input>=dest.Params.Input.Count)throw new ArgumentException("A wire target input does not exist.");
                dest.Params.Input[wire.Input].AddSource(source);
            }
            return new GrasshopperPrepared(rhino.RuntimeSerialNumber,candidate,target,recipe,nodes);
        }
        catch {candidate.Dispose();throw;}
    }
    private static IGH_Param Output(IGH_DocumentObject n,int i)
    {
        if(n is IGH_Component c && i<c.Params.Output.Count)return c.Params.Output[i];
        if(n is IGH_Param p && i==0)return p;throw new ArgumentException("A wire source output does not exist.");
    }
    private static void SetData(IGH_Param p,IEnumerable<GrasshopperInput> b)
    {
        if(p is GH_PersistentParam<GH_Number> n){Data(n,b,v=>new GH_Number(v.GetDouble()),"number");return;}
        if(p is GH_PersistentParam<GH_Integer> i){Data(i,b,v=>new GH_Integer(v.GetInt32()),"integer");return;}
        if(p is GH_PersistentParam<GH_Boolean> bo){Data(bo,b,v=>new GH_Boolean(v.GetBoolean()),"boolean");return;}
        if(p is GH_PersistentParam<GH_String> s){Data(s,b,v=>new GH_String(v.GetString()),"text");return;}
        if(p is GH_PersistentParam<GH_Point> pt){Data(pt,b,v=>new GH_Point(Pt(v)),"point");return;}
        if(p is GH_PersistentParam<GH_Vector> ve){Data(ve,b,v=>new GH_Vector(new Vector3d(Pt(v))),"vector");return;}
        if(p is GH_PersistentParam<GH_Interval> it){Data(it,b,v=>new GH_Interval(new Interval(v[0].GetDouble(),v[1].GetDouble())),"interval");return;}
        if(p is GH_PersistentParam<GH_Plane> pl){Data(pl,b,v=>new GH_Plane(new Plane(Pt(v),Vector3d.ZAxis)),"plane_xy");return;}
        throw new ArgumentException($"{p.Name} does not support typed inline data. Construct its data using wired components instead.");
    }
    private static Point3d Pt(JsonElement v){var p=PlanValidator.Coordinates(v);return new Point3d(p[0],p[1],p[2]);}
    private static void Data<T>(GH_PersistentParam<T> p,IEnumerable<GrasshopperInput> branches,Func<JsonElement,T> convert,string type) where T:class,IGH_Goo
    {
        p.PersistentData.Clear();foreach(var branch in branches)
        {
            if(branch.Type!=type)throw new ArgumentException($"Input {p.Name} expects type {type}.");
            foreach(var v in branch.Values.EnumerateArray())p.PersistentData.Append(convert(v),new GH_Path(branch.Path));
        }
        p.ExpireSolution(false);
    }
    private static GH_Archive Archive(GH_Document doc){var a=new GH_Archive();if(!a.AppendObject(doc,"Definition"))throw new InvalidOperationException("Cannot snapshot Grasshopper definition.");return a;}
    internal sealed class GrasshopperPrepared : IPreparedGrasshopper
    {
        private readonly uint rhinoId;private readonly GH_Document candidate;private readonly GH_Document? target;
        private readonly GrasshopperDefinition recipe;private readonly Dictionary<string,IGH_DocumentObject> nodes;
        private readonly string? fingerprint;private bool committed;
        internal GrasshopperPrepared(uint id,GH_Document c,GH_Document? t,GrasshopperDefinition r,Dictionary<string,IGH_DocumentObject> n)
        {rhinoId=id;candidate=c;target=t;recipe=r;nodes=n;fingerprint=t is null?null:Archive(t).Serialize_Xml();}
        public ExecutionReport Apply(RhinoDoc rhino,CancellationToken cancellation)
        {
            if(committed)throw new InvalidOperationException("This definition was already committed.");
            if(rhino.RuntimeSerialNumber!=rhinoId)throw new InvalidOperationException("The Rhino document changed.");
            if(target is not null && (!Instances.DocumentServer.Any(d=>d==target)||Archive(target).Serialize_Xml()!=fingerprint))throw new InvalidOperationException("The Grasshopper definition changed after preview. Request a fresh plan.");
            if(!GH_Document.EnableSolutions)throw new InvalidOperationException("Grasshopper solver is disabled. Enable it before execution.");
            cancellation.ThrowIfCancellationRequested();
            // A detached GH_Document starts disabled. Solve it BEFORE committing/baking, even when another canvas document is active.
            candidate.Enabled=true;
            using(var cancel=cancellation.Register(()=>candidate.RequestAbortSolution()))candidate.NewSolution(true,GH_SolutionMode.Silent);
            cancellation.ThrowIfCancellationRequested();
            var errors=candidate.Objects.OfType<IGH_ActiveObject>().SelectMany(n=>n.RuntimeMessages(GH_RuntimeMessageLevel.Error).Select(e=>n.Name+": "+e)).ToArray();
            if(errors.Length>0)throw new InvalidOperationException("Grasshopper solution failed; no definition or bake committed. "+string.Join(" | ",errors.Take(12)));
            if(candidate.Objects.OfType<IGH_Component>().Sum(c=>c.Params.Output.Sum(p=>(long)p.VolatileDataCount))>20000)throw new InvalidOperationException("Graph output exceeds the 20,000-item limit.");
            if(recipe.BakeNodes.Count>0 && rhino.CurrentUndoRecordSerialNumber!=0)throw new InvalidOperationException("Finish the current Rhino command before baking.");
            var serial=recipe.BakeNodes.Count>0?rhino.BeginUndoRecord("AI Archi-Tec: bake "+recipe.Name):0;
            if(recipe.BakeNodes.Count>0&&serial==0)throw new InvalidOperationException("Rhino Undo is unavailable for baking.");
            var created=new List<Guid>();var dest=target??candidate;var backup=target is null?null:Archive(target);var graphMutated=false;
            try
            {
                foreach(var id in recipe.BakeNodes)
                {
                    var node=nodes[id];var outputs=node is IGH_Component c?c.Params.Output.ToArray():new[]{Output(node,0)};
                    var data=outputs.SelectMany(p=>p.VolatileData.AllData(true)).ToArray();
                    if(data.Length==0 || created.Count+data.Length>1500)throw new ArgumentException("Bake nodes must produce 1–1500 geometry items in total.");
                    foreach(var goo in data)
                    {
                        cancellation.ThrowIfCancellationRequested();var a=new ObjectAttributes{Name=recipe.Name+" / "+id,LayerIndex=rhino.Layers.CurrentLayerIndex};
                        a.SetUserString("AIArchiTec.Grasshopper",dest.RuntimeID.ToString());
                        // GH_Box and several other geometric goo types do not implement IGH_BakeAwareData.
                        // Convert their native value explicitly rather than silently rejecting valid geometry.
                        using var geometry=NativeGeometry(goo);
                        if(!geometry.IsValid)throw new InvalidOperationException("Grasshopper produced invalid bake geometry.");
                        var baked=rhino.Objects.Add(geometry,a);
                        if(baked==Guid.Empty)throw new InvalidOperationException("Rhino rejected Grasshopper bake geometry.");created.Add(baked);
                    }
                }
                cancellation.ThrowIfCancellationRequested();
                if(target is not null)
                {
                    var record=target.UndoUtil.CreateRemoveObjectEvent("AI Archi-Tec: "+recipe.Name,target.Objects.ToArray());
                    graphMutated=true;
                    target.RemoveObjects(target.Objects.ToArray(),false);
                    if(!Archive(candidate).ExtractObject(target,"Definition"))throw new InvalidOperationException("Cannot commit graph snapshot.");
                    foreach(var action in target.UndoUtil.CreateAddObjectEvent("Replace definition",target.Objects.ToArray()).Actions)record.AddAction(action);
                    target.UndoUtil.RecordEvent(record);target.NewSolution(false,GH_SolutionMode.Silent);
                }
                else {Instances.DocumentServer.AddDocument(candidate);candidate.UndoUtil.RecordAddObjectEvent("AI Archi-Tec: "+recipe.Name,candidate.Objects.ToArray());}
                Managed[dest.RuntimeID]=(rhinoId,dest,recipe);committed=true;
                // Presenting the editor is best effort; a window failure must not report a committed graph as failed.
                string? uiWarning=null;
                try{Instances.DocumentEditor?.Show();if(Instances.ActiveCanvas is not null){Instances.ActiveCanvas.Document=dest;Instances.ActiveCanvas.Refresh();}rhino.Views.Redraw();}
                catch(Exception e){uiWarning="Definition committed; open Grasshopper to view it. "+e.Message;}
                var report=new {definition_id=dest.RuntimeID,name=recipe.Name,node_count=dest.ObjectCount,wire_count=recipe.Wires.Count,
                    nodes=dest.Objects.Select(n=>new {node_id=n.InstanceGuid,name=n.Name,ports=Ports(n),messages=Messages(n)}).ToArray(),bake_count=created.Count,ui_warning=uiWarning,undo="Graph: Grasshopper Undo. Baked geometry: Rhino Undo. Separate undo stacks."};
                return new ExecutionReport(created,[],[],serial,report);
            }
            catch
            {
                if(graphMutated&&target is not null&&backup is not null){target.RemoveObjects(target.Objects.ToArray(),false);if(!backup.ExtractObject(target,"Definition"))throw new InvalidOperationException("Graph rollback failed. Inspect Grasshopper before retrying.");target.NewSolution(true,GH_SolutionMode.Silent);}
                if(serial!=0){rhino.EndUndoRecord(serial);serial=0;if(created.Count>0){rhino.Undo();if(rhino.CurrentUndoRecordSerialNumber!=0)rhino.EndUndoRecord(rhino.CurrentUndoRecordSerialNumber);}}throw;
            }
            finally{if(serial!=0)rhino.EndUndoRecord(serial);}
        }
        public void Dispose(){if(!committed||target is not null)candidate.Dispose();}
    }
    private static GeometryBase NativeGeometry(IGH_Goo goo)=>goo.ScriptVariable() switch
    {
        GeometryBase g=>g.Duplicate(),
        Box b when b.IsValid=>b.ToBrep(),
        Rectangle3d r when r.IsValid=>r.ToNurbsCurve(),
        Circle c when c.IsValid=>c.ToNurbsCurve(),
        Arc a when a.IsValid=>a.ToNurbsCurve(),
        Line l when l.IsValid=>l.ToNurbsCurve(),
        Point3d p when p.IsValid=>new Rhino.Geometry.Point(p),
        _=>throw new ArgumentException("Bake requires native geometry (Brep, Surface, Curve, Mesh, Extrusion, SubD, Box, Rectangle, Circle, Arc, Line or Point). Numbers and other data cannot be baked.")
    };
}
