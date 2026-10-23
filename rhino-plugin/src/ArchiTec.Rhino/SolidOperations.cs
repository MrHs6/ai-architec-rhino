using System.Text.Json;
using ArchiTec.Core;
using Rhino.Geometry;

namespace ArchiTec.RhinoPlugin;

public static class SolidOperations
{
    private static Point3d Pt(JsonElement e,double scale){var p=PlanValidator.Coordinates(e);return new(p[0]*scale,p[1]*scale,p[2]*scale);}
    private static Brep BrepSource(Guid id,Func<Guid,GeometryBase> get)=>get(id) as Brep??throw new ArgumentException("This operation requires native Brep sources, not construction lines or meshes.");
    private static Guid[] Ids(JsonElement p,string key)=>p.GetProperty(key).EnumerateArray().Select(v=>Guid.Parse(v.GetString()!)).ToArray();
    private static Brep Combine(Brep[]? parts,double tolerance,bool solid=false)
    {
        if(parts is null||parts.Length==0)throw new ArgumentException("The native operation returned no geometry. Check intersections and source validity.");
        var result=new Brep();foreach(var part in parts){result.Append(part);part.Dispose();}
        if(!result.IsValid||(solid&&!result.IsSolid)){result.Dispose();throw new ArgumentException("The operation did not produce valid requested solid geometry.");}
        return result;
    }
    public static GeometryBase Create(string tool,JsonElement p,double scale,double tolerance,Func<Guid,GeometryBase> source)
    {
        if(tool=="create_geometry")
        {
            var common=Rhino.Runtime.CommonObject.FromJSON(p.GetProperty("geometry_json").GetString()!);
            if(common is not GeometryBase geometry){common?.Dispose();throw new ArgumentException("The native payload is not geometry.");}
            if(geometry is not (Brep or Curve or Mesh or Surface or Extrusion or SubD)||!geometry.IsValid){geometry.Dispose();throw new ArgumentException("Unsupported or invalid native geometry payload.");}
            if(scale!=1&&!geometry.Transform(Transform.Scale(Point3d.Origin,scale))){geometry.Dispose();throw new ArgumentException("Could not scale the native geometry to document units.");}
            return geometry;
        }
        if(tool=="create_boolean")
        {
            var objects=Ids(p,"object_ids").Select(id=>BrepSource(id,source)).ToArray();
            var operation=p.GetProperty("operation").GetString();
            if(operation=="join")return Combine(Brep.JoinBreps(objects,tolerance),tolerance);
            if(objects.Any(b=>!b.IsSolid))throw new ArgumentException("Boolean operands must be closed valid Breps.");
            if(operation=="union")return Combine(Brep.CreateBooleanUnion(objects,tolerance),tolerance,true);
            if(!p.TryGetProperty("cutter_ids",out _))throw new ArgumentException("difference/intersection require cutter_ids.");
            var cutters=Ids(p,"cutter_ids").Select(id=>BrepSource(id,source)).ToArray();
            if(cutters.Any(b=>!b.IsSolid))throw new ArgumentException("Boolean cutters must be closed valid Breps.");
            return Combine(operation=="difference"?Brep.CreateBooleanDifference(objects,cutters,tolerance):Brep.CreateBooleanIntersection(objects,cutters,tolerance),tolerance,true);
        }
        if(tool=="create_sweep")
        {
            var rail=source(Guid.Parse(p.GetProperty("rail_id").GetString()!)) as Curve??throw new ArgumentException("The sweep rail must be a curve.");
            var sections=Ids(p,"section_ids").Select(id=>source(id) as Curve??throw new ArgumentException("Sweep sections must be curves.")).ToArray();
            var brep=Combine(Brep.CreateFromSweep(rail,sections,false,tolerance),tolerance);
            return Cap(brep,p,tolerance);
        }
        if(tool=="create_offset")
        {
            var brep=BrepSource(PlanValidator.Id(p,"object_id"),source);
            var solid=p.TryGetProperty("solid",out var s)&&s.GetBoolean();
            var pieces=Brep.CreateOffsetBrep(brep,PlanValidator.Number(p,"distance")*scale,solid,false,tolerance,out var blends,out var walls);
            if(blends is not null)foreach(var part in blends)part.Dispose();if(walls is not null)foreach(var part in walls)part.Dispose();
            return Combine(pieces,tolerance,solid);
        }
        var points=p.GetProperty("points").EnumerateArray().Select(v=>Pt(v,scale)).ToArray();
        using Curve curve=tool=="create_pipe"&&points.Length>=4&&p.TryGetProperty("smooth",out var smooth)&&smooth.GetBoolean()?Curve.CreateInterpolatedCurve(points,3):new PolylineCurve(points);
        if(tool=="create_pipe")return Combine(Brep.CreatePipe(curve,PlanValidator.Number(p,"radius")*scale,false,PipeCapMode.Round,true,tolerance,.01),tolerance,true);
        var axis=new Line(Pt(p.GetProperty("axis_start"),scale),Pt(p.GetProperty("axis_end"),scale));
        if(axis.Length<=tolerance)throw new ArgumentException("Revolution axis is below tolerance.");
        using var surface=RevSurface.Create(curve,axis,0,PlanValidator.Number(p,"angle_degrees")*Math.PI/180)??throw new ArgumentException("Revolve failed.");
        return Cap(surface.ToBrep(),p,tolerance);
    }
    private static Brep Cap(Brep brep,JsonElement p,double tolerance)
    {
        if(!p.TryGetProperty("cap",out var cap)||!cap.GetBoolean())return brep;
        var result=brep.CapPlanarHoles(tolerance);brep.Dispose();
        if(result is null||!result.IsValid||!result.IsSolid){result?.Dispose();throw new ArgumentException("Could not cap this operation as a valid solid.");}
        return result;
    }
}
