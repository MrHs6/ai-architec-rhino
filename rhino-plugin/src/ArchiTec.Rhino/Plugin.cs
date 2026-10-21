using System.Runtime.InteropServices;
using Rhino;
using Rhino.Commands;
using Rhino.PlugIns;
using Rhino.UI;

[assembly: Guid("0ab9a769-52f9-4378-85cb-cf8177ed5166")]

namespace ArchiTec.RhinoPlugin;

public sealed class ArchiTecPlugin : PlugIn
{
    public override PlugInLoadTime LoadTime=>PlugInLoadTime.WhenNeededOrOptionsDialog;
    protected override void OptionsDialogPages(List<OptionsDialogPage> pages)=>pages.Add(new ArchiTecOptionsPage());
    protected override LoadReturnCode OnLoad(ref string errorMessage)
    {
        Panels.RegisterPanel(this, typeof(ArchiTecPanel), "AI Archi-Tec", (System.Drawing.Icon?)null);
        RhinoApp.WriteLine("AI Archi-Tec loaded. Type archtec to open the modeling assistant. Settings: Tools > Options > AI Archi-Tec.");
        return LoadReturnCode.Success;
    }
    protected override void OnShutdown() {ArchiTecPanel.Current?.StopMcp();ArchiTecPanel.Current?.ClearPreview();}
}

public sealed class ArchtecCommand : Command
{
    public override string EnglishName=>"archtec";
    protected override Result RunCommand(RhinoDoc doc,RunMode mode)
    {
        Panels.OpenPanel(typeof(ArchiTecPanel));
        ArchiTecPanel.SizeFloatingWindow();
        return Result.Success;
    }
}

public sealed class AIArchitectCommand : Command
{
    public override string EnglishName => "AIArchitect";
    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        Panels.OpenPanel(typeof(ArchiTecPanel));
        ArchiTecPanel.SizeFloatingWindow();
        return Result.Success;
    }
}

public sealed class AIArchitectFloatCommand : Command
{
    public override string EnglishName => "AIArchitectFloat";
    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        Panels.OpenPanel(typeof(ArchiTecPanel));
        Panels.FloatPanel(typeof(ArchiTecPanel), Panels.FloatPanelMode.Show);
        ArchiTecPanel.SizeFloatingWindow();
        return Result.Success;
    }
}
