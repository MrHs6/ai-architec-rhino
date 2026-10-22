using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ArchiTec.Core;

// The client's stdio server survives a stopped/restarted Rhino listener. Credentials
// are rediscovered on this Windows account; a failed modeling request is not replayed.
internal sealed class ReconnectingBridge : IAsyncDisposable
{
    private readonly int port;
    private readonly HttpClient http = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(35) };
    private readonly SemaphoreSlim gate = new(1);
    private readonly CancellationTokenSource lifetime = new();
    private readonly Task heartbeat;
    private JsonElement initialization;
    private bool initialized;
    private string? endpoint, credential, session;
    private const string Offline = "Rhino MCP is off or reconnecting. In Rhino, open Archi-Tec > MCP connection and click Enable connection. Keep this client open; it reconnects automatically. Then retry get_context. Do not repeat a modeling request until you have checked its preview or get_plan_status.";

    public ReconnectingBridge(int port) { this.port = port; heartbeat = Heartbeat(); }

    public async Task<object?> Handle(JsonElement request)
    {
        var method = request.GetProperty("method").GetString() ?? "";
        if (!request.TryGetProperty("id", out var id)) return null;
        var args = request.TryGetProperty("params", out var value) ? value.Clone() : JsonSerializer.SerializeToElement(new { });
        await gate.WaitAsync();
        try
        {
            if (method == "initialize")
            {
                initialization = args; initialized = true; session = null;
                await TryConnect();
                return new { jsonrpc = "2.0", id, result = McpServer.InitializationResult(args) };
            }
            if (!initialized) return new { jsonrpc = "2.0", id, error = new { code = -32002, message = "Initialize this MCP client first." } };
            if (method == "tools/list")
            {
                await TryConnect();
                return new { jsonrpc = "2.0", id, result = new { tools = McpServer.ToolDefinitions } };
            }
            if (method == "ping") return new { jsonrpc = "2.0", id, result = new { } };
            try
            {
                await EnsureConnected(lifetime.Token);
                using var response = await Send(request.GetRawText(), lifetime.Token);
                // These statuses are rejected before tool dispatch. A connection loss,
                // timeout, 500, or tool error may have an outcome: never replay those.
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound)
                {
                    session = null;
                    await EnsureConnected(lifetime.Token);
                    using var retry = await Send(request.GetRawText(), lifetime.Token);
                    retry.EnsureSuccessStatusCode();
                    using var retried = JsonDocument.Parse(await retry.Content.ReadAsStringAsync());
                    return retried.RootElement.Clone();
                }
                response.EnsureSuccessStatusCode();
                using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                return body.RootElement.Clone();
            }
            catch
            {
                session = null;
                if (method == "tools/call") return new { jsonrpc = "2.0", id, result = new { content = new[] { new { type = "text", text = Offline } }, isError = true } };
                return new { jsonrpc = "2.0", id, error = new { code = -32001, message = Offline } };
            }
        }
        finally { gate.Release(); }
    }

    private async Task TryConnect()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        try { await EnsureConnected(timeout.Token); } catch { session = null; }
    }

    private async Task EnsureConnected(CancellationToken cancel)
    {
        var explicitToken = Environment.GetEnvironmentVariable("ARCHITEC_MCP_TOKEN");
        var local = string.IsNullOrWhiteSpace(explicitToken)
            ? McpSessionFile.Read(port)
            : (Endpoint: Environment.GetEnvironmentVariable("ARCHITEC_MCP_URL") ?? "", Token: explicitToken);
        if (!Uri.TryCreate(local.Endpoint, UriKind.Absolute, out var uri) || uri.Scheme != "http" || !uri.IsLoopback || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0 || uri.AbsolutePath != "/mcp")
            throw new InvalidOperationException("A loopback MCP endpoint is required.");
        if (endpoint == local.Endpoint && credential == local.Token && session is not null) return;
        endpoint = local.Endpoint; credential = local.Token; session = null;
        using var hello = await Send(JsonSerializer.Serialize(new { jsonrpc = "2.0", id = "bridge-initialize", method = "initialize", @params = initialization }), cancel);
        hello.EnsureSuccessStatusCode();
        using var result = JsonDocument.Parse(await hello.Content.ReadAsStringAsync(cancel));
        if (!result.RootElement.TryGetProperty("result", out _)) throw new InvalidOperationException("Rhino initialization failed.");
        session = hello.Headers.GetValues("Mcp-Session-Id").Single();
        using var ready = await Send("{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}", cancel);
        ready.EnsureSuccessStatusCode();
        using var tools = await Send("{\"jsonrpc\":\"2.0\",\"id\":\"bridge-tools\",\"method\":\"tools/list\"}", cancel);
        tools.EnsureSuccessStatusCode();
    }

    private async Task<HttpResponseMessage> Send(string body, CancellationToken cancel)
    {
        using var outgoing = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        outgoing.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential);
        outgoing.Headers.Accept.ParseAdd("application/json, text/event-stream");
        outgoing.Headers.Add("MCP-Protocol-Version", "2025-11-25");
        if (session is not null) outgoing.Headers.Add("Mcp-Session-Id", session);
        return await http.SendAsync(outgoing, cancel);
    }

    private async Task Heartbeat()
    {
        try
        {
            while (!lifetime.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(3), lifetime.Token);
                await gate.WaitAsync(lifetime.Token);
                try
                {
                    if (!initialized) continue;
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                    timeout.CancelAfter(TimeSpan.FromSeconds(2));
                    try
                    {
                        await EnsureConnected(timeout.Token);
                        using var response = await Send("{\"jsonrpc\":\"2.0\",\"id\":\"bridge-heartbeat\",\"method\":\"ping\"}", timeout.Token);
                        if (!response.IsSuccessStatusCode) session = null;
                    }
                    catch { session = null; }
                }
                finally { gate.Release(); }
            }
        }
        catch (OperationCanceledException) { }
    }

    public async ValueTask DisposeAsync()
    {
        lifetime.Cancel(); await heartbeat;
        if (session is not null)
            try
            {
                using var goodbye = new HttpRequestMessage(HttpMethod.Delete, endpoint);
                goodbye.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential);
                goodbye.Headers.Add("Mcp-Session-Id", session);
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                using var response = await http.SendAsync(goodbye, timeout.Token);
            }
            catch { }
        http.Dispose(); gate.Dispose(); lifetime.Dispose();
    }
}
