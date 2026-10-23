using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace FormatWheel;

internal enum FeedbackOutcome { Success, Failure, Canceled }

// One presentation for conversion success, failure and previews. The glass uses an
// in-memory desktop sample taken before showing this window; it is never saved.
internal sealed class FeedbackBanner : Window
{
    static FeedbackBanner? current;
    readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(30)};
    readonly FeedbackOutcome outcome;
    readonly PrivacyResult? before,after;
    readonly TextBlock? stars;
    long started;
    IDisposable? clicks;
    bool closed,dismissQueued;
    internal const int PassiveStyles=0x08000000|0x00000020|0x00000080; // NOACTIVATE, TRANSPARENT, TOOLWINDOW
    internal bool HasBackdrop {get;private set;}
    internal bool ClosedAutomatically {get;private set;}
    internal bool ClosedByClick {get;private set;}

    internal static FeedbackBanner ShowResult(FeedbackOutcome outcome,string title,string detail,string sizeChange="",PrivacyResult? before=null,PrivacyResult? after=null,Func<Action,IDisposable>? observeClicks=null)
    {
        current?.Close();
        var banner=new FeedbackBanner(outcome,title,detail,sizeChange,before,after,observeClicks);
        current=banner;banner.Closed+=(_,_)=>{if(current==banner)current=null;};banner.Show();return banner;
    }
    FeedbackBanner(FeedbackOutcome outcome,string title,string detail,string sizeChange,PrivacyResult? before,PrivacyResult? after,Func<Action,IDisposable>? observeClicks)
    {
        this.outcome=outcome;this.before=before;this.after=after;
        Title="FormatWheel · "+Headline(outcome);WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;
        AllowsTransparency=true;Background=Brushes.Transparent;Topmost=true;ShowInTaskbar=false;ShowActivated=false;
        Focusable=false;IsHitTestVisible=false;UseLayoutRounding=true;
        var screen=Forms.Screen.FromPoint(Forms.Cursor.Position);
        var handle=new WindowInteropHelper(this).EnsureHandle();
        SetWindowLongPtr(handle,-20,new IntPtr(GetWindowLongPtr(handle,-20).ToInt64()|PassiveStyles));
        // Move the hidden HWND to the destination monitor before reading its DPI.
        Native.SetWindowPos(handle,IntPtr.Zero,screen.WorkingArea.Left,screen.WorkingArea.Top,1,1,0x14);
        double dpi=GetDpiForWindow(handle)/96d;if(dpi<=0)dpi=1;
        double width=Math.Min(1120,screen.WorkingArea.Width/dpi-40);
        double maxHeight=screen.WorkingArea.Height/dpi-40;
        var card=CreateCard(outcome,title,detail,sizeChange,after,width,maxHeight,out stars);
        card.Measure(new Size(width,maxHeight));card.Arrange(new Rect(new Point(),card.DesiredSize));
        Width=card.DesiredSize.Width;Height=card.DesiredSize.Height;Content=card;
        var area=screen.WorkingArea;
        var bounds=Place(new Rect(area.X,area.Y,area.Width,area.Height),dpi,new Size(Width,Height));
        if(Settings.Current.Blur)
        {
            try
            {
                var image=Capture(bounds);
                var backdrop=new Image{Source=image,Stretch=Stretch.Fill,Effect=new BlurEffect{Radius=34,RenderingBias=RenderingBias.Quality}};
                backdrop.Clip=new RectangleGeometry(new Rect(18,18,Width-36,Height-36),18,18);
                // Clip after blurring as well, so rounded corners stay transparent.
                var glass=new Grid{Clip=new RectangleGeometry(new Rect(18,18,Width-36,Height-36),18,18),IsHitTestVisible=false};
                glass.Children.Add(backdrop);card.Children.Insert(0,glass);HasBackdrop=true;
            }
            catch(Exception ex){Output.Log("Feedback backdrop unavailable: "+ex.GetType().Name);}
        }
        Native.SetWindowPos(handle,new IntPtr(-1),(int)bounds.X,(int)bounds.Y,(int)bounds.Width,(int)bounds.Height,0x30);
        Loaded+=(_,_)=>
        {
            started=Environment.TickCount64;
            // Observe only fresh button-down events after showing. The release of
            // the click/drag that started conversion must not dismiss this result.
            clicks=(observeClicks??(action=>new BannerClickObserver(action)))(QueueClickDismiss);
            Output.Log($"result feedback outcome={outcome} visible={IsVisible} blur={HasBackdrop} size={ActualWidth}x{ActualHeight} dpi={dpi} passive={(GetWindowLongPtr(handle,-20).ToInt64()&PassiveStyles)==PassiveStyles}");
            timer.Start();
        };
        timer.Tick+=(_,_)=>
        {
            double seconds=(Environment.TickCount64-started)/1000d;
            Opacity=OpacityAt(outcome,seconds,SystemParameters.ClientAreaAnimation);
            if(stars!=null&&before?.CanRate==true&&before.Level>0&&after?.VerifiedClean==true&&SystemParameters.ClientAreaAnimation)
            {
                stars.Text=seconds<1.8?Stars(before.Level):Stars(0);
                stars.Opacity=seconds<1.2?.4+.6*(.5+.5*Math.Cos(seconds*Math.PI*3)):seconds<1.8?Math.Max(0,1-(seconds-1.2)/.6):1;
            }
            if(seconds>=Duration(outcome)){ClosedAutomatically=true;Close();}
        };
        Closed+=(_,_)=>{closed=true;clicks?.Dispose();clicks=null;timer.Stop();Content=null;Output.Log($"result feedback closed automatic={ClosedAutomatically} click={ClosedByClick}");};
    }
    void QueueClickDismiss()
    {
        if(closed||dismissQueued)return;
        dismissQueued=true;
        // Return promptly from the native hook; never swallow the original input.
        Dispatcher.BeginInvoke(DispatcherPriority.Send,new Action(()=>
        {
            if(closed)return;
            ClosedByClick=true;Close();
        }));
    }
    internal static double Duration(FeedbackOutcome outcome)=>outcome==FeedbackOutcome.Failure?8:5;
    internal static double OpacityAt(FeedbackOutcome outcome,double seconds,bool animate)
    {
        double end=Duration(outcome);
        if(seconds>=end)return 0;
        return !animate?1:Math.Clamp(Math.Min(seconds/.18,(end-seconds)/.45),0,1);
    }
    internal static Rect Place(Rect workArea,double dpi,Size logical)
    {
        double width=Math.Min(workArea.Width,Math.Ceiling(logical.Width*dpi)),height=Math.Min(workArea.Height,Math.Ceiling(logical.Height*dpi));
        return new Rect(Math.Round(workArea.Left+(workArea.Width-width)/2),Math.Round(workArea.Top+(workArea.Height-height)/2),width,height);
    }
    static string Headline(FeedbackOutcome outcome)=>outcome switch{FeedbackOutcome.Success=>"MISSION PASSED",FeedbackOutcome.Failure=>"WASTED",_=>"CANCELED"};
    static Brush Accent(FeedbackOutcome outcome)=>outcome switch{FeedbackOutcome.Success=>new SolidColorBrush(Color.FromRgb(255,207,62)),FeedbackOutcome.Failure=>new SolidColorBrush(Color.FromRgb(232,46,48)),_=>Brushes.Silver};
    static string Stars(int level)=>new string('☆',5-Math.Clamp(level,0,5))+new string('★',Math.Clamp(level,0,5));

    internal static Grid CreateCard(FeedbackOutcome outcome,string title,string detail,string sizeChange,PrivacyResult? after,double width,double maxHeight,out TextBlock? stars)
    {
        var root=new Grid{Width=width,IsHitTestVisible=false};
        var surface=new Border{Margin=new Thickness(18),CornerRadius=new CornerRadius(18),Background=new SolidColorBrush(Color.FromArgb(174,12,16,23)),BorderBrush=new SolidColorBrush(Color.FromArgb(40,255,255,255)),BorderThickness=new Thickness(1),Effect=new DropShadowEffect{BlurRadius=22,ShadowDepth=3,Opacity=.3,Color=Colors.Black}};
        root.Children.Add(surface);
        var body=new StackPanel{Width=Math.Max(120,width-116),Margin=new Thickness(40,19,40,24)};
        body.Children.Add(new HeadlineText(Headline(outcome),Accent(outcome)){Height=76,Margin=new Thickness(0,0,0,3)});
        body.Children.Add(new Border{Height=1,Width=Math.Min(590,width-140),Background=new SolidColorBrush(Color.FromArgb(86,255,255,255)),Margin=new Thickness(0,0,0,10)});
        TextBlock Add(string text,double size,Brush brush,bool bold=false)
        {
            var label=new TextBlock{Text=text,FontFamily=new FontFamily("Segoe UI, Microsoft JhengHei UI"),FontSize=size,FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,Foreground=brush,TextAlignment=TextAlignment.Center,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,3,0,3)};
            body.Children.Add(label);return label;
        }
        Add(title,20,Brushes.White,true);
        if(sizeChange.Length>0)Add(sizeChange,15,Brushes.WhiteSmoke);
        if(detail.Length>0)Add(detail,14,new SolidColorBrush(Color.FromRgb(223,229,237)));
        stars=null;
        if(after!=null)
        {
            if(after.CanRate)stars=Add(Stars(after.Level),27,Brushes.White);
            Add(L.Text("輸出檔案 · 通緝星級","Output file · wanted level")+" · "+after.StatusText,12,Brushes.WhiteSmoke);
            Add(after.Detail,12,new SolidColorBrush(Color.FromRgb(200,209,222)));
        }
        // Preserve full paths/messages; unusually tall content scales down to the work area.
        var fit=new Viewbox{Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,Child=body,MaxHeight=Math.Max(100,maxHeight-36),Margin=new Thickness(18)};
        root.Children.Add(fit);return root;
    }
    sealed class HeadlineText(string text,Brush color):FrameworkElement
    {
        protected override void OnRender(DrawingContext dc)
        {
            var formatted=new FormattedText(text,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Impact"),66,color,VisualTreeHelper.GetDpi(this).PixelsPerDip);
            double scale=Math.Min(1,Math.Max(1,ActualWidth-8)/formatted.WidthIncludingTrailingWhitespace);
            var shape=formatted.BuildGeometry(new Point());
            dc.PushTransform(new TranslateTransform((ActualWidth-formatted.WidthIncludingTrailingWhitespace*scale)/2,(ActualHeight-formatted.Height*scale)/2));
            dc.PushTransform(new ScaleTransform(scale,scale));
            dc.PushTransform(new TranslateTransform(1,3));dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(150,0,0,0)),new Pen(Brushes.Black,5),shape);dc.Pop();
            dc.DrawGeometry(color,new Pen(Brushes.Black,3.5){LineJoin=PenLineJoin.Round},shape);dc.DrawGeometry(color,null,shape);
            dc.Pop();dc.Pop();
        }
    }
    static BitmapSource Capture(Rect bounds)
    {
        DwmFlush(); // Finish hiding the progress window before sampling its backdrop.
        using var capture=new System.Drawing.Bitmap((int)bounds.Width,(int)bounds.Height);
        using(var graphics=System.Drawing.Graphics.FromImage(capture))graphics.CopyFromScreen((int)bounds.X,(int)bounds.Y,0,0,capture.Size);
        var bitmap=capture.GetHbitmap();
        try{var source=Imaging.CreateBitmapSourceFromHBitmap(bitmap,IntPtr.Zero,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());source.Freeze();return source;}
        finally{DeleteObject(bitmap);}
    }
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr handle);
    [DllImport("dwmapi.dll")] static extern int DwmFlush();
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(IntPtr handle);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] internal static extern IntPtr GetWindowLongPtr(IntPtr handle,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr handle,int index,IntPtr value);
}

