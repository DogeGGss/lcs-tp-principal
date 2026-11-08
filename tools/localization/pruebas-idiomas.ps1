# F30 (Idiomas): corre todas las pruebas automáticas de idiomas y deja la evidencia para el plan de pruebas.
#   1. Validador del catálogo (node tools/localization/manage.mjs validate).
#   2. Unity EditMode, categoría US 349: catálogo completo, datos insertados y textos sin traducir en el código.
#   3. Unity PlayMode, categorías US 347 y US 348: menús, perfil y partidas en inglés (captura + textos por caso).
#   4. Pruebas del launcher.
# Uso (con Unity cerrado en este proyecto):
#   powershell -ExecutionPolicy Bypass -File tools/localization/pruebas-idiomas.ps1 [-Unity "D:\unity\6000.6.2f1\Editor\Unity.exe"]
# La evidencia queda en output/pruebas-idiomas/<fecha>: un .png y un .txt por caso (US-347_CA1_..., US-348_CA2_...),
# los resultados XML de Unity y resumen.txt con una línea por caso para copiar al plan de pruebas.
param(
    [string]$Unity = "D:\unity\6000.6.2f1\Editor\Unity.exe",
    [switch]$SinLauncher
)
$ErrorActionPreference = "Stop"
$raiz = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$proyecto = Join-Path $raiz "My project"
$evidencia = Join-Path $raiz ("output\pruebas-idiomas\" + (Get-Date -Format "yyyy-MM-dd_HH-mm"))
New-Item -ItemType Directory -Force $evidencia | Out-Null
$env:RIFTWALKER_EVIDENCIA = $evidencia
$resumen = New-Object System.Collections.Generic.List[string]
$fallo = $false

Write-Host "1/4 Validador del catálogo"
$validador = & node (Join-Path $raiz "tools\localization\manage.mjs") validate 2>&1 | Out-String
Set-Content -Encoding utf8 (Join-Path $evidencia "US-349_validador.txt") $validador
if ($LASTEXITCODE -ne 0) { $fallo = $true; $resumen.Add("US 349  validador         FALLÓ") } else { $resumen.Add("US 349  validador         OK  " + $validador.Trim()) }

function Unity-Tests([string]$plataforma, [string]$filtro, [string]$nombre) {
    $xml = Join-Path $evidencia "$nombre.xml"
    & $Unity -batchmode -projectPath $proyecto -runTests -testPlatform $plataforma -testCategory $filtro `
        -testResults $xml -logFile (Join-Path $evidencia "$nombre.log") | Out-Null
    if (-not (Test-Path $xml)) { $script:fallo = $true; $script:resumen.Add("$nombre  no corrió (ver $nombre.log)"); return }
    [xml]$r = Get-Content -Encoding utf8 $xml
    foreach ($caso in $r.SelectNodes("//test-case")) {
        $estado = if ($caso.result -eq "Passed") { "OK   " } else { $script:fallo = $true; "FALLÓ" }
        $detalle = if ($caso.failure) { "  " + ($caso.failure.message.InnerText -split "`n")[0].Trim() } else { "" }
        $script:resumen.Add("{0,-48} {1}{2}" -f $caso.name, $estado, $detalle)
    }
}

Write-Host "2/4 Unity EditMode (US 349)"
Unity-Tests "EditMode" "US 349" "unity-editmode"
Write-Host "3/4 Unity PlayMode (US 347 y US 348)"
Unity-Tests "PlayMode" "US 347;US 348" "unity-playmode"

if (-not $SinLauncher) {
    Write-Host "4/4 Launcher"
    $launcher = & dotnet run --project (Join-Path $raiz "Launcher\Tests\Riftwalker.Launcher.Tests.csproj") 2>&1 | Out-String
    Set-Content -Encoding utf8 (Join-Path $evidencia "launcher.txt") $launcher
    if ($LASTEXITCODE -ne 0) { $fallo = $true; $resumen.Add("Launcher                FALLÓ") } else { $resumen.Add("Launcher                OK  " + ($launcher.Trim() -split "`n")[-1]) }
}

Set-Content -Encoding utf8 (Join-Path $evidencia "resumen.txt") $resumen
$resumen | ForEach-Object { Write-Host $_ }
Write-Host "Evidencia: $evidencia"
if ($fallo) { exit 1 }
