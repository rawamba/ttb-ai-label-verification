param(
    [string]$OutputDirectory = (
        Join-Path `
            $PSScriptRoot `
            "..\sample-data\labels\verification"
    )
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

$OutputDirectory =
    [System.IO.Path]::GetFullPath(
        $OutputDirectory)

New-Item `
    -ItemType Directory `
    -Force `
    $OutputDirectory |
    Out-Null

$width = 1600
$height = 2200

$requiredWarningBody =
    "(1) According to the Surgeon General, women should not drink " +
    "alcoholic beverages during pregnancy because of the risk of birth " +
    "defects. (2) Consumption of alcoholic beverages impairs your " +
    "ability to drive a car or operate machinery, and may cause health problems."

function New-LabelBitmap {
    param(
        [string]$BrandName = "OLD TOM DISTILLERY",

        [string]$ClassType =
            "KENTUCKY STRAIGHT BOURBON WHISKEY",

        [decimal]$AlcoholByVolume = 45,

        [decimal]$Proof = 90,

        [decimal]$NetContents = 750,

        [ValidateSet(
            "Exact",
            "Missing",
            "Modified"
        )]
        [string]$WarningMode = "Exact"
    )

    $bitmap =
        [System.Drawing.Bitmap]::new(
            $width,
            $height,
            [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)

    $graphics =
        [System.Drawing.Graphics]::FromImage(
            $bitmap)

    $blackBrush =
        [System.Drawing.SolidBrush]::new(
            [System.Drawing.Color]::FromArgb(
                28,
                28,
                28))

    $secondaryBrush =
        [System.Drawing.SolidBrush]::new(
            [System.Drawing.Color]::FromArgb(
                70,
                70,
                70))

    $borderPen =
        [System.Drawing.Pen]::new(
            [System.Drawing.Color]::FromArgb(
                65,
                65,
                65),
            5)

    $separatorPen =
        [System.Drawing.Pen]::new(
            [System.Drawing.Color]::FromArgb(
                120,
                120,
                120),
            2)

    $brandFont =
        [System.Drawing.Font]::new(
            "Arial",
            72,
            [System.Drawing.FontStyle]::Bold,
            [System.Drawing.GraphicsUnit]::Pixel)

    $classFont =
        [System.Drawing.Font]::new(
            "Arial",
            43,
            [System.Drawing.FontStyle]::Bold,
            [System.Drawing.GraphicsUnit]::Pixel)

    $fieldFont =
        [System.Drawing.Font]::new(
            "Arial",
            42,
            [System.Drawing.FontStyle]::Regular,
            [System.Drawing.GraphicsUnit]::Pixel)

    $smallFont =
        [System.Drawing.Font]::new(
            "Arial",
            30,
            [System.Drawing.FontStyle]::Regular,
            [System.Drawing.GraphicsUnit]::Pixel)

    $warningHeadingFont =
        [System.Drawing.Font]::new(
            "Arial",
            34,
            [System.Drawing.FontStyle]::Bold,
            [System.Drawing.GraphicsUnit]::Pixel)

    $warningBodyFont =
        [System.Drawing.Font]::new(
            "Arial",
            31,
            [System.Drawing.FontStyle]::Regular,
            [System.Drawing.GraphicsUnit]::Pixel)

    $centerFormat =
        [System.Drawing.StringFormat]::new()

    $centerFormat.Alignment =
        [System.Drawing.StringAlignment]::Center

    try {
        $graphics.Clear(
            [System.Drawing.Color]::FromArgb(
                250,
                248,
                242))

        $graphics.SmoothingMode =
            [System.Drawing.Drawing2D.SmoothingMode]::HighQuality

        $graphics.InterpolationMode =
            [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

        $graphics.TextRenderingHint =
            [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

        # Outer label border.
        $graphics.DrawRectangle(
            $borderPen,
            60,
            60,
            $width - 120,
            $height - 120)

        # Brand name.
        $graphics.DrawString(
            $BrandName,
            $brandFont,
            $blackBrush,
            [System.Drawing.RectangleF]::new(
                120,
                135,
                1360,
                110),
            $centerFormat)

        # Class/type.
        $graphics.DrawString(
            $ClassType,
            $classFont,
            $blackBrush,
            [System.Drawing.RectangleF]::new(
                120,
                280,
                1360,
                80),
            $centerFormat)

        $graphics.DrawLine(
            $separatorPen,
            180,
            400,
            1420,
            400)

        $abvText =
            $AlcoholByVolume.ToString(
                "0.#",
                [System.Globalization.CultureInfo]::InvariantCulture) +
            "% ALCOHOL BY VOLUME"

        $proofText =
            $Proof.ToString(
                "0.#",
                [System.Globalization.CultureInfo]::InvariantCulture) +
            " PROOF"

        $netContentsText =
            $NetContents.ToString(
                "0.#",
                [System.Globalization.CultureInfo]::InvariantCulture) +
            " mL"

        $graphics.DrawString(
            $abvText,
            $fieldFont,
            $blackBrush,
            210,
            505)

        $graphics.DrawString(
            $proofText,
            $fieldFont,
            $blackBrush,
            210,
            610)

        $graphics.DrawString(
            $netContentsText,
            $fieldFont,
            $blackBrush,
            210,
            715)

        $graphics.DrawString(
            "BOTTLED BY OLD TOM DISTILLERY",
            $smallFont,
            $secondaryBrush,
            210,
            850)

        $graphics.DrawString(
            "FRANKFORT, KENTUCKY",
            $smallFont,
            $secondaryBrush,
            210,
            900)

        $graphics.DrawLine(
            $separatorPen,
            180,
            1010,
            1420,
            1010)

        if ($WarningMode -ne "Missing") {
            $graphics.DrawString(
                "GOVERNMENT WARNING:",
                $warningHeadingFont,
                $blackBrush,
                180,
                1080)

            $warningBody =
                $requiredWarningBody

            if ($WarningMode -eq "Modified") {
                $warningBody =
                    $warningBody.Replace(
                        "may cause health problems.",
                        "may cause serious health problems.")
            }

            $graphics.DrawString(
                $warningBody,
                $warningBodyFont,
                $blackBrush,
                [System.Drawing.RectangleF]::new(
                    180,
                    1150,
                    1240,
                    680))
        }

        return $bitmap
    }
    finally {
        $centerFormat.Dispose()
        $warningBodyFont.Dispose()
        $warningHeadingFont.Dispose()
        $smallFont.Dispose()
        $fieldFont.Dispose()
        $classFont.Dispose()
        $brandFont.Dispose()
        $separatorPen.Dispose()
        $borderPen.Dispose()
        $secondaryBrush.Dispose()
        $blackBrush.Dispose()
        $graphics.Dispose()
    }
}

function Save-Png {
    param(
        [System.Drawing.Bitmap]$Bitmap,
        [string]$Path
    )

    $Bitmap.Save(
        $Path,
        [System.Drawing.Imaging.ImageFormat]::Png)
}

function Save-Jpeg {
    param(
        [System.Drawing.Bitmap]$Bitmap,
        [string]$Path,
        [long]$Quality = 55
    )

    $encoder =
        [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() |
        Where-Object {
            $_.MimeType -eq "image/jpeg"
        } |
        Select-Object -First 1

    $qualityEncoder =
        [System.Drawing.Imaging.Encoder]::Quality

    $parameters =
        [System.Drawing.Imaging.EncoderParameters]::new(1)

    $parameters.Param[0] =
        [System.Drawing.Imaging.EncoderParameter]::new(
            $qualityEncoder,
            $Quality)

    try {
        $Bitmap.Save(
            $Path,
            $encoder,
            $parameters)
    }
    finally {
        $parameters.Dispose()
    }
}

function New-RotatedBitmap {
    param(
        [System.Drawing.Bitmap]$Source,
        [single]$Degrees = 6
    )

    $target =
        [System.Drawing.Bitmap]::new(
            $Source.Width,
            $Source.Height,
            [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)

    $graphics =
        [System.Drawing.Graphics]::FromImage(
            $target)

    try {
        $graphics.Clear(
            [System.Drawing.Color]::White)

        $graphics.SmoothingMode =
            [System.Drawing.Drawing2D.SmoothingMode]::HighQuality

        $graphics.InterpolationMode =
            [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

        $graphics.TranslateTransform(
            $Source.Width / 2,
            $Source.Height / 2)

        $graphics.RotateTransform(
            $Degrees)

        $graphics.TranslateTransform(
            -$Source.Width / 2,
            -$Source.Height / 2)

        $graphics.DrawImage(
            $Source,
            0,
            0,
            $Source.Width,
            $Source.Height)

        return $target
    }
    finally {
        $graphics.Dispose()
    }
}

function New-DegradedBitmap {
    param(
        [System.Drawing.Bitmap]$Source
    )

    # Deliberately reduce resolution before scaling back up.
    # This produces softer character edges without making the
    # sample completely unreadable.
    $smallWidth =
        [int]($Source.Width * 0.42)

    $smallHeight =
        [int]($Source.Height * 0.42)

    $small =
        [System.Drawing.Bitmap]::new(
            $smallWidth,
            $smallHeight)

    $smallGraphics =
        [System.Drawing.Graphics]::FromImage(
            $small)

    try {
        $smallGraphics.InterpolationMode =
            [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBilinear

        $smallGraphics.DrawImage(
            $Source,
            0,
            0,
            $smallWidth,
            $smallHeight)
    }
    finally {
        $smallGraphics.Dispose()
    }

    $target =
        [System.Drawing.Bitmap]::new(
            $Source.Width,
            $Source.Height,
            [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)

    $graphics =
        [System.Drawing.Graphics]::FromImage(
            $target)

    $darkOverlay =
        [System.Drawing.SolidBrush]::new(
            [System.Drawing.Color]::FromArgb(
                40,
                0,
                0,
                0))

    $glareBrush =
        [System.Drawing.SolidBrush]::new(
            [System.Drawing.Color]::FromArgb(
                105,
                255,
                255,
                255))

    try {
        $graphics.InterpolationMode =
            [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBilinear

        $graphics.DrawImage(
            $small,
            0,
            0,
            $Source.Width,
            $Source.Height)

        # Simulate lower-light capture.
        $graphics.FillRectangle(
            $darkOverlay,
            0,
            0,
            $Source.Width,
            $Source.Height)

        # Simulate a modest glare region without hiding the entire label.
        $graphics.FillEllipse(
            $glareBrush,
            930,
            350,
            540,
            900)

        return $target
    }
    finally {
        $glareBrush.Dispose()
        $darkOverlay.Dispose()
        $graphics.Dispose()
        $small.Dispose()
    }
}

function New-SemanticFixture {
    param(
        [string]$FileName,

        [string]$BrandName =
            "OLD TOM DISTILLERY",

        [decimal]$AlcoholByVolume = 45,

        [decimal]$Proof = 90,

        [decimal]$NetContents = 750,

        [ValidateSet(
            "Exact",
            "Missing",
            "Modified"
        )]
        [string]$WarningMode = "Exact"
    )

    $bitmap =
        New-LabelBitmap `
            -BrandName $BrandName `
            -AlcoholByVolume $AlcoholByVolume `
            -Proof $Proof `
            -NetContents $NetContents `
            -WarningMode $WarningMode

    try {
        Save-Png `
            -Bitmap $bitmap `
            -Path (
                Join-Path `
                    $OutputDirectory `
                    $FileName
            )
    }
    finally {
        $bitmap.Dispose()
    }
}

