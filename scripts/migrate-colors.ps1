# Migrates the brand color #18a088 (teal) + its hue-family tints to #1d273b (navy).
# Uses HSL: hue is shifted from source to target, saturation is scaled by the
# same ratio target/source, and lightness is rescaled so the source-color maps
# exactly to the target while white stays white (linear stretch from source.L
# to 100, remapped to target.L..100).

param([switch]$DryRun)

function ConvertFrom-HexColor {
    param([string]$hex)
    $hex = $hex.TrimStart('#')
    $arr = New-Object 'object[]' 3
    $arr[0] = [Convert]::ToInt32($hex.Substring(0,2), 16)
    $arr[1] = [Convert]::ToInt32($hex.Substring(2,2), 16)
    $arr[2] = [Convert]::ToInt32($hex.Substring(4,2), 16)
    return ,$arr
}

function ConvertTo-Hsl {
    param([int]$r, [int]$g, [int]$b)
    $rf = $r / 255.0; $gf = $g / 255.0; $bf = $b / 255.0
    $max = [Math]::Max([Math]::Max($rf, $gf), $bf)
    $min = [Math]::Min([Math]::Min($rf, $gf), $bf)
    $l = ($max + $min) / 2.0
    $arr = New-Object 'object[]' 3
    if ($max -eq $min) {
        $arr[0] = 0.0
        $arr[1] = 0.0
        $arr[2] = $l * 100
        return ,$arr
    }
    $d = $max - $min
    if ($l -gt 0.5) { $s = $d / (2.0 - $max - $min) } else { $s = $d / ($max + $min) }
    $h = 0.0
    if ($max -eq $rf) {
        $h = ($gf - $bf) / $d
        if ($gf -lt $bf) { $h += 6 }
    } elseif ($max -eq $gf) {
        $h = (($bf - $rf) / $d) + 2
    } else {
        $h = (($rf - $gf) / $d) + 4
    }
    $arr[0] = $h * 60
    $arr[1] = $s * 100
    $arr[2] = $l * 100
    return ,$arr
}

function _HueToRgb {
    param([double]$p, [double]$q, [double]$t)
    if ($t -lt 0) { $t += 1 }
    if ($t -gt 1) { $t -= 1 }
    if ($t -lt 1.0/6.0) { return $p + ($q - $p) * 6 * $t }
    if ($t -lt 1.0/2.0) { return $q }
    if ($t -lt 2.0/3.0) { return $p + ($q - $p) * (2.0/3.0 - $t) * 6 }
    return $p
}

function ConvertTo-HexColor {
    param([double]$h, [double]$s, [double]$l)
    $h = $h / 360.0; $s = $s / 100.0; $l = $l / 100.0
    if ($s -eq 0) { $r = $g = $b = $l }
    else {
        $q = if ($l -lt 0.5) { $l * (1 + $s) } else { $l + $s - $l * $s }
        $p = 2 * $l - $q
        $r = _HueToRgb $p $q ($h + 1.0/3.0)
        $g = _HueToRgb $p $q $h
        $b = _HueToRgb $p $q ($h - 1.0/3.0)
    }
    $rb = [int][Math]::Round($r * 255)
    $gb = [int][Math]::Round($g * 255)
    $bb = [int][Math]::Round($b * 255)
    return ('#{0:x2}{1:x2}{2:x2}' -f $rb, $gb, $bb)
}

# Source and target brand colors
$srcRgb = ConvertFrom-HexColor '#18a088'
$tgtRgb = ConvertFrom-HexColor '#1d273b'
$srcHsl = ConvertTo-Hsl $srcRgb[0] $srcRgb[1] $srcRgb[2]
$tgtHsl = ConvertTo-Hsl $tgtRgb[0] $tgtRgb[1] $tgtRgb[2]

$hueTolerance = 15  # ± degrees from source hue (170° ±15 = 155°..185°, captures teal family without bootstrap success/info)

function Transform-Color {
    param([string]$hex)
    $rgb = ConvertFrom-HexColor $hex
    $hsl = ConvertTo-Hsl $rgb[0] $rgb[1] $rgb[2]
    $h = $hsl[0]; $s = $hsl[1]; $l = $hsl[2]

    # circular hue distance
    $dh = [Math]::Abs($h - $srcHsl[0])
    if ($dh -gt 180) { $dh = 360 - $dh }
    if ($dh -gt $hueTolerance) { return $null }

    # skip near-grays (very low saturation, not really a brand color)
    if ($s -lt 5) { return $null }

    $newH = $tgtHsl[0]
    $newS = [Math]::Max(0.0, [Math]::Min(100.0, $s * ($tgtHsl[1] / $srcHsl[1])))

    # Lightness: linear remap [srcL..100] -> [tgtL..100], clamped at low end
    if ($l -le $srcHsl[2]) {
        $newL = $tgtHsl[2]
    } else {
        $t = ($l - $srcHsl[2]) / (100 - $srcHsl[2])
        $newL = $tgtHsl[2] + $t * (100 - $tgtHsl[2])
    }

    return ConvertTo-HexColor $newH $newS $newL
}

# Files to process
$root = 'd:\Work\programming\luxira\Crm_LotusBlue'
$files = @()
$files += Get-ChildItem -Path "$root\wwwroot\css" -Filter '*.css' -File
$files += Get-ChildItem -Path "$root\Views" -Filter '*.cshtml' -File -Recurse
$files += Get-ChildItem -Path "$root\Areas" -Filter '*.cshtml' -File -Recurse
$files += Get-ChildItem -Path "$root\Services" -Filter '*.cs' -File

# Collect unique hex colors and build mapping
$colorMap = @{}
$pattern = '#[0-9a-fA-F]{6}\b'
foreach ($f in $files) {
    $text = Get-Content -LiteralPath $f.FullName -Raw -ErrorAction SilentlyContinue
    if ($null -eq $text) { continue }
    foreach ($m in [regex]::Matches($text, $pattern)) {
        $hex = $m.Value.ToLower()
        if (-not $colorMap.ContainsKey($hex)) {
            $new = Transform-Color $hex
            if ($null -ne $new) { $colorMap[$hex] = $new }
        }
    }
}

Write-Host "===== Color mappings ====="
foreach ($k in ($colorMap.Keys | Sort-Object)) {
    Write-Host ("  {0} -> {1}" -f $k, $colorMap[$k])
}
Write-Host ""

if ($DryRun) {
    Write-Host "[DRY RUN] No files modified."
    return
}

# Apply mappings (case-insensitive replacement)
$changedFiles = 0
foreach ($f in $files) {
    $text = Get-Content -LiteralPath $f.FullName -Raw -ErrorAction SilentlyContinue
    if ($null -eq $text) { continue }
    $orig = $text
    foreach ($k in $colorMap.Keys) {
        $regex = [regex]::new([regex]::Escape($k), 'IgnoreCase')
        $text = $regex.Replace($text, $colorMap[$k])
    }
    if ($text -ne $orig) {
        [System.IO.File]::WriteAllText($f.FullName, $text)
        $changedFiles++
        Write-Host "  updated: $($f.FullName)"
    }
}
Write-Host ""
Write-Host "Done. $changedFiles file(s) updated."
