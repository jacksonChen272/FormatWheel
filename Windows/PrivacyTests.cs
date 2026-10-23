using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using ImageMagick;
using PdfSharp.Pdf;

namespace FormatWheel;

public static class PrivacyTests
{
    public static async Task Run(Func<string,Func<Task>,Task> check,string root)
    {
        root=Path.Combine(root,"privacy-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        void Assert(bool condition,string reason) { if(!condition) throw new Exception(reason); }
        Task Case(string name,Action test) => check("privacy-"+name,()=>{test();return Task.CompletedTask;});
        void Complete(PrivacyResult result,int score,int count=1)
        {
            Assert(result.State==PrivacyScanState.Checked && result.CheckedCount==count && result.TotalCount==count,"Scan not complete: "+result.Detail);
            Assert(result.Level==score && result.CanRate && result.VerifiedClean==(score==0),"Wrong score/clean state: "+result.Detail);
        }
        void Unknown(PrivacyResult result,PrivacyScanState state)
        {
            Assert(result.State==state && !result.CanRate && !result.VerifiedClean,"Unknown scan exposed a rating: "+result.Detail);
        }
        void Canceled(Action scan)
        {
            try { scan(); }
            catch(OperationCanceledException) { return; }
            throw new Exception("Caller cancellation returned a scan result");
        }
        string Image(string name,Action<ExifProfile>? configure=null,string? xmp=null)
        {
            var path=Path.Combine(root,name+".png");
            using var image=new MagickImage(MagickColors.CornflowerBlue,320,200);
            if(configure!=null) { var profile=new ExifProfile();configure(profile);image.SetProfile(profile); }
            if(xmp!=null) image.SetProfile(new XmpProfile(Encoding.UTF8.GetBytes(xmp)));
            image.Write(path);return path;
        }
        string Xmp(string attributes="",string content="") =>
            "<x:xmpmeta xmlns:x='adobe:ns:meta/'><rdf:RDF xmlns:rdf='http://www.w3.org/1999/02/22-rdf-syntax-ns#'>"+
            "<rdf:Description rdf:about='' xmlns:exif='http://ns.adobe.com/exif/1.0/' xmlns:tiff='http://ns.adobe.com/tiff/1.0/' "+
            "xmlns:aux='http://ns.adobe.com/exif/1.0/aux/' xmlns:dc='http://purl.org/dc/elements/1.1/' "+
            "xmlns:xmp='http://ns.adobe.com/xap/1.0/' "+attributes+">"+content+"</rdf:Description></rdf:RDF></x:xmpmeta>";
        string Pdf(string name,string author="",string creator=" ",string subject="",string keywords="")
        {
            var path=Path.Combine(root,name+".pdf");
            using var pdf=new PdfDocument();pdf.AddPage();
            pdf.Info.Author=author;pdf.Info.Creator=creator;pdf.Info.Subject=subject;pdf.Info.Keywords=keywords;
            pdf.Save(path);return path;
        }
        string Docx(string name,string? core=null,string? app=null)
        {
            var folder=Path.Combine(root,name);Directory.CreateDirectory(folder);
            var path=Engine.WriteTextDocument("Privacy fixture","docx",folder);
            using var zip=ZipFile.Open(path,ZipArchiveMode.Update);
            void Entry(string entry,string text) { using var writer=new StreamWriter(zip.CreateEntry(entry).Open());writer.Write(text); }
            if(core!=null) Entry("docProps/core.xml",core);
            if(app!=null) Entry("docProps/app.xml",app);
            return path;
        }
        string Core(string creator,string modifier) => new XElement(XName.Get("coreProperties","http://schemas.openxmlformats.org/package/2006/metadata/core-properties"),
            new XElement(XName.Get("creator","http://purl.org/dc/elements/1.1/"),creator),
            new XElement(XName.Get("lastModifiedBy","http://schemas.openxmlformats.org/package/2006/metadata/core-properties"),modifier)).ToString();
        string App(string company) => new XElement(XName.Get("Properties","http://schemas.openxmlformats.org/officeDocument/2006/extended-properties"),
            new XElement(XName.Get("Company","http://schemas.openxmlformats.org/officeDocument/2006/extended-properties"),company)).ToString();
        PrivacyResult Video(string json) => Privacy.Scan([Path.Combine(root,"fixture.mov")],_=>json);

        await Case("result-backward-compatible",()=>
        {
            var legacy=new PrivacyResult(0,"legacy");var (level,detail)=legacy;
            Assert(level==0 && detail=="legacy","Positional API changed");
            Assert(!legacy.CanRate && !legacy.VerifiedClean,"Unverified legacy result treated as clean");
        });
        await Case("unsupported-and-empty-not-clean",()=>
        {
            var result=Privacy.Scan([Path.Combine(root,"program.exe"),Path.Combine(root,"notes.txt"),Path.Combine(root,"sound.wav"),root]);
            Unknown(result,PrivacyScanState.Unsupported);
            Assert(result.UnsupportedCount==4 && result.CheckedCount==0 && result.TotalCount==4,"Unsupported coverage wrong");
            Unknown(Privacy.Scan([]),PrivacyScanState.Unsupported);
        });
        await Case("failed-files-not-clean",()=>
        {
            foreach(var extension in new[]{"png","pdf","docx"})
            {
                var path=Path.Combine(root,"corrupt."+extension);File.WriteAllText(path,"not a valid file");
                var result=Privacy.Scan([path]);Unknown(result,PrivacyScanState.Error);
                Assert(result.ErrorCount==1 && result.CheckedCount==0,"Failed file counted as checked");
            }
            Unknown(Privacy.Scan([Path.Combine(root,"missing.png")]),PrivacyScanState.Error);
        });
        await Case("image-clean-and-blank-tags",()=>
        {
            Complete(Privacy.Scan([Image("clean")]),0);
            Complete(Privacy.Scan([Image("blank",p=>{p.SetValue(ExifTag.Artist,"   ");p.SetValue(ExifTag.Model," ");},Xmp("tiff:Make=' ' exif:DateTimeOriginal=' '","<dc:creator><rdf:Seq><rdf:li> </rdf:li></rdf:Seq></dc:creator>"))]),0);
        });
        await Case("image-category-weights",()=>
        {
            Complete(Privacy.Scan([Image("gps",p=>{p.SetValue(ExifTag.GPSLatitude,new[]{new Rational(25u),new Rational(0u),new Rational(0u)});p.SetValue(ExifTag.GPSLongitude,new[]{new Rational(121u),new Rational(0u),new Rational(0u)});})]),2);
            Complete(Privacy.Scan([Image("device",p=>{p.SetValue(ExifTag.Make,"Camera maker");p.SetValue(ExifTag.Model,"Camera model");p.SetValue(ExifTag.LensModel,"Lens");})]),1);
            Complete(Privacy.Scan([Image("capture-time",p=>p.SetValue(ExifTag.DateTimeOriginal,"2026:10:23 12:00:00"))]),1);
            Complete(Privacy.Scan([Image("author-serial",p=>{p.SetValue(ExifTag.Artist,"Fixture person");p.SetValue(ExifTag.SerialNumber,"S123");p.SetValue(ExifTag.LensSerialNumber,"L456");})]),1);
            Complete(Privacy.Scan([Image("gps-camera",p=>
            {
                p.SetValue(ExifTag.GPSLatitude,new[]{new Rational(25u),new Rational(0u),new Rational(0u)});
                p.SetValue(ExifTag.GPSLatitudeRef,"N");
                p.SetValue(ExifTag.GPSLongitude,new[]{new Rational(121u),new Rational(0u),new Rational(0u)});
                p.SetValue(ExifTag.GPSLongitudeRef,"E");
                p.SetValue(ExifTag.Model,"Fixture camera");p.SetValue(ExifTag.DateTimeOriginal,"2026:10:23 12:00:00");
                p.SetValue(ExifTag.Artist,"Fixture person");
            })]),5);
        });
        await Case("image-xmp-supported-tags-and-dedup",()=>
        {
            var xmp=Xmp("exif:GPSLatitude='25,0N' exif:GPSLongitude='121,0E' tiff:Make='Maker' tiff:Model='Model' exif:DateTimeOriginal='2026-10-23T12:00:00' aux:SerialNumber='S123'",
                "<dc:creator><rdf:Seq><rdf:li>Fixture person</rdf:li></rdf:Seq></dc:creator>");
            Complete(Privacy.Scan([Image("xmp-only",xmp:xmp)]),5);
            var result=Privacy.Scan([Image("exif-and-xmp",p=>{p.SetValue(ExifTag.Model,"Model");p.SetValue(ExifTag.Artist,"Fixture person");},Xmp("tiff:Model='Model' aux:SerialNumber='S123'"))]);
            Complete(result,2);
            Assert(!result.Detail.Contains("Fixture person") && !result.Detail.Contains("S123"),"Raw private values leaked into reasons");
            Complete(Privacy.Scan([Image("xmp-elements",xmp:Xmp(content:"<exif:GPSLongitude>121,0E</exif:GPSLongitude><tiff:Model>Model</tiff:Model><exif:DateTimeOriginal>2026-10-23</exif:DateTimeOriginal>"))]),4);
        });
        await Case("image-benign-xmp-is-not-risk",()=>
        {
            Complete(Privacy.Scan([Image("benign-xmp",xmp:Xmp("xmp:CreateDate='2026-10-23' xmp:CreatorTool='Editor'", "<other:Model xmlns:other='urn:unrelated'>Not a camera</other:Model>"))]),0);
        });
        await Case("image-broken-xmp-retains-findings",()=>
        {
            var result=Privacy.Scan([Image("broken-xmp",p=>p.SetValue(ExifTag.Model,"Model"),"<x:xmpmeta")]);
            Assert(result.State==PrivacyScanState.Partial && result.PartialCount==1 && result.Level==1 && result.CanRate && !result.VerifiedClean,"Broken XMP hid a known finding or became clean: "+result.Detail);
        });
        await Case("pdf-category-weights",()=>
        {
            Complete(Privacy.Scan([Pdf("pdf-empty")]),0);
            Complete(Privacy.Scan([Pdf("pdf-author",author:"Fixture person")]),2);
            Complete(Privacy.Scan([Pdf("pdf-creator",creator:"Fixture editor")]),1);
            Complete(Privacy.Scan([Pdf("pdf-subject",subject:"Private subject")]),1);
            Complete(Privacy.Scan([Pdf("pdf-keywords",keywords:"Private keywords")]),1);
            Complete(Privacy.Scan([Pdf("pdf-all","Fixture person","Fixture editor","Private subject","Private keywords")]),4);
        });
        await Case("docx-distinct-modifier-and-company",()=>
        {
            Complete(Privacy.Scan([Docx("docx-empty")]),0);
            Complete(Privacy.Scan([Docx("docx-creator",Core("Alice",""))]),2);
            Complete(Privacy.Scan([Docx("docx-same-modifier",Core(" Alice ","alice"))]),2);
            Complete(Privacy.Scan([Docx("docx-different-modifier",Core("Alice","Bob"))]),3);
            Complete(Privacy.Scan([Docx("docx-modifier-only",Core("","Bob"))]),1);
            Complete(Privacy.Scan([Docx("docx-company",app:App("Example company"))]),1);
            Complete(Privacy.Scan([Docx("docx-all",Core("Alice","Bob"),App("Example company"))]),4);
        });
        await Case("docx-broken-properties-are-partial",()=>
        {
            var result=Privacy.Scan([Docx("docx-broken",Core("Alice","Bob"),"<Properties")]);
            Assert(result.State==PrivacyScanState.Partial && result.Level==3 && result.PartialCount==1 && result.CheckedCount==0 && result.CanRate && !result.VerifiedClean,"Broken properties lost known findings: "+result.Detail);
            Unknown(Privacy.Scan([Docx("docx-broken-only","<coreProperties")]),PrivacyScanState.Partial);
        });
        await Case("video-missing-failed-timeout-not-clean",()=>
        {
            var missing=Privacy.Scan(["fixture.mov"],_=>null);Unknown(missing,PrivacyScanState.Unsupported);
            Assert(missing.UnsupportedCount==1 && missing.Detail.Contains("ffprobe"),"Missing tool not explained");
            Unknown(Privacy.Scan(["fixture.mov"],_=>throw new IOException("failed probe")),PrivacyScanState.Error);
            Unknown(Privacy.Scan(["fixture.mov"],_=>throw new OperationCanceledException()),PrivacyScanState.Error);
            foreach(var json in new[]{"invalid","{}","[]","{\"error\":{}}","{\"streams\":[]}","{\"format\":{\"tags\":null}}"}) Unknown(Video(json),PrivacyScanState.Error);
        });
        await Case("pre-canceled-scan-does-no-work",()=>
        {
            using var stop=new CancellationTokenSource();stop.Cancel();
            int calls=0;
            Canceled(()=>Privacy.Scan(["fixture.mov"],_=>{calls++;return "{\"format\":{}}";},stop.Token));
            Assert(calls==0,"Pre-canceled scan started ffprobe");
            Canceled(()=>Privacy.Scan(["fixture.mov"],stop.Token));
            Canceled(()=>Privacy.Scan([],stop.Token));
        });
        await Case("caller-cancellation-stops-between-files",()=>
        {
            // Cover normal completion, a linked-token cancellation and a reader error
            // racing with cancellation. None may start file two or return partial success.
            foreach(var outcome in new[]{"returned","canceled","failed"})
            {
                using var stop=new CancellationTokenSource();int calls=0;
                Canceled(()=>Privacy.Scan(["first.mov","second.mov"],file=>
                {
                    calls++;Assert(file=="first.mov","Scan continued into second file");
                    stop.Cancel();
                    if(outcome=="canceled")throw new OperationCanceledException(stop.Token);
                    if(outcome=="failed")throw new IOException("Reader failed after cancellation");
                    return "{\"format\":{\"tags\":{\"model\":\"Camera\"}}}";
                },stop.Token));
                Assert(calls==1,"Cancellation did not stop after the in-flight file");
            }
        });
        await Case("caller-cancellation-on-last-file-is-not-success",()=>
        {
            using var stop=new CancellationTokenSource();
            Canceled(()=>Privacy.Scan(["last.mov"],_=>{stop.Cancel();return "{\"format\":{}}";},stop.Token));
        });
        await Case("probe-timeout-keeps-caller-scan-running",()=>
        {
            using var stop=new CancellationTokenSource();int calls=0;
            var result=Privacy.Scan(["timeout.mov","checked.mov"],_=>
            {
                if(++calls==1)throw new OperationCanceledException();
                return "{\"format\":{}}";
            },stop.Token);
            Unknown(result,PrivacyScanState.Partial);
            Assert(!stop.IsCancellationRequested && calls==2 && result.ErrorCount==1 && result.CheckedCount==1,"Probe timeout was treated as caller cancellation");
        });
        await Case("video-generic-dates-ignored",()=>
        {
            Complete(Video("""{"format":{"tags":{"creation_time":"2026-10-23T12:00:00Z","date":"2026","DATE_RECORDED":"2026","encoder":"Editor","model_version":"1","relocation":"yes"}},"streams":[{"tags":{"creation_time":"2026-10-23"}}]}"""),0);
            Complete(Video("""{"format":{},"streams":[]}"""),0);
        });
        await Case("video-camera-tags-format-and-streams",()=>
        {
            Complete(Video("""{"format":{"tags":{"location":"+25.0+121.0/","com.apple.quicktime.location.ISO6709":"+25.0+121.0/"}},"streams":[{"tags":{"location-eng":"+25.0+121.0/"}}]}"""),2);
            Complete(Video("""{"format":{"tags":{"com.apple.quicktime.make":"Maker","com.apple.quicktime.model":"Phone"}},"streams":[{"tags":{"model":"Phone"}}]}"""),1);
            Complete(Video("""{"format":{"tags":{"com.apple.quicktime.creationdate":"2026-10-23T12:00:00"}},"streams":[{"tags":{"DateTimeOriginal":"2026-10-23"}}]}"""),1);
            Complete(Video("""{"format":{"tags":{"com.apple.quicktime.location.ISO6709":"+25.0+121.0/","com.apple.quicktime.model":"Phone","com.apple.quicktime.creationdate":"2026-10-23T12:00:00"}},"streams":[{"tags":{"model":"Phone","location":"+25.0+121.0/","datetimeoriginal":"2026-10-23"}}]}"""),4);
            Complete(Video("""{"format":{"tags":{"location":" ","model":"","datetimeoriginal":" "}}}"""),0);
        });
        await Case("batch-max-keeps-unknown-visible",()=>
        {
            var author=Image("batch-author",p=>p.SetValue(ExifTag.Artist,"Alice"));
            var device=Image("batch-device",p=>p.SetValue(ExifTag.Model,"Camera"));
            Complete(Privacy.Scan([author,device]),1,2);
            var result=Privacy.Scan([author,device,"unknown.exe",Path.Combine(root,"missing.pdf")]);
            Assert(result.State==PrivacyScanState.Partial && result.Level==1 && result.CanRate && !result.VerifiedClean,"Batch rating wrong");
            Assert(result.CheckedCount==2 && result.TotalCount==4 && result.UnsupportedCount==1 && result.ErrorCount==1 && result.UnknownCount==2 && result.Detail.Contains("2/4"),"Batch coverage hidden");
            Unknown(Privacy.Scan([Image("batch-clean"),"unknown.exe"]),PrivacyScanState.Partial);
        });
        await Case("batch-cap-transparent-not-clean",()=>
        {
            int calls=0;
            var files=Enumerable.Range(0,25).Select(i=>"video"+i+".mov").ToArray();
            var result=Privacy.Scan(files,_=>{calls++;return "{\"format\":{}}";});
            Unknown(result,PrivacyScanState.Partial);
            Assert(calls==24 && result.CheckedCount==24 && result.TotalCount==25 && result.SkippedCount==1 && result.AttemptedCount==24 && result.UnknownCount==1 && result.Detail.Contains("24/25"),"Cap not enforced or coverage hidden");
            Complete(Privacy.Scan(files.Take(24).ToArray(),_=>"{\"format\":{}}"),0,24);
        });
        await Case("new-ui-text-localized",()=>
        {
            var previous=Settings.Current.Language;
            try
            {
                Settings.Current.Language="en";
                foreach(var result in new[]{Privacy.Scan(["unknown.exe"]),Privacy.Scan(["fixture.mov"],_=>null),Video("{}"),Video("{\"format\":{}}")})
                    Assert(!Regex.IsMatch(result.Detail+result.StatusText,@"\p{IsCJKUnifiedIdeographs}"),"Untranslated privacy UI");
                Settings.Current.Language="zh-TW";var traditionalStatus=Privacy.Scan(["unknown.exe"]).StatusText;
                Settings.Current.Language="zh-CN";var simplified=Privacy.Scan(["unknown.exe"]);
                Assert(traditionalStatus.Contains("檢查") && simplified.StatusText.Contains("检查"),"Chinese state text not localized");
                Assert(simplified.Detail.Contains("检查") && !simplified.Detail.Contains("檢查"),"Simplified privacy detail not localized");
            }
            finally { Settings.Current.Language=previous; }
        });
        await check("privacy-output-rescan-after-strip",async()=>
        {
            var input=Image("strip-input",p=>{p.SetValue(ExifTag.Artist,"Alice");p.SetValue(ExifTag.Model,"Camera");});
            Complete(Privacy.Scan([input]),2);
            var output=await Engine.Run("img.strip",[input]);
            Complete(Privacy.Scan(output.Files.ToArray()),0,output.Files.Count);
            Complete(Privacy.Scan([input]),2);
        });
    }
}
