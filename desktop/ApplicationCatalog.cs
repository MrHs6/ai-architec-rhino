namespace Studio;

internal sealed record ApplicationCatalog(string Name,string Mark,string Color,string Interface,string Docs,string[] Editions)
{
    internal static readonly ApplicationCatalog[] All =
    [
        new("Rhino","Rh","#B9B8CC","RhinoCommon · Archi-Tec MCP","https://developer.rhino3d.com/",["Automatic","8","9"]),
        new("Revit","R","#77AFF8","Revit API · BIM documents","https://aps.autodesk.com/developer/overview/revit",["2025","2026","2027"]),
        new("Grasshopper","Gh","#97D19C","Parametric definitions through Rhino","https://developer.rhino3d.com/guides/grasshopper/",["Automatic","8","9"]),
        new("AutoCAD","A","#FF8A96","ActiveX · DWG layers","https://aps.autodesk.com/developer/overview/autocad",["Automatic","2025","2026","2027"]),
        new("3ds Max","Mx","#86CFD5","3ds Max SDK · scene tools","https://aps.autodesk.com/developer/overview/3ds-max",["Automatic","2025","2026","2027"]),
        new("SketchUp","Su","#80B6EF","Ruby API · groups and components","https://ruby.sketchup.com/",["Automatic","2025","2026"]),
        new("Archicad","Ac","#A6A8E8","Graphisoft Add-On API · BIM elements","https://graphisoft.github.io/archicad-api-devkit/",["Automatic","28","29","30"]),
        new("Blender","Bl","#FFB07D","Python API · objects and scenes","https://docs.blender.org/api/current/",["Automatic","4.x","5.x"]),
        new("Photoshop","Ps","#64BCFF","Photoshop scripting · raster layers","https://developer.adobe.com/photoshop/uxp/",["Automatic","2023","2025","2026"]),
        new("Illustrator","Ai","#FFB267","Illustrator scripting · vector layers","https://developer.adobe.com/illustrator/",["Automatic","2025","2026"]),
        new("InDesign","Id","#FF92C0","InDesign scripting · layout layers","https://developer.adobe.com/indesign/uxp/",["Automatic","2025","2026"]),
        new("FreeCAD","Fc","#90B9DF","Parametric CAD · document and mesh tools","https://www.freecad.org/",["Automatic","1.x"]),
        new("Maya","Ma","#7CD4C5","Maya Python API · editable meshes","https://help.autodesk.com/view/MAYAUL/2025/ENU/",["Automatic","2025","2026","2027"]),
        new("Both","⇄","#C3A5FF","Revit wall axes to Rhino curves","https://ai-arch.ir/home",["Automatic"])
    ];
    internal static ApplicationCatalog? Find(string name)=>All.FirstOrDefault(a=>a.Name==name);
}
