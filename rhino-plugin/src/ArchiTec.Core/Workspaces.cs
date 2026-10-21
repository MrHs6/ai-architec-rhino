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
/// <summary>Model history and recent targets belong to one open document, never another document.</summary>
public sealed class DocumentWorkspaceState
{
    public WorkspaceSessions Sessions {get;}=new();
    public List<Guid> RecentIds {get;}=[];
    public Dictionary<Guid,uint> GeometrySignatures {get;}=[];
    public string? LastReport {get;set;}
    public bool Persian {get;set;}
    public int Revision {get;set;}
}
public sealed class DocumentWorkspaces
{
    private readonly Dictionary<uint,DocumentWorkspaceState> documents=[];
    public DocumentWorkspaceState For(uint documentId)
    {
        if(!documents.TryGetValue(documentId,out var state))
        {
            // Bound session memory; no prompts, keys or model data are written to disk here.
            if(documents.Count>=16)documents.Remove(documents.Keys.First());
            documents[documentId]=state=new();
        }
        return state;
    }
}
public static class ConversationIntent
{
    public static bool IsPersian(string text)=>text.Any(c=>c>='\u0600'&&c<='\u06FF');
    public static bool IsExplicitEdit(string text)
    {
        var value=text.Replace('\u200c',' ').ToLowerInvariant();
        if(System.Text.RegularExpressions.Regex.IsMatch(value,@"\b(create|build|new)\b|ساختمان جدید|بساز|ایجاد کن"))return false;
        return System.Text.RegularExpressions.Regex.IsMatch(value,@"\b(move|shift|scale|extrude|modify|edit|resize|change)\b|جا\s*به\s*جا|جابجا|تغییر|اصلاح|منتقل|بزرگ\s*تر|کوچک\s*تر");
    }
}
public static class ExecutionFeedback
{
    public static string Describe(int revision,int created,int modified,IReadOnlyList<ObjectInfo> objects,string units,bool persian)
    {
        var lines=new List<string>{persian?$"نتیجهٔ ثبت‌شده در راینو · مرحلهٔ {revision}\n{created} جزء ساخته شد و {modified} جزء تغییر کرد.":$"CONFIRMED IN RHINO · revision {revision}\n{created} native objects created · {modified} modified."};
        if(objects.Count>0)
        {
            var dimensions=Enumerable.Range(0,3).Select(axis=>objects.Max(o=>o.Max[axis])-objects.Min(o=>o.Min[axis])).Select(n=>n.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture));
            var size=string.Join(" × ",dimensions);
            lines.Add(persian?$"ابعاد محدودهٔ نتیجه: {size} {units}.":$"Measured result bounds: {size} {units}.");
            var floors=objects.Where(o=>o.Floor is not null).Select(o=>o.Floor!.Value).Distinct().Order().ToArray();
            if(floors.Length>0)lines.Add(persian?"شمارهٔ طبقات موجود در نتیجه: "+string.Join(", ",floors):"Floor metadata in this result: "+string.Join(", ",floors));
            foreach(var group in objects.GroupBy(o=>o.Role??o.Type).Take(8))lines.Add($"{group.Key}: {group.Count()}");
        }
        lines.Add(persian?"برای ادامه، تغییر بعدی را در چت بنویسید یا Refine result را بزنید. شناسهٔ اشیا برای ادامه حفظ می‌شود؛ Ctrl+Z این مرحله را برمی‌گرداند.":"Describe the next edit in chat, or choose Refine result. Existing target GUIDs are retained for follow-ups. Rhino Ctrl+Z undoes this operation.");
        return string.Join("\n",lines);
    }
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
