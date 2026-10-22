using System;
using System.Windows;
namespace FormatWheel;
public static class WheelPlacement
{
    public static (Point Center,double Radius) Place(Point cursor,Size viewport,double requested,bool dragging)
    {
        double radius=Math.Min(requested,Math.Min(viewport.Width,viewport.Height)*.36);
        var center=dragging?new Point(Math.Clamp(cursor.X,radius+20,Math.Max(radius+20,viewport.Width-radius-20)),Math.Clamp(cursor.Y,radius+20,Math.Max(radius+20,viewport.Height-radius-20))):new Point(viewport.Width/2,viewport.Height/2);
        return (center,radius);
    }
}
