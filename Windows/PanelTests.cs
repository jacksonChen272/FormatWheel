using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageMagick;
using PdfSharp.Pdf.IO;

namespace FormatWheel;

public static class PanelTests
{
    public static async Task Run(Func<string,Func<Task>,Task> check,string root)
    {
        void Assert(bool ok,string reason){if(!ok)throw new Exception(reason);}
        await check("comparison-bar-retained-gain-loss",()=>
        {
            var gain=StatsPanel.Segments(.4,.8);Assert(gain.Kept==.4&&Math.Abs(gain.Gain-.4)<.0001&&gain.Loss==0,"gain segments");
            var loss=StatsPanel.Segments(.9,.3);Assert(loss.Kept==.3&&loss.Gain==0&&Math.Abs(loss.Loss-.6)<.0001,"loss segments");
            Assert(StatsPanel.Segments(1,1)==(1d,0d,0d),"unchanged");return Task.CompletedTask;
        });
        await check("multi-image-pdf-count-order-originals",async()=>
        {
            string[] files=Enumerable.Range(1,3).Select(i=>Path.Combine(root,$"merge-{i}.png")).ToArray();
            for(int i=0;i<3;i++){using var image=new MagickImage(MagickColors.Red,(uint)(120+40*i),100);image.Write(files[i]);}
            var result=await Engine.Run("img.to.pdf",files);
            using var pdf=PdfReader.Open(result.Files.Single(),PdfDocumentOpenMode.Import);
            Assert(pdf.PageCount==3,"not three pages");
            for(int i=0;i<3;i++)Assert(Math.Abs(pdf.Pages[i].Width.Point-(120+40*i))<1,"changed image order");
            Assert(files.All(File.Exists),"removed originals");
            var summary=FileAssessment.Read(files).Evaluate(Catalog.All["img.to.pdf"]);
            Assert(summary.Detail.Contains('3'),"missing batch count");
        });
        await check("actual-size-change-direction",()=>
        {
            var source=Path.Combine(root,"size-source.dat");var output=Path.Combine(root,"size-result.dat");
            File.WriteAllBytes(source,new byte[1000]);File.WriteAllBytes(output,new byte[400]);var prior=Settings.Current.Language;
            try{Settings.Current.Language="en";Assert(ConversionFeedback.SizeChange([source],[output]).Contains("60% smaller"),"wrong reduction");Assert(ConversionFeedback.SizeChange([output],[source]).Contains("150% larger"),"wrong expansion");Assert(ConversionFeedback.SizeChange([source],[])=="","clipboard-only comparison");}
            finally{Settings.Current.Language=prior;}return Task.CompletedTask;
        });
        await check("floating-stats-three-languages-bounds-and-dismiss",()=>
        {
            var file=Path.Combine(root,"panel-transparent.png");using(var image=new MagickImage(MagickColors.Transparent,640,480))image.Write(file);
            var prior=Settings.Current.Language;
            try
            {
                foreach(var language in new[]{"en","zh-TW","zh-CN"})
                {
                    Settings.Current.Language=language;
                    var metadata=FileAssessment.Read([file]);var privacy=Privacy.Scan([file]);
                    foreach(var size in new[]{new Size(1440,900),new Size(960,540)})
                    {
                        var placed=WheelPlacement.Place(default,size,235,false);
                        var view=new WheelCanvas{Width=size.Width,Height=size.Height,Center=placed.Center,Radius=placed.Radius,Files=[file],Assessment=metadata,PrivacyScan=privacy};
                        var grid=new Grid{Width=size.Width,Height=size.Height,Background=new SolidColorBrush(Color.FromRgb(45,50,62))};grid.Children.Add(view);
                        grid.Measure(size);grid.Arrange(new Rect(size));grid.UpdateLayout();
                        for(int slot=0;slot<8;slot++)
                        {
                            view.UpdatePointer(WheelCanvas.Polar(view.Center,view.Radius*.8,slot*Math.PI/4));
                            grid.UpdateLayout();var bitmap=new RenderTargetBitmap((int)size.Width,(int)size.Height,96,96,PixelFormats.Pbgra32);bitmap.Render(grid);
                            var b=view.LastHintBounds;Assert(!b.IsEmpty&&b.Left>=0&&b.Top>=0&&b.Right<=size.Width&&b.Bottom<=size.Height,"clipped card "+language+slot);
                            Assert(view.Hover==slot,"card changed selection");
                            if(slot==1&&size.Width==1440){var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(Path.Combine(root,"panel-"+language+".png"));png.Save(stream);}
                        }
                        view.UpdatePointer(view.Center);grid.UpdateLayout();new RenderTargetBitmap((int)size.Width,(int)size.Height,96,96,PixelFormats.Pbgra32).Render(grid);Assert(view.LastHintBounds.IsEmpty,"card remains after leave");
                    }
                }
            }finally{Settings.Current.Language=prior;}
            return Task.CompletedTask;
        });
    }
}
