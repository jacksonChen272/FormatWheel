using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImageMagick;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace FormatWheel;

public static class AssessmentTests
{
    public static async Task Run(Func<string, Func<Task>, Task> check, string root)
    {
        string directory = Path.Combine(root, "assessment-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string previous = Settings.Current.Language;
        Settings.Current.Language = "en";
        void Assert(bool value, string message) { if (!value) throw new Exception(message); }
        FileAssessment.Assessment Assess(string path, string id) => FileAssessment.Read([path]).Evaluate(Catalog.All[id]);
        FileAssessment.Metric Metric(FileAssessment.Assessment a, string name) => a.Metrics.Single(m => m.Label == name);
        string transparent = Path.Combine(directory, "transparent.png"), opaque = Path.Combine(directory, "opaque.jpg");
        try
        {
            using (var image = new MagickImage(MagickColors.Transparent, 80, 40)) image.Write(transparent);
            using (var image = new MagickImage(MagickColors.CornflowerBlue, 80, 40)) { image.Quality = 65; image.Write(opaque); }
            await check("assessment-image-metadata-and-name", () =>
            {
                var s = FileAssessment.Read([transparent]);
                var m = s.Metadata.Single();
                Assert(s.SourceName == "transparent.png", "source label is not the filename");
                Assert(s.FileCount == 1 && s.InspectedCount == 1 && s.TotalBytes == new FileInfo(transparent).Length, "wrong counts/size");
                Assert(m.Width == 80 && m.Height == 40 && m.HasAlpha == true && m.FrameCount == 1, "wrong actual image metadata");
                Assert(s.Summary.Contains("PNG") && s.Summary.Split('\n').Length <= 2, "summary must include format within two lines");
                return Task.CompletedTask;
            });
            await check("assessment-alpha-loss-matches-conversion", async () =>
            {
                var alpha = Metric(Assess(transparent, "img.to.jpg"), "Alpha retained");
                Assert(alpha.Base == 1 && alpha.Target == 0, "PNG alpha loss not predicted");
                var output = await Engine.Run("img.to.jpg", [transparent]);
                using var result = new MagickImage(output.Files.Single());
                Assert(!result.HasAlpha && result.Width == 80 && result.Height == 40, "actual JPG does not match prediction");
                var noAlpha = Metric(Assess(opaque, "img.to.png"), "Alpha retained");
                Assert(noAlpha.Base == 0 && noAlpha.Target == 0, "conversion invented transparency");
            });
            await check("assessment-lossy-to-lossless-no-upgrade", async () =>
            {
                var quality = Metric(Assess(opaque, "img.to.png"), "Quality retained");
                Assert(quality.Base == 1 && quality.Target == 1, "PNG falsely upgrades source quality");
                Assert(quality.Note.Contains("cannot restore"), "missing source-loss caveat");
                var output = await Engine.Run("img.to.png", [opaque]);
                using var original = new MagickImage(opaque);
                using var result = new MagickImage(output.Files.Single());
                Assert(original.Compare(result, ErrorMetric.Absolute) == 0, "lossless output changed decoded pixels");
                Assert(Metric(Assess(opaque, "img.to.tiff"), "Quality retained").Target == 1, "TIFF not treated as lossless output");
            });
            await check("assessment-three-images-pdf-and-generic-actions", async () =>
            {
                var third = Path.Combine(directory, "third.png");
                File.Copy(transparent, third);
                string[] files = [transparent, opaque, third];
                var s = FileAssessment.Read(files);
                var a = s.Evaluate(Catalog.All["img.to.pdf"]);
                Assert(a.Detail.Contains("3 images") && a.Detail.Contains("1 PDF (3 pages)"), "wrong image merge count");
                Assert(a.Title == Catalog.All["img.to.pdf"].DisplayLabel(files), "wrong image merge label");
                Assert(Metric(a, "Efficiency estimate").Target.HasValue, "missing PDF efficiency estimate");
                foreach (var action in Catalog.All.Values.Where(x => x.Id.StartsWith("gen.")))
                    Assert(s.Evaluate(action).Metrics.Count == 0, "generic operation invents media metrics");
                Assert(s.Evaluate(new ActionItem("img.unknown", "Unknown", "")).Metrics.Count == 0, "unsupported operation invents metrics");
                var output = await Engine.Run("img.to.pdf", files);
                using var pdf = PdfReader.Open(output.Files.Single(), PdfDocumentOpenMode.Import);
                Assert(pdf.PageCount == 3, "actual PDF page count differs");
                Assert(System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(output.Files.Single())).Contains("/SMask"), "PDF lost image alpha mask");
                Assert(Metric(Assess(transparent,"img.to.pdf"), "Alpha retained").Target == 1, "PDF alpha prediction differs from embedded mask");
            });
            await check("assessment-animation-ping-and-first-frame-loss", () =>
            {
                var path = Path.Combine(directory, "animated.gif");
                using (var images = new MagickImageCollection())
                {
                    images.Add(new MagickImage(MagickColors.Red, 16, 12));
                    images.Add(new MagickImage(MagickColors.Blue, 16, 12));
                    foreach (var image in images) image.AnimationDelay = 10;
                    images.Write(path);
                }
                var s = FileAssessment.Read([path]);
                Assert(s.Metadata[0].FrameCount == 2, "animated frame count not read");
                Assert(Metric(s.Evaluate(Catalog.All["img.to.png"]), "Frames retained").Target == 0, "first-frame-only operation hides animation loss");
                return Task.CompletedTask;
            });
            await check("assessment-frame-cap-does-not-assume-no-alpha", () =>
            {
                var path = Path.Combine(directory, "many-frames.gif");
                using (var images = new MagickImageCollection())
                {
                    for (int i = 0; i <= FileAssessment.MaxImageFrames; i++)
                    {
                        var image = new MagickImage(i % 2 == 0 ? MagickColors.Red : MagickColors.Blue, 2, 2);
                        image.AnimationDelay = 1;
                        images.Add(image);
                    }
                    images.Write(path);
                }
                var s = FileAssessment.Read([path]);
                Assert(s.Metadata[0].Error == null, "bounded animation fixture failed to inspect");
                Assert(s.Metadata[0].FrameCount == null && s.Metadata[0].HasAlpha == null, "capped frame scan claims all frames have no alpha");
                var a = s.Evaluate(Catalog.All["img.to.png"]);
                Assert(Metric(a, "Alpha retained").Base == null, "capped frame scan creates definite alpha score");
                Assert(a.Notices.Any(x => x.Contains("Frame count exceeds")), "frame cap not disclosed");
                return Task.CompletedTask;
            });
            await check("assessment-image-tools-and-relative-estimates", () =>
            {
                var half = Assess(transparent, "img.half");
                Assert(Metric(half, "Pixel count retained").Target == .25, "halving dimensions must quarter pixel count");
                Assert(Assess(opaque, "img.strip").Notices.Any(x => x.Contains("lossy JPG")), "strip hides JPG recompression");
                var compress = Metric(Assess(opaque, "img.compress"), "Efficiency estimate");
                Assert(compress.Target > compress.Base && compress.Note.Contains("not actual size"), "same-format compression estimate missing or misleading");
                Assert(Metric(Assess(transparent, "img.ocr"), "Layout retained").Target == 0, "OCR claims layout preservation");
                foreach (var a in Catalog.All.Values.Where(x => x.Id.StartsWith("img.")))
                    foreach (var metric in FileAssessment.Read([opaque]).Evaluate(a).Metrics)
                        Assert((metric.Base == null || metric.Base is >= 0 and <= 1) && (metric.Target == null || metric.Target is >= 0 and <= 1), "bar outside normalized range");
                return Task.CompletedTask;
            });
            await check("assessment-pdf-pages-and-text-layout", () =>
            {
                string path = Path.Combine(directory, "pages.pdf");
                using (var pdf = new PdfDocument()) { pdf.AddPage(); pdf.AddPage(); pdf.AddPage(); pdf.Save(path); }
                var s = FileAssessment.Read([path]);
                Assert(s.Metadata[0].PageCount == 3, "wrong actual PDF page count");
                var text = s.Evaluate(Catalog.All["pdf.to.docx"]);
                Assert(Metric(text, "Layout retained").Target == 0, "text extraction claims formatted Word output");
                Assert(text.Metrics.Count >= 4, "document assessment missing capability rows");
                string second = Path.Combine(directory, "more-pages.pdf");
                File.Copy(path, second);
                var merged = FileAssessment.Read([path, second]).Evaluate(Catalog.All["pdf.merge"]);
                Assert(merged.Detail.Contains("6 pages"), "merge page total not based on metadata");
                return Task.CompletedTask;
            });
            await check("assessment-docx-stale-page-cache-omitted", () =>
            {
                string path = Engine.WriteTextDocument("One short paragraph", "docx", directory);
                using (var zip = ZipFile.Open(path, ZipArchiveMode.Update))
                using (var writer = new StreamWriter(zip.CreateEntry("docProps/app.xml").Open()))
                    writer.Write("<Properties xmlns=\"http://schemas.openxmlformats.org/officeDocument/2006/extended-properties\"><Pages>987</Pages></Properties>");
                var s = FileAssessment.Read([path]);
                Assert(s.Metadata[0].PageCount == null && !s.Summary.Contains("987"), "stale docProps counted as actual pages");
                Assert(s.Notices.Any(x => x.Contains("cached page counts")), "missing pagination limitation");
                Assert(s.Evaluate(Catalog.All["doc.to.pdf"]).Metrics.Count == 4, "document capability rows missing");
                return Task.CompletedTask;
            });
            await check("assessment-metadata-failure-does-not-invent-values", () =>
            {
                string path = Path.Combine(directory, "broken.png");
                File.WriteAllText(path, "not an image");
                var s = FileAssessment.Read([path]);
                Assert(s.Metadata[0].Width == null && s.Metadata[0].HasAlpha == null && s.Metadata[0].Error != null, "bad image received fabricated metadata");
                var a = s.Evaluate(Catalog.All["img.to.jpg"]);
                Assert(a.Metrics.All(x => x.Base == null && x.Target == null), "bad image received fabricated scores");
                Assert(s.Notices.All(a.Notices.Contains), "snapshot failure notices missing from assessment");
                Assert(s.Summary.Contains("Unknown"), "unknown metadata not visible");
                var missing = FileAssessment.Read([Path.Combine(directory, "missing.mp4")]);
                Assert(missing.TotalBytes == null && missing.Metadata[0].VideoCodec == null, "missing media receives fabricated data");
                return Task.CompletedTask;
            });
            await check("assessment-batch-cap-counts-and-sizes", () =>
            {
                var paths = Enumerable.Range(0, 26).Select(i => Path.Combine(directory, "batch-" + i + ".png")).ToArray();
                foreach (var path in paths) File.Copy(transparent, path);
                var s = FileAssessment.Read(paths);
                Assert(s.FileCount == 26 && s.InspectedCount == 24 && s.Metadata.Count == 26, "inspection cap/count incorrect");
                Assert(s.TotalBytes == 26 * new FileInfo(transparent).Length, "total size only covers sampled files");
                Assert(s.Metadata.Skip(24).All(x => x.Width == null), "uninspected entries fabricated");
                var a = s.Evaluate(Catalog.All["img.to.jpg"]);
                Assert(s.Notices.Any(x => x.Contains("24/26")) && s.Notices.All(a.Notices.Contains), "incomplete scan not disclosed");
                Assert(a.Metrics.All(x => x.Base == null && x.Target == null), "sample presented as full batch");
                var mixed = FileAssessment.Read([transparent, opaque]).Evaluate(Catalog.All["img.to.png"]);
                Assert(Metric(mixed, "Alpha retained").Base == null, "mixed alpha silently represented by one file");
                return Task.CompletedTask;
            });
            await check("assessment-oversize-read-bounded-and-evaluate-no-io", () =>
            {
                var path = Path.Combine(directory, "large.png");
                using (var stream = File.Create(path)) stream.SetLength(65L * 1024 * 1024);
                var timer = System.Diagnostics.Stopwatch.StartNew();
                var s = FileAssessment.Read([path]);
                Assert(timer.Elapsed < TimeSpan.FromSeconds(9) && s.Metadata[0].Width == null && s.Notices.Count > 0, "large file not bounded/unknown");
                var copy = Path.Combine(directory, "removed-after-snapshot.png");
                File.Copy(transparent, copy);
                var saved = FileAssessment.Read([copy]);
                File.Delete(copy);
                var after = saved.Evaluate(Catalog.All["img.to.jpg"]);
                Assert(Metric(after, "Alpha retained").Base == 1, "Evaluate rereads filesystem instead of snapshot");
                return Task.CompletedTask;
            });
            if (Tool.Find("ffmpeg") != null && Tool.Find("ffprobe") != null)
                await check("assessment-media-actual-codecs-and-stream-metadata", async () =>
                {
                    var video = Path.Combine(directory, "probe.mp4");
                    var audio = Path.Combine(directory, "probe.wav");
                    using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                    await Tool.Run("ffmpeg", ["-y", "-f", "lavfi", "-i", "color=c=blue:size=160x96:rate=10", "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000", "-t", "1", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", video], stop.Token);
                    await Tool.Run("ffmpeg", ["-y", "-i", video, "-vn", "-ac", "2", "-ar", "48000", "-c:a", "pcm_s16le", audio], stop.Token);
                    var v = FileAssessment.Read([video]);
                    var vm = v.Metadata[0];
                    Assert(vm.Width == 160 && vm.Height == 96 && vm.FrameRate == 10, "wrong ffprobe video metadata");
                    Assert(vm.VideoCodec == "h264" && vm.AudioCodec == "aac" && vm.BitRate > 0 && vm.DurationSeconds is > .8 and < 1.3, "missing actual codec/duration/bitrate");
                    Assert(v.Summary.Contains("h264"), "MP4 container hides actual codec");
                    Assert(Metric(v.Evaluate(Catalog.All["vid.to.hevc"]), "Compatibility estimate").Target < Metric(v.Evaluate(Catalog.All["vid.to.mp4"]), "Compatibility estimate").Target, "codec does not affect compatibility");
                    var a = FileAssessment.Read([audio]);
                    Assert(a.Metadata[0].SampleRate == 48000 && a.Metadata[0].Channels == 2 && a.Metadata[0].AudioCodec == "pcm_s16le", "wrong audio metadata");
                    Assert(Metric(a.Evaluate(Catalog.All["aud.mono"]), "Channels retained").Target == .5, "mono effect missing");
                });
        }
        finally { Settings.Current.Language = previous; }
    }
}
