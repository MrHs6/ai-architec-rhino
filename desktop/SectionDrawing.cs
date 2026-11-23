using System.Windows;
using System.Windows.Media;
namespace Studio;

// A scaled axonometric section drawing for the workspace masthead.
public sealed class SectionDrawing : FrameworkElement
{
    protected override void OnRender(DrawingContext dc)
    {
        double scale=Math.Min(ActualWidth/270,ActualHeight/105);
        if(scale<=0)return;
        dc.PushTransform(new ScaleTransform(scale,scale));
        Point Project(double x,double y,double z)=>new(90+x*0.84+y*0.88,73+x*0.27-y*0.30-z);
        var ink=new Pen(new SolidColorBrush(Color.FromArgb(135,180,147,222)),0.85);
        var grid=new Pen(new SolidColorBrush(Color.FromArgb(48,176,149,202)),0.65);
        void Line(Point a,Point b,Pen pen)=>dc.DrawLine(pen,a,b);
        for(int k=-80;k<=120;k+=15){Line(Project(k,-65,0),Project(k,105,0),grid);Line(Project(-85,k,0),Project(95,k,0),grid);}
        for(int i=0;i<13;i++){
            double y=-30+6*i;
            var a=Project(-48,y,0);var b=Project(-48,y,18);var c=Project(-3,y,50+10*Math.Sin(i*.19));var d=Project(54,y,33);var e=Project(54,y,0);
            Line(a,b,ink);Line(b,c,ink);Line(c,d,ink);Line(d,e,ink);
            if(i==0||i==12)Line(a,e,ink);
        }
        foreach(var x in new[]{-48,54})Line(Project(x,-30,0),Project(x,42,0),ink);
        dc.Pop();
    }
}
