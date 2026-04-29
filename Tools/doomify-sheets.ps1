param(
    [string]$ImagesRoot = (Join-Path $PSScriptRoot '..\Game\Images')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

function New-ArgbColor {
    param(
        [int]$R,
        [int]$G,
        [int]$B,
        [int]$A = 255
    )

    return [System.Drawing.Color]::FromArgb($A, $R, $G, $B)
}

function Clamp-Byte {
    param([double]$Value)

    return [Math]::Max(0, [Math]::Min(255, [int][Math]::Round($Value)))
}

function Blend-Color {
    param(
        [System.Drawing.Color]$Base,
        [System.Drawing.Color]$Overlay,
        [double]$Amount
    )

    $t = [Math]::Max(0.0, [Math]::Min(1.0, $Amount))

    return (New-ArgbColor `
        -R (Clamp-Byte (($Base.R * (1.0 - $t)) + ($Overlay.R * $t))) `
        -G (Clamp-Byte (($Base.G * (1.0 - $t)) + ($Overlay.G * $t))) `
        -B (Clamp-Byte (($Base.B * (1.0 - $t)) + ($Overlay.B * $t))) `
        -A $Base.A)
}

function Multiply-Color {
    param(
        [System.Drawing.Color]$Color,
        [double]$Factor
    )

    return (New-ArgbColor `
        -R (Clamp-Byte ($Color.R * $Factor)) `
        -G (Clamp-Byte ($Color.G * $Factor)) `
        -B (Clamp-Byte ($Color.B * $Factor)) `
        -A $Color.A)
}

function Get-Noise01 {
    param(
        [int]$X,
        [int]$Y,
        [int]$Seed
    )

    $value = ($X * 73856093) -bxor ($Y * 19349663) -bxor ($Seed * 83492791)
    $value = $value -band 0x7fffffff
    return (($value % 997) / 996.0)
}

function Get-Luma {
    param([System.Drawing.Color]$Color)

    return ((0.299 * $Color.R) + (0.587 * $Color.G) + (0.114 * $Color.B)) / 255.0
}

function Get-Saturation {
    param([System.Drawing.Color]$Color)

    $max = [Math]::Max($Color.R, [Math]::Max($Color.G, $Color.B))
    $min = [Math]::Min($Color.R, [Math]::Min($Color.G, $Color.B))
    if ($max -eq 0) {
        return 0.0
    }

    return ($max - $min) / [double]$max
}

function Get-Theme {
    param([string]$Name)

    switch -Regex ($Name.ToLowerInvariant()) {
        'slime_imp' {
            return @{
                Id             = 'slime_imp'
                Outline        = (New-ArgbColor 15 8 5)
                FleshDark      = (New-ArgbColor 74 35 14)
                FleshMid       = (New-ArgbColor 124 67 22)
                FleshLight     = (New-ArgbColor 174 109 47)
                MetalDark      = (New-ArgbColor 30 41 17)
                MetalMid       = (New-ArgbColor 72 95 31)
                MetalLight     = (New-ArgbColor 119 154 58)
                NeutralLight   = (New-ArgbColor 150 166 92)
                Accent         = (New-ArgbColor 74 244 67)
                Energy         = (New-ArgbColor 255 140 30)
                Blood          = (New-ArgbColor 134 22 14)
                Eye            = (New-ArgbColor 255 112 26)
                Horns          = $true
                Spectral       = $false
            }
        }
        'blind_pinky' {
            return @{
                Id             = 'blind_pinky'
                Outline        = (New-ArgbColor 17 6 6)
                FleshDark      = (New-ArgbColor 81 28 28)
                FleshMid       = (New-ArgbColor 150 71 69)
                FleshLight     = (New-ArgbColor 205 135 127)
                MetalDark      = (New-ArgbColor 42 20 18)
                MetalMid       = (New-ArgbColor 88 43 36)
                MetalLight     = (New-ArgbColor 138 76 58)
                NeutralLight   = (New-ArgbColor 224 191 179)
                Accent         = (New-ArgbColor 245 197 139)
                Energy         = (New-ArgbColor 255 129 47)
                Blood          = (New-ArgbColor 145 13 19)
                Eye            = (New-ArgbColor 255 57 37)
                Horns          = $false
                Spectral       = $false
            }
        }
        'beam_revenant' {
            return @{
                Id             = 'beam_revenant'
                Outline        = (New-ArgbColor 11 11 12)
                FleshDark      = (New-ArgbColor 86 75 59)
                FleshMid       = (New-ArgbColor 166 149 114)
                FleshLight     = (New-ArgbColor 223 212 182)
                MetalDark      = (New-ArgbColor 34 36 43)
                MetalMid       = (New-ArgbColor 86 91 101)
                MetalLight     = (New-ArgbColor 150 156 170)
                NeutralLight   = (New-ArgbColor 238 225 195)
                Accent         = (New-ArgbColor 255 124 23)
                Energy         = (New-ArgbColor 255 182 42)
                Blood          = (New-ArgbColor 130 41 25)
                Eye            = (New-ArgbColor 255 116 18)
                Horns          = $false
                Spectral       = $false
            }
        }
        'blood_ghost' {
            return @{
                Id             = 'blood_ghost'
                Outline        = (New-ArgbColor 8 4 7)
                FleshDark      = (New-ArgbColor 43 9 17)
                FleshMid       = (New-ArgbColor 91 19 35)
                FleshLight     = (New-ArgbColor 157 40 51)
                MetalDark      = (New-ArgbColor 22 11 18)
                MetalMid       = (New-ArgbColor 54 18 29)
                MetalLight     = (New-ArgbColor 96 42 54)
                NeutralLight   = (New-ArgbColor 189 94 98)
                Accent         = (New-ArgbColor 255 79 55)
                Energy         = (New-ArgbColor 255 169 58)
                Blood          = (New-ArgbColor 157 16 32)
                Eye            = (New-ArgbColor 255 83 58)
                Horns          = $false
                Spectral       = $true
            }
        }
        'hellion' {
            return @{
                Id             = 'hellion'
                Outline        = (New-ArgbColor 12 9 8)
                FleshDark      = (New-ArgbColor 59 27 20)
                FleshMid       = (New-ArgbColor 110 50 31)
                FleshLight     = (New-ArgbColor 170 92 55)
                MetalDark      = (New-ArgbColor 30 28 30)
                MetalMid       = (New-ArgbColor 71 66 71)
                MetalLight     = (New-ArgbColor 118 112 115)
                NeutralLight   = (New-ArgbColor 196 173 152)
                Accent         = (New-ArgbColor 255 99 34)
                Energy         = (New-ArgbColor 255 194 43)
                Blood          = (New-ArgbColor 139 20 19)
                Eye            = (New-ArgbColor 255 143 27)
                Horns          = $true
                Spectral       = $false
            }
        }
        'zombie_scientist' {
            return @{
                Id             = 'zombie_scientist'
                Outline        = (New-ArgbColor 15 16 12)
                FleshDark      = (New-ArgbColor 73 86 45)
                FleshMid       = (New-ArgbColor 118 134 72)
                FleshLight     = (New-ArgbColor 193 198 138)
                MetalDark      = (New-ArgbColor 50 52 56)
                MetalMid       = (New-ArgbColor 102 104 110)
                MetalLight     = (New-ArgbColor 168 170 176)
                NeutralLight   = (New-ArgbColor 225 223 212)
                Accent         = (New-ArgbColor 129 207 88)
                Energy         = (New-ArgbColor 255 110 47)
                Blood          = (New-ArgbColor 143 18 19)
                Eye            = (New-ArgbColor 255 70 44)
                Horns          = $false
                Spectral       = $false
                LabCoat        = $true
            }
        }
        'arachnocortex' {
            return @{
                Id             = 'arachnocortex'
                Outline        = (New-ArgbColor 9 10 13)
                FleshDark      = (New-ArgbColor 71 45 27)
                FleshMid       = (New-ArgbColor 118 75 39)
                FleshLight     = (New-ArgbColor 177 118 64)
                MetalDark      = (New-ArgbColor 33 37 44)
                MetalMid       = (New-ArgbColor 81 90 106)
                MetalLight     = (New-ArgbColor 149 160 181)
                NeutralLight   = (New-ArgbColor 220 222 231)
                Accent         = (New-ArgbColor 255 127 39)
                Energy         = (New-ArgbColor 255 193 48)
                Blood          = (New-ArgbColor 121 29 18)
                Eye            = (New-ArgbColor 255 109 42)
                Horns          = $false
                Spectral       = $false
            }
        }
        'agaures' {
            return @{
                Id             = 'agaures'
                Outline        = (New-ArgbColor 14 8 8)
                FleshDark      = (New-ArgbColor 68 23 22)
                FleshMid       = (New-ArgbColor 120 46 39)
                FleshLight     = (New-ArgbColor 188 88 61)
                MetalDark      = (New-ArgbColor 48 38 17)
                MetalMid       = (New-ArgbColor 108 84 33)
                MetalLight     = (New-ArgbColor 183 145 69)
                NeutralLight   = (New-ArgbColor 224 197 118)
                Accent         = (New-ArgbColor 105 233 85)
                Energy         = (New-ArgbColor 255 140 34)
                Blood          = (New-ArgbColor 145 17 19)
                Eye            = (New-ArgbColor 255 87 35)
                Horns          = $true
                Spectral       = $false
            }
        }
        'azazel' {
            return @{
                Id             = 'azazel'
                Outline        = (New-ArgbColor 12 9 6)
                FleshDark      = (New-ArgbColor 49 44 13)
                FleshMid       = (New-ArgbColor 93 84 25)
                FleshLight     = (New-ArgbColor 155 138 49)
                MetalDark      = (New-ArgbColor 29 36 20)
                MetalMid       = (New-ArgbColor 60 82 33)
                MetalLight     = (New-ArgbColor 109 150 63)
                NeutralLight   = (New-ArgbColor 187 213 121)
                Accent         = (New-ArgbColor 255 132 35)
                Energy         = (New-ArgbColor 255 201 55)
                Blood          = (New-ArgbColor 137 19 16)
                Eye            = (New-ArgbColor 255 108 41)
                Horns          = $true
                Spectral       = $false
            }
        }
        'behemoth' {
            return @{
                Id             = 'behemoth'
                Outline        = (New-ArgbColor 11 10 12)
                FleshDark      = (New-ArgbColor 70 43 26)
                FleshMid       = (New-ArgbColor 124 77 43)
                FleshLight     = (New-ArgbColor 180 120 70)
                MetalDark      = (New-ArgbColor 31 35 41)
                MetalMid       = (New-ArgbColor 78 86 99)
                MetalLight     = (New-ArgbColor 145 154 171)
                NeutralLight   = (New-ArgbColor 214 220 228)
                Accent         = (New-ArgbColor 255 129 33)
                Energy         = (New-ArgbColor 255 199 61)
                Blood          = (New-ArgbColor 130 31 20)
                Eye            = (New-ArgbColor 255 112 44)
                Horns          = $false
                Spectral       = $false
            }
        }
        default {
            return @{
                Id             = 'default'
                Outline        = (New-ArgbColor 12 10 10)
                FleshDark      = (New-ArgbColor 67 35 25)
                FleshMid       = (New-ArgbColor 118 71 45)
                FleshLight     = (New-ArgbColor 185 121 86)
                MetalDark      = (New-ArgbColor 32 35 40)
                MetalMid       = (New-ArgbColor 83 89 96)
                MetalLight     = (New-ArgbColor 147 154 165)
                NeutralLight   = (New-ArgbColor 216 212 202)
                Accent         = (New-ArgbColor 114 214 76)
                Energy         = (New-ArgbColor 255 146 41)
                Blood          = (New-ArgbColor 137 20 18)
                Eye            = (New-ArgbColor 255 88 38)
                Horns          = $false
                Spectral       = $false
            }
        }
    }
}

function Get-FrameBounds {
    param(
        [System.Drawing.Bitmap]$Bitmap,
        [System.Drawing.Rectangle]$Rect
    )

    $minX = $Rect.Right
    $minY = $Rect.Bottom
    $maxX = -1
    $maxY = -1

    for ($y = $Rect.Top; $y -lt $Rect.Bottom; $y++) {
        for ($x = $Rect.Left; $x -lt $Rect.Right; $x++) {
            if ($Bitmap.GetPixel($x, $y).A -gt 0) {
                if ($x -lt $minX) { $minX = $x }
                if ($y -lt $minY) { $minY = $y }
                if ($x -gt $maxX) { $maxX = $x }
                if ($y -gt $maxY) { $maxY = $y }
            }
        }
    }

    if ($maxX -lt $minX -or $maxY -lt $minY) {
        return $null
    }

    return [System.Drawing.Rectangle]::FromLTRB($minX, $minY, $maxX + 1, $maxY + 1)
}

function Test-EdgePixel {
    param(
        [System.Drawing.Bitmap]$Bitmap,
        [System.Drawing.Rectangle]$FrameRect,
        [int]$X,
        [int]$Y
    )

    foreach ($offset in @(
        @{ X = -1; Y = 0 },
        @{ X = 1; Y = 0 },
        @{ X = 0; Y = -1 },
        @{ X = 0; Y = 1 }
    )) {
        $nx = $X + $offset.X
        $ny = $Y + $offset.Y

        if ($nx -lt $FrameRect.Left -or $nx -ge $FrameRect.Right -or $ny -lt $FrameRect.Top -or $ny -ge $FrameRect.Bottom) {
            return $true
        }

        if ($Bitmap.GetPixel($nx, $ny).A -eq 0) {
            return $true
        }
    }

    return $false
}

function Get-ProjectileColor {
    param(
        [System.Drawing.Rectangle]$Bounds,
        [hashtable]$Theme,
        [int]$X,
        [int]$Y
    )

    $cx = $Bounds.Left + ($Bounds.Width / 2.0)
    $cy = $Bounds.Top + ($Bounds.Height / 2.0)
    $rx = [Math]::Max(1.0, $Bounds.Width / 2.0)
    $ry = [Math]::Max(1.0, $Bounds.Height / 2.0)
    $dx = (($X + 0.5) - $cx) / $rx
    $dy = (($Y + 0.5) - $cy) / $ry
    $distance = [Math]::Sqrt(($dx * $dx) + ($dy * $dy))

    if ($distance -gt 1.05) {
        return $Theme.Outline
    }

    if ($distance -lt 0.28) {
        return $Theme.NeutralLight
    }

    if ($distance -lt 0.55) {
        return (Blend-Color $Theme.Energy $Theme.NeutralLight 0.35)
    }

    if ($distance -lt 0.82) {
        return (Blend-Color $Theme.Accent $Theme.Energy 0.60)
    }

    return (Blend-Color $Theme.Outline $Theme.Blood 0.32)
}

function Get-BaseDoomColor {
    param(
        [System.Drawing.Color]$SourceColor,
        [hashtable]$Theme,
        [double]$Nx,
        [double]$Ny,
        [double]$Noise,
        [bool]$IsEdge
    )

    if ($IsEdge) {
        return $Theme.Outline
    }

    $luma = Get-Luma $SourceColor
    $saturation = Get-Saturation $SourceColor
    $result = $Theme.MetalMid

    if ($Theme.Spectral) {
        if ($luma -gt 0.55) {
            $result = $Theme.FleshLight
        }
        elseif ($luma -gt 0.28) {
            $result = $Theme.FleshMid
        }
        else {
            $result = $Theme.FleshDark
        }
    }
    elseif (($Theme.ContainsKey('LabCoat')) -and $Theme.LabCoat -and $luma -gt 0.68 -and $saturation -lt 0.30) {
        $result = if ($Ny -gt 0.70) { $Theme.FleshDark } else { $Theme.NeutralLight }
    }
    elseif ($SourceColor.G -gt ($SourceColor.R + 16) -and $SourceColor.G -gt ($SourceColor.B + 8)) {
        $result = if ($luma -gt 0.62) { $Theme.MetalLight } else { $Theme.Accent }
    }
    elseif ($SourceColor.R -gt ($SourceColor.G + 34) -and $SourceColor.R -gt ($SourceColor.B + 34)) {
        $result = if ($luma -lt 0.33) { $Theme.Blood } else { (Blend-Color $Theme.Blood $Theme.Energy 0.16) }
    }
    elseif ($saturation -lt 0.18) {
        if ($luma -gt 0.66) {
            $result = $Theme.MetalLight
        }
        elseif ($luma -lt 0.30) {
            $result = $Theme.MetalDark
        }
        else {
            $result = $Theme.MetalMid
        }
    }
    elseif ($luma -gt 0.62) {
        $result = $Theme.FleshLight
    }
    elseif ($luma -lt 0.32) {
        $result = $Theme.FleshDark
    }
    else {
        $result = $Theme.FleshMid
    }

    $shade = 0.68 + ((1.0 - $Ny) * 0.23)
    $shade += (0.08 * (0.5 - [Math]::Abs($Nx - 0.5)))
    $shade += (($Noise - 0.5) * 0.20)

    if ($Nx -lt 0.33 -and $Ny -lt 0.45) {
        $shade += 0.10
    }

    $shade = [Math]::Max(0.42, [Math]::Min(1.24, $shade))
    $result = Multiply-Color $result $shade

    if ([Math]::Abs($Nx - 0.5) -lt 0.16 -and $Ny -gt 0.38 -and $Ny -lt 0.58) {
        $result = Blend-Color $result $Theme.Energy 0.20
    }

    if (($Nx -lt 0.22 -or $Nx -gt 0.78) -and $Ny -gt 0.28 -and $Ny -lt 0.62 -and $Noise -gt 0.73) {
        $result = Blend-Color $result $Theme.Accent 0.22
    }

    if ($Ny -gt 0.26 -and $Ny -lt 0.82 -and $Noise -gt 0.90) {
        $result = Blend-Color $result $Theme.Blood 0.28
    }

    return $result
}

function Apply-FrameOverlays {
    param(
        [System.Drawing.Bitmap]$Source,
        [System.Drawing.Bitmap]$Target,
        [System.Drawing.Rectangle]$FrameRect,
        [System.Drawing.Rectangle]$Bounds,
        [hashtable]$Theme
    )

    $cx = [int][Math]::Round($Bounds.Left + ($Bounds.Width / 2.0))
    $eyeY = [int][Math]::Round($Bounds.Top + ($Bounds.Height * 0.20))
    $eyeSpread = [Math]::Max(1, [int][Math]::Round($Bounds.Width * 0.11))

    foreach ($eyeX in @(( $cx - $eyeSpread ), ( $cx + $eyeSpread ))) {
        if ($eyeX -ge $FrameRect.Left -and $eyeX -lt $FrameRect.Right -and $eyeY -ge $FrameRect.Top -and $eyeY -lt $FrameRect.Bottom) {
            if ($Source.GetPixel($eyeX, $eyeY).A -gt 0) {
                $Target.SetPixel($eyeX, $eyeY, (New-ArgbColor -R $Theme.Eye.R -G $Theme.Eye.G -B $Theme.Eye.B -A $Source.GetPixel($eyeX, $eyeY).A))
            }
        }
    }

    if ($Theme.Horns) {
        foreach ($offset in @(-2, -1, 1, 2)) {
            $hornX = $cx + $offset
            $hornY = $Bounds.Top
            if ($hornX -ge $FrameRect.Left -and $hornX -lt $FrameRect.Right) {
                if ($Source.GetPixel($hornX, $hornY).A -gt 0) {
                    $Target.SetPixel($hornX, $hornY, (New-ArgbColor -R $Theme.Accent.R -G $Theme.Accent.G -B $Theme.Accent.B -A $Source.GetPixel($hornX, $hornY).A))
                }
            }
        }
    }

    $coreX = $cx
    $coreY = [int][Math]::Round($Bounds.Top + ($Bounds.Height * 0.48))
    if ($coreX -ge $FrameRect.Left -and $coreX -lt $FrameRect.Right -and $coreY -ge $FrameRect.Top -and $coreY -lt $FrameRect.Bottom) {
        if ($Source.GetPixel($coreX, $coreY).A -gt 0) {
            $Target.SetPixel($coreX, $coreY, (New-ArgbColor -R $Theme.Energy.R -G $Theme.Energy.G -B $Theme.Energy.B -A $Source.GetPixel($coreX, $coreY).A))
        }
    }
}

function Invoke-DoomifySheet {
    param([string]$Path)

    $fileName = [System.IO.Path]::GetFileNameWithoutExtension($Path)
    $directory = Split-Path -Parent $Path
    $theme = Get-Theme $fileName
    $outputPath = Join-Path $directory ($fileName + '_doom.png')

    $source = [System.Drawing.Bitmap]::new($Path)
    $target = [System.Drawing.Bitmap]::new($source.Width, $source.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

    try {
        $frameWidth = [int]($source.Width / 6)
        $frameHeight = [int]($source.Height / 5)
        $seed = [Math]::Abs($fileName.GetHashCode())

        for ($row = 0; $row -lt 5; $row++) {
            for ($col = 0; $col -lt 6; $col++) {
                $frameRect = [System.Drawing.Rectangle]::new($col * $frameWidth, $row * $frameHeight, $frameWidth, $frameHeight)
                $bounds = Get-FrameBounds -Bitmap $source -Rect $frameRect
                if ($null -eq $bounds) {
                    continue
                }

                $isProjectile = ($bounds.Width -le 34 -and $bounds.Height -le 34)

                for ($y = $frameRect.Top; $y -lt $frameRect.Bottom; $y++) {
                    for ($x = $frameRect.Left; $x -lt $frameRect.Right; $x++) {
                        $src = $source.GetPixel($x, $y)
                        if ($src.A -eq 0) {
                            continue
                        }

                        if ($isProjectile) {
                            $color = Get-ProjectileColor -Bounds $bounds -Theme $theme -X $x -Y $y
                        }
                        else {
                            $nx = if ($bounds.Width -le 1) { 0.5 } else { ($x - $bounds.Left) / [double]($bounds.Width - 1) }
                            $ny = if ($bounds.Height -le 1) { 0.5 } else { ($y - $bounds.Top) / [double]($bounds.Height - 1) }
                            $noise = Get-Noise01 -X $x -Y $y -Seed $seed
                            $edge = Test-EdgePixel -Bitmap $source -FrameRect $frameRect -X $x -Y $y
                            $color = Get-BaseDoomColor -SourceColor $src -Theme $theme -Nx $nx -Ny $ny -Noise $noise -IsEdge $edge
                        }

                        $target.SetPixel($x, $y, (New-ArgbColor -R $color.R -G $color.G -B $color.B -A $src.A))
                    }
                }

                if (-not $isProjectile) {
                    Apply-FrameOverlays -Source $source -Target $target -FrameRect $frameRect -Bounds $bounds -Theme $theme
                }
            }
        }

        if (Test-Path -LiteralPath $outputPath) {
            Remove-Item -LiteralPath $outputPath -Force
        }

        $target.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)
        return $outputPath
    }
    finally {
        $target.Dispose()
        $source.Dispose()
    }
}

$targets = Get-ChildItem -LiteralPath $ImagesRoot -Recurse -File -Filter '*_sheet.png' |
    Sort-Object FullName

if ($targets.Count -eq 0) {
    throw "No sprite sheets were found under '$ImagesRoot'."
}

$generated = foreach ($target in $targets) {
    Invoke-DoomifySheet -Path $target.FullName
}

$generated