Write-Host "Creating semantic fixtures..." -ForegroundColor Cyan

New-SemanticFixture `
    -FileName "compliant-label.png"

New-SemanticFixture `
    -FileName "brand-variation-label.png" `
    -BrandName "OLD TOM DISTILERY"

New-SemanticFixture `
    -FileName "incorrect-abv-label.png" `
    -AlcoholByVolume 46

New-SemanticFixture `
    -FileName "incorrect-proof-label.png" `
    -Proof 92

New-SemanticFixture `
    -FileName "incorrect-net-contents-label.png" `
    -NetContents 700

New-SemanticFixture `
    -FileName "missing-warning-label.png" `
    -WarningMode "Missing"

New-SemanticFixture `
    -FileName "modified-warning-label.png" `
    -WarningMode "Modified"

Write-Host "Creating image-quality fixtures..." -ForegroundColor Cyan

$baseline =
    New-LabelBitmap

try {
    $rotated =
        New-RotatedBitmap `
            -Source $baseline `
            -Degrees 6

    try {
        Save-Png `
            -Bitmap $rotated `
            -Path (
                Join-Path `
                    $OutputDirectory `
                    "rotated-label.png"
            )
    }
    finally {
        $rotated.Dispose()
    }

    $degraded =
        New-DegradedBitmap `
            -Source $baseline

    try {
        Save-Jpeg `
            -Bitmap $degraded `
            -Path (
                Join-Path `
                    $OutputDirectory `
                    "degraded-label.jpg"
            ) `
            -Quality 55
    }
    finally {
        $degraded.Dispose()
    }
}
finally {
    $baseline.Dispose()
}

$manifest = [ordered]@{
    schemaVersion = 1

    applicationId = "COLA-84729"

    applicationExpectedData = [ordered]@{
        brandName = "Old Tom Distillery"
        classType = "Kentucky Straight Bourbon Whiskey"
        alcoholByVolume = 45.0
        proof = 90
        netContents = [ordered]@{
            value = 750
            unit = "mL"
        }
    }

    notes = @(
        "Fixtures are synthetic and contain no production or applicant data.",
        "Semantic fixtures mutate one field at a time so verifier behavior can be isolated.",
        "Class/type is rendered on every label but is not yet part of the current automated verification aggregate.",
        "Rotated and degraded samples are OCR robustness fixtures; their exact field status is validated separately during integration testing."
    )

    samples = @(
        [ordered]@{
            file = "compliant-label.png"
            purpose = "Baseline label matching the mock application."
            intendedOverallStatus = "PASS"
            intendedChecks = [ordered]@{
                brand = "PASS"
                abv = "PASS"
                proof = "PASS"
                netContents = "PASS"
                governmentWarning = "PASS"
            }
        },

        [ordered]@{
            file = "brand-variation-label.png"
            purpose = "One-character brand variation intended to fall into the REVIEW similarity band."
            intendedOverallStatus = "REVIEW"
            intendedChecks = [ordered]@{
                brand = "REVIEW"
                abv = "PASS"
                proof = "PASS"
                netContents = "PASS"
                governmentWarning = "PASS"
            }
        },

        [ordered]@{
            file = "incorrect-abv-label.png"
            purpose = "ABV differs from the approved application."
            intendedOverallStatus = "FAIL"
            intendedChecks = [ordered]@{
                brand = "PASS"
                abv = "FAIL"
                proof = "PASS"
                netContents = "PASS"
                governmentWarning = "PASS"
            }
        },

        [ordered]@{
            file = "incorrect-proof-label.png"
            purpose = "Proof differs from the approved application."
            intendedOverallStatus = "FAIL"
            intendedChecks = [ordered]@{
                brand = "PASS"
                abv = "PASS"
                proof = "FAIL"
                netContents = "PASS"
                governmentWarning = "PASS"
            }
        },

        [ordered]@{
            file = "incorrect-net-contents-label.png"
            purpose = "Net contents differ from the approved application."
            intendedOverallStatus = "FAIL"
            intendedChecks = [ordered]@{
                brand = "PASS"
                abv = "PASS"
                proof = "PASS"
                netContents = "FAIL"
                governmentWarning = "PASS"
            }
        },

        [ordered]@{
            file = "missing-warning-label.png"
            purpose = "Government Warning is absent."
            intendedOverallStatus = "REVIEW"
            intendedChecks = [ordered]@{
                brand = "PASS"
                abv = "PASS"
                proof = "PASS"
                netContents = "PASS"
                governmentWarning = "REVIEW"
            }
        },

        [ordered]@{
            file = "modified-warning-label.png"
            purpose = "Government Warning contains a deliberate wording change."
            intendedOverallStatus = "FAIL"
            intendedChecks = [ordered]@{
                brand = "PASS"
                abv = "PASS"
                proof = "PASS"
                netContents = "PASS"
                governmentWarning = "FAIL"
            }
        },

        [ordered]@{
            file = "rotated-label.png"
            purpose = "Baseline compliant content rotated six degrees to exercise OCR robustness."
            intendedOverallStatus = "TO_BE_VALIDATED"
        },

        [ordered]@{
            file = "degraded-label.jpg"
            purpose = "Baseline compliant content with lower resolution, compression, lower light, and glare."
            intendedOverallStatus = "TO_BE_VALIDATED"
        }
    )
}

$manifestJson =
    $manifest |
    ConvertTo-Json -Depth 10

$utf8NoBom =
    New-Object System.Text.UTF8Encoding($false)

[System.IO.File]::WriteAllText(
    (Join-Path $OutputDirectory "manifest.json"),
    $manifestJson,
    $utf8NoBom)

$readmeLines = @(
    "# Representative Label Verification Dataset",
    "",
    "This directory contains synthetic fixtures for the TTB label-verification prototype.",
    "",
    "The fixtures are aligned with mock application `COLA-84729`:",
    "",
    "- Brand: Old Tom Distillery",
    "- Class/type: Kentucky Straight Bourbon Whiskey",
    "- ABV: 45%",
    "- Proof: 90",
    "- Net contents: 750 mL",
    "",
    "The Government Warning uses the exact regulatory text expected by the current verifier.",
    "",
    "Semantic fixtures intentionally mutate one field at a time. This isolates verification behavior even where the resulting combination would not represent a realistic beverage formulation.",
    "",
    "The rotated and degraded images preserve the compliant semantic content and are intended to exercise OCR robustness.",
    "",
    "`manifest.json` records the purpose and intended outcome of each sample.",
    "",
    "These files are synthetic and contain no production, applicant, or personally identifiable information."
)

[System.IO.File]::WriteAllText(
    (Join-Path $OutputDirectory "README.md"),
    ($readmeLines -join [Environment]::NewLine),
    $utf8NoBom)

Write-Host ""
Write-Host "Representative dataset created:" -ForegroundColor Green
Write-Host $OutputDirectory
Write-Host ""

Get-ChildItem `
    $OutputDirectory `
    -File |
    Sort-Object Name |
    Select-Object Name, Length