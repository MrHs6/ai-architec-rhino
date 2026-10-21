using System.Globalization;
using ArchiTec.Core;
using Rhino;
using Rhino.Geometry;

namespace ArchiTec.RhinoPlugin;
public static class ModelAudit
{
    public static string Report(RhinoDoc doc,ModelContext context,string focus)
    {
        var lines=new List<string>{$"LOCAL RHINO AUDIT · {focus}",$"Document: {context.ObjectCount} objects · {context.Units}",$"Scoped objects: {context.Objects.Length} · read-only"};
        if(context.Truncated)lines.Add("Context was truncated. Narrow the selection for a complete audit.");
        if(context.Objects.Length==0){lines.Add("Select objects in Rhino or choose Recent generated output.");return string.Join("\n",lines);}
        if(focus=="layers")
        {
            foreach(var group in context.Objects.GroupBy(o=>o.Layer))
            {var layer=doc.Layers.FindByFullPath(group.Key,-1);var item=layer>=0?doc.Layers[layer]:null;lines.Add($"{group.Key}: {group.Count()} scoped objects · {(item?.IsVisible==false?"hidden":"visible")} · {(item?.IsLocked==true?"locked":"unlocked")}");}
        }
        else foreach(var info in context.Objects)
        {
            var obj=doc.Objects.FindId(Guid.Parse(info.Id));if(obj is null)continue;
            var name=string.IsNullOrWhiteSpace(info.Name)?info.Type:info.Name;
            if(focus=="health")
            {var shape=obj.Geometry switch{Brep brep=>brep.IsSolid?"closed solid":"open Brep",Curve curve=>curve.IsClosed?"closed curve":"open curve",Mesh mesh=>mesh.IsClosed?"closed mesh":"open mesh",_=>obj.ObjectType.ToString()};lines.Add($"{name}: {(obj.Geometry.IsValid?"valid":"INVALID")} · {shape} · {info.Layer}");}
            else lines.Add($"{name}: {N(info.Max[0]-info.Min[0])} × {N(info.Max[1]-info.Min[1])} × {N(info.Max[2]-info.Min[2])} {context.Units} · {info.Layer}");
        }
        lines.Add("Measured Rhino geometry only; this is not structural, code, energy or daylight simulation.");return string.Join("\n",lines);
    }
    private static string N(double value)=>value.ToString("0.###",CultureInfo.InvariantCulture);
}
