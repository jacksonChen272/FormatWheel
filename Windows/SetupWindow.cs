using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FormatWheel;

public static class Ui
{
    public static readonly Brush Background=new SolidColorBrush(Color.FromRgb(17,22,32));
    public static readonly Brush Card=new SolidColorBrush(Color.FromRgb(29,37,51));
    public static readonly Brush Muted=new SolidColorBrush(Color.FromRgb(177,191,211));
    public static readonly Brush Blue=new SolidColorBrush(Color.FromRgb(79,155,255));
    public static TextBlock Text(string text,double size=15,Brush? color=null)=>new(){Text=text,FontSize=size,Foreground=color??Brushes.White,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12),LineHeight=size*1.45};
    public static Button Button(string text,Action action,bool primary=false){var b=new Button{Content=text,Padding=new Thickness(18,10,18,10),Margin=new Thickness(0,6,10,6),FontSize=14,Background=primary?Blue:Card,Foreground=Brushes.White,BorderThickness=new Thickness(0),MinHeight=42};b.Click+=(_,_)=>action();return b;}
    public static void Window(Window w,string title,double width,double height)
    {
        w.Title=title;w.Width=width;w.Height=height;w.MinWidth=Math.Min(width,560);w.MinHeight=420;w.MaxWidth=SystemParameters.WorkArea.Width;w.MaxHeight=SystemParameters.WorkArea.Height;w.WindowStartupLocation=WindowStartupLocation.CenterScreen;w.Background=Background;w.Foreground=Brushes.White;w.FontFamily=new FontFamily("Segoe UI, Microsoft JhengHei UI");w.UseLayoutRounding=true;w.SnapsToDevicePixels=true;TextOptions.SetTextFormattingMode(w,TextFormattingMode.Display);
        w.Resources=(ResourceDictionary)System.Windows.Markup.XamlReader.Parse("""
        <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
          <Style TargetType="Button"><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button">
            <Border x:Name="frame" Background="{TemplateBinding Background}" CornerRadius="7" Padding="{TemplateBinding Padding}" BorderBrush="#83B7FF" BorderThickness="0"><ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" RecognizesAccessKey="True"/></Border>
            <ControlTemplate.Triggers><Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.4"/></Trigger><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="frame" Property="Opacity" Value="0.85"/></Trigger><Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="frame" Property="BorderThickness" Value="2"/></Trigger></ControlTemplate.Triggers>
          </ControlTemplate></Setter.Value></Setter></Style>
          <Style TargetType="CheckBox"><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="CheckBox"><Grid>
            <Border Width="22" Height="22" HorizontalAlignment="Left" VerticalAlignment="Top" Background="#25354D" BorderBrush="#7893B8" BorderThickness="1" CornerRadius="4"><TextBlock x:Name="tick" Text="✓" Foreground="White" FontSize="16" HorizontalAlignment="Center" VerticalAlignment="Center" Visibility="Hidden"/></Border>
            <ContentPresenter Margin="34,0,0,0" RecognizesAccessKey="True"/>
          </Grid><ControlTemplate.Triggers><Trigger Property="IsChecked" Value="True"><Setter TargetName="tick" Property="Visibility" Value="Visible"/></Trigger><Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.55"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
        </ResourceDictionary>
        """);
    }
    public static ComboBox Languages(Action changed)
    {
        var c=new ComboBox{ItemsSource=new[]{"繁體中文","简体中文","English"},SelectedIndex=L.Language switch{"zh-TW"=>0,"zh-CN"=>1,_=>2},MinWidth=145,FontSize=14,Padding=new Thickness(8),HorizontalAlignment=HorizontalAlignment.Right};
        c.SelectionChanged+=(_,_)=>{Settings.Current.Language=new[]{"zh-TW","zh-CN","en"}[c.SelectedIndex];Settings.Current.Save();changed();};return c;
    }
}

