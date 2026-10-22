using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace FormatWheel;

public static class L
{
    public static string Language => Settings.Current.Language == "auto" ? Detect(CultureInfo.CurrentUICulture.Name) : Settings.Current.Language;
    public static string Detect(string culture) => culture.StartsWith("zh",StringComparison.OrdinalIgnoreCase) ? (culture.Contains("TW")||culture.Contains("HK")||culture.Contains("MO")||culture.Contains("Hant")?"zh-TW":"zh-CN") : "en";
    static readonly Lazy<Dictionary<string,string>> English = new(()=>JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Assets.File("en.json")))!);
    public static string T(string text) => Language switch {"en"=>English.Value.GetValueOrDefault(text,text),"zh-CN"=>Simplify(text),_=>text};
    public static string Text(string traditional,string english) => Language switch {"en"=>english,"zh-CN"=>Simplify(traditional),_=>traditional};
    public static string F(string text,params object[] args)=>string.Format(CultureInfo.CurrentCulture,T(text),args);
    public static string On(bool value)=>Text(value?"開":"關",value?"On":"Off");
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    static extern int LCMapStringEx(string locale,uint flags,string source,int length,StringBuilder? destination,int capacity,IntPtr version,IntPtr reserved,IntPtr sort);
    public static string Simplify(string text)
    {
        if(text.Length==0)return text;
        int size=LCMapStringEx("zh-CN",0x02000000,text,-1,null,0,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);
        if(size==0)return text;var b=new StringBuilder(size);
        return LCMapStringEx("zh-CN",0x02000000,text,-1,b,size,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero)>0?b.ToString():text;
    }
}
