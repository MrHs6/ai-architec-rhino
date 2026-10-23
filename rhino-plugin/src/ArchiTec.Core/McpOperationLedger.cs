using System.Security.Cryptography;
using System.Text.Json;

namespace ArchiTec.Core;

/// <summary>Session receipts survive listener reconnects. Never evict a receipt and silently re-execute its ID.</summary>
public sealed class McpOperationLedger
{
    private sealed record Entry(uint Document,string Fingerprint){public object Receipt{get;set;}=new{status="processing"};}
    private readonly Dictionary<string,Entry> entries=[];
    public bool Begin(string id,uint document,ActionPlan plan,out object receipt)
    {
        if(!Guid.TryParse(id,out var uuid)||uuid==Guid.Empty)throw new ArgumentException("request_id must be a non-empty UUID; keep it to retrieve the outcome.");
        id=uuid.ToString("D");
        PlanValidator.Validate(plan);
        var hash=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(plan,Wire.Json)));
        if(entries.TryGetValue(id,out var prior))
        {
            if(prior.Document!=document||prior.Fingerprint!=hash)throw new InvalidOperationException("request_id is already bound to another document or plan. Inspect its receipt; do not reuse IDs for different operations.");
            receipt=prior.Receipt;return false;
        }
        if(entries.Count>=2000)throw new InvalidOperationException("This panel session has reached its receipt limit. Save your work and restart Rhino before submitting a new batch.");
        var entry=new Entry(document,hash);entries.Add(id,entry);receipt=entry.Receipt;return true;
    }
    private static string Normalize(string id)=>Guid.TryParse(id,out var uuid)?uuid.ToString("D"):id;
    public object Get(string id)=>entries.TryGetValue(Normalize(id),out var entry)?entry.Receipt:new{status="unknown",message="No receipt in this running Rhino panel session. Do not blindly repeat an uncertain operation."};
    public void Complete(string id,object receipt){if(!entries.TryGetValue(Normalize(id),out var entry))throw new InvalidOperationException("Unregistered operation.");entry.Receipt=receipt;}
    public bool Contains(string id)=>entries.ContainsKey(Normalize(id));
}
