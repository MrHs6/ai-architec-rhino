using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Studio.Acceptance")]

namespace Studio;

internal sealed class Workspace : IDisposable
{
    private readonly Connections connections;
    private readonly HttpClient http;
    internal Workspace(HttpMessageHandler? transport = null, Connections? hosts = null)
    {
        connections=hosts??new Connections();
        http = new HttpClient(transport ?? new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(2) };
    }
    public const string SystemPrompt = "You are AI Archi-Tec, an architectural modeling assistant. Respond in the user's language. Use get_context before modeling and read capabilities when available. Follow the user's chosen host and existing object IDs. Read actual receipts before claiming success. Never retry an uncertain write. For transfers use real inspected geometry, verify units and explain what was transferred. Images are design references; ask for scale if necessary.";
    public async Task<JsonArray> Tools(string target)
    {
        var result = new JsonArray();
        foreach (var host in target == "Both" ? new[] { "Rhino", "Revit" } : new[] { target })
        {
            foreach (var source in await connections.Tools(host))
            {
                var tool = source!.DeepClone();
                if (host is "Rhino" or "Revit") tool["name"] = host.ToLowerInvariant() + "_" + (string?)tool["name"];
                result.Add(tool);
            }
        }
        if (target == "Both")
            result.Add(Wire.Node(new { name = "transfer_wall_axis", description = "Copy one straight Revit wall location line into Rhino as an editable curve in matching internal/world coordinates. This transfers the axis only, without BIM properties. Read both document IDs first. Retry with the same UUID.",
                inputSchema = new { type="object", properties=new { revit_document_id=new {type="string"}, rhino_document_id=new {type="integer"}, wall_id=new {type="string"}, request_id=new {type="string"} }, required=new[]{"revit_document_id","rhino_document_id","wall_id","request_id"}, additionalProperties=false } }));
        return result;
    }

    public Task<JsonNode> Call(string target, string name, JsonNode args)
    {
        if (target=="Both" && name=="transfer_wall_axis") return TransferAxis(args);
        if (target is not ("Rhino" or "Revit" or "Both")) return connections.Call(target,name,args);
        var prefix = name.StartsWith("rhino_", StringComparison.Ordinal) ? "Rhino" : name.StartsWith("revit_", StringComparison.Ordinal) ? "Revit" : throw new ArgumentException("Unknown host.");
        if (target != "Both" && prefix != target) throw new ArgumentException("Tool outside selected host.");
        return connections.Call(prefix, name[(prefix.Length+1)..], args);
    }

    private async Task<JsonNode> TransferAxis(JsonNode args)
    {
        var request = Guid.Parse((string)args["request_id"]!);
        var wall = await connections.Call("Revit","inspect_wall", Wire.Node(new { document_id=(string?)args["revit_document_id"],wall_id=(string?)args["wall_id"] }));
        if((bool?)wall["isError"]==true) return wall;
        var source = JsonNode.Parse(Wire.Describe(wall))!;
        if((bool?)source["straight"]!=true) return Wire.Text("Choose a straight wall for axis transfer.",true);
        var plan=new { version=1, summary="Transfer Revit wall axis",length_unit="meters",
            actions=new[]{new { tool="create_polyline",parameters=new {points=new[]{source["start_m"],source["end_m"]},closed=false,layer="Revit_Axes",name="Wall "+(string?)source["wall_id"]}}}};
        return await connections.Call("Rhino","execute_plan",Wire.Node(new {document_id=(int)args["rhino_document_id"]!,request_id=request,plan}));
    }

