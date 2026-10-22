$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    dotnet publish FormatWheel.Windows.csproj -c Release -r win-x64 --self-contained true -o publish -p:DebugType=None -p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
    Copy-Item ../LICENSE publish/LICENSE
    Copy-Item README-Windows.md,THIRD-PARTY-NOTICES.md,setup-media.ps1 publish/
    Copy-Item licenses publish/licenses -Recurse -Force
} finally { Pop-Location }
