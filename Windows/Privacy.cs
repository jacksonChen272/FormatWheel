using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.IO.Compression;
using System.Text.Json;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using ImageMagick;
using PdfSharp.Pdf.IO;

namespace FormatWheel;

public enum PrivacyScanState { Checked, Partial, Unsupported, Error }

// Preserve the positional constructor and two-value deconstruction for existing callers.
public record PrivacyResult(int Level,string Detail)
{
    public PrivacyScanState State {get;init;} = PrivacyScanState.Unsupported;
    public int CheckedCount {get;init;}
    public int TotalCount {get;init;}
    public int PartialCount {get;init;}
    public int UnsupportedCount {get;init;}
    public int ErrorCount {get;init;}
    public int SkippedCount {get;init;}
    public int AttemptedCount => TotalCount-SkippedCount;
    public int UnknownCount => TotalCount-CheckedCount;
    public IReadOnlyList<string> Reasons {get;init;} = Array.Empty<string>();

    // Positive findings remain useful in a partial scan. Zero is meaningful only
    // when every input was supported and successfully checked.
    public bool CanRate => Complete || Level>0;
    public bool VerifiedClean => Complete && Level==0;
    bool Complete => State==PrivacyScanState.Checked && TotalCount>0 && CheckedCount==TotalCount
        && PartialCount==0 && UnsupportedCount==0 && ErrorCount==0 && SkippedCount==0;
    public string StatusText => State switch
    {
        PrivacyScanState.Checked => L.Text("已檢查","Checked"),
        PrivacyScanState.Partial => L.Text("部分檢查","Partially checked"),
        PrivacyScanState.Error => L.Text("檢查失敗","Scan failed"),
        _ => L.Text("未支援檢查","Scan unsupported")
    };
}

public static class Privacy
{
    public const int ScanLimit=24;
    public static PrivacyResult Scan(string[] files) => Scan(files,ProbeVideo);

    // Per-call injection tests missing tools, timeouts and probe responses without
    // modifying PATH or shared application state. Null means ffprobe is missing.
    internal static PrivacyResult Scan(string[] files,Func<string,string?> probeVideo)
    {
        ArgumentNullException.ThrowIfNull(files);
        var reasons=new List<string>();
        int max=0,checkedCount=0,partial=0,unsupported=0,errors=0;
        foreach(var file in files.Take(ScanLimit))
        {
            var scan=new FileScan();
            try
            {
                var kind=Catalog.Kind(file);
                if(kind=="img") ScanImage(file,scan);
                else if(kind=="pdf") ScanPdf(file,scan);
                else if(kind=="doc" && Path.GetExtension(file).Equals(".docx",StringComparison.OrdinalIgnoreCase)) ScanDocx(file,scan);
                else if(kind=="vid")
                {
                    var json=probeVideo(file);
                    if(json==null) scan.Unsupported(L.Text("缺少 ffprobe，未檢查影片","ffprobe missing; video not checked"));
                    else ScanVideo(json,scan);
                }
                else scan.Unsupported(L.Text("此格式不支援隱私檢查","Privacy scanning is unsupported for this format"));
            }
            catch(OperationCanceledException) { scan.Fail(L.Text("檢查逾時","Scan timed out")); }
            catch(Exception) { scan.Fail(L.Text("無法讀取檔案或中繼資料","Could not read the file or its metadata")); }
            max=Math.Max(max,scan.Score);
            foreach(var reason in scan.Reasons) if(!reasons.Contains(reason)) reasons.Add(reason);
            switch(scan.State)
            {
                case PrivacyScanState.Checked: checkedCount++;break;
                case PrivacyScanState.Partial: partial++;break;
                case PrivacyScanState.Unsupported: unsupported++;break;
                case PrivacyScanState.Error: errors++;break;
            }
        }
        int skipped=Math.Max(0,files.Length-ScanLimit);
        var state=files.Length==0 || unsupported==files.Length ? PrivacyScanState.Unsupported
            : checkedCount==files.Length ? PrivacyScanState.Checked
            : errors==files.Length ? PrivacyScanState.Error : PrivacyScanState.Partial;
        if(files.Length==0) reasons.Add(L.Text("沒有可檢查的檔案","No files to scan"));
        if(skipped>0) reasons.Add(string.Format(L.Text("每批最多檢查 {0} 個；另有 {1} 個未檢查","Up to {0} files per batch; {1} not scanned"),ScanLimit,skipped));
        if(state==PrivacyScanState.Checked && max==0) reasons.Add(L.Text("未發現常見隱私中繼資料","No common privacy metadata detected"));
        reasons.Add(L.Text("僅檢查常見中繼資料；不代表匿名","Checks common metadata only; does not guarantee anonymity"));
        var coverage=string.Format(L.Text("已檢查 {0}/{1}","Checked {0}/{1}"),checkedCount,files.Length);
        if(partial>0) coverage+=" · "+string.Format(L.Text("部分檢查 {0}","Partially checked {0}"),partial);
        if(unsupported>0) coverage+=" · "+string.Format(L.Text("未支援 {0}","Unsupported {0}"),unsupported);
        if(errors>0) coverage+=" · "+string.Format(L.Text("失敗 {0}","Failed {0}"),errors);
        return new(Math.Min(max,5),coverage+" · "+string.Join(" · ",reasons))
        {
            State=state,CheckedCount=checkedCount,TotalCount=files.Length,PartialCount=partial,
            UnsupportedCount=unsupported,ErrorCount=errors,SkippedCount=skipped,
            Reasons=Array.AsReadOnly(reasons.ToArray())
        };
    }

