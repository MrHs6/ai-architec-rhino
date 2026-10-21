using System.Reflection;
using System.Windows;
using Eto.Forms;

namespace ArchiTec.RhinoPlugin;

internal static class NativeViewHost
{
    public static Control Wrap(FrameworkElement view)
    {
        // Rhino distributes a signed Eto build; upstream Eto.Wpf NuGet uses a different
        // assembly identity. Resolve the host adapter at runtime to keep CI references
        // identical to RhinoCommon's signed Eto, without shipping another UI framework.
        var platform=AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a=>a.GetName().Name=="Eto.Wpf")??Assembly.Load("Eto.Wpf");
        var method=platform.GetType("Eto.Forms.WpfHelpers")?.GetMethod("ToEto",[typeof(FrameworkElement)])
            ??throw new InvalidOperationException("Rhino's WPF panel adapter is unavailable.");
        return (Control)(method.Invoke(null,[view])??throw new InvalidOperationException("Rhino could not create the panel."));
    }
}
