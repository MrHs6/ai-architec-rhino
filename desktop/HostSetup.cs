using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Studio;

internal sealed record HostInstall(string Name,string Edition,string Exe)
{
    [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet=System.Runtime.InteropServices.CharSet.Unicode)]
    static extern uint ExtractIconEx(string file,int index,out IntPtr large,IntPtr small,uint count);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool DestroyIcon(IntPtr icon);
    internal ImageSource? Icon()
    {
        if(ExtractIconEx(Exe,0,out var icon,IntPtr.Zero,1)!=1 || icon==IntPtr.Zero)return null;
        try {
            var image=Imaging.CreateBitmapSourceFromHIcon(icon,Int32Rect.Empty,BitmapSizeOptions.FromWidthAndHeight(32,32));
            image.Freeze();return image;
        }finally{DestroyIcon(icon);}
    }
}

internal static class HostSetup
{
    static readonly string Programs=Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
    static readonly string Roaming=Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    static readonly string Payload=Path.Combine(AppContext.BaseDirectory,"Bridges");
    static readonly string Scripts=Path.Combine(AppContext.BaseDirectory,"Connectors");

    internal static IReadOnlyList<HostInstall> Detect()
    {
        var found=new List<HostInstall>();
        void Scan(string name,string vendor,string pattern,string exe)
        {
            var root=Path.Combine(Programs,vendor);
            if(!Directory.Exists(root))return;
            foreach(var folder in Directory.EnumerateDirectories(root,pattern))
            {
                var path=Path.Combine(folder,exe);
                if(!File.Exists(path))continue;
                var edition=Regex.Match(Path.GetFileName(folder),@"\d+(\.\d+)?").Value;
                found.Add(new(name,edition,path));
            }
        }
        Scan("FreeCAD","","FreeCAD *","bin/FreeCAD.exe");
        Scan("Maya","Autodesk","Maya*","bin/maya.exe");
        Scan("Rhino","","Rhino *","System/Rhino.exe");
        Scan("Revit","Autodesk","Revit *","Revit.exe");
        Scan("AutoCAD","Autodesk","AutoCAD *","acad.exe");
        Scan("3ds Max","Autodesk","3ds Max *","3dsmax.exe");
        Scan("Blender","Blender Foundation","Blender *","blender.exe");
        Scan("SketchUp","SketchUp","SketchUp *","SketchUp.exe");
        Scan("Archicad","GRAPHISOFT","Archicad *","Archicad.exe");
        Scan("Photoshop","Adobe","Adobe Photoshop *","Photoshop.exe");
        Scan("InDesign","Adobe","Adobe InDesign *","InDesign.exe");
        Scan("Illustrator","Adobe","Adobe Illustrator *","Support Files/Contents/Windows/Illustrator.exe");
        return found.OrderByDescending(x=>Version.TryParse(x.Edition.Contains('.')?x.Edition:x.Edition+".0",out var v)?v:new Version()).ToArray();
    }

    internal static HostInstall? Find(IEnumerable<HostInstall> installs,string name,string edition)=>
        installs.FirstOrDefault(x=>x.Name==(name=="Grasshopper"?"Rhino":name) &&
            (edition=="Automatic" || x.Edition==edition || edition.EndsWith(".x") && x.Edition.StartsWith(edition[..^1],StringComparison.Ordinal)));

    static string RevitFolder=>Path.Combine(Roaming,"Autodesk","Revit","Addins","2025");
    static string SketchFile(HostInstall host)=>Path.Combine(Roaming,"SketchUp","SketchUp "+host.Edition,"SketchUp","Plugins","architec_studio.rb");
    internal static bool NeedsInstall(HostInstall host)
    {
        if(host.Name=="Rhino"){
            var path=Path.Combine(Roaming,"McNeel","Rhinoceros","packages",host.Edition+".0","ai-architec-rhino");
            return !Directory.Exists(path)||Directory.GetFiles(path,"AIArchiTec.McpBridge.exe",SearchOption.AllDirectories).Length==0;
        }
        if(host.Name=="Revit" && host.Edition=="2025")return !File.Exists(Path.Combine(RevitFolder,"Studio.Revit2025.addin"))||!File.Exists(Path.Combine(RevitFolder,"ArchiTecStudio","ArchiTec.Revit.dll")) || (File.Exists(Path.Combine(Payload,"ArchiTec.Revit.dll")) && !File.ReadAllBytes(Path.Combine(Payload,"ArchiTec.Revit.dll")).SequenceEqual(File.ReadAllBytes(Path.Combine(RevitFolder,"ArchiTecStudio","ArchiTec.Revit.dll"))));
        if(host.Name!="SketchUp")return false;
        var bundled=Path.Combine(Scripts,"architec_studio.rb");
        return !File.Exists(SketchFile(host)) || (File.Exists(bundled) && !File.ReadAllBytes(bundled).SequenceEqual(File.ReadAllBytes(SketchFile(host))));
    }

    static void InstallFile(string source,string target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var temp=target+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{File.Copy(source,temp);File.Move(temp,target,true);}finally{File.Delete(temp);}
    }

    internal static async Task Install(HostInstall host)
    {
        if(host.Name=="Revit" && host.Edition=="2025"){
            InstallFile(Path.Combine(Payload,"ArchiTec.Revit.dll"),Path.Combine(RevitFolder,"ArchiTecStudio","ArchiTec.Revit.dll"));
            InstallFile(Path.Combine(Payload,"Studio.Revit2025.addin"),Path.Combine(RevitFolder,"Studio.Revit2025.addin"));
        }else if(host.Name=="SketchUp"){
            InstallFile(Path.Combine(Scripts,"architec_studio.rb"),SketchFile(host));
        }else if(host.Name=="Rhino"){
            var pattern=host.Edition switch{"8"=>"*-rh8_31-win.yak","9"=>"*-rh9-win.yak",_=>throw new IOException("Choose Rhino 8 or 9.")};
            var info=new ProcessStartInfo(Path.Combine(Path.GetDirectoryName(host.Exe)!,"Yak.exe")){UseShellExecute=false,CreateNoWindow=true};
            info.ArgumentList.Add("install");info.ArgumentList.Add(Directory.GetFiles(Payload,pattern).Single());
            using var process=Process.Start(info)!;await process.WaitForExitAsync();
            if(process.ExitCode!=0)throw new IOException("Rhino Package Manager could not install the bridge.");
        }
    }

    internal static void Launch(HostInstall host)
    {
        var info=new ProcessStartInfo(host.Exe){UseShellExecute=false};
        if(host.Name=="Maya"){
            var script=Path.Combine(Scripts,"StudioScene.py").Replace('\\','/');
            string python="import runpy; runpy.run_path("+System.Text.Json.JsonSerializer.Serialize(script)+")";
            info.ArgumentList.Add("-command");
            info.ArgumentList.Add("python(\""+python.Replace("\\","\\\\").Replace("\"","\\\"")+"\")");
        }
        if(host.Name=="FreeCAD")info.ArgumentList.Add(Path.Combine(Scripts,"StudioScene.FCMacro"));
        if(host.Name=="Blender"){info.ArgumentList.Add("--python");info.ArgumentList.Add(Path.Combine(Scripts,"StudioScene.py"));}
        if(host.Name=="3ds Max"){info.ArgumentList.Add("-U");info.ArgumentList.Add("MAXScript");info.ArgumentList.Add(Path.Combine(Scripts,"StudioStart.ms"));}
        using var process=Process.Start(info);
    }
}
