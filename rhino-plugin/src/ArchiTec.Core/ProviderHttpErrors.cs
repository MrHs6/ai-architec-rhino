using System.Text.Json;

namespace ArchiTec.Core;

/// <summary>Bounded error classification. Never returns provider text, identifiers, HTML or request data.</summary>
internal static class ProviderHttpErrors
{
    public static string? Describe(HttpResponseMessage response,string body)
    {
        var httpCode=(int)response.StatusCode;
        var html=response.Content.Headers.ContentType?.MediaType?.Contains("html",StringComparison.OrdinalIgnoreCase)==true||body.AsSpan().TrimStart().StartsWith("<",StringComparison.Ordinal);
        if(html)return $"The API returned an HTML page (HTTP {httpCode}), not an API response. A gateway, proxy or access page may be intercepting this connection. Test the selected API host from Rhino; this response does not validate the key. No model fallback was used.";
        var code=httpCode;var jsonError=false;var hints=new List<string>();
        try
        {
            using var json=JsonDocument.Parse(body,new JsonDocumentOptions{MaxDepth=16});
            var root=json.RootElement;
            if(root.ValueKind==JsonValueKind.Object&&root.TryGetProperty("error",out var error)&&error.ValueKind is not (JsonValueKind.Null or JsonValueKind.False))
            {
                jsonError=true;
                if(error.ValueKind==JsonValueKind.String){var text=error.GetString()??"";if(text.Length<=2000)hints.Add(text);}
                else if(error.ValueKind==JsonValueKind.Object)
                {
                    Collect(error,hints);
                    if(error.TryGetProperty("code",out var errorCode)&&errorCode.ValueKind==JsonValueKind.Number&&errorCode.TryGetInt32(out var parsed)&&parsed is >=400 and <=599)code=parsed;
                    if(error.TryGetProperty("metadata",out var metadata)&&metadata.ValueKind==JsonValueKind.Object)Collect(metadata,hints);
                }
            }
            // Some API gateways return a top-level JSON message, without the usual error envelope.
            if(!response.IsSuccessStatusCode&&root.ValueKind==JsonValueKind.Object)Collect(root,hints);
        }
        catch(JsonException){}
        if(response.IsSuccessStatusCode&&!jsonError)return null;
        var hint=string.Join(" ",hints).ToLowerInvariant();
        bool Has(params string[] words)=>words.Any(hint.Contains);
        string explanation;
        if(Has("openrouter_guardrail_budget","openrouter_workspace_budget","openrouter_key_limit","budget limit","spend limit"))
            explanation="The provider blocked a key, workspace or guardrail spending limit. Check the account budget and its reset window; entering the same key again will not remove this limit.";
        else if(code==401||Has("invalid_api_key","authentication_error","invalid credentials","invalid api key","invalid key","unauthenticated","authentication failed"))
            explanation="The API rejected the credential. Check the selected service, key status and account permissions, then reconnect with a valid key.";
        else if(code==402||Has("insufficient_quota","insufficient credits","insufficient credit","credit balance","billing limit","billing_error","quota exceeded"))
            explanation="Provider credit or quota is exhausted. Check the API account balance, key limit and billing; a chat subscription does not automatically include API credit.";
        else if(Has("unsupported_country","unsupported_region","country is not","region is not","country not supported","region not supported","unsupported country","unsupported region","restricted country","restricted region","not available in your country","not available in your region","geographic restriction","location not supported","territory"))
            explanation="API access is restricted for this region or account location. Check the provider's availability and account policy. The plug-in cannot remove this restriction.";
        else if(code==429||Has("rate_limit","rate limit","too many requests","resource_exhausted"))
            explanation="The API rate limit was reached. Wait for the provider limit to reset or check this model's account quota before retrying.";
        else if(Has("proxy","firewall","cloudflare","gateway access","network policy","security policy"))
            explanation="A network access policy or gateway blocked the API request. This does not establish whether the key is valid. If network or proxy settings changed while Rhino was open, save your work and restart Rhino, then reconnect: its .NET 8 runtime can retain earlier proxy settings.";
        else if(Has("no endpoints","no available provider","routing requirements","privacy policy","data policy","no_allowed_provider"))
            explanation="No model endpoint meets the account's routing or data-policy settings. Review those settings or explicitly choose another compatible model. The selected model was not changed.";
        else if(code==403||Has("permission_error","permission_denied","insufficient permission","forbidden","guardrail","moderation","content_filter"))
            explanation="Access was denied by API permissions, account policy or a request guardrail. Check key permissions and provider account settings. HTTP 403 alone does not prove the key is invalid.";
        else if(code==404)
            explanation="The API path or selected model is unavailable. Check the service endpoint and reload its model catalog.";
        else if(code is 400 or 422)
            explanation="The API rejected the request format or a required feature. Use a text model that supports JSON schema structured output; check the chosen provider's compatibility.";
        else if(code is 408 or 504)
            explanation="The API timed out. Inspect the current result before explicitly retrying; this request was not automatically replayed.";
        else if(code>=500)
            explanation="The API or selected model endpoint is temporarily unavailable. Retry later; no alternate model was selected.";
        else explanation="The API request failed. Check the selected endpoint and provider account access, then retry.";
        var format=jsonError?"JSON API error":response.Content.Headers.ContentType?.MediaType?.Contains("json",StringComparison.OrdinalIgnoreCase)==true?"JSON response":"non-JSON response";
        return $"{explanation} (HTTP {httpCode}; {format}). No model fallback was used.";
    }
    private static void Collect(JsonElement value,List<string> hints)
    {
        foreach(var field in new[]{"message","type","code","status","error_type","provider_code","limit_source"})
            if(value.TryGetProperty(field,out var item)&&item.ValueKind==JsonValueKind.String)
            {
                var text=item.GetString()??"";
                if(text.Length<=2000)hints.Add(text);
            }
    }
}
