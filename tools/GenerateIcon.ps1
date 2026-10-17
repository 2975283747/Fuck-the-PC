[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$resourceDirectory = Join-Path $root "src\TrayTemps\Resources"
$outputDirectory = Join-Path $root "outputs"
$iconPath = Join-Path $resourceDirectory "App.ico"
$previewPath = Join-Path $outputDirectory "icon-preview.png"

New-Item -ItemType Directory -Force -Path $resourceDirectory | Out-Null
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null

function New-RoundedRectanglePath {
    param(
        [float]$X,
        [float]$Y,
        [float]$Width,
        [float]$Height,
        [float]$Radius
    )

    $diameter = $Radius * 2
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($X, $Y, $diameter, $diameter, 180, 90)
    $path.AddArc($X + $Width - $diameter, $Y, $diameter, $diameter, 270, 90)
    $path.AddArc($X + $Width - $diameter, $Y + $Height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($X, $Y + $Height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-ThermometerPath {
    param([float]$Scale)

    $path = New-RoundedRectanglePath `
        -X (103 * $Scale) `
        -Y (42 * $Scale) `
        -Width (50 * $Scale) `
        -Height (126 * $Scale) `
        -Radius (25 * $Scale)

    $path.AddEllipse(
        (94 * $Scale),
        (151 * $Scale),
        (68 * $Scale),
        (68 * $Scale))

    $path.FillMode = [System.Drawing.Drawing2D.FillMode]::Winding
    return $path
}

function Render-IconBitmap {
    param(
        [int]$Size,
        [bool]$ShowTicks
    )

    $superSample = 4
    $canvas = $Size * $superSample
    $scale = $canvas / 256.0

    $bitmap = New-Object System.Drawing.Bitmap(
        $canvas,
        $canvas,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

    $outerPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $outerPath.AddEllipse(
        (10 * $scale),
        (10 * $scale),
        (236 * $scale),
        (236 * $scale))

    $glowBrush = New-Object System.Drawing.Drawing2D.PathGradientBrush($outerPath)
    $glowBrush.CenterColor = [System.Drawing.Color]::FromArgb(255, 30, 41, 59)
    $glowBrush.SurroundColors = @(
        [System.Drawing.Color]::FromArgb(255, 8, 15, 28)
    )
    $graphics.FillPath($glowBrush, $outerPath)

    $rimPen = New-Object System.Drawing.Pen(
        [System.Drawing.Color]::FromArgb(210, 56, 189, 248),
        (5 * $scale))
    $rimPen.Alignment = [System.Drawing.Drawing2D.PenAlignment]::Inset
    $graphics.DrawEllipse(
        $rimPen,
        (13 * $scale),
        (13 * $scale),
        (230 * $scale),
        (230 * $scale))

    $innerGlowPen = New-Object System.Drawing.Pen(
        [System.Drawing.Color]::FromArgb(70, 255, 255, 255),
        (2 * $scale))
    $innerGlowPen.Alignment = [System.Drawing.Drawing2D.PenAlignment]::Inset
    $graphics.DrawEllipse(
        $innerGlowPen,
        (24 * $scale),
        (24 * $scale),
        (208 * $scale),
        (208 * $scale))

    $tubePath = New-ThermometerPath -Scale $scale

    $tubeBounds = New-Object System.Drawing.RectangleF(
        (94 * $scale),
        (42 * $scale),
        (68 * $scale),
        (177 * $scale))
    $tubeBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $tubeBounds,
        [System.Drawing.Color]::FromArgb(255, 255, 255, 255),
        [System.Drawing.Color]::FromArgb(255, 203, 213, 225),
        [System.Drawing.Drawing2D.LinearGradientMode]::Horizontal)
    $graphics.FillPath($tubeBrush, $tubePath)

    $mercuryPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $mercuryStem = New-Object System.Drawing.RectangleF(
        (116 * $scale),
        (102 * $scale),
        (24 * $scale),
        (75 * $scale))
    $mercuryPath.AddRectangle($mercuryStem)
    $mercuryPath.AddEllipse(
        (100 * $scale),
        (157 * $scale),
        (56 * $scale),
        (56 * $scale))
    $mercuryPath.FillMode = [System.Drawing.Drawing2D.FillMode]::Winding

    $originalClip = $graphics.Clip
    $graphics.SetClip($tubePath)

    $mercuryBounds = New-Object System.Drawing.RectangleF(
        (100 * $scale),
        (94 * $scale),
        (56 * $scale),
        (125 * $scale))
    $mercuryBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $mercuryBounds,
        [System.Drawing.Color]::FromArgb(255, 251, 191, 36),
        [System.Drawing.Color]::FromArgb(255, 239, 68, 68),
        [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
    $graphics.FillPath($mercuryBrush, $mercuryPath)
    $graphics.Clip = $originalClip

    $outlinePen = New-Object System.Drawing.Pen(
        [System.Drawing.Color]::FromArgb(255, 248, 250, 252),
        (6 * $scale))
    $outlinePen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $graphics.DrawPath($outlinePen, $tubePath)

    if ($ShowTicks) {
        $tickPen = New-Object System.Drawing.Pen(
            [System.Drawing.Color]::FromArgb(220, 125, 211, 252),
            (6 * $scale))
        $tickPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $tickPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

        foreach ($tick in @(
            @(160, 78, 181),
            @(160, 105, 174),
            @(160, 132, 181)
        )) {
            $graphics.DrawLine(
                $tickPen,
                ($tick[0] * $scale),
                ($tick[1] * $scale),
                ($tick[2] * $scale),
                ($tick[1] * $scale))
        }
    }

    $highlightBrush = New-Object System.Drawing.SolidBrush(
        [System.Drawing.Color]::FromArgb(135, 255, 255, 255))
    $graphics.FillEllipse(
        $highlightBrush,
        (112 * $scale),
        (55 * $scale),
        (9 * $scale),
        (58 * $scale))
    $graphics.FillEllipse(
        $highlightBrush,
        (111 * $scale),
        (168 * $scale),
        (15 * $scale),
        (12 * $scale))

    $graphics.Dispose()
    $outerPath.Dispose()
    $tubePath.Dispose()
    $mercuryPath.Dispose()
    $glowBrush.Dispose()
    $rimPen.Dispose()
    $innerGlowPen.Dispose()
    $tubeBrush.Dispose()
    $mercuryBrush.Dispose()
    $outlinePen.Dispose()
    $highlightBrush.Dispose()
    if ($null -ne $tickPen) {
        $tickPen.Dispose()
    }

    if ($superSample -eq 1) {
        return $bitmap
    }

    $finalBitmap = New-Object System.Drawing.Bitmap(
        $Size,
        $Size,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $finalGraphics = [System.Drawing.Graphics]::FromImage($finalBitmap)
    $finalGraphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $finalGraphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $finalGraphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $finalGraphics.DrawImage(
        $bitmap,
        (New-Object System.Drawing.Rectangle(0, 0, $Size, $Size)),
        0,
        0,
        $canvas,
        $canvas,
        [System.Drawing.GraphicsUnit]::Pixel)
    $finalGraphics.Dispose()
    $bitmap.Dispose()

    return $finalBitmap
}

function Save-Png {
    param(
        [System.Drawing.Bitmap]$Bitmap,
        [string]$Path
    )

    $Bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
}

function Write-IcoFile {
    param(
        [string]$Path,
        [int[]]$Sizes
    )

    $images = New-Object System.Collections.Generic.List[byte[]]
    $widths = New-Object System.Collections.Generic.List[int]

    foreach ($size in $Sizes) {
        $bitmap = Render-IconBitmap -Size $size -ShowTicks ($size -ge 24)
        $stream = New-Object System.IO.MemoryStream
        $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        $images.Add($stream.ToArray())
        $widths.Add($size)
        $stream.Dispose()
        $bitmap.Dispose()
    }

    $stream = [System.IO.File]::Open(
        $Path,
        [System.IO.FileMode]::Create,
        [System.IO.FileAccess]::Write,
        [System.IO.FileShare]::None)
    $writer = New-Object System.IO.BinaryWriter($stream)

    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$images.Count)

    $offset = 6 + (16 * $images.Count)

    for ($index = 0; $index -lt $images.Count; $index++) {
        $size = $widths[$index]
        $dimension = if ($size -eq 256) { 0 } else { $size }

        $writer.Write([byte]$dimension)
        $writer.Write([byte]$dimension)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$images[$index].Length)
        $writer.Write([uint32]$offset)

        $offset += $images[$index].Length
    }

    foreach ($image in $images) {
        $writer.Write($image)
    }

    $writer.Dispose()
    $stream.Dispose()
}

$sizes = @(16, 20, 24, 32, 48, 64, 128, 256)
Write-IcoFile -Path $iconPath -Sizes $sizes

$preview = Render-IconBitmap -Size 512 -ShowTicks $true
Save-Png -Bitmap $preview -Path $previewPath
$preview.Dispose()

Get-Item $iconPath, $previewPath | Select-Object FullName, Length, LastWriteTime
