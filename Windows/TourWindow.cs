using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FormatWheel;

// An isolated practice stage: no real files or conversion callbacks are reachable.
public sealed class TourWindow:Window
{
    int step;
    readonly Canvas stage=new(){Width=520,Height=580,ClipToBounds=true};
    readonly WheelCanvas wheel=new(){Width=520,Height=580,Center=new Point(260,290),Radius=210,Demo=true,CompactPreview=true,Files=["Example.png"],IsHitTestVisible=false};
    readonly Border file;
    readonly TextBlock title=Ui.Text("",25),body=Ui.Text("",16),count=Ui.Text("",13,Ui.Blue);
    readonly Button back,next;
    readonly System.Windows.Shapes.Path shade=new(){Fill=new SolidColorBrush(Color.FromArgb(185,0,0,0)),IsHitTestVisible=false};
    readonly System.Windows.Shapes.Rectangle outline=new(){Stroke=Ui.Blue,StrokeThickness=2,RadiusX=12,RadiusY=12,IsHitTestVisible=false};
    public TourWindow()
    {
        Ui.Window(this,L.Text("FormatWheel · 新手教學","FormatWheel · Quick tour"),980,690);
        var root=new Grid{Margin=new Thickness(24)};root.ColumnDefinitions.Add(new(){Width=new GridLength(1.3,GridUnitType.Star)});root.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        var box=new Viewbox{Child=stage,Stretch=Stretch.Uniform};root.Children.Add(box);
        stage.Children.Add(wheel);
        file=new Border{Background=Ui.Card,CornerRadius=new CornerRadius(10),Padding=new Thickness(16,8,16,8),Child=Ui.Text("Example.png",16),Width=180};Canvas.SetLeft(file,170);Canvas.SetTop(file,10);stage.Children.Add(file);
        var tray=new Border{Background=Ui.Card,CornerRadius=new CornerRadius(8),Padding=new Thickness(16,8,16,8),Child=Ui.Text("FormatWheel  ⚙",16),Width=230};Canvas.SetLeft(tray,145);Canvas.SetTop(tray,520);stage.Children.Add(tray);
        stage.Children.Add(shade);stage.Children.Add(outline);
        var side=new Grid{Margin=new Thickness(26,18,0,18)};Grid.SetColumn(side,1);root.Children.Add(side);side.RowDefinitions.Add(new(){Height=GridLength.Auto});side.RowDefinitions.Add(new());side.RowDefinitions.Add(new(){Height=GridLength.Auto});
        var text=new StackPanel();text.Children.Add(count);text.Children.Add(title);text.Children.Add(body);text.Children.Add(Ui.Text(L.Text("安全示範 · 不會變更任何檔案","Safe practice · no files are changed"),12,Ui.Muted));side.Children.Add(text);
        var navigation=new WrapPanel();back=Ui.Button(L.Text("上一步","Back"),()=>Go(-1));next=Ui.Button(L.Text("下一步","Next"),()=>Go(1),true);navigation.Children.Add(back);navigation.Children.Add(next);navigation.Children.Add(Ui.Button(L.Text("跳過","Skip"),Close));Grid.SetRow(navigation,2);side.Children.Add(navigation);
        Content=root;KeyDown+=(_,e)=>{if(e.Key==Key.Escape){Close();e.Handled=true;}if(e.Key==Key.Right){Go(1);e.Handled=true;}if(e.Key==Key.Left){Go(-1);e.Handled=true;}};
        Closed+=(_,_)=>{Settings.Current.TourSeen=true;Settings.Current.Save();};Render();Loaded+=(_,_)=>next.Focus();
    }
    void Go(int delta){if(step==7&&delta>0){Close();return;}step=Math.Clamp(step+delta,0,7);Render();}
    void Render()
    {
        string[] titles=[L.Text("一分鐘，學會格式輪盤","Meet your conversion wheel"),L.Text("先選擇檔案","Choose a file"),L.Text("拖住，再按 Shift","Drag, then press Shift"),L.Text("朝想要的格式放手","Drop toward a format"),L.Text("再按 Shift，切換工具","Press Shift again for tools"),L.Text("移回中心即可取消","Move to the center to cancel"),L.Text("結果就在原檔旁邊","Find the result beside your file"),L.Text("隨時補裝、設定與重看","Tools, settings and help")];
        string[] descriptions=[L.Text("接下來會依照實際操作順序介紹。隨時可跳過，也能從右鍵選單再看一次。","Follow the workflow in this safe practice view. Skip any time and replay from the right-click menu."),L.Text("在檔案總管選擇一個或多個同類型檔案。也可雙擊系統匣圖示，開啟檔案選擇器。","Select one or more files of the same type in File Explorer. Or double-click the system tray icon to open the file picker."),L.Text("按住滑鼠左鍵開始拖曳，再按 Shift。輪盤會在游標所在螢幕出現。整個過程都要保持拖住檔案。","Keep the left mouse button held while dragging, then press Shift. The wheel appears on the screen under your pointer. Keep holding the file."),L.Text("把游標移向 PNG、JPG 或其他格式，放開滑鼠就會轉換。灰色選項表示需要安裝工具，或不適用目前檔案。","Move toward PNG, JPG or another format and release to convert. Dimmed options need an extra tool or do not support the selected files."),L.Text("再次按 Shift 可在格式與工具之間翻頁。也支援 Tab 或滾輪；圖片工具包括壓縮、去背、旋轉等。","Press Shift again to switch between formats and tools. Tab and the mouse wheel also change pages. Image tools include compression, background removal and rotation."),L.Text("不想轉換時，移回中心再放開滑鼠。用點選模式時，也可以按 Esc。原檔會保留。","To cancel a drag, move back to the center and release. In click mode you can also press Esc. Your original file stays intact."),L.Text("轉換結果儲存在來源檔案旁邊。遇到同名檔案會自動加上編號，完成後可從「最近轉換」找到。","Results are saved next to the source. Existing names get a numbered suffix. Find recent outputs through Recent conversions."),L.Text("右鍵點擊系統匣圖示，可安裝 FFmpeg／LibreOffice、切換語言、重播教學。現在試試雙擊圖示，選擇一張圖片吧。","Right-click the system tray icon to install FFmpeg / LibreOffice, switch language or replay this tour. Try double-clicking the icon and choosing an image.")];
        title.Text=titles[step];body.Text=descriptions[step];count.Text=$"{step+1:D2} / 08";back.IsEnabled=step>0;next.Content=step==7?L.Text("完成","Done"):L.Text("下一步","Next");
        wheel.Page=step==4?1:0;wheel.Hover=step==3?1:step==4?0:-1;wheel.InvalidateVisual();
        file.Visibility=step is 1 or 2 or 6?Visibility.Visible:Visibility.Hidden;
        Rect target=step switch{1=>new(165,5,190,65),2=>new(40,75,440,430),3=>new(335,125,135,140),4=>new(165,75,190,130),5=>new(182,225,156,130),6=>new(165,5,190,65),7=>new(140,515,240,60),_=>new(40,75,440,430)};
        shade.Data=new CombinedGeometry(GeometryCombineMode.Exclude,new RectangleGeometry(new Rect(0,0,520,580)),new RectangleGeometry(target,12,12));outline.Width=target.Width;outline.Height=target.Height;Canvas.SetLeft(outline,target.X);Canvas.SetTop(outline,target.Y);
    }
}
