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
    private static readonly string PreferencesPath=Path.Combine(Folder,"preferences.json");
    private static readonly object Gate=new();
    private static readonly Dictionary<string,Dictionary<string,(ConnectionSettings Plain,ConnectionSettings Stored)>> SessionProfiles=new(StringComparer.OrdinalIgnoreCase);
    private static ConnectionSettings Decode(ConnectionSettings value)=>string.IsNullOrEmpty(value.ApiKey)?value:value with{ApiKey=Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(value.ApiKey),null,DataProtectionScope.CurrentUser))};
    private static ConnectionSettings Encode(ConnectionSettings value)=>value with{ApiKey=value.RememberKey&&value.ApiKey.Length>0?Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value.ApiKey),null,DataProtectionScope.CurrentUser)):""};
    public static ConnectionSettings Load()=>LoadFrom(Folder);
    public static ConnectionSettings LoadFrom(string storageDirectory)
    {
        lock(Gate)
        {
            var path=Path.Combine(storageDirectory,"connection.json");
            if(!File.Exists(path))return new();
            var active=Wire.Read<ConnectionSettings>(File.ReadAllText(path));
            var profiles=LoadProfilesFrom(storageDirectory);
            return profiles.TryGetValue(active.Source,out var current)?current:Decode(active);
        }
    }
    public static Dictionary<string,ConnectionSettings> LoadProfiles()=>LoadProfilesFrom(Folder);
    public static Dictionary<string,ConnectionSettings> LoadProfilesFrom(string storageDirectory)
    {
        lock(Gate)
        {
            var folder=Path.GetFullPath(storageDirectory);var profilesPath=Path.Combine(folder,"profiles.json");
            var encoded=File.Exists(profilesPath)?Wire.Read<Dictionary<string,ConnectionSettings>>(File.ReadAllText(profilesPath)):new();
            var activePath=Path.Combine(folder,"connection.json");
            if(File.Exists(activePath)){var legacy=Wire.Read<ConnectionSettings>(File.ReadAllText(activePath));encoded.TryAdd(legacy.Source,legacy);}
            var cache=Cache(folder);var result=new Dictionary<string,ConnectionSettings>();
            foreach(var pair in encoded)
            {
                if(cache.TryGetValue(pair.Key,out var entry)&&entry.Stored==pair.Value){result[pair.Key]=entry.Plain;continue;}
                // A damaged or another account's DPAPI blob must not hide every other provider.
                ConnectionSettings plain;
                try{plain=Decode(pair.Value);}catch(Exception ex)when(ex is CryptographicException or FormatException){plain=pair.Value with{ApiKey=""};}
                result[pair.Key]=plain;cache[pair.Key]=(plain,pair.Value);
            }
            return result;
        }
    }
    public static void Save(ConnectionSettings settings)=>SaveProfile(settings);
    public static void SaveProfile(ConnectionSettings settings,string? storageDirectory=null)
    {
        lock(Gate)
        {
            var folder=Path.GetFullPath(storageDirectory??Folder);var profilesPath=Path.Combine(folder,"profiles.json");
            Directory.CreateDirectory(folder);
            var profiles=File.Exists(profilesPath)?Wire.Read<Dictionary<string,ConnectionSettings>>(File.ReadAllText(profilesPath)):new();
            var encoded=Encode(settings);profiles[settings.Source]=encoded;
            AtomicWrite(profilesPath,JsonSerializer.Serialize(profiles,Wire.Json));
            // Saving explicitly selects this profile. Failure to discover models never erases it.
            AtomicWrite(Path.Combine(folder,"connection.json"),JsonSerializer.Serialize(encoded,Wire.Json));
            Cache(folder)[settings.Source]=(settings,encoded);
        }
    }
    private static Dictionary<string,(ConnectionSettings Plain,ConnectionSettings Stored)> Cache(string folder)
    {if(!SessionProfiles.TryGetValue(folder,out var cache))SessionProfiles[folder]=cache=new();return cache;}
    public static void ForgetSessionCredentials(string storageDirectory){lock(Gate)SessionProfiles.Remove(Path.GetFullPath(storageDirectory));}
    private static void AtomicWrite(string path,string contents)
    {
        var temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{File.WriteAllText(temporary,contents);File.Move(temporary,path,true);}
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    public static StudioPreferences LoadPreferences()=>File.Exists(PreferencesPath)?Wire.Read<StudioPreferences>(File.ReadAllText(PreferencesPath)):new();
    public static void SavePreferences(StudioPreferences preferences)
    {
        if(preferences.McpPort is <1024 or >65535||preferences.DefaultWorkspace is not ("generate" or "analyze" or "assist"))throw new ArgumentException("Choose a valid workspace and localhost port (1024–65535).");
        Directory.CreateDirectory(Folder);AtomicWrite(PreferencesPath,JsonSerializer.Serialize(preferences,Wire.Json));
    }
}
