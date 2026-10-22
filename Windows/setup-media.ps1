# Optional user-invoked download. No registry, elevation or system PATH changes.
$ErrorActionPreference = 'Stop'
$mediaRoot = Join-Path $PSScriptRoot 'tools/ffmpeg'
New-Item -ItemType Directory -Force $mediaRoot | Out-Null
$archive = Join-Path $mediaRoot 'download.zip'
$url = 'https://www.gyan.dev/ffmpeg/builds/packages/ffmpeg-9.0.2-essentials_build.zip'
Write-Host 'Downloading FFmpeg 9.0.2 essentials from gyan.dev (linked by ffmpeg.org)...'
Invoke-WebRequest $url -OutFile $archive
$expected = '60f467265b1e312373dbcd92200c2618a74850f98d3d078e94296bb3fa2047ba'
if ((Get-FileHash $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected) { throw 'Checksum mismatch. Download not installed.' }
Expand-Archive $archive (Join-Path $mediaRoot 'package') -Force
$package = Get-ChildItem (Join-Path $mediaRoot 'package') -Directory | Select-Object -First 1
Copy-Item (Join-Path $package.FullName '*') $mediaRoot -Recurse -Force
Write-Host 'Done. Restart FormatWheel. FFmpeg retains its own GPL license; see tools/ffmpeg/LICENSE.'
