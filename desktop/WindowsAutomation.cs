using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
namespace Studio;

internal static class WindowsAutomation
{
    static readonly Dictionary<string,string> ProgIds=new(){
        ["Photoshop"]="Photoshop.Application",["Illustrator"]="Illustrator.Application",
        ["InDesign"]="InDesign.Application",["AutoCAD"]="AutoCAD.Application"};
    internal static bool Supports(string host)=>ProgIds.ContainsKey(host.Split(" · ")[0]);
    [DllImport("ole32.dll",CharSet=CharSet.Unicode)] static extern int CLSIDFromProgIDEx(string name,out Guid clsid);
    [DllImport("oleaut32.dll",PreserveSig=false)] static extern void GetActiveObject(ref Guid clsid,IntPtr reserved,[MarshalAs(UnmanagedType.IUnknown)] out object app);
    internal static JsonArray Tools(string host){
        var tools=new JsonArray(
        Wire.Node(new{name="get_context",description="Inspect the active document, application version and layer names.",inputSchema=new{type="object",properties=new{},additionalProperties=false}}),
        Wire.Node(new{name="create_layer",description="Ensure a named layer exists in this document. Read get_context first.",inputSchema=new{type="object",properties=new{document_id=new{type="string"},name=new{type="string"}},required=new[]{"document_id","name"},additionalProperties=false}}));
        if(host.Split(" · ")[0]=="AutoCAD"){
            var point=new{type="array",minItems=3,maxItems=3,items=new{type="number"}};
            tools.Add(Wire.Node(new{name="save_document",description="Save the active named DWG in place when requested.",inputSchema=new{type="object",properties=new{document_id=new{type="string"}},required=new[]{"document_id"},additionalProperties=false}}));
            tools.Add(Wire.Node(new{name="move_entity",description="Move an existing DWG entity from one WCS point to another using its document handle.",inputSchema=new{type="object",properties=new{document_id=new{type="string"},handle=new{type="string"},start=point,end=point},required=new[]{"document_id","handle","start","end"},additionalProperties=false}}));
            tools.Add(Wire.Node(new{name="create_line",description="Create one straight line in model space, WCS drawing units. Read the document ID first.",inputSchema=new{
                type="object",properties=new{document_id=new{type="string"},start=point,end=point},required=new[]{"document_id","start","end"},additionalProperties=false}}));
        }
        return tools;
    }
    static double[] Point(JsonNode? node){
        if(node is not JsonArray point || point.Count!=3)throw new IOException("Supply three coordinates.");
        return point.Select(n=>n is JsonValue v && v.TryGetValue<double>(out var x) && double.IsFinite(x)?x:throw new IOException("Invalid point.")).ToArray();
    }
    internal static JsonNode Call(string host,string tool,JsonNode args)
    {
        var parts=host.Split(" · ");host=parts[0];
        string? expected=null;
        if(parts.Length>1){
            int year=int.Parse(parts[1]);
            expected=host switch {"AutoCAD"=>year switch{2025=>"25.0",2026=>"25.1",2027=>"26.0",_=>throw new IOException("Unknown release.")},"Photoshop"=>(year-1999).ToString(),"Illustrator"=>(year-1996).ToString(),"InDesign"=>(year-2005).ToString(),_=>null};
        }
        string prog=ProgIds[host];
        if (host=="AutoCAD" && expected!=null)
        {
            string specific = prog+"."+expected;
            if (Type.GetTypeFromProgID(specific) != null) prog=specific;
            else if (Type.GetTypeFromProgID(prog+"."+expected.Split('.')[0]) != null) prog+="."+expected.Split('.')[0];
        }
        if(CLSIDFromProgIDEx(prog,out var id)<0)throw new IOException("Install the application's Windows scripting interface or configure an MCP connector.");
        object obj;try{GetActiveObject(ref id,IntPtr.Zero,out obj);}catch(COMException){throw new IOException("Open the application and a document, or configure its MCP connector.");}
        dynamic app=obj;
        try{
            var actual=Convert.ToString(app.Version) as string ?? "";
            if(expected!=null && !System.Text.RegularExpressions.Regex.IsMatch(actual,@"^"+System.Text.RegularExpressions.Regex.Escape(expected)+@"(?![0-9])"))throw new IOException("The running application is version "+actual+". Open the selected edition.");
            dynamic doc=app.ActiveDocument;
            string documentName=Convert.ToString(doc.Name);
            string documentId;
            try{documentId=Convert.ToString(doc.FullName);}catch(COMException){documentId=documentName;}
            if(string.IsNullOrEmpty(documentId))documentId=documentName;
            dynamic layers=host=="Photoshop"?doc.ArtLayers:doc.Layers;
            if(tool=="get_context"){
                var names=new List<string>();
                foreach(dynamic layer in layers){names.Add(Convert.ToString(layer.Name));if(names.Count>=150)break;}
                return Wire.Text(Wire.Node(new{document_id=documentId,document=documentName,version=Convert.ToString(app.Version),layers=names}).ToJsonString());
            }
            if((string?)args["document_id"]!=documentId)throw new IOException("Refresh the active document before editing.");
            if(tool=="save_document" && host=="AutoCAD"){
                if(string.IsNullOrEmpty(Convert.ToString(doc.Path)))throw new IOException("Name the drawing before saving.");
                doc.Save();return Wire.Text("Saved "+documentId);
            }
            if(tool=="move_entity" && host=="AutoCAD"){
                string handle=(string?)args["handle"]??throw new IOException("Missing entity handle.");
                dynamic entity=doc.HandleToObject(handle);
                entity.Move(Point(args["start"]),Point(args["end"]));
                return Wire.Text("Moved entity "+handle);
            }
            if(tool=="create_line" && host=="AutoCAD"){
                var start=Point(args["start"]);var end=Point(args["end"]);
                if(start.SequenceEqual(end))throw new IOException("Choose two distinct points.");
                dynamic line=doc.ModelSpace.AddLine(start,end);
                return Wire.Text(Wire.Node(new{document_id=documentId,handle=Convert.ToString(line.Handle),start,end}).ToJsonString());
            }
            if(tool!="create_layer")throw new IOException("Unsupported document tool.");
            string name=((string?)args["name"]??"").Trim();if(name.Length is 0 or >120)throw new IOException("Use a layer name of 1–120 characters.");
            foreach(dynamic layer in layers)if(string.Equals(Convert.ToString(layer.Name),name,StringComparison.OrdinalIgnoreCase))return Wire.Text("Layer already exists: "+name);
            dynamic created;
            if(host=="AutoCAD")created=layers.Add(name);
            else{created=layers.Add();created.Name=name;}
            return Wire.Text("Created layer: "+Convert.ToString(created.Name));
        }finally{Marshal.ReleaseComObject(obj);}
    }
}
