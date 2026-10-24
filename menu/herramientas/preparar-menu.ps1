# Copia las imágenes del Perfil del juego a la página, en tamaño web.
# Banners a 1800x600, marcos partidos en dos mitades a la mitad del original (960x640 cada una), fondos y avatares tal cual.
Add-Type -AssemblyName System.Drawing
$src = "D:\Juego Unity\My project\Assets\Resources\Perfil"
$dst = "C:\Users\Doge\AppData\Local\Temp\claude\D--Juego-Unity\98fa2e44-203f-4228-8ade-9108a3399b35\scratchpad\guia\menu\img"
foreach ($d in 'avatares', 'banners', 'marcos', 'fondos') { New-Item -ItemType Directory -Force "$dst\$d" | Out-Null }
$jpg = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object { $_.MimeType -eq 'image/jpeg' }
function GuardarJpg($img, $w, $h, $ruta, $q) {
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = 'HighQualityBicubic'; $g.PixelOffsetMode = 'HighQuality'
    $g.DrawImage($img, 0, 0, $w, $h)
    $p = New-Object System.Drawing.Imaging.EncoderParameters 1
    $p.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter ([System.Drawing.Imaging.Encoder]::Quality), ([long]$q)
    $bmp.Save($ruta, $jpg, $p); $g.Dispose(); $bmp.Dispose()
}
Get-ChildItem "$src\Avatares\*.jpg" | ForEach-Object { Copy-Item $_.FullName "$dst\avatares\$($_.Name)" }
Get-ChildItem "$src\Fondos\*.jpg" | ForEach-Object { Copy-Item $_.FullName "$dst\fondos\$($_.Name)" }
Copy-Item "$src\Icono.png" "$dst\icono.png"
Get-ChildItem "$src\Banners\*.jpg" | ForEach-Object {
    $img = [System.Drawing.Image]::FromFile($_.FullName)
    GuardarJpg $img 1800 600 "$dst\banners\$($_.Name)" 84
    $img.Dispose()
}
Get-ChildItem "$src\Marcos\*.png" | ForEach-Object {
    $img = [System.Drawing.Image]::FromFile($_.FullName)
    $w = [int]($img.Width / 2); $h = [int]($img.Height / 2); $mitad = [int]($w / 2)
    foreach ($lado in 'i', 'd') {
        $bmp = New-Object System.Drawing.Bitmap $mitad, $h, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.InterpolationMode = 'HighQualityBicubic'; $g.PixelOffsetMode = 'HighQuality'; $g.CompositingMode = 'SourceCopy'
        $x0 = if ($lado -eq 'i') { 0 } else { $img.Width / 2 }
        $g.DrawImage($img, (New-Object System.Drawing.Rectangle 0, 0, $mitad, $h), $x0, 0, ($img.Width / 2), $img.Height, ([System.Drawing.GraphicsUnit]::Pixel))
        $bmp.Save("$dst\marcos\$($_.BaseName)-$lado.png", [System.Drawing.Imaging.ImageFormat]::Png)
        $g.Dispose(); $bmp.Dispose()
    }
    $img.Dispose()
}
foreach ($d in 'avatares', 'banners', 'marcos', 'fondos') {
    $f = Get-ChildItem "$dst\$d"
    "{0}: {1} archivos, {2:N1} MB" -f $d, $f.Count, (($f | Measure-Object Length -Sum).Sum / 1MB)
}