    sealed class FileScan
    {
        readonly HashSet<string> categories=new(StringComparer.Ordinal);
        public int Score {get;private set;}
        public PrivacyScanState State {get;private set;} = PrivacyScanState.Checked;
        public List<string> Reasons {get;} = new();
        public void Add(string category,int weight,string reason)
        {
            if(!categories.Add(category)) return;
            Score+=weight;Reasons.Add(reason);
        }
        public void Partial(string reason) { State=PrivacyScanState.Partial;Reasons.Add(reason); }
        public void Unsupported(string reason) { State=PrivacyScanState.Unsupported;Reasons.Add(reason); }
        public void Fail(string reason)
        {
            State=Score>0 || State==PrivacyScanState.Partial ? PrivacyScanState.Partial : PrivacyScanState.Error;
            Reasons.Add(reason);
        }
    }

    static void Gps(FileScan scan) => scan.Add("gps",2,L.Text("GPS 位置","GPS location"));
    static void Device(FileScan scan) => scan.Add("device",1,L.Text("拍攝裝置","Capture device"));
    static void CaptureTime(FileScan scan) => scan.Add("capture-time",1,L.Text("拍攝時間","Capture time"));
    static void AuthorOrSerial(FileScan scan) => scan.Add("author-serial",1,L.Text("作者 / 序號","Author / serial number"));
    static bool HasValue(object? value) => value switch
    {
        null => false,
        string text => !string.IsNullOrWhiteSpace(text.Trim('\0')),
        Array values => values.Cast<object?>().Any(HasValue),
        _ => true
    };

    static void ScanImage(string file,FileScan scan)
    {
        using var image=new MagickImage();
        bool warning=false;
        image.Warning+=(_,_)=>warning=true;
        image.Read(file);
        try
        {
            var exif=image.GetExifProfile();
            if(exif!=null)
            {
                foreach(var value in exif.Values)
                    if(HasValue(value.GetValue())) AddExifTag(value.Tag.ToString(),scan);
                if(exif.InvalidTags.Count>0) scan.Partial(L.Text("部分 EXIF 標籤無法讀取","Some EXIF tags could not be read"));
            }
        }
        catch(Exception) { scan.Partial(L.Text("EXIF 資料無法完整讀取","EXIF metadata could not be fully read")); }
        try
        {
            var xmp=image.GetXmpProfile();
            if(xmp!=null)
            {
                using var stream=new MemoryStream(xmp.ToByteArray());
                ScanImageXmp(ReadXml(stream),scan);
            }
        }
        catch(Exception) { scan.Partial(L.Text("XMP 資料無法完整讀取","XMP metadata could not be fully read")); }
        if(warning) scan.Partial(L.Text("圖片讀取有警告，檢查可能不完整","Image reader reported a warning; scan may be incomplete"));
    }

