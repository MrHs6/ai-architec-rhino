using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;

namespace ArchiTec.Core;

public static class ConnectionDiagnostics
{
    public static string Describe(Exception error)
    {
        for(Exception? e=error;e is not null;e=e.InnerException)
        {
            if(e is SocketException socket)return socket.SocketErrorCode switch
            {
                SocketError.AccessDenied=>"Windows blocked this process's outbound socket (WSAEACCES / 10013). This is a process, firewall or network-policy restriction; the API key was not validated. Reopen Rhino normally from Windows and use Test network. Do not disable security protections.",
                SocketError.HostNotFound or SocketError.TryAgain=>"DNS could not resolve the API host. Check your network connection and DNS, then retry.",
                SocketError.ConnectionRefused=>"The API host refused the connection. For a local endpoint, check that your local AI server is running on the configured port.",
                _=>"The network connection failed ("+socket.SocketErrorCode+"). Check connectivity and your configured endpoint, then retry."
            };
            if(e is AuthenticationException)return "TLS certificate validation failed. Check Windows time and the endpoint certificate; certificate validation remains enabled.";
        }
        if(error is OperationCanceledException)return "The connection timed out or was cancelled. Your saved profile is retained; retry when the service is reachable.";
        if(error is HttpRequestException)return "The API host could not be reached. Your saved profile is retained. Check the endpoint and Test network before retrying.";
        // Provider HTTP errors are already bounded and do not include response bodies or credentials.
        return error is ArgumentException or InvalidOperationException?error.Message:"Connection could not be completed. Your saved profile is retained.";
    }
    public static async Task<string> Probe(string source,string endpoint,CancellationToken cancel,HttpMessageHandler? handler=null)
    {
        var preset=ProviderProfiles.Get(source);
        if(!Uri.TryCreate(endpoint.TrimEnd('/')+"/",UriKind.Absolute,out var uri)||uri.UserInfo.Length>0||uri.Query.Length>0||uri.Fragment.Length>0||!(uri.Scheme=="https"||uri.Scheme=="http"&&uri.IsLoopback))throw new ArgumentException("Use an HTTPS API base URL or localhost HTTP without credentials.");
        if(source!="OpenAI-compatible / local"&&uri!=new Uri(preset.Endpoint))throw new ArgumentException("Use the selected provider's official endpoint.");
        using var http=new HttpClient(handler??new HttpClientHandler{AllowAutoRedirect=false}){BaseAddress=uri,Timeout=TimeSpan.FromSeconds(12)};
        using var response=await http.GetAsync(source=="xAI / Grok"?"language-models":"models",HttpCompletionOption.ResponseHeadersRead,cancel);
        var code=(int)response.StatusCode;
        return response.IsSuccessStatusCode||response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            ?$"Network reached {uri.Host} (HTTP {code}). No API key or model data was sent. This checks connectivity, not key validity or account access."
            :$"Host responded with HTTP {code}. No API key was sent. Check the API base path; model compatibility is checked when connecting.";
    }
}
