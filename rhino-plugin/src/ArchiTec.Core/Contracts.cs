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
        "extrude_curve", "move_object", "scale_object", "create_layer", "select_objects", "get_object_properties"];

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
            "create_layer" => ["name"],
            "select_objects" => ["object_ids"],
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
            case "create_layer": Text(p, "name"); break;
            case "select_objects":
                var ids = Required(p, "object_ids");
                if (ids.ValueKind != JsonValueKind.Array || ids.GetArrayLength() is < 1 or > 100)
                    throw new ArgumentException("object_ids must contain 1–100 IDs.");
                foreach (var id in ids.EnumerateArray()) ParseId(id);
                break;
            case "get_object_properties": Id(p, "object_id"); break;
        }
        foreach (var name in new[] { "layer", "name" }) if (p.TryGetProperty(name, out _)) Text(p, name);
        foreach (var name in new[] { "origin", "center" }) if (p.TryGetProperty(name, out var point)) Coordinates(point);
        foreach (var name in new[] { "cap", "closed" }) if (p.TryGetProperty(name, out var b) && b.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new ArgumentException($"{name} must be boolean.");
    }
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
