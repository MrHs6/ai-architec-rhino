using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ArchiTec.Core;

/// <summary>One-time client setup for the current Windows user; never exports a credential.</summary>
public static class McpClientInstaller
{
    public static string? FindCodex()
    {
        // Prefer the desktop-managed CLI over PATH shims inherited by a CAD host.
        var root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OpenAI","Codex","bin");
        if(Directory.Exists(root))
        {
            var installed=Directory.EnumerateDirectories(root).OrderByDescending(Directory.GetLastWriteTimeUtc).Select(d=>Path.Combine(d,"codex.exe")).FirstOrDefault(File.Exists);
            if(installed is not null)return installed;
        }
        foreach(var directory in (Environment.GetEnvironmentVariable("PATH")??"").Split(Path.PathSeparator))
        {try{var path=Path.Combine(directory.Trim('"'),"codex.exe");if(File.Exists(path))return path;}catch{}}
        return null;
    }

    public static async Task Register(string client,string bridge,int port,CancellationToken cancel)
    {
        if(port is <1024 or >65535||!File.Exists(bridge))throw new ArgumentException("The installed Archi-Tec bridge could not be found.");
        if(client=="Codex")
        {
            var cli=FindCodex()??throw new InvalidOperationException("Codex was not found. Install Codex first, or expand Manual setup and add the copied configuration in your client.");
            // Rhino is a GUI process with no console input handle. Give the CLI a
            // complete redirected stream set and a writable user working directory.
            var start=new ProcessStartInfo(cli){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,
                WorkingDirectory=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)};
            foreach(var arg in new[]{"mcp","add","ai-architec-rhino","--env","ARCHITEC_MCP_PORT="+port,"--",bridge})start.ArgumentList.Add(arg);
            using var process=Process.Start(start)??throw new InvalidOperationException("Codex setup could not start.");
            process.StandardInput.Close();
            var stdout=process.StandardOutput.ReadToEndAsync(cancel);var stderr=process.StandardError.ReadToEndAsync(cancel);
            try{await process.WaitForExitAsync(cancel);await Task.WhenAll(stdout,stderr);}
            catch{if(!process.HasExited)process.Kill();throw;}
            if(process.ExitCode!=0)
            {
                // Never echo arbitrary configuration lines, which may contain keys.
                var error=(await stderr).ToLowerInvariant();
                var reason=error.Contains("access is denied")||error.Contains("permission denied")?"Configuration access was denied."
                    :error.Contains("invalid handle")?"The client rejected a Windows input/output handle."
                    :error.Contains("parse")||error.Contains("toml")?"The client reported a configuration parsing error."
                    :error.Contains("unrecognized")||error.Contains("unexpected argument")?"This Codex CLI does not support the setup arguments."
                    :"The client did not accept the setup request. "+SafeSetupDiagnostic(await stderr);
                throw new InvalidOperationException($"Codex setup failed (exit {process.ExitCode}). {reason} Use Manual setup; existing servers and credentials were preserved.");
            }
            return;
        }
        if(client!="Claude Desktop")throw new InvalidOperationException("Use Manual setup for this MCP client.");
        var folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Claude");
        Directory.CreateDirectory(folder);
        var path=Path.Combine(folder,"claude_desktop_config.json");
        var before=File.Exists(path)?await File.ReadAllTextAsync(path,cancel):"{}";
        var after=MergeClaude(before,bridge,port);
        // The backup stays beside the original, under the same user's application data.
        if(File.Exists(path))File.Copy(path,path+".architec-backup",true);
        var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{await File.WriteAllTextAsync(temp,after,cancel);File.Move(temp,path,true);}
        finally{if(File.Exists(temp))File.Delete(temp);}
    }

    public static string SafeSetupDiagnostic(string error)
    {
        // Configuration parser output may include the offending source line.
        // Suppress assignments and credential-like lines before showing diagnostics.
        var lines=error.Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries)
            .Where(line=>!line.Contains('=')&&!new[]{"token","secret","password","authorization","api key","api_key","sk-","bearer"}.Any(word=>line.Contains(word,StringComparison.OrdinalIgnoreCase)))
            .Take(5);
        var text=string.Join(" ",lines).Trim();return text.Length>500?text[..500]:text;
    }

    public static string MergeClaude(string existing,string bridge,int port)
    {
        if(port is <1024 or >65535)throw new ArgumentException("Invalid MCP port.");
        var root=JsonNode.Parse(existing) as JsonObject??throw new ArgumentException("Claude configuration is not a JSON object. Its existing file was not changed.");
        if(root["mcpServers"] is not null&&root["mcpServers"] is not JsonObject)throw new ArgumentException("Claude mcpServers is not an object. Its existing file was not changed.");
        var servers=root["mcpServers"] as JsonObject;
        if(servers is null){servers=new JsonObject();root["mcpServers"]=servers;}
        servers["ai-architec-rhino"]=new JsonObject{["command"]=bridge,["env"]=new JsonObject{["ARCHITEC_MCP_PORT"]=port.ToString(System.Globalization.CultureInfo.InvariantCulture)}};
        return root.ToJsonString(new JsonSerializerOptions{WriteIndented=true});
    }

    public static void Open(string client)
    {
        if(client=="Codex")
        {
            var cli=FindCodex()??throw new InvalidOperationException("Open your Codex desktop application to continue.");
            var start=new ProcessStartInfo(cli){UseShellExecute=false,CreateNoWindow=true};start.ArgumentList.Add("app");Process.Start(start);return;
        }
        var local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var candidates=new[]{Path.Combine(local,"AnthropicClaude","claude.exe"),Path.Combine(local,"Programs","Claude","Claude.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Claude","Claude.exe")};
        var executable=candidates.FirstOrDefault(File.Exists)??throw new InvalidOperationException("Open Claude Desktop to continue. If it is already running after first-time setup, fully quit and reopen it once.");
        Process.Start(new ProcessStartInfo(executable){UseShellExecute=true});
    }
}
