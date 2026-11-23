using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ShapePath=System.Windows.Shapes.Path;
namespace Studio;

internal static class HostMark
{
    // Studio pictograms for catalog entries without a local application icon.
    static readonly Dictionary<string,string> Paths=new()
    {
        ["Rhino"]="M2,13 L5,8 12,7 16,8 18,6 18,10 23,8 21,13 15,15 13,19 10,19 11,15 7,15 5,19 3,19 Z",
        ["Grasshopper"]="M2,5 H7 V10 H2 Z M17,14 H22 V19 H17 Z M7,7 C14,7 10,16 17,16",
        ["Revit"]="M4,20 V4 H14 Q21,4 20,10 Q19,14 14,14 H8 M13,14 L20,20",
        ["SketchUp"]="M12,2 L22,8 V17 L12,23 2,17 V8 Z M2,8 L12,14 22,8 M12,14 V23 M7,5 L17,11 V16 L12,19",
        ["3ds Max"]="M2,18 L6,6 12,15 18,4 22,18 M2,18 L10,20 22,18",
        ["Blender"]="M2,13 L8,7 3,7 M8,7 L15,7 9,2 M15,7 C25,8 24,20 15,21 C4,22 4,10 14,10 C22,10 21,18 15,18 C9,18 9,13 14,13",
        ["AutoCAD"]="M2,20 L11,3 H14 L22,20 M6,15 H18 M4,19 L12,16 20,19",
        ["Archicad"]="M3,21 Q11,2 21,3 Q15,8 14,20 M6,17 L15,15",
        ["Both"]="M2,8 H21 M16,3 L21,8 16,13 M22,17 H3 M8,12 L3,17 8,22"
    };
    internal static FrameworkElement Draw(string name,string mark,string tint)
    {
        var ink=(Brush)new BrushConverter().ConvertFromString(tint)!;
        FrameworkElement symbol;
        if(Paths.TryGetValue(name,out var data))
            symbol=new ShapePath{Data=Geometry.Parse(data),Stroke=ink,StrokeThickness=1.7,Stretch=Stretch.Uniform,Width=21,Height=20,StrokeLineJoin=PenLineJoin.Round,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center};
        else symbol=new TextBlock{Text=mark,Foreground=ink,FontSize=15,FontWeight=FontWeights.SemiBold,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
        return new Border{Width=32,Height=30,CornerRadius=new CornerRadius(7),Background=new SolidColorBrush(Color.FromRgb(36,34,42)),Child=symbol};
    }
}
