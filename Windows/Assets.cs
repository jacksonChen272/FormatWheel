using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Collections.Generic;

namespace FormatWheel;
public static class Assets
{
    static readonly object Gate=new();
    static readonly Dictionary<string,string> Cached=new();
    public static string File(string name)
    {
        if(name!=Path.GetFileName(name))throw new ArgumentException("Invalid asset name");
        lock(Gate)
        {
            if(Cached.TryGetValue(name,out var cached)&&System.IO.File.Exists(cached))return cached;
            string directory=Path.Combine(Settings.Folder,"assets","0.3.1");Directory.CreateDirectory(directory);
            string path=Path.Combine(directory,name);
            using var resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("Assets."+name)??throw new FileNotFoundException("Missing built-in asset: "+name);
            var hash=SHA256.HashData(resource);resource.Position=0;
            if(System.IO.File.Exists(path)){using var existing=System.IO.File.OpenRead(path);if(System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(hash,SHA256.HashData(existing))){Cached[name]=path;return path;}}
            string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";using(var output=System.IO.File.Create(temp))resource.CopyTo(output);System.IO.File.Move(temp,path,true);Cached[name]=path;return path;
        }
    }
    public static string Notices()
    {
        string directory=Path.Combine(Settings.Folder,"licenses","0.3.1");Directory.CreateDirectory(directory);var assembly=Assembly.GetExecutingAssembly();
        foreach(string name in assembly.GetManifestResourceNames())if(name.StartsWith("Docs.")){using var input=assembly.GetManifestResourceStream(name)!;using var output=System.IO.File.Create(Path.Combine(directory,name[5..]));input.CopyTo(output);}
        return directory;
    }
}
