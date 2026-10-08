param([string]$OutputDirectory = "$PSScriptRoot\..\dist-next", [switch]$UpdateSdk)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$components = Get-Content -LiteralPath (Join-Path $repoRoot 'runtime-components.json') -Raw | ConvertFrom-Json
$cache = Join-Path $repoRoot 'work\runtime-downloads'
New-Item -ItemType Directory -Force -Path $cache,$OutputDirectory | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
function Get-VerifiedArchive($component, $name) {
    $file = Join-Path $cache $name
    if (-not (Test-Path -LiteralPath $file) -or (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $component.sha256) {
        Invoke-WebRequest -Uri $component.url -OutFile $file
    }
    if ((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $component.sha256) { throw "Component checksum mismatch: $name" }
    return [IO.Compression.ZipFile]::OpenRead($file)
}
function Extract-Entry($archive, $entryName, $target) {
    $entry = $archive.GetEntry($entryName)
    if (-not $entry) { throw "Missing archive entry: $entryName" }
    [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $true)
}
$tunnel = Get-VerifiedArchive $components.tunnel ('tunnel-' + $components.tunnel.version + '.zip')
try {
    Extract-Entry $tunnel 'tunnel-client.exe' (Join-Path $OutputDirectory 'tunnel-client.exe')
    Extract-Entry $tunnel 'LICENSE' (Join-Path $OutputDirectory 'LICENSE.TunnelClient.txt')
    Extract-Entry $tunnel ('tunnel-client-v' + $components.tunnel.version + '-windows-amd64-licenses.txt') (Join-Path $OutputDirectory 'THIRD-PARTY.TunnelClient.txt')
    Extract-Entry $tunnel ('tunnel-client-v' + $components.tunnel.version + '-windows-amd64.spdx.json') (Join-Path $OutputDirectory 'tunnel-client.spdx.json')
} finally { $tunnel.Dispose() }
if ($UpdateSdk) {
    $sdk = Get-VerifiedArchive $components.webview2 ('webview2-' + $components.webview2.version + '.zip')
    $sdkDirectory = Join-Path $repoRoot 'vendor\webview2'
    # Stage outside vendor: nothing in vendor changes until every file is verified.
    $staging = Join-Path ([IO.Path]::GetTempPath()) ('webview2-sdk-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force -Path $staging | Out-Null
    try {
        $entries = [ordered]@{
            'Microsoft.Web.WebView2.Core.dll' = 'lib/net462/Microsoft.Web.WebView2.Core.dll'
            'Microsoft.Web.WebView2.WinForms.dll' = 'lib/net462/Microsoft.Web.WebView2.WinForms.dll'
            'WebView2Loader.dll' = 'runtimes/win-x64/native/WebView2Loader.dll'
            'LICENSE.WebView2.txt' = 'LICENSE.txt'
        }
        try { foreach ($name in $entries.Keys) { Extract-Entry $sdk $entries[$name] (Join-Path $staging $name) } } finally { $sdk.Dispose() }
        foreach ($name in @('Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll','WebView2Loader.dll')) {
            $staged = Join-Path $staging $name
            $signature = Get-AuthenticodeSignature -LiteralPath $staged
            if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Microsoft Corporation') { throw "SDK signature invalid: $name" }
            $fileVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($staged).FileVersion
            if ($fileVersion -ne $components.webview2.version) { throw "SDK version mismatch: $name is $fileVersion, expected $($components.webview2.version)" }
        }
        New-Item -ItemType Directory -Force -Path $sdkDirectory | Out-Null
        foreach ($name in $entries.Keys) { Copy-Item -LiteralPath (Join-Path $staging $name) -Destination (Join-Path $sdkDirectory $name) -Force }
    } finally { Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue }
}
Copy-Item -LiteralPath (Join-Path $repoRoot 'vendor\webview2\LICENSE.WebView2.txt') -Destination $OutputDirectory -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'runtime-components.json') -Destination $OutputDirectory -Force
Write-Output ('Runtime components ready: Tunnel ' + $components.tunnel.version + ', WebView2 SDK ' + $components.webview2.version)