    static void AddExifTag(string tag,FileScan scan)
    {
        switch(tag)
        {
            case "GPSLatitude": case "GPSLongitude": case "GPSDestLatitude": case "GPSDestLongitude":
                Gps(scan);break;
            case "Make": case "Model": case "LensMake": case "LensModel":
                Device(scan);break;
            case "DateTimeOriginal": CaptureTime(scan);break;
            case "Artist": case "CameraOwnerName": case "OwnerName": case "BodySerialNumber": case "SerialNumber": case "LensSerialNumber":
                AuthorOrSerial(scan);break;
        }
    }

    static void ScanImageXmp(XDocument document,FileScan scan)
    {
        const string rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        if(!document.Descendants(XName.Get("RDF",rdf)).Any()) throw new InvalidDataException("Invalid XMP RDF packet.");
        void Property(XName name,string value)
        {
            if(!HasValue(value)) return;
            switch(name.NamespaceName)
            {
                case "http://ns.adobe.com/exif/1.0/":
                    if(name.LocalName is "GPSLatitude" or "GPSLongitude" or "GPSDestLatitude" or "GPSDestLongitude" or "DateTimeOriginal") AddExifTag(name.LocalName,scan);
                    break;
                case "http://ns.adobe.com/tiff/1.0/":
                    if(name.LocalName is "Make" or "Model" or "Artist") AddExifTag(name.LocalName,scan);
                    break;
                case "http://cipa.jp/exif/1.0/":
                case "http://ns.adobe.com/exif/1.0/aux/":
                    if(name.LocalName is "LensMake" or "LensModel" or "CameraOwnerName" or "OwnerName" or "BodySerialNumber" or "SerialNumber" or "LensSerialNumber") AddExifTag(name.LocalName,scan);
                    break;
                case "http://purl.org/dc/elements/1.1/":
                    if(name.LocalName=="creator") AuthorOrSerial(scan);
                    break;
                case "http://ns.adobe.com/photoshop/1.0/":
                    if(name.LocalName=="DateCreated") CaptureTime(scan);
                    break;
            }
        }
        foreach(var element in document.Descendants())
        {
            // RDF permits attribute and element properties, including dc:creator arrays.
            Property(element.Name,element.Value);
            foreach(var attribute in element.Attributes().Where(a=>!a.IsNamespaceDeclaration)) Property(attribute.Name,attribute.Value);
            var resource=element.Attribute(XName.Get("resource",rdf));
            if(resource!=null) Property(element.Name,resource.Value);
        }
    }

    static XDocument ReadXml(Stream stream)
    {
        using var reader=XmlReader.Create(stream,new XmlReaderSettings
        {
            DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4*1024*1024
        });
        return XDocument.Load(reader);
    }

    static void ScanPdf(string file,FileScan scan)
    {
        using var pdf=PdfReader.Open(file,PdfDocumentOpenMode.Import);
        if(HasValue(pdf.Info.Author)) scan.Add("pdf-author",2,L.Text("PDF 作者","PDF author"));
        if(HasValue(pdf.Info.Creator)) scan.Add("pdf-creator",1,L.Text("PDF 製作軟體","PDF creator"));
        if(HasValue(pdf.Info.Subject)||HasValue(pdf.Info.Keywords)) scan.Add("pdf-subject-keywords",1,L.Text("PDF 主旨 / 關鍵字","PDF subject / keywords"));
    }

