using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ImageMagick;
using PdfSharp.Pdf.IO;

namespace FormatWheel;

/// <summary>Bounded metadata inspection. Call Read off the UI thread; Evaluate performs no I/O.</summary>
public static class FileAssessment
{
    public const int MaxInspectedFiles = 24;
    public const int MaxImageFrames = 256;
    const long MaxDocumentBytes = 64L * 1024 * 1024;
    const int BudgetMilliseconds = 8000;
    // Native readers cannot be forcibly cancelled safely. At most one may outlive its caller.
    // The bounded stream stops subsequent reads, and this gate prevents abandoned task buildup.
    static readonly SemaphoreSlim ReaderGate = new(1, 1);

    public sealed record Metric(string Label, double? Base, double? Target, string Note);
    public sealed record Assessment(string Title, string Detail, IReadOnlyList<Metric> Metrics, IReadOnlyList<string> Notices);
    public sealed record FileMetadata(string Path, string Kind, string Format)
    {
        public long? Bytes { get; init; }
        public bool IsDirectory { get; init; }
        public bool Inspected { get; init; }
        public string? Error { get; init; }
        public int? Width { get; init; }
        public int? Height { get; init; }
        // Channel presence from Magick Ping, not a decoded-pixel opacity measurement.
        public bool? HasAlpha { get; init; }
        public int? FrameCount { get; init; }
        public bool SwapsDimensions { get; init; }
        public int? PageCount { get; init; }
        public double? DurationSeconds { get; init; }
        public double? FrameRate { get; init; }
        public string? VideoCodec { get; init; }
        public string? AudioCodec { get; init; }
        public string? Container { get; init; }
        public long? BitRate { get; init; }
        public long? VideoBitRate { get; init; }
        public long? AudioBitRate { get; init; }
        public int? SampleRate { get; init; }
        public int? Channels { get; init; }
        public bool? HasAudio { get; init; }
    }

    public sealed record Snapshot(string SourceName, string Summary, int FileCount, int InspectedCount,
        long? TotalBytes, IReadOnlyList<FileMetadata> Metadata, IReadOnlyList<string> Notices)
    {
        /// <summary>Only examines the immutable snapshot and action ID; safe during rendering.</summary>
        public Assessment Evaluate(ActionItem item) => Assess(this, item);
    }

    static string T(string zh, string en) => L.Text(zh, en);
    static string Unknown => T("未知", "Unknown");
    static string F(string zh, string en, params object[] values) => string.Format(CultureInfo.CurrentCulture, T(zh, en), values);
    static IReadOnlyList<TValue> Frozen<TValue>(IEnumerable<TValue> values) => Array.AsReadOnly(values.ToArray());

    public static Snapshot Read(string[] files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var clock = Stopwatch.StartNew();
        var metadata = new List<FileMetadata>(files.Length);
        var notices = new List<string>();
        // Stat every selection, including those beyond the content-inspection cap. Never recurse.
        foreach (var path in files)
        {
            var initial = new FileMetadata(path ?? "", "gen", "");
            try
            {
                string format = Normalize(Path.GetExtension(path ?? "").TrimStart('.'));
                initial = initial with { Format = format, Kind = Kind(format) };
                initial = Bounded(() =>
                {
                    var info = new FileInfo(path!);
                    var attributes = info.Attributes;
                    if (attributes == (FileAttributes)(-1)) throw new FileNotFoundException();
                    if ((attributes & FileAttributes.Directory) != 0)
                        return initial with { IsDirectory = true, Kind = "gen" };
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                        return initial with { Error = T("連結未讀取", "Link not inspected") };
                    return initial with { Bytes = info.Length };
                }, Math.Min(300, Remaining(clock)));
            }
            catch (Exception ex) { initial = initial with { Error = Failure(ex) }; }
            metadata.Add(initial);
        }
        int inspected = 0;
        for (int i = 0; i < Math.Min(MaxInspectedFiles, metadata.Count); i++)
        {
            var entry = metadata[i];
            if (entry.IsDirectory || entry.Error != null) continue;
            if (Remaining(clock) <= 0) break;
            // DOCX docProps/app.xml is a stale pagination cache, not actual rendered pages.
            // Do not inspect it or synthesize a page count for any word-processing document.
            if (entry.Kind is "doc" or "gen") continue;
            inspected++;
            try
            {
                int timeout = Math.Min(entry.Kind is "vid" or "aud" ? 2000 : 1200, Remaining(clock));
                metadata[i] = Bounded(() => Inspect(entry, timeout), timeout) with { Inspected = true };
            }
            catch (Exception ex) { metadata[i] = entry with { Inspected = true, Error = Failure(ex) }; }
        }
        long? bytes = metadata.All(m => m.Bytes.HasValue) ? SafeSum(metadata.Select(m => m.Bytes!.Value)) : null;
        if (files.Length > MaxInspectedFiles || metadata.Any(m => m.Kind is "img" or "pdf" or "vid" or "aud" && !m.Inspected))
            notices.Add(F("已檢查 {0}/{1} 個；未檢查的資料標為未知。", "Checked {0}/{1} files; uninspected metadata is unknown.", inspected, files.Length));
        if (metadata.Any(m => m.Error != null)) notices.Add(T("部分資料讀取失敗、逾時或超出限制；相關數值為未知。", "Some metadata failed, timed out or exceeded limits; values are unknown."));
        if (metadata.Any(m => m.IsDirectory)) notices.Add(T("資料夾不遞迴計算大小。", "Folder contents are not included in size totals."));
        if (metadata.Any(m => m.Format == "docx")) notices.Add(T("DOCX 頁數需實際排版；已略過快取頁數。", "DOCX pagination requires rendering; cached page counts are omitted."));
        if (files.Length > 1) notices.Add(T("批次資料有差異時顯示範圍或未知，不以單一檔案代表整批。", "Batch differences use ranges or unknown, never a representative file."));
        string sourceName = files.Length == 1 ? Path.GetFileName(files[0]) : F("{0} 個檔案", "{0} files", files.Length);
        return new(sourceName, Summary(metadata, bytes), files.Length, inspected, bytes, Frozen(metadata), Frozen(notices));
    }

