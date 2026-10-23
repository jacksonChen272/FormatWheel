# Windows 0.3.1 validation — 2026-10-23 (Asia/Shanghai)

Independent Windows x64 preview. Upstream baseline: `1eb19a1903ffd26ef31a183b7a8f5ea14ae7e4cc`. Original MIT attribution and Swift source preserved.

## 0.3.1 result banners

- Removed only the property card's connector; the card still follows the hovered sector.
- Success and failure share a rounded, centered banner with outlined Impact headings, full wrapped messages and output directories. Completion size measurements and verified privacy-star behavior are retained. Cancellation has a separate neutral heading.
- Blur uses an in-memory desktop sample taken after hiding the progress window, with a 34-DIP blur clipped to the rounded panel. The sample is not saved or uploaded. Disabled blur or unavailable capture falls back to a translucent readable panel.
- Native layered-window styles prevent activation and pass mouse input through. Success/cancellation close automatically at 5 seconds; failure at 8. Reduced animations retain automatic closing without fades.
- New checks cover output-directory preservation, centered placement with negative monitor coordinates at 100–200% DPI, three-language rendering and full long-path text, transparent corners, native passive styles, and actual automatic window lifetime.
- Computer Use inspected Traditional/Simplified Chinese and English previews, an actual transient failure banner, and a successful PNG-to-JPG conversion at 125% DPI. The actual success banner showed the source/action, measured sizes and full output folder; the failure banner showed visible blur and retained focus, then disappeared automatically. The HEIC hover card was visually checked without its connector. Input pass-through is verified by native layered-window styles; a manual preview click arrived after the banner expired and is not claimed as an active-overlay click-through test.
- Earlier results below retain their original version and scope. Physical multi-monitor and the full global drag gesture are not newly certified by this patch.

## 0.3.0 properties, comparisons and privacy stars

- Added real image/PDF/media metadata inspection, immutable off-thread snapshots, a 24-file content-inspection cap, frame-count limits, bounded metadata reads and explicit unknown/error states. Format/codec bars are relative estimates, not measured quality or predicted byte counts. Generic file actions do not fabricate quality bars.
- Added EXIF/XMP category deduplication, supported PDF/Word metadata weights, camera-date-only video scoring, partial/unsupported/error counts and output rescanning. Unknown or unsupported zero scores are never presented as verified clean. Clean-star animation requires every output to be checked successfully.
- Expanded regression coverage: privacy weighting/states/batch/localization/rescan; real transparent/opaque/animated/corrupt/oversize image fixtures; no false quality upgrade; alpha loss and frame limits; PDF page order, alpha mask and batch count; metadata-only evaluation; actual FFprobe codecs; property-bar segments; measured output sizes; three-language WPF rendering at 1440 × 900 and 960 × 540 across all eight sectors, with bounded cards and center dismissal.
- Testing found a real PNG-to-PDF compatibility issue: PDFsharp rejected some indexed transparent PNGs. Embedding now uses explicit PNG32 with orientation correction. Three-image PDF page count/order and the transparency soft mask are checked.
- Multi-frame alpha predictions use the first frame actually converted, not alpha found only in a later frame. A two-frame GIF fixture verifies the predicted loss against the PNG output. A transparent TIFF fixture exposed Magick 15 Ping underreporting alpha; negative TIFF header-only results are now unknown, with an explicit notice. Privacy scans check cancellation between files and propagate it to FFprobe; an in-progress native image/PDF read may still need to finish before cancellation is observed.
- Computer Use at 125% DPI inspected Traditional Chinese image properties and five detected privacy stars, Shift switching to tools, the strip preview on the opposite side, English three-image PDF merging, card dismissal at the center, and Simplified Chinese video properties/GIF tradeoffs. Actual image stripping and three-image PDF conversion produced outputs. Completion-window visibility and dimensions were logged; this borderless notification was not listed by Computer Use, so its animation was not visually certified.
- GUI tests used separate FORMATWHEEL_DATA profiles; the user's running 0.2.2 instance/settings were preserved. Explicit isolated profiles now have separate instance mutexes. No real personal documents were used for conversion tests.
- Final standalone test counts, exact source commit, SHA-256 and CI URL are recorded in the release's test-report.json / RELEASE-MANIFEST.json. Earlier version results below are historical and retain their original scope.

## 0.2.2 tour layout

The only behavior change is onboarding layout: 1180 × 790 default window, 660 × 660 practice canvas, wider spotlight padding, separate sample-file/tray positions, a circular center spotlight, and scrollable explanatory text with navigation kept outside the scroll region. The previous 0.2.1 sound and conversion changes remain unchanged.

Computer Use inspected all eight English steps, including the wider opening frame, sample file, JPG/tools focus, center and tray. Traditional and Simplified Chinese use the same layout. Build and the existing standalone self-test are rerun for the release; no new tests that merely repeat layout constants were added. The detailed 0.2.1 and 0.2.0 results below retain their original scope.
## 0.2.1 feedback regression checks