    static void ScanDocx(string file,FileScan scan)
    {
        using var zip=ZipFile.OpenRead(file);
        if(zip.GetEntry("[Content_Types].xml")==null || zip.GetEntry("word/document.xml")==null)
            throw new InvalidDataException("Not a Word document package.");
        foreach(var part in new[]{"docProps/core.xml","docProps/app.xml"})
        {
            var entries=zip.Entries.Where(e=>e.FullName==part).ToArray();
            if(entries.Length>1) scan.Partial(L.Text("文件屬性重複，檢查可能不完整","Duplicate document properties; scan may be incomplete"));
            foreach(var entry in entries) try
            {
                using var stream=entry.Open();
                var doc=ReadXml(stream);
                if(part=="docProps/core.xml")
                {
                    XNamespace cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties";
                    XNamespace dc="http://purl.org/dc/elements/1.1/";
                    if(doc.Root?.Name!=cp+"coreProperties") throw new InvalidDataException("Invalid core properties.");
                    var creators=doc.Root.Elements(dc+"creator").Select(e=>e.Value.Trim()).Where(HasValue).ToArray();
                    if(creators.Length>0) scan.Add("docx-creator",2,L.Text("Word 作者","Word creator"));
                    if(doc.Root.Elements(cp+"lastModifiedBy").Select(e=>e.Value.Trim()).Any(value=>HasValue(value)&&!creators.Contains(value,StringComparer.OrdinalIgnoreCase)))
                        scan.Add("docx-modifier",1,L.Text("Word 不同的最後修改者","Word distinct last modifier"));
                }
                else
                {
                    XNamespace ep=doc.Root?.Name.Namespace??XNamespace.None;
                    if(doc.Root?.Name.LocalName!="Properties" || (ep.NamespaceName!="http://schemas.openxmlformats.org/officeDocument/2006/extended-properties" && ep.NamespaceName!="http://purl.oclc.org/ooxml/officeDocument/extendedProperties"))
                        throw new InvalidDataException("Invalid extended properties.");
                    if(doc.Root.Elements(ep+"Company").Any(e=>HasValue(e.Value))) scan.Add("docx-company",1,L.Text("Word 公司","Word company"));
                }
            }
            catch(Exception) { scan.Partial(L.Text("Word 文件屬性無法完整讀取","Word properties could not be fully read")); }
        }
    }

    static string? ProbeVideo(string file)
    {
        var tool=Tool.Find("ffprobe");
        if(tool==null) return null;
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));
        return Tool.Run(tool,new[]{"-v","error","-show_entries","format_tags:stream_tags","-of","json",file},timeout.Token).GetAwaiter().GetResult();
    }

    static void ScanVideo(string json,FileScan scan)
    {
        using var document=JsonDocument.Parse(json);
        var root=document.RootElement;
        if(root.ValueKind!=JsonValueKind.Object || root.TryGetProperty("error",out _)) throw new InvalidDataException("Invalid ffprobe response.");
        bool hasContainer=false;
        void Tags(JsonElement container)
        {
            if(container.ValueKind!=JsonValueKind.Object) throw new InvalidDataException("Invalid ffprobe container.");
            if(!container.TryGetProperty("tags",out var tags)) return;
            if(tags.ValueKind!=JsonValueKind.Object) throw new InvalidDataException("Invalid ffprobe tags.");
            foreach(var property in tags.EnumerateObject())
            {
                if(property.Value.ValueKind!=JsonValueKind.String) throw new InvalidDataException("Invalid ffprobe tag value.");
                if(!HasValue(property.Value.GetString())) continue;
                var tag=property.Name.ToLowerInvariant();
                switch(tag)
                {
                    case "location": case "location.iso6709": case "com.apple.quicktime.location.iso6709":
                    case "gpslatitude": case "gpslongitude": case "gpscoordinates":
                        Gps(scan);break;
                    case "make": case "model": case "camera_make": case "camera_model":
                    case "com.apple.quicktime.make": case "com.apple.quicktime.model":
                        Device(scan);break;
                    // creation_time/date often describe encoding or exporting, not capture.
                    case "com.apple.quicktime.creationdate": case "datetimeoriginal": case "date_time_original": case "exif:datetimeoriginal":
                        CaptureTime(scan);break;
                    default:
                        if(tag.StartsWith("location-",StringComparison.Ordinal) && tag.Length==12 && tag[9..].All(c=>c>='a'&&c<='z')) Gps(scan);
                        break;
                }
            }
        }
        if(root.TryGetProperty("format",out var format)) { Tags(format);hasContainer=true; }
        if(root.TryGetProperty("streams",out var streams))
        {
            if(streams.ValueKind!=JsonValueKind.Array) throw new InvalidDataException("Invalid ffprobe streams.");
            foreach(var stream in streams.EnumerateArray()) { Tags(stream);hasContainer=true; }
        }
        if(!hasContainer) throw new InvalidDataException("ffprobe returned no media metadata containers.");
    }
}
