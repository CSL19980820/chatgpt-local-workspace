param([switch]$CheckOnly, [switch]$StageWhileRunning, [ValidateRange(1, 50)][int]$KeepRollbacks = 3)
$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'dist-next'
$target = Join-Path $PSScriptRoot 'dist'
$names = @('LocalWorkspace.exe','tunnel-client.exe','dashboard.html','runtime-components.json','LICENSE.TunnelClient.txt','THIRD-PARTY.TunnelClient.txt','tunnel-client.spdx.json','LICENSE.WebView2.txt')
foreach ($name in $names) { if (-not (Test-Path -LiteralPath (Join-Path $source $name) -PathType Leaf)) { throw "Missing staged file: $name. Run Get-RuntimeComponents.ps1 and build.ps1 first." } }
$active = @(Get-CimInstance Win32_Process -Filter "Name='LocalWorkspace.exe' OR Name='tunnel-client.exe'" |
    Where-Object { $_.ExecutablePath -eq (Join-Path $target 'LocalWorkspace.exe') -or $_.ExecutablePath -eq (Join-Path $target 'tunnel-client.exe') })
if ($active.Count -gt 0 -and -not $StageWhileRunning) { throw 'Update deferred: the workspace is running. Finish tasks and close it, or explicitly use -StageWhileRunning. No process was stopped.' }
if ($CheckOnly) { Write-Output 'Ready to update the app, Tunnel, dashboard and component notices. No process will be restarted.'; return }
New-Item -ItemType Directory -Force -Path $target | Out-Null
$backup = Join-Path $target ('rollback-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $backup | Out-Null
$moved = @(); $written = @()
try {
    foreach ($name in $names) {
        $destination = Join-Path $target $name
        if (Test-Path -LiteralPath $destination) {
            if ($active.ExecutablePath -contains $destination) { Move-Item -LiteralPath $destination -Destination (Join-Path $backup $name); $moved += $name }
            else { Copy-Item -LiteralPath $destination -Destination (Join-Path $backup $name) }
        }
        $written += $name
        Copy-Item -LiteralPath (Join-Path $source $name) -Destination $destination -Force
        if ((Get-FileHash -LiteralPath (Join-Path $source $name)).Hash -ne (Get-FileHash -LiteralPath $destination).Hash) { throw "Update verification failed: $name" }
    }
} catch {
    foreach ($name in $written) {
        $destination = Join-Path $target $name; $original = Join-Path $backup $name
        if ($moved -contains $name) { if (Test-Path -LiteralPath $destination) { Remove-Item -LiteralPath $destination -Force }; Move-Item -LiteralPath $original -Destination $destination }
        elseif (Test-Path -LiteralPath $original) { Copy-Item -LiteralPath $original -Destination $destination -Force }
        elseif (Test-Path -LiteralPath $destination) { Remove-Item -LiteralPath $destination -Force }
    }
    throw
}
Write-Output ('Update applied; rollback files: ' + $backup)
# Keep only the newest rollback folders. A folder still holding a running program's moved files is skipped.
$old = @(Get-ChildItem -LiteralPath $target -Directory -Filter 'rollback-*' | Sort-Object Name -Descending | Select-Object -Skip $KeepRollbacks)
foreach ($folder in $old) {
    try { Remove-Item -LiteralPath $folder.FullName -Recurse -Force -ErrorAction Stop; Write-Output ('Removed old rollback: ' + $folder.Name) }
    catch { Write-Output ('Kept rollback in use: ' + $folder.Name) }
}
Write-Output 'Start dist/LocalWorkspace.exe manually and refresh the host tool metadata. Existing processes were not restarted.'
