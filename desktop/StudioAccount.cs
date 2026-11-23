using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Studio;

internal sealed record SiteClient(string Url, string Key);
internal sealed record SiteSession(string Access, string Refresh, long Expires);
internal sealed record StudioMember(Guid Id, string Email, string Access);
internal static class StudioAccount
{
    static readonly HttpClient Http = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(15) };
    internal static readonly SiteClient Site = JsonSerializer.Deserialize<SiteClient>(Asset("site.json"))!;
    static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AIArchiTec","Studio");
    static readonly string FileName = Path.Combine(Folder,"account.bin");
    static readonly string DeviceFile=Path.Combine(Folder,"device.bin");
    internal static bool HasSession => File.Exists(DeviceFile) || File.Exists(FileName);
    internal static string Asset(string name)
    {
        var assembly = typeof(StudioAccount).Assembly;
        using var stream = assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(r => r.EndsWith(".Assets."+name,StringComparison.Ordinal)))!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    static async Task<FileStream> Lock()
    {
        Directory.CreateDirectory(Folder);
        for(int i=0;;i++)
        {
            try { return new FileStream(Path.Combine(Folder,"account.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None); }
            catch(IOException) when(i<120){ await Task.Delay(200); }
        }
    }

    static SiteSession Load() => JsonSerializer.Deserialize<SiteSession>(
        ProtectedData.Unprotect(File.ReadAllBytes(FileName),null,DataProtectionScope.CurrentUser))!;

    static void Save(SiteSession value)
    {
        var bytes=ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(value),null,DataProtectionScope.CurrentUser);
        File.WriteAllBytes(FileName+".new",bytes);
        File.Move(FileName+".new",FileName,true);
    }

    static async Task<JsonNode> Api(string path,string? bearer=null,object? post=null)
    {
        using var request = new HttpRequestMessage(post==null?HttpMethod.Get:HttpMethod.Post,Site.Url.TrimEnd('/')+path);
        request.Headers.Add("apikey",Site.Key);
        if(bearer!=null)request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",bearer);
        if(post!=null)request.Content=JsonContent.Create(post);
        using var response=await Http.SendAsync(request);
        if(!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Account service returned {(int)response.StatusCode}.",null,response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonNode>() ?? throw new IOException("Empty account service response.");
    }

    internal static async Task<StudioMember> Authorize(string token)
    {
        var user=await Api("/auth/v1/user",token);
        var id=Guid.Parse((string)user["id"]!);
        var email=(string?)user["email"]??"Studio member";
        var members=(await Api("/rest/v1/workspace_members?select=workspace_id&status=eq.active&user_id=eq."+id,token)).AsArray();
        foreach(var row in members)
        {
            var workspace=Guid.Parse((string)row!["workspace_id"]!);
            if((bool)await Api("/rest/v1/rpc/workspace_entitlement",token,new {_workspace=workspace,_module="chat-agent"}))
                return new(id,email,"Licensed workspace");
        }
        if((bool)await Api("/rest/v1/rpc/has_legacy_ai_access",token,new {}))
            return new(id,email,"Personal workspace");
        throw new UnauthorizedAccessException("Your account needs an active workspace with AI chat access. Check your workspace with the administrator.");
    }

    static SiteSession Tokens(JsonNode response)=>new((string)response["access_token"]!,(string)response["refresh_token"]!,
        DateTimeOffset.UtcNow.ToUnixTimeSeconds()+(int)response["expires_in"]!);

    static string LoadDevice()=>Convert.ToHexString(ProtectedData.Unprotect(File.ReadAllBytes(DeviceFile),null,DataProtectionScope.CurrentUser)).ToLowerInvariant();

    internal static async Task RememberDevice(string secret)
    {
        using var gate=await Lock();
        File.WriteAllBytes(DeviceFile+".new",ProtectedData.Protect(Convert.FromHexString(secret),null,DataProtectionScope.CurrentUser));
        File.Move(DeviceFile+".new",DeviceFile,true);
        File.Delete(FileName);
    }

    static async Task<HttpResponseMessage> DeviceCall(string method,string secret,CancellationToken ct)
    {
        using var request=new HttpRequestMessage(HttpMethod.Post,Site.Url+"/rest/v1/rpc/studio_device_"+method);
        request.Headers.Add("apikey",Site.Key);
        request.Content=JsonContent.Create(new {_secret=secret});
        var response=await Http.SendAsync(request,ct);
        if(response.IsSuccessStatusCode)return response;
        var status=response.StatusCode;
        response.Dispose();
        throw new HttpRequestException(status==HttpStatusCode.NotFound?"Website device sign-in is not published yet.":$"Device verification returned {(int)status}.",null,status);
    }

    internal static async Task<StudioMember?> CheckDevice(string secret,CancellationToken ct)
    {
        using var response=await DeviceCall("status",secret,ct);
        var value=await response.Content.ReadAsStringAsync(ct);
        if(string.IsNullOrWhiteSpace(value) || JsonNode.Parse(value) is not JsonObject data)return null;
        if((bool?)data["denied"]==true)throw new UnauthorizedAccessException("The website has removed this account’s workspace access.");
        return new(Guid.Parse((string)data["id"]!),(string)data["email"]!,(string)data["access"]!);
    }

    internal static async Task<StudioMember> Verify()
    {
        using var gate=await Lock();
        if(File.Exists(DeviceFile))return await CheckDevice(LoadDevice(),CancellationToken.None)??throw new UnauthorizedAccessException("This device session has expired. Sign in again on ai-arch.ir.");
        if(!HasSession)throw new UnauthorizedAccessException("Sign in to AI Archi-Tec Studio first.");
        var session=Load();
        if(session.Expires<DateTimeOffset.UtcNow.ToUnixTimeSeconds()+90)
        {
            session=Tokens(await Api("/auth/v1/token?grant_type=refresh_token",post:new {refresh_token=session.Refresh}));
            Save(session);
        }
        return await Authorize(session.Access);
    }

    internal static async Task SignOut()
    {
        using var gate=await Lock();
        if(File.Exists(DeviceFile)){
            using var reply=await DeviceCall("disconnect",LoadDevice(),CancellationToken.None);
            File.Delete(DeviceFile);
        }
        if(!File.Exists(FileName))return;
        var session=Load();
        File.Delete(FileName);
        using var request=new HttpRequestMessage(HttpMethod.Post,Site.Url+"/auth/v1/logout?scope=local");
        request.Headers.Add("apikey",Site.Key);
        request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",session.Access);
        try { using var response=await Http.SendAsync(request); } catch(HttpRequestException){} catch(TaskCanceledException){}
    }
}
