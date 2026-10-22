using System;
using System.Drawing;
namespace FormatWheel;
public sealed class DragGesture
{
    bool wasLeft,triggered,moved;Point start;
    public bool Update(bool left,bool shift,Point position)
    {
        if(left&&!wasLeft){start=position;triggered=false;moved=false;}
        if(left&&(Math.Abs(position.X-start.X)>6||Math.Abs(position.Y-start.Y)>6))moved=true;
        bool fire=left&&moved&&shift&&!triggered;if(fire)triggered=true;if(!left)moved=false;wasLeft=left;return fire;
    }
}
