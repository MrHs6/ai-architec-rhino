using System.Net;
using System.Text;
using ArchiTec.Core;

internal static class ProviderConnectionTests
{
    public static async Task<int> Run()
    {
        var passed=0;
        void Check(string name,bool condition){if(!condition)throw new Exception(name);passed++;Console.WriteLine("PASS "+name);}
        const string secret="fixture-private-key-do-not-display";
        using(var fixture=new Fixture())
        using(var client=ProviderProfiles.Create("OpenRouter",ProviderProfiles.Get("OpenRouter").Endpoint,secret,fixture))
        {
            var catalog=await client.Models(default);
            Check("OpenRouter confirms authentication before loading its public model catalog",fixture.Paths.SequenceEqual(new[]{"/api/v1/key","/api/v1/models"})&&fixture.Authorization.Count==2&&fixture.Authorization.All(v=>v=="Bearer "+secret));
            Check("Schema-capable OpenRouter models do not require unused tool calling",catalog.Models.Count==1&&catalog.Models[0].Id=="vendor/schema-only");
        }
        using(var fixture=new Fixture{KeyStatus=HttpStatusCode.Unauthorized,KeyBody="{\"error\":{\"code\":401,\"message\":\"Invalid credential fixture-private-key-do-not-display\"}}"})
        using(var client=ProviderProfiles.Create("OpenRouter",ProviderProfiles.Get("OpenRouter").Endpoint,secret,fixture))
        {
            var error=await Failure(()=>client.Models(default));
            Check("Rejected OpenRouter keys never reach public model discovery",fixture.Paths.SequenceEqual(new[]{"/api/v1/key"})&&error.Contains("401")&&!error.Contains(secret));
        }
        foreach(var flag in new[]{"is_management_key","is_provisioning_key"})
        {
            using var fixture=new Fixture{KeyBody="{\"data\":{\""+flag+"\":true}}"};
            using var client=ProviderProfiles.Create("OpenRouter",ProviderProfiles.Get("OpenRouter").Endpoint,secret,fixture);
            var error=await Failure(()=>client.Models(default));
            Check("OpenRouter rejects "+flag+" for inference",fixture.Paths.Count==1&&error.Contains("management key"));
        }
        using(var fixture=new Fixture{KeyBody="{\"data\":[]}"})
        using(var client=ProviderProfiles.Create("OpenRouter",ProviderProfiles.Get("OpenRouter").Endpoint,secret,fixture))
        {
            var error=await Failure(()=>client.Models(default));
            Check("Malformed key confirmation cannot establish OpenRouter connection",fixture.Paths.Count==1&&error.Contains("did not confirm"));
        }
        using(var fixture=new Fixture{KeyBody="[]"})
        using(var client=ProviderProfiles.Create("OpenRouter",ProviderProfiles.Get("OpenRouter").Endpoint,secret,fixture))
        {
            var error=await Failure(()=>client.Models(default));
            Check("Non-object key confirmation returns a safe connection error",fixture.Paths.Count==1&&error.Contains("did not confirm"));
        }
        var cases=new[]{
            new ErrorCase("credential",401,"application/json","{\"error\":{\"code\":\"invalid_api_key\",\"message\":\"fixture-private-key-do-not-display\"}}","rejected the credential"),
            new ErrorCase("credit",402,"application/json","{\"error\":{\"code\":402,\"message\":\"Insufficient credits\"}}","credit or quota"),
            new ErrorCase("guardrail budget",403,"application/json","{\"error\":{\"message\":\"private prompt must never appear\",\"metadata\":{\"limit_source\":\"openrouter_guardrail_budget\",\"scope\":\"private-workspace-id\"}}}","spending limit"),
            new ErrorCase("region",403,"application/json","{\"error\":{\"type\":\"permission_error\",\"message\":\"Country is not supported\"}}","restricted for this region"),
            new ErrorCase("permission",403,"application/json","{\"error\":{\"type\":\"permission_error\",\"message\":\"private prompt must never appear\"}}","permissions, account policy"),
            new ErrorCase("gateway",403,"application/json","{\"message\":\"Network policy blocked fixture-private-key-do-not-display\"}","network access policy"),
            new ErrorCase("security-policy gateway",403,"application/json","{\"success\":false,\"error\":\"Access denied by security policy.\"}","save your work and restart Rhino"),
            new ErrorCase("HTML interception",403,"text/html","<html>Private gateway fixture-private-key-do-not-display</html>","HTML page"),
            new ErrorCase("rate limit",429,"application/json","{\"error\":{\"type\":\"rate_limit_error\"}}","rate limit"),
            new ErrorCase("routing",404,"application/json","{\"error\":{\"message\":\"No endpoints found matching data policy\"}}","routing or data-policy"),
            new ErrorCase("required feature",400,"application/json","{\"error\":{\"message\":\"response_format unsupported\"}}","required feature"),
            new ErrorCase("server",502,"application/json","{\"error\":{\"message\":\"private prompt must never appear\"}}","temporarily unavailable"),
            new ErrorCase("regional server availability",502,"application/json","{\"error\":{\"message\":\"Regional server is temporarily down\"}}","temporarily unavailable"),
            new ErrorCase("late API error",200,"application/json","{\"error\":{\"code\":429,\"message\":\"Rate limit\"}}","rate limit"),
            new ErrorCase("oversized body",403,"text/plain",new string('x',100_000)+secret,"Access was denied")
        };
        foreach(var sample in cases)
        {
            using var fixture=new ErrorFixture(sample);using var client=new ProviderClient("https://example.com/v1/",secret,false,fixture);
            var error=await Failure(()=>client.Models(default));
            Check("API diagnostic classifies "+sample.Name+" without leaking data",error.Contains(sample.Expected,StringComparison.OrdinalIgnoreCase)&&error.Contains("HTTP "+sample.Code)&&!error.Contains(secret)&&!error.Contains("private prompt")&&!error.Contains("private-workspace-id")&&error.Length<800);
        }
        foreach(var source in new[]{"OpenAI","Anthropic / Claude","Google Gemini","xAI / Grok"})
        {
            using var fixture=new ErrorFixture(cases[0]);using var client=ProviderProfiles.Create(source,ProviderProfiles.Get(source).Endpoint,secret,fixture);
            var error=await Failure(()=>client.Models(default));
            Check(source+" uses shared credential-safe diagnostics",error.Contains("rejected the credential")&&!error.Contains(secret));
        }
        return passed;
    }
    private static async Task<string> Failure(Func<Task<ModelCatalog>> operation)
    {
        try{await operation();}catch(InvalidOperationException error){return error.Message;}
        throw new Exception("Expected a safe provider connection error.");
    }
    private sealed record ErrorCase(string Name,int Code,string ContentType,string Body,string Expected);
    private sealed class ErrorFixture(ErrorCase sample):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>Task.FromResult(new HttpResponseMessage((HttpStatusCode)sample.Code){Content=new StringContent(sample.Body,Encoding.UTF8,sample.ContentType)});
    }
    private sealed class Fixture:HttpMessageHandler
    {
        public HttpStatusCode KeyStatus=HttpStatusCode.OK;public string KeyBody="{\"data\":{\"is_management_key\":false}}";
        public List<string> Paths=[];public List<string> Authorization=[];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            Paths.Add(request.RequestUri!.AbsolutePath);Authorization.Add(request.Headers.Authorization?.ToString()??"");
            var key=request.RequestUri.AbsolutePath.EndsWith("/key",StringComparison.Ordinal);
            return Task.FromResult(new HttpResponseMessage(key?KeyStatus:HttpStatusCode.OK){Content=new StringContent(key?KeyBody:"{\"data\":[{\"id\":\"vendor/schema-only\",\"name\":\"Schema-only model\",\"supported_parameters\":[\"structured_outputs\"],\"architecture\":{\"output_modalities\":[\"text\"]}},{\"id\":\"vendor/tools-only\",\"supported_parameters\":[\"tools\"]}]}",Encoding.UTF8,"application/json")});
        }
    }
}
