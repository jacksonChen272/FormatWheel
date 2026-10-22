using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.IO.Compression;
using System.Xml.Linq;
using ImageMagick;
using PdfSharp.Pdf.IO;

namespace FormatWheel;
public record PrivacyResult(int Level,string Detail);
public static class Privacy
{
    public static PrivacyResult Scan(string[] files)
    {
        int max=0;var reasons=new HashSet<string>();
        foreach(var f in files.Take(24))try
        {
            int n=0;switch(Catalog.Kind(f))
            {
                case "img":using(var im=new MagickImage(f)){var profile=im.GetExifProfile();if(profile!=null){foreach(var v in profile.Values){var tag=v.Tag.ToString();if(tag=="GPSLatitude"){n+=2;reasons.Add("GPS 位置");}if(tag=="Model"){n++;reasons.Add("相機型號");}if(tag=="DateTimeOriginal"){n++;reasons.Add("拍攝時間");}if(tag=="BodySerialNumber"||tag=="Artist"){n++;reasons.Add("作者 / 序號");}}}if(im.GetXmpProfile()!=null){n++;reasons.Add("XMP 資料");}}break;
                case "pdf":using(var pdf=PdfReader.Open(f,PdfDocumentOpenMode.Import)){if(!string.IsNullOrWhiteSpace(pdf.Info.Author)){n+=2;reasons.Add("PDF 作者");}if(!string.IsNullOrWhiteSpace(pdf.Info.Creator)){n++;reasons.Add("製作軟體");}if(!string.IsNullOrWhiteSpace(pdf.Info.Keywords)){n++;reasons.Add("關鍵字");}}break;
                case "doc":if(Path.GetExtension(f).Equals(".docx",StringComparison.OrdinalIgnoreCase)){using var zip=ZipFile.OpenRead(f);foreach(var name in new[]{"docProps/core.xml","docProps/app.xml"}){var entry=zip.GetEntry(name);if(entry==null)continue;using var s=entry.Open();var doc=XDocument.Load(s);foreach(var el in doc.Descendants().Where(e=>new[]{"creator","lastModifiedBy","Company"}.Contains(e.Name.LocalName)&&!string.IsNullOrWhiteSpace(e.Value))){n+=el.Name.LocalName=="creator"?2:1;reasons.Add("Word 文件屬性");}}}break;
                case "vid":
                    if(Tool.Find("ffprobe")==null){reasons.Add("未裝備 ffprobe，未檢查影片");break;}
                    using(var timeout=new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5))) {var json=Tool.Run("ffprobe",new[]{"-v","error","-show_entries","format_tags:stream_tags","-of","json",f},timeout.Token).GetAwaiter().GetResult();using var doc=System.Text.Json.JsonDocument.Parse(json);var tags=new List<string>();void Walk(System.Text.Json.JsonElement el){if(el.ValueKind==System.Text.Json.JsonValueKind.Object)foreach(var property in el.EnumerateObject()){if(property.Value.ValueKind==System.Text.Json.JsonValueKind.String&&!string.IsNullOrEmpty(property.Value.GetString()))tags.Add(property.Name.ToLowerInvariant());else Walk(property.Value);}else if(el.ValueKind==System.Text.Json.JsonValueKind.Array)foreach(var child in el.EnumerateArray())Walk(child);}Walk(doc.RootElement);if(tags.Any(t=>t.Contains("location"))){n+=2;reasons.Add("影片位置");}if(tags.Any(t=>t.Contains("model"))){n++;reasons.Add("拍攝裝置");}if(tags.Any(t=>t.Contains("creation")||t.Contains("date"))){n++;reasons.Add("拍攝時間");}}
                    break;
            }max=Math.Max(max,n);
        }catch{reasons.Add("部分檔案無法檢查");}
        return new(Math.Min(max,5),reasons.Count==0?"僅檢查常見中繼資料；不代表匿名":string.Join(" · ",reasons));
    }
}
