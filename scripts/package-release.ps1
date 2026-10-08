param([string]$Directory = "$PSScriptRoot\..\dist-next", [string]$OutputDirectory = "$PSScriptRoot\..\work\release")
$ErrorActionPreference = 'Stop'
$Directory = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Directory)
$OutputDirectory = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$version = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $Directory 'LocalWorkspace.exe')).FileVersion -replace '\.0$', ''
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Executable is missing a valid release version.' }
$names = @('LocalWorkspace.exe','tunnel-client.exe','dashboard.html','runtime-components.json','LICENSE.TunnelClient.txt','THIRD-PARTY.TunnelClient.txt','tunnel-client.spdx.json','LICENSE.WebView2.txt')
foreach ($name in $names) { if (-not (Test-Path -LiteralPath (Join-Path $Directory $name) -PathType Leaf)) { throw "Missing release file: $name" } }
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$staging = Join-Path $OutputDirectory ('package-v' + $version)
if (Test-Path -LiteralPath $staging) { throw 'Release staging already exists; select a fresh output directory.' }
New-Item -ItemType Directory -Path $staging | Out-Null
foreach ($name in $names) { Copy-Item -LiteralPath (Join-Path $Directory $name) -Destination $staging }
foreach ($name in @('README.md','README.en.md','LICENSE','NOTICE','UPGRADE-NOTES.md','VERIFICATION.md')) { Copy-Item -LiteralPath (Join-Path $repoRoot $name) -Destination $staging }
Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination (Join-Path $staging 'LICENSE.LocalWorkspace.txt')
# Include only documentation/images referenced by the two shipped READMEs.
$documentation = [regex]::Matches(([IO.File]::ReadAllText((Join-Path $repoRoot 'README.md')) + [IO.File]::ReadAllText((Join-Path $repoRoot 'README.en.md'))), '\]\((docs/[^)#\s]+)') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
foreach ($relative in $documentation) {
    $original = [IO.Path]::GetFullPath((Join-Path $repoRoot $relative))
    if (-not $original.StartsWith($repoRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $original -PathType Leaf)) { throw "Invalid documentation reference: $relative" }
    $destination = Join-Path $staging $relative
    New-Item -ItemType Directory -Force -Path ([IO.Path]::GetDirectoryName($destination)) | Out-Null
    Copy-Item -LiteralPath $original -Destination $destination
}
$hashes = Get-ChildItem -LiteralPath $staging -File -Recurse | Sort-Object FullName | ForEach-Object { ((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $_.FullName.Substring($staging.Length).TrimStart('\').Replace('\','/')) }
[IO.File]::WriteAllLines((Join-Path $staging 'SHA256SUMS.txt'), $hashes, (New-Object Text.UTF8Encoding($false)))
$zip = Join-Path $OutputDirectory ('local-workspace-' + $version + '-windows-x64.zip')
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($staging,$zip)
$zipHash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'SHA256SUMS.txt'), $zipHash + '  ' + [IO.Path]::GetFileName($zip) + "`n", (New-Object Text.UTF8Encoding($false)))
Get-Item -LiteralPath $zip | Select-Object FullName,Length
Write-Output ('SHA256 ' + $zipHash)
