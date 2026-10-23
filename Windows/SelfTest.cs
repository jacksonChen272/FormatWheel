using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.IO.Compression;
using System.Text.Json;
using ImageMagick;
using PdfSharp.Pdf.IO;
namespace FormatWheel;
public static class SelfTest
{
    public static async Task Run(string root)
    {
        Directory.CreateDirectory(root);root=Path.GetFullPath(root);var checks=new List<object>();int failed=0;
        async Task Check(string name,Func<Task> test){try{await test();checks.Add(new{name,status="PASS"});}catch(Exception ex){failed++;checks.Add(new{name,status="FAIL",error=ex.ToString()});}File.WriteAllText(Path.Combine(root,"report.json"),JsonSerializer.Serialize(new{passed=checks.Count-failed,failed,checks},new JsonSerializerOptions{WriteIndented=true}));}
        void Assert(bool ok,string why){if(!ok)throw new Exception(why);}
        var image=Path.Combine(root,"測試 圖片.png");using(var im=new MagickImage(MagickColors.CornflowerBlue,320,200)){im.SetProfile(new ExifProfile());im.Write(image);}var hash=SHA256.HashData(File.ReadAllBytes(image));
        foreach(var fmt in new[]{"png","jpg","webp","tiff","gif","heic","avif"})await Check("image-"+fmt,async()=>{var r=await Engine.Run("img.to."+fmt,[image]);using var im=new MagickImage(r.Files.Single());Assert(im.Width==320&&im.Height==200,"wrong size");});
        await Check("resize-half",async()=>{var r=await Engine.Run("img.half",[image]);using var im=new MagickImage(r.Files.Single());Assert(im.Width==160&&im.Height==100,"wrong half size");});
        await Check("rotate",async()=>{var r=await Engine.Run("img.rotate",[image]);using var im=new MagickImage(r.Files.Single());Assert(im.Width==200&&im.Height==320,"wrong orientation");});
        await Check("offline-cutout",async()=>{var r=await Engine.Run("img.cutout",[image]);using var im=new MagickImage(r.Files.Single());Assert(im.Width==320&&im.Height==200&&im.HasAlpha,"invalid cutout output");});
        await Check("ocr",async()=>{var p=Path.Combine(root,"ocr.png");using(var bitmap=new System.Drawing.Bitmap(800,160)){using var g=System.Drawing.Graphics.FromImage(bitmap);g.Clear(System.Drawing.Color.White);using var font=new System.Drawing.Font("Arial",48);g.DrawString("HELLO 123",font,System.Drawing.Brushes.Black,30,30);bitmap.Save(p,System.Drawing.Imaging.ImageFormat.Png);}var text=await Engine.Recognize(p,default);Assert(text.Contains("123"),"OCR failed: "+text);});
        await Check("no-overwrite",async()=>{var a=await Engine.Run("img.to.jpg",[image]);var b=await Engine.Run("img.to.jpg",[image]);Assert(a.Files[0]!=b.Files[0],"collision");Assert(SHA256.HashData(File.ReadAllBytes(image)).SequenceEqual(hash),"source modified");});
        string? pdf=null;
        await Check("images-to-pdf",async()=>{var r=await Engine.Run("img.to.pdf",[image]);pdf=r.Files.Single();using var doc=PdfReader.Open(pdf,PdfDocumentOpenMode.Import);Assert(doc.PageCount==1,"wrong page count");});
        if(pdf!=null){foreach(var op in new[]{"to.png","to.jpg","to.long","to.txt","to.docx","to.rtf","to.html","rotate","split","mono","strip"})await Check("pdf-"+op,async()=>{var r=await Engine.Run("pdf."+op,[pdf]);Assert(r.Files.Count>0&&r.Files.All(File.Exists),"no output");});
            await Check("pdf-merge",async()=>{var second=Path.Combine(root,"second.pdf");File.Copy(pdf,second,true);var r=await Engine.Run("pdf.merge",[pdf,second]);using var doc=PdfReader.Open(r.Files.Single(),PdfDocumentOpenMode.Import);Assert(doc.PageCount==2,"wrong merge page count");});}
        await Check("zip",async()=>{var r=await Engine.Run("gen.zip",[image]);using var z=ZipFile.OpenRead(r.Files.Single());Assert(z.Entries.Count==1,"wrong entry count");});
        await Check("zip-preserves-empty-directories",async()=>{var folder=Path.Combine(root,"folder");Directory.CreateDirectory(Path.Combine(folder,"empty"));var r=await Engine.Run("gen.zip",[folder]);using var z=ZipFile.OpenRead(r.Files.Single());Assert(z.GetEntry("folder/")!=null&&z.GetEntry("folder/empty/")!=null,"empty directory lost");});
        await Check("sha256",async()=>{var r=await Engine.Run("gen.sha",[image]);Assert(r.Clipboard!.StartsWith(Convert.ToHexString(hash).ToLowerInvariant()),"hash mismatch");});
        await Check("document-unicode-roundtrip",async()=>{string text="你好 Windows\nHello & <world>";string docx=Engine.WriteTextDocument(text,"docx",root);var r=await Engine.Run("doc.copy",[docx]);Assert(r.Clipboard==text,"unicode mismatch");var stripped=await Engine.Run("doc.strip",[docx]);using var zip=ZipFile.OpenRead(stripped.Files.Single());Assert(zip.GetEntry("word/document.xml")!=null,"document damaged");});
        await Check("reject-mixed-input",async()=>{try{await Engine.Run("img.to.jpg",[image,Path.Combine(root,"report.json")]);throw new Exception("did not reject");}catch(ArgumentException){}});
        await Check("wheel-hit-directions",()=>{var v=new WheelCanvas{Center=new System.Windows.Point(400,400),Radius=285};Assert(v.Hit(v.Center)==-1,"center not cancel");for(int i=0;i<8;i++)Assert(v.Hit(WheelCanvas.Polar(v.Center,250,i*Math.PI/4))==i,"wrong slot "+i);return Task.CompletedTask;});
        await FeedbackTests.Run(Check);
        await PrivacyTests.Run(Check,root);
        await AssessmentTests.Run(Check,root);
        await PanelTests.Run(Check,root);
        await ResultBannerTests.Run(Check,root);
        if(Tool.Find("ffmpeg")!=null){var video=Path.Combine(root,"video.mp4");var audio=Path.Combine(root,"audio.wav");await Tool.Run("ffmpeg",["-y","-f","lavfi","-i","testsrc=size=128x96:rate=10","-f","lavfi","-i","sine=frequency=440","-t","1","-pix_fmt","yuv420p","-c:v","libx264","-c:a","aac",video]);await Tool.Run("ffmpeg",["-y","-i",video,"-vn",audio]);foreach(var a in Catalog.All.Values.Where(a=>a.Id.StartsWith("vid.")||a.Id.StartsWith("aud.")))await Check(a.Id,async()=>{var r=await Engine.Run(a.Id,[a.Id.StartsWith("vid.")?video:audio]);Assert(r.Files.Count==1&&new FileInfo(r.Files[0]).Length>0,"empty media");var probe=await Tool.Run("ffprobe",["-v","error","-show_entries","format=duration","-of","json",r.Files[0]]);Assert(probe.Contains("format"),"invalid media");});}
        await Check("english-action-coverage",()=>{var previous=Settings.Current.Language;try{Settings.Current.Language="en";foreach(var a in Catalog.All.Values)Assert(!System.Text.RegularExpressions.Regex.IsMatch(a.Label+a.Detail+a.Ammo,@"\p{IsCJKUnifiedIdeographs}"),"Untranslated action "+a.Id);}finally{Settings.Current.Language=previous;}return Task.CompletedTask;});
        await Check("language-switch-without-restart",()=>{var previous=Settings.Current.Language;try{Settings.Current.Language="en";Assert(Catalog.All["img.compress"].Label=="Compress","English");Settings.Current.Language="zh-CN";Assert(Catalog.All["img.compress"].Label=="压缩","Simplified");Settings.Current.Language="zh-TW";Assert(Catalog.All["img.compress"].Label=="壓縮","Traditional");}finally{Settings.Current.Language=previous;}return Task.CompletedTask;});
        await Check("language-detection",()=>{Assert(L.Detect("en-GB")=="en"&&L.Detect("zh-HK")=="zh-TW"&&L.Detect("zh-CN")=="zh-CN","locale detection");return Task.CompletedTask;});
        await Check("simplified-text-termination",()=>{var resources=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Assets.File("en.json")))!;foreach(string key in resources.Keys)Assert(L.Simplify(key).Length==key.Length,"Unexpected characters: "+key);Assert(L.Simplify("安裝轉換工具。") == "安装转换工具。","Incorrect Simplified Chinese");return Task.CompletedTask;});
        await Check("installer-rejects-corrupt-download",async()=>{try{await Dependencies.Verify(image,Dependencies.FFmpeg.Hash);throw new Exception("accepted corrupted data");}catch(IOException){}});
        await Check("installer-hash-verification",async()=>await Dependencies.Verify(image,Convert.ToHexString(hash)));
        await Check("installer-cancellation",async()=>{using var c=new System.Threading.CancellationTokenSource();c.Cancel();try{await Dependencies.Verify(image,Convert.ToHexString(hash),c.Token);throw new Exception("ignored cancellation");}catch(OperationCanceledException){}});
        await Check("first-run-settings-roundtrip",()=>{var saved=new Settings{Language="en",SetupPromptSeen=true,TourSeen=true};var loaded=JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(saved))!;Assert(loaded.Language=="en"&&loaded.SetupPromptSeen&&loaded.TourSeen,"lost onboarding state");Assert(!new Settings().SetupPromptSeen&&!new Settings().TourSeen,"new installation skipped onboarding");return Task.CompletedTask;});
        await Check("drag-trigger-and-reset",()=>{var g=new DragGesture();Assert(!g.Update(true,false,new(10,10)),"fired on click");Assert(!g.Update(true,false,new(60,10)),"fired without Shift");Assert(g.Update(true,true,new(60,10)),"missed Shift during drag");Assert(!g.Update(true,true,new(80,10)),"duplicate trigger");g.Update(false,false,new(80,10));Assert(!g.Update(true,true,new(-1900,-100)),"fired before movement");Assert(g.Update(true,true,new(-1800,-100)),"missed held Shift / negative screen coordinates");return Task.CompletedTask;});
        foreach(double dpi in new[]{1.0,1.25,1.5,2.0})await Check("wheel-placement-dpi-"+dpi,()=>{var viewport=new System.Windows.Size(1920/dpi,1080/dpi);foreach(var p in new[]{new System.Windows.Point(0,0),new System.Windows.Point(viewport.Width,viewport.Height),new System.Windows.Point(-2000,300)}){var place=WheelPlacement.Place(p,viewport,330,true);Assert(place.Center.X-place.Radius>=0&&place.Center.Y-place.Radius>=0&&place.Center.X+place.Radius<=viewport.Width&&place.Center.Y+place.Radius<=viewport.Height,"wheel outside monitor");}return Task.CompletedTask;});
        if(Tool.Find("soffice")!=null)
        {
            var documentRoot=Path.Combine(root,"libreoffice");Directory.CreateDirectory(documentRoot);
            var document=Engine.WriteTextDocument("FormatWheel document test\n繁體 简体 English 123", "docx",documentRoot);
            foreach(var fmt in new[]{"pdf","docx","rtf","odt","doc"})await Check("libreoffice-"+fmt,async()=>{var result=await Engine.Run("doc.to."+fmt,[document]);var output=result.Files.Single();Assert(new FileInfo(output).Length>100,"empty output");if(fmt=="pdf"){using var pdfCheck=PdfReader.Open(output,PdfDocumentOpenMode.Import);Assert(pdfCheck.PageCount>0,"invalid PDF");}else{var text=await Engine.Run("doc.copy",[output]);Assert(text.Clipboard!.Contains("FormatWheel document test"),"lost document text");}});
        }
        if(failed>0)throw new Exception($"{failed} integration checks failed. See report.json.");
    }
}
