using System.Text.Json;
namespace ArchiTec.Core;

/// <summary>Session-only state; never writes prompts, images or model data into connection settings.</summary>
public sealed class WorkspaceState
{
    public string Prompt {get;set;}="";
    public string? ImageData {get;set;}
    public string? ImageName {get;set;}
    public List<ChatMessage> History {get;}=[];
}
public sealed class WorkspaceSessions
{
    private readonly Dictionary<string,WorkspaceState> states=new(){["generate"]=new(),["analyze"]=new(),["assist"]=new()};
    public WorkspaceState this[string mode]=>states.TryGetValue(mode,out var value)?value:throw new ArgumentException("Unknown workspace.");
}
public static class WorkspacePolicy
{
    public static void Validate(string mode,ActionPlan? plan,ModelContext context)
    {
        if(plan is null)return;
        PlanValidator.Validate(plan);
        if(mode=="analyze")throw new InvalidOperationException("Analyze is read-only. Use Model Assist to edit geometry.");
        if(mode=="generate"&&plan.Actions.Any(a=>a.Tool is "move_object" or "scale_object" or "select_objects"))
            throw new InvalidOperationException("Generate creates new geometry. Switch to Model Assist to edit existing objects.");
        if(mode=="assist")
        {
            if(context.Objects.Length==0)throw new InvalidOperationException("Select target objects or choose recent output in Model Assist.");
            foreach(var action in plan.Actions)
            {
                if(action.Tool is not ("move_object" or "scale_object" or "extrude_curve" or "select_objects" or "get_object_properties"))
                    throw new InvalidOperationException("Model Assist edits or inspects existing targets. Use Generate for new concept geometry.");
                if(action.Parameters.TryGetProperty("object_id",out var id)&&!context.Objects.Any(o=>o.Id==id.GetString()))
                    throw new InvalidOperationException("The proposed target is outside this workspace's target scope.");
                if(action.Tool=="select_objects"&&action.Parameters.GetProperty("object_ids").EnumerateArray().Any(id=>!context.Objects.Any(o=>o.Id==id.GetString())))
                    throw new InvalidOperationException("Selection is outside this workspace's target scope.");
            }
        }
    }
}