    static int Remaining(Stopwatch clock) => Math.Max(0, BudgetMilliseconds - (int)clock.ElapsedMilliseconds);
    static TResult Bounded<TResult>(Func<TResult> read, int milliseconds)
    {
        if (milliseconds <= 0 || !ReaderGate.Wait(0)) throw new TimeoutException();
        var task = Task.Run(() => { try { return read(); } finally { ReaderGate.Release(); } });
        // Observe late faults as well as completion; do not leave unobserved native-reader errors.
        _ = task.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        return task.WaitAsync(TimeSpan.FromMilliseconds(milliseconds)).GetAwaiter().GetResult();
    }

    static string Failure(Exception ex) => ex is TimeoutException or OperationCanceledException
        ? T("讀取逾時", "Read timed out") : ex is NotSupportedException
        ? T("超出讀取限制", "Inspection limit exceeded") : T("無法讀取中繼資料", "Metadata unavailable");

    static FileMetadata Inspect(FileMetadata entry, int timeout)
    {
        if (entry.Kind is "vid" or "aud") return Probe(entry, timeout);
        if (entry.Bytes is null or > MaxDocumentBytes) throw new NotSupportedException();
        using var stream = new LimitedStream(entry.Path, timeout);
        if (entry.Kind == "pdf")
        {
            using var pdf = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
            return entry with { PageCount = pdf.PageCount };
        }
        using var images = new MagickImageCollection();
        images.Ping(stream, new MagickReadSettings { FrameCount = MaxImageFrames + 1 });
        if (images.Count == 0) throw new InvalidDataException();
        var first = images[0];
        string format = Normalize(first.Format.ToString().ToLowerInvariant());
        return entry with
        {
            Format = format,
            Width = checked((int)first.Width), Height = checked((int)first.Height),
            HasAlpha = images.Any(im => im.HasAlpha) ? true : images.Count > MaxImageFrames ? null : false,
            FrameCount = images.Count <= MaxImageFrames ? images.Count : null,
            SwapsDimensions = (int)first.Orientation is >= 5 and <= 8
        };
    }

