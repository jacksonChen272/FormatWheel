using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using System.Text.Json;
using System.Diagnostics;
using Forms=System.Windows.Forms;

namespace FormatWheel;
public class App : Application
{
    Forms.NotifyIcon tray=null!;
    WheelWindow? wheel;
    CancellationTokenSource? job;
    DispatcherTimer watcher=new(){Interval=TimeSpan.FromMilliseconds(16)};
    bool lastShift,wasLeft,dragged;
    System.Drawing.Point down;
    [STAThread] public static int Main(string[] args)
    {
        var app=new App{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        if(args.FirstOrDefault()=="--ids"){File.WriteAllText(args.Length>1?args[1]:"actions.json",JsonSerializer.Serialize(Catalog.All.Values,new JsonSerializerOptions{WriteIndented=true}));return 0;}
        if(args.FirstOrDefault()=="--snapshot"){app.Snapshot(args[1],args.Contains("--weapons"));return 0;}
        if(args.FirstOrDefault()=="--self-test"||args.FirstOrDefault()=="--run")
        {
            int code=0;app.Startup+=async(_,_)=>{try{if(args[0]=="--self-test")await SelfTest.Run(args[1]);else {var r=await Engine.Run(args[1],args.Skip(2).ToArray());File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"last-result.json"),JsonSerializer.Serialize(r));}}catch(Exception ex){code=1;File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"last-error.txt"),ex.ToString());}app.Shutdown(code);};app.Run();return code;
        }
        using var mutex=new Mutex(true,"Local\\FormatWheel.Windows",out bool first);if(!first){MessageBox.Show("FormatWheel 已在系統匣執行。請從右下角圖示開啟。","FormatWheel");return 0;}
        app.DispatcherUnhandledException+=(_,e)=>{Output.Log(e.Exception.ToString());MessageBox.Show(e.Exception.Message,"FormatWheel");e.Handled=true;};
        app.Startup+=(_,_)=>app.Start(args);app.Run();return 0;
    }
    void Start(string[] args)
    {
        tray=new Forms.NotifyIcon{Text="FormatWheel · 格式輪盤",Icon=System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!)??System.Drawing.SystemIcons.Application,Visible=true};
        tray.ContextMenuStrip=new Forms.ContextMenuStrip();tray.ContextMenuStrip.Opening+=(_,_)=>BuildMenu();tray.DoubleClick+=(_,_)=>Pick();BuildMenu();
        watcher.Tick+=(_,_)=>{bool left=Native.Down(1),shift=Native.Down(0x10);var pos=Forms.Cursor.Position;if(left&&!wasLeft){down=pos;dragged=false;}if(left&&(Math.Abs(pos.X-down.X)>6||Math.Abs(pos.Y-down.Y)>6))dragged=true;if(left&&dragged&&shift&&!lastShift&&wheel==null&&job==null)Open([],true);if(!left)dragged=false;wasLeft=left;lastShift=shift;};watcher.Start();
        Exit+=(_,_)=>{watcher.Stop();tray.Dispose();};
        var files=args.Where(File.Exists).ToArray();if(files.Length>0)Open(files,false);else if(!args.Contains("--tray"))Welcome();
    }
    void BuildMenu()
    {
        var m=tray.ContextMenuStrip!.Items;m.Clear();void Add(string text,Action action){m.Add(text,null,(_,_)=>action());}
        Add("選擇檔案開啟輪盤…",Pick);Add("預覽輪盤",()=>Open([],false,true));
        var recent=new Forms.ToolStripMenuItem("最近轉換");foreach(var f in Settings.Current.Recents.Where(File.Exists).Take(10))recent.DropDownItems.Add(Path.GetFileName(f),null,(_,_)=>Process.Start(new ProcessStartInfo("explorer.exe"){ArgumentList={"/select,",f},UseShellExecute=true}));m.Add(recent);
        m.Add(new Forms.ToolStripSeparator());
        Add("格子圖示："+(Settings.Current.Weapons?"武器":"格式"),()=>{Settings.Current.Weapons=!Settings.Current.Weapons;Settings.Current.Save();});
        var colors=new Forms.ToolStripMenuItem("角色配色");string[] names=["線上模式 · 藍","麥克 · 天藍","富蘭克林 · 綠","崔佛 · 橙"];string[] hex=["#3380E6","#66B5D4","#6BD173","#FF9947"];for(int i=0;i<4;i++){string h=hex[i];colors.DropDownItems.Add(names[i],null,(_,_)=>{Settings.Current.Color=h;Settings.Current.Save();});}m.Add(colors);
        var sizes=new Forms.ToolStripMenuItem("輪盤大小");foreach(int s in new[]{235,285,330})sizes.DropDownItems.Add(s.ToString(),null,(_,_)=>{Settings.Current.Size=s;Settings.Current.Save();});m.Add(sizes);
        Add("音效："+(Settings.Current.Sound?"開":"關"),()=>{Settings.Current.Sound=!Settings.Current.Sound;Settings.Current.Save();});
        Add("背景模糊："+(Settings.Current.Blur?"開":"關"),()=>{Settings.Current.Blur=!Settings.Current.Blur;Settings.Current.Save();});
        Add("完成橫幅："+(Settings.Current.Banner?"開":"關"),()=>{Settings.Current.Banner=!Settings.Current.Banner;Settings.Current.Save();});
        Add("登入啟動："+(Settings.AutoStart?"開":"關"),()=>Settings.AutoStart=!Settings.AutoStart);
        m.Add(new Forms.ToolStripSeparator());Add("使用說明 / 外部工具",Welcome);Add("開啟記錄資料夾",()=>{Directory.CreateDirectory(Settings.Folder);Process.Start(new ProcessStartInfo(Settings.Folder){UseShellExecute=true});});
        if(job!=null)Add("取消目前轉換",()=>job.Cancel());Add("結束",()=>{job?.Cancel();Shutdown();});
    }
    void Pick(){using var dialog=new Forms.OpenFileDialog{Multiselect=true,Title="選擇要轉換的檔案"};if(dialog.ShowDialog()==Forms.DialogResult.OK)Open(dialog.FileNames,false);}
    void Open(string[] files,bool drag,bool demo=false){if(wheel!=null)return;Output.Log($"wheel open files={files.Length} drag={drag}");wheel=new WheelWindow(files,drag,Execute,demo);wheel.Loaded+=(_,_)=>Output.Log($"wheel loaded visible={wheel?.IsVisible}");wheel.Closed+=(_,_)=>{Output.Log("wheel closed");wheel=null;};wheel.Show();if(!drag)wheel.Activate();Sound.Play("open");}
    async void Execute(ActionItem action,string[] files)
    {
        if(job!=null)return;job=new CancellationTokenSource();var busy=Banner.Busy(action.Label,()=>job?.Cancel());
        try{var r=await Task.Run(()=>Engine.Run(action.Id,files,new Progress<string>(s=>Dispatcher.Invoke(()=>busy.Title=s)),job.Token));if(r.Clipboard!=null)Clipboard.SetText(r.Clipboard);Settings.Current.Recents=r.Files.Concat(Settings.Current.Recents).Distinct().Take(20).ToList();Settings.Current.Save();Sound.Play(action.Id);if(Settings.Current.Banner)Banner.Show(true,action.Label,r.Message+(r.Files.Count>0?"\n儲存在："+Path.GetDirectoryName(r.Files[0]):""));}
        catch(OperationCanceledException){Banner.Show(false,"已取消","已完成的輸出檔案會保留");}
        catch(Exception ex){Output.Log(ex.ToString());Sound.Play("failed");Banner.Show(false,action.Label,ex.Message);}
        finally{busy.Close();job.Dispose();job=null;}
    }
    void Welcome()
    {
        var w=new Window{Title="FormatWheel for Windows · 預覽版",Width=580,Height=480,WindowStartupLocation=WindowStartupLocation.CenterScreen,Background=new SolidColorBrush(Color.FromRgb(20,24,33)),Foreground=Brushes.White};
        var panel=new StackPanel{Margin=new Thickness(30)};panel.Children.Add(new TextBlock{Text="格式輪盤",FontSize=32,FontWeight=FontWeights.Bold});
        panel.Children.Add(new TextBlock{Text="拖住檔案 → 按一下 Shift → 朝格子方向拖 → 放手\n\nShift / Tab / 滾輪：翻頁　　中心 / Esc：取消\n也可以從系統匣選擇檔案。轉換結果存在來源旁邊，原檔不覆寫。\n\n圖片、PDF、文字工具已內建。\n影音轉換需要 FFmpeg；完整文件轉換需要 LibreOffice；去背已內建離線 U²-Net 模型。\n\n這是獨立 Windows 移植預覽版，非原作者官方 Windows 發行。\n原作：Elise616/FormatWheel · MIT License\n部分字型、圖示、轉換引擎與 macOS 有差異。",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,18,0,18),LineHeight=24});
        var button=new Button{Content="選擇檔案，開始轉換",Padding=new Thickness(15)};button.Click+=(_,_)=>{Pick();w.Close();};panel.Children.Add(button);w.Content=panel;w.Show();
    }
    void Snapshot(string path,bool weapons)
    {
        Settings.Current.Weapons=weapons;var v=new WheelCanvas{Width=1440,Height=960,Files=["Sunset Photo.jpg"],Hover=4,Center=new Point(720,480),Radius=285,Demo=true};
        var grid=new Grid{Width=1440,Height=960,Background=new LinearGradientBrush(Color.FromRgb(67,82,107),Color.FromRgb(25,49,37),60)};grid.Children.Add(v);grid.Measure(new Size(1440,960));grid.Arrange(new Rect(0,0,1440,960));grid.UpdateLayout();var bitmap=new RenderTargetBitmap(1440,960,96,96,PixelFormats.Pbgra32);bitmap.Render(grid);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(path);png.Save(file);
    }
}
public static class Banner
{
    public static Window Busy(string title,Action cancel){var w=new Window{Title=title,Width=380,Height=130,Topmost=true,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterScreen};var stack=new StackPanel{Margin=new Thickness(16)};stack.Children.Add(new TextBlock{Text="正在轉換 · "+title});stack.Children.Add(new ProgressBar{IsIndeterminate=true,Height=5,Margin=new Thickness(0,12,0,12)});var b=new Button{Content="取消"};b.Click+=(_,_)=>cancel();stack.Children.Add(b);w.Content=stack;w.Closing+=(_,_)=>cancel();w.Show();return w;}
    public static void Show(bool passed,string title,string detail)
    {
        var w=new Window{Title=title,Width=650,Height=240,WindowStyle=WindowStyle.None,AllowsTransparency=true,Background=new SolidColorBrush(Color.FromArgb(235,10,12,15)),Foreground=Brushes.White,Topmost=true,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.CenterScreen};var s=new StackPanel{Margin=new Thickness(24)};
        s.Children.Add(new TextBlock{Text=passed?"MISSION PASSED":"WASTED",FontFamily=new FontFamily("Impact"),FontSize=52,Foreground=passed?Brushes.Gold:Brushes.IndianRed,HorizontalAlignment=HorizontalAlignment.Center});s.Children.Add(new TextBlock{Text=title,FontSize=18,TextAlignment=TextAlignment.Center});s.Children.Add(new TextBlock{Text=detail,FontSize=13,Margin=new Thickness(0,12,0,0),TextWrapping=TextWrapping.Wrap,TextTrimming=TextTrimming.CharacterEllipsis,MaxHeight=85,TextAlignment=TextAlignment.Center});w.Content=s;w.MouseDown+=(_,_)=>w.Close();w.Show();var t=new DispatcherTimer{Interval=TimeSpan.FromSeconds(passed?4:9)};t.Tick+=(_,_)=>{t.Stop();w.Close();};w.Closed+=(_,_)=>t.Stop();t.Start();
    }
}
public static class Sound
{
    static System.Media.SoundPlayer? player;
    static MemoryStream? current;
    public static void Play(string kind)
    {
        if(!Settings.Current.Sound)return;
        try{const int rate=44100;double seconds=kind=="failed"?.65:kind.Contains('.')?.5:.12;int count=(int)(rate*seconds);var random=new Random(616);using var stream=new MemoryStream();using var w=new BinaryWriter(stream);w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+count*2);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(rate);w.Write(rate*2);w.Write((short)2);w.Write((short)16);w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(count*2);for(int i=0;i<count;i++){double t=i/(double)rate;double signal=kind=="failed"?Math.Sin(2*Math.PI*(180*t-70*t*t))*Math.Exp(-t*5):((random.NextDouble()*2-1)*Math.Exp(-t*85)+.35*Math.Sin(2*Math.PI*760*t)*Math.Exp(-t*22));if(kind.Contains('.')&&t>.14)signal+=.22*Math.Sin(2*Math.PI*523.25*(t-.14))*Math.Exp(-(t-.14)*9);w.Write((short)(Math.Clamp(signal,-1,1)*18000));}player?.Stop();player?.Dispose();current?.Dispose();current=new MemoryStream(stream.ToArray());player=new System.Media.SoundPlayer(current);player.Play();}catch{}
    }
}
