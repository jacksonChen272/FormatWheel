using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Win32;

namespace FormatWheel;

public class Settings
{
    public bool Sound {get;set;} = true;
    public bool Blur {get;set;} = true;
    public bool Banner {get;set;} = true;
    public bool Weapons {get;set;}
    public string Language {get;set;} = "auto";
    public bool SetupPromptSeen {get;set;}
    public bool TourSeen {get;set;}
    public int Size {get;set;} = 285;
    public string Color {get;set;} = "#3380E6";
    public List<string> Recents {get;set;} = new();
    public static string Folder => Environment.GetEnvironmentVariable("FORMATWHEEL_DATA") is string custom && Path.IsPathFullyQualified(custom) ? Path.GetFullPath(custom) : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FormatWheel");
    public static Settings Current {get;} = Load();
    static Settings Load() {try{return JsonSerializer.Deserialize<Settings>(File.ReadAllText(Path.Combine(Folder,"settings.json")))??new();}catch{return new();}}
    public void Save() { Directory.CreateDirectory(Folder);string temp=Path.Combine(Folder,"settings-"+Guid.NewGuid().ToString("N")+".tmp");File.WriteAllText(temp,JsonSerializer.Serialize(this));File.Move(temp,Path.Combine(Folder,"settings.json"),true); }
    public static bool AutoStart { get {using var k=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"); return k?.GetValue("FormatWheel")!=null;} set {using var k=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"); if(value) k.SetValue("FormatWheel",'"'+Environment.ProcessPath+'"'+" --tray"); else k.DeleteValue("FormatWheel",false);} }
}
public static class Tool
{
    public static string? Find(string name)
    {
        var dirs=new List<string>{Path.Combine(AppContext.BaseDirectory,"tools",name),Path.Combine(AppContext.BaseDirectory,"tools",name,"bin"),Path.Combine(AppContext.BaseDirectory,"tools")};
        dirs.Insert(0,Path.Combine(Settings.Folder,"tools",name=="soffice"?"libreoffice":"ffmpeg",name=="soffice"?"program":"bin"));
        if(name=="ffprobe") {dirs.Add(Path.Combine(AppContext.BaseDirectory,"tools","ffmpeg"));dirs.Add(Path.Combine(AppContext.BaseDirectory,"tools","ffmpeg","bin"));}
        if(name=="soffice") {dirs.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"LibreOffice","program"));}
        dirs.AddRange((Environment.GetEnvironmentVariable("PATH")??"").Split(Path.PathSeparator));
        if(name=="soffice")return dirs.SelectMany(d=>new[]{Path.Combine(d,"soffice.com"),Path.Combine(d,"soffice.exe")}).FirstOrDefault(File.Exists);
        return dirs.Select(d=>Path.Combine(d,name+".exe")).FirstOrDefault(File.Exists);
    }
    public static async Task<string> Run(string name,IEnumerable<string> args,CancellationToken token=default)
    {
        string exe=Path.IsPathRooted(name)?name:Find(name)??throw new InvalidOperationException(L.T("未裝備：")+name+L.T("。請參閱 README-Windows.md。"));
        var psi=new ProcessStartInfo(exe){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var a in args) psi.ArgumentList.Add(a);
        using var p=Process.Start(psi)??throw new IOException(L.T("無法啟動 ")+name);
        using var cancel=token.Register(()=>{try{p.Kill(true);}catch{}});
        var output=p.StandardOutput.ReadToEndAsync(); var error=p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync(token); var s=await output; var e=await error;
        if(p.ExitCode!=0) throw new IOException(name+" (exit "+p.ExitCode+"): "+(e.Length>1800?e[^1800..]:e));
        return s;
    }
}
public static class Output
{
    public static string Unique(string source,string ext,string suffix="")
    {
        var dir=Path.GetDirectoryName(Path.GetFullPath(source))!;
        var name=Path.GetFileNameWithoutExtension(source)+suffix;
        var path=Path.Combine(dir,name+"."+ext); int i=2;
        while(File.Exists(path)||Directory.Exists(path)) path=Path.Combine(dir,name+" ("+i+++ ")."+ext);
        return path;
    }
    // Each operation writes in a private staging directory then moves without overwrite.
    public static string Commit(string temp,string source,string ext,string suffix="")
    {
        for(int i=0;i<100;i++) {var dest=Unique(source,ext,suffix); try{File.Move(temp,dest,false);return dest;}catch(IOException) when(File.Exists(dest)){}}
        throw new IOException(L.T("無法建立不重複的輸出檔名"));
    }
    public static void Log(string text) {try{Directory.CreateDirectory(Settings.Folder);var p=Path.Combine(Settings.Folder,"FormatWheel.log");if(File.Exists(p)&&new FileInfo(p).Length>1000000)File.WriteAllText(p,"");File.AppendAllText(p,DateTime.Now.ToString("O")+" "+text+Environment.NewLine);}catch{}}
}
