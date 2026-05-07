#Requires -Version 5.1
# packaging/Assets/ 의 placeholder 아이콘들을 생성한다.
# 추후 진짜 로고로 교체.

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$assetsDir = Join-Path $PSScriptRoot 'Assets'
if (-not (Test-Path $assetsDir)) { New-Item -ItemType Directory -Path $assetsDir | Out-Null }

function New-LogoPng {
    param(
        [int]$Width,
        [int]$Height,
        [string]$Path,
        [string]$Label = ''
    )
    $bmp = New-Object System.Drawing.Bitmap($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit

    $rect = New-Object System.Drawing.Rectangle(0, 0, $Width, $Height)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $rect,
        [System.Drawing.Color]::FromArgb(0xFF, 0x3B, 0x82, 0xF6),
        [System.Drawing.Color]::FromArgb(0xFF, 0x1E, 0x40, 0xAF),
        [System.Drawing.Drawing2D.LinearGradientMode]::Diagonal)
    $g.FillRectangle($brush, $rect)

    if ($Label) {
        $fontSize = [Math]::Max(8, [Math]::Min($Width, $Height) / 5)
        $font = New-Object System.Drawing.Font('Segoe UI', $fontSize, [System.Drawing.FontStyle]::Bold)
        $textBrush = [System.Drawing.Brushes]::White
        $sf = New-Object System.Drawing.StringFormat
        $sf.Alignment = [System.Drawing.StringAlignment]::Center
        $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
        $rectF = New-Object System.Drawing.RectangleF(0, 0, [float]$Width, [float]$Height)
        $g.DrawString($Label, $font, $textBrush, $rectF, $sf)
        $font.Dispose()
        $sf.Dispose()
    }

    $g.Dispose()
    $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $brush.Dispose()
    Write-Host "  [+] $Path ($Width x $Height)"
}

Write-Host 'Generating placeholder logos…'
New-LogoPng -Width 50  -Height 50  -Path (Join-Path $assetsDir 'StoreLogo.png')        -Label 'E2E'
New-LogoPng -Width 44  -Height 44  -Path (Join-Path $assetsDir 'Square44x44Logo.png')  -Label 'E2E'
New-LogoPng -Width 150 -Height 150 -Path (Join-Path $assetsDir 'Square150x150Logo.png')-Label 'E2E'
New-LogoPng -Width 310 -Height 150 -Path (Join-Path $assetsDir 'Wide310x150Logo.png')  -Label 'Everything2Everything'
Write-Host 'Done.'
