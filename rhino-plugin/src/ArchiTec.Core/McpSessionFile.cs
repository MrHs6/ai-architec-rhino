using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace ArchiTec.Core;

/// <summary>Windows CurrentUser DPAPI session discovery; exported client setup contains no token.</summary>
public static class McpSessionFile
{
    private sealed record Session(string Endpoint,string Credential,int ProcessId,string Owner);
    public static string PathFor(int port)=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AIArchiTec","Rhino8","mcp-session-"+port+".json");
    public static void Publish(int port,string endpoint,string token,string owner)
    {
        Validate(endpoint);var path=PathFor(port);Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var value=new Session(endpoint,Convert.ToBase64String(Crypt(Encoding.UTF8.GetBytes(token),true)),Environment.ProcessId,owner);
        var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{File.WriteAllText(temp,JsonSerializer.Serialize(value));File.Move(temp,path,true);}finally{if(File.Exists(temp))File.Delete(temp);}
    }
    public static (string Endpoint,string Token) Read(int port)
    {
        var value=JsonSerializer.Deserialize<Session>(File.ReadAllText(PathFor(port)))??throw new InvalidOperationException("Start MCP in Rhino first.");
        Validate(value.Endpoint);
        using var process=System.Diagnostics.Process.GetProcessById(value.ProcessId);
        if(process.HasExited)throw new InvalidOperationException("The Rhino session has ended. Start MCP again.");
        var bytes=Crypt(Convert.FromBase64String(value.Credential),false);
        try{return(value.Endpoint,Encoding.UTF8.GetString(bytes));}finally{System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes);}
    }
    public static void Remove(int port,string owner)
    {
        var path=PathFor(port);if(!File.Exists(path))return;
        var value=JsonSerializer.Deserialize<Session>(File.ReadAllText(path));if(value?.Owner==owner)File.Delete(path);
    }
    private static void Validate(string endpoint)
    {
        if(!Uri.TryCreate(endpoint,UriKind.Absolute,out var uri)||uri.Scheme!="http"||!uri.IsLoopback||uri.UserInfo.Length>0||uri.Query.Length>0||uri.Fragment.Length>0||uri.AbsolutePath!="/mcp")throw new ArgumentException("A loopback MCP endpoint is required.");
    }
    [StructLayout(LayoutKind.Sequential)]private struct Blob{public int Length;public IntPtr Data;}
    [DllImport("crypt32.dll",SetLastError=true,CharSet=CharSet.Unicode)]private static extern bool CryptProtectData(ref Blob input,string? description,IntPtr entropy,IntPtr reserved,IntPtr prompt,int flags,out Blob output);
    [DllImport("crypt32.dll",SetLastError=true,CharSet=CharSet.Unicode)]private static extern bool CryptUnprotectData(ref Blob input,IntPtr description,IntPtr entropy,IntPtr reserved,IntPtr prompt,int flags,out Blob output);
    [DllImport("kernel32.dll")]private static extern IntPtr LocalFree(IntPtr memory);
    private static byte[] Crypt(byte[] bytes,bool protect)
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("Local session discovery requires Windows.");
        var input=new Blob{Length=bytes.Length,Data=Marshal.AllocHGlobal(bytes.Length)};Blob output=default;
        try
        {
            Marshal.Copy(bytes,0,input.Data,bytes.Length);
            var ok=protect?CryptProtectData(ref input,null,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output):CryptUnprotectData(ref input,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output);
            if(!ok)throw new System.Security.Cryptography.CryptographicException("The current Windows user could not access this MCP session.");
            var result=new byte[output.Length];Marshal.Copy(output.Data,result,0,result.Length);return result;
        }
        finally{for(var i=0;i<input.Length;i++)Marshal.WriteByte(input.Data,i,0);Marshal.FreeHGlobal(input.Data);if(output.Data!=IntPtr.Zero){for(var i=0;i<output.Length;i++)Marshal.WriteByte(output.Data,i,0);LocalFree(output.Data);}}
    }
}
