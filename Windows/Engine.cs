using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Xml.Linq;
using ImageMagick;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Drawing;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace FormatWheel;

public record RunResult(List<string> Files,string Message,string? Clipboard=null);
public static class Engine
{
    public static async Task<RunResult> Run(string id,string[] files,IProgress<string>? progress=null,CancellationToken cancel=default)
    {
        if(files.Length==0) throw new IOException(L.T("請選擇檔案"));
        files=files.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if(files.Any(f=>!File.Exists(f)&&!Directory.Exists(f))) throw new FileNotFoundException(L.T("來源檔案不存在"));
        if(!Catalog.All.TryGetValue(id,out var action)) throw new ArgumentException(L.T("未知操作：")+id);
        var unavailable=action.Unavailable(files); if(unavailable!=null)throw new InvalidOperationException(unavailable);
        if(id=="gen.path"||id=="gen.name") return new([],L.T("已複製到剪貼簿"),string.Join(Environment.NewLine,files.Select(f=>id=="gen.name"?Path.GetFileName(f):f)));
        if(!id.StartsWith("gen.")&&files.Any(f=>Catalog.Kind(f)!=id.Split('.')[0]))throw new ArgumentException(L.T("請選取相同類型的檔案"));
        string tmp=Path.Combine(Path.GetTempPath(),"FormatWheel-"+Guid.NewGuid()); Directory.CreateDirectory(tmp);
        var outputs=new List<string>();var clips=new List<string>();var notes=new List<string>();
        try
        {
            if(id=="gen.zip")
            {
                string zip=Path.Combine(tmp,"archive.zip");
                using(var z=ZipFile.Open(zip,ZipArchiveMode.Create)) {var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(var f in files){cancel.ThrowIfCancellationRequested();var root=Path.GetFileName(f.TrimEnd(Path.DirectorySeparatorChar));var basename=root;int n=2;while(!names.Add(root))root=basename+" ("+n+++")";
                    if(Directory.Exists(f))
                    {
                        z.CreateEntry(root+"/");
                        var options=new EnumerationOptions{RecurseSubdirectories=true,AttributesToSkip=System.IO.FileAttributes.ReparsePoint,IgnoreInaccessible=false};
                        foreach(var subdir in Directory.EnumerateDirectories(f,"*",options))
                        {
                            cancel.ThrowIfCancellationRequested();
                            z.CreateEntry(root+"/"+Path.GetRelativePath(f,subdir).Replace(Path.DirectorySeparatorChar,'/')+"/");
                        }
                        foreach(var child in Directory.EnumerateFiles(f,"*",options))
                        {
                            cancel.ThrowIfCancellationRequested();
                            z.CreateEntryFromFile(child,root+"/"+Path.GetRelativePath(f,child).Replace(Path.DirectorySeparatorChar,'/'));
                        }
                    }
                    else z.CreateEntryFromFile(f,root);
                }}
                outputs.Add(Output.Commit(zip,files[0],"zip",files.Length>1?L.T(" 合併"):""));return new(outputs,L.T("ZIP 已建立"));
            }
            if(id=="img.to.pdf"||id=="pdf.merge")
            {
                string outFile=Path.Combine(tmp,"merged.pdf");using var pdf=new PdfDocument();
                foreach(var f in files){cancel.ThrowIfCancellationRequested();if(id=="pdf.merge"){using var src=PdfReader.Open(f,PdfDocumentOpenMode.Import);foreach(var page in src.Pages)pdf.AddPage(page);}else {using var image=new MagickImage(f);using var stream=new MemoryStream(image.ToByteArray(MagickFormat.Png));AddImagePage(pdf,stream,image.Width,image.Height);}}
                pdf.Save(outFile);outputs.Add(Output.Commit(outFile,files[0],"pdf",files.Length>1?L.T(" 合併"):""));return new(outputs,L.T("PDF 已建立"));
            }
            for(int i=0;i<files.Length;i++)
            {
                cancel.ThrowIfCancellationRequested();var f=files[i];progress?.Report($"{i+1}/{files.Length} · {Path.GetFileName(f)}");
                if(id=="gen.sha"){using var stream=File.OpenRead(f);clips.Add(Convert.ToHexString(await SHA256.HashDataAsync(stream,cancel)).ToLowerInvariant()+"  "+Path.GetFileName(f));continue;}
                var stage=Path.Combine(tmp,i.ToString());Directory.CreateDirectory(stage);
                RunResult r=id.Split('.')[0] switch {"img"=>await Image(id,f,stage,cancel),"pdf"=>await Pdf(id,f,stage,cancel),"doc"=>await Document(id,f,stage,cancel),_=>await Media(id,f,stage,cancel)};
                foreach(var p in r.Files) {outputs.Add(Output.Commit(p,f,Path.GetExtension(p).TrimStart('.')," "+Path.GetFileNameWithoutExtension(p)));}
                if(r.Clipboard!=null)clips.Add(r.Clipboard);if(!string.IsNullOrWhiteSpace(r.Message))notes.Add(r.Message);
            }
            return new(outputs,notes.Count>0?string.Join("; ",notes.Distinct()):L.F("完成 {0} 個檔案",outputs.Count),clips.Count>0?string.Join("\n\n",clips):null);
        }
        catch(Exception ex){Output.Log(id+" failed: "+ex);if(outputs.Count>0)throw new IOException(L.F("部分完成，已儲存 {0} 個檔案：\n",outputs.Count)+string.Join("\n",outputs)+"\n"+ex.Message,ex);throw;}
        finally{try{Directory.Delete(tmp,true);}catch{}}
    }
    static void AddImagePage(PdfDocument doc,Stream stream,uint w,uint h)
    {
        var page=doc.AddPage();double scale=Math.Min(1,14000.0/Math.Max(w,h));page.Width=XUnit.FromPoint(w*scale);page.Height=XUnit.FromPoint(h*scale);
        using var x=XImage.FromStream(stream);using var g=XGraphics.FromPdfPage(page);g.DrawImage(x,0,0,page.Width.Point,page.Height.Point);
    }
    static async Task<RunResult> Image(string id,string f,string tmp,CancellationToken ct)
    {
        if(id=="img.ocr"){string text=await Recognize(f,ct);var dest=Path.Combine(tmp,L.T("文字.txt"));await File.WriteAllTextAsync(dest,text,ct);return new([dest],L.T("文字已辨識"),text);}
        if(id=="img.cutout"){var dest=Path.Combine(tmp,L.T("去背.png"));Cutout.Run(f,dest,ct);return new([dest],L.T("已使用本機 U²-Net 模型去背"));}
        using var image=new MagickImage(f);image.AutoOrient();
        string format=id.Contains(".to.")?id.Split('.').Last():Path.GetExtension(f).TrimStart('.').ToLowerInvariant();
        if(format=="jpeg")format="jpg";if(format=="tif")format="tiff";
        switch(id)
        {
            case "img.half":image.Resize(Math.Max(1,image.Width/2),Math.Max(1,image.Height/2));break;
            case "img.rotate":image.Rotate(90);break;
            case "img.mono":image.ColorSpace=ColorSpace.Gray;break;
            case "img.strip":image.Strip();break;
            case "img.compress":image.Quality=72;image.Strip();break;
            case "img.backdrop":
                image.Resize(new MagickGeometry(1400,1000));
                var backdrop=Path.Combine(tmp,L.T("背景.png"));Backdrop(image,backdrop);return new([backdrop],L.T("已加入漸層背景、圓角與陰影"));
        }
        if(format=="jpg"){image.BackgroundColor=MagickColors.White;image.Alpha(AlphaAction.Remove);}
        if(id!="img.compress")image.Quality=90;
        var dest2=Path.Combine(tmp,(id.Contains(".to.")?L.T("轉換"):Catalog.All[id].Label)+"."+format);
        image.Write(dest2);
        if(id=="img.compress"&&new FileInfo(dest2).Length>=new FileInfo(f).Length)throw new IOException(L.T("原圖已經很精簡，未產生更大的壓縮結果"));
        return new([dest2],id=="img.strip"?L.T("已移除中繼資料；Windows 版會重新編碼圖片"):"");
    }
    static void Backdrop(MagickImage image,string output)
    {
        int w=(int)image.Width,h=(int)image.Height;using var canvas=new System.Drawing.Bitmap(w+160,h+160);using var g=System.Drawing.Graphics.FromImage(canvas);g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var gradient=new System.Drawing.Drawing2D.LinearGradientBrush(new System.Drawing.Rectangle(0,0,w+160,h+160),System.Drawing.Color.FromArgb(103,136,204),System.Drawing.Color.FromArgb(205,160,189),45);g.FillRectangle(gradient,0,0,w+160,h+160);
        System.Drawing.Drawing2D.GraphicsPath Rounded(int x,int y,int width,int height){var p=new System.Drawing.Drawing2D.GraphicsPath();int r=Math.Min(24,Math.Min(width,height));p.AddArc(x,y,r,r,180,90);p.AddArc(x+width-r,y,r,r,270,90);p.AddArc(x+width-r,y+height-r,r,r,0,90);p.AddArc(x,y+height-r,r,r,90,90);p.CloseFigure();return p;}
        for(int i=18;i>=1;i--){using var shadow=Rounded(80-i,88-i,w+2*i,h+2*i);using var brush=new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(5,0,0,0));g.FillPath(brush,shadow);}
        using var clip=Rounded(80,80,w,h);g.SetClip(clip);using var stream=new MemoryStream(image.ToByteArray(MagickFormat.Png));using var photo=System.Drawing.Image.FromStream(stream);g.DrawImage(photo,80,80,w,h);g.ResetClip();canvas.Save(output,System.Drawing.Imaging.ImageFormat.Png);
    }
    public static async Task<string> Recognize(string path,CancellationToken ct)
    {
        // Convert via Magick so all image formats become WinRT-readable PNG.
        using var magick=new MagickImage(path);magick.AutoOrient();uint limit=OcrEngine.MaxImageDimension;
        if(magick.Width>limit||magick.Height>limit)magick.Resize(new MagickGeometry(limit,limit));
        using var stream=new InMemoryRandomAccessStream();using(var writer=new DataWriter(stream)){writer.WriteBytes(magick.ToByteArray(MagickFormat.Png));await writer.StoreAsync();writer.DetachStream();}stream.Seek(0);
        var decoder=await BitmapDecoder.CreateAsync(stream);using var bitmap=await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied);
        var engine=OcrEngine.TryCreateFromUserProfileLanguages()??OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("en-US"))??throw new InvalidOperationException(L.T("請在 Windows 語言設定安裝 OCR 語言套件"));
        ct.ThrowIfCancellationRequested();var result=await engine.RecognizeAsync(bitmap);return string.Join("\n",result.Lines.Select(l=>l.Text));
    }
    static async Task<List<string>> RenderPdf(string f,string dir,CancellationToken ct)
    {
        var file=await StorageFile.GetFileFromPathAsync(Path.GetFullPath(f));var doc=await Windows.Data.Pdf.PdfDocument.LoadFromFileAsync(file);var paths=new List<string>();
        for(uint i=0;i<doc.PageCount;i++){ct.ThrowIfCancellationRequested();using var page=doc.GetPage(i);using var stream=new InMemoryRandomAccessStream();double scale=Math.Min(2,4096/Math.Max(page.Size.Width,page.Size.Height));await page.RenderToStreamAsync(stream,new Windows.Data.Pdf.PdfPageRenderOptions{DestinationWidth=(uint)Math.Max(1,page.Size.Width*scale),DestinationHeight=(uint)Math.Max(1,page.Size.Height*scale)});stream.Seek(0);var reader=new DataReader(stream.GetInputStreamAt(0));await reader.LoadAsync((uint)stream.Size);var bytes=new byte[(int)stream.Size];reader.ReadBytes(bytes);reader.Dispose();var p=Path.Combine(dir,L.F("頁面-{0:D4}",i+1)+".png");await File.WriteAllBytesAsync(p,bytes,ct);paths.Add(p);}return paths;
    }
    static async Task<RunResult> Pdf(string id,string f,string tmp,CancellationToken ct)
    {
        if(new[]{"pdf.to.txt","pdf.to.md","pdf.to.html","pdf.to.rtf","pdf.to.docx"}.Contains(id))
        {
            using var pdf=UglyToad.PdfPig.PdfDocument.Open(f);string text=string.Join("\n\n",pdf.GetPages().Select(p=>UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor.ContentOrderTextExtractor.GetText(p)));
            var dest=WriteTextDocument(text,id.Split('.').Last(),tmp);return new([dest],L.T("已提取文字；不保留原頁面布局"));
        }
        if(new[]{"pdf.split","pdf.rotate","pdf.strip"}.Contains(id))
        {
            var outputs=new List<string>();using var pdf=PdfReader.Open(f,id=="pdf.split"?PdfDocumentOpenMode.Import:PdfDocumentOpenMode.Modify);
            if(id=="pdf.split"){for(int i=0;i<pdf.PageCount;i++){using var one=new PdfDocument();one.AddPage(pdf.Pages[i]);var p=Path.Combine(tmp,L.F("頁面-{0:D4}",i+1)+".pdf");one.Save(p);outputs.Add(p);}}
            else {if(id=="pdf.rotate")foreach(var page in pdf.Pages)page.Rotate=(page.Rotate+90)%360;
                else {pdf.Info.Elements.Clear();pdf.Internals.Catalog.Elements.Remove("/Metadata");foreach(var page in pdf.Pages)page.Elements.Remove("/Metadata");}
                var p=Path.Combine(tmp,Catalog.All[id].Label+".pdf");pdf.Save(p);outputs.Add(p);}
            return new(outputs,id=="pdf.strip"?L.T("已移除文件屬性與文件／頁面 XMP；不清除內文、註解或附件"):"");
        }
        var images=await RenderPdf(f,tmp,ct);
        if(id=="pdf.to.png")return new(images,"");
        if(id=="pdf.ocr"){var texts=new List<string>();foreach(var p in images)texts.Add(await Recognize(p,ct));var text=string.Join("\n\n",texts);var dest=Path.Combine(tmp,L.T("文字.txt"));await File.WriteAllTextAsync(dest,text,ct);return new([dest],L.T("已辨識頁面文字"),text);}
        if(id=="pdf.to.jpg"){var outputs=new List<string>();foreach(var p in images){using var im=new MagickImage(p);im.BackgroundColor=MagickColors.White;im.Alpha(AlphaAction.Remove);im.Quality=90;var o=Path.ChangeExtension(p,"jpg");im.Write(o);outputs.Add(o);}return new(outputs,"");}
        if(id=="pdf.to.long")
        {
            var outputs=new List<string>();using var collection=new MagickImageCollection();uint height=0;int part=1;
            void Flush(){if(collection.Count==0)return;using var joined=collection.AppendVertically();var o=Path.Combine(tmp,L.T("長圖-")+part+++".png");joined.Write(o);outputs.Add(o);collection.Clear();height=0;}
            foreach(var p in images){var im=new MagickImage(p);im.Resize(1242,0);if(height+im.Height>15000)Flush();height+=im.Height;collection.Add(im);}Flush();return new(outputs,"");
        }
        using(var doc=new PdfDocument()){foreach(var p in images){using var im=new MagickImage(p);im.BackgroundColor=MagickColors.White;im.Alpha(AlphaAction.Remove);if(id=="pdf.mono")im.ColorSpace=ColorSpace.Gray;im.Quality=id=="pdf.compress"?65u:90u;using var stream=new MemoryStream(im.ToByteArray(MagickFormat.Jpeg));AddImagePage(doc,stream,im.Width,im.Height);}var o=Path.Combine(tmp,Catalog.All[id].Label+".pdf");doc.Save(o);if(id=="pdf.compress"&&new FileInfo(o).Length>=new FileInfo(f).Length)throw new IOException(L.T("無法縮小此 PDF，沒有輸出更大的檔案"));return new([o],L.T("已重建為圖片 PDF，文字、表單與連結不再可編輯"));}
    }
    public static string WriteTextDocument(string text,string fmt,string tmp)
    {
        var o=Path.Combine(tmp,L.T("文字.")+fmt);string esc=System.Net.WebUtility.HtmlEncode(text);
        if(fmt=="html")File.WriteAllText(o,"<!doctype html><meta charset=\"utf-8\"><pre>"+esc+"</pre>");
        else if(fmt=="rtf"){var b=new StringBuilder(@"{\rtf1\ansi\deff0 ");foreach(char c in text){if(c=='\n')b.Append(@"\par ");else if(c=='\\'||c=='{'||c=='}')b.Append('\\').Append(c);else if(c>127)b.Append(@"\u").Append((short)c).Append('?');else b.Append(c);}File.WriteAllText(o,b.Append('}').ToString(),Encoding.ASCII);}
        else if(fmt=="docx")
        {
            using var zip=ZipFile.Open(o,ZipArchiveMode.Create);void Entry(string name,string data){using var w=new StreamWriter(zip.CreateEntry(name).Open());w.Write(data);}
            Entry("[Content_Types].xml","<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>");
            Entry("_rels/.rels","<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");
            XNamespace w="http://schemas.openxmlformats.org/wordprocessingml/2006/main";Entry("word/document.xml",new XDocument(new XElement(w+"document",new XElement(w+"body",text.Split('\n').Select(l=>new XElement(w+"p",new XElement(w+"r",new XElement(w+"t",new XAttribute(XNamespace.Xml+"space","preserve"),l))))))).ToString());
        }
        else File.WriteAllText(o,text);return o;
    }
    static async Task<string> DocumentText(string f,string tmp,CancellationToken ct)
    {
        var ext=Path.GetExtension(f).ToLowerInvariant();if(ext==".txt"||ext==".md"||ext==".markdown")return await File.ReadAllTextAsync(f,ct);
        if(ext==".docx"){using var zip=ZipFile.OpenRead(f);using var s=(zip.GetEntry("word/document.xml")??throw new IOException(L.T("無效 DOCX"))).Open();var xml=XDocument.Load(s);XNamespace w="http://schemas.openxmlformats.org/wordprocessingml/2006/main";return string.Join("\n",xml.Descendants(w+"p").Select(p=>string.Concat(p.Descendants(w+"t").Select(t=>t.Value))));}
        var target=await LibreOffice(f,"txt:Text (encoded):UTF8",tmp,ct);return await File.ReadAllTextAsync(target,ct);
    }
    static async Task<string> LibreOffice(string f,string format,string tmp,CancellationToken ct)
    {
        string profile=new Uri(Path.Combine(tmp,"lo-profile")).AbsoluteUri;
        await Tool.Run("soffice",["-env:UserInstallation="+profile,"--headless","--convert-to",format,"--outdir",tmp,f],ct);
        string output=Path.Combine(tmp,Path.GetFileNameWithoutExtension(f)+"."+format.Split(':')[0]);if(!File.Exists(output))throw new IOException(L.T("LibreOffice 未產生輸出，請確認此文件格式可讀取"));return output;
    }
    static async Task<RunResult> Document(string id,string f,string tmp,CancellationToken ct)
    {
        if(id=="doc.strip")
        {
            var o=Path.Combine(tmp,L.T("去隱私.docx"));File.Copy(f,o);using(var zip=ZipFile.Open(o,ZipArchiveMode.Update))foreach(var name in new[]{"docProps/core.xml","docProps/app.xml","docProps/custom.xml"}){var entry=zip.GetEntry(name);if(entry==null)continue;XDocument xml;using(var s=entry.Open())xml=XDocument.Load(s);var remove=new[]{"creator","lastModifiedBy","created","modified","Company","Manager","LastPrinted","lastPrinted","revision"};foreach(var el in xml.Descendants().Where(e=>name.EndsWith("custom.xml")?e.Name.LocalName=="property":remove.Contains(e.Name.LocalName)).ToList())el.Remove();entry.Delete();using var w=new StreamWriter(zip.CreateEntry(name).Open());w.Write(xml.ToString());}
            return new([o],L.T("已清除文件屬性；不移除正文、註解與修訂記錄"));
        }
        if(id=="doc.copy"||id=="doc.count"||new[]{"doc.to.txt","doc.to.md","doc.to.html"}.Contains(id))
        {
            string text=await DocumentText(f,tmp,ct);if(id=="doc.count")return new([],L.F("{0} 字元 · {1} 詞段",text.Length,Regex.Matches(text,@"\S+").Count));if(id=="doc.copy")return new([],L.T("已複製全文"),text);
            return new([WriteTextDocument(text,id.Split('.').Last(),tmp)],id=="doc.to.md"?L.T("純文字 Markdown；未重建標題層級"):"");
        }
        return new([await LibreOffice(f,id.Split('.').Last(),tmp,ct)],"");
    }
    static async Task<RunResult> Media(string id,string f,string tmp,CancellationToken ct)
    {
        bool video=id.StartsWith("vid.");var op=id.Split('.').Last();string ext=op;
        var args=new List<string>{"-hide_banner","-nostdin","-loglevel","error","-y","-i",f};
        if(video)
        {
            switch(op){
                case "mp4":case "mov":args.AddRange(["-c:v","libx264","-crf","20","-pix_fmt","yuv420p","-c:a","aac"]);break;
                case "hevc":ext="mp4";args.AddRange(["-c:v","libx265","-crf","24","-tag:v","hvc1","-c:a","aac"]);break;
                case "webm":args.AddRange(["-c:v","libvpx-vp9","-crf","32","-b:v","0","-c:a","libopus"]);break;
                case "gif":args.AddRange(["-t","30","-filter_complex","fps=12,scale=640:-1:flags=lanczos,split[a][b];[a]palettegen[p];[b][p]paletteuse"]);break;
                case "mute":ext=Path.GetExtension(f).TrimStart('.');args.AddRange(["-c:v","copy","-an"]);break;
                case "strip":ext=Path.GetExtension(f).TrimStart('.');args.AddRange(["-map","0","-map_metadata","-1","-map_metadata:s","-1","-map_chapters","-1","-c","copy"]);break;
                case "cover":ext="jpg";args.AddRange(["-frames:v","1","-q:v","2"]);break;
                case "compress":case "720p":ext="mp4";args.AddRange(["-vf",op=="720p"?"scale=-2:'min(720,ih)'":"scale=-2:'min(1080,ih)'","-c:v","libx264","-crf",op=="compress"?"28":"23","-c:a","aac","-b:a","128k"]);break;
                case "fast":case "slow":ext="mp4";args.AddRange(["-vf",op=="fast"?"setpts=0.5*PTS":"setpts=2*PTS","-af",op=="fast"?"atempo=2":"atempo=0.5","-c:v","libx264","-c:a","aac"]);break;
                default:AddAudio(args,op);break;
            }
        }
        else
        {
            if(new[]{"compress","mono","loud","fast"}.Contains(op)){ext="m4a";args.AddRange(["-vn","-c:a","aac","-b:a",op=="compress"?"96k":"192k"]);if(op=="mono")args.AddRange(["-ac","1"]);if(op=="loud")args.AddRange(["-af","loudnorm=I=-16:TP=-1.5:LRA=11"]);if(op=="fast")args.AddRange(["-af","atempo=1.5"]);}
            else {if(op=="alac")ext="m4a";AddAudio(args,op);}
        }
        var o=Path.Combine(tmp,Catalog.All[id].Label.Replace('/','-')+"."+ext);args.Add(o);await Tool.Run("ffmpeg",args,ct);return new([o],"");
    }
    static void AddAudio(List<string> args,string format)
    {
        args.Add("-vn");args.AddRange(format switch {"mp3"=>["-c:a","libmp3lame","-q:a","2"],"m4a"=>["-c:a","aac","-b:a","192k"],"m4r"=>["-t","40","-c:a","aac","-b:a","192k","-f","ipod"],"wav"=>["-c:a","pcm_s16le"],"flac"=>["-c:a","flac"],"aiff"=>["-c:a","pcm_s16be"],"alac"=>["-c:a","alac"],"opus"=>["-c:a","libopus","-b:a","128k"],_=>throw new ArgumentException(L.T("未知音訊格式"))});
    }
}
