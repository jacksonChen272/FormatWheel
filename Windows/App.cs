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
    SetupWindow? setup;
    TourWindow? tour;
    Window? home;
    CancellationTokenSource? job;
    DispatcherTimer watcher=new(){Interval=TimeSpan.FromMilliseconds(16)};
    readonly DragGesture gesture=new();
    [STAThread] public static int Main(string[] args)
    {
        var app=new App{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        if(args.FirstOrDefault()=="--ids"){File.WriteAllText(args.Length>1?args[1]:"actions.json",JsonSerializer.Serialize(Catalog.All.Values,new JsonSerializerOptions{WriteIndented=true}));return 0;}
        if(args.FirstOrDefault()=="--snapshot"){app.Snapshot(args[1],args.Contains("--weapons"));return 0;}
        if(args.FirstOrDefault()=="--self-test"||args.FirstOrDefault()=="--run"||args.FirstOrDefault()=="--install-test")
        {
            int code=0;app.Startup+=async(_,_)=>{try{if(args[0]=="--self-test")await SelfTest.Run(args[1]);else if(args[0]=="--install-test"){var d=args[1]=="ffmpeg"?Dependencies.FFmpeg:Dependencies.LibreOffice;await Dependencies.Install(d,new Progress<InstallProgress>(p=>Output.Log(p.Message)),verifiedPackage:args.Length>2?args[2]:null);}else {var r=await Engine.Run(args[1],args.Skip(2).ToArray());File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"last-result.json"),JsonSerializer.Serialize(r));}}catch(Exception ex){code=1;File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"last-error.txt"),ex.ToString());}app.Shutdown(code);};app.Run();return code;
        }
        using var mutex=new Mutex(true,"Local\\FormatWheel.Windows",out bool first);if(!first){MessageBox.Show(L.T("FormatWheel 已在系統匣執行。請從右下角圖示開啟。"),"FormatWheel");return 0;}
        app.DispatcherUnhandledException+=(_,e)=>{Output.Log(e.Exception.ToString());MessageBox.Show(e.Exception.Message,"FormatWheel");e.Handled=true;};
        app.Startup+=(_,_)=>app.Start(args);app.Run();return 0;
    }
    void Start(string[] args)
    {
        tray=new Forms.NotifyIcon{Text=L.T("FormatWheel · 格式輪盤"),Icon=System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!)??System.Drawing.SystemIcons.Application,Visible=true};
        tray.ContextMenuStrip=new Forms.ContextMenuStrip();tray.ContextMenuStrip.Opening+=(_,_)=>BuildMenu();tray.DoubleClick+=(_,_)=>Pick();BuildMenu();
        watcher.Tick+=(_,_)=>{if(gesture.Update(Native.Down(1),Native.Down(0x10),Forms.Cursor.Position)&&wheel==null&&job==null&&setup==null&&tour==null)Open([],true);};watcher.Start();
        Exit+=(_,_)=>{watcher.Stop();tray.Dispose();};
        var files=args.Where(File.Exists).ToArray();
        if(!Settings.Current.SetupPromptSeen){ShowSetup();setup!.Closed+=(_,_)=>{Settings.Current.SetupPromptSeen=true;Settings.Current.Save();FirstTour(files);};}
        else if(!Settings.Current.TourSeen)FirstTour(files);
        else if(files.Length>0)Open(files,false);else if(!args.Contains("--tray"))Welcome();
    }
    void BuildMenu()
    {
        var m=tray.ContextMenuStrip!.Items;m.Clear();void Add(string text,Action action){m.Add(L.T(text),null,(_,_)=>action());}
        Add(L.T("選擇檔案開啟輪盤…"),Pick);Add(L.T("預覽輪盤"),()=>Open([],false,true));
        var recent=new Forms.ToolStripMenuItem(L.T("最近轉換"));foreach(var f in Settings.Current.Recents.Where(File.Exists).Take(10))recent.DropDownItems.Add(Path.GetFileName(f),null,(_,_)=>Process.Start(new ProcessStartInfo("explorer.exe"){ArgumentList={"/select,",f},UseShellExecute=true}));m.Add(recent);
        m.Add(new Forms.ToolStripSeparator());
        Add(L.T("格子圖示：")+(Settings.Current.Weapons?L.T("武器"):L.T("格式")),()=>{Settings.Current.Weapons=!Settings.Current.Weapons;Settings.Current.Save();});
        var colors=new Forms.ToolStripMenuItem(L.T("角色配色"));string[] names=[L.T("線上模式 · 藍"),L.T("麥克 · 天藍"),L.T("富蘭克林 · 綠"),L.T("崔佛 · 橙")];string[] hex=["#3380E6","#66B5D4","#6BD173","#FF9947"];for(int i=0;i<4;i++){string h=hex[i];colors.DropDownItems.Add(names[i],null,(_,_)=>{Settings.Current.Color=h;Settings.Current.Save();});}m.Add(colors);
        var sizes=new Forms.ToolStripMenuItem(L.T("輪盤大小"));foreach(int s in new[]{235,285,330})sizes.DropDownItems.Add(s.ToString(),null,(_,_)=>{Settings.Current.Size=s;Settings.Current.Save();});m.Add(sizes);
        Add(L.T("音效：")+(Settings.Current.Sound?L.T("開"):L.T("關")),()=>{Settings.Current.Sound=!Settings.Current.Sound;Settings.Current.Save();});
        Add(L.T("背景模糊：")+(Settings.Current.Blur?L.T("開"):L.T("關")),()=>{Settings.Current.Blur=!Settings.Current.Blur;Settings.Current.Save();});
        Add(L.T("完成橫幅：")+(Settings.Current.Banner?L.T("開"):L.T("關")),()=>{Settings.Current.Banner=!Settings.Current.Banner;Settings.Current.Save();});
        Add(L.T("登入啟動：")+(Settings.AutoStart?L.T("開"):L.T("關")),()=>Settings.AutoStart=!Settings.AutoStart);
        m.Add(new Forms.ToolStripSeparator());
        Add(L.Text("安裝轉換工具…","Conversion tools…"),ShowSetup);
        var languages=new Forms.ToolStripMenuItem("Language / 語言 / 语言");string[] codes=["zh-TW","zh-CN","en"],labels=["繁體中文","简体中文","English"];for(int i=0;i<codes.Length;i++){string code=codes[i];var item=new Forms.ToolStripMenuItem(labels[i]){Checked=L.Language==code};item.Click+=(_,_)=>{Settings.Current.Language=code;Settings.Current.Save();home?.Close();home=null;Welcome();};languages.DropDownItems.Add(item);}m.Add(languages);
        Add(L.Text("重播新手教學","Replay quick tour"),ShowTour);Add(L.Text("下次啟動自動播放教學","Show tour on next launch"),()=>{Settings.Current.TourSeen=false;Settings.Current.Save();});
        Add(L.Text("開源授權與來源","Licenses and source"),()=>Process.Start(new ProcessStartInfo(Assets.Notices()){UseShellExecute=true}));
        Add(L.T("使用說明 / 外部工具"),Welcome);Add(L.T("開啟記錄資料夾"),()=>{Directory.CreateDirectory(Settings.Folder);Process.Start(new ProcessStartInfo(Settings.Folder){UseShellExecute=true});});
        if(job!=null)Add(L.T("取消目前轉換"),()=>job.Cancel());Add(L.T("結束"),()=>{job?.Cancel();Shutdown();});
    }
    void Pick(){using var dialog=new Forms.OpenFileDialog{Multiselect=true,Title=L.T("選擇要轉換的檔案")};if(dialog.ShowDialog()==Forms.DialogResult.OK)Open(dialog.FileNames,false);}
    void Open(string[] files,bool drag,bool demo=false){if(wheel!=null)return;Output.Log($"wheel open files={files.Length} drag={drag}");wheel=new WheelWindow(files,drag,Execute,demo);wheel.Loaded+=(_,_)=>Output.Log($"wheel loaded visible={wheel?.IsVisible}");wheel.Closed+=(_,_)=>{Output.Log("wheel closed");wheel=null;};wheel.Show();if(!drag)wheel.Activate();}
    async void Execute(ActionItem action,string[] files)
    {
        if(job!=null)return;job=new CancellationTokenSource();var busy=Banner.Busy(action.Label,()=>job?.Cancel());
        try{var r=await Task.Run(()=>Engine.Run(action.Id,files,new Progress<string>(s=>Dispatcher.Invoke(()=>busy.Title=s)),job.Token));if(r.Clipboard!=null)Clipboard.SetText(r.Clipboard);Settings.Current.Recents=r.Files.Concat(Settings.Current.Recents).Distinct().Take(20).ToList();Settings.Current.Save();Sound.Play(action.Id);if(Settings.Current.Banner)Banner.Show(true,action.Label,r.Message+(r.Files.Count>0?L.T("\n儲存在：")+Path.GetDirectoryName(r.Files[0]):""));}
        catch(OperationCanceledException){Banner.Show(false,L.T("已取消"),L.T("已完成的輸出檔案會保留"));}
        catch(Exception ex){Output.Log(ex.ToString());Sound.Play("failed");Banner.Show(false,action.Label,ex.Message);}
        finally{busy.Close();job.Dispose();job=null;}
    }
    void ShowSetup(){if(setup!=null){setup.Activate();return;}setup=new SetupWindow();setup.Closed+=(_,_)=>setup=null;setup.Show();}
    void ShowTour(){if(tour!=null){tour.Activate();return;}tour=new TourWindow();tour.Closed+=(_,_)=>tour=null;tour.Show();}
    void FirstTour(string[] files){ShowTour();tour!.Closed+=(_,_)=>{if(files.Length>0)Open(files,false);else Welcome();};}
    void Welcome()
    {
        if(home!=null){home.Activate();return;}var w=new Window();home=w;w.Closed+=(_,_)=>home=null;Ui.Window(w,"FormatWheel for Windows",660,570);
        void Render(){var panel=new StackPanel{Margin=new Thickness(32)};panel.Children.Add(Ui.Languages(Render));panel.Children.Add(Ui.Text("FormatWheel",38));panel.Children.Add(Ui.Text(L.Text("拖住檔案 → 按 Shift → 選擇方向 → 放手","Drag a file → press Shift → choose a direction → release"),19));panel.Children.Add(Ui.Text(L.Text("圖片、PDF、文字與離線去背已內建。原檔保留，結果儲存在來源旁邊。關閉此視窗後，程式會留在系統匣。","Image, PDF, text and offline background-removal tools are built in. Originals are preserved; results are saved beside them. Closing this window keeps FormatWheel in the system tray."),15,Ui.Muted));
        panel.Children.Add(Ui.Button(L.Text("選擇檔案，開始轉換","Choose files to convert"),()=>{Pick();w.Close();},true));var row=new WrapPanel();row.Children.Add(Ui.Button(L.Text("安裝轉換工具","Conversion tools"),ShowSetup));row.Children.Add(Ui.Button(L.Text("新手教學","Quick tour"),ShowTour));panel.Children.Add(row);
        panel.Children.Add(Ui.Text(L.Text("Shift / Tab / 滾輪：翻頁　中心 / Esc：取消\n右鍵點擊系統匣圖示：工具、語言及設定","Shift / Tab / scroll: change page · Center / Esc: cancel\nRight-click the system tray icon for tools, language and settings"),13,Ui.Muted));panel.Children.Add(Ui.Text(L.Text("Windows 社群移植版 · 原作 Elise616/FormatWheel · MIT\n此版本由 rickuuyu-create 維護，非原作者官方 Windows 發行。","Windows community port · Original: Elise616/FormatWheel · MIT\nMaintained by rickuuyu-create; not an official upstream Windows release."),12,Ui.Muted));w.Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        var menu=new ContextMenu();void Item(string label,Action action){var item=new MenuItem{Header=label};item.Click+=(_,_)=>action();menu.Items.Add(item);}Item(L.Text("安裝轉換工具","Conversion tools"),ShowSetup);Item(L.Text("重播新手教學","Replay quick tour"),ShowTour);w.ContextMenu=menu;}
        Render();w.Show();
    }
    void Snapshot(string path,bool weapons)
    {
        Settings.Current.Weapons=weapons;var v=new WheelCanvas{Width=1440,Height=960,Files=["Sunset Photo.jpg"],Hover=4,Center=new Point(720,480),Radius=285,Demo=true};
        var grid=new Grid{Width=1440,Height=960,Background=new LinearGradientBrush(Color.FromRgb(67,82,107),Color.FromRgb(25,49,37),60)};grid.Children.Add(v);grid.Measure(new Size(1440,960));grid.Arrange(new Rect(0,0,1440,960));grid.UpdateLayout();var bitmap=new RenderTargetBitmap(1440,960,96,96,PixelFormats.Pbgra32);bitmap.Render(grid);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(path);png.Save(file);
    }
}
public static class Banner
{
    public static Window Busy(string title,Action cancel){var w=new Window{Title=title,Width=380,Height=130,Topmost=true,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterScreen};var stack=new StackPanel{Margin=new Thickness(16)};stack.Children.Add(new TextBlock{Text=L.T("正在轉換 · ")+title});stack.Children.Add(new ProgressBar{IsIndeterminate=true,Height=5,Margin=new Thickness(0,12,0,12)});var b=new Button{Content=L.T("取消")};b.Click+=(_,_)=>cancel();stack.Children.Add(b);w.Content=stack;w.Closing+=(_,_)=>cancel();w.Show();return w;}
    public static void Show(bool passed,string title,string detail)
    {
        var w=new Window{Title=title,Width=650,Height=240,WindowStyle=WindowStyle.None,AllowsTransparency=true,Background=new SolidColorBrush(Color.FromArgb(235,10,12,15)),Foreground=Brushes.White,Topmost=true,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.CenterScreen};var s=new StackPanel{Margin=new Thickness(24)};
        s.Children.Add(new TextBlock{Text=passed?"MISSION PASSED":"WASTED",FontFamily=new FontFamily("Impact"),FontSize=52,Foreground=passed?Brushes.Gold:Brushes.IndianRed,HorizontalAlignment=HorizontalAlignment.Center});s.Children.Add(new TextBlock{Text=title,FontSize=18,TextAlignment=TextAlignment.Center});s.Children.Add(new TextBlock{Text=detail,FontSize=13,Margin=new Thickness(0,12,0,0),TextWrapping=TextWrapping.Wrap,TextTrimming=TextTrimming.CharacterEllipsis,MaxHeight=85,TextAlignment=TextAlignment.Center});w.Content=s;w.MouseDown+=(_,_)=>w.Close();w.Show();var t=new DispatcherTimer{Interval=TimeSpan.FromSeconds(passed?4:9)};t.Tick+=(_,_)=>{t.Stop();w.Close();};w.Closed+=(_,_)=>t.Stop();t.Start();
    }
}
