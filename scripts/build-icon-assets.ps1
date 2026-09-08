param(
    [Parameter(Mandatory)]
    [string]$SourcePath,

    [string]$AssetDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) "src\TaskbarLyriz.App\Assets")
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

Add-Type -AssemblyName System.Drawing

$resolvedSource = (Resolve-Path -LiteralPath $SourcePath).Path
$resolvedAssets = (Resolve-Path -LiteralPath $AssetDirectory).Path
$masterPath = Join-Path $resolvedAssets "TaskbarLyrizLogo.png"
Copy-Item -LiteralPath $resolvedSource -Destination $masterPath -Force

function New-ResizedPngBytes
{
    param(
        [System.Drawing.Image]$Source,
        [int]$Width,
        [int]$Height,
        [double]$ContentScale = 1.0
    )

    $bitmap = [System.Drawing.Bitmap]::new(
        $Width,
        $Height,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try
    {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try
        {
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
            $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality

            $availableWidth = $Width * $ContentScale
            $availableHeight = $Height * $ContentScale
            $scale = [Math]::Min($availableWidth / $Source.Width, $availableHeight / $Source.Height)
            $drawWidth = [Math]::Max(1, [int][Math]::Round($Source.Width * $scale))
            $drawHeight = [Math]::Max(1, [int][Math]::Round($Source.Height * $scale))
            $x = [int](($Width - $drawWidth) / 2)
            $y = [int](($Height - $drawHeight) / 2)
            $destination = [System.Drawing.Rectangle]::new($x, $y, $drawWidth, $drawHeight)
            $graphics.DrawImage($Source, $destination)
        }
        finally
        {
            $graphics.Dispose()
        }

        $stream = [System.IO.MemoryStream]::new()
        $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        return $stream.ToArray()
    }
    finally
    {
        $bitmap.Dispose()
    }
}

function Write-PngAsset
{
    param(
        [System.Drawing.Image]$Source,
        [string]$Name,
        [int]$Width,
        [int]$Height,
        [double]$ContentScale = 1.0
    )

    $bytes = New-ResizedPngBytes $Source $Width $Height $ContentScale
    [System.IO.File]::WriteAllBytes((Join-Path $resolvedAssets $Name), $bytes)
}

$source = [System.Drawing.Image]::FromFile($masterPath)
try
{
    Write-PngAsset $source "LockScreenLogo.scale-200.png" 48 48
    Write-PngAsset $source "Square150x150Logo.scale-200.png" 300 300
    Write-PngAsset $source "Square44x44Logo.scale-200.png" 88 88
    Write-PngAsset $source "Square44x44Logo.targetsize-24_altform-unplated.png" 24 24
    Write-PngAsset $source "Square44x44Logo.targetsize-48_altform-lightunplated.png" 48 48
    Write-PngAsset $source "StoreLogo.png" 50 50
    Write-PngAsset $source "Wide310x150Logo.scale-200.png" 620 300 0.82
    Write-PngAsset $source "SplashScreen.scale-200.png" 1240 600 0.72

    $iconSizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
    $iconImages = [System.Collections.Generic.List[byte[]]]::new()
    foreach ($size in $iconSizes)
    {
        $iconImages.Add((New-ResizedPngBytes $source $size $size))
    }
    $iconPath = Join-Path $resolvedAssets "AppIcon.ico"
    $iconStream = [System.IO.File]::Create($iconPath)
    try
    {
        $writer = [System.IO.BinaryWriter]::new($iconStream)
        try
        {
            $writer.Write([uint16]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]$iconImages.Count)
            $imageOffset = 6 + (16 * $iconImages.Count)
            for ($index = 0; $index -lt $iconImages.Count; $index++)
            {
                $size = $iconSizes[$index]
                $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
                $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
                $writer.Write([byte]0)
                $writer.Write([byte]0)
                $writer.Write([uint16]1)
                $writer.Write([uint16]32)
                $writer.Write([uint32]$iconImages[$index].Length)
                $writer.Write([uint32]$imageOffset)
                $imageOffset += $iconImages[$index].Length
            }

            foreach ($image in $iconImages)
            {
                $writer.Write($image)
            }
        }
        finally
        {
            $writer.Dispose()
        }
    }
    finally
    {
        $iconStream.Dispose()
    }
}
finally
{
    $source.Dispose()
}

Write-Host "TaskbarLyriz icon assets updated from $masterPath"
