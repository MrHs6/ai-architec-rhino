using System.Text.Json;
using ArchiTec.Core;
using Rhino;
using Rhino.Geometry;
using Rhino.DocObjects;
using Rhino.Display;

namespace ArchiTec.RhinoPlugin;

public static class McpDocumentTools
{
    public static object Inspect(RhinoDoc doc,JsonElement args)
    {
        var ids=args.TryGetProperty("object_ids",out var list)?list.EnumerateArray().Select(v=>Guid.Parse(v.GetString()!)).ToHashSet():null;
        if(ids?.Count>2000)throw new ArgumentException("Use at most 2000 object IDs.");
        var prefix=args.TryGetProperty("layer_prefix",out var filter)?filter.GetString()??"":"";
        var includeHidden=args.TryGetProperty("include_hidden",out var hidden)&&hidden.GetBoolean();
        var limit=args.TryGetProperty("limit",out var l)?l.GetInt32():200;
        if(limit is <1 or >2000)throw new ArgumentException("limit must be between 1 and 2000.");
        var offset=args.TryGetProperty("offset",out var page)?page.GetInt32():0;
        if(offset is <0 or >1_000_000)throw new ArgumentException("offset must be between 0 and 1000000.");
        var probes=args.TryGetProperty("probe_points",out var probeArray)?probeArray.EnumerateArray().Select(Point).ToArray():Array.Empty<Point3d>();
        if(probes.Length>100||probes.Any(p=>!p.IsValid))throw new ArgumentException("Use at most 100 finite probe points in document units.");
        if(probes.Length>0&&(ids is null||ids.Count>20))throw new ArgumentException("Point containment probes require 1–20 explicit object IDs.");
        var enumeration=new ObjectEnumeratorSettings{NormalObjects=true,LockedObjects=true,HiddenObjects=includeHidden,ReferenceObjects=true,ObjectTypeFilter=ObjectType.AnyObject};
        var objects=doc.Objects.GetObjectList(enumeration).Where(o=>!o.IsDeleted&&(includeHidden||!o.IsHidden&&doc.Layers[o.Attributes.LayerIndex].IsVisible))
            .Where(o=>(ids is null||ids.Contains(o.Id))&&doc.Layers[o.Attributes.LayerIndex].FullPath.StartsWith(prefix,StringComparison.Ordinal)).Skip(offset).Take(limit+1).ToArray();
        return new{document_id=doc.RuntimeSerialNumber,units=doc.ModelUnitSystem.ToString(),offset,returned=Math.Min(limit,objects.Length),next_offset=objects.Length>limit?(int?)(offset+limit):null,truncated=objects.Length>limit,objects=objects.Take(limit).Select(o=>
        {
            var geometry=o.Geometry;bool? solid=geometry switch{Brep b=>b.IsSolid,Mesh m=>m.IsClosed&&m.IsManifold(true,out _,out _),Extrusion e=>e.IsSolid,_=>null};
            double? volume=null,area=null;
            try
            {
                if(geometry is Brep b){using var a=AreaMassProperties.Compute(b);area=a?.Area;if(b.IsSolid){using var v=VolumeMassProperties.Compute(b);volume=v?.Volume;}}
                if(geometry is Mesh m){using var a=AreaMassProperties.Compute(m);area=a?.Area;if(solid==true){using var v=VolumeMassProperties.Compute(m);volume=v?.Volume;}}
            }catch{/* Unsupported mass properties must not invalidate a read-only audit. */}
            bool[]? contains=geometry is Brep closed&&closed.IsSolid&&probes.Length>0
                ?probes.Select(p=>closed.IsPointInside(p,doc.ModelAbsoluteTolerance,false)).ToArray():null;
            return new{info=ContextReader.Info(doc,o),valid=geometry.IsValid,solid,volume,area,hidden=o.IsHidden,locked=o.IsLocked,contains_probe_points=contains};
        }).ToArray()};
    }
    public static object Frame(RhinoDoc doc,JsonElement args,IEnumerable<Guid> recent)
    {
        var ids=args.TryGetProperty("object_ids",out var value)?value.EnumerateArray().Select(v=>Guid.Parse(v.GetString()!)).ToArray():recent.ToArray();
        if(ids.Length is <1 or >2000)throw new ArgumentException("Frame 1–2000 explicit or recent objects.");
        var box=BoundingBox.Empty;
        foreach(var id in ids)
        {
            var obj=doc.Objects.FindId(id)??throw new ArgumentException("A framing target no longer exists.");
            if(obj.IsHidden||!doc.Layers[obj.Attributes.LayerIndex].IsVisible)throw new ArgumentException("Cannot frame hidden targets.");
            box.Union(obj.Geometry.GetBoundingBox(true));
        }
        var view=doc.Views.ActiveView??throw new InvalidOperationException("Open a Rhino viewport first.");
        var viewport=view.ActiveViewport;
        viewport.ChangeToPerspectiveProjection(true,50);
        if(args.TryGetProperty("display_mode",out var dm))
        {var name=dm.GetString();if(name is not ("Shaded" or "Rendered" or "Arctic" or "Wireframe"))throw new ArgumentException("Unsupported display mode.");viewport.DisplayMode=DisplayModeDescription.GetDisplayModes().First(m=>m.EnglishName==name);}
        var camera=args.TryGetProperty("camera_location",out var location)?Point(location):viewport.CameraLocation;
        var target=args.TryGetProperty("camera_target",out var aim)?Point(aim):viewport.CameraTarget;
        if(args.TryGetProperty("camera_location",out _)||args.TryGetProperty("camera_target",out _))
        {
            if(camera.DistanceTo(target)<=doc.ModelAbsoluteTolerance)throw new ArgumentException("Camera and target must be distinct.");
            viewport.SetCameraLocations(target,camera);
        }
        viewport.ZoomBoundingBox(box);view.Redraw();
        return new{status="framed",document_id=doc.RuntimeSerialNumber,count=ids.Length,min=new[]{box.Min.X,box.Min.Y,box.Min.Z},max=new[]{box.Max.X,box.Max.Y,box.Max.Z}};
    }
    private static Point3d Point(JsonElement e){var p=PlanValidator.Coordinates(e);return new(p[0],p[1],p[2]);}
    public static McpImage Capture(RhinoDoc doc)
    {
        var view=doc.Views.ActiveView??throw new InvalidOperationException("Open a Rhino viewport first.");
        var capture=new ViewCapture{Width=1280,Height=900,ScaleScreenItems=false,DrawAxes=false,DrawGrid=false,DrawGridAxes=false,TransparentBackground=false};
        using var bitmap=capture.CaptureToBitmap(view)??throw new InvalidOperationException("Viewport capture failed.");
        using var stream=new MemoryStream();bitmap.Save(stream,System.Drawing.Imaging.ImageFormat.Png);
        return new McpImage(Convert.ToBase64String(stream.ToArray()),"image/png",new{document_id=doc.RuntimeSerialNumber,view=view.ActiveViewport.Name,display_mode=view.ActiveViewport.DisplayMode.EnglishName,width=1280,height=900});
    }
}
