using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;

namespace Studio;

internal sealed record RouterModel(string Id, long Created, bool Tools);
internal static class RouterAccess
{
    static readonly HttpClient Client=new(new HttpClientHandler{AllowAutoRedirect=false}) { Timeout=TimeSpan.FromSeconds(20) };

    static async Task<JsonNode> Get(string path,string key,HttpClient client)
    {
        using var request=new HttpRequestMessage(HttpMethod.Get,"https://openrouter.ai/api/v1/"+path);
        request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);
        using var response=await client.SendAsync(request);
        if(!response.IsSuccessStatusCode)throw new IOException(Workspace.ProviderError("OpenRouter",(int)response.StatusCode));
        return JsonNode.Parse(await response.Content.ReadAsStringAsync())?["data"] ?? throw new IOException("OpenRouter returned an incomplete response.");
    }

    internal static async Task Verify(string key,HttpClient? client=null)
    {
        if(await Get("key",key,client??Client) is not JsonObject info || info["label"]==null)
            throw new IOException("OpenRouter did not confirm this key.");
    }

    internal static async Task<RouterModel[]> Models(string key,HttpClient? client=null)
    {
        var list=(await Get("models",key,client??Client)).AsArray();
        return list.OfType<JsonObject>().Where(m=>m["id"]!=null && (m["architecture"]?["output_modalities"] as JsonArray)?.Any(v=>(string?)v=="text")==true)
            .Select(m=>new RouterModel((string)m["id"]!, (long?)m["created"]??0,
                (m["supported_parameters"] as JsonArray)?.Any(v=>(string?)v=="tools")==true))
            .OrderByDescending(m=>m.Created).ThenBy(m=>m.Id).ToArray();
    }
}
