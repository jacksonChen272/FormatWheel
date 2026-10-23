using System;
using System.IO;
using System.Linq;

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
    public static string Title(ActionItem item,string[] sources)
        => (sources.Length==1?Path.GetFileName(sources[0]):L.F("{0} 個檔案",sources.Length))+" → "+item.DisplayLabel(sources);
    public static string SavedLocations(string[] outputs)
        => outputs.Length==0?"":L.Text("儲存在：","Saved to: ")+string.Join("\n",outputs.Select(p=>Path.GetDirectoryName(Path.GetFullPath(p))??p).Distinct(StringComparer.OrdinalIgnoreCase));
    public static void Show(string title,string detail,string sizeChange,PrivacyResult? before,PrivacyResult? after)
        => FeedbackBanner.ShowResult(FeedbackOutcome.Success,title,detail,sizeChange,before,after);
}
