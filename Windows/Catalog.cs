using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace FormatWheel;

public record ActionItem(string Id, string RawLabel, string RawDetail, string RawAmmo = "")
{
    public string Label => L.T(RawLabel);
    public string Detail => L.T(RawDetail);
    public string Ammo => L.T(RawAmmo);
    public string DisplayLabel(string[] files) => Id=="img.to.pdf"&&files.Length>1?L.Text("合併成 PDF","Merge into PDF"):Label;
    public string DisplayDetail(string[] files) => Id=="img.to.pdf"&&files.Length>1
        ?string.Format(L.Text("{0} 張圖片 → 1 份 PDF，共 {0} 頁；依檔案順序，每張一頁。","{0} images → 1 PDF, {0} pages; one image per page, in file order."),files.Length):Detail;
    public string? Unavailable(string[] files)
    {
        if (Id == "pdf.merge" && files.Length < 2) return L.T("請同時拖入兩個以上 PDF");
        if (Id == "doc.strip" && files.Any(f => Path.GetExtension(f).ToLowerInvariant() != ".docx")) return L.T("僅支援 DOCX");
        if (Id == "gen.sha" && files.Any(Directory.Exists)) return L.T("請選擇檔案，不包含資料夾");
        if (Id.StartsWith("vid.") || Id.StartsWith("aud."))
            if (Tool.Find("ffmpeg") == null) return L.T("未裝備：請安裝 FFmpeg，或放入 tools/ffmpeg/");
        if (Id == "img.cutout" && !File.Exists(Assets.File("u2netp.onnx"))) return L.T("未裝備：缺少 u2netp.onnx 模型");
        if (Id.StartsWith("doc.to.") && Tool.Find("soffice") == null && !new[]{"txt","md","html"}.Contains(Id.Split('.').Last()))
            return L.T("未裝備：需要 LibreOffice");
        return null;
    }
}

public static class Catalog
{
    public static string Kind(string path)
    {
        if (Directory.Exists(path)) return "gen";
        var e = Path.GetExtension(path).ToLowerInvariant().TrimStart('.');
        if ("png jpg jpeg heic heif webp avif gif tif tiff bmp ico jxl".Split(' ').Contains(e)) return "img";
        if (e == "pdf") return "pdf";
        if ("mp4 mov m4v mkv avi webm mpg mpeg wmv ts".Split(' ').Contains(e)) return "vid";
        if ("mp3 m4a aac wav flac aif aiff opus ogg alac m4r wma".Split(' ').Contains(e)) return "aud";
        if ("txt md markdown html htm rtf doc docx odt".Split(' ').Contains(e)) return "doc";
        return "gen";
    }
    public static string Name(string kind) => kind switch {"img"=>L.T("圖片"), "pdf"=>"PDF", "vid"=>L.T("影片"), "aud"=>L.T("音訊"), "doc"=>L.T("文件"), _=>L.T("檔案")};
    public static readonly Dictionary<string, ActionItem> All = new();
    static ActionItem A(string id,string label,string detail,string ammo="") { var a=new ActionItem(id,label,detail,ammo); All[id]=a; return a; }
    static ActionItem?[] Formats(string kind, string list, string[] descriptions, string[]? ammo=null) => list.Split(' ').Select((f,i)=>(ActionItem?)A(kind+".to."+f, f.ToUpperInvariant(), descriptions[i], ammo?[i]??"轉換")).ToArray();
    static ActionItem?[] Tools(string kind, string ids, string labels, string[] descriptions) => ids.Split(' ').Select((id,i)=>id=="-"?null:A(id.Contains('.')?id:kind+"."+id,labels.Split('|')[i],descriptions[i],labels.Split('|')[i])).ToArray();
    public static readonly Dictionary<string, ActionItem?[][]> Pages = new();
    static Catalog()
    {
        Pages["img"] = [Formats("img","png jpg heic webp pdf avif gif tiff",["無損畫質，支援透明背景","到哪都能打開，體積適中","Apple 相片格式，小而清晰","網頁常用，小體積高畫質","一張圖一頁，多張合併為 PDF","新一代圖片格式","256 色圖片","無損，適合印刷與存檔"],["無損","通用","Apple","網頁","文件","極小","256色","印刷"]),Tools("img","compress half cutout ocr mono rotate strip backdrop","壓縮|縮小一半|去背|識字|黑白|旋轉|去隱私|加背景",["降低圖片檔案大小","長寬各減半","本機模型識別主體，輸出透明 PNG","辨識文字並複製到剪貼簿","轉成灰階圖片","順時針旋轉 90°","移除 EXIF、GPS 等中繼資料","加上漸層背景、圓角與陰影"])];
        Pages["pdf"] = [Formats("pdf","png jpg long docx txt md rtf html",["每頁輸出一張 PNG","每頁輸出一張 JPG","將頁面垂直拼接","提取文字為可編輯 Word（不保留原頁面布局）","提取純文字","提取為 Markdown 文字","提取文字為 RTF","提取文字為網頁"]),Tools("pdf","compress split merge rotate mono ocr strip gen.zip","壓縮|拆頁|合併|旋轉|黑白|識字|去隱私|ZIP",["重建為圖片 PDF，文字與連結將失去可編輯性","每頁拆成一個 PDF","按選取順序合併 PDF","頁面順時針旋轉 90°","重建為灰階圖片 PDF","辨識掃描頁面文字","移除文件資訊及 XMP；不等於內容塗黑","打包成 ZIP"])];
        Pages["vid"] = [Formats("vid","mp4 mov hevc webm gif mp3 m4a wav",["通用 H.264 影片","MOV 影片","H.265 / HEVC","WebM 影片","前 30 秒轉成動圖","擷取 MP3 音訊","擷取 M4A 音訊","擷取 WAV 音訊"]),Tools("vid","compress mute 720p cover fast slow strip gen.zip","壓縮|靜音|720P|截封面|2×|0.5×|去隱私|ZIP",["最高 1080P，H.264 壓縮","移除音軌，不重壓畫面","縮至最高 720P","擷取第一幀 JPG","畫面和聲音同步加速","畫面和聲音同步慢放","移除容器中繼資料，不重壓畫面","打包成 ZIP"])];
        Pages["aud"] = [Formats("aud","mp3 m4a wav flac aiff opus alac m4r",["通用 MP3","AAC 音訊","無損 WAV","無損壓縮","AIFF 音訊","Opus 音訊","Apple 無損","前 40 秒 iPhone 鈴聲"]),Tools("aud","compress mono loud fast gen.zip gen.path - -","壓縮|單聲道|響度統一|1.5×|ZIP|複製路徑||",["96 kbps AAC","合併左右聲道","標準化至 -16 LUFS","加速至 1.5 倍","打包成 ZIP","複製完整路徑","",""])];
        Pages["doc"] = [Formats("doc","pdf docx txt md html rtf odt doc",["固定排版文件","Word 文件","純文字","Markdown 文字","HTML 網頁","富文字文件","開放文件格式","舊版 Word"]),Tools("doc","count - copy - strip - gen.zip gen.path","字數||複製全文||去隱私||ZIP|複製路徑",["統計字元與詞數","","提取文字並複製","","移除作者、修改者、公司等文件屬性","","打包成 ZIP","複製完整路徑"])];
        Pages["gen"] = [Tools("gen","zip - path - name - sha -","ZIP||複製路徑||複製檔名||SHA-256|",["打包成 ZIP","","複製完整路徑","","複製檔案名稱","","計算 SHA-256 校驗值",""])];
    }
    public static string ForFiles(string[] files) => files.Select(Kind).Distinct().Count()==1?Kind(files[0]):"gen";
}
