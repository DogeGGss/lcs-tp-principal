param([string]$Destino = (Join-Path (Split-Path $PSScriptRoot -Parent) "App/Assets/riftwalker.ico"), [string]$Muestra)
# Ícono del launcher: el rayo del logo sobre una placa oscura con las esquinas cortadas como la ventana.
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase
$tamanos = 16, 24, 32, 48, 64, 128, 256
$pngs = New-Object System.Collections.Generic.List[byte[]]
$fondo = New-Object System.Windows.Media.SolidColorBrush ([System.Windows.Media.Color]::FromRgb(0x14, 0x19, 0x22))
$naranja = New-Object System.Windows.Media.SolidColorBrush ([System.Windows.Media.Color]::FromRgb(0xF2, 0x9A, 0x38))
foreach ($t in $tamanos) {
    $v = New-Object System.Windows.Media.DrawingVisual
    $dc = $v.RenderOpen()
    $c = [double]$t * 0.22   # esquinas grandes: arriba a la izquierda y abajo a la derecha
    $s = [double]$t * 0.08   # esquinas chicas
    $inv = [Globalization.CultureInfo]::InvariantCulture
    $datos = [string]::Format($inv, "M {0} 0 L {1} 0 L {2} {3} L {2} {4} L {4} {2} L {3} {2} L 0 {5} L 0 {0} Z", $c, ($t - $s), $t, $s, ($t - $c), ($t - $s))
    $placa = [System.Windows.Media.Geometry]::Parse($datos)
    $borde = $null
    if ($t -ge 32) { $borde = New-Object System.Windows.Media.Pen ($naranja, [Math]::Max(1.0, $t / 32.0)) }
    $dc.DrawGeometry($fondo, $borde, $placa)
    # El rayo del logo (caja de 30 x 35), centrado y al 66 % del alto.
    $rayo = [System.Windows.Media.Geometry]::Parse("M 16,0 L 3,19 L 13,19 L 7,35 L 29,12 L 18,12 L 24,0 Z").Clone()
    $escala = ($t * 0.66) / 35.0
    $grupo = New-Object System.Windows.Media.TransformGroup
    $grupo.Children.Add((New-Object System.Windows.Media.ScaleTransform ($escala, $escala)))
    $grupo.Children.Add((New-Object System.Windows.Media.TranslateTransform ((($t - 30 * $escala) / 2), (($t - 35 * $escala) / 2))))
    $rayo.Transform = $grupo
    $dc.DrawGeometry($naranja, $null, $rayo)
    $dc.Close()
    $bmp = New-Object System.Windows.Media.Imaging.RenderTargetBitmap ($t, $t, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $bmp.Render($v)
    $enc = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $enc.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bmp))
    $ms = New-Object System.IO.MemoryStream
    $enc.Save($ms)
    $pngs.Add($ms.ToArray())
}
# ICO con imágenes PNG adentro (válido desde Windows Vista).
$ms = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $ms
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$tamanos.Count)
$offset = 6 + 16 * $tamanos.Count
for ($i = 0; $i -lt $tamanos.Count; $i++) {
    $t = $tamanos[$i]; $dim = if ($t -ge 256) { 0 } else { $t }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$pngs[$i].Length); $w.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $w.Write($p) }
$w.Flush()
[System.IO.File]::WriteAllBytes($Destino, $ms.ToArray())
if ($Muestra) { [System.IO.File]::WriteAllBytes($Muestra, $pngs[$pngs.Count - 1]) }
"ícono: $((Get-Item $Destino).Length) bytes"
