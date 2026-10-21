using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArchiTec.Core;

namespace ArchiTec.RhinoPlugin;

public sealed record ConnectionSettings(string Source="OpenRouter",string Endpoint="https://openrouter.ai/api/v1/",string ApiKey="",bool RememberKey=false,string Model="");
public sealed record StudioPreferences(string DefaultWorkspace="generate",bool ZoomPreview=false,bool Compact=false,int McpPort=5188);
public static class SettingsStore
{
    private static readonly string Folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AIArchiTec","Rhino8");
    private static readonly string SettingsPath=Path.Combine(Folder,"connection.json");
    private static readonly string ProfilesPath=Path.Combine(Folder,"profiles.json");
    private static readonly string PreferencesPath=Path.Combine(Folder,"preferences.json");
    private static ConnectionSettings Decode(ConnectionSettings value)=>string.IsNullOrEmpty(value.ApiKey)?value:value with{ApiKey=Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(value.ApiKey),null,DataProtectionScope.CurrentUser))};
    private static ConnectionSettings Encode(ConnectionSettings value)=>value with{ApiKey=value.RememberKey&&value.ApiKey.Length>0?Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value.ApiKey),null,DataProtectionScope.CurrentUser)):""};
    public static ConnectionSettings Load()=>File.Exists(SettingsPath)?Decode(Wire.Read<ConnectionSettings>(File.ReadAllText(SettingsPath))):new();
    public static Dictionary<string,ConnectionSettings> LoadProfiles()
    {
        var result=File.Exists(ProfilesPath)?Wire.Read<Dictionary<string,ConnectionSettings>>(File.ReadAllText(ProfilesPath)).ToDictionary(p=>p.Key,p=>Decode(p.Value)):new();
        var current=Load();if(!result.ContainsKey(current.Source))result[current.Source]=current;
        return result;
    }
    public static void Save(ConnectionSettings settings)
    {
        SaveProfile(settings);
        File.WriteAllText(SettingsPath,JsonSerializer.Serialize(Encode(settings),Wire.Json));
    }
    public static void SaveProfile(ConnectionSettings settings,string? storageDirectory=null)
    {
        var folder=storageDirectory??Folder;var profilesPath=Path.Combine(folder,"profiles.json");var activePath=Path.Combine(folder,"connection.json");
        Directory.CreateDirectory(folder);
        var profiles=File.Exists(profilesPath)?Wire.Read<Dictionary<string,ConnectionSettings>>(File.ReadAllText(profilesPath)):new();
        profiles[settings.Source]=Encode(settings);
        File.WriteAllText(profilesPath,JsonSerializer.Serialize(profiles,Wire.Json));
        // A Remember-off change must also remove the legacy active-profile copy.
        if(File.Exists(activePath)&&Wire.Read<ConnectionSettings>(File.ReadAllText(activePath)).Source==settings.Source)
            File.WriteAllText(activePath,JsonSerializer.Serialize(Encode(settings),Wire.Json));
    }
    public static StudioPreferences LoadPreferences()=>File.Exists(PreferencesPath)?Wire.Read<StudioPreferences>(File.ReadAllText(PreferencesPath)):new();
    public static void SavePreferences(StudioPreferences preferences)
    {
        if(preferences.McpPort is <1024 or >65535||preferences.DefaultWorkspace is not ("generate" or "analyze" or "assist"))throw new ArgumentException("Choose a valid workspace and localhost port (1024–65535).");
        Directory.CreateDirectory(Folder);File.WriteAllText(PreferencesPath,JsonSerializer.Serialize(preferences,Wire.Json));
    }
}
