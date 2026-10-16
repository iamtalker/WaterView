# Draws the WaterView icon (a cute water drop) and writes NeeView\Resources\App.ico.
#   powershell -ExecutionPolicy Bypass -File tools\make-icon.ps1 [-Preview <png path>]
param([string]$Preview = "")

Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root "NeeView\Resources\App.ico"

function New-DropBitmap([int]$size) {
    $big = 1024
    $bmp = New-Object System.Drawing.Bitmap $big, $big, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    # drop outline: pointed top, round bottom
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $tip = New-Object System.Drawing.PointF 512, 70
    $path.AddBezier($tip, (New-Object System.Drawing.PointF 440, 250), (New-Object System.Drawing.PointF 150, 430), (New-Object System.Drawing.PointF 150, 640))
    $path.AddArc(150, 280, 724, 724, 180, -180)
    $path.AddBezier((New-Object System.Drawing.PointF 874, 640), (New-Object System.Drawing.PointF 874, 430), (New-Object System.Drawing.PointF 584, 250), $tip)
    $path.CloseFigure()

    # body gradient (light top, deeper blue at the bottom)
    $rect = New-Object System.Drawing.RectangleF 150, 70, 724, 934
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, ([System.Drawing.Color]::FromArgb(255, 140, 224, 255)), ([System.Drawing.Color]::FromArgb(255, 38, 140, 245)), 90.0
    $g.FillPath($grad, $path)
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 22, 98, 200)), 28
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawPath($pen, $path)

    # shine
    $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(200, 255, 255, 255))
    $g.FillEllipse($white, 255, 470, 120, 210)
    $g.FillEllipse($white, 280, 400, 50, 50)

    if ($size -ge 32) {
        # face
        $dark = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 20, 50, 110))
        $g.FillEllipse($dark, 360, 590, 92, 120)
        $g.FillEllipse($dark, 572, 590, 92, 120)
        $eyeShine = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
        $g.FillEllipse($eyeShine, 385, 612, 36, 36)
        $g.FillEllipse($eyeShine, 597, 612, 36, 36)
        $smile = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 20, 50, 110)), 26
        $smile.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $smile.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $g.DrawArc($smile, 452, 690, 120, 80, 20, 140)
        $cheek = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(150, 255, 150, 190))
        $g.FillEllipse($cheek, 290, 700, 90, 56)
        $g.FillEllipse($cheek, 644, 700, 90, 56)
    }
    $g.Dispose()

    $small = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g2 = [System.Drawing.Graphics]::FromImage($small)
    $g2.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g2.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g2.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g2.DrawImage($bmp, 0, 0, $size, $size)
    $g2.Dispose(); $bmp.Dispose()
    return $small
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$pngs = @()
foreach ($s in $sizes) {
    $b = New-DropBitmap $s
    $ms = New-Object System.IO.MemoryStream
    $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += , $ms.ToArray()
    if ($s -eq 256 -and $Preview -ne "") { $b.Save($Preview, [System.Drawing.Imaging.ImageFormat]::Png) }
    $b.Dispose()
}

# ICO container with PNG frames
$fs = [System.IO.File]::Create($out)
$w = New-Object System.IO.BinaryWriter $fs
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $dim = if ($s -ge 256) { 0 } else { $s }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$pngs[$i].Length); $w.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $w.Write($p) }
$w.Close(); $fs.Close()
"wrote $out ($((Get-Item $out).Length) bytes)"
