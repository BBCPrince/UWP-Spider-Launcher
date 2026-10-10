$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$output = Join-Path (Split-Path $PSScriptRoot -Parent) 'artwork'
New-Item -ItemType Directory -Path $output -Force | Out-Null
foreach ($item in @(@('Logo.png',150,150),@('SmallLogo.png',44,44),@('StoreLogo.png',50,50),@('SplashScreen.png',620,300))) {
    $name,$width,$height = $item
    $bitmap = New-Object System.Drawing.Bitmap $width,$height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $background = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(17,24,39))
    $accent = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(206,42,73))
    try {
        $graphics.FillRectangle($background,0,0,$width,$height)
        $side = [Math]::Min($width,$height) * 0.64
        $left = ($width-$side)/2
        $top = ($height-$side)/2
        $graphics.FillRectangle($accent,[single]$left,[single]$top,[single]$side,[single]$side)
        $points = [System.Drawing.PointF[]]@(
            (New-Object System.Drawing.PointF ([single]($left+$side*0.36)),([single]($top+$side*0.25))),
            (New-Object System.Drawing.PointF ([single]($left+$side*0.36)),([single]($top+$side*0.75))),
            (New-Object System.Drawing.PointF ([single]($left+$side*0.73)),([single]($top+$side*0.5))))
        $graphics.FillPolygon([System.Drawing.Brushes]::White,$points)
        $bitmap.Save((Join-Path $output $name),[System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $accent.Dispose();$background.Dispose();$graphics.Dispose();$bitmap.Dispose() }
}
