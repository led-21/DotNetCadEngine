param(
    [string]$OutputDir = "$PSScriptRoot\..\installer\assets",
    [string]$AppAssetsDir = "$PSScriptRoot\..\src\DotNetCad.Demo\Assets"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

if (-not (Test-Path $OutputDir)) { New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null }
if (-not (Test-Path $AppAssetsDir)) { New-Item -ItemType Directory -Path $AppAssetsDir -Force | Out-Null }

Write-Host "Gerando ativos visuais técnicos para DotNetCadEngine..." -ForegroundColor Cyan

# Cores da identidade visual técnica
$cBgDark    = [System.Drawing.Color]::FromArgb(15, 23, 42)     # #0F172A
$cSurface   = [System.Drawing.Color]::FromArgb(30, 41, 59)     # #1E293B
$cBorder    = [System.Drawing.Color]::FromArgb(51, 65, 85)     # #334155
$cGrid      = [System.Drawing.Color]::FromArgb(71, 85, 105)    # #475569
$cCyan      = [System.Drawing.Color]::FromArgb(56, 189, 248)   # #38BDF8
$cCyanDark  = [System.Drawing.Color]::FromArgb(2, 132, 199)    # #0284C7
$cWhite     = [System.Drawing.Color]::FromArgb(248, 250, 252)  # #F8FAFC
$cMuted     = [System.Drawing.Color]::FromArgb(148, 163, 184)  # #94A3B8

function Draw-CadIcon([System.Drawing.Graphics]$g, [int]$width, [int]$height) {
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

    # Background card
    $margin = [Math]::Max(1, [int]($width * 0.05))
    $rectW = $width - (2 * $margin)
    $rectH = $height - (2 * $margin)
    $rect = New-Object System.Drawing.Rectangle($margin, $margin, $rectW, $rectH)
    $brushBg = New-Object System.Drawing.SolidBrush($cBgDark)
    $penBorder = New-Object System.Drawing.Pen($cBorder, [float]([Math]::Max(1.0, $width * 0.025)))
    
    # Fill rounded or rect
    $g.FillRectangle($brushBg, $rect)
    $g.DrawRectangle($penBorder, $rect)

    # Grid lines / dots
    $penGrid = New-Object System.Drawing.Pen($cGrid, 1.0)
    $penGrid.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash
    $step = [Math]::Max(4, [int]($width / 6))
    for ($x = $rect.Left + $step; $x -lt $rect.Right; $x += $step) {
        $g.DrawLine($penGrid, $x, $rect.Top, $x, $rect.Bottom)
    }
    for ($y = $rect.Top + $step; $y -lt $rect.Bottom; $y += $step) {
        $g.DrawLine($penGrid, $rect.Left, $y, $rect.Right, $y)
    }

    # CAD Arc
    $penArc = New-Object System.Drawing.Pen($cCyanDark, [float]([Math]::Max(1.0, $width * 0.03)))
    $penArc.DashStyle = [System.Drawing.Drawing2D.DashStyle]::DashDot
    $arcRect = New-Object System.Drawing.Rectangle([int]($width * 0.15), [int]($height * 0.15), [int]($width * 0.7), [int]($height * 0.7))
    $g.DrawArc($penArc, $arcRect, 180, 90)

    # Main CAD Geometric Polygon (Mechanical Bracket)
    $p1 = New-Object System.Drawing.PointF([float]($width * 0.22), [float]($height * 0.78))
    $p2 = New-Object System.Drawing.PointF([float]($width * 0.78), [float]($height * 0.78))
    $p3 = New-Object System.Drawing.PointF([float]($width * 0.78), [float]($height * 0.55))
    $p4 = New-Object System.Drawing.PointF([float]($width * 0.50), [float]($height * 0.55))
    $p5 = New-Object System.Drawing.PointF([float]($width * 0.50), [float]($height * 0.22))
    $p6 = New-Object System.Drawing.PointF([float]($width * 0.22), [float]($height * 0.22))
    [System.Drawing.PointF[]]$poly = @($p1, $p2, $p3, $p4, $p5, $p6)

    $brushPoly = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(200, 30, 41, 59))
    $penPoly = New-Object System.Drawing.Pen($cCyan, [float]([Math]::Max(1.5, $width * 0.04)))
    $penPoly.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

    $g.FillPolygon($brushPoly, $poly)
    $g.DrawPolygon($penPoly, $poly)

    # Snaps & Dimension arrow
    if ($width -ge 32) {
        $penSnap = New-Object System.Drawing.Pen($cWhite, [float]([Math]::Max(1.0, $width * 0.025)))
        $snapSz = [Math]::Max(2.0, $width * 0.05)
        $g.DrawRectangle($penSnap, [float]($p1.X - $snapSz/2), [float]($p1.Y - $snapSz/2), $snapSz, $snapSz)
        $g.DrawRectangle($penSnap, [float]($p2.X - $snapSz/2), [float]($p2.Y - $snapSz/2), $snapSz, $snapSz)

        # Dimension line
        $dimY = [float]($height * 0.88)
        if ($dimY -lt ($height - 2)) {
            $penDim = New-Object System.Drawing.Pen($cCyan, 1.0)
            $g.DrawLine($penDim, $p1.X, $dimY, $p2.X, $dimY)
            $g.DrawLine($penDim, $p1.X, [float]($dimY - 3), $p1.X, [float]($dimY + 3))
            $g.DrawLine($penDim, $p2.X, [float]($dimY - 3), $p2.X, [float]($dimY + 3))
        }
    }

    $brushBg.Dispose()
    $penBorder.Dispose()
    $penGrid.Dispose()
    $penArc.Dispose()
    $brushPoly.Dispose()
    $penPoly.Dispose()
}

