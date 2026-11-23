using System.Security.Cryptography;
namespace Studio;

internal static class BrowserSignIn
{
    internal static (string Secret,string Fingerprint) CreateDevice()
    {
        var secret=RandomNumberGenerator.GetBytes(32);
        return (Convert.ToHexString(secret).ToLowerInvariant(),Convert.ToHexString(SHA256.HashData(secret)).ToLowerInvariant());
    }

    internal static string SiteUrl(string fingerprint)=>"https://ai-arch.ir/desktop/sign-in#device="+fingerprint;

    internal static async Task<StudioMember> Run(CancellationToken token,Action<string> launch,Action<string> showCode)
    {
        var device=CreateDevice();
        showCode(device.Fingerprint[..8].ToUpperInvariant());
        launch(SiteUrl(device.Fingerprint));
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));
        try
        {
            while(true)
            {
                await Task.Delay(TimeSpan.FromSeconds(3),timeout.Token);
                var member=await StudioAccount.CheckDevice(device.Secret,timeout.Token);
                if(member==null)continue;
                timeout.Token.ThrowIfCancellationRequested();
                await StudioAccount.RememberDevice(device.Secret);
                return member;
            }
        }
        catch(OperationCanceledException) when(!token.IsCancellationRequested){throw new TimeoutException();}
    }
}
