param(
    [string]$OutputDir = "$PSScriptRoot\..\installer\assets",
    [string]$AppAssetsDir = "$PSScriptRoot\..\src\DotNetCad.Demo\Assets"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

if (-not (Test-Path $OutputDir)) { New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null }
if (-not (Test-Path $AppAssetsDir)) { New-Item -ItemType Directory -Path $AppAssetsDir -Force | Out-Null }

Write-Host "Gerando icones e ativos visuais de alta fidelidade para DotNetCadEngine..." -ForegroundColor Cyan

# Paleta técnica de alta precisão CAD
$cBgTop       = [System.Drawing.Color]::FromArgb(30, 41, 59)     # #1E293B
$cBgBottom    = [System.Drawing.Color]::FromArgb(7, 13, 26)      # #070D1A
$cBorder      = [System.Drawing.Color]::FromArgb(51, 65, 85)     # #334155
$cBorderGlow  = [System.Drawing.Color]::FromArgb(120, 56, 189, 248) # #38BDF8 semi-glow
$cGrid        = [System.Drawing.Color]::FromArgb(35, 56, 189, 248) # #38BDF8 @ 14%
$cCyanLight   = [System.Drawing.Color]::FromArgb(224, 242, 254)  # #E0F2FE
$cCyanMid     = [System.Drawing.Color]::FromArgb(56, 189, 248)    # #38BDF8
$cBlueDark    = [System.Drawing.Color]::FromArgb(2, 132, 199)     # #0284C7
$cWhite       = [System.Drawing.Color]::FromArgb(255, 255, 255)   # #FFFFFF
$cMuted       = [System.Drawing.Color]::FromArgb(148, 163, 184)   # #94A3B8

function Draw-ModernCadIcon([System.Drawing.Graphics]$g, [int]$size) {
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit

    $scale = [float]($size / 256.0)

    # 1. Base Squircle / Rounded Tech Container
    $margin = [float](14.0 * $scale)
    $cardSize = [float]($size - (2.0 * $margin))
    $radius = [float](54.0 * $scale)
    $d = [float]($radius * 2.0)

    $pathCard = New-Object System.Drawing.Drawing2D.GraphicsPath
    $pathCard.AddArc($margin, $margin, $d, $d, 180, 90)
    $pathCard.AddArc([float]($margin + $cardSize - $d), $margin, $d, $d, 270, 90)
    $pathCard.AddArc([float]($margin + $cardSize - $d), [float]($margin + $cardSize - $d), $d, $d, 0, 90)
    $pathCard.AddArc($margin, [float]($margin + $cardSize - $d), $d, $d, 90, 90)
    $pathCard.CloseFigure()

    # Preenchimento gradiente suave da base
    $ptTop = [System.Drawing.PointF]::new(0, $margin)
    $ptBottom = [System.Drawing.PointF]::new(0, [float]($margin + $cardSize))
    $brushCard = [System.Drawing.Drawing2D.LinearGradientBrush]::new($ptTop, $ptBottom, $cBgTop, $cBgBottom)
    $g.FillPath($brushCard, $pathCard)

    # Borda externa técnica
    $borderW = [float]([Math]::Max(1.0, 3.0 * $scale))
    $penBorder = [System.Drawing.Pen]::new($cBorder, $borderW)
    $g.DrawPath($penBorder, $pathCard)

    # 2. Grade Blueprint e Mostrador de Compasso (para ícones >= 48px)
    if ($size -ge 48) {
        $penGrid = [System.Drawing.Pen]::new($cGrid, [float]([Math]::Max(1.0, 1.0 * $scale)))
        $penGrid.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash
        $gridStep = [float](32.0 * $scale)
        for ($x = $margin + $gridStep; $x -lt ($margin + $cardSize); $x += $gridStep) {
            $g.DrawLine($penGrid, $x, [float]($margin + 6.0), $x, [float]($margin + $cardSize - 6.0))
        }
        for ($y = $margin + $gridStep; $y -lt ($margin + $cardSize); $y += $gridStep) {
            $g.DrawLine($penGrid, [float]($margin + 6.0), $y, [float]($margin + $cardSize - 6.0), $y)
        }

        # Anel de compasso técnico concêntrico
        $penCompass = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(45, 56, 189, 248), [float](1.0 * $scale))
        $penCompass.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dot
        $center = [float]($size / 2.0)
        $r1 = [float](76.0 * $scale)
        $g.DrawEllipse($penCompass, [float]($center - $r1), [float]($center - $r1), [float](2.0 * $r1), [float](2.0 * $r1))
        $penCompass.Dispose()
        $penGrid.Dispose()
    }

    # 3. Braço / Régua Vertical do Compasso (Spine)
    $spineX = [float](76.0 * $scale)
    $spineY = [float](56.0 * $scale)
    $spineW = [float](28.0 * $scale)
    $spineH = [float](144.0 * $scale)
    $spineR = [float]([Math]::Max(2.0, 6.0 * $scale))
    $sd = [float]($spineR * 2.0)

    $pathSpine = New-Object System.Drawing.Drawing2D.GraphicsPath
    $pathSpine.AddArc($spineX, $spineY, $sd, $sd, 180, 90)
    $pathSpine.AddArc([float]($spineX + $spineW - $sd), $spineY, $sd, $sd, 270, 90)
    $pathSpine.AddArc([float]($spineX + $spineW - $sd), [float]($spineY + $spineH - $sd), $sd, $sd, 0, 90)
    $pathSpine.AddArc($spineX, [float]($spineY + $spineH - $sd), $sd, $sd, 90, 90)
    $pathSpine.CloseFigure()

    $ptSpine1 = [System.Drawing.PointF]::new($spineX, $spineY)
    $ptSpine2 = [System.Drawing.PointF]::new($spineX, [float]($spineY + $spineH))
    $brushSpine = [System.Drawing.Drawing2D.LinearGradientBrush]::new($ptSpine1, $ptSpine2, $cCyanMid, $cBlueDark)
    $g.FillPath($brushSpine, $pathSpine)

    # Marcadores de graduação (ticks) da régua
    if ($size -ge 40) {
        $penTick = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(210, 2, 132, 199), [float]([Math]::Max(1.0, 1.8 * $scale)))
        $tickStep = [float](16.0 * $scale)
        for ($ty = $spineY + $tickStep; $ty -lt ($spineY + $spineH - 6.0); $ty += $tickStep) {
            $tickLen = if (([int]($ty / $tickStep)) % 2 -eq 0) { [float](10.0 * $scale) } else { [float](6.0 * $scale) }
            $g.DrawLine($penTick, $spineX, [float]$ty, [float]($spineX + $tickLen), [float]$ty)
        }
        $penTick.Dispose()
    }

    # 4. Fita Curva de Precisão (Arco do Monograma CAD)
    $arcStrokeW = [float]([Math]::Max(2.5, 17.0 * $scale))
    $penArc = [System.Drawing.Pen]::new($cCyanMid, $arcStrokeW)
    $penArc.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $penArc.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

    $arcRectX = [float](86.0 * $scale)
    $arcRectY = [float](62.0 * $scale)
    $arcRectW = [float](110.0 * $scale)
    $arcRectH = [float](132.0 * $scale)
    $g.DrawArc($penArc, $arcRectX, $arcRectY, $arcRectW, $arcRectH, 270, 180)

    # Linha de destaque luminoso interno do arco
    if ($size -ge 32) {
        $penHighlight = [System.Drawing.Pen]::new($cCyanLight, [float]([Math]::Max(1.0, 3.2 * $scale)))
        $penHighlight.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $penHighlight.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $g.DrawArc($penHighlight, [float]($arcRectX + 2.0 * $scale), [float]($arcRectY + 2.0 * $scale), [float]($arcRectW - 4.0 * $scale), [float]($arcRectH - 4.0 * $scale), 280, 160)
        $penHighlight.Dispose()
    }

    # 5. Diagonal Estrutural de Engenharia (Trelica CAD 45°)
    if ($size -ge 24) {
        $pStrut1 = [System.Drawing.PointF]::new([float](88.0 * $scale), [float](186.0 * $scale))
        $pStrut2 = [System.Drawing.PointF]::new([float](172.0 * $scale), [float](104.0 * $scale))
        $penStrut = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(230, 2, 132, 199), [float]([Math]::Max(1.5, 9.5 * $scale)))
        $penStrut.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $penStrut.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $g.DrawLine($penStrut, $pStrut1, $pStrut2)
        $penStrut.Dispose()
    }

    # 6. Snap Nodes / Pontos de Precisão (Vertices CAD)
    $nodeR = [float]([Math]::Max(1.5, 7.0 * $scale))
    $penNodeBorder = [System.Drawing.Pen]::new($cBlueDark, [float]([Math]::Max(1.0, 2.0 * $scale)))
    $brushNode = [System.Drawing.SolidBrush]::new($cWhite)

    # Vértice Superior
    $topX = [float](102.0 * $scale)
    $topY = [float](62.0 * $scale)
    $g.FillEllipse($brushNode, [float]($topX - $nodeR), [float]($topY - $nodeR), [float](2.0 * $nodeR), [float](2.0 * $nodeR))
    $g.DrawEllipse($penNodeBorder, [float]($topX - $nodeR), [float]($topY - $nodeR), [float](2.0 * $nodeR), [float](2.0 * $nodeR))

    # Vértice Ápice Direito (Ponto Focal)
    $apexX = [float](196.0 * $scale)
    $apexY = [float](128.0 * $scale)
    $apexR = [float]([Math]::Max(2.0, 8.0 * $scale))
    $g.FillEllipse($brushNode, [float]($apexX - $apexR), [float]($apexY - $apexR), [float](2.0 * $apexR), [float](2.0 * $apexR))
    $g.DrawEllipse($penNodeBorder, [float]($apexX - $apexR), [float]($apexY - $apexR), [float](2.0 * $apexR), [float](2.0 * $apexR))

    # Vértice Inferior
    $botX = [float](102.0 * $scale)
    $botY = [float](194.0 * $scale)
    $g.FillEllipse($brushNode, [float]($botX - $nodeR), [float]($botY - $nodeR), [float](2.0 * $nodeR), [float](2.0 * $nodeR))
    $g.DrawEllipse($penNodeBorder, [float]($botX - $nodeR), [float]($botY - $nodeR), [float](2.0 * $nodeR), [float](2.0 * $nodeR))

    # Mira de Alvo Central / Crosshair (para ícones >= 48px)
    if ($size -ge 48) {
        $penCross = [System.Drawing.Pen]::new($cCyanMid, [float]([Math]::Max(1.0, 1.5 * $scale)))
        $cX = [float](138.0 * $scale)
        $cY = [float](128.0 * $scale)
        $cR = [float](5.0 * $scale)
        $g.DrawEllipse($penCross, [float]($cX - $cR), [float]($cY - $cR), [float](2.0 * $cR), [float](2.0 * $cR))
        $g.DrawLine($penCross, [float]($cX - 8.0 * $scale), $cY, [float]($cX + 8.0 * $scale), $cY)
        $g.DrawLine($penCross, $cX, [float]($cY - 8.0 * $scale), $cX, [float]($cY + 8.0 * $scale))
        $penCross.Dispose()
    }

    # Descarte de objetos gráficos
    $brushCard.Dispose()
    $penBorder.Dispose()
    $pathCard.Dispose()
    $pathSpine.Dispose()
    $brushSpine.Dispose()
    $penArc.Dispose()
    $brushNode.Dispose()
    $penNodeBorder.Dispose()
}

