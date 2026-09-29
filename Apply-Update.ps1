param([switch]$CheckOnly, [switch]$StageWhileRunning)
$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'dist-next\LocalWorkspace.exe'
$target = Join-Path $PSScriptRoot 'dist\LocalWorkspace.exe'
$tunnel = Join-Path $PSScriptRoot 'dist\tunnel-client.exe'
if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw 'Build dist-next first.' }
# The launcher owns the tunnel and command trees. Never stop it to deploy an update.
$active = @(Get-CimInstance Win32_Process -Filter "Name='LocalWorkspace.exe' OR Name='tunnel-client.exe'" |
    Where-Object { $_.ExecutablePath -eq $target -or $_.ExecutablePath -eq $tunnel })
if ($active.Count -gt 0 -and -not $StageWhileRunning) { throw 'Update deferred: the current workspace is still running. Finish all tasks and close the workspace app, or explicitly stage the next launch with -StageWhileRunning. No process was stopped.' }
if ($CheckOnly) { Write-Output 'Ready to update files; running processes will not be restarted.'; return }
$runningBackup = $null
if ($active.Count -gt 0) {
    # Windows keeps the already mapped image alive after a rename. The next launch
    # uses the new filename contents; the old process and its commands keep running.
    $runningBackup = Join-Path $PSScriptRoot ('dist\LocalWorkspace.running-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.exe')
    if (Test-Path -LiteralPath $runningBackup) { throw 'Running-image backup already exists; no files changed.' }
    Move-Item -LiteralPath $target -Destination $runningBackup
}
try { Copy-Item -LiteralPath $source -Destination $target -Force }
catch {
    if ($runningBackup -and -not (Test-Path -LiteralPath $target)) { Move-Item -LiteralPath $runningBackup -Destination $target }
    throw
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'dist-next\dashboard.html') -Destination (Join-Path $PSScriptRoot 'dist\dashboard.html') -Force
if ((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $target).Hash) { throw 'Update verification failed.' }
Write-Output 'Update applied. Start dist/LocalWorkspace.exe manually, then refresh the ChatGPT tool metadata before issuing commands with the new default shell.'
if ($runningBackup) { Write-Output ('Running processes still use the old image. Keep this rollback backup until they exit: ' + $runningBackup) }
