using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArchiTec.Core;

namespace ArchiTec.RhinoPlugin;

public sealed record ConnectionSettings(string Source="OpenRouter",string Endpoint="https://openrouter.ai/api/v1/",string ApiKey="",bool RememberKey=false,string Model="");
public static class SettingsStore
{
    private static readonly string Folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AIArchiTec","Rhino8");
    private static readonly string SettingsPath=Path.Combine(Folder,"connection.json");
    public static ConnectionSettings Load()
    {
        if(!File.Exists(SettingsPath))return new ConnectionSettings();
        var saved=Wire.Read<ConnectionSettings>(File.ReadAllText(SettingsPath));
        if(!string.IsNullOrEmpty(saved.ApiKey))
            saved=saved with {ApiKey=Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(saved.ApiKey),null,DataProtectionScope.CurrentUser))};
        return saved;
    }
    public static void Save(ConnectionSettings settings)
    {
        var protectedKey=settings.RememberKey && settings.ApiKey.Length>0
            ? Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(settings.ApiKey),null,DataProtectionScope.CurrentUser)) : "";
        Directory.CreateDirectory(Folder);
        File.WriteAllText(SettingsPath,JsonSerializer.Serialize(settings with {ApiKey=protectedKey},Wire.Json));
    }
}
