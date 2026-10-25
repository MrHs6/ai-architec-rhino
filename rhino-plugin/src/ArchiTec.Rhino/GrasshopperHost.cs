using System.Runtime.CompilerServices;
using ArchiTec.Core;
using Rhino;

namespace ArchiTec.RhinoPlugin;

internal interface IPreparedGrasshopper : IDisposable
{
    ExecutionReport Apply(RhinoDoc doc,CancellationToken cancellation);
}

// GH loads only on explicit activation; ordinary Rhino requests do not load it.
public static class GrasshopperHost
{
    public static void EnsureLoaded()
    {
        var automation=RhinoApp.GetPlugInObject("Grasshopper")
            ?? throw new InvalidOperationException("Grasshopper could not load. Enable Grasshopper in this Rhino 8 installation.");
        // Loading the plug-in alone does not create its canvas/editor or finish component discovery.
        // Use the official automation entry point before touching Instances.ComponentServer.
        var load=automation.GetType().GetMethod("LoadEditor",Type.EmptyTypes)
            ?? throw new InvalidOperationException("This Grasshopper installation has no editor automation interface.");
        load.Invoke(automation,null);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static object Context(uint id) { EnsureLoaded(); return GrasshopperBridge.Context(id); }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static object Search(string query,int limit) { EnsureLoaded(); return GrasshopperBridge.Search(query,limit); }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static object PlanningContext(uint id) { EnsureLoaded(); return new { state=GrasshopperBridge.Context(id), components=GrasshopperBridge.PlanningCatalog() }; }
    public static void Prepare(RhinoDoc doc,PreparedPlan plan) { EnsureLoaded(); PrepareLoaded(doc,plan); }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void PrepareLoaded(RhinoDoc doc,PreparedPlan plan) => plan.Grasshopper = GrasshopperBridge.Prepare(doc,plan.Plan);
}
