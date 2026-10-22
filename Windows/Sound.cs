using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace FormatWheel;

// One worker owns playback. A new hover cannot stop/dispose the opening sound.
// Coalesce only pending hover ticks so fast movement never leaves a sound backlog.
public sealed class SoundQueue(Action<string> play,Func<bool> enabled)
{
    readonly object gate=new();
    readonly Queue<string> pending=new();
    Task worker=Task.CompletedTask;
    bool running;
    public void Enqueue(string cue)
    {
        lock(gate)
        {
            if(!enabled())return;
            if(cue=="hover"&&pending.Contains("hover"))return;
            if(cue!="hover")RemoveHover();
            if(pending.Count>=12)return;
            pending.Enqueue(cue);
            if(!running){running=true;worker=Task.Run(Drain);}
        }
    }
    public Task Idle(){lock(gate)return worker;}
    public void ClearHover(){lock(gate)RemoveHover();}
    void RemoveHover(){if(pending.Count==0)return;var keep=pending.Where(x=>x!="hover").ToArray();pending.Clear();foreach(var cue in keep)pending.Enqueue(cue);}
    void Drain()
    {
        while(true)
        {
            string cue;lock(gate){if(pending.Count==0){running=false;return;}cue=pending.Dequeue();}
            if(!enabled())continue;
            try{play(cue);}catch(Exception ex){Output.Log("sound playback failed: "+ex.Message);}
        }
    }
}

public static class FeedbackTrace
{
    public static void Write(string value){if(Environment.GetEnvironmentVariable("FORMATWHEEL_FEEDBACK_TRACE")=="1")Output.Log(value);}
}

public static class Sound
{
    static readonly string[] CueNames=["open","hover","flip","denied","failed","complete"];
    static readonly Dictionary<string,byte[]> Clips=CueNames.ToDictionary(x=>x,CreateWave);
    static readonly SoundQueue Queue=new(PlayClip,()=>Settings.Current.Sound);
    public static void Play(string kind)=>Queue.Enqueue(kind.Contains('.')?"complete":kind);
    public static void ClearHover()=>Queue.ClearHover();
    public static Task Idle()=>Queue.Idle();
    static void PlayClip(string cue)
    {
        // Synchronous memory playback on the worker keeps the PCM buffer valid
        // through completion, and exposes failure instead of silently swallowing it.
        bool success=PlaySound(Clips[cue],IntPtr.Zero,0x0004|0x0002);
        FeedbackTrace.Write($"audio {cue} completed={success}");
        if(!success)Output.Log("Windows could not play sound: "+cue);
    }
    [DllImport("winmm.dll",EntryPoint="PlaySoundW",ExactSpelling=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    static extern bool PlaySound(byte[] sound,IntPtr module,uint flags);
    internal static byte[] CreateWave(string cue)
    {
        const int rate=44100;
        double duration=cue switch{"hover"=>.035,"open"=>.14,"flip"=>.08,"failed"=>.5,"complete"=>.4,_=>.1};
        int count=(int)(rate*duration);using var stream=new MemoryStream();using var writer=new BinaryWriter(stream);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+count*2);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(count*2);
        var random=new Random(616);
        for(int i=0;i<count;i++)
        {
            double t=i/(double)rate;
            double signal=cue switch
            {
                "hover"=>.65*Math.Sin(2*Math.PI*1450*t)*Math.Exp(-t*95)+.18*(random.NextDouble()*2-1)*Math.Exp(-t*160),
                "open"=>.7*Math.Sin(2*Math.PI*(540*t+1700*t*t))*Math.Exp(-t*18),
                "flip"=>.6*Math.Sin(2*Math.PI*(1000*t-2600*t*t))*Math.Exp(-t*35),
                "failed" or "denied"=>.65*Math.Sin(2*Math.PI*(220*t-80*t*t))*Math.Exp(-t*8),
                _=>.5*Math.Sin(2*Math.PI*(t<.14?523.25:783.99)*t)*Math.Exp(-t*6)
            };
            // Short fades avoid clicks at PCM boundaries.
            signal*=Math.Min(1,t/.002)*Math.Min(1,(duration-t)/.006);
            writer.Write((short)(Math.Clamp(signal,-1,1)*18000));
        }
        return stream.ToArray();
    }
}
