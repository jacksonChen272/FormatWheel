# Validation — 2026-10-23

Windows x64 preview, upstream baseline `1eb19a1903ffd26ef31a183b7a8f5ea14ae7e4cc`.

- Release build: 0 warnings, 0 errors with .NET SDK 10.0.402.
- 57 integration checks passed, 0 failed. Generated fixtures only; no private documents used.
- Formats decoded after conversion: PNG, JPG, WebP, TIFF, GIF, HEIC, AVIF.
- Verified resize/rotation dimensions, source SHA-256 preservation, unique output names, valid ZIP and SHA-256 output, DOCX Unicode text round trip, rejection of mixed file kinds, PDF page count/merge and output creation, eight wheel directions and center cancellation.
- Windows OCR recognized generated `HELLO 123`; U²-Net inference generated a full-size PNG with alpha. This does not establish accuracy on real photographs or all OCR languages.
- All 27 video/audio action paths exercised with FFmpeg 9.0.2 using a generated test pattern and sine wave; FFprobe could read every resulting output. Does not establish quality or compatibility for every codec/input container.
- UI inspected with the computer-use skill: welcome screen, native file picker, actual full-screen wheel with blurred desktop. A JPG was created by clicking the wheel. Preview PNGs additionally generated directly from WPF rendering.
- Native ONNX inference tests were run outside the restricted automation process sandbox after sandbox execution terminated. Packaged app runs as an ordinary user application.

Not yet verified: full physical drag + Shift end-to-end interaction, mixed-DPI multiple monitors, elevated drag sources, LibreOffice conversions, screen readers, prolonged/malformed-file stress, ARM64/x86. See README-Windows.md for known differences, especially PDF text/layout, image metadata re-encoding, audio/graphics fidelity and missing original visual effects.

This is a functional preview, not a completed 100% parity certification.
