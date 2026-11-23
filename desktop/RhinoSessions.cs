using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace Studio;

internal sealed record RhinoSession(int Port, int ProcessId, int Major);

internal static class RhinoSessions
{
    internal static RhinoSession? Resolve(string host, bool allowMissing = false)
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AIArchiTec", "Rhino8");
        return Resolve(host, folder, pid =>
        {
            using var process = Process.GetProcessById(pid);
            if (process.HasExited || !process.ProcessName.Equals("Rhino", StringComparison.OrdinalIgnoreCase)) return null;
            return process.MainModule?.FileVersionInfo.FileMajorPart;
        },allowMissing);
    }

    internal static RhinoSession? Resolve(string host, string folder, Func<int,int?> majorForProcess,bool allowMissing=false)
    {
        var edition = host.Split(" · ").ElementAtOrDefault(1);
        var matches = new List<RhinoSession>();
        if (Directory.Exists(folder))
        {
            foreach (var file in Directory.EnumerateFiles(folder, "mcp-session-*.json"))
            {
                try
                {
                    using var json = JsonDocument.Parse(File.ReadAllText(file));
                    var info = json.RootElement;
                    int pid = info.GetProperty("ProcessId").GetInt32();
                    if (!Uri.TryCreate(info.GetProperty("Endpoint").GetString(), UriKind.Absolute, out var uri)
                        || uri.Scheme != "http" || !uri.IsLoopback || uri.AbsolutePath != "/mcp"
                        || uri.Port < 1024 || Path.GetFileName(file) != $"mcp-session-{uri.Port}.json") continue;
                    var major = majorForProcess(pid);
                    if (major is 8 or 9 && (edition == null || edition == major.ToString()))
                        matches.Add(new(uri.Port, pid, major.Value));
                }
                catch (Exception ex) when (ex is IOException or JsonException or ArgumentException or InvalidOperationException or KeyNotFoundException or Win32Exception or UnauthorizedAccessException or FormatException) { }
            }
        }
        return matches.Count switch
        {
            1 => matches[0],
            0 when allowMissing => null,
            0 => throw new IOException($"Open {host}, enable MCP in the Archi-Tec panel, then check the connection."),
            _ => throw new IOException("Multiple Rhino bridges match. Select the Rhino edition or stop the extra bridge.")
        };
    }
}
