param([string]$OutputDirectory = "$PSScriptRoot\dist-next")
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath "$PSScriptRoot\node_modules\@tailwindcss\cli\dist\index.mjs"){
    & node "$PSScriptRoot\scripts\build-dashboard.cjs"
    if($LASTEXITCODE -ne 0){throw 'Dashboard build failed'}
}
# The embedded WebView2 SDK must be exactly the version runtime-components.json and WebviewLoader declare.
$sdkVersion=(Get-Content -LiteralPath "$PSScriptRoot\runtime-components.json" -Raw | ConvertFrom-Json).webview2.version
foreach($name in 'Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll','WebView2Loader.dll'){
    $vendored=[Diagnostics.FileVersionInfo]::GetVersionInfo("$PSScriptRoot\vendor\webview2\$name").FileVersion
    if($vendored -ne $sdkVersion){throw "vendor\webview2\$name is $vendored but runtime-components.json declares $sdkVersion. Run scripts\Get-RuntimeComponents.ps1 -UpdateSdk."}
}
if((Get-Content -LiteralPath "$PSScriptRoot\src\WorkbenchHost.cs" -Raw) -notmatch ('SdkVersion="'+[regex]::Escape($sdkVersion)+'"')){throw "WebviewLoader.SdkVersion in src\WorkbenchHost.cs does not match $sdkVersion"}
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$compiler=Join-Path ([Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()) 'csc.exe'
if(-not(Test-Path -LiteralPath $compiler)){$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'}
if(-not(Test-Path -LiteralPath $compiler)){throw 'C# compiler not found in the system .NET Framework runtime.'}
& $compiler /nologo /target:winexe /platform:x64 /optimize+ "/win32icon:$PSScriptRoot\assets\local-workspace.ico" "/out:$OutputDirectory\LocalWorkspace.exe" "/resource:$PSScriptRoot\src\dashboard.html,dashboard.html" "/resource:$PSScriptRoot\vendor\webview2\Microsoft.Web.WebView2.Core.dll,wv2.core.dll" "/resource:$PSScriptRoot\vendor\webview2\Microsoft.Web.WebView2.WinForms.dll,wv2.winforms.dll" "/resource:$PSScriptRoot\vendor\webview2\WebView2Loader.dll,wv2.loader.dll" /r:Microsoft.CSharp.dll /r:System.Security.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll "/r:$PSScriptRoot\vendor\webview2\Microsoft.Web.WebView2.Core.dll" "/r:$PSScriptRoot\vendor\webview2\Microsoft.Web.WebView2.WinForms.dll" "$PSScriptRoot\src\Program.cs" "$PSScriptRoot\src\UiKit.cs" "$PSScriptRoot\src\WorkbenchHost.cs" "$PSScriptRoot\src\WorkspaceServer.cs" "$PSScriptRoot\src\Presentation.cs" "$PSScriptRoot\src\FileSearch.cs" "$PSScriptRoot\src\WorkspaceContext.cs" "$PSScriptRoot\src\PatchEditor.cs" "$PSScriptRoot\src\WorkspaceActivity.cs" "$PSScriptRoot\src\WorkspaceDetail.cs" "$PSScriptRoot\src\WorkspaceThreads.cs" "$PSScriptRoot\src\LocalDashboard.cs" "$PSScriptRoot\src\DashboardImages.cs" "$PSScriptRoot\src\WorkspaceContracts.cs" "$PSScriptRoot\src\WorkspaceTasks.cs" "$PSScriptRoot\src\AttachmentImport.cs" "$PSScriptRoot\src\WorkspaceDiagnostics.cs" "$PSScriptRoot\src\WorkspaceStore.cs" "$PSScriptRoot\src\WorkspaceCredentials.cs" "$PSScriptRoot\src\WorkspaceFiles.cs" "$PSScriptRoot\src\WorkspaceJournal.cs" "$PSScriptRoot\src\WorkspaceRequests.cs" "$PSScriptRoot\src\WorkspaceReview.cs"
if($LASTEXITCODE -ne 0){throw 'Build failed'}
Copy-Item -LiteralPath "$PSScriptRoot\src\dashboard.html" -Destination "$OutputDirectory\dashboard.html" -Force
Copy-Item -LiteralPath "$PSScriptRoot\vendor\webview2\LICENSE.WebView2.txt","$PSScriptRoot\runtime-components.json" -Destination $OutputDirectory -Force
Get-Item "$OutputDirectory\LocalWorkspace.exe" | Select-Object FullName,Length
