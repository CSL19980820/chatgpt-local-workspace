# Run in PowerShell 7 after closing the old DevSpace launcher.
$ErrorActionPreference='Stop'
$targets=@(
 'E:\my_space\devspace-launcher',
 'C:\Users\86185\AppData\Local\DevSpaceLauncher',
 'C:\Users\86185\Documents\Codex\2026-09-15\devspace-502-9-01-x20\outputs\devspace-openai-tunnel'
)
foreach($p in $targets){
 if(Test-Path -LiteralPath $p){
  $item=Get-Item -LiteralPath $p -Force
  if($item.FullName -ne $p -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)){throw "Unexpected cleanup target: $p"}
  Remove-Item -LiteralPath $p -Recurse -Force
 }
 if(Test-Path -LiteralPath $p){throw "Still present: $p"}
 Write-Output "Removed: $p"
}
