using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace FormatWheel;

public static class ResultBannerTests
{
    public static async Task Run(Func<string,Func<Task>,Task> check,string root)
    {
        void Assert(bool ok,string detail){if(!ok)throw new Exception(detail);}
        await check("banner-information-paths-and-monitor-placement",()=>
        {
            var previous=Settings.Current.Language;
            try
            {
                Settings.Current.Language="en";
                var a=Path.Combine(root,"first folder","sample.png");var b=Path.Combine(root,"second folder","sample.jpg");
                var text=ConversionFeedback.SavedLocations([a,b,a]);
                Assert(text.Contains(Path.GetDirectoryName(a)!)&&text.Contains(Path.GetDirectoryName(b)!)&&text.Split('\n').Length==2,"output folders lost or duplicated");
                Assert(ConversionFeedback.Title(Catalog.All["img.to.jpg"],[a]).Contains("sample.png → JPG"),"missing source/action");
                foreach(double dpi in new[]{1,1.25,1.5,2})foreach(var area in new[]{new Rect(0,0,1920,1040),new Rect(-1920,-300,1920,1040)})
                {
                    var p=FeedbackBanner.Place(area,dpi,new Size(1100,300));
                    Assert(area.Contains(p)&&Math.Abs((p.Left+p.Width/2)-(area.Left+area.Width/2))<=1,"banner outside working area or not centered");
                }
            }finally{Settings.Current.Language=previous;}
            return Task.CompletedTask;
        });
        await check("banner-three-language-render-and-full-path",()=>
        {
            var previous=Settings.Current.Language;
            try
            {
                foreach(var lang in new[]{"en","zh-TW","zh-CN"})foreach(var failure in new[]{false,true})
                {
                    Settings.Current.Language=lang;
                    string path=@"C:\Users\Example\Documents\FormatWheel conversion results\旅行照片與原始素材\高解像度影像輸出\2026-10-23";
                    string detail=failure?L.Text("轉換失敗：來源檔案無法讀取。請確認檔案是否完整，然後重試。","Conversion failed: the source file could not be read. Check that the file is complete and try again."):L.Text("完成 1 個檔案\n儲存在：","Completed 1 file\nSaved to: ")+path;
                    var card=FeedbackBanner.CreateCard(failure?FeedbackOutcome.Failure:FeedbackOutcome.Success,"Sunset Photo.png → HEIC",detail,failure?"":"1.43 MiB → 1.16 MiB · 18.5%",null,1000,640,out _);
                    card.Measure(new Size(1000,640));card.Arrange(new Rect(new Point(),card.DesiredSize));card.UpdateLayout();
                    Assert(card.ActualHeight<640&&card.ActualHeight>150,"invalid banner bounds");
                    var labels=Descendants(card).OfType<TextBlock>().ToArray();
                    Assert(labels.Any(x=>x.Text==detail)&&labels.All(x=>x.TextTrimming==TextTrimming.None),"message/path truncated");
                    Assert(!Descendants(card).OfType<Button>().Any(),"dismiss button added");
                    var image=new RenderTargetBitmap(1000,(int)Math.Ceiling(card.ActualHeight),96,96,PixelFormats.Pbgra32);image.Render(card);
                    var pixel=new byte[4];image.CopyPixels(new Int32Rect(0,0,1,1),pixel,4,0);Assert(pixel[3]==0,"square opaque corner");
                    var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using var file=File.Create(Path.Combine(root,$"banner-{lang}-{(failure?"failure":"success")}.png"));png.Save(file);
                }
            }finally{Settings.Current.Language=previous;}
            return Task.CompletedTask;
        });
        await check("banner-passive-window-and-automatic-lifetime",async()=>
        {
            foreach(var outcome in new[]{FeedbackOutcome.Success,FeedbackOutcome.Failure})
            {
                var banner=FeedbackBanner.ShowResult(outcome,"FormatWheel test",L.Text("自動驗證 · 不會改動檔案","Automatic validation · no files are changed"));
                try
                {
                    await Task.Delay(220);
                    long styles=FeedbackBanner.GetWindowLongPtr(new WindowInteropHelper(banner).Handle,-20).ToInt64();
                    Assert((styles&FeedbackBanner.PassiveStyles)==FeedbackBanner.PassiveStyles&&!banner.ShowActivated&&!banner.IsActive,"banner takes mouse/focus");
                    // Do not assume the user stayed idle: only reject this banner stealing foreground.
                    Assert(GetForegroundWindow()!=new WindowInteropHelper(banner).Handle,"banner stole foreground");
                    await Task.Delay(TimeSpan.FromSeconds(FeedbackBanner.Duration(outcome)+.2));
                    Assert(!banner.IsVisible&&banner.ClosedAutomatically,"banner required manual close");
                }
                finally{if(banner.IsVisible)banner.Close();}
            }
            Assert(FeedbackBanner.OpacityAt(FeedbackOutcome.Success,4.8,true) is >0 and <1,"no fade out");
            Assert(FeedbackBanner.OpacityAt(FeedbackOutcome.Failure,7.8,false)==1&&FeedbackBanner.OpacityAt(FeedbackOutcome.Failure,8,false)==0,"reduced animation preference ignored");
        });
    }
    static System.Collections.Generic.IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);yield return child;foreach(var nested in Descendants(child))yield return nested;}
    }
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();

    // Explicit developer preview; never displayed in normal use or during setup.
    public static void Preview()
    {
        var window=new Window{Title="FormatWheel · Banner preview",Width=1280,Height=800,Background=Brushes.Black};
        var root=new Grid();window.Content=root;
        var background=new Grid{Background=new LinearGradientBrush(Color.FromRgb(40,57,127),Color.FromRgb(20,132,93),35)};
        for(int i=0;i<11;i++)background.Children.Add(new Border{Width=140,Height=1000,Background=new SolidColorBrush(Color.FromArgb(100,(byte)(160+i*7),100,200)),HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(i*125-100,-100,0,0),RenderTransform=new RotateTransform(-25)});
        root.Children.Add(background);
        var display=new Grid{VerticalAlignment=VerticalAlignment.Center};root.Children.Add(display);
        var controls=new WrapPanel{VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(24)};root.Children.Add(controls);
        var info=new TextBlock{Text="Generated preview · no files are changed",Foreground=Brushes.White,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(24)};root.Children.Add(info);
        int clicks=0;
        window.MouseDown+=(_,e)=>{if(e.GetPosition(window).Y>150){info.Text=$"Underlying window clicks: {++clicks}";Output.Log($"preview underlying click {clicks}");}};
        void Render(bool failed)
        {
            display.Children.Clear();
            string detail=failed?L.Text("轉換失敗：來源檔案無法讀取。請確認檔案是否完整，然後重試。","Conversion failed: the source file could not be read. Check the file and try again."):L.Text("完成 1 個檔案\n儲存在：C:\\Users\\Example\\Downloads\\旅行照片與原始素材","Completed 1 file\nSaved to: C:\\Users\\Example\\Downloads\\Travel photos and originals");
            var card=FeedbackBanner.CreateCard(failed?FeedbackOutcome.Failure:FeedbackOutcome.Success,"Sunset Photo.png → HEIC",detail,failed?"":"1.43 MiB → 1.16 MiB · 18.5%",null,1000,600,out _);
            // Preview the same clipped blur treatment against generated colors.
            card.Measure(new Size(1000,600));card.Arrange(new Rect(new Point(),card.DesiredSize));
            var blur=new Border{Background=new VisualBrush(background){Stretch=Stretch.UniformToFill},Effect=new BlurEffect{Radius=34},Clip=new RectangleGeometry(new Rect(18,18,964,card.DesiredSize.Height-36),18,18)};
            var clipped=new Grid{Clip=new RectangleGeometry(new Rect(18,18,964,card.DesiredSize.Height-36),18,18)};clipped.Children.Add(blur);card.Children.Insert(0,clipped);
            display.Children.Add(card);
        }
        void Button(string title,Action action){var b=new Button{Content=title,Padding=new Thickness(14,8,14,8),Margin=new Thickness(5)};b.Click+=(_,_)=>action();controls.Children.Add(b);}
        Button("Success",()=>Render(false));Button("Failure",()=>Render(true));
        foreach(var lang in new[]{"en","zh-TW","zh-CN"}){string code=lang;Button(code,()=>{Settings.Current.Language=code;Render(false);});}
        Button("Live success · 5s",()=>FeedbackBanner.ShowResult(FeedbackOutcome.Success,"Sunset Photo.png → HEIC",L.Text("完成 1 個檔案\n儲存在：C:\\Users\\Example\\Downloads","Completed 1 file\nSaved to: C:\\Users\\Example\\Downloads"),"1.43 MiB → 1.16 MiB · 18.5%"));
        Button("Live failure · 8s",()=>FeedbackBanner.ShowResult(FeedbackOutcome.Failure,"Clip.mov → WebM",L.Text("無法讀取來源檔案。請檢查檔案後重試。","Cannot read the source file. Check the file and try again.")));
        Render(false);window.Closed+=(_,_)=>Application.Current.Shutdown();window.Show();
    }
}
