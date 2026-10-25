using System.Text.Json;

namespace ArchiTec.Core;

/// <summary>A complete editable graph, not generated source code. Values use Rhino document units.</summary>
public sealed record GrasshopperDefinition
{
    public string Name { get; init; } = "AI Archi-Tec definition";
    public ulong? DefinitionId { get; init; }
    public List<GrasshopperNode> Nodes { get; init; } = [];
    public List<GrasshopperWire> Wires { get; init; } = [];
    public List<string> BakeNodes { get; init; } = [];
}
public sealed record GrasshopperNode
{
    public string Id { get; init; } = "";
    public string Kind { get; init; } = "component";
    public Guid? ComponentId { get; init; }
    public string? Nickname { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
    public decimal Value { get; init; }
    public decimal Minimum { get; init; }
    public decimal Maximum { get; init; } = 100;
    public List<GrasshopperInput> Inputs { get; init; } = [];
}
public sealed record GrasshopperInput
{
    public int Index { get; init; }
    public string Type { get; init; } = "number";
    public int[] Path { get; init; } = [0];
    public JsonElement Values { get; init; }
}
public sealed record GrasshopperWire(string From, int Output, string To, int Input);

public static class GrasshopperPlan
{
    public const string Tool = "grasshopper_definition";
    public static bool IsGrasshopper(ActionPlan plan) => plan.Actions.Any(a => a?.Tool == Tool);
    public static GrasshopperDefinition Read(JsonElement parameters)
    {
        UniqueKeys(parameters);
        var graph = Wire.Read<GrasshopperDefinition>(parameters.GetRawText());
        if (string.IsNullOrWhiteSpace(graph.Name) || graph.Name.Length > 120 || graph.Name.Any(char.IsControl)) throw new ArgumentException("Use a printable definition name, at most 120 characters.");
        // GH_Document.RuntimeID is zero-based: zero is a valid first document.
        if (graph.Nodes is null || graph.Nodes.Count is < 1 or > 200 || graph.Wires is null || graph.Wires.Count > 500 || graph.BakeNodes is null || graph.BakeNodes.Count > 20) throw new ArgumentException("Use 1–200 nodes, at most 500 wires and 20 bake nodes.");
        var nodes = new Dictionary<string, GrasshopperNode>(StringComparer.Ordinal);
        foreach (var node in graph.Nodes)
        {
            if (node is null || !ValidId(node.Id) || !nodes.TryAdd(node.Id,node)) throw new ArgumentException("Node IDs must be unique, 1–64 ASCII letters, digits, underscores or hyphens.");
            if (!double.IsFinite(node.X) || !double.IsFinite(node.Y) || Math.Abs(node.X)>100000 || Math.Abs(node.Y)>100000) throw new ArgumentException("Invalid canvas position.");
            if (node.Nickname is not null && (node.Nickname.Length>120 || node.Nickname.Any(char.IsControl))) throw new ArgumentException("Invalid node nickname.");
            if (node.Kind is not ("component" or "slider")) throw new ArgumentException("Node kind must be component or slider.");
            if (node.Inputs is null || node.Inputs.Count>64) throw new ArgumentException("Too many input branches.");
            if (node.Kind == "component" && (node.ComponentId is null || node.ComponentId == Guid.Empty)) throw new ArgumentException("Discover a real component_id with search_grasshopper_components before building a graph.");
            if (node.Kind == "slider" && (node.ComponentId is not null || node.Minimum >= node.Maximum || node.Value<node.Minimum || node.Value>node.Maximum || Math.Abs(node.Minimum)>10000000 || Math.Abs(node.Maximum)>10000000 || node.Inputs.Count>0)) throw new ArgumentException("Slider needs a bounded minimum < maximum, value in range, and no component_id or inputs.");
            var paths = new HashSet<string>();
            foreach (var input in node.Inputs)
            {
                if (input is null || input.Index is <0 or >63 || input.Path is null || input.Path.Length is <1 or >8 || input.Path.Any(i=>i<0) || !paths.Add(input.Index+":"+string.Join(";",input.Path))) throw new ArgumentException("Invalid or duplicate input branch.");
                if (input.Values.ValueKind!=JsonValueKind.Array || input.Values.GetArrayLength() is <1 or >1000) throw new ArgumentException("Each input branch needs 1–1000 values.");
                foreach (var value in input.Values.EnumerateArray()) ValidateValue(input.Type,value);
            }
        }
        var wires = new HashSet<GrasshopperWire>();
        var indegrees=nodes.Keys.ToDictionary(id=>id,_=>0);
        var downstream=nodes.Keys.ToDictionary(id=>id,_=>new List<string>());
        foreach (var wire in graph.Wires)
        {
            if (wire is null || !nodes.ContainsKey(wire.From) || !nodes.ContainsKey(wire.To) || wire.From==wire.To || wire.Output is <0 or >63 || wire.Input is <0 or >63 || !wires.Add(wire)) throw new ArgumentException("Invalid, missing or duplicate wire endpoint.");
            if (nodes[wire.To].Inputs.Any(i=>i.Index==wire.Input)) throw new ArgumentException("An input cannot have both wires and persistent values.");
            indegrees[wire.To]++;downstream[wire.From].Add(wire.To);
        }
        var queue=new Queue<string>(indegrees.Where(p=>p.Value==0).Select(p=>p.Key));var seen=0;
        while(queue.TryDequeue(out var id)){seen++;foreach(var target in downstream[id])if(--indegrees[target]==0)queue.Enqueue(target);}
        if(seen!=nodes.Count)throw new ArgumentException("Feedback cycles are not supported. Use an acyclic Grasshopper definition.");
        if(graph.BakeNodes.Distinct().Count()!=graph.BakeNodes.Count || graph.BakeNodes.Any(id=>!nodes.ContainsKey(id)))throw new ArgumentException("Bake nodes must name distinct nodes in this graph.");
        return graph;
    }
    private static void UniqueKeys(JsonElement value)
    {
        if(value.ValueKind==JsonValueKind.Object)
        {
            var names=new HashSet<string>();foreach(var p in value.EnumerateObject()){if(!names.Add(p.Name))throw new ArgumentException("Duplicate graph fields are not allowed.");UniqueKeys(p.Value);}
        }
        else if(value.ValueKind==JsonValueKind.Array)foreach(var item in value.EnumerateArray())UniqueKeys(item);
    }
    private static bool ValidId(string id)=>!string.IsNullOrEmpty(id)&&id.Length<=64&&id.All(c=>char.IsAsciiLetterOrDigit(c)||c is '_' or '-');
    private static void ValidateValue(string type,JsonElement value)
    {
        switch(type)
        {
            case "number": Number(value);break;
            case "integer": Number(value);if(!value.TryGetInt32(out _))throw new ArgumentException("Expected an integer.");break;
            case "boolean": if(value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))throw new ArgumentException("Expected a boolean.");break;
            case "text": if(value.ValueKind!=JsonValueKind.String || value.GetString()!.Length>4000)throw new ArgumentException("Expected bounded text.");break;
            case "point":case "vector":case "plane_xy": PlanValidator.Coordinates(value);break;
            case "interval": if(value.ValueKind!=JsonValueKind.Array || value.GetArrayLength()!=2)throw new ArgumentException("Interval needs [start,end].");foreach(var v in value.EnumerateArray())Number(v);break;
            default: throw new ArgumentException("Supported input types: number, integer, boolean, text, point, vector, plane_xy, interval.");
        }
    }
    private static void Number(JsonElement value){if(value.ValueKind!=JsonValueKind.Number || !value.TryGetDouble(out var v) || !double.IsFinite(v) || Math.Abs(v)>10000000)throw new ArgumentException("Expected a finite bounded numeric value.");}
}
