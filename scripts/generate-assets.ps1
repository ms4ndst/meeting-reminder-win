<#
.SYNOPSIS
    Generates the PNG asset files that an MSIX manifest requires.

.DESCRIPTION
    Renders Catppuccin-themed PNGs procedurally with System.Drawing.
    Also produces a .ico for the WPF window icon.

.PARAMETER OutputDir
    Where to write the PNGs. Default: MeetingReminder.App\Assets
#>

[CmdletBinding()]
param(
    [string]$OutputDir = (Join-Path $PSScriptRoot '..\MeetingReminder.App\Assets')
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

# Catppuccin Mocha
$base   = [System.Drawing.Color]::FromArgb(0x1e, 0x1e, 0x2e)
$accent = [System.Drawing.Color]::FromArgb(0xcb, 0xa6, 0xf7)   # Mauve
$text   = [System.Drawing.Color]::FromArgb(0xcd, 0xd6, 0xf4)
$mantle = [System.Drawing.Color]::FromArgb(0x18, 0x18, 0x25)

function New-RoundedPath {
    param([System.Drawing.Rectangle]$rect, [int]$radius)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $r = [int]$radius
    $path.AddArc($rect.X,             $rect.Y,              $r, $r, 180, 90)
    $path.AddArc($rect.Right - $r,    $rect.Y,              $r, $r, 270, 90)
    $path.AddArc($rect.Right - $r,    $rect.Bottom - $r,    $r, $r,   0, 90)
    $path.AddArc($rect.X,             $rect.Bottom - $r,    $r, $r,  90, 90)
    $path.CloseFigure()
    return $path
}

function New-Logo {
    param([int]$w, [int]$h, [string]$path)

    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

    # Backdrop
    $bg = New-Object System.Drawing.SolidBrush $base
    $g.FillRectangle($bg, 0, 0, $w, $h)
    $bg.Dispose()

    # Mantle inset
    $insetMargin = [int]([Math]::Min($w, $h) * 0.08)
    $mantleRect = New-Object System.Drawing.Rectangle($insetMargin, $insetMargin, ($w - 2 * $insetMargin), ($h - 2 * $insetMargin))
    $radius = [int]([Math]::Min($w, $h) * 0.12)
    $mantlePath = New-RoundedPath $mantleRect $radius
    $mantleBrush = New-Object System.Drawing.SolidBrush $mantle
    $g.FillPath($mantleBrush, $mantlePath)
    $mantleBrush.Dispose()
    $mantlePath.Dispose()

    # Draw cat face glyph (simplified)
    $strokeWidth = [Math]::Max(2, [int]([Math]::Min($w, $h) * 0.06))
    $pen = New-Object System.Drawing.Pen $accent, $strokeWidth
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round

    $cx = $w / 2.0
    $cy = $h / 2.0
    $s = [Math]::Min($w, $h) * 0.25

    # Cat ears
    $g.DrawLine($pen, ($cx - $s * 0.8), ($cy + $s * 0.1), ($cx - $s * 0.5), ($cy - $s * 0.9))
    $g.DrawLine($pen, ($cx - $s * 0.5), ($cy - $s * 0.9), ($cx - $s * 0.1), ($cy - $s * 0.1))
    $g.DrawLine($pen, ($cx + $s * 0.8), ($cy + $s * 0.1), ($cx + $s * 0.5), ($cy - $s * 0.9))
    $g.DrawLine($pen, ($cx + $s * 0.5), ($cy - $s * 0.9), ($cx + $s * 0.1), ($cy - $s * 0.1))

    # Cat face circle
    $faceR = $s * 0.65
    $g.DrawEllipse($pen, ($cx - $faceR), ($cy - $faceR * 0.4), ($faceR * 2), ($faceR * 1.6))

    # Eyes
    $eyeR = $s * 0.12
    $accentBrush = New-Object System.Drawing.SolidBrush $accent
    $g.FillEllipse($accentBrush, ($cx - $s * 0.3 - $eyeR), ($cy - $eyeR), ($eyeR * 2), ($eyeR * 2))
    $g.FillEllipse($accentBrush, ($cx + $s * 0.3 - $eyeR), ($cy - $eyeR), ($eyeR * 2), ($eyeR * 2))
    $accentBrush.Dispose()

    # Nose
    $g.DrawLine($pen, ($cx), ($cy + $s * 0.15), ($cx - $s * 0.1), ($cy + $s * 0.3))
    $g.DrawLine($pen, ($cx), ($cy + $s * 0.15), ($cx + $s * 0.1), ($cy + $s * 0.3))

    $pen.Dispose()
    $g.Dispose()

    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "  $path"
}

function New-Wide {
    param([int]$w, [int]$h, [string]$path)
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias

    $bg = New-Object System.Drawing.SolidBrush $base
    $g.FillRectangle($bg, 0, 0, $w, $h)
    $bg.Dispose()

    New-Logo $h $h (Join-Path $env:TEMP "mr-logo-tmp.png")
    $logo = [System.Drawing.Image]::FromFile((Join-Path $env:TEMP "mr-logo-tmp.png"))
    $g.DrawImage($logo, 12, 12, ($h - 24), ($h - 24))
    $logo.Dispose()

    $font = New-Object System.Drawing.Font 'Segoe UI Semibold', 28
    $textBrush = New-Object System.Drawing.SolidBrush $text
    $g.DrawString('MeetingReminder', $font, $textBrush, ($h + 8), 18)
    $font.Dispose()

    $sub = New-Object System.Drawing.Font 'Segoe UI', 13
    $subBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(0xa6,0xad,0xc8))
    $g.DrawString('Never miss a meeting', $sub, $subBrush, ($h + 10), 70)
    $sub.Dispose()
    $subBrush.Dispose()
    $textBrush.Dispose()

    $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "  $path"
}

Write-Host "Generating MSIX assets in $OutputDir" -ForegroundColor Cyan

New-Logo 50  50  (Join-Path $OutputDir 'StoreLogo.png')
New-Logo 44  44  (Join-Path $OutputDir 'Square44x44Logo.png')
New-Logo 150 150 (Join-Path $OutputDir 'Square150x150Logo.png')
New-Wide 310 150 (Join-Path $OutputDir 'Wide310x150Logo.png')
New-Wide 620 300 (Join-Path $OutputDir 'SplashScreen.png')

# Also generate a .ico for the EXE window icon.
$icoSrc = [System.Drawing.Image]::FromFile((Join-Path $OutputDir 'Square150x150Logo.png'))
$icoBmp = New-Object System.Drawing.Bitmap $icoSrc, 256, 256
$icoSrc.Dispose()
$iconPath = Join-Path $OutputDir 'meetingreminder.ico'
$ms = New-Object System.IO.MemoryStream
$icoBmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
$pngBytes = $ms.ToArray()
$ms.Dispose()
$icoBmp.Dispose()

$buf = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter $buf
$bw.Write([uint16]0)
$bw.Write([uint16]1)
$bw.Write([uint16]1)
$bw.Write([byte]0)
$bw.Write([byte]0)
$bw.Write([byte]0)
$bw.Write([byte]0)
$bw.Write([uint16]1)
$bw.Write([uint16]32)
$bw.Write([uint32]$pngBytes.Length)
$bw.Write([uint32]22)
$bw.Write($pngBytes)
[System.IO.File]::WriteAllBytes($iconPath, $buf.ToArray())
$bw.Dispose()
$buf.Dispose()
Write-Host "  $iconPath"

Write-Host ""
Write-Host "Assets generated." -ForegroundColor Green
