using System;
using System.Linq;
using System.IO;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Text.Json;

namespace FormatWheel;

public sealed class WheelCanvas : FrameworkElement
{
    public string[] Files = [];
    public int Page, Hover=-1;
    public Point Center;
    public double Radius=285;
    public string Status="";
    public string Privacy="";
    public int Wanted;
    public bool Demo;
    public bool CompactPreview;
    public Brush Accent => new SolidColorBrush((Color)ColorConverter.ConvertFromString(Settings.Current.Color));
    public string Kind => Files.Length==0?"img":Catalog.ForFiles(Files);
    public ActionItem?[] Options => Catalog.Pages[Kind][Page % Catalog.Pages[Kind].Length];
    public int Pages => Catalog.Pages[Kind].Length;
    string? hoveredAction;
    public int Hit(Point p){double dx=p.X-Center.X,dy=p.Y-Center.Y,d=dx*dx+dy*dy;if(d<Math.Pow(Radius*.56,2)||d>Radius*Radius)return -1;return ((int)Math.Floor((Math.Atan2(dx,-dy)+Math.PI/8)/(Math.PI/4))+8)%8;}
    // Both WPF mouse events and OLE drag events use this transition; repeated moves
    // inside one sector do not replay the tick. Empty slots are not selections.
    public bool UpdatePointer(Point? point)
    {
        int next=point.HasValue?Hit(point.Value):-1;
        if(next>=0&&Options[next]==null)next=-1;
        string? id=next>=0?Options[next]?.Id:null;
        bool entered=id!=null&&id!=hoveredAction;
        if(next!=Hover||id!=hoveredAction){Hover=next;hoveredAction=id;Status="";InvalidateVisual();}
        return entered;
    }
    static readonly Typeface Font=new(new FontFamily("Microsoft JhengHei UI"),FontStyles.Normal,FontWeights.Bold,FontStretches.Normal);
    static readonly Typeface Condensed=new(new FontFamily("Impact"),FontStyles.Normal,FontWeights.Normal,FontStretches.Condensed);
    public void Text(DrawingContext dc,string s,Point p,double size,Brush? brush=null,bool center=true,bool latin=false,double maxWidth=1000)
    {
        var t=new FormattedText(s,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,latin?Condensed:Font,size,brush??Brushes.White,VisualTreeHelper.GetDpi(this).PixelsPerDip){MaxTextWidth=Math.Max(1,maxWidth),TextAlignment=center?TextAlignment.Center:TextAlignment.Left,Trimming=TextTrimming.CharacterEllipsis};
        if(center)p.X-=maxWidth/2;
        var geometry=t.BuildGeometry(p);dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(230,0,0,0)),new Pen(new SolidColorBrush(Color.FromArgb(210,0,0,0)),2.4),geometry);dc.DrawGeometry(brush??Brushes.White,null,geometry);
    }
    public static Point Polar(Point c,double r,double a)=>new(c.X+Math.Sin(a)*r,c.Y-Math.Cos(a)*r);
    public static Geometry Sector(Point c,double inner,double outer,double a,double b)
    {
        var g=new StreamGeometry();using(var x=g.Open()){x.BeginFigure(Polar(c,outer,a),true,true);x.ArcTo(Polar(c,outer,b),new Size(outer,outer),0,false,SweepDirection.Clockwise,true,false);x.LineTo(Polar(c,inner,b),true,false);x.ArcTo(Polar(c,inner,a),new Size(inner,inner),0,false,SweepDirection.Counterclockwise,true,false);}g.Freeze();return g;
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);double r=Radius,inner=r*.56,scale=r/285;
        if(Center==default)Center=new Point(ActualWidth/2,ActualHeight/2);
        dc.DrawEllipse(new RadialGradientBrush(Color.FromArgb(150,0,0,0),Colors.Transparent),null,Center,inner,inner);
        for(int i=0;i<8;i++)
        {
            double a=i*Math.PI/4;bool selected=i==Hover;var option=Options[i];bool enabled=option!=null&&(Demo||option.Unavailable(Files)==null);
            Brush fill=selected?new LinearGradientBrush(Color.FromArgb(130,200,220,240),(Color)ColorConverter.ConvertFromString(Settings.Current.Color),90):new LinearGradientBrush(Color.FromArgb(155,115,115,120),Color.FromArgb(155,55,55,59),90);
            dc.DrawGeometry(fill,new Pen(new SolidColorBrush(Color.FromArgb(180,220,220,220)),1.5),Sector(Center,inner,r,a-Math.PI/8,a+Math.PI/8));
            if(selected)dc.DrawGeometry(Accent,null,Sector(Center,r-5,r,a-Math.PI/8,a+Math.PI/8));
            if(option==null)continue;var p=Polar(Center,r*.78,a);double opacity=enabled?1:.4;dc.PushOpacity(opacity);
            if(Settings.Current.Weapons)DrawWeapon(dc,option.Id,new Rect(p.X-42*scale,p.Y-42*scale,84*scale,47*scale));else DrawIcon(dc,option,new Rect(p.X-25*scale,p.Y-45*scale,50*scale,53*scale));
            Text(dc,option.Label,new Point(p.X,p.Y+10*scale),23*scale,null,true,option.Label.All(c=>c<128),145*scale);
            Text(dc,enabled?option.Ammo:L.T("未裝備"),new Point(p.X,p.Y+36*scale),13*scale,null,true,false,140*scale);dc.Pop();
        }
        dc.DrawEllipse(null,new Pen(new SolidColorBrush(Color.FromArgb(200,10,10,12)),4),Center,r,r);
        dc.DrawEllipse(null,new Pen(new SolidColorBrush(Color.FromArgb(190,15,15,15)),3),Center,inner,inner);
        var active=Hover>=0?Options[Hover]:null;
        Text(dc,Catalog.Name(Kind)+" · "+(Page==0?L.T("格式轉換"):L.T("工具")),new Point(Center.X,Center.Y-85*scale),16*scale,maxWidth:inner*1.9);
        Text(dc,active?.Label??(Files.Length==0?L.T("拖入檔案"):L.T("選擇方向")),new Point(Center.X,Center.Y-48*scale),32*scale,latin:active?.Label.All(c=>c<128)??false,maxWidth:inner*1.9);
        string detail=Demo?L.T("預覽模式 · 不會轉換檔案"):Files.Length==0?L.T("等待拖曳的檔案…"):L.F("{0} 個檔案 · 移回中心取消",Files.Length);
        if(CompactPreview&&active!=null)detail=active.Detail;
        Text(dc,detail,new Point(Center.X,Center.Y+16*scale),14*scale,maxWidth:inner*1.8);
        Text(dc,$"‹   {Page+1} / {Pages}   ›",new Point(Center.X,Center.Y+94*scale),21*scale,Accent,maxWidth:inner*1.8);
        if(CompactPreview)return;
        double cardW=Math.Min(310,ActualWidth*.28);dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(200,0,0,0)),null,new Rect(28,35,cardW,110));dc.DrawRectangle(Accent,null,new Rect(28,35,cardW,3));
        Text(dc,L.T("按 ⇧ Shift 切換  轉換 / 工具\n朝格子方向拖，放開滑鼠執行\n移回中心再放開 = 取消"),new Point(43,53),14,center:false,maxWidth:cardW-25);
        Text(dc,Status.Length>0?Status:L.T("FORMATWHEEL  /  WINDOWS    ·    Esc 取消    ·    Tab / 滾輪 翻頁"),new Point(ActualWidth/2,ActualHeight-40),12,Brushes.LightGray,maxWidth:ActualWidth-60);
        if(active!=null)DrawHint(dc,active);
    }
    void DrawHint(DrawingContext dc,ActionItem item)
    {
        double width=Math.Min(280,Math.Max(140,ActualWidth-32)),padding=16;
        FormattedText Line(string value,double size,Brush brush)=>new(value,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,Font,size,brush,VisualTreeHelper.GetDpi(this).PixelsPerDip){MaxTextWidth=width-padding*2};
        var lines=new List<FormattedText>{Line(item.Label,18,Accent),Line(item.Detail,14,Brushes.White)};
        if(!Demo&&item.Unavailable(Files) is string reason)lines.Add(Line(reason,13,Brushes.Gold));
        if(Files.Length>0){var file=Line(Path.GetFileName(Files[0])+(Files.Length>1?$" +{Files.Length-1}":""),11,Brushes.LightGray);file.MaxLineCount=1;file.Trimming=TextTrimming.CharacterEllipsis;lines.Add(file);}
        if(!Demo&&Privacy.Length>0)lines.Add(Line(new string('★',Wanted)+new string('☆',5-Wanted)+"  "+Privacy,11,Wanted>0?Brushes.Gold:Brushes.LightGray));
        double height=padding*2+lines.Sum(line=>line.Height)+7*(lines.Count-1);
        var bounds=WheelPlacement.Hint(Center,Radius,Hover,new Size(ActualWidth,ActualHeight),new Size(width,height));
        var anchor=Polar(Center,Radius+3,Hover*Math.PI/4);
        var edge=new Point(Math.Clamp(anchor.X,bounds.Left,bounds.Right),Math.Clamp(anchor.Y,bounds.Top,bounds.Bottom));
        dc.DrawLine(new Pen(Accent,2),anchor,edge);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(247,20,24,32)),new Pen(Accent,1.5),bounds,10,10);
        dc.PushClip(new RectangleGeometry(bounds));
        double y=bounds.Top+padding;foreach(var line in lines){dc.DrawText(line,new Point(bounds.Left+padding,y));y+=line.Height+7;}dc.Pop();
    }
    void DrawIcon(DrawingContext dc,ActionItem item,Rect r)
    {
        var border=new Pen(new SolidColorBrush(Color.FromRgb(25,30,40)),2);
        if(item.Label=="TIFF") {dc.DrawRectangle(Brushes.WhiteSmoke,border,new Rect(r.X+8,r.Y,r.Width-16,20));dc.DrawRoundedRectangle(Brushes.SlateGray,border,new Rect(r.X-4,r.Y+15,r.Width+8,25),4,4);dc.DrawRectangle(Brushes.WhiteSmoke,border,new Rect(r.X+6,r.Y+34,r.Width-12,18));for(int i=0;i<3;i++)dc.DrawLine(new Pen(Brushes.Gray,1.5),new Point(r.X+12,r.Y+39+i*4),new Point(r.Right-12,r.Y+39+i*4));dc.DrawEllipse(Brushes.LightGreen,border,new Point(r.Right-4,r.Y+26),3,3);return;}
        if(item.Label=="WEBP") {dc.DrawRoundedRectangle(Brushes.LightGray,border,r,4,4);dc.DrawLine(border,new Point(r.X,r.Y+12),new Point(r.Right,r.Y+12));for(int i=0;i<3;i++)dc.DrawEllipse(Brushes.DimGray,null,new Point(r.X+6+i*7,r.Y+6),1.7,1.7);var c=new Point(r.X+r.Width/2,r.Y+33);dc.DrawEllipse(null,border,c,16,16);dc.DrawEllipse(null,border,c,7,16);dc.DrawLine(border,new Point(c.X-16,c.Y),new Point(c.X+16,c.Y));return;}
        if(item.Label=="GIF") {dc.DrawRoundedRectangle(Brushes.SlateGray,border,r,3,3);for(int i=0;i<5;i++){dc.DrawRectangle(Brushes.LightGray,null,new Rect(r.X+3,r.Y+3+i*10,4,6));dc.DrawRectangle(Brushes.LightGray,null,new Rect(r.Right-7,r.Y+3+i*10,4,6));}var play=Geometry.Parse($"M {r.X+18},{r.Y+13} L {r.X+18},{r.Bottom-13} L {r.Right-14},{r.Y+r.Height/2} Z");dc.DrawGeometry(Brushes.White,border,play);return;}
        if(item.Label=="JPG") {dc.DrawRoundedRectangle(Brushes.Gray,border,new Rect(r.X-4,r.Y+4,r.Width+8,r.Height-8),2,2);var photo=new Rect(r.X,r.Y+8,r.Width,r.Height-16);dc.DrawRectangle(Brushes.SkyBlue,border,photo);dc.DrawEllipse(Brushes.Gold,null,new Point(photo.Right-9,photo.Top+9),5,5);dc.DrawGeometry(GeometryBrush(),null,Geometry.Parse($"M {photo.Left},{photo.Bottom} L {photo.Left},{photo.Bottom-10} L {photo.Left+16},{photo.Top+15} L {photo.Right},{photo.Bottom} Z"));return;}
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(228,233,240)),border,r,2,2);
        var top=new Point(r.Right-12,r.Top);var fold=new StreamGeometry();using(var g=fold.Open()){g.BeginFigure(top,true,true);g.LineTo(new Point(r.Right,r.Top+12),true,false);g.LineTo(new Point(r.Right-12,r.Top+12),true,false);}dc.DrawGeometry(Brushes.SlateGray,border,fold);
        if(new[]{"PNG","AVIF"}.Contains(item.Label))
        {
            var photo=new Rect(r.X+5,r.Y+17,r.Width-10,r.Height-24);dc.DrawRectangle(Brushes.SkyBlue,border,photo);dc.DrawEllipse(Brushes.Gold,null,new Point(photo.Right-8,photo.Top+8),4,4);var mountain=Geometry.Parse($"M {photo.Left},{photo.Bottom} L {photo.Left},{photo.Bottom-10} L {photo.Left+12},{photo.Top+13} L {photo.Right},{photo.Bottom} Z");dc.DrawGeometry(Brushes.ForestGreen,null,mountain);
            if(item.Label=="PNG")for(int y=0;y<10;y++)for(int x=0;x<2;x++)dc.DrawRectangle((x+y)%2==0?Brushes.White:Brushes.Gray,null,new Rect(r.Right-8+x*4,r.Y+12+y*4,4,4));
            if(item.Label=="AVIF")Text(dc,"↘",new Point(r.Right,r.Bottom-20),28,maxWidth:40);
        }
        else {string badge=item.Id.Contains(".to.")?item.Label:item.Id.Split('.').Last() switch {"rotate"=>"↻","half"=>"½","compress"=>"↓","strip"=>"×","ocr"=>L.T("文"),"cutout"=>"✂","fast"=>"»","slow"=>"«","merge"=>"+","split"=>"÷",_=>item.Label};var b=item.Label=="PDF"?Brushes.IndianRed:Accent;dc.DrawRoundedRectangle(b,border,new Rect(r.X-5,r.Y+21,r.Width+10,23),1,1);Text(dc,badge,new Point(r.X+r.Width/2,r.Y+23),13,latin:badge.All(c=>c<128),maxWidth:r.Width+8);}
    }
    static Brush GeometryBrush()=>Brushes.ForestGreen;
    static Dictionary<string,string>? weapons;
    static Dictionary<string,string>? weaponMap;
    void DrawWeapon(DrawingContext dc,string id,Rect rect)
    {
        try{weapons??=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Assets.File("weapons.json")));weaponMap??=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Assets.File("weapon-map.json")));string name=weaponMap!.GetValueOrDefault(id,id.EndsWith(".strip")?"silencedPistol":id.EndsWith(".compress")?"grenade":"pistol");var shape=Geometry.Parse(weapons![name]);var b=shape.Bounds;double s=Math.Min(rect.Width/b.Width,rect.Height/b.Height);dc.PushTransform(new TranslateTransform(rect.X+(rect.Width-b.Width*s)/2,rect.Y+(rect.Height+b.Height*s)/2));dc.PushTransform(new ScaleTransform(s,-s));dc.PushTransform(new TranslateTransform(-b.X,-b.Y));dc.DrawGeometry(Brushes.White,new Pen(Brushes.Black,2/s),shape);dc.Pop();dc.Pop();dc.Pop();}catch{Text(dc,"◆",new Point(rect.X+rect.Width/2,rect.Y),30,maxWidth:rect.Width);}
    }
}

