param(
    [string]$ProductVersion,
    [string]$Configuration = "Release",
    [string]$InnoCompiler
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $repoRoot "src\Musait\Musait.csproj"
$dist = Join-Path $repoRoot "dist"
$stage = Join-Path $dist "inno-stage"
$assets = Join-Path $repoRoot "assets"
$logoSvg = Join-Path $assets "Musait.svg"
$logoPng = Join-Path $assets "Musait.png"
$logo16Png = Join-Path $assets "Musait-16x16.png"
$logo32Png = Join-Path $assets "Musait-32x32.png"
$iconIco = Join-Path $assets "Musait.ico"
$iss = Join-Path $repoRoot "installer\Musait.iss"
$net48 = Join-Path $repoRoot "src\Musait\bin\$Configuration\net48"
$net8 = Join-Path $repoRoot "src\Musait\bin\$Configuration\net8.0-windows"
$net10 = Join-Path $repoRoot "src\Musait\bin\$Configuration\net10.0-windows"

if ([string]::IsNullOrWhiteSpace($ProductVersion)) {
    $tag = (git -C $repoRoot describe --tags --exact-match 2>$null)
    if ([string]::IsNullOrWhiteSpace($tag)) {
        throw "ProductVersion is required unless the current commit is tagged vX.Y.Z."
    }

    $ProductVersion = $tag.TrimStart("v")
}

function Resolve-InnoCompiler {
    param([string]$ExplicitPath)

    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        if (-not (Test-Path $ExplicitPath)) {
            throw "Inno Setup compiler was not found at '$ExplicitPath'."
        }

        return (Resolve-Path $ExplicitPath).Path
    }

    $command = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $candidateRoots = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6",
        "${env:ProgramFiles}\Inno Setup 6",
        "${env:ProgramFiles(x86)}\Inno Setup 7",
        "${env:ProgramFiles}\Inno Setup 7"
    )

    foreach ($root in $candidateRoots) {
        $candidate = Join-Path $root "ISCC.exe"
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    throw "Inno Setup Compiler 6.7+ was not found. Install Inno Setup and rerun this script, or pass -InnoCompiler C:\Path\To\ISCC.exe."
}