# 1. Gerar tamanhos de ícone para o ICO
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$bitmaps = @()
foreach ($sz in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($sz, $sz, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $gfx = [System.Drawing.Graphics]::FromImage($bmp)
    $gfx.Clear([System.Drawing.Color]::Transparent)
    Draw-CadIcon $gfx $sz $sz
    $gfx.Dispose()
    $bitmaps += $bmp
}

# Salvar como .ico multi-resolução
$icoPath = Join-Path $AppAssetsDir "dotnetcad.ico"
$icoInstallerPath = Join-Path $OutputDir "dotnetcad.ico"

$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter($fs)

# ICONHEADER
$bw.Write([uint16]0) # Reserved
$bw.Write([uint16]1) # Type (1 = Icon)
$bw.Write([uint16]$sizes.Count) # Image count

# PNG streams for each size
$pngStreams = @()
foreach ($bmp in $bitmaps) {
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngStreams += $ms
}

$offset = 6 + (16 * $sizes.Count)
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $sz = $sizes[$i]
    $pngLen = [int]$pngStreams[$i].Length

    $dimByte = [byte]$(if ($sz -ge 256) { 0 } else { $sz })
    $bw.Write($dimByte) # Width
    $bw.Write($dimByte) # Height
    $bw.Write([byte]0)                                     # Color count
    $bw.Write([byte]0)                                     # Reserved
    $bw.Write([uint16]1)                                   # Color planes
    $bw.Write([uint16]32)                                  # Bits per pixel
    $bw.Write([uint32]$pngLen)                             # Bytes in resource
    $bw.Write([uint32]$offset)                             # Image offset

    $offset += $pngLen
}

foreach ($ms in $pngStreams) {
    $bytes = $ms.ToArray()
    $bw.Write($bytes)
    $ms.Dispose()
}

$bw.Flush()
$fs.Close()
Copy-Item -Path $icoPath -Destination $icoInstallerPath -Force
Write-Host "Ícone criado: $icoPath ($($sizes.Count) resoluções)" -ForegroundColor Green

# 2. Gerar Wizard Banner (164 x 314 BMP) para Inno Setup
$bannerWidth = 164
$bannerHeight = 314
$bannerBmp = New-Object System.Drawing.Bitmap($bannerWidth, $bannerHeight, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
$bgfx = [System.Drawing.Graphics]::FromImage($bannerBmp)
$bgfx.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

# Fundo escuro com gradiente sutil
$bannerBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    (New-Object System.Drawing.Point(0, 0)),
    (New-Object System.Drawing.Point($bannerWidth, $bannerHeight)),
    $cSurface, $cBgDark
)
$bgfx.FillRectangle($bannerBrush, 0, 0, $bannerWidth, $bannerHeight)

# Grade técnica de engenharia
$penGridB = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(40, 56, 189, 248), 1.0)
$penGridB.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash
for ($gx = 16; $gx -lt $bannerWidth; $gx += 24) { $bgfx.DrawLine($penGridB, $gx, 0, $gx, $bannerHeight) }
for ($gy = 16; $gy -lt $bannerHeight; $gy += 24) { $bgfx.DrawLine($penGridB, 0, $gy, $bannerWidth, $gy) }

# Desenho CAD técnico no centro do banner
$bgfx.TranslateTransform(14, 80)
Draw-CadIcon $bgfx 136 136
$bgfx.ResetTransform()

# Textos técnicos na parte inferior do banner
$fontTech = New-Object System.Drawing.Font("Segoe UI", 9.0, [System.Drawing.FontStyle]::Bold)
$fontSub = New-Object System.Drawing.Font("Segoe UI", 7.5, [System.Drawing.FontStyle]::Regular)
$brushCyan = New-Object System.Drawing.SolidBrush($cCyan)
$brushMuted = New-Object System.Drawing.SolidBrush($cMuted)

$bgfx.DrawString("DotNetCadEngine", $fontTech, $brushCyan, 16, 235)
$bgfx.DrawString("CAD 2D Técnico", $fontSub, $brushMuted, 16, 255)
$bgfx.DrawString("Motor C# / WPF / MCP", $fontSub, $brushMuted, 16, 272)

$bannerPath = Join-Path $OutputDir "wizard-banner.bmp"
$bannerBmp.Save($bannerPath, [System.Drawing.Imaging.ImageFormat]::Bmp)
$bgfx.Dispose()
$bannerBmp.Dispose()
Write-Host "Wizard banner criado: $bannerPath" -ForegroundColor Green

# 3. Gerar Wizard Small (55 x 58 BMP) para o canto superior direito do Inno Setup
$smallWidth = 55
$smallHeight = 58
$smallBmp = New-Object System.Drawing.Bitmap($smallWidth, $smallHeight, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
$sgfx = [System.Drawing.Graphics]::FromImage($smallBmp)
$sgfx.Clear($cBgDark)
Draw-CadIcon $sgfx $smallWidth $smallHeight
$smallPath = Join-Path $OutputDir "wizard-small.bmp"
$smallBmp.Save($smallPath, [System.Drawing.Imaging.ImageFormat]::Bmp)
$sgfx.Dispose()
$smallBmp.Dispose()
Write-Host "Wizard small logo criado: $smallPath" -ForegroundColor Green

foreach ($bmp in $bitmaps) { $bmp.Dispose() }
Write-Host "Todos os ativos visuais foram gerados com sucesso!" -ForegroundColor Green