public sealed class WheelWindow : Window
{
    public WheelCanvas View=new();
    readonly Action<ActionItem,string[]> execute;
    readonly bool dragging;
    bool committed,closed,lastShift;
    readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(16)};
    public WheelWindow(string[] files,bool drag,Action<ActionItem,string[]> run,bool demo=false)
    {
        execute=run;dragging=drag;View.Files=files;View.Demo=demo;View.Radius=Settings.Current.Size;Title=L.T("FormatWheel · 輪盤");
        WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=new SolidColorBrush(Color.FromArgb(105,0,0,0));Topmost=true;ShowInTaskbar=!drag;ShowActivated=!drag;AllowDrop=true;
        var screen=System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position);Left=screen.Bounds.Left;Top=screen.Bounds.Top;Width=screen.Bounds.Width;Height=screen.Bounds.Height;
        var grid=new System.Windows.Controls.Grid();
        if(Settings.Current.Blur)try{using var capture=new System.Drawing.Bitmap(screen.Bounds.Width,screen.Bounds.Height);using(var graphics=System.Drawing.Graphics.FromImage(capture))graphics.CopyFromScreen(screen.Bounds.Location,System.Drawing.Point.Empty,screen.Bounds.Size);using var bytes=new MemoryStream();capture.Save(bytes,System.Drawing.Imaging.ImageFormat.Png);bytes.Position=0;var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=bytes;bitmap.EndInit();bitmap.Freeze();grid.Children.Add(new System.Windows.Controls.Image{Source=bitmap,Stretch=Stretch.Fill,Effect=new BlurEffect{Radius=22},IsHitTestVisible=false});grid.Children.Add(new System.Windows.Controls.Border{Background=new SolidColorBrush(Color.FromArgb(100,0,0,0)),IsHitTestVisible=false});}catch{}
        grid.Children.Add(View);Content=grid;
        SourceInitialized+=(_,_)=>{var h=new WindowInteropHelper(this).Handle;Native.SetWindowPos(h,new IntPtr(-1),screen.Bounds.Left,screen.Bounds.Top,screen.Bounds.Width,screen.Bounds.Height,0x10);};
        Loaded+=(_,_)=>{var mouse=Pointer();var placement=WheelPlacement.Place(mouse,new Size(ActualWidth,ActualHeight),View.Radius,drag);View.Radius=placement.Radius;View.Center=placement.Center;View.InvalidateVisual();Output.Log($"wheel display={screen.DeviceName} bounds={screen.Bounds} logical={ActualWidth}x{ActualHeight} dpi={VisualTreeHelper.GetDpi(this).PixelsPerDip}");Sound.Play("open");UpdatePointer(mouse,false);timer.Start();if(SystemParameters.ClientAreaAnimation)BeginAnimation(OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(110)));};
        DragEnter+=Drag;DragOver+=Drag;
        DragLeave+=(_,_)=>UpdatePointer(null);
        Drop+=(_,e)=>{e.Handled=true;e.Effects=DragDropEffects.None;if(e.Data.GetDataPresent(DataFormats.FileDrop)){View.Files=(string[])e.Data.GetData(DataFormats.FileDrop);UpdatePointer(e.GetPosition(View),false);Select();if(committed)e.Effects=DragDropEffects.Copy;}if(!committed&&!closed)Close();};
        MouseMove+=(_,e)=>UpdatePointer(e.GetPosition(View));MouseLeave+=(_,_)=>UpdatePointer(null);
        MouseLeftButtonDown+=(_,e)=>{if(!dragging){UpdatePointer(e.GetPosition(View),false);Select();}};MouseRightButtonDown+=(_,_)=>Close();
        MouseWheel+=(_,_)=>Flip();KeyDown+=(_,e)=>{if(e.Key==Key.Escape)Close();else if(e.Key==Key.LeftShift||e.Key==Key.RightShift)Shift(true);else if(e.Key==Key.Tab||e.Key==Key.Q||e.Key==Key.E)Flip();else if(e.Key==Key.Enter||e.Key==Key.Space)Select();};
        KeyUp+=(_,e)=>{if(e.Key==Key.LeftShift||e.Key==Key.RightShift)Shift(Native.Down(0x10));};
        lastShift=Native.Down(0x10);timer.Tick+=(_,_)=>{if(Native.Down(0x1B)){Close();return;}Shift(Native.Down(0x10));UpdatePointer(dragging||IsActive?Pointer():null);if(dragging&&!Native.Down(1)){timer.Stop();Dispatcher.BeginInvoke(()=>{if(!closed)Close();},DispatcherPriority.Background);}};Closed+=(_,_)=>{closed=true;timer.Stop();Sound.ClearHover();};
        if(files.Length>0)_=Scan();
    }
    Point Pointer()=>View.PointFromScreen(new Point(System.Windows.Forms.Cursor.Position.X,System.Windows.Forms.Cursor.Position.Y));
    void UpdatePointer(Point? point,bool audible=true){if(View.UpdatePointer(point)){if(audible)Sound.Play("hover");FeedbackTrace.Write($"hover {View.Options[View.Hover]!.Id}");}else if(View.Hover<0)Sound.ClearHover();}
    void Shift(bool down){if(down&&!lastShift)Flip();lastShift=down;}
    void Drag(object sender,DragEventArgs e){e.Handled=true;e.Effects=DragDropEffects.None;if(!e.Data.GetDataPresent(DataFormats.FileDrop)){UpdatePointer(null);return;}var files=(string[])e.Data.GetData(DataFormats.FileDrop);if(!View.Files.SequenceEqual(files)){View.Files=files;View.Page=0;_=Scan();View.InvalidateVisual();}UpdatePointer(e.GetPosition(View));if(View.Hover>=0&&View.Options[View.Hover]?.Unavailable(files)==null)e.Effects=DragDropEffects.Copy;}
    async System.Threading.Tasks.Task Scan(){var files=View.Files;var scan=await System.Threading.Tasks.Task.Run(()=>Privacy.Scan(files));if(closed||!files.SequenceEqual(View.Files))return;View.Wanted=scan.Level;View.Privacy=scan.Detail;View.InvalidateVisual();}
    public void Flip(){View.Page=(View.Page+1)%View.Pages;UpdatePointer(Pointer(),false);View.InvalidateVisual();Sound.ClearHover();Sound.Play("flip");}
    void Select(){if(committed)return;if(View.Hover<0){Close();return;}var item=View.Options[View.Hover];if(item==null)return;if(View.Demo){Close();Banner.Show(true,L.T("預覽完成"),L.T("這是演示，沒有轉換檔案"));return;}if(item.Unavailable(View.Files) is string reason){View.Status=reason;View.InvalidateVisual();Sound.Play("denied");return;}if(View.Files.Length==0)return;committed=true;var files=View.Files;Close();execute(item,files);}
}
public static class Native
{
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
    public static bool Down(int key)=>(GetAsyncKeyState(key)&0x8000)!=0;
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int height,uint flags);
}