function Remove-DirectoryInside {
    param(
        [string]$Path,
        [string]$AllowedRoot
    )

    if (-not (Test-Path $Path)) {
        return
    }

    $resolvedPath = (Resolve-Path $Path).Path
    $resolvedRoot = (Resolve-Path $AllowedRoot).Path
    if (-not $resolvedPath.StartsWith($resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove '$resolvedPath' because it is outside '$resolvedRoot'."
    }

    Remove-Item -LiteralPath $resolvedPath -Recurse -Force
}

function Convert-HexColor {
    param([string]$Hex)

    if ([string]::IsNullOrWhiteSpace($Hex) -or $Hex -eq "none") {
        return [System.Drawing.Color]::Transparent
    }

    return [System.Drawing.ColorTranslator]::FromHtml($Hex)
}

function Get-SvgNumber {
    param(
        [System.Xml.XmlElement]$Element,
        [string]$Name
    )

    return [double]::Parse($Element.GetAttribute($Name), [System.Globalization.CultureInfo]::InvariantCulture)
}

function New-LogoPng {
    param(
        [string]$SourceSvg,
        [string]$TargetPng,
        [int]$Size
    )

    if (-not (Test-Path $SourceSvg)) {
        throw "Logo source was not found at '$SourceSvg'."
    }

    [xml]$svg = Get-Content -LiteralPath $SourceSvg -Raw

    Add-Type -AssemblyName System.Drawing
    $bitmap = New-Object System.Drawing.Bitmap $Size, $Size
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.ScaleTransform($Size / 32.0, $Size / 32.0)

        $nodes = $svg.DocumentElement.SelectNodes("//*[local-name()='line' or local-name()='path' or local-name()='circle']")
        foreach ($node in $nodes) {
            $element = [System.Xml.XmlElement]$node
            switch ($element.LocalName) {
                "line" {
                    $color = Convert-HexColor $element.GetAttribute("stroke")
                    $width = Get-SvgNumber $element "stroke-width"
                    $pen = New-Object System.Drawing.Pen $color, $width
                    try {
                        $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
                        $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
                        $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
                        $graphics.DrawLine(
                            $pen,
                            (Get-SvgNumber $element "x1"),
                            (Get-SvgNumber $element "y1"),
                            (Get-SvgNumber $element "x2"),
                            (Get-SvgNumber $element "y2"))
                    }
                    finally {
                        $pen.Dispose()
                    }
                }
                "path" {
                    $color = Convert-HexColor $element.GetAttribute("stroke")
                    $width = Get-SvgNumber $element "stroke-width"
                    $numbers = [regex]::Matches($element.GetAttribute("d"), "-?\d+(?:\.\d+)?") |
                        ForEach-Object { [double]::Parse($_.Value, [System.Globalization.CultureInfo]::InvariantCulture) }
                    $points = for ($i = 0; $i -lt $numbers.Count; $i += 2) {
                        New-Object System.Drawing.PointF ([single]$numbers[$i]), ([single]$numbers[$i + 1])
                    }
                    $pen = New-Object System.Drawing.Pen $color, $width
                    try {
                        $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
                        $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
                        $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
                        $graphics.DrawLines($pen, [System.Drawing.PointF[]]$points)
                    }
                    finally {
                        $pen.Dispose()
                    }
                }
                "circle" {
                    $color = Convert-HexColor $element.GetAttribute("fill")
                    $cx = Get-SvgNumber $element "cx"
                    $cy = Get-SvgNumber $element "cy"
                    $r = Get-SvgNumber $element "r"
                    $brush = New-Object System.Drawing.SolidBrush $color
                    try {
                        $graphics.FillEllipse($brush, $cx - $r, $cy - $r, $r * 2, $r * 2)
                    }
                    finally {
                        $brush.Dispose()
                    }
                }
            }
        }

        $bitmap.Save($TargetPng, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

function New-InstallerIcon {
    param(
        [string]$SourcePng,
        [string]$TargetIco
    )

    if (-not (Test-Path $SourcePng)) {
        throw "Installer icon source was not found at '$SourcePng'."
    }

    Add-Type -AssemblyName System.Drawing
    $bitmap = [System.Drawing.Bitmap]::FromFile($SourcePng)
    if ($bitmap.Width -ne 256 -or $bitmap.Height -ne 256) {
        $bitmap.Dispose()
        throw "Installer icon source must be a 256x256 PNG."
    }

    $width = $bitmap.Width
    $height = $bitmap.Height
    $xorSize = $width * $height * 4
    $andStride = [int]([Math]::Ceiling($width / 32.0) * 4)
    $andSize = $andStride * $height
    $imageSize = 40 + $xorSize + $andSize

    $stream = [System.IO.File]::Create($TargetIco)
    $writer = New-Object System.IO.BinaryWriter $stream
    try {
        $writer.Write([UInt16]0) # reserved
        $writer.Write([UInt16]1) # icon
        $writer.Write([UInt16]1) # image count
        $writer.Write([Byte]0)   # 256px width
        $writer.Write([Byte]0)   # 256px height
        $writer.Write([Byte]0)   # color count
        $writer.Write([Byte]0)   # reserved
        $writer.Write([UInt16]1) # color planes
        $writer.Write([UInt16]32)
        $writer.Write([UInt32]$imageSize)
        $writer.Write([UInt32]22)

        $writer.Write([UInt32]40)          # BITMAPINFOHEADER size
        $writer.Write([Int32]$width)
        $writer.Write([Int32]($height * 2)) # XOR bitmap + AND mask
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]32)
        $writer.Write([UInt32]0)
        $writer.Write([UInt32]$xorSize)
        $writer.Write([Int32]0)
        $writer.Write([Int32]0)
        $writer.Write([UInt32]0)
        $writer.Write([UInt32]0)

        for ($y = $height - 1; $y -ge 0; $y--) {
            for ($x = 0; $x -lt $width; $x++) {
                $color = $bitmap.GetPixel($x, $y)
                $writer.Write([Byte]$color.B)
                $writer.Write([Byte]$color.G)
                $writer.Write([Byte]$color.R)
                $writer.Write([Byte]$color.A)
            }
        }

        $writer.Write((New-Object byte[] $andSize))
    }
    finally {
        $writer.Dispose()
        $stream.Dispose()
        $bitmap.Dispose()
    }
}

function New-LogoAssets {
    New-LogoPng -SourceSvg $logoSvg -TargetPng $logo16Png -Size 16
    New-LogoPng -SourceSvg $logoSvg -TargetPng $logo32Png -Size 32
    New-LogoPng -SourceSvg $logoSvg -TargetPng $logoPng -Size 256
    New-InstallerIcon -SourcePng $logoPng -TargetIco $iconIco
}

$iscc = Resolve-InnoCompiler $InnoCompiler
$setup = Join-Path $dist "Musait-Setup.exe"

New-Item -ItemType Directory -Force -Path $dist, $assets | Out-Null
New-LogoAssets

Push-Location $repoRoot
try {
    Remove-DirectoryInside `
        -Path (Join-Path $repoRoot "src\Musait\bin\$Configuration") `
        -AllowedRoot (Join-Path $repoRoot "src\Musait\bin")

    dotnet build $project -c $Configuration -p:Version=$ProductVersion -p:AssemblyVersion="$ProductVersion.0" -p:FileVersion="$ProductVersion.0" -p:InformationalVersion=$ProductVersion
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    if ((Test-Path $stage) -and ((Resolve-Path $stage).Path.StartsWith((Resolve-Path $dist).Path))) {
        Remove-Item -LiteralPath $stage -Recurse -Force
    }

    foreach ($version in 2022, 2023, 2024, 2025, 2026, 2027) {
        $payloadSource = if ($version -le 2024) { $net48 } elseif ($version -le 2026) { $net8 } else { $net10 }
        $versionStage = Join-Path $stage $version
        $payloadTarget = Join-Path $versionStage "Musait"

        New-Item -ItemType Directory -Force -Path $payloadTarget | Out-Null
        Copy-Item -Path (Join-Path $payloadSource "*") -Destination $payloadTarget -Recurse -Force
        Get-ChildItem -LiteralPath $payloadTarget -Recurse -Force -Filter "*.pdb" |
            Remove-Item -Force
    }

    & $iscc /Qp "/DAppVersion=$ProductVersion" "/DStageRoot=$stage" "/DSetupIcon=$iconIco" $iss
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Get-FileHash -Algorithm SHA256 -Path $setup |
        ForEach-Object { "$($_.Hash)  $(Split-Path -Leaf $_.Path)" } |
        Set-Content -Path (Join-Path $dist "Musait-Setup.sha256") -Encoding ascii
}
finally {
    Pop-Location
}

Write-Host "Built $setup"
