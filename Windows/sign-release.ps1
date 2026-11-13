# Authenticode-sign the Windows release only when a trusted PFX is configured.
# This script is intentionally not invoked for pull requests or ordinary unsigned CI.
param(
    [string]$TimestampUrl = 'http://timestamp.digicert.com'
)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

if ([string]::IsNullOrWhiteSpace($env:FORMATWHEEL_SIGNING_PFX_BASE64) -or
    [string]::IsNullOrWhiteSpace($env:FORMATWHEEL_SIGNING_PFX_PASSWORD)) {
    throw 'Signing is enabled, but the PFX or password GitHub secret is missing.'
}

$publish = Join-Path $PSScriptRoot 'publish'
$release = Join-Path $PSScriptRoot 'release'
$releaseExe = Join-Path $release 'FormatWheel-Windows-x64.exe'
if (-not (Test-Path -LiteralPath $releaseExe -PathType Leaf) -or
    -not (Test-Path -LiteralPath $publish -PathType Container)) {
    throw 'Build the Windows release with build.ps1 before signing.'
}

$kits = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
$signTool = Get-ChildItem -Path $kits -Filter signtool.exe -File -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '[\\/]x64[\\/]signtool\.exe$' } |
    Sort-Object FullName -Descending |
    Select-Object -First 1
if ($null -eq $signTool) { throw 'Windows SDK x64 signtool.exe is not available.' }

$tempDirectory = if ($env:RUNNER_TEMP) { $env:RUNNER_TEMP } else { [IO.Path]::GetTempPath() }
$pfx = Join-Path $tempDirectory ('formatwheel-signing-' + [guid]::NewGuid().ToString('N') + '.pfx')
try {
    [IO.File]::WriteAllBytes($pfx, [Convert]::FromBase64String($env:FORMATWHEEL_SIGNING_PFX_BASE64))
    $targets = @(
        Get-ChildItem -LiteralPath $publish -Recurse -File |
            Where-Object { $_.Extension -in @('.exe', '.dll') }
    ) + @(Get-Item -LiteralPath $releaseExe)

    foreach ($target in $targets) {
        $existing = Get-AuthenticodeSignature -LiteralPath $target.FullName
        if ($existing.Status -eq 'Valid') {
            Write-Host ('Keeping existing valid signature: ' + $target.Name)
            continue
        }
        Write-Host ('Signing: ' + $target.Name)
        & $signTool.FullName sign /fd SHA256 /tr $TimestampUrl /td SHA256 /f $pfx /p $env:FORMATWHEEL_SIGNING_PFX_PASSWORD $target.FullName
        if ($LASTEXITCODE -ne 0) { throw ('Signing failed: ' + $target.FullName) }
        & $signTool.FullName verify /pa /v $target.FullName
        if ($LASTEXITCODE -ne 0) { throw ('Authenticode verification failed: ' + $target.FullName) }
    }

    # The portable ZIP must contain the signed binaries, not the pre-signing copies.
    Compress-Archive -Path (Join-Path $publish '*') -DestinationPath (Join-Path $release 'FormatWheel-Windows-x64.zip') -Force
    Get-ChildItem -LiteralPath $release -File |
        Where-Object { $_.Extension -in @('.exe', '.zip') } |
        Get-FileHash -Algorithm SHA256 |
        ForEach-Object { $_.Hash.ToLowerInvariant() + '  ' + (Split-Path $_.Path -Leaf) } |
        Set-Content -LiteralPath (Join-Path $release 'SHA256SUMS.txt') -Encoding ascii
    Write-Host 'Release signed and checksums regenerated.'
}
finally {
    if (Test-Path -LiteralPath $pfx) { Remove-Item -LiteralPath $pfx -Force }
}
