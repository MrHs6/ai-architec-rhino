using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ShapePath = System.Windows.Shapes.Path;

namespace Studio;

public sealed class StudioGlyph : Viewbox
{
    static readonly Dictionary<string,string> Shapes = new()
    {
        ["apps"]="M3,3 h7 v7 H3 Z M14,3 h7 v7 h-7 Z M3,14 h7 v7 H3 Z M14,14 h7 v7 h-7 Z",
        ["flow"]="M3,9 h6 v6 H3 Z M16,3 h5 v5 h-5 Z M16,16 h5 v5 h-5 Z M9,12 h3 V5.5 h4 M12,12 v6.5 h4",
        ["settings"]="M3,6 H8 M12,6 H21 M3,17 H13 M17,17 H21 M10,3 A3,3 0 1 0 10.01,3 M15,14 A3,3 0 1 0 15.01,14",
        ["account"]="M12,3 A4,4 0 1 0 12.01,3 M4,21 v-2 a8,6 0 0 1 16,0 v2",
        ["tools"]="M3,7 H12 M18,7 H21 M3,17 H6 M12,17 H21 M15,4 A3,3 0 1 0 15.01,4 M9,14 A3,3 0 1 0 9.01,14",
        ["attach"]="M8,14 L15,7 a2,2 0 0 1 3,3 l-8,8 a4,4 0 0 1 -6,-6 l9,-9 a5,5 0 0 1 7,7 l-8,8",
        ["mic"]="M9,5 a3,3 0 0 1 6,0 v7 a3,3 0 0 1 -6,0 Z M5,11 v1 a7,7 0 0 0 14,0 v-1 M12,19 v3 M8,22 h8",
        ["stop"]="M5,5 h14 v14 H5 Z", ["send"]="M4,4 L21,12 4,20 7,12 Z M7,12 h14",
        ["chat"]="M3,4 h18 v13 H9 l-6,5 Z", ["build"]="M12,2 L21,7 v10 l-9,5 -9,-5 V7 Z M3,7 l9,5 9,-5 M12,12 v10",
        ["mail"]="M2,5 H22 V19 H2 Z M2,6 l10,8 10,-8",
        ["book"]="M12,6 Q7,2 2,4 V19 Q7,17 12,21 Q17,17 22,19 V4 Q17,2 12,6 V21",
        ["link"]="M9,15 l6,-6 M7,11 l-2,2 a4,4 0 0 0 6,6 l3,-3 M10,8 l3,-3 a4,4 0 0 1 6,6 l-2,2",
        ["exit"]="M10,3 H4 V21 H10 M10,12 H21 M16,7 l5,5 -5,5",
        ["plus"]="M12,4 V20 M4,12 H20"
    };

    string kind = "";
    public string Kind
    {
        get => kind;
        set
        {
            kind = value;
            var path = new ShapePath { Data=Geometry.Parse(Shapes[value]), StrokeThickness=1.65,
                StrokeLineJoin=PenLineJoin.Round, StrokeStartLineCap=PenLineCap.Round, StrokeEndLineCap=PenLineCap.Round };
            path.SetBinding(ShapePath.StrokeProperty, new System.Windows.Data.Binding { Path=new PropertyPath(TextBlock.ForegroundProperty), Source=this });
            Child=new Grid {Width=24,Height=24,Children={path}};
        }
    }

    public StudioGlyph() { Width=17; Height=17; IsHitTestVisible=false; }
}
