using System.Text.Json;
using ArchiTec.Core;
using Rhino.Geometry;

namespace ArchiTec.RhinoPlugin;

/// <summary>Bounded native geometry operations, with no script or command execution.</summary>
public static class SurfaceTools
{
    private static Point3d Pt(JsonElement e,double scale){var v=PlanValidator.Coordinates(e);return new(v[0]*scale,v[1]*scale,v[2]*scale);}
    public static GeometryBase Create(string tool,JsonElement p,double scale,double tolerance)
    {
        if(tool=="create_mesh")
        {
            var mesh=new Mesh();
            try
            {
                foreach(var vertex in p.GetProperty("vertices").EnumerateArray())mesh.Vertices.Add(Pt(vertex,scale));
                foreach(var face in p.GetProperty("faces").EnumerateArray())
                {var f=face.EnumerateArray().Select(v=>v.GetInt32()).ToArray();if(f.Length==3)mesh.Faces.AddFace(f[0],f[1],f[2]);else mesh.Faces.AddFace(f[0],f[1],f[2],f[3]);}
                mesh.Vertices.CombineIdentical(true,true);mesh.UnifyNormals();mesh.Normals.ComputeNormals();mesh.Compact();
                if(!mesh.IsValid||mesh.Faces.Count==0)throw new ArgumentException("The mesh is invalid or degenerate.");
                if(!p.TryGetProperty("require_solid",out var solid)||solid.GetBoolean())
                {
                    if(!mesh.IsClosed||!mesh.IsManifold(true,out _,out _))throw new ArgumentException("A closed manifold mesh is required. Repair its topology before retrying.");
                    if(mesh.SolidOrientation()<0)mesh.Flip(true,true,true);
                }
                return mesh;
            }
            catch{mesh.Dispose();throw;}
        }
        if(tool=="create_extrusion")
        {
            var points=p.GetProperty("points").EnumerateArray().Select(v=>Pt(v,scale)).ToList();
            var cap=!p.TryGetProperty("cap",out var c)||c.GetBoolean();
            if(cap&&points[0].DistanceTo(points[^1])>tolerance)points.Add(points[0]);
            using var profile=new PolylineCurve(points);
            if(!profile.IsPlanar(tolerance))throw new ArgumentException("The extrusion profile must be planar.");
            var vector=PlanValidator.Point(p,"vector");var direction=new Vector3d(vector[0]*scale,vector[1]*scale,vector[2]*scale);
            if(direction.Length<=tolerance)throw new ArgumentException("The extrusion vector is below document tolerance.");
            using var surface=Surface.CreateExtrusion(profile,direction)??throw new ArgumentException("Extrusion failed.");
            var brep=surface.ToBrep();
            if(!cap)return brep;
            var result=brep.CapPlanarHoles(tolerance);brep.Dispose();
            if(result is null||!result.IsSolid||!result.IsValid){result?.Dispose();throw new ArgumentException("The extrusion could not produce a valid closed solid.");}
            return result;
        }
        var curves=new List<Curve>();
        try
        {
            var closed=!p.TryGetProperty("closed_profiles",out var cp)||cp.GetBoolean();
            var degree=p.TryGetProperty("curve_degree",out var d)?d.GetInt32():1;
            foreach(var section in p.GetProperty("profiles").EnumerateArray())
            {
                var points=section.EnumerateArray().Select(v=>Pt(v,scale)).ToList();
                if(closed&&points[0].DistanceTo(points[^1])>tolerance)points.Add(points[0]);
                Curve curve=degree==1?new PolylineCurve(points):Curve.CreateInterpolatedCurve(points,3)??throw new ArgumentException("Profile interpolation failed.");
                curves.Add(curve);
                if(!curve.IsValid||curve.GetLength()<=tolerance)throw new ArgumentException("A loft profile is invalid or below tolerance.");
            }
            var pieces=Brep.CreateFromLoft(curves,Point3d.Unset,Point3d.Unset,LoftType.Normal,false);
            if(pieces is null||pieces.Length==0)throw new ArgumentException("Loft failed. Check profile order, orientation and seams.");
            Brep result;
            if(pieces.Length==1)result=pieces[0];
            else
            {
                var joined=Brep.JoinBreps(pieces,tolerance);foreach(var piece in pieces)piece.Dispose();
                if(joined is null||joined.Length!=1){if(joined is not null)foreach(var piece in joined)piece.Dispose();throw new ArgumentException("Loft did not form a single connected Brep.");}
                result=joined[0];
            }
            if(p.TryGetProperty("cap",out var cap)&&cap.GetBoolean())
            {
                var capped=result.CapPlanarHoles(tolerance);result.Dispose();result=capped??throw new ArgumentException("Loft caps failed; end sections must be closed and planar.");
                if(!result.IsSolid){result.Dispose();throw new ArgumentException("The capped loft is not a closed solid.");}
            }
            if(!result.IsValid){result.Dispose();throw new ArgumentException("The loft produced an invalid Brep.");}
            return result;
        }
        finally{foreach(var curve in curves)curve.Dispose();}
    }
}
