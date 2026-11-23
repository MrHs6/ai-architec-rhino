using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text.Json;
namespace Studio;

internal sealed class VoiceInput : IDisposable
{
    private readonly string alias="studio"+Guid.NewGuid().ToString("N");
    public bool Recording { get; private set; }
    [DllImport("winmm.dll", CharSet=CharSet.Unicode)] static extern int mciSendString(string command,System.Text.StringBuilder? result,int length,IntPtr handle);
    static void Send(string cmd)
    {
        if(mciSendString(cmd,null,0,IntPtr.Zero)!=0)throw new IOException("Windows could not access the microphone. Check input device and microphone permission.");
    }
    public void Start()
    {
        Send("open new type waveaudio alias "+alias);
        try { Send("record "+alias); Recording=true; }catch{Dispose();throw;}
    }
    public string Stop()
    {
        string path=Path.Combine(Path.GetTempPath(),alias+".wav");
        try {Send("stop "+alias); Send("save "+alias+" \""+path+"\"");return path;}
        finally {Dispose();}
    }
    public void Dispose(){mciSendString("close "+alias,null,0,IntPtr.Zero);Recording=false;}
    public static async Task<string> Transcribe(string path,string key,bool router=false)
    {
        if(new FileInfo(path).Length>24_000_000)throw new IOException("Choose an audio clip under 24 MB.");
        using var http=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromMinutes(3)};
        using var form=new MultipartFormDataContent();
        form.Add(new StringContent(router?"openai/whisper-large-v3":"whisper-1"),"model");
        form.Add(new StreamContent(File.OpenRead(path)),"file",Path.GetFileName(path));
        using var req=new HttpRequestMessage(HttpMethod.Post,router?"https://openrouter.ai/api/v1/audio/transcriptions":"https://api.openai.com/v1/audio/transcriptions"){Content=form};
        req.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);
        using var res=await http.SendAsync(req);
        if(!res.IsSuccessStatusCode)throw new IOException((router?"OpenRouter":"OpenAI")+" transcription returned HTTP "+(int)res.StatusCode+". Check the voice key, credits and audio format.");
        using var json=JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        if(!json.RootElement.TryGetProperty("text",out var value) || value.ValueKind!=JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))throw new IOException("No speech was transcribed. Check your microphone and record again.");
        return value.GetString()!;
    }
}
