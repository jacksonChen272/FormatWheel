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
        await Check("sha256",async()=>{var r=await Engine.Run("gen.sha",[image]);Assert(r.Clipboard!.StartsWith(Convert.ToHexString(hash).ToLowerInvariant()),"hash mismatch");});
        await Check("document-unicode-roundtrip",async()=>{string text="你好 Windows\nHello & <world>";string docx=Engine.WriteTextDocument(text,"docx",root);var r=await Engine.Run("doc.copy",[docx]);Assert(r.Clipboard==text,"unicode mismatch");var stripped=await Engine.Run("doc.strip",[docx]);using var zip=ZipFile.OpenRead(stripped.Files.Single());Assert(zip.GetEntry("word/document.xml")!=null,"document damaged");});
        await Check("reject-mixed-input",async()=>{try{await Engine.Run("img.to.jpg",[image,Path.Combine(root,"report.json")]);throw new Exception("did not reject");}catch(ArgumentException){}});
        await Check("wheel-hit-directions",()=>{var v=new WheelCanvas{Center=new System.Windows.Point(400,400),Radius=285};Assert(v.Hit(v.Center)==-1,"center not cancel");for(int i=0;i<8;i++)Assert(v.Hit(WheelCanvas.Polar(v.Center,250,i*Math.PI/4))==i,"wrong slot "+i);return Task.CompletedTask;});
        if(Tool.Find("ffmpeg")!=null){var video=Path.Combine(root,"video.mp4");var audio=Path.Combine(root,"audio.wav");await Tool.Run("ffmpeg",["-y","-f","lavfi","-i","testsrc=size=128x96:rate=10","-f","lavfi","-i","sine=frequency=440","-t","1","-pix_fmt","yuv420p","-c:v","libx264","-c:a","aac",video]);await Tool.Run("ffmpeg",["-y","-i",video,"-vn",audio]);foreach(var a in Catalog.All.Values.Where(a=>a.Id.StartsWith("vid.")||a.Id.StartsWith("aud.")))await Check(a.Id,async()=>{var r=await Engine.Run(a.Id,[a.Id.StartsWith("vid.")?video:audio]);Assert(r.Files.Count==1&&new FileInfo(r.Files[0]).Length>0,"empty media");var probe=await Tool.Run("ffprobe",["-v","error","-show_entries","format=duration","-of","json",r.Files[0]]);Assert(probe.Contains("format"),"invalid media");});}
        if(failed>0)throw new Exception($"{failed} integration checks failed. See report.json.");
    }
}
