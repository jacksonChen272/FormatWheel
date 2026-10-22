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
    public static string T(string text) => Language switch {"en"=>English.Value.GetValueOrDefault(text,text),"zh-CN"=>SimplifiedUi(text),_=>text};
    public static string Text(string traditional,string english) => Language switch {"en"=>english,"zh-CN"=>SimplifiedUi(traditional),_=>traditional};
    static string SimplifiedUi(string text)=>Simplify(text).Replace("资料夹","文件夹").Replace("系统匣","系统托盘").Replace("档案","文件").Replace("档名","文件名").Replace("程式","程序").Replace("视窗","窗口").Replace("图示","图标").Replace("滑鼠","鼠标").Replace("音讯","音频").Replace("影片","视频").Replace("去背","去背景").Replace("设定","设置");
    public static string F(string text,params object[] args)=>string.Format(CultureInfo.CurrentCulture,T(text),args);
    public static string On(bool value)=>Text(value?"開":"關",value?"On":"Off");
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    static extern int LCMapStringEx(string locale,uint flags,string source,int length,StringBuilder? destination,int capacity,IntPtr version,IntPtr reserved,IntPtr sort);
    public static string Simplify(string text)
    {
        if(text.Length==0)return text;
        int size=LCMapStringEx("zh-CN",0x02000000,text,-1,null,0,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);
        if(size==0)return text;var b=new StringBuilder(size);
        return LCMapStringEx("zh-CN",0x02000000,text,-1,b,size,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero)>0?b.ToString().Replace('後','后').Replace('裡','里'):text;
    }
}
