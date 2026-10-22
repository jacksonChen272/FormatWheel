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
    // Keep the wheel's readable size; expand the space around it instead of
    // scaling the entire wheel up against the spotlight border.
    readonly Canvas stage=new(){Width=660,Height=660,ClipToBounds=true};
    readonly WheelCanvas wheel=new(){Width=660,Height=660,Center=new Point(330,330),Radius=210,Demo=true,CompactPreview=true,Files=["Example.png"],IsHitTestVisible=false};
    readonly Border file;
    readonly TextBlock title=Ui.Text("",25),body=Ui.Text("",16),count=Ui.Text("",13,Ui.Blue);
    readonly ScrollViewer description=new(){VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new Thickness(0,0,8,0)};
    readonly Button back,next;
    readonly System.Windows.Shapes.Path shade=new(){Fill=new SolidColorBrush(Color.FromArgb(185,0,0,0)),IsHitTestVisible=false};
    readonly System.Windows.Shapes.Path outline=new(){Stroke=Ui.Blue,StrokeThickness=2,IsHitTestVisible=false};
    public TourWindow()
    {
        Ui.Window(this,L.Text("FormatWheel · 新手教學","FormatWheel · Quick tour"),1180,790);
        MinWidth=Math.Min(960,SystemParameters.WorkArea.Width);MinHeight=Math.Min(640,SystemParameters.WorkArea.Height);
        var root=new Grid{Margin=new Thickness(28)};root.ColumnDefinitions.Add(new(){Width=new GridLength(1.5,GridUnitType.Star)});root.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        var box=new Viewbox{Child=stage,Stretch=Stretch.Uniform};root.Children.Add(box);
        stage.Children.Add(wheel);
        var fileLabel=Ui.Text("Example.png",16);fileLabel.Margin=new Thickness(0);
        file=new Border{Background=Ui.Card,CornerRadius=new CornerRadius(10),Padding=new Thickness(20,16,20,16),Child=fileLabel,Width=250};Canvas.SetLeft(file,205);Canvas.SetTop(file,32);stage.Children.Add(file);
        var trayLabel=Ui.Text("FormatWheel  ⚙",16);trayLabel.Margin=new Thickness(0);
        var tray=new Border{Background=Ui.Card,CornerRadius=new CornerRadius(10),Padding=new Thickness(20,14,20,14),Child=trayLabel,Width=250};Canvas.SetLeft(tray,205);Canvas.SetTop(tray,590);stage.Children.Add(tray);
        stage.Children.Add(shade);stage.Children.Add(outline);
        var side=new Grid{Margin=new Thickness(36,24,8,24)};Grid.SetColumn(side,1);root.Children.Add(side);side.RowDefinitions.Add(new());side.RowDefinitions.Add(new(){Height=GridLength.Auto});
        count.Margin=new Thickness(0,0,0,18);title.Margin=new Thickness(0,0,0,20);body.Margin=new Thickness(0,0,0,20);
        var text=new StackPanel();text.Children.Add(count);text.Children.Add(title);text.Children.Add(body);text.Children.Add(Ui.Text(L.Text("安全示範 · 不會變更任何檔案","Safe practice · no files are changed"),12,Ui.Muted));
        description.Content=text;side.Children.Add(description);
        var navigation=new WrapPanel{Margin=new Thickness(0,24,0,0)};back=Ui.Button(L.Text("上一步","Back"),()=>Go(-1));next=Ui.Button(L.Text("下一步","Next"),()=>Go(1),true);navigation.Children.Add(back);navigation.Children.Add(next);navigation.Children.Add(Ui.Button(L.Text("跳過","Skip"),Close));Grid.SetRow(navigation,1);side.Children.Add(navigation);
        Content=root;KeyDown+=(_,e)=>{if(e.Key==Key.Escape){Close();e.Handled=true;}if(e.Key==Key.Right){Go(1);e.Handled=true;}if(e.Key==Key.Left){Go(-1);e.Handled=true;}};
        Closed+=(_,_)=>{Settings.Current.TourSeen=true;Settings.Current.Save();};Render();Loaded+=(_,_)=>next.Focus();
    }
    void Go(int delta){if(step==7&&delta>0){Close();return;}step=Math.Clamp(step+delta,0,7);Render();}
    void Render()
    {
        string[] titles=[L.Text("一分鐘，學會格式輪盤","Meet your conversion wheel"),L.Text("先選擇檔案","Choose a file"),L.Text("拖住，再按 Shift","Drag, then press Shift"),L.Text("朝想要的格式放手","Drop toward a format"),L.Text("再按 Shift，切換工具","Press Shift again for tools"),L.Text("移回中心即可取消","Move to the center to cancel"),L.Text("結果就在原檔旁邊","Find the result beside your file"),L.Text("隨時補裝、設定與重看","Tools, settings and help")];
        string[] descriptions=[L.Text("接下來會依照實際操作順序介紹。隨時可跳過，也能從右鍵選單再看一次。","Follow the workflow in this safe practice view. Skip any time and replay from the right-click menu."),L.Text("在檔案總管選擇一個或多個同類型檔案。也可雙擊系統匣圖示，開啟檔案選擇器。","Select one or more files of the same type in File Explorer. Or double-click the system tray icon to open the file picker."),L.Text("按住滑鼠左鍵開始拖曳，再按 Shift。輪盤會在游標所在螢幕出現。整個過程都要保持拖住檔案。","Keep the left mouse button held while dragging, then press Shift. The wheel appears on the screen under your pointer. Keep holding the file."),L.Text("把游標移向 PNG、JPG 或其他格式，放開滑鼠就會轉換。灰色選項表示需要安裝工具，或不適用目前檔案。","Move toward PNG, JPG or another format and release to convert. Dimmed options need an extra tool or do not support the selected files."),L.Text("再次按 Shift 可在格式與工具之間翻頁。也支援 Tab 或滾輪；圖片工具包括壓縮、去背、旋轉等。","Press Shift again to switch between formats and tools. Tab and the mouse wheel also change pages. Image tools include compression, background removal and rotation."),L.Text("不想轉換時，移回中心再放開滑鼠。用點選模式時，也可以按 Esc。原檔會保留。","To cancel a drag, move back to the center and release. In click mode you can also press Esc. Your original file stays intact."),L.Text("轉換結果儲存在來源檔案旁邊。遇到同名檔案會自動加上編號，完成後可從「最近轉換」找到。","Results are saved next to the source. Existing names get a numbered suffix. Find recent outputs through Recent conversions."),L.Text("右鍵點擊系統匣圖示，可安裝 FFmpeg／LibreOffice、切換語言、重播教學。現在試試雙擊圖示，選擇一張圖片吧。","Right-click the system tray icon to install FFmpeg / LibreOffice, switch language or replay this tour. Try double-clicking the icon and choosing an image.")];
        title.Text=titles[step];body.Text=descriptions[step];count.Text=$"{step+1:D2} / 08";back.IsEnabled=step>0;next.Content=step==7?L.Text("完成","Done"):L.Text("下一步","Next");
        description.ScrollToTop();
        wheel.Page=step==4?1:0;wheel.Hover=step==3?1:step==4?0:-1;wheel.InvalidateVisual();
        file.Visibility=step is 1 or 2 or 6?Visibility.Visible:Visibility.Hidden;
        Rect target=step switch{1=>new(181,12,298,96),2=>new(52,12,556,574),3=>new(385,140,146,170),4=>new(250,94,160,150),5=>new(202,230,256,214),6=>new(181,12,298,96),7=>new(181,574,298,80),_=>new(52,74,556,512)};
        Geometry focus=step==5?new EllipseGeometry(new Point(330,330),126,126):new RectangleGeometry(target,12,12);
        shade.Data=new CombinedGeometry(GeometryCombineMode.Exclude,new RectangleGeometry(new Rect(0,0,stage.Width,stage.Height)),focus);outline.Data=focus;
    }
}
