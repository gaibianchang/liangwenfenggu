Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = "Stop"
$out = Join-Path $PSScriptRoot "build"
New-Item -ItemType Directory -Force -Path $out | Out-Null

function New-RoundedPath([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc(($x + $w - $d), $y, $d, $d, 270, 90)
    $p.AddArc(($x + $w - $d), ($y + $h - $d), $d, $d, 0, 90)
    $p.AddArc($x, ($y + $h - $d), $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function New-IconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $s = $size / 256.0
    $pad = 10.0 * $s
    $w = $size - 2.0 * $pad
    $radius = 58.0 * $s
    $rect = New-Object System.Drawing.RectangleF([single]$pad, [single]$pad, [single]$w, [single]$w)
    $path = New-RoundedPath ([single]$pad) ([single]$pad) ([single]$w) ([single]$w) ([single]$radius)

    $top = [System.Drawing.Color]::FromArgb(255, 32, 38, 48)
    $bottom = [System.Drawing.Color]::FromArgb(255, 9, 12, 16)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, $top, $bottom, [single]90.0)
    $g.FillPath($brush, $path)

    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(46, 255, 255, 255), [single](2.0 * $s))
    $g.DrawPath($pen, $path)

    $cx = $size / 2.0
    $cy = $size / 2.0
    $r = 66.0 * $s
    $orb = New-Object System.Drawing.Drawing2D.GraphicsPath
    $orb.AddEllipse([single]($cx - $r), [single]($cy - $r), [single](2.0 * $r), [single](2.0 * $r))
    $state = $g.Save()
    $g.SetClip($orb)
    $gap = 3.0 * $s
    $red = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 255, 69, 58))
    $green = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 50, 215, 75))
    $redW = $r - $gap
    $greenW = $r - $gap + 1.0
    $g.FillRectangle($red, [single]($cx - $r), [single]($cy - $r), [single]$redW, [single](2.0 * $r))
    $g.FillRectangle($green, [single]($cx + $gap), [single]($cy - $r), [single]$greenW, [single](2.0 * $r))
    $g.Restore($state)

    $ring = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(38, 255, 255, 255), [single](1.5 * $s))
    $g.DrawEllipse($ring, [single]($cx - $r), [single]($cy - $r), [single](2.0 * $r), [single](2.0 * $r))

    $g.Dispose()
    return $bmp
}

$sizes = @(256, 64, 48, 32, 16)
$pngs = @()
foreach ($s in $sizes) {
    $bmp = New-IconBitmap $s
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += , @($s, $ms.ToArray())
    $bmp.Dispose()
    $ms.Dispose()
}

$icoPath = Join-Path $out "app.ico"
$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([uint16]0)
$bw.Write([uint16]1)
$bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
foreach ($item in $pngs) {
    $s = $item[0]
    $data = $item[1]
    if ($s -ge 256) { $dim = 0 } else { $dim = $s }
    $bw.Write([byte]$dim)
    $bw.Write([byte]$dim)
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]32)
    $bw.Write([uint32]$data.Length)
    $bw.Write([uint32]$offset)
    $offset += $data.Length
}
foreach ($item in $pngs) { $bw.Write($item[1]) }
$bw.Flush()
$bw.Close()
$fs.Close()

$preview = New-IconBitmap 256
$preview.Save((Join-Path $out "icon-preview.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$preview.Dispose()
Write-Output ("icon written: " + $icoPath)