    // Bounds both source bytes and repeated parser reads/seeks, without changing global Magick limits.
    sealed class LimitedStream : Stream
    {
        readonly FileStream file;
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly int timeout;
        long read;
        public LimitedStream(string path, int milliseconds)
        {
            timeout = milliseconds;
            file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (file.Length > MaxDocumentBytes) { file.Dispose(); throw new NotSupportedException(); }
        }
        void Guard(int count = 0)
        {
            if (clock.ElapsedMilliseconds >= timeout) throw new TimeoutException();
            read += count;
            if (read > MaxDocumentBytes * 2) throw new NotSupportedException();
        }
        public override int Read(byte[] buffer, int offset, int count) { Guard(count); return file.Read(buffer, offset, count); }
        public override int Read(Span<byte> buffer) { Guard(buffer.Length); return file.Read(buffer); }
        public override int ReadByte() { Guard(1); return file.ReadByte(); }
        public override long Seek(long offset, SeekOrigin origin) { Guard(); return file.Seek(offset, origin); }
        public override long Position { get => file.Position; set { Guard(); file.Position = value; } }
        public override long Length => file.Length;
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) file.Dispose(); base.Dispose(disposing); }
    }

    static FileMetadata Probe(FileMetadata entry, int timeout)
    {
        string executable = Tool.Find("ffprobe") ?? throw new FileNotFoundException("ffprobe");
        using var stop = new CancellationTokenSource(timeout);
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-v", "error", "-protocol_whitelist", "file,pipe", "-probesize", "1048576", "-analyzeduration", "1000000", "-show_entries", "format=format_name,duration,bit_rate:stream=codec_type,codec_name,width,height,duration,avg_frame_rate,r_frame_rate,bit_rate,sample_rate,channels:stream_disposition=attached_pic", "-of", "json", "-i", System.IO.Path.GetFullPath(entry.Path) }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new IOException();
        using var kill = stop.Token.Register(() => { try { process.Kill(true); } catch { } });
        try
        {
            var output = ReadLimited(process.StandardOutput, 64 * 1024, stop.Token);
            var errors = ReadLimited(process.StandardError, 8 * 1024, stop.Token);
            Task.WhenAll(output, errors, process.WaitForExitAsync(stop.Token)).GetAwaiter().GetResult();
            if (process.ExitCode != 0) throw new InvalidDataException();
            using var json = JsonDocument.Parse(output.Result);
            var root = json.RootElement;
            var streams = root.TryGetProperty("streams", out var list) ? list.EnumerateArray().ToArray() : [];
            var video = streams.FirstOrDefault(s => Str(s, "codec_type") == "video" && !(s.TryGetProperty("disposition", out var d) && Number(d, "attached_pic") == 1));
            var audio = streams.FirstOrDefault(s => Str(s, "codec_type") == "audio");
            if (entry.Kind == "vid" && video.ValueKind == JsonValueKind.Undefined || entry.Kind == "aud" && audio.ValueKind == JsonValueKind.Undefined) throw new InvalidDataException();
            root.TryGetProperty("format", out var format);
            return entry with
            {
                Container = Str(format, "format_name"),
                Width = Int(video, "width"), Height = Int(video, "height"),
                DurationSeconds = Number(format, "duration") ?? Number(video, "duration") ?? Number(audio, "duration"),
                FrameRate = Rational(Str(video, "avg_frame_rate")) ?? Rational(Str(video, "r_frame_rate")),
                VideoCodec = Str(video, "codec_name"), AudioCodec = Str(audio, "codec_name"),
                BitRate = Long(format, "bit_rate"), VideoBitRate = Long(video, "bit_rate"), AudioBitRate = Long(audio, "bit_rate"),
                SampleRate = Int(audio, "sample_rate"), Channels = Int(audio, "channels"), HasAudio = audio.ValueKind != JsonValueKind.Undefined
            };
        }
        finally { try { if (!process.HasExited) process.Kill(true); } catch { } }
    }

    static async Task<string> ReadLimited(StreamReader reader, int limit, CancellationToken token)
    {
        var result = new StringBuilder();
        var buffer = new char[2048];
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false)) != 0)
        {
            if (result.Length + count > limit) throw new NotSupportedException();
            result.Append(buffer, 0, count);
        }
        return result.ToString();
    }
    static string? Str(JsonElement e, string key) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var value) ? value.ToString() : null;
    static double? Number(JsonElement e, string key) => double.TryParse(Str(e, key), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n) && n > 0 ? n : null;
    static int? Int(JsonElement e, string key) => Number(e, key) is double n && n <= int.MaxValue ? (int)n : null;
    static long? Long(JsonElement e, string key) => long.TryParse(Str(e, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n > 0 ? n : null;
    static double? Rational(string? text)
    {
        var parts = (text ?? "").Split('/');
        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var n)) return null;
        if (parts.Length == 2) { if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var d) || d == 0) return null; n /= d; }
        return n > 0 && double.IsFinite(n) ? n : null;
    }
    static long? SafeSum(IEnumerable<long> values) { try { return values.Aggregate(0L, (a, b) => checked(a + b)); } catch (OverflowException) { return null; } }
    static string Normalize(string format) => format.ToLowerInvariant() switch { "jpeg" or "jpe" => "jpg", "tif" => "tiff", "aif" => "aiff", "heif" => "heic", "png8" or "png24" or "png32" or "png64" => "png", var f => f };
    static string Kind(string format) => format switch
    {
        "png" or "jpg" or "heic" or "webp" or "avif" or "gif" or "tiff" or "bmp" or "ico" or "jxl" => "img",
        "pdf" => "pdf",
        "mp4" or "mov" or "m4v" or "mkv" or "avi" or "webm" or "mpg" or "mpeg" or "wmv" or "ts" => "vid",
        "mp3" or "m4a" or "aac" or "wav" or "flac" or "aiff" or "opus" or "ogg" or "alac" or "m4r" or "wma" => "aud",
        "txt" or "md" or "markdown" or "html" or "htm" or "rtf" or "doc" or "docx" or "odt" => "doc", _ => "gen"
    };
    static string Source(IReadOnlyList<FileMetadata> metadata)
    {
        var names = metadata.Select(m => m.IsDirectory ? T("資料夾", "Folder") : string.IsNullOrEmpty(m.Format) ? Unknown : m.Format.ToUpperInvariant() + (m.Kind == "vid" ? " / " + (m.VideoCodec ?? Unknown) : m.Kind == "aud" ? " / " + (m.AudioCodec ?? Unknown) : "")).Distinct().ToArray();
        return names.Length == 0 ? Unknown : names.Length <= 2 ? string.Join(" + ", names) : T("混合格式", "Mixed formats");
    }
    static string Range(IEnumerable<double?> numbers, string suffix, string format = "0.##")
    {
        var all = numbers.ToArray();
        if (all.Length == 0 || all.Any(n => !n.HasValue)) return Unknown;
        double low = all.Min(n => n!.Value), high = all.Max(n => n!.Value);
        return low.ToString(format, CultureInfo.CurrentCulture) + (low == high ? "" : "–" + high.ToString(format, CultureInfo.CurrentCulture)) + suffix;
    }
    static string Summary(IReadOnlyList<FileMetadata> m, long? bytes)
    {
        string line = Source(m) + " · " + F("{0} 個檔案", "{0} files", m.Count) + " · " + (bytes.HasValue ? Size(bytes.Value) : T("大小未知", "Size unknown"));
        if (m.Count == 0) return line;
        string[] kinds = m.Select(x => x.Kind).Distinct().ToArray();
        if (kinds.Length != 1) return line + "\n" + T("混合來源；詳細資料依檔案而異", "Mixed sources; metadata varies by file");
        string detail = kinds[0] switch
        {
            "img" => Range(m.Select(x => (double?)x.Width), "", "0") + " × " + Range(m.Select(x => (double?)x.Height), " px", "0") + " · " + Range(m.Select(x => (double?)x.FrameCount), T(" 幀", " frames"), "0") + " · " + (m.All(x => x.HasAlpha == true) ? T("有 alpha", "Alpha") : m.All(x => x.HasAlpha == false) ? T("無 alpha", "No alpha") : T("alpha 未知／混合", "Alpha unknown/mixed")),
            "pdf" => Range(m.Select(x => (double?)x.PageCount), T(" 頁／檔", " pages/file"), "0"),
            "vid" => Range(m.Select(x => (double?)x.Width), "", "0") + " × " + Range(m.Select(x => (double?)x.Height), "", "0") + " · " + Range(m.Select(x => x.DurationSeconds), " s") + " · " + Range(m.Select(x => x.FrameRate), " fps"),
            "aud" => Range(m.Select(x => x.DurationSeconds), " s") + " · " + Range(m.Select(x => x.SampleRate / 1000.0), " kHz") + " · " + Range(m.Select(x => (double?)x.Channels), T(" 聲道", " ch"), "0") + " · " + Range(m.Select(x => x.BitRate / 1000.0), " kb/s", "0"),
            "doc" => T("頁數未知；未執行文件排版", "Page count unknown; document not rendered"), _ => ""
        };
        return line + (detail.Length == 0 ? "" : "\n" + detail);
    }
    static string Size(long bytes) => bytes < 1024 ? bytes + " B" : bytes < 1024 * 1024 ? (bytes / 1024.0).ToString("0.#") + " KiB" : bytes < 1024L * 1024 * 1024 ? (bytes / (1024.0 * 1024)).ToString("0.#") + " MiB" : (bytes / (1024.0 * 1024 * 1024)).ToString("0.#") + " GiB";

    static Assessment Assess(Snapshot s, ActionItem item)
    {
        var rows = new List<Metric>();
        var notices = new List<string>(s.Notices);
        string id = item.Id, title = item.Label, detail = item.Detail;
        var m = s.Metadata;
        bool known = m.Count > 0 && m.All(x => x.Error == null && (x.Inspected || x.Kind == "doc") && !x.IsDirectory);
        double? Common(Func<FileMetadata, double?> value)
        {
            if (!known) return null;
            var values = m.Select(value).ToArray();
            if (values.Any(v => !v.HasValue) || values.Distinct().Count() != 1) return null;
            return values[0];
        }
        void Row(string zh, string en, double? baseline, double? target, string zhNote, string enNote)
            => rows.Add(new(T(zh, en), known ? baseline : null, known ? target : null, T(zhNote, enNote)));
        void Quality(double? target, string zhNote, string enNote) => Row("畫質保留", "Quality retained", 1, target, zhNote, enNote);
        void Formats(string target, string? codec = null)
        {
            double? efficiency = Efficiency(target, codec);
            if (id == "img.compress" && IsLossyImage(target) && efficiency.HasValue) efficiency = Math.Min(1, efficiency.Value + .2);
            Row("壓縮效率（估計）", "Efficiency estimate", Common(Efficiency), known ? efficiency : null,
                "格式／編碼相對估計，非實際縮小比例。", "Relative format/codec estimate; not actual size savings.");
            Row("相容性（估計）", "Compatibility estimate", Common(Compatibility), known ? Compatibility(target, codec) : null,
                "依格式與編碼估計，仍取決於播放軟體。", "Relative format + codec support; depends on the application.");
        }
        void Layout(double? target, string zhNote, string enNote) => Row("版面保留", "Layout retained", 1, target, zhNote, enNote);
        Assessment Result()
        {
            if (rows.Any(r => r.Base is null || r.Target is null)) notices.Add(T("未知或混合資料不顯示確定分數。", "Unknown or mixed values have no definite score."));
            return new(title, detail, Frozen(rows), Frozen(notices.Distinct()));
        }
        if (id.StartsWith("gen.", StringComparison.Ordinal) || !Catalog.All.ContainsKey(id)) return Result();
        string kind = id.Split('.')[0];
        if (m.Count == 0 || m.Any(x => x.Kind != kind)) return Result();
        string op = id.Split('.').Last();

        if (kind == "img")
        {
            string? target = id.Contains(".to.", StringComparison.Ordinal) ? op : id is "img.cutout" or "img.backdrop" ? "png" : m.Select(x => x.Format).Distinct().Count() == 1 ? m[0].Format : null;
            if (id == "img.to.pdf")
            {
                title = item.DisplayLabel(m.Select(x => x.Path).ToArray());
                detail = F("{0} 張圖片 → 1 個 PDF（{0} 頁）", "{0} images → 1 PDF ({0} pages)", s.FileCount);
                Quality(1, "保留首幀像素；每個來源檔案一頁。", "First-frame pixels retained; one page per source file.");
                Formats("pdf");
            }
            else if (id == "img.ocr")
            {
                Layout(0, "OCR 輸出純文字，不保留圖片與原版面。", "OCR outputs plain text; images and layout are not retained.");
                notices.Add(T("辨識準確度未知，需人工校對。", "Recognition accuracy is unknown; review the text."));
                return Result();
            }
            else
            {
                if (target == null) Quality(null, "來源格式不同，重新編碼影響未知。", "Mixed source formats; re-encoding effects are unknown.");
                else if (id is "img.cutout" or "img.backdrop") Quality(null, "會改動像素內容，無法預估品質分數。", "Pixel content changes; quality cannot be predicted.");
                else Quality(id == "img.half" ? .6 : id == "img.mono" ? .6 : target == "gif" ? .6 : IsLossyImage(target) ? id == "img.compress" ? .6 : .8 : target is "png" or "bmp" or "tiff" ? 1 : null,
                    target == "png" ? "保留現有像素，無法還原來源已損失的細節。" : "重新編碼影響的估計，並非來源品質檢測。",
                    target == "png" ? "Keeps existing pixels; cannot restore previously lost detail." : "Estimated re-encoding impact, not a source quality measurement.");
                if (target != null) Formats(target);
            }
            if (id != "img.cutout")
            {
                double? alpha = Common(x => x.HasAlpha.HasValue ? x.HasAlpha.Value ? 1 : 0 : null);
                double? retained = target == "jpg" || id == "img.backdrop" ? 0 : target is "png" or "webp" or "tiff" or "avif" or "pdf" ? alpha : alpha == 0 ? 0 : null;
                Row("透明度保留", "Alpha retained", alpha, alpha.HasValue ? retained : null,
                    target == "jpg" ? "JPG 移除 alpha，透明區域填白。" : target == "pdf" ? "保留圖片透明通道，PDF 閱讀器決定頁面底色。" : "依來源 alpha 通道；不會憑空新增透明度。",
                    target == "jpg" ? "JPG removes alpha and fills transparent areas white." : target == "pdf" ? "Keeps image alpha; the PDF viewer determines the page background." : "Based on source alpha; conversion does not create transparency.");
            }
            if (id == "img.half")
                Row("像素數保留", "Pixel count retained", 1, Common(x => x.Width is int w && x.Height is int h && w > 0 && h > 0 ? (double)Math.Max(1, w / 2) * Math.Max(1, h / 2) / w / h : null), "長寬各減半（至少 1 px）。", "Width and height halved (minimum 1 px).");
            if (id == "img.rotate") notices.Add(T("自動校正方向後順時針旋轉 90°；長寬交換。", "Auto-orients, then rotates 90° clockwise; width and height swap."));
            if (id == "img.strip") notices.Add(T("移除 EXIF／GPS；圖片會重新編碼，JPG 並非無損清除。", "Removes EXIF/GPS; re-encodes pixels, including lossy JPG."));
            if (id == "img.compress") notices.Add(T("降低編碼品質並去除中繼資料；僅輸出較小檔案。", "Lowers encoder quality and strips metadata; saves only if smaller."));
            if (id == "img.cutout") notices.Add(T("模型建立新的透明遮罩；不代表還原原始細節。", "Model creates a new alpha mask; it does not restore original detail."));
            if (id == "img.backdrop") notices.Add(T("縮至 1400 × 1000 範圍，加上每邊 80 px 背景。", "Fits within 1400 × 1000; adds 80 px of background on each edge."));
            if (id == "img.mono") notices.Add(T("轉為灰階，原有色彩將移除。", "Converts to grayscale; original colors are removed."));
            if (m.Any(x => x.FrameCount > 1))
            {
                Row("多幀保留", "Frames retained", 1, 0, "此操作只讀取首幀，不保留動畫／其他頁面。", "Only the first frame is read; animation/other pages are lost.");
                notices.Add(T("多幀圖片僅使用首幀。", "Multi-frame images use only the first frame."));
            }
            if (m.Any(x => x.Inspected && x.Error == null && x.FrameCount == null)) notices.Add(T("幀數超出讀取上限；動畫狀態未知。", "Frame count exceeds inspection limits; animation is unknown."));
            return Result();
        }
        if (kind == "pdf")
        {
            if (id == "pdf.merge")
            {
                string pages = m.All(x => x.PageCount.HasValue) ? m.Sum(x => (long)x.PageCount!.Value).ToString(CultureInfo.CurrentCulture) : Unknown;
                title = item.Label;
                detail = F("{0} 個 PDF → 1 個 PDF（{1} 頁）", "{0} PDFs → 1 PDF ({1} pages)", s.FileCount, pages);
                Layout(1, "按選取順序合併頁面；文件層級功能可能改變。", "Pages merge in selection order; document-level features may change.");
            }
            else if (op is "txt" or "md" or "html" or "rtf" or "docx" || id == "pdf.ocr")
            {
                Layout(0, "僅提取文字，不重建欄位、圖片、表格與原版面。", "Text only; columns, images, tables and layout are not recreated.");
                notices.Add(id == "pdf.ocr" ? T("OCR 準確度未知，需人工校對。", "OCR accuracy is unknown; review the text.") : T("掃描頁面可能沒有可提取的文字。", "Scanned pages may have no extractable text."));
            }
            else if (id is "pdf.strip" or "pdf.rotate" or "pdf.split")
            {
                Layout(1, "保留頁面內容，不重新點陣化。", "Page content is retained without rasterization.");
                if (id == "pdf.strip") notices.Add(T("清除文件屬性與 XMP；不清除內文、註解或附件。", "Clears properties/XMP; content, comments and attachments remain."));
                if (id == "pdf.rotate") notices.Add(T("每頁順時針旋轉 90°。", "Rotates each page 90° clockwise."));
                if (id == "pdf.split") detail = T("每頁輸出為獨立 PDF。", "Each page becomes a separate PDF.");
            }
            else if (op is "png" or "jpg" or "long" || id is "pdf.compress" or "pdf.mono")
            {
                Quality(op == "jpg" || id is "pdf.compress" or "pdf.mono" ? .6 : .8, "頁面點陣化：最多 2×，最長邊上限 4096 px。", "Rasterizes pages: up to 2×, with a 4096 px longest-edge limit.");
                Row("可編輯內容保留", "Editable content retained", 1, 0, "文字、表單與連結不再可編輯。", "Text, forms and links no longer remain editable.");
                if (id == "pdf.compress") notices.Add(T("重建為有損圖片 PDF；僅儲存較小結果。", "Rebuilds a lossy image PDF; saves only if smaller."));
                if (id == "pdf.mono") notices.Add(T("轉為灰階圖片 PDF，移除原有色彩。", "Rebuilds a grayscale image PDF; removes original colors."));
                if (op == "long") notices.Add(T("每頁縮至寬 1242 px；長圖約每 15000 px 分段。", "Pages resize to 1242 px wide; long images split near 15000 px."));
            }
            if (!rows.Any(r => r.Label == T("版面保留", "Layout retained"))) Layout(.8, "頁面外觀轉為圖片，細節受解析度限制。", "Page appearance becomes an image; resolution limits fine detail.");
            if (!rows.Any(r => r.Label == T("可編輯內容保留", "Editable content retained")))
                Row("可編輯性", "Editability", .4, op is "txt" or "md" or "html" or "rtf" or "docx" || id == "pdf.ocr" ? 1 : .4, "格式功能估計；提取文字可編輯但不保留版面。", "Format capability estimate; extracted text loses page layout.");
            Formats(op is "png" or "jpg" ? op : op == "long" ? "png" : op is "txt" or "md" or "html" or "rtf" or "docx" ? op : id == "pdf.ocr" ? "txt" : "pdf");
            return Result();
        }
        if (kind == "doc")
        {
            if (id is "doc.count" or "doc.copy") return Result();
            if (id == "doc.strip") { Layout(1, "保留文件內容；清除作者等文件屬性。", "Keeps document content; clears author and similar properties."); notices.Add(T("不移除內文、註解與修訂記錄。", "Body text, comments and tracked changes remain.")); }
            else if (op is "txt" or "md" or "html") Layout(0, "輸出提取的文字，不重建原格式或標題階層。", "Exports extracted text; formatting and heading levels are not rebuilt.");
            else if (id.StartsWith("doc.to.", StringComparison.Ordinal)) Layout(null, "由 LibreOffice 轉換，字型與排版保留程度未知。", "LibreOffice conversion; font and layout fidelity is unknown.");
            Row("可編輯性", "Editability", 1, op == "pdf" ? .4 : 1, "格式功能估計，非實際內容完整度。", "Format capability estimate, not measured content fidelity.");
            if (id == "doc.strip") { if (m.Select(x => x.Format).Distinct().Count() == 1) Formats(m[0].Format); }
            else Formats(op);
            return Result();
        }
        if (kind is "vid" or "aud")
        {
            bool video = kind == "vid";
            if (id is "vid.mute" or "vid.strip")
            {
                Quality(1, "視訊串流直接複製，不重新編碼。", "Video stream is copied without re-encoding.");
                Row("相容性（估計）", "Compatibility estimate", Common(Compatibility), Common(Compatibility), "容器與視訊編碼保持不變。", "Container and video codec stay the same.");
                notices.Add(id == "vid.mute" ? T("移除所有音訊。", "Removes all audio.") : T("移除容器／串流中繼資料與章節；不遮蔽畫面內容。", "Removes container/stream metadata and chapters; visible content remains."));
                return Result();
            }
            if (id == "vid.cover") { Quality(.8, "擷取首幀為有損 JPEG。", "First frame becomes a lossy JPEG image."); notices.Add(T("僅一張靜態圖片，不含音訊。", "One still image; no audio.")); return Result(); }
            if (id == "vid.to.gif")
            {
                Quality(.4, "前 30 秒、12 fps、寬 640 px、256 色，無音訊。", "First 30 s, 12 fps, 640 px wide, 256 colors; no audio.");
                Formats("gif"); return Result();
            }
            bool audioOutput = !video || op is "mp3" or "m4a" or "wav";
            if (audioOutput)
            {
                string target = op is "compress" or "mono" or "loud" or "fast" or "alac" ? "m4a" : op;
                string? codec = op switch { "mp3" => "mp3", "wav" => "pcm_s16le", "aiff" => "pcm_s16be", "flac" => "flac", "alac" => "alac", "opus" => "opus", "m4a" or "m4r" or "compress" or "mono" or "loud" or "fast" => "aac", _ => null };
                if (codec == null) return Result();
                bool lossless = codec is "flac" or "alac";
                Row("音質保留", "Audio quality retained", 1, lossless ? 1 : codec.StartsWith("pcm_", StringComparison.Ordinal) ? null : op == "compress" ? .6 : .8,
                    lossless ? "保留解碼後音訊，無法修復來源已損失的音質。" : "轉碼影響的估計；PCM 輸出為 16 位元。",
                    lossless ? "Keeps decoded audio; cannot repair previously lost sound quality." : "Estimated transcoding impact; PCM output is 16-bit.");
                string settings = op switch { "mp3" => T("MP3 可變位元率", "MP3 variable bitrate"), "wav" => "PCM 16-bit", "aiff" => "PCM 16-bit", "opus" => "Opus 128 kb/s", "flac" => "FLAC", "alac" => "ALAC / M4A", "compress" => "AAC 96 kb/s", _ => "AAC 192 kb/s" };
                notices.Add(settings);
                // Audio extraction compares the actual source audio codec, not its video codec.
                Row("壓縮效率（估計）", "Efficiency estimate", Common(x => Efficiency(x.Format, x.AudioCodec)), Efficiency(target, codec), "依音訊編碼估計，非實際縮小比例。", "Audio codec estimate; not actual size savings.");
                Row("相容性（估計）", "Compatibility estimate", Common(x => Compatibility(x.Format, x.AudioCodec)), Compatibility(target, codec), "依容器與音訊編碼估計。", "Relative container + audio codec support.");
                if (video) notices.Add(T("只擷取音軌；影像會移除。", "Extracts audio only; video is removed."));
                if (op == "mono") Row("聲道保留", "Channels retained", 1, Common(x => x.Channels is > 0 ? 1.0 / x.Channels.Value : null), "合併至 1 聲道，AAC 192 kb/s。", "Mixes down to one channel, AAC 192 kb/s.");
                if (op == "loud") notices.Add(T("統一響度至目標 −16 LUFS；會重新編碼。", "Normalizes loudness toward −16 LUFS; re-encodes audio."));
                if (op == "fast") notices.Add(T("加速至 1.5 倍；預期時長為來源的 2/3。", "Speeds up 1.5×; expected duration is two thirds of the source."));
                if (op == "m4r") notices.Add(T("只保留前 40 秒。", "Keeps only the first 40 seconds."));
                return Result();
            }
            string? videoCodec = op is "mp4" or "mov" or "compress" or "720p" or "fast" or "slow" ? "h264" : op == "hevc" ? "hevc" : op == "webm" ? "vp9" : null;
            if (videoCodec == null) return Result();
            Quality(op == "compress" ? .6 : .8, "以來源為基準的轉碼預估，不代表來源實測畫質。", "Transcoding estimate relative to the source, not measured quality.");
            Formats(op == "mov" ? "mov" : op == "webm" ? "webm" : "mp4", videoCodec);
            notices.Add(op switch
            {
                "mp4" or "mov" => T("重新編碼為 H.264 影片與 AAC 音訊。", "Re-encodes to H.264 video and AAC audio."),
                "hevc" => T("HEVC／AAC；較新裝置支援較佳。", "HEVC/AAC; support is better on newer devices."),
                "webm" => T("VP9／Opus；適合網頁播放。", "VP9/Opus; suitable for web playback."),
                "compress" => T("最高 1080p · H.264 · AAC 128 kb/s", "Up to 1080p · H.264 · AAC 128 kb/s"),
                "720p" => T("最高 720p · H.264 · AAC 128 kb/s", "Up to 720p · H.264 · AAC 128 kb/s"),
                "fast" => T("畫面和聲音加速至 2 倍；時長減半。", "Video and audio speed up 2×; duration halves."),
                _ => T("畫面和聲音減速至 0.5 倍；時長加倍。", "Video and audio slow to 0.5×; duration doubles.")
            });
            if (op is "720p" or "compress") Row("解析度保留", "Resolution retained", 1, Common(x => x.Height is > 0 ? Math.Pow(Math.Min(1, (op == "720p" ? 720.0 : 1080.0) / x.Height.Value), 2) : null), "不放大；維持比例，寬度取偶數。", "No upscale; keeps aspect ratio and rounds width to even pixels.");
            if (op is "fast" or "slow" && m.Any(x => x.HasAudio == false)) notices.Add(T("來源無音軌；此引擎的音訊濾鏡可能使操作失敗。", "Source has no audio; this engine's audio filter may fail."));
            return Result();
        }
        return Result();
    }

    static bool IsLossyImage(string format) => format is "jpg" or "webp" or "heic" or "avif";
    // Deliberately coarse five-segment format tendencies. These are NOT ratios, quality
    // measurements, predicted byte counts or promised savings; content/settings dominate.
    static double? Efficiency(FileMetadata m) => Efficiency(m.Format, m.Kind == "vid" ? m.VideoCodec : m.Kind == "aud" ? m.AudioCodec : null);
    static double? Efficiency(string format, string? codec = null)
    {
        if (codec != null) return codec switch { "h264" or "aac" or "mp3" => .8, "hevc" or "vp9" or "av1" or "opus" => 1, "flac" or "alac" => .4, "pcm_s16le" or "pcm_s16be" or "pcm_s24le" or "pcm_f32le" => .2, _ => null };
        return format switch { "png" or "pdf" or "docx" or "odt" => .4, "tiff" or "bmp" or "rtf" or "doc" => .2, "txt" or "md" or "html" => .8, "jpg" => .6, "webp" or "heic" => .8, "avif" or "jxl" => 1, "gif" => .6, _ => null };
    }
    static double? Compatibility(FileMetadata m) => Compatibility(m.Format, m.Kind == "vid" ? m.VideoCodec : m.Kind == "aud" ? m.AudioCodec : null);
    static double? Compatibility(string format, string? codec = null)
    {
        if (codec != null)
        {
            double? support = codec switch { "h264" or "mp3" or "pcm_s16le" => 1, "aac" or "pcm_s16be" => .8, "hevc" or "vp9" or "flac" => .6, "opus" or "av1" or "alac" => .4, _ => null };
            double? container = format switch { "mp4" or "m4a" or "mp3" or "wav" or "aac" => 1, "mov" or "webm" or "aiff" or "flac" or "ogg" => .8, "mkv" or "m4r" or "opus" => .6, _ => null };
            return support.HasValue && container.HasValue ? Math.Min(support.Value, container.Value) : null;
        }
        return format switch { "png" or "jpg" or "pdf" or "gif" or "txt" or "html" => 1, "webp" or "bmp" or "docx" or "rtf" or "md" => .8, "tiff" or "doc" or "odt" => .6, "avif" or "heic" => .4, "jxl" => .2, _ => null };
    }
}