    public async Task<string> Chat(string target, string model, string key, JsonArray history, bool build, Action<string> log, Action<byte[]> preview, Action<string,string>? progress=null, CancellationToken cancellation=default,Action<string>? partial=null, bool probe=false)
    {
        const string url="https://openrouter.ai/api/v1/chat/completions";
        var tools = new JsonArray();
        if (build){
            progress?.Invoke("Discovering tools · "+target,"running");
            foreach (var t in await Tools(target))
                tools.Add(Wire.Node(new { type = "function", function = new { name = (string?)t!["name"], description = (string?)t["description"], parameters = t["inputSchema"] } }));
            progress?.Invoke($"{tools.Count} tools available · {target}","done");
        }
        for (int step = 0; step < 10; step++)
        {
            cancellation.ThrowIfCancellationRequested();
            JsonNode answer;
            {
                var payload = new JsonObject { ["model"] = model, ["messages"] = history.DeepClone() };
                if (probe) payload["max_tokens"] = probe ? 64 : 4096;
                if (build) { payload["tools"] = tools.DeepClone(); payload["provider"]=Wire.Node(new {require_parameters=true}); }
                bool stream=!build && partial!=null;
                if(stream)payload["stream"]=true;
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                if(!string.IsNullOrWhiteSpace(key))request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
                request.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
                progress?.Invoke("Waiting for "+model,"running");
                using var response = await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancellation);
                if (!response.IsSuccessStatusCode) throw new IOException(ProviderError("OpenRouter",(int)response.StatusCode));
                if(stream && response.Content.Headers.ContentType?.MediaType=="text/event-stream"){
                    using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                    timeout.CancelAfter(TimeSpan.FromMinutes(3));
                    using var reader=new StreamReader(await response.Content.ReadAsStreamAsync(timeout.Token));
                    var text=new StringBuilder();bool finished=false;
                    while(await reader.ReadLineAsync(timeout.Token) is string line){
                        if(!line.StartsWith("data:",StringComparison.Ordinal))continue;
                        var data=line[5..].Trim();if(data=="[DONE]"){finished=true;break;}
                        var chunk=JsonNode.Parse(data);
                        if(chunk?["error"]!=null)throw new IOException("The provider interrupted the stream.");
                        if(chunk?["choices"] is not JsonArray choices || choices.Count==0)continue;
                        if(choices[0]?["delta"]?["content"] is JsonValue token){string value=token.ToString();text.Append(value);partial!(value);}
                        if(choices[0]?["finish_reason"]!=null)finished=true;
                    }
                    if(!finished || text.Length==0)throw new IOException("The response stream ended before completion.");
                    history.Add(Wire.Node(new{role="assistant",content=text.ToString()}));progress?.Invoke("Response received · "+model,"done");return text.ToString();
                }
                answer = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellation))?["choices"]?[0]?["message"] ?? throw new IOException("Missing provider message.");
            }
            progress?.Invoke("Response received · "+model,"done");
            history.Add(answer.DeepClone());
            if (answer["tool_calls"] is not JsonArray calls || calls.Count == 0) return answer["content"]?.ToString() ?? "";
            foreach (var c in calls)
            {
                string name = (string)c!["function"]!["name"]!;
                string report;
                if (!build || !tools.Any(t=>(string?)t!["function"]!["name"]==name)) throw new IOException("Tool outside the selected workspace.");
                string action=name.Replace('_',' ');
                progress?.Invoke(action,"running");
                bool success=false;
                try
                {
                    var args = JsonNode.Parse((string)c["function"]!["arguments"]!)!;
                    if(args?["plan"]?["actions"] is JsonArray batch){
                        action=string.Join(" · ",batch.Take(6).Select(a=>(string?)a?["tool"]??"operation"));progress?.Invoke(action,"update");
                    }
                    if(cancellation.IsCancellationRequested){report="Cancelled before dispatch.";history.Add(Wire.Node(new{role="tool",tool_call_id=(string?)c["id"],content=report}));progress?.Invoke(report,"failed");continue;}
                    var result = await Call(target, name, args!);
                    report = Wire.Describe(result);
                    foreach (var item in result["content"]!.AsArray())
                        if ((string?)item?["type"] == "image") { preview(Convert.FromBase64String((string)item!["data"]!)); report += " Viewport captured in Studio."; }
                    success=(bool?)result["isError"]!=true;
                    if ((bool?)result["isError"] == true) report = "Tool failed: " + report;
                }
                catch (Exception ex) { report = "Execution error. Check current state before retry. " + ex.Message; }
                progress?.Invoke(action,success?"done":"failed");
                log(name + "\n" + report);
                history.Add(Wire.Node(new { role = "tool", tool_call_id = (string?)c["id"], content = report }));
            }
        }
        return "Step limit reached. Review the tool receipts before continuing.";
    }

    internal static string ProviderError(string provider,int code) => $"{provider} · HTTP {code}. " + (code switch {
        400 or 422 => "Check the model ID and its support for the requested image or design tools.",
        401 => "The provider rejected the API key. Enter a valid key for this provider.",
        402 => "The provider requires more credit for this request.",
        403 => "The provider denied access. Check account, model permissions and network availability.",
        404 => "Check the endpoint and model ID.",
        429 => "The provider reported a rate or usage limit.",
        _ => "The provider could not complete this request."
    });

    public void ResetConnections() => connections.Dispose();
    public void Dispose() { connections.Dispose(); http.Dispose(); }
}
