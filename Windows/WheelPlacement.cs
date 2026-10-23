using System;
using System.Windows;
namespace FormatWheel;
public static class WheelPlacement
{
    public static Rect Hint(Point center,double radius,int slot,Size viewport,Size requested)
    {
        const double margin=12,gap=16;
        double width=Math.Min(requested.Width,Math.Max(1,viewport.Width-margin*2)),height=Math.Min(requested.Height,Math.Max(1,viewport.Height-margin*2));
        double angle=slot*Math.PI/4,dx=Math.Sin(angle),dy=-Math.Cos(angle);
        var anchor=WheelCanvas.Polar(center,radius+gap,angle);
        var preferred=new Point(Math.Abs(dx)<.1?anchor.X-width/2:dx>0?anchor.X:anchor.X-width,Math.Abs(dx)<.1?(dy<0?anchor.Y-height:anchor.Y):anchor.Y-height/2);
        Point[] candidates=[preferred,new(center.X+radius+gap,anchor.Y-height/2),new(center.X-radius-gap-width,anchor.Y-height/2),new(anchor.X-width/2,center.Y-radius-gap-height),new(anchor.X-width/2,center.Y+radius+gap)];
        Rect best=Rect.Empty;double score=double.MaxValue;
        foreach(var p in candidates)
        {
            var rect=new Rect(Math.Clamp(p.X,margin,Math.Max(margin,viewport.Width-width-margin)),Math.Clamp(p.Y,margin,Math.Max(margin,viewport.Height-height-margin)),width,height);
            double nx=Math.Clamp(center.X,rect.Left,rect.Right)-center.X,ny=Math.Clamp(center.Y,rect.Top,rect.Bottom)-center.Y;
            double overlap=Math.Max(0,radius+8-Math.Sqrt(nx*nx+ny*ny));
            double distance=(new Point(rect.X,rect.Y)-preferred).Length+overlap*100;
            if(distance<score){score=distance;best=rect;}
        }
        return best;
    }
    public static (Point Center,double Radius) Place(Point cursor,Size viewport,double requested,bool dragging)
    {
        double radius=Math.Min(requested,Math.Min(viewport.Width,viewport.Height)*.36);
        var center=dragging?new Point(Math.Clamp(cursor.X,radius+20,Math.Max(radius+20,viewport.Width-radius-20)),Math.Clamp(cursor.Y,radius+20,Math.Max(radius+20,viewport.Height-radius-20))):new Point(viewport.Width/2,viewport.Height/2);
        // On narrow displays reserve a readable side column for the expanded card.
        // Keep the ring inside the monitor; the hint still chooses the nearest free side.
        if(!dragging&&viewport.Width-center.X-radius<408&&viewport.Width>=radius*2+428)
            center.X=viewport.Width-radius-408;
        return (center,radius);
    }
}