- Added six regressions: enter/leave/re-entry with one tick per transition; page changes, empty and unavailable options; hint bounds for all eight directions at four DPI scales and five wheel positions; valid, non-silent, distinct PCM cues; serialized opening/hover playback with bounded hover backlog; mute and pending-hover cancellation.
- Computer Use at 125% DPI verified TXT → MD descriptions moving alongside their sectors, center cancellation hiding the card, the unavailable PDF explanation, Shift and scroll changing the card immediately, and an empty tool slot hiding the previous description.
- The diagnostic trace recorded Windows audio API success for opening, hover and page-change cues. This confirms playback completion reported by Windows, not a human listening test or the speaker's volume/mute setting. Optional diagnostic trace: `FORMATWHEEL_FEEDBACK_TRACE=1`; off by default.
- Sound data stays valid until synchronous playback finishes on a background worker. No new cue calls Stop/Dispose on the previous cue. Pending hover ticks are coalesced and cleared on leaving to avoid delayed feedback.
- The full 82-check local run includes existing FFmpeg and LibreOffice conversions. Prior 0.2.0 installer/tour UI validation below is historical; the installer and tour were not changed by this patch.
## Automated checks

- .NET SDK 10.0.402, self-contained .NET 10.0.13, Windows x64. Builds succeeded without compiler warnings/errors.
- A standalone EXE was copied into a new folder without DLLs, model or JSON sidecars. It passed **82 checks, 0 failures**, exit code 0. Its embedded assets were extracted into fresh isolated test data. FFmpeg and LibreOffice were separately installed optional engines.
- Image round trips: PNG, JPG, WebP, TIFF, GIF, HEIC and AVIF; resize/rotation dimensions; original SHA-256 preserved; unique output naming.
- U²-Net inference produced a correctly sized PNG with alpha. Windows OCR recognized a generated `HELLO 123` fixture. These checks do not establish real-photo segmentation quality or all OCR languages.
- PDF conversion, merge, split, rotation, metadata, page count and image reconstruction; ZIP preserves empty directories; SHA-256 and Unicode DOCX round trip; mixed input rejection.
- All 27 media actions produced outputs readable by FFprobe, using a generated test pattern and sine wave.
- LibreOffice PDF, DOCX, RTF, ODT and DOC outputs created; PDF opened successfully and document text was read back.
- English labels/details for every catalog action; live English/Traditional/Simplified action switching; culture detection; Simplified Chinese string termination across all translation keys.
- Download hash acceptance/rejection and cancellation, onboarding settings round trip, drag-trigger state/reset including negative coordinates, wheel placement at 100%, 125%, 150% and 200% DPI.

Example reproduction (PowerShell):

```powershell
$env:FORMATWHEEL_DATA = Join-Path $PWD 'isolated-data'
./FormatWheel-Windows-x64.exe --install-test ffmpeg
./FormatWheel-Windows-x64.exe --install-test libreoffice
./FormatWheel-Windows-x64.exe --self-test ./fresh-results
```

Use a new results folder for each run. Test fixtures are generated; no personal documents were used. External native inference/conversion checks ran outside the restricted automation process sandbox. The application manifest remains `asInvoker`.

## Installer checks and corrections

- Downloaded the official LibreOffice MSI and matched its published SHA-256. Confirmed the immutable official archive has the same hash.
- Exercised the application's FFmpeg HTTPS download, pinned hash, extraction and ffmpeg/ffprobe execution checks: exit 0.
- Exercised LibreOffice MSI administrative extraction, private VC runtime placement and execution check: exit 0. No system-wide LibreOffice registration or default file association changes.
- Initial test runs found mixed slash normalization and MSI long-path failures. Fixed by normalizing the data path and extracting into a short unique temp directory. The corrected installation passed. An early test-fixture filename collision was also fixed before the passing 76-check run.
- UI inspection found overlapping tour chrome and an unterminated Simplified Chinese mapping buffer. Corrected the practice layout and explicit native string termination; added a regression check.

## Computer Use

Observed first-run optional install prompt, selected checkboxes, English/Traditional/Simplified UI, skip leading to tour, Esc skipping tour, persisted first-run flags, subsequent launch going to home, home right-click opening Conversion tools, and the one-click installer downloading sequentially with progress and disabled controls.

- **Actual one-click UI installation passed:** selected both tools once; FFmpeg and LibreOffice downloaded sequentially, were checksum-verified, extracted, and passed executable checks. The dialog reached 100% and displayed success. MSI log exit code 0. All tool files remained in isolated test data, with no manual terminal steps or UAC prompts during this user flow.
- Inspected all eight tutorial steps on the desktop. Spotlights followed the sample file, wheel, JPG choice, tools page, center and tray illustration. Left/right keys, Back/Next, disabled Back on the first step, final Done and Esc/skip worked. Clicking the practice JPG did not start a conversion. The practice page switched to tools for its corresponding step.
- GitHub Actions build and standalone-EXE verification passed for implementation commit `e76c7b8c3a6861d92d54164e3a1dc5411accb0a7`: https://github.com/rickuuyu-create/FormatWheel/actions/runs/39830380389 . Optional tool conversions were verified locally; CI checks depend on engines available on the runner.

## NOT VERIFIED

- Full physical Explorer drag + Shift + drop sequence: the available Computer Use API exposes complete drag and key-press operations separately, with no supported held-button/modifier combination. State-machine tests are not an end-to-end substitute.
- Physical multi-monitor / mixed-DPI behavior: only one active display was detected. Simulated placement checks do not certify a second display.
- Elevated drag sources, remote desktop and virtual-file providers; ARM64/x86; screen readers; prolonged/malformed-file stress; all codecs and all document layouts.
- No Authenticode signing. Windows may identify the app as an unknown publisher.

This is a preview, not a 100% macOS parity certification. PDF-to-Word extracts text rather than reconstructing layout. See README-Windows.md for conversion limitations.