// Scoped to the visible result. No coordinates or input history are collected.
internal sealed class BannerClickObserver : IDisposable
{
    readonly HookProc callback;
    IntPtr hook;
    internal bool IsInstalled=>hook!=IntPtr.Zero;
    internal BannerClickObserver(Action clicked)
    {
        callback=(code,message,data)=>
        {
            if(code>=0&&IsButtonDown(message.ToInt64()))clicked();
            return CallNextHookEx(IntPtr.Zero,code,message,data);
        };
        hook=SetWindowsHookEx(14,callback,GetModuleHandle(null),0); // WH_MOUSE_LL
        if(hook==IntPtr.Zero)Output.Log("Result click observer unavailable: "+Marshal.GetLastWin32Error());
    }
    internal static bool IsButtonDown(long message)=>message is 0x0201 or 0x0204 or 0x0207 or 0x020B;
    public void Dispose()
    {
        if(hook==IntPtr.Zero)return;
        UnhookWindowsHookEx(hook);hook=IntPtr.Zero;
        GC.KeepAlive(callback);
    }
    delegate IntPtr HookProc(int code,IntPtr message,IntPtr data);
    [DllImport("user32.dll",SetLastError=true)] static extern IntPtr SetWindowsHookEx(int kind,HookProc callback,IntPtr module,uint thread);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr GetModuleHandle(string? name);
}
