# Windows 0.2.0 validation — 2026-10-23 (Asia/Shanghai)

Independent Windows x64 preview. Upstream baseline: `1eb19a1903ffd26ef31a183b7a8f5ea14ae7e4cc`. Original MIT attribution and Swift source preserved.

## Automated checks

- .NET SDK 10.0.402, self-contained .NET 10.0.13, Windows x64. Builds succeeded without compiler warnings/errors.
- A standalone EXE was copied into a new folder without DLLs, model or JSON sidecars. It passed **76 checks, 0 failures**, exit code 0. Its embedded assets were extracted into fresh isolated test data. FFmpeg and LibreOffice were separately installed optional engines.
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

Full two-tool UI installation completion and the complete tour sequence are being checked before publication; final results will be recorded here.

## NOT VERIFIED

- Full physical Explorer drag + Shift + drop sequence: the available Computer Use API exposes complete drag and key-press operations separately, with no supported held-button/modifier combination. State-machine tests are not an end-to-end substitute.
- Physical multi-monitor / mixed-DPI behavior: only one active display was detected. Simulated placement checks do not certify a second display.
- Elevated drag sources, remote desktop and virtual-file providers; ARM64/x86; screen readers; prolonged/malformed-file stress; all codecs and all document layouts.
- No Authenticode signing. Windows may identify the app as an unknown publisher.

This is a preview, not a 100% macOS parity certification. PDF-to-Word extracts text rather than reconstructing layout. See README-Windows.md for conversion limitations.
