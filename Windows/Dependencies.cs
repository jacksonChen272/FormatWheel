using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace FormatWheel;

public record Dependency(string Id,string Name,string Version,string Url,string Hash,long Bytes,string Executable);
public record InstallProgress(string Message,double? Percent=null,bool CanCancel=true);
public static class Dependencies
{
    public static readonly Dependency FFmpeg = new("ffmpeg","FFmpeg","9.0.2","https://www.gyan.dev/ffmpeg/builds/packages/ffmpeg-9.0.2-essentials_build.zip","60f467265b1e312373dbcd92200c2618a74850f98d3d078e94296bb3fa2047ba",114768076,"ffmpeg");
    public static readonly Dependency LibreOffice = new("libreoffice","LibreOffice","26.8.1","https://downloadarchive.documentfoundation.org/libreoffice/old/26.8.1.1/win/x86_64/LibreOffice_26.8.1.1_Win_x86-64.msi","c6298b10cfa1748bcbedad94c62ce771e240e5021bfd6d7a967cd571b04b5312",374870016,"soffice");
    static readonly SemaphoreSlim Gate=new(1,1);
    static readonly HttpClient Http=new(new HttpClientHandler{AllowAutoRedirect=true}){Timeout=TimeSpan.FromMinutes(30)};
    public static string Root => Path.Combine(Settings.Folder,"tools");
    public static async Task<bool> Ready(Dependency dependency,CancellationToken token=default)
    {
        var path=Tool.Find(dependency.Executable);if(path==null)return false;
        try{using var limit=CancellationTokenSource.CreateLinkedTokenSource(token);limit.CancelAfter(TimeSpan.FromSeconds(20));
            string result=await Tool.Run(path,[dependency.Id=="ffmpeg"?"-version":"--version"],limit.Token);
            if(!result.Contains(dependency.Name,StringComparison.OrdinalIgnoreCase))return false;
            if(dependency.Id=="ffmpeg")return Tool.Find("ffprobe") is string probe && (await Tool.Run(probe,["-version"],limit.Token)).Contains("ffprobe version");
            return true;
        }catch{return false;}
    }
    public static async Task Verify(string path,string expected,CancellationToken token=default)
    {
        using var f=File.OpenRead(path);var actual=Convert.ToHexString(await SHA256.HashDataAsync(f,token));
        if(!actual.Equals(expected,StringComparison.OrdinalIgnoreCase))throw new IOException(L.Text("下載檔案校驗失敗，請重試。","The download failed its integrity check. Please retry."));
    }
    public static async Task Install(Dependency dependency,IProgress<InstallProgress> progress,CancellationToken token=default,string? verifiedPackage=null)
    {
        await Gate.WaitAsync(token);
        // MSI still has legacy path limits, so extraction uses a short private temp path.
        string stage=Path.Combine(Path.GetTempPath(),"FormatWheel-install-"+Guid.NewGuid().ToString("N"));
        try
        {
            if(await Ready(dependency,token)){progress.Report(new(L.Text("已可使用","Ready"),100));return;}
            Directory.CreateDirectory(stage);Directory.CreateDirectory(Root);
            string archive=Path.Combine(stage,dependency.Id=="ffmpeg"?"download.zip":"download.msi");
            if(verifiedPackage!=null)File.Copy(verifiedPackage,archive);else
            {
                progress.Report(new(L.Text("正在下載","Downloading")+" · "+dependency.Name,0));
                using var response=await Http.GetAsync(dependency.Url,HttpCompletionOption.ResponseHeadersRead,token);response.EnsureSuccessStatusCode();
                if(response.RequestMessage?.RequestUri?.Scheme!="https")throw new IOException("HTTPS is required.");
                await using var input=await response.Content.ReadAsStreamAsync(token);await using var output=File.Create(archive);
                var buffer=new byte[131072];long total=0;int read;var last=DateTime.MinValue;
                while((read=await input.ReadAsync(buffer,token))>0){total+=read;if(total>dependency.Bytes)throw new IOException("Unexpected download size.");await output.WriteAsync(buffer.AsMemory(0,read),token);if((DateTime.UtcNow-last).TotalMilliseconds>150){progress.Report(new($"{L.Text("正在下載","Downloading")} {dependency.Name} · {total/1048576.0:F1} / {dependency.Bytes/1048576.0:F1} MB",total*100.0/dependency.Bytes));last=DateTime.UtcNow;}}
                if(total!=dependency.Bytes)throw new IOException(L.Text("下載未完成，請重試。","The download is incomplete. Please retry."));
            }
            progress.Report(new(L.Text("正在校驗下載檔案","Verifying download")));
            await Verify(archive,dependency.Hash,token);token.ThrowIfCancellationRequested();
            string payload=Path.Combine(stage,"payload");Directory.CreateDirectory(payload);
            progress.Report(new(L.Text("正在安裝，請稍候…","Installing, please wait…"),null,false));
            if(dependency.Id=="ffmpeg")
            {
                await Task.Run(()=>ZipFile.ExtractToDirectory(archive,payload),CancellationToken.None);
                payload=Directory.GetDirectories(payload).Single();
            }
            else
            {
                // Administrative extraction creates a private application image; it does not
                // register LibreOffice, replace the system installation, or change file associations.
                string msi=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"msiexec.exe");
                await Tool.Run(msi,["/a",archive,"/qn","/norestart","TARGETDIR="+payload,"/L*v",Path.Combine(Settings.Folder,"LibreOffice-install.log")]);
                // Keep bundled VC runtime private instead of installing it system-wide.
                string runtime=Path.Combine(payload,"System64");
                if(Directory.Exists(runtime))foreach(var dll in Directory.GetFiles(runtime,"*.dll")){var target=Path.Combine(payload,"program",Path.GetFileName(dll));if(!File.Exists(target))File.Copy(dll,target);}
            }
            var exe=Path.Combine(payload,dependency.Id=="ffmpeg"?"bin":"program",dependency.Executable+(dependency.Id=="ffmpeg"?".exe":".com"));
            using(var limit=new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            {
                var version=await Tool.Run(exe,[dependency.Id=="ffmpeg"?"-version":"--version"],limit.Token);
                if(!version.Contains(dependency.Name,StringComparison.OrdinalIgnoreCase))throw new IOException(L.Text("安裝後檢查失敗。","Post-install verification failed."));
                if(dependency.Id=="ffmpeg")await Tool.Run(Path.Combine(payload,"bin","ffprobe.exe"),["-version"],limit.Token);
            }
            string destination=Path.Combine(Root,dependency.Id),backup=destination+".previous-"+Guid.NewGuid().ToString("N");
            bool hadOld=Directory.Exists(destination);if(hadOld)Directory.Move(destination,backup);
            try{if(Path.GetPathRoot(payload)==Path.GetPathRoot(destination))Directory.Move(payload,destination);else{Directory.CreateDirectory(destination);foreach(var f in Directory.EnumerateFiles(payload,"*",SearchOption.AllDirectories)){var target=Path.Combine(destination,Path.GetRelativePath(payload,f));Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(f,target);}}}catch{Clean(destination);if(hadOld)Directory.Move(backup,destination);throw;}
            if(hadOld)Clean(backup);
            progress.Report(new(L.Text("已安裝，可立即使用","Installed and ready to use"),100,false));
        }
        finally{Clean(stage);Gate.Release();}
    }
    static void Clean(string path){try{string full=Path.GetFullPath(path);bool ownTool=full.StartsWith(Path.GetFullPath(Root)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);bool ownTemp=Path.GetDirectoryName(full)==Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)&&Path.GetFileName(full).StartsWith("FormatWheel-install-",StringComparison.Ordinal);if(Directory.Exists(full)&&(ownTool||ownTemp))Directory.Delete(full,true);}catch(Exception e){Output.Log(e.Message);}}
}