# 1. Gerar bitmaps em múltiplas resoluções para o arquivo .ICO
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$bitmaps = @()
foreach ($sz in $sizes) {
    $bmp = [System.Drawing.Bitmap]::new($sz, $sz, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $gfx = [System.Drawing.Graphics]::FromImage($bmp)
    $gfx.Clear([System.Drawing.Color]::Transparent)
    Draw-ModernCadIcon $gfx $sz
    $gfx.Dispose()
    $bitmaps += $bmp
}

# Salvar como .ico multi-resolução
$icoPath = Join-Path $AppAssetsDir "dotnetcad.ico"
$icoInstallerPath = Join-Path $OutputDir "dotnetcad.ico"

$fs = [System.IO.File]::Create($icoPath)
$bw = [System.IO.BinaryWriter]::new($fs)

# ICONHEADER
$bw.Write([uint16]0) # Reserved
$bw.Write([uint16]1) # Type (1 = Icon)
$bw.Write([uint16]$sizes.Count) # Image count

# PNG streams para cada tamanho
$pngStreams = @()
foreach ($bmp in $bitmaps) {
    $ms = [System.IO.MemoryStream]::new()
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
Write-Host "Novo icone de alta qualidade criado: $icoPath ($($sizes.Count) resolucoes)" -ForegroundColor Green

# 2. Gerar Wizard Banner (164 x 314 BMP) para Inno Setup
$bannerWidth = 164
$bannerHeight = 314
$bannerBmp = [System.Drawing.Bitmap]::new($bannerWidth, $bannerHeight, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
$bgfx = [System.Drawing.Graphics]::FromImage($bannerBmp)
$bgfx.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

# Fundo escuro com gradiente
$ptBanner1 = [System.Drawing.Point]::new(0, 0)
$ptBanner2 = [System.Drawing.Point]::new($bannerWidth, $bannerHeight)
$bannerBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new($ptBanner1, $ptBanner2, $cBgTop, $cBgBottom)
$bgfx.FillRectangle($bannerBrush, 0, 0, $bannerWidth, $bannerHeight)

# Grade técnica de engenharia no banner
$penGridB = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(30, 56, 189, 248), 1.0)
$penGridB.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash
for ($gx = 16; $gx -lt $bannerWidth; $gx += 24) { $bgfx.DrawLine($penGridB, $gx, 0, $gx, $bannerHeight) }
for ($gy = 16; $gy -lt $bannerHeight; $gy += 24) { $bgfx.DrawLine($penGridB, 0, $gy, $bannerWidth, $gy) }

# Desenho do ícone no banner
$bgfx.TranslateTransform(20, 68)
Draw-ModernCadIcon $bgfx 124
$bgfx.ResetTransform()

# Tipografia sóbria do banner
$fontTech = [System.Drawing.Font]::new("Segoe UI", 10.0, [System.Drawing.FontStyle]::Bold)
$fontSub  = [System.Drawing.Font]::new("Segoe UI", 8.0, [System.Drawing.FontStyle]::Regular)
$brushCyan = [System.Drawing.SolidBrush]::new($cCyanMid)
$brushMuted = [System.Drawing.SolidBrush]::new($cMuted)
$brushWhite = [System.Drawing.SolidBrush]::new($cWhite)

$bgfx.DrawString("DotNetCadEngine", $fontTech, $brushCyan, 16, 218)
$bgfx.DrawString("CAD 2D Técnico", $fontSub, $brushWhite, 16, 240)
$bgfx.DrawString("Motor C# / WPF / MCP", $fontSub, $brushMuted, 16, 258)
$bgfx.DrawString("Versão 1.0.0 (x64)", $fontSub, $brushMuted, 16, 276)

$bannerPath = Join-Path $OutputDir "wizard-banner.bmp"
$bannerBmp.Save($bannerPath, [System.Drawing.Imaging.ImageFormat]::Bmp)
$bgfx.Dispose()
$bannerBmp.Dispose()
Write-Host "Novo Wizard banner criado: $bannerPath" -ForegroundColor Green

# 3. Gerar Wizard Small (55 x 58 BMP)
$smallWidth = 55
$smallHeight = 58
$smallBmp = [System.Drawing.Bitmap]::new($smallWidth, $smallHeight, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
$sgfx = [System.Drawing.Graphics]::FromImage($smallBmp)
$sgfx.Clear($cBgBottom)
$sgfx.TranslateTransform(-1, 0)
Draw-ModernCadIcon $sgfx 56
$sgfx.ResetTransform()
$smallPath = Join-Path $OutputDir "wizard-small.bmp"
$smallBmp.Save($smallPath, [System.Drawing.Imaging.ImageFormat]::Bmp)
$sgfx.Dispose()
$smallBmp.Dispose()
Write-Host "Novo Wizard small logo criado: $smallPath" -ForegroundColor Green

foreach ($bmp in $bitmaps) { $bmp.Dispose() }
Write-Host "Todos os novos ativos visuais foram gerados com sucesso!" -ForegroundColor Green
