using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace FormatWheel;

public static class FeedbackTests
{
    public static async Task Run(Func<string,Func<Task>,Task> check)
    {
        void Assert(bool condition,string reason){if(!condition)throw new Exception(reason);}
        await check("hover-enter-leave-reenter",()=>
        {
            var view=new WheelCanvas{Center=new Point(400,400),Radius=285,Files=["example.png"]};
            var north=WheelCanvas.Polar(view.Center,230,0);var east=WheelCanvas.Polar(view.Center,230,Math.PI/2);
            Assert(view.UpdatePointer(north)&&view.Hover==0,"first entry silent");
            Assert(!view.UpdatePointer(new Point(north.X+2,north.Y+2)),"replayed inside same option");
            Assert(view.UpdatePointer(east)&&view.Hover==2,"switch did not update");
            Assert(!view.UpdatePointer(view.Center)&&view.Hover==-1,"center kept hint");
            Assert(view.UpdatePointer(east),"re-entry silent");
            view.UpdatePointer(new Point(900,400));Assert(view.Hover==-1,"outside ring kept hint");
            view.UpdatePointer(east);view.UpdatePointer(null);Assert(view.Hover==-1,"leave kept hint");
            Assert(view.Hit(new Point(400,260))==-1,"visible center incorrectly selects");
            return Task.CompletedTask;
        });
        await check("hover-page-empty-disabled",()=>
        {
            var view=new WheelCanvas{Center=new Point(400,400),Radius=285,Files=["example.rtf"]};
            var northeast=WheelCanvas.Polar(view.Center,230,Math.PI/4);
            Assert(view.UpdatePointer(northeast),"format entry");view.Page=1;
            Assert(!view.UpdatePointer(northeast)&&view.Hover==-1,"empty slot has hint or tick");
            Assert(view.UpdatePointer(WheelCanvas.Polar(view.Center,230,Math.PI)),"disabled option has no explanation");
            Assert(view.Options[view.Hover]!.Unavailable(view.Files)!=null,"fixture not disabled");
            view.Page=0;Assert(view.UpdatePointer(WheelCanvas.Polar(view.Center,230,Math.PI)),"page change kept old action");
            Assert(view.Options[view.Hover]!.Id=="doc.to.html","wrong page description");return Task.CompletedTask;
        });
        await check("hint-bounds-all-directions-dpi",()=>
        {
            foreach(double dpi in new[]{1d,1.25,1.5,2})
            {
                var viewport=new Size(1920/dpi,1080/dpi);var screen=new Rect(new Point(),viewport);
                foreach(var corner in new[]{new Point(),new Point(viewport.Width,0),new Point(0,viewport.Height),new Point(viewport.Width,viewport.Height),new Point(viewport.Width/2,viewport.Height/2)})
                {
                    var wheel=WheelPlacement.Place(corner,viewport,330,true);
                    for(int i=0;i<8;i++){var hint=WheelPlacement.Hint(wheel.Center,wheel.Radius,i,viewport,new Size(280,210));Assert(screen.Contains(hint),$"clipped hint at {dpi}/{i}");}
                }
            }
            var center=new Point(960,540);var previous=Rect.Empty;
            for(int i=0;i<8;i++){var hint=WheelPlacement.Hint(center,240,i,new Size(1920,1080),new Size(280,130));Assert(hint!=previous,"hint stuck at fixed location");Assert(!hint.Contains(WheelCanvas.Polar(center,220,i*Math.PI/4)),"hint covers active option");previous=hint;}
            return Task.CompletedTask;
        });
        await check("audio-cues-valid-distinct-pcm",()=>
        {
            var hashes=new HashSet<string>();
            foreach(var cue in new[]{"open","hover","flip","denied","failed","complete"})
            {
                var wave=Sound.CreateWave(cue);using var reader=new BinaryReader(new MemoryStream(wave));
                Assert(Encoding.ASCII.GetString(reader.ReadBytes(4))=="RIFF"&&reader.ReadInt32()==wave.Length-8,"invalid RIFF");reader.BaseStream.Position=22;
                Assert(reader.ReadInt16()==1&&reader.ReadInt32()==44100,"wrong PCM format");reader.BaseStream.Position=40;
                Assert(reader.ReadInt32()==wave.Length-44,"invalid data length");int peak=0;while(reader.BaseStream.Position<wave.Length)peak=Math.Max(peak,Math.Abs((int)reader.ReadInt16()));
                Assert(peak>3000&&peak<32767,"silent or clipped cue");Assert(hashes.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(wave))),"duplicate cues");
            }
            return Task.CompletedTask;
        });
        await check("audio-opening-not-interrupted-by-hover",async()=>
        {
            using var release=new ManualResetEventSlim();var started=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var played=new List<string>();int active=0,overlap=0;
            var queue=new SoundQueue(cue=>{if(Interlocked.Increment(ref active)!=1)Interlocked.Increment(ref overlap);lock(played)played.Add(cue);if(cue=="open"){started.TrySetResult();if(!release.Wait(5000))throw new TimeoutException();}Interlocked.Decrement(ref active);},()=>true);
            queue.Enqueue("open");await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            try{for(int i=0;i<100;i++)queue.Enqueue("hover");Assert(played.SequenceEqual(new[]{"open"}),"opening interrupted");}finally{release.Set();}
            await queue.Idle().WaitAsync(TimeSpan.FromSeconds(5));Assert(overlap==0&&played.SequenceEqual(new[]{"open","hover"}),"overlap or delayed hover backlog");
        });
        await check("audio-mute-and-leave-clear-pending",async()=>
        {
            using var release=new ManualResetEventSlim();var started=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var played=new List<string>();bool enabled=false;
            var queue=new SoundQueue(cue=>{played.Add(cue);if(cue=="open"){started.TrySetResult();if(!release.Wait(5000))throw new TimeoutException();}},()=>enabled);
            queue.Enqueue("hover");await queue.Idle();Assert(played.Count==0,"mute ignored");enabled=true;
            queue.Enqueue("open");await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            try{queue.Enqueue("hover");queue.ClearHover();}finally{release.Set();}
            await queue.Idle().WaitAsync(TimeSpan.FromSeconds(5));Assert(played.SequenceEqual(new[]{"open"}),"stale hover played after leaving");
            queue.Enqueue("flip");await queue.Idle();Assert(played.Last()=="flip","worker did not restart");
        });
    }
}
