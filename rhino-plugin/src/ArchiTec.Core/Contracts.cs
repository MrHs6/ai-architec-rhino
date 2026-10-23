using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArchiTec.Core;

public static class Wire
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
        MaxDepth = 32
    };
    public static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, Json)
        ?? throw new ArgumentException("Empty JSON response.");
}

public sealed record ModelAction
{
    public string Tool { get; init; } = "";
    public JsonElement Parameters { get; init; }
    public static ModelAction Make(string tool, object parameters) => new()
    { Tool = tool, Parameters = JsonSerializer.SerializeToElement(parameters, Wire.Json) };
}

public sealed record ActionPlan
{
    public int Version { get; init; } = 1;
    public string Summary { get; init; } = "";
    public string LengthUnit { get; init; } = "meters";
    public List<ModelAction> Actions { get; init; } = [];
}

public sealed record ObjectInfo(string Id, string Type, string Layer, double[] Min, double[] Max,string? Name=null,string? Role=null,int? Floor=null);
public sealed record ModelContext(uint DocumentId, string Units, string ActiveLayer,
    string[] Layers, int ObjectCount, ObjectInfo[] Objects, string[] SelectedIds,
    string[] LastCreatedIds, bool Truncated);
public sealed record ChatMessage(string Role, string Content);
public sealed record PlanRequest(string Prompt, string Model, string Mode, ModelContext Context,
    List<ChatMessage> History, string? ImageDataUrl = null);
public sealed record PlanResponse(string Message, ActionPlan? Plan, string Model);
public sealed record ModelDescriptor(string Id, string Name, bool Vision);
public sealed record ModelCatalog(List<ModelDescriptor> Models);

public static class PlanValidator
{
    public static readonly string[] Tools = ["create_building", "create_box", "create_line", "create_polyline", "create_curve",
        "create_loft", "create_mesh", "create_extrusion", "create_boolean", "create_sweep", "create_revolve", "create_pipe", "create_offset", "create_geometry", "extrude_curve", "move_object", "scale_object", "rotate_object", "copy_object", "set_visibility", "create_layer", "select_objects", "delete_objects", "get_object_properties"];

    public static void Validate(ActionPlan plan)
    {
        if (plan.Version != 1) throw new ArgumentException("Unsupported action plan version.");
        if (plan.LengthUnit is not ("meters" or "millimeters" or "centimeters" or "feet" or "inches" or "document"))
            throw new ArgumentException("Unsupported length_unit.");
        if (string.IsNullOrWhiteSpace(plan.Summary) || plan.Summary.Length > 4000)
            throw new ArgumentException("A concise plan summary is required.");
        if (plan.Actions is null || plan.Actions.Count is < 1 or > 100)
            throw new ArgumentException("Plans must contain 1–100 actions.");
        foreach (var action in plan.Actions) ValidateAction(action);
    }