public sealed class SetupWindow:Window
{
    CheckBox ff=null!,lo=null!;TextBlock status=null!;ProgressBar progress=null!;Button install=null!,close=null!;ComboBox language=null!;
    CancellationTokenSource? cancellation;bool busy,canCancel=true;
    bool ffReady,loReady;
    public SetupWindow()
    {
        Ui.Window(this,"FormatWheel",700,720);Render();Loaded+=async(_,_)=>await Refresh();
        Closing+=(_,e)=>{if(busy){e.Cancel=true;if(canCancel)cancellation?.Cancel();}};
    }
    void Render()
    {
        Title=L.Text("FormatWheel · 安裝轉換工具","FormatWheel · Conversion tools");
        var p=new StackPanel{Margin=new Thickness(30)};
        language=Ui.Languages(Render);p.Children.Add(language);
        p.Children.Add(Ui.Text(L.Text("解鎖影音與文件轉換","Add media & document conversion"),27));
        p.Children.Add(Ui.Text(L.Text("圖片、PDF 與文字工具已可使用。需要影音或完整文件轉換時，再安裝以下免費工具。","Images, PDFs and text tools are ready. Add these free tools for media and full document conversion."),15,Ui.Muted));
        p.Children.Add(Option("FFmpeg · 9.0.2",L.Text("影片／音訊轉換、壓縮、靜音。下載約 110 MB。","Video/audio conversion, compression and mute. About 110 MB."),ffReady,out ff));
        p.Children.Add(Option("LibreOffice · 26.8.1",L.Text("Word、RTF、ODT 與 PDF 文件轉換。下載約 358 MB。","Word, RTF, ODT and PDF document conversion. About 358 MB."),loReady,out lo));
        p.Children.Add(Ui.Text(L.Text("點擊安裝後會連線至 Gyan.dev／LibreOffice 官方下載站及鏡像，自動校驗與安裝到此使用者的資料夾。轉換過程留在本機。建議保留 3 GB 空間。","Install downloads from Gyan.dev / LibreOffice's official download service and mirrors, verifies the files and installs privately for this user. Conversions stay on your PC. Allow 3 GB of free space."),13,Ui.Muted));
        status=Ui.Text(L.Text("正在檢查已安裝的工具…","Checking existing tools…"),14);p.Children.Add(status);
        progress=new ProgressBar{Height=5,Minimum=0,Maximum=100,Margin=new Thickness(0,0,0,12)};p.Children.Add(progress);
        var row=new WrapPanel();install=Ui.Button(L.Text("安裝所選工具","Install selected tools"),()=>_=Install(),true);install.IsEnabled=false;row.Children.Add(install);
        close=Ui.Button(L.Text("暫時略過","Maybe later"),()=>{if(busy){if(canCancel)cancellation?.Cancel();}else Close();});row.Children.Add(close);p.Children.Add(row);
        p.Children.Add(Ui.Text(L.Text("之後可右鍵點擊系統匣圖示 → 安裝轉換工具。","Later: right-click the system tray icon → Conversion tools."),13,Ui.Muted));
        Content=new ScrollViewer{Content=p,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        if(IsLoaded)_=Refresh();
    }
    static Border Option(string title,string detail,bool ready,out CheckBox check)
    {
        check=new CheckBox{Content=title+(ready?" ✓":""),FontSize=18,IsChecked=!ready,IsEnabled=!ready,Foreground=Brushes.White,Margin=new Thickness(0,0,0,10)};
        var p=new StackPanel();p.Children.Add(check);var description=Ui.Text(detail,13,Ui.Muted);description.Margin=new Thickness(34,0,0,0);p.Children.Add(description);
        return new Border{Child=p,Background=Ui.Card,CornerRadius=new CornerRadius(10),Padding=new Thickness(16),Margin=new Thickness(0,4,0,12)};
    }
    async Task Refresh()
    {
        var a=Dependencies.Ready(Dependencies.FFmpeg);var b=Dependencies.Ready(Dependencies.LibreOffice);await Task.WhenAll(a,b);ffReady=a.Result;loReady=b.Result;
        ff.IsEnabled=!ffReady;ff.IsChecked=true;lo.IsEnabled=!loReady;lo.IsChecked=true;
        ff.Content="FFmpeg · "+(ffReady?L.Text("已可使用 ✓","Ready ✓"):Dependencies.FFmpeg.Version);
        lo.Content="LibreOffice · "+(loReady?L.Text("已可使用 ✓","Ready ✓"):Dependencies.LibreOffice.Version);
        status.Text=ffReady&&loReady?L.Text("兩項工具都已可使用。","Both tools are ready."):L.Text("未安裝的工具已勾選，可自行取消。","Missing tools are selected. You can uncheck either one.");install.IsEnabled=!(ffReady&&loReady);
        if(ffReady&&loReady)close.Content=L.Text("完成","Done");
    }
    async Task Install()
    {
        bool useFf=ff.IsChecked==true&&!ffReady,useLo=lo.IsChecked==true&&!loReady;if(!useFf&&!useLo){status.Text=L.Text("請先勾選需要的工具。","Select at least one tool.");return;}
        busy=true;cancellation=new();install.IsEnabled=false;ff.IsEnabled=lo.IsEnabled=language.IsEnabled=false;close.Content=L.Text("取消下載","Cancel download");
        var feedback=new Progress<InstallProgress>(p=>{status.Text=p.Message;progress.IsIndeterminate=p.Percent==null;if(p.Percent is double n)progress.Value=n;canCancel=p.CanCancel;close.IsEnabled=canCancel;});
        string? error=null;
        try{if(useFf)await Dependencies.Install(Dependencies.FFmpeg,feedback,cancellation.Token);if(useLo)await Dependencies.Install(Dependencies.LibreOffice,feedback,cancellation.Token);}
        catch(OperationCanceledException){error=L.Text("下載已取消。可隨時重試。","Download canceled. You can retry at any time.");}
        catch(Exception e){Output.Log(e.ToString());error=L.Text("未完成安裝：","Installation did not complete: ")+e.Message;}
        finally{busy=false;cancellation.Dispose();cancellation=null;language.IsEnabled=close.IsEnabled=true;progress.IsIndeterminate=false;close.Content=L.Text("關閉","Close");await Refresh();if(error!=null)status.Text=error;else status.Text=L.Text("所選工具已安裝並通過執行檢查，可立即使用。","Selected tools are installed, verified and ready to use.");}
    }
}
