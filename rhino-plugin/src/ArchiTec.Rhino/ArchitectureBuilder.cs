using System.Text.Json;
using ArchiTec.Core;
using Rhino;
using Rhino.Geometry;
using Rhino.DocObjects;

namespace ArchiTec.RhinoPlugin;

/// <summary>Deterministic concept-building generator. Every component is a separate editable native Brep.</summary>
internal static class ArchitectureBuilder
{
    internal static List<PreparedAction> Create(JsonElement p,double scale,RhinoDoc doc)
    {
        var w=PlanValidator.Number(p,"width")*scale;var d=PlanValidator.Number(p,"depth")*scale;
        var h=PlanValidator.Number(p,"floor_height")*scale;var floors=(int)PlanValidator.Number(p,"floors");
        var meter=RhinoMath.UnitScale(UnitSystem.Meters,doc.ModelUnitSystem);
        if(w<meter||d<meter||w>1000*meter||d>1000*meter||h<meter||h>20*meter)throw new ArgumentException("Concept buildings require width/depth 1–1000 meters and floor height 1–20 meters.");
        var slab=p.TryGetProperty("slab_thickness",out var thickness)?thickness.GetDouble()*scale:Math.Min(.25*meter,h*.08);
        if(slab<doc.ModelAbsoluteTolerance*2||slab>=h*.4)throw new ArgumentException("Slab thickness must exceed tolerance and remain below 40% of floor height.");
        var column=Math.Min(.3*meter,Math.Min(w,d)/10);var wall=Math.Min(.2*meter,Math.Min(w,d)/15);
        var facade=p.TryGetProperty("facade",out var f)?f.GetString():"glazed";
        var panelThickness=facade=="solid"?wall:Math.Max(.04*meter,doc.ModelAbsoluteTolerance*2);
        var origin=p.TryGetProperty("origin",out var o)?PlanValidator.Coordinates(o):new double[]{0,0,0};
        var basePoint=new Point3d(origin[0]*scale,origin[1]*scale,origin[2]*scale);
        var prefix=p.TryGetProperty("layer",out var l)?l.GetString()!:"AI_Building";
        if(prefix.Length>85||prefix.Contains("::")||!ModelComponent.IsValidComponentName(prefix))throw new ArgumentException("Use a single-level layer prefix with at most 85 characters.");
        var result=new List<PreparedAction>();
        void Part(double x,double y,double z,double width,double depth,double height,string layer,string role,int floor)
        {
            var start=basePoint+new Vector3d(x,y,z);
            var geometry=new Box(new Plane(start,Vector3d.ZAxis),new Interval(0,width),new Interval(0,depth),new Interval(0,height)).ToBrep();
            if(geometry is null||!geometry.IsValid)throw new ArgumentException("A building component could not be constructed.");
            var parameters=JsonSerializer.SerializeToElement(new{name=$"Level {floor:00} · {role}"},Wire.Json);
            result.Add(new PreparedAction("create_box",parameters,Guid.Empty,geometry,prefix+"_"+layer,role,floor));
        }
        try
        {
            for(int level=0;level<floors;level++)
            {
                var z=level*h;var clearHeight=h-slab-(level==floors-1?slab:0);
                Part(0,0,z,w,d,slab,"Slabs","floor slab",level+1);
                foreach(var x in new[]{0,w-column})foreach(var y in new[]{0,d-column})
                    Part(x,y,z+slab,column,column,clearHeight,"Structure","column",level+1);
                if(facade!="none")
                {
                    var layer=facade=="solid"?"Walls":"Glazing";var role=facade=="solid"?"wall":"glazing";
                    Part(column,0,z+slab,w-2*column,panelThickness,clearHeight,layer,role,level+1);
                    Part(column,d-panelThickness,z+slab,w-2*column,panelThickness,clearHeight,layer,role,level+1);
                    Part(0,column,z+slab,panelThickness,d-2*column,clearHeight,layer,role,level+1);
                    Part(w-panelThickness,column,z+slab,panelThickness,d-2*column,clearHeight,layer,role,level+1);
                }
            }
            Part(0,0,floors*h-slab,w,d,slab,"Slabs","roof slab",floors);
            return result;
        }
        catch{foreach(var item in result)item.Geometry?.Dispose();throw;}
    }
}
