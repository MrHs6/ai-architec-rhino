using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;

namespace Studio;

internal static class CodexModel
{
    internal const string Provider = "ChatGPT via Codex";

    internal static string Executable()
    {
        var bin = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OpenAI","Codex","bin");
        var installed = Directory.Exists(bin) ? Directory.GetDirectories(bin).OrderByDescending(Directory.GetLastWriteTimeUtc) : Enumerable.Empty<string>();
        var paths = installed.Concat((Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator));
        foreach (string folder in paths)
        {
            if (!Path.IsPathFullyQualified(folder.Trim('"'))) continue;
            var cli = Path.Combine(folder.Trim('"'),"codex.exe");
            if (File.Exists(cli)) return cli;
        }
        throw new IOException("Install the Codex CLI and sign in with ChatGPT using codex login.");
    }

    static ProcessStartInfo Start(string directory, IEnumerable<string> args,string? executable=null)
    {
        var info = new ProcessStartInfo(executable??Executable()){
            WorkingDirectory=directory, UseShellExecute=false, CreateNoWindow=true,
            RedirectStandardInput=true, RedirectStandardOutput=true, RedirectStandardError=true,
            StandardInputEncoding=new UTF8Encoding(false), StandardOutputEncoding=Encoding.UTF8,
            StandardErrorEncoding=Encoding.UTF8
        };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        return info;
    }

    static async Task Run(ProcessStartInfo info, string? input, CancellationToken cancel)
    {
        using var proc = Process.Start(info) ?? throw new IOException("Codex could not start.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        deadline.CancelAfter(TimeSpan.FromMinutes(3));
        using var stop = deadline.Token.Register(() => {try {if(!proc.HasExited) proc.Kill(true);} catch(InvalidOperationException) {}});
        var stdout = proc.StandardOutput.BaseStream.CopyToAsync(Stream.Null);
        var stderr = proc.StandardError.ReadToEndAsync();
        try {
            if(input!=null) await proc.StandardInput.WriteAsync(input.AsMemory(),deadline.Token);
            proc.StandardInput.Close();
            await proc.WaitForExitAsync(deadline.Token);
            await Task.WhenAll(stdout, stderr);
        } catch(OperationCanceledException) when(!cancel.IsCancellationRequested) { throw new IOException("Codex timed out. Check the connection and try again."); }
        if(proc.ExitCode==0)return;
        string error=await stderr;
        throw new IOException(error.Contains("401") || error.Contains("login",StringComparison.OrdinalIgnoreCase)
            ? "Sign in to the Codex CLI with ChatGPT (codex login), then test again."
            : error.Contains("429") ? "Codex reported a usage limit. Check your ChatGPT account."
            : $"Codex exited with code {proc.ExitCode}. Check network access, CLI version and codex login status.");
    }

    internal static Task Login()=>Run(Start(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),["login"]),null,CancellationToken.None);

    internal static async Task Register(string target)
    {
        var slug = string.Concat(target.ToLowerInvariant().Select(c=>char.IsAsciiLetterOrDigit(c)?c:'-')).Trim('-');
        // One stable registration per target. Custom host names can share the same slug.
        var suffix = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(target)))[..8].ToLowerInvariant();
        string name = "architec-studio-"+slug+"-"+suffix;
        var dir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        await Run(Start(dir,["mcp","add",name,"--",Environment.ProcessPath!,"--mcp",target]), null, CancellationToken.None);
        await Run(Start(dir,["mcp","get",name]), null, CancellationToken.None);
    }

    internal static async Task<JsonNode> Reply(JsonArray history, JsonArray tools, CancellationToken cancellation,string? executable=null)
    {
        string dir = Directory.CreateTempSubdirectory("architec-turn-").FullName;
        var files = new List<string>();
        try {
            var thread = (JsonArray)history.DeepClone();
            var args = new List<string>{"exec","--ignore-user-config","--skip-git-repo-check","--ephemeral","--sandbox","read-only",
                "--disable","shell_tool","--disable","unified_exec","--disable","apps","--disable","plugins","-c","web_search=\"disabled\""};
            foreach (var message in thread)
            {
                if (message?["content"] is not JsonArray parts) continue;
                for(int i=0;i<parts.Count;i++)
                {
                    if ((string?)parts[i]?["type"]!="image_url") continue;
                    string data=(string)parts[i]!["image_url"]!["url"]!;
                    int split=data.IndexOf(',');
                    if(split<0 || !data.StartsWith("data:image/",StringComparison.Ordinal)) throw new IOException("Unsupported image reference.");
                    string extension=data.StartsWith("data:image/png;",StringComparison.Ordinal)?".png":".jpg";
                    string file=Path.Combine(dir,"reference-"+files.Count+extension);
                    files.Add(file);
                    await File.WriteAllBytesAsync(file,Convert.FromBase64String(data[(split+1)..]),cancellation);
                    args.Add("--image");args.Add(file);
                    parts[i]=Wire.Node(new{type="text",text="Image attached: "+Path.GetFileName(file)});
                }
            }
            string schema=Path.Combine(dir,"response.json"), answer=Path.Combine(dir,"answer.json");
            files.Add(schema);files.Add(answer);
            var shape=new {type="object",additionalProperties=false,required=new[]{"text","tool","arguments"},properties=new{
                text=new{type="string"},tool=new{type="string", @enum=tools.Select(t=>(string)t!["function"]!["name"]!).Prepend("").ToArray()},arguments=new{type="string"}}};
            await File.WriteAllTextAsync(schema, Wire.Node(shape).ToJsonString(),cancellation);
            args.AddRange(["--output-schema",schema,"--output-last-message",answer,"-"]);
            string prompt="Respond to the conversation below. To use a listed design tool, return its name and arguments encoded as a JSON object string. Studio will execute it and return a receipt. To answer normally set tool to an empty string. Use the supplied context and attached images only.\n"+
                Wire.Node(new{conversation=thread,tools}).ToJsonString();
            await Run(Start(dir,args,executable),prompt,cancellation);
            var result=JsonNode.Parse(await File.ReadAllTextAsync(answer,cancellation));
            string name=(string?)result?["tool"]??throw new IOException("Codex returned no structured response.");
            if(name=="")return Wire.Node(new{role="assistant",content=(string?)result!["text"]??""});
            if(!tools.Any(t=>(string?)t!["function"]!["name"]==name))throw new IOException("Codex requested an unavailable design tool.");
            return Wire.Node(new{role="assistant",content=(string?)result!["text"],tool_calls=new[]{new{
                id="call_"+Guid.NewGuid().ToString("N"),type="function",function=new{name,arguments=(string?)result["arguments"]??"{}"}}}});
        } finally {
            foreach(string file in files) File.Delete(file);
            if(!Directory.EnumerateFileSystemEntries(dir).Any())Directory.Delete(dir);
        }
    }
}
