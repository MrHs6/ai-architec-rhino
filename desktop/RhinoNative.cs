using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json.Nodes;

namespace Studio;
internal static class RhinoNative
{
    internal static async Task<JsonNode> Request(string target,string operation,JsonNode args)
    {
        string? edition=target.Split(" · ").ElementAtOrDefault(1);
        var ids=new List<int>();
        foreach(var p in Process.GetProcessesByName("Rhino")){
            using(p) {
                try {if(edition==null || p.MainModule?.FileVersionInfo.FileMajorPart.ToString()==edition)ids.Add(p.Id);}
                catch(System.ComponentModel.Win32Exception){}
            }
        }
        if(ids.Count!=1)throw new IOException("Open one matching Rhino session and select its edition.");
        using var pipe=new NamedPipeClientStream(".","AIArchiTec.Studio.Rhino."+ids[0],PipeDirection.InOut,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
        await pipe.ConnectAsync(3000);
        using var writer=new StreamWriter(pipe,new UTF8Encoding(false),1024,true){AutoFlush=true};
        using var reader=new StreamReader(pipe);
        await writer.WriteLineAsync(Wire.Node(new {operation,args}).ToJsonString());
        var result=JsonNode.Parse(await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(90))??"null");
        if(result==null)throw new IOException("Rhino closed before returning a result.");
        if(result["error"]!=null)throw new IOException(result["error"]!.ToString());
        return result["result"]??throw new IOException("Rhino returned an incomplete result.");
    }
}