    public static void ValidateAction(ModelAction a)
    {
        if (a is null || !Tools.Contains(a.Tool)) throw new ArgumentException("Unknown modeling tool.");
        var p = a.Parameters;
        if (p.ValueKind != JsonValueKind.Object) throw new ArgumentException("Parameters must be an object.");
        if(p.EnumerateObject().Select(v=>v.Name).Distinct().Count()!=p.EnumerateObject().Count())throw new ArgumentException("Duplicate tool parameters are not allowed.");
        string[] allowed = a.Tool switch
        {
            "create_building" => ["width","depth","floors","floor_height","slab_thickness","facade","origin","layer"],
            "create_box" => ["width", "depth", "height", "origin", "layer", "name"],
            "create_line" => ["start", "end", "layer", "name"],
            "create_polyline" or "create_curve" => ["points", "closed", "layer", "name"],
            "extrude_curve" => ["object_id", "height", "cap", "layer", "name"],
            "move_object" => ["object_id", "vector"],
            "scale_object" => ["object_id", "factor", "center"],
            "create_loft" => ["profiles","closed_profiles","curve_degree","cap","layer","name"],
            "create_mesh" => ["vertices","faces","require_solid","layer","name"],
            "create_extrusion" => ["points","vector","cap","layer","name"],
            "create_boolean" => ["operation","object_ids","cutter_ids","layer","name"],
            "create_sweep" => ["rail_id","section_ids","cap","layer","name"],
            "create_revolve" => ["points","axis_start","axis_end","angle_degrees","cap","layer","name"],
            "create_pipe" => ["points","radius","smooth","layer","name"],
            "create_offset" => ["object_id","distance","solid","layer","name"],
            "create_geometry" => ["geometry_json","layer","name"],
            "copy_object" => ["object_id","vector","layer","name"],
            "rotate_object" => ["object_id","axis_origin","axis_direction","angle_degrees"],
            "set_visibility" => ["object_id","visible"],
            "create_layer" => ["name","color","transparency"],
            "select_objects" or "delete_objects" => ["object_ids"],
            _ => ["object_id"]
        };
        foreach (var prop in p.EnumerateObject())
            if (!allowed.Contains(prop.Name)) throw new ArgumentException($"Unknown parameter '{prop.Name}' for {a.Tool}.");
        switch (a.Tool)
        {
            case "create_building":
                Positive(p,"width");Positive(p,"depth");Positive(p,"floor_height");
                var floors=Number(p,"floors");if(floors!=Math.Truncate(floors)||floors is <1 or >50)throw new ArgumentException("floors must be an integer from 1 to 50.");
                if(p.TryGetProperty("slab_thickness",out _)){Positive(p,"slab_thickness");if(Number(p,"slab_thickness")>=Number(p,"floor_height"))throw new ArgumentException("Slab thickness must be below floor height.");}
                if(p.TryGetProperty("facade",out var facade)&&facade.GetString() is not ("glazed" or "solid" or "none"))throw new ArgumentException("facade must be glazed, solid or none.");
                break;
            case "create_box": Positive(p, "width"); Positive(p, "depth"); Positive(p, "height"); break;
            case "create_line": Point(p, "start"); Point(p, "end"); break;
            case "create_polyline": case "create_curve":
                var points = Required(p, "points");
                if (points.ValueKind != JsonValueKind.Array || points.GetArrayLength() is < 2 or > 1000)
                    throw new ArgumentException("points must contain 2–1000 coordinates.");
                foreach (var point in points.EnumerateArray()) Coordinates(point);
                break;
            case "extrude_curve": Id(p, "object_id"); Positive(p, "height"); break;
            case "move_object": Id(p, "object_id"); Point(p, "vector"); break;
            case "scale_object": Id(p, "object_id"); Positive(p, "factor", 1000); Point(p, "center"); break;
            case "create_loft":
                var profiles=Required(p,"profiles");
                if(profiles.ValueKind!=JsonValueKind.Array||profiles.GetArrayLength() is <2 or >64)throw new ArgumentException("profiles must contain 2–64 ordered sections.");
                foreach(var profile in profiles.EnumerateArray())ValidatePoints(profile,3,256);
                if(p.TryGetProperty("curve_degree",out var degree)&&(degree.ValueKind!=JsonValueKind.Number||!degree.TryGetInt32(out var d)||d is not (1 or 3)))throw new ArgumentException("curve_degree must be 1 or 3.");
                break;
            case "create_mesh":
                var vertices=Required(p,"vertices");ValidatePoints(vertices,4,20000);
                var faces=Required(p,"faces");
                if(faces.ValueKind!=JsonValueKind.Array||faces.GetArrayLength() is <1 or >40000)throw new ArgumentException("faces must contain 1–40000 triangles or quads.");
                foreach(var face in faces.EnumerateArray())
                {
                    if(face.ValueKind!=JsonValueKind.Array||face.GetArrayLength() is not (3 or 4))throw new ArgumentException("Each face requires 3 or 4 vertex indices.");
                    var indices=face.EnumerateArray().Select(v=>v.ValueKind==JsonValueKind.Number&&v.TryGetInt32(out var i)?i:-1).ToArray();
                    if(indices.Any(i=>i<0||i>=vertices.GetArrayLength())||indices.Distinct().Count()!=indices.Length)throw new ArgumentException("Mesh indices must be distinct valid vertex indices.");
                }
                break;
            case "create_extrusion": ValidatePoints(Required(p,"points"),3,1000);Point(p,"vector");break;
            case "create_boolean":
                if(Required(p,"operation").GetString() is not ("union" or "difference" or "intersection" or "join"))throw new ArgumentException("operation must be union, difference, intersection or join.");
                ValidateIds(Required(p,"object_ids"));if(p.TryGetProperty("cutter_ids",out var cutters))ValidateIds(cutters);break;
            case "create_sweep": ParseId(Required(p,"rail_id"));ValidateIds(Required(p,"section_ids"));break;
            case "create_revolve":ValidatePoints(Required(p,"points"),2,1000);Point(p,"axis_start");Point(p,"axis_end");Positive(p,"angle_degrees",360);break;
            case "create_pipe":ValidatePoints(Required(p,"points"),2,1000);Positive(p,"radius");break;
            case "create_offset":Id(p,"object_id");if(Math.Abs(Number(p,"distance"))<1e-9)throw new ArgumentException("Offset distance cannot be zero.");break;
            case "create_geometry":
                var encoded=Required(p,"geometry_json");if(encoded.ValueKind!=JsonValueKind.String||string.IsNullOrEmpty(encoded.GetString())||encoded.GetString()!.Length>1_500_000)throw new ArgumentException("Use a bounded native Rhino geometry JSON string.");break;
            case "copy_object":Id(p,"object_id");Point(p,"vector");break;
            case "rotate_object":Id(p,"object_id");Point(p,"axis_origin");Point(p,"axis_direction");Number(p,"angle_degrees");break;
            case "set_visibility": Id(p,"object_id");Required(p,"visible");break;
            case "create_layer":
                Text(p, "name");
                if(p.TryGetProperty("color",out var rgb))
                {
                    var values=Coordinates(rgb);if(values.Any(v=>v<0||v>255||v!=Math.Truncate(v)))throw new ArgumentException("color must contain three integer RGB values from 0 to 255.");
                }
                if(p.TryGetProperty("transparency",out _)){var t=Number(p,"transparency");if(t<0||t>1)throw new ArgumentException("transparency must be between 0 and 1.");}
                break;
            case "select_objects": case "delete_objects":
                var ids = Required(p, "object_ids");
                if (ids.ValueKind != JsonValueKind.Array || ids.GetArrayLength() is < 1 or > 100)
                    throw new ArgumentException("object_ids must contain 1–100 IDs.");
                foreach (var id in ids.EnumerateArray()) ParseId(id);
                break;
            case "get_object_properties": Id(p, "object_id"); break;
        }
        foreach (var name in new[] { "layer", "name" }) if (p.TryGetProperty(name, out _)) Text(p, name);
        foreach (var name in new[] { "origin", "center" }) if (p.TryGetProperty(name, out var point)) Coordinates(point);
        foreach (var name in new[] { "cap", "closed", "closed_profiles", "require_solid", "visible", "smooth", "solid" }) if (p.TryGetProperty(name, out var b) && b.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new ArgumentException($"{name} must be boolean.");
    }
    private static void ValidatePoints(JsonElement points,int minimum,int maximum)
    {
        if(points.ValueKind!=JsonValueKind.Array||points.GetArrayLength()<minimum||points.GetArrayLength()>maximum)throw new ArgumentException($"Expected {minimum}–{maximum} coordinate triples.");
        foreach(var point in points.EnumerateArray())Coordinates(point);
    }
    private static void ValidateIds(JsonElement ids)
    {if(ids.ValueKind!=JsonValueKind.Array||ids.GetArrayLength() is <1 or >64)throw new ArgumentException("Use 1–64 source IDs.");foreach(var id in ids.EnumerateArray())ParseId(id);}
    public static JsonElement Required(JsonElement p, string key) => p.TryGetProperty(key, out var value)
        ? value : throw new ArgumentException($"Missing parameter: {key}.");
    public static double Number(JsonElement p, string key) => Finite(Required(p, key));
    private static double Finite(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Number || !e.TryGetDouble(out var x) || !double.IsFinite(x) || Math.Abs(x) > 1e7)
            throw new ArgumentException("Numeric values must be finite and within ±10,000,000.");
        return x;
    }
    private static void Positive(JsonElement p, string key, double max = 100000)
    { var x = Number(p, key); if (x <= 0 || x > max) throw new ArgumentException($"{key} must be greater than zero and at most {max}."); }
    public static double[] Coordinates(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Array || e.GetArrayLength() != 3)
            throw new ArgumentException("Coordinates must be [x, y, z].");
        return e.EnumerateArray().Select(Finite).ToArray();
    }
    public static double[] Point(JsonElement p, string key) => Coordinates(Required(p, key));
    public static string Text(JsonElement p, string key)
    {
        var e = Required(p, key);
        if (e.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(e.GetString()) || e.GetString()!.Length > 120 || e.GetString()!.Any(char.IsControl))
            throw new ArgumentException($"{key} must contain 1–120 printable characters.");
        return e.GetString()!;
    }
    public static Guid Id(JsonElement p, string key) => ParseId(Required(p, key));
    private static Guid ParseId(JsonElement e) => e.ValueKind == JsonValueKind.String && Guid.TryParse(e.GetString(), out var id) && id != Guid.Empty
        ? id : throw new ArgumentException("Invalid object ID.");
}
