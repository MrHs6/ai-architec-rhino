namespace ArchiTec.Core;

public sealed record ProviderPreset(string Name,string Endpoint,string Protocol,string Notes);
public static class ProviderProfiles
{
    public static readonly ProviderPreset[] All=[
        new("OpenRouter","https://openrouter.ai/api/v1/","OpenAI","Connect validates your API key, then lists models with structured output support. Vision follows the live catalog."),
        new("OpenAI","https://api.openai.com/v1/","OpenAI Responses","Direct OpenAI Responses API with JSON schema. Models come from your account; choose a text model that supports structured output. Vision is not confirmed by this catalog."),
        new("xAI / Grok","https://api.x.ai/v1/","OpenAI","Direct xAI connection. Language models and image capabilities are read from your account catalog."),
        new("Google Gemini","https://generativelanguage.googleapis.com/v1beta/","Gemini","Direct Gemini GenerateContent API. Uses x-goog-api-key and native structured output/image parts."),
        new("Anthropic / Claude","https://api.anthropic.com/v1/","Anthropic","Direct Anthropic Messages API. Uses x-api-key; structured output and vision follow model capabilities."),
        new("OpenAI-compatible / local","http://127.0.0.1:1234/v1/","OpenAI","Custom or local API. Requires /models, /chat/completions and JSON schema. Image support is unconfirmed.")];
    public static ProviderPreset Get(string source)=>All.FirstOrDefault(p=>p.Name==source)??throw new ArgumentException("Unknown AI service.");
    public static string ValidateEndpoint(string source,string endpoint)
    {
        var preset=Get(source);
        if(!Uri.TryCreate(endpoint.TrimEnd('/')+"/",UriKind.Absolute,out var uri)||uri.UserInfo.Length>0||uri.Query.Length>0||uri.Fragment.Length>0||!(uri.Scheme=="https"||uri.Scheme=="http"&&uri.IsLoopback))throw new ArgumentException("Use an HTTPS API base URL, or localhost HTTP, without embedded credentials.");
        if(source!="OpenAI-compatible / local"&&uri!=new Uri(preset.Endpoint))throw new ArgumentException($"{source} uses {preset.Endpoint}");
        return uri.AbsoluteUri;
    }
    public static void ValidateCredentialSource(string source,string key)
    {
        if(source=="OpenAI-compatible / local")return;
        var owner=key.StartsWith("sk-or-v1-",StringComparison.Ordinal)?"OpenRouter":key.StartsWith("sk-ant-",StringComparison.Ordinal)?"Anthropic / Claude":key.StartsWith("sk-proj-",StringComparison.Ordinal)||key.StartsWith("sk-svcacct-",StringComparison.Ordinal)?"OpenAI":key.StartsWith("xai-",StringComparison.Ordinal)?"xAI / Grok":null;
        if(owner is not null&&source!=owner)throw new ArgumentException($"This key appears to belong to {owner}. Select {owner} before connecting; it was not sent to {source}.");
    }
    public static IPlannerClient Create(string source,string endpoint,string key,HttpMessageHandler? handler=null)
    {
        var preset=Get(source);
        ValidateEndpoint(source,endpoint);ValidateCredentialSource(source,key);
        if(source=="OpenAI")return new OpenAiResponsesClient(endpoint,key,handler);
        if(source is "Google Gemini" or "Anthropic / Claude")return new NativeProviderClient(preset,endpoint,key,handler);
        return new ProviderClient(endpoint,key,source=="OpenRouter",handler,source=="xAI / Grok");
    }
}

internal static class ProviderReplyReader
{
    public static PlanResponse Parse(string raw,PlanRequest request)
    {
        var result=Wire.Read<Reply>(raw);
        if(result.Message is null||result.Message.Length>20000)throw new InvalidOperationException("Provider response text is missing or too large.");
        var plan=result.Plan is null?null:new ActionPlan{Version=result.Plan.Version,Summary=result.Plan.Summary,LengthUnit=result.Plan.LengthUnit,
            Actions=(result.Plan.Actions??throw new InvalidOperationException("Missing action list.")).Select(a=>new ModelAction{Tool=a.Tool,Parameters=Parameters(a.ParametersJson)}).ToList()};
        if(plan is not null)PlanValidator.Validate(plan);
        if(request.Mode=="analyze"&&plan is not null)throw new InvalidOperationException("Analyze response included modeling actions and was rejected.");
        return new PlanResponse(result.Message,plan,request.Model);
    }
    private sealed record Reply(string Message,Plan? Plan);
    private static System.Text.Json.JsonElement Parameters(string raw){using var json=System.Text.Json.JsonDocument.Parse(raw);return json.RootElement.Clone();}
    private sealed record Plan(int Version,string Summary,string LengthUnit,List<Action>? Actions);
    private sealed record Action(string Tool,string ParametersJson);
}
