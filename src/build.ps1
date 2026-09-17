$ErrorActionPreference = "Stop"
$src = $PSScriptRoot
$assets = Join-Path $PSScriptRoot "build"
$out = Join-Path $PSScriptRoot "dist"
New-Item -ItemType Directory -Force -Path $out | Out-Null

$fw = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
$csc = Join-Path $fw "csc.exe"
$icon = Join-Path $assets "app.ico"

$refs = @(
    (Join-Path $fw "WPF\PresentationFramework.dll"),
    (Join-Path $fw "WPF\PresentationCore.dll"),
    (Join-Path $fw "WPF\WindowsBase.dll"),
    (Join-Path $fw "System.Xaml.dll"),
    (Join-Path $fw "System.dll"),
    (Join-Path $fw "System.Core.dll"),
    (Join-Path $fw "System.Drawing.dll"),
    (Join-Path $fw "System.Web.dll"),
    (Join-Path $fw "System.Web.Extensions.dll"),
    (Join-Path $fw "System.Windows.Forms.dll")
)

$tmpExe = Join-Path $env:TEMP ("_lwfg_build_" + [Guid]::NewGuid().ToString("N") + ".exe")

$cscArgs = New-Object System.Collections.Generic.List[string]
$cscArgs.Add("/nologo")
$cscArgs.Add("/target:winexe")
$cscArgs.Add("/codepage:65001")
$cscArgs.Add("/platform:anycpu")
$cscArgs.Add("/optimize+")
$cscArgs.Add("/warn:4")
$cscArgs.Add(("/out:" + $tmpExe))
$cscArgs.Add(("/win32icon:" + $icon))
$cscArgs.Add(("/win32manifest:" + (Join-Path $src "app.manifest")))
$cscArgs.Add(("/resource:" + (Join-Path $src "Xaml\Widget.xaml") + ",Widget.xaml"))
$cscArgs.Add(("/resource:" + (Join-Path $src "Xaml\Settings.xaml") + ",Settings.xaml"))
$cscArgs.Add(("/resource:" + $icon + ",app.ico"))
foreach ($r in $refs) { $cscArgs.Add("/r:" + $r) }
Get-ChildItem (Join-Path $src "*.cs") | Sort-Object Name | ForEach-Object { $cscArgs.Add($_.FullName) }

& $csc $cscArgs.ToArray()
if ($LASTEXITCODE -ne 0) {
    Remove-Item $tmpExe -ErrorAction SilentlyContinue
    throw ("csc failed with exit code " + $LASTEXITCODE)
}

$finalExe = Join-Path $out "LiangWenFengGu.exe"
$copied = $false
for ($i = 0; $i -lt 20; $i++) {
    try {
        Copy-Item $tmpExe -Destination $finalExe -Force -ErrorAction Stop
        $copied = $true
        break
    } catch {
        Start-Sleep -Milliseconds 400
    }
}
Remove-Item $tmpExe -ErrorAction SilentlyContinue
if (-not $copied) { throw ("could not replace " + $finalExe + " (locked)") }
Write-Output ("built: " + $finalExe)
