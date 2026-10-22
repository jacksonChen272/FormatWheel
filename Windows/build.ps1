$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    dotnet publish FormatWheel.Windows.csproj -c Release -r win-x64 --self-contained true -o publish -p:DebugType=None -p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
    Copy-Item ../LICENSE publish/LICENSE
    Copy-Item README-Windows.md,THIRD-PARTY-NOTICES.md,VALIDATION.md,setup-media.ps1 publish/
    Copy-Item licenses publish/licenses -Recurse -Force
    dotnet publish FormatWheel.Windows.csproj -c Release -r win-x64 --self-contained true -o standalone -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) { throw 'Single EXE build failed' }
    New-Item -ItemType Directory -Force release | Out-Null
    Copy-Item standalone/FormatWheel.exe release/FormatWheel-Windows-x64.exe -Force
    Compress-Archive -Path publish/* -DestinationPath release/FormatWheel-Windows-x64.zip -Force
    Get-ChildItem release -File | Where-Object Extension -in '.exe','.zip' | Get-FileHash -Algorithm SHA256 | ForEach-Object { $_.Hash.ToLower()+'  '+(Split-Path $_.Path -Leaf) } | Set-Content release/SHA256SUMS.txt -Encoding ascii
} finally { Pop-Location }
