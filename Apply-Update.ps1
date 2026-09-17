param([switch]$CheckOnly)
$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'dist-next\LocalWorkspace.exe'
$target = Join-Path $PSScriptRoot 'dist\LocalWorkspace.exe'
$tunnel = Join-Path $PSScriptRoot 'dist\tunnel-client.exe'
if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw 'Build dist-next first.' }
# The launcher owns the tunnel and command trees. Never stop it to deploy an update.
$active = @(Get-CimInstance Win32_Process -Filter "Name='LocalWorkspace.exe' OR Name='tunnel-client.exe'" |
    Where-Object { $_.ExecutablePath -eq $target -or $_.ExecutablePath -eq $tunnel })
if ($active.Count -gt 0) { throw 'Update deferred: the current workspace is still running. Finish all tasks and close the workspace app before applying this update. No process was stopped.' }
if ($CheckOnly) { Write-Output 'Ready to update; no files changed.'; return }
Copy-Item -LiteralPath $source -Destination $target -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'dist-next\dashboard.html') -Destination (Join-Path $PSScriptRoot 'dist\dashboard.html') -Force
if ((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $target).Hash) { throw 'Update verification failed.' }
Write-Output 'Update applied. Start dist/LocalWorkspace.exe manually, then refresh the ChatGPT tool metadata before issuing commands with the new default shell.'
