using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace FormatWheel;

// A drawing-only card: it never intercepts a drag or changes the selected sector.
public static class StatsPanel
{
    static readonly Brush Muted=new SolidColorBrush(Color.FromRgb(178,187,202));
    static readonly Brush Gain=new SolidColorBrush(Color.FromRgb(91,186,231));
    static readonly Brush Loss=new SolidColorBrush(Color.FromRgb(235,79,76));
    static readonly Brush Track=new SolidColorBrush(Color.FromRgb(62,68,81));
    static readonly Typeface Regular=new("Microsoft JhengHei UI");
    static readonly Typeface Bold=new(new FontFamily("Microsoft JhengHei UI"),FontStyles.Normal,FontWeights.Bold,FontStretches.Normal);
    public static (double Kept,double Gain,double Loss) Segments(double baseline,double target)
    {
        baseline=Math.Clamp(baseline,0,1);target=Math.Clamp(target,0,1);
        return(Math.Min(baseline,target),Math.Max(0,target-baseline),Math.Max(0,baseline-target));
    }
    public static void Draw(DrawingContext dc,WheelCanvas view,ActionItem item)
    {
        const double width=380,pad=22;double y=20;
        var content=new List<Action<DrawingContext>>();
        double dpi=VisualTreeHelper.GetDpi(view).PixelsPerDip;
        void Text(string value,double size,Brush brush,bool bold=false,int lines=0,double gap=8)
        {
            if(string.IsNullOrWhiteSpace(value))return;
            var text=new FormattedText(value,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,bold?Bold:Regular,size,brush,dpi){MaxTextWidth=width-pad*2};
            if(lines>0){text.MaxLineCount=lines;text.Trimming=TextTrimming.CharacterEllipsis;}
            double at=y;content.Add(c=>c.DrawText(text,new Point(pad,at)));y+=text.Height+gap;
        }
        void Rule(){double at=y+2;content.Add(c=>c.DrawLine(new Pen(Track,1),new Point(pad,at),new Point(width-pad,at)));y+=14;}
        var assessment=view.Assessment?.Evaluate(item);
        Text(assessment?.Title??item.DisplayLabel(view.Files),20,view.Accent,true,2);
        Text((view.Assessment?.SourceName??(view.Files.Length>0?System.IO.Path.GetFileName(view.Files[0]):L.Text("示範檔案","Example file")))+" → "+item.Label,12,Muted,lines:2,gap:5);
        Text(view.Assessment?.Summary??(view.Demo?L.Text("示範模式 · 選擇檔案後顯示實際屬性","Demo · choose files to inspect their properties"):L.Text("正在讀取檔案資料…","Reading file information…")),12,Muted,lines:3,gap:10);
        Text(assessment?.Detail??item.DisplayDetail(view.Files),13,Brushes.White,lines:3,gap:10);
        if(!view.Demo&&item.Unavailable(view.Files) is string reason)Text(reason,12,Brushes.Gold);
        if(assessment?.Metrics.Count>0)
        {
            Rule();
            foreach(var metric in assessment.Metrics)
            {
                double at=y;
                var label=new FormattedText(metric.Label,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,Bold,13,Brushes.White,dpi){MaxTextWidth=125};
                var bar=new Rect(158,at+5,width-pad-158,10);
                content.Add(c=>{c.DrawText(label,new Point(pad,at));DrawBar(c,bar,metric.Base,metric.Target);});
                y+=Math.Max(23,label.Height+4);
                Text(metric.Note,11.5,Muted,gap:12);
            }
            Text(L.Text("白色 保留　藍色 提升　紅色 犧牲","White: retained · Blue: gain · Red: loss"),11,Muted,gap:5);
            Text(L.Text("屬性條為相對預估；不是實測畫質或縮小百分比。","Relative estimates, not measured quality or size reduction."),11,Muted,gap:10);
        }
        if(assessment!=null)foreach(var notice in assessment.Notices.Distinct())Text(notice,11.5,Muted,gap:7);
        Rule();
        var privacy=view.PrivacyScan;
        Text(L.Text("通緝星級 · 隱私痕跡","Wanted level · privacy traces"),13,Brushes.White,true,gap:7);
        if(privacy==null)Text(view.Demo?L.Text("示範模式不檢查檔案","Files are not scanned in demo mode"):L.Text("正在檢查中繼資料…","Checking metadata…"),12,Muted);
        else
        {
            if(privacy.CanRate)
            {
                double at=y;bool evade=item.Id.EndsWith(".strip")&&privacy.Level>0;
                // A slow pulse only previews removal. The score is unchanged until output is scanned.
                double opacity=evade&&SystemParameters.ClientAreaAnimation ? .45+.55*(.5+.5*Math.Cos(Environment.TickCount64/900d*Math.PI*2)) : 1;
                content.Add(c=>{c.PushOpacity(opacity);DrawStars(c,new Point(pad+12,at+12),privacy.Level);c.Pop();});
                y+=32;
            }
            Text(privacy.StatusText,11.5,Muted,gap:5);
            Text(privacy.Detail,11.5,privacy.CanRate&&privacy.Level>0?Brushes.Gold:Muted,gap:6);
            if(item.Id.EndsWith(".strip"))Text(L.Text("移除可處理的中繼資料；完成後重新檢查。","Removes supported metadata; checks the output again."),11.5,Muted,gap:6);
        }
        double height=y+14;
        double scale=Math.Min(1,Math.Min(Math.Max(1,view.ActualWidth-24)/width,Math.Max(1,view.ActualHeight-32)/height));
        var bounds=WheelPlacement.Hint(view.Center,view.Radius,view.Hover,new Size(view.ActualWidth,view.ActualHeight),new Size(width*scale,height*scale));
        view.LastHintBounds=bounds;
        var anchor=WheelCanvas.Polar(view.Center,view.Radius+3,view.Hover*Math.PI/4);
        dc.DrawLine(new Pen(view.Accent,1.5),anchor,new Point(Math.Clamp(anchor.X,bounds.Left,bounds.Right),Math.Clamp(anchor.Y,bounds.Top,bounds.Bottom)));
        dc.PushTransform(new TranslateTransform(bounds.Left,bounds.Top));dc.PushTransform(new ScaleTransform(scale,scale));
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(250,20,24,33)),new Pen(view.Accent,1.5),new Rect(0,0,width,height),12,12);
        foreach(var draw in content)draw(dc);dc.Pop();dc.Pop();
    }
    static void DrawBar(DrawingContext dc,Rect rect,double? baseline,double? target)
    {
        dc.DrawRectangle(Track,null,rect);
        if(baseline.HasValue&&target.HasValue)
        {
            var parts=Segments(baseline.Value,target.Value);
            dc.DrawRectangle(Brushes.WhiteSmoke,null,new Rect(rect.X,rect.Y,rect.Width*parts.Kept,rect.Height));
            dc.DrawRectangle(parts.Gain>0?Gain:Loss,null,new Rect(rect.X+rect.Width*parts.Kept,rect.Y,rect.Width*(parts.Gain+parts.Loss),rect.Height));
        }
        else dc.DrawLine(new Pen(Muted,1),new Point(rect.Left,rect.Top+rect.Height/2),new Point(rect.Right,rect.Top+rect.Height/2));
        for(int i=1;i<5;i++)dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(20,24,33)),null,new Rect(rect.Left+rect.Width*i/5-1,rect.Top,2,rect.Height));
    }
    public static void DrawStars(DrawingContext dc,Point first,int level)
    {
        for(int i=0;i<5;i++)
        {
            var center=new Point(first.X+i*32,first.Y);var g=new StreamGeometry();
            using(var path=g.Open())for(int p=0;p<10;p++){double angle=p*Math.PI/5-Math.PI/2,r=p%2==0?12:5.4;var at=new Point(center.X+Math.Cos(angle)*r,center.Y+Math.Sin(angle)*r);if(p==0)path.BeginFigure(at,true,true);else path.LineTo(at,true,false);}
            dc.DrawGeometry(i>=5-Math.Clamp(level,0,5)?Brushes.WhiteSmoke:Track,new Pen(Muted,1),g);
        }
    }
}
