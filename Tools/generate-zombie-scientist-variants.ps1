param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$enemyRoot = Join-Path $Root 'Game\Images\Enemy'
$sourceRoot = Join-Path $enemyRoot 'zombie_scientist'

if (-not (Test-Path -LiteralPath $sourceRoot)) {
    throw "Source sprite folder not found: $sourceRoot"
}

$resolvedEnemyRoot = (Resolve-Path -LiteralPath $enemyRoot).Path

function Assert-InEnemyRoot {
    param([string]$Path)

    $full = [System.IO.Path]::GetFullPath($Path)
    if (-not $full.StartsWith($resolvedEnemyRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to write outside enemy image root: $full"
    }
}

function Clamp-Byte {
    param([double]$Value)
    if ($Value -lt 0) { return 0 }
    if ($Value -gt 255) { return 255 }
    return [int][Math]::Round($Value)
}

function New-VariantColor {
    param(
        [System.Drawing.Color]$Color,
        [System.Drawing.Color]$Tint,
        [double]$Strength,
        [double]$Brightness,
        [double]$Contrast
    )

    if ($Color.A -le 8) {
        return [System.Drawing.Color]::FromArgb(0, 0, 0, 0)
    }

    $lum = (($Color.R * 0.299) + ($Color.G * 0.587) + ($Color.B * 0.114)) / 255.0
    $shade = (($lum - 0.5) * $Contrast) + 0.5
    if ($shade -lt 0) { $shade = 0 }
    if ($shade -gt 1) { $shade = 1 }

    $targetR = $Tint.R * (0.34 + $shade * 0.86) * $Brightness
    $targetG = $Tint.G * (0.34 + $shade * 0.86) * $Brightness
    $targetB = $Tint.B * (0.34 + $shade * 0.86) * $Brightness

    $r = ($Color.R * (1.0 - $Strength)) + ($targetR * $Strength)
    $g = ($Color.G * (1.0 - $Strength)) + ($targetG * $Strength)
    $b = ($Color.B * (1.0 - $Strength)) + ($targetB * $Strength)

    return [System.Drawing.Color]::FromArgb($Color.A, (Clamp-Byte $r), (Clamp-Byte $g), (Clamp-Byte $b))
}

function New-VariantBitmap {
    param(
        [System.Drawing.Bitmap]$Source,
        [System.Drawing.Color]$Tint,
        [double]$Strength,
        [double]$Brightness,
        [double]$Contrast
    )

    $rect = [System.Drawing.Rectangle]::new(0, 0, $Source.Width, $Source.Height)
    $target = [System.Drawing.Bitmap]::new($Source.Width, $Source.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $sourceData = $null
    $targetData = $null

    try {
        $sourceData = $Source.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $targetData = $target.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::WriteOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

        $sourceStride = [Math]::Abs($sourceData.Stride)
        $targetStride = [Math]::Abs($targetData.Stride)
        $sourceBytes = [byte[]]::new($sourceStride * $Source.Height)
        $targetBytes = [byte[]]::new($targetStride * $Source.Height)

        [System.Runtime.InteropServices.Marshal]::Copy($sourceData.Scan0, $sourceBytes, 0, $sourceBytes.Length)

        for ($yy = 0; $yy -lt $Source.Height; $yy++) {
            $sourceRow = $yy * $sourceStride
            $targetRow = $yy * $targetStride

            for ($xx = 0; $xx -lt $Source.Width; $xx++) {
                $sourceOffset = $sourceRow + ($xx * 4)
                $targetOffset = $targetRow + ($xx * 4)

                $b = [double]$sourceBytes[$sourceOffset]
                $g = [double]$sourceBytes[$sourceOffset + 1]
                $r = [double]$sourceBytes[$sourceOffset + 2]
                $a = $sourceBytes[$sourceOffset + 3]

                if ($a -le 8) {
                    $targetBytes[$targetOffset] = 0
                    $targetBytes[$targetOffset + 1] = 0
                    $targetBytes[$targetOffset + 2] = 0
                    $targetBytes[$targetOffset + 3] = 0
                    continue
                }

                $lum = (($r * 0.299) + ($g * 0.587) + ($b * 0.114)) / 255.0
                $shade = (($lum - 0.5) * $Contrast) + 0.5
                if ($shade -lt 0) { $shade = 0 }
                if ($shade -gt 1) { $shade = 1 }

                $targetR = $Tint.R * (0.34 + $shade * 0.86) * $Brightness
                $targetG = $Tint.G * (0.34 + $shade * 0.86) * $Brightness
                $targetB = $Tint.B * (0.34 + $shade * 0.86) * $Brightness

                $targetBytes[$targetOffset] = Clamp-Byte (($b * (1.0 - $Strength)) + ($targetB * $Strength))
                $targetBytes[$targetOffset + 1] = Clamp-Byte (($g * (1.0 - $Strength)) + ($targetG * $Strength))
                $targetBytes[$targetOffset + 2] = Clamp-Byte (($r * (1.0 - $Strength)) + ($targetR * $Strength))
                $targetBytes[$targetOffset + 3] = $a
            }
        }

        [System.Runtime.InteropServices.Marshal]::Copy($targetBytes, 0, $targetData.Scan0, $targetBytes.Length)
        return $target
    }
    catch {
        $target.Dispose()
        throw
    }
    finally {
        if ($sourceData -ne $null) {
            $Source.UnlockBits($sourceData)
        }

        if ($targetData -ne $null) {
            $target.UnlockBits($targetData)
        }
    }
}

function Fill-Rect {
    param(
        [System.Drawing.Graphics]$Graphics,
        [System.Drawing.Color]$Color,
        [float]$X,
        [float]$Y,
        [float]$Width,
        [float]$Height
    )

    $brush = [System.Drawing.SolidBrush]::new($Color)
    try {
        $Graphics.FillRectangle($brush, $X, $Y, $Width, $Height)
    }
    finally {
        $brush.Dispose()
    }
}

function Fill-Ellipse {
    param(
        [System.Drawing.Graphics]$Graphics,
        [System.Drawing.Color]$Color,
        [float]$X,
        [float]$Y,
        [float]$Width,
        [float]$Height
    )

    $brush = [System.Drawing.SolidBrush]::new($Color)
    try {
        $Graphics.FillEllipse($brush, $X, $Y, $Width, $Height)
    }
    finally {
        $brush.Dispose()
    }
}

function Fill-Polygon {
    param(
        [System.Drawing.Graphics]$Graphics,
        [System.Drawing.Color]$Color,
        [System.Drawing.PointF[]]$Points
    )

    $brush = [System.Drawing.SolidBrush]::new($Color)
    try {
        $Graphics.FillPolygon($brush, $Points)
    }
    finally {
        $brush.Dispose()
    }
}

function Draw-VariantOverlay {
    param(
        [System.Drawing.Graphics]$Graphics,
        [string]$Variant,
        [string]$Action,
        [int]$Width,
        [int]$Height
    )

    $Graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::None
    $Graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $Graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half

    $x = { param([double]$n) [float]($Width * $n) }
    $y = { param([double]$n) [float]($Height * $n) }

    $isDeath = $Action -ieq 'DEATH'

    switch ($Variant) {
        'uzi_trooper' {
            $vest = [System.Drawing.Color]::FromArgb(92, 18, 22, 24)
            $metal = [System.Drawing.Color]::FromArgb(210, 34, 38, 42)
            $muzzle = [System.Drawing.Color]::FromArgb(230, 16, 18, 20)
            Fill-Rect $Graphics $vest (& $x 0.37) (& $y 0.38) (& $x 0.24) (& $y 0.23)
            if (-not $isDeath) {
                Fill-Rect $Graphics $metal (& $x 0.55) (& $y 0.51) (& $x 0.20) (& $y 0.045)
                Fill-Rect $Graphics $metal (& $x 0.59) (& $y 0.555) (& $x 0.055) (& $y 0.105)
                Fill-Rect $Graphics $muzzle (& $x 0.73) (& $y 0.50) (& $x 0.08) (& $y 0.026)
            }
        }
        'plasma_tech' {
            $glow = [System.Drawing.Color]::FromArgb(185, 38, 224, 224)
            $core = [System.Drawing.Color]::FromArgb(235, 166, 255, 245)
            $purple = [System.Drawing.Color]::FromArgb(120, 80, 42, 132)
            Fill-Rect $Graphics $purple (& $x 0.39) (& $y 0.39) (& $x 0.20) (& $y 0.18)
            if (-not $isDeath) {
                Fill-Ellipse $Graphics $glow (& $x 0.54) (& $y 0.49) (& $x 0.16) (& $y 0.16)
                Fill-Rect $Graphics $glow (& $x 0.61) (& $y 0.53) (& $x 0.18) (& $y 0.035)
                Fill-Ellipse $Graphics $core (& $x 0.585) (& $y 0.535) (& $x 0.055) (& $y 0.055)
                Fill-Rect $Graphics $core (& $x 0.74) (& $y 0.525) (& $x 0.045) (& $y 0.018)
            }
        }
        'grenadier_scientist' {
            $armor = [System.Drawing.Color]::FromArgb(120, 58, 66, 34)
            $hazard = [System.Drawing.Color]::FromArgb(220, 222, 176, 42)
            $dark = [System.Drawing.Color]::FromArgb(190, 26, 30, 18)
            Fill-Rect $Graphics $armor (& $x 0.36) (& $y 0.39) (& $x 0.27) (& $y 0.22)
            Fill-Rect $Graphics $hazard (& $x 0.38) (& $y 0.43) (& $x 0.20) (& $y 0.026)
            Fill-Rect $Graphics $hazard (& $x 0.41) (& $y 0.49) (& $x 0.20) (& $y 0.026)
            if (-not $isDeath) {
                foreach ($gx in 0.39,0.46,0.53,0.60) {
                    Fill-Ellipse $Graphics $dark (& $x $gx) (& $y 0.59) (& $x 0.045) (& $y 0.06)
                }
            }
        }
        'lab_butcher' {
            $apron = [System.Drawing.Color]::FromArgb(150, 210, 198, 174)
            $blood = [System.Drawing.Color]::FromArgb(210, 132, 14, 14)
            $blade = [System.Drawing.Color]::FromArgb(225, 190, 190, 174)
            $handle = [System.Drawing.Color]::FromArgb(220, 50, 28, 18)
            Fill-Rect $Graphics $apron (& $x 0.39) (& $y 0.39) (& $x 0.22) (& $y 0.27)
            Fill-Ellipse $Graphics $blood (& $x 0.45) (& $y 0.46) (& $x 0.04) (& $y 0.05)
            Fill-Ellipse $Graphics $blood (& $x 0.52) (& $y 0.55) (& $x 0.035) (& $y 0.04)
            Fill-Rect $Graphics $blood (& $x 0.48) (& $y 0.61) (& $x 0.08) (& $y 0.025)
            if (-not $isDeath) {
                Fill-Rect $Graphics $handle (& $x 0.61) (& $y 0.53) (& $x 0.045) (& $y 0.16)
                $points = [System.Drawing.PointF[]]@(
                    [System.Drawing.PointF]::new((& $x 0.64), (& $y 0.49)),
                    [System.Drawing.PointF]::new((& $x 0.77), (& $y 0.52)),
                    [System.Drawing.PointF]::new((& $x 0.70), (& $y 0.63)),
                    [System.Drawing.PointF]::new((& $x 0.62), (& $y 0.61))
                )
                Fill-Polygon $Graphics $blade $points
            }
        }
    }
}

$variants = @(
    @{
        Id = 'uzi_trooper'
        Tint = [System.Drawing.Color]::FromArgb(72, 88, 96)
        Strength = 0.42
        Brightness = 0.82
        Contrast = 1.20
    },
    @{
        Id = 'plasma_tech'
        Tint = [System.Drawing.Color]::FromArgb(46, 148, 174)
        Strength = 0.50
        Brightness = 0.94
        Contrast = 1.16
    },
    @{
        Id = 'grenadier_scientist'
        Tint = [System.Drawing.Color]::FromArgb(96, 112, 48)
        Strength = 0.45
        Brightness = 0.88
        Contrast = 1.22
    },
    @{
        Id = 'lab_butcher'
        Tint = [System.Drawing.Color]::FromArgb(138, 62, 48)
        Strength = 0.47
        Brightness = 0.90
        Contrast = 1.24
    }
)

$actions = @('IDLE', 'MOVE', 'ATTACK', 'DEATH')

foreach ($variant in $variants) {
    $variantRoot = Join-Path $enemyRoot $variant.Id
    Assert-InEnemyRoot $variantRoot

    foreach ($action in $actions) {
        $sourceActionDir = Join-Path $sourceRoot $action
        $targetActionDir = Join-Path $variantRoot $action
        Assert-InEnemyRoot $targetActionDir
        [System.IO.Directory]::CreateDirectory($targetActionDir) | Out-Null
        Get-ChildItem -LiteralPath $targetActionDir -Filter '*.png' -File | ForEach-Object {
            Assert-InEnemyRoot $_.FullName
            Remove-Item -LiteralPath $_.FullName
        }

        $files = Get-ChildItem -LiteralPath $sourceActionDir -Filter '*.png' -File | Sort-Object Name
        $index = 1
        foreach ($file in $files) {
            $sourceBitmap = [System.Drawing.Bitmap]::new($file.FullName)
            try {
                $targetBitmap = New-VariantBitmap $sourceBitmap $variant.Tint $variant.Strength $variant.Brightness $variant.Contrast
                try {
                    $graphics = [System.Drawing.Graphics]::FromImage($targetBitmap)
                    try {
                        Draw-VariantOverlay $graphics $variant.Id $action $targetBitmap.Width $targetBitmap.Height
                    }
                    finally {
                        $graphics.Dispose()
                    }

                    $targetName = '{0}_sheet_doom_{1}{2}.png' -f $variant.Id, $action, $index
                    $targetPath = Join-Path $targetActionDir $targetName
                    Assert-InEnemyRoot $targetPath
                    $targetBitmap.Save($targetPath, [System.Drawing.Imaging.ImageFormat]::Png)
                    $index++
                }
                finally {
                    $targetBitmap.Dispose()
                }
            }
            finally {
                $sourceBitmap.Dispose()
            }
        }
    }
}

Write-Host "Generated zombie scientist variants under $enemyRoot"
