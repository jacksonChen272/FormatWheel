# Windows 0.2.1 validation — 2026-10-23 (Asia/Shanghai)

Independent Windows x64 preview. Upstream baseline: `1eb19a1903ffd26ef31a183b7a8f5ea14ae7e4cc`. Original MIT attribution and Swift source preserved.

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
