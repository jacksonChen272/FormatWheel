using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace FormatWheel;

public static class ConversionFeedback
{
    public static string SizeChange(string[] sources,string[] outputs)
    {
        if(sources.Length==0||outputs.Length==0||sources.Any(Directory.Exists))return "";
        try
        {
            long before=sources.Sum(f=>new FileInfo(f).Length),after=outputs.Sum(f=>new FileInfo(f).Length);
            if(before<=0)return "";
            double change=100d*(before-after)/before;
            string sizes=Bytes(before)+" → "+Bytes(after);
            return sizes+" · "+string.Format(change>=0?L.Text("實際縮小 {0:0.#}%","Actually {0:0.#}% smaller"):L.Text("實際增大 {0:0.#}%","Actually {0:0.#}% larger"),Math.Abs(change));
        }catch{return "";}
    }
    static string Bytes(long bytes)=>bytes<1024?$"{bytes} B":bytes<1048576?$"{bytes/1024d:0.##} KiB":$"{bytes/1048576d:0.##} MiB";
    public static void Show(string title,string detail,string sizeChange,PrivacyResult? before,PrivacyResult? after)
    {
        var window=new Window{Title=title,Width=620,SizeToContent=SizeToContent.Height,MaxHeight=SystemParameters.WorkArea.Height-40,WindowStyle=WindowStyle.None,AllowsTransparency=true,Background=new SolidColorBrush(Color.FromArgb(248,20,24,33)),Foreground=Brushes.White,Topmost=true,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.CenterScreen};
        var body=new StackPanel{Margin=new Thickness(26)};
        body.Children.Add(new TextBlock{Text=L.Text("轉換完成","Conversion complete"),FontSize=24,FontWeight=FontWeights.Bold,Foreground=Ui.Blue,Margin=new Thickness(0,0,0,12)});
        body.Children.Add(Ui.Text(title,18));
        if(sizeChange.Length>0)body.Children.Add(Ui.Text(sizeChange,14,Brushes.White));
        body.Children.Add(Ui.Text(detail,13,Ui.Muted));
        TextBlock? stars=null;
        if(after!=null)
        {
            body.Children.Add(Ui.Text(L.Text("輸出檔案 · 通緝星級","Output file · wanted level"),14));
            if(after.CanRate){stars=new TextBlock{Text=Stars(after.Level),FontSize=28,Foreground=Brushes.White};body.Children.Add(stars);}
            body.Children.Add(Ui.Text(after.StatusText+"\n"+after.Detail,13,Ui.Muted));
        }
        body.Children.Add(Ui.Text(L.Text("點擊關閉","Click to dismiss"),11,Ui.Muted));
        window.Content=new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};window.MouseDown+=(_,_)=>window.Close();
        window.Loaded+=(_,_)=>Output.Log($"result feedback visible={window.IsVisible} size={window.ActualWidth}x{window.ActualHeight}");
        window.Show();
        long start=Environment.TickCount64;
        var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(50)};
        timer.Tick+=(_,_)=>
        {
            double seconds=(Environment.TickCount64-start)/1000d;
            if(stars!=null&&before?.CanRate==true&&before.Level>0&&after?.VerifiedClean==true&&SystemParameters.ClientAreaAnimation)
            {
                // Only a fully checked, clean output earns the original's disappearing-stars effect.
                stars.Text=seconds<1.8?Stars(before.Level):Stars(0);
                stars.Opacity=seconds<1.2 ? .4+.6*(.5+.5*Math.Cos(seconds*Math.PI*3)):seconds<1.8?Math.Max(0,1-(seconds-1.2)/.6):1;
            }
            if(seconds>=8)window.Close();
        };
        window.Closed+=(_,_)=>{timer.Stop();Output.Log("result feedback closed");};timer.Start();
    }
    static string Stars(int level)=>new string('☆',5-Math.Clamp(level,0,5))+new string('★',Math.Clamp(level,0,5));
}
