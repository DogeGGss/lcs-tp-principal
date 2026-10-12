# Game version release: only the game ZIP and release.json (the launcher has its own fixed release, Publish-Launcher.ps1).
# Generates the files locally. -Upload creates a DRAFT; -Publish explicitly publishes it.
param(
    [Parameter(Mandatory)][string]$GameBuild,
    [Parameter(Mandatory)][string]$Version,
    [Parameter(Mandatory)][string]$Notes,
    [switch]$Upload,
    [switch]$Publish
)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(-exp\.(0|[1-9]\d*))?$') { throw 'Usá X.Y.Z o X.Y.Z-exp.N.' }
if ($Publish -and !$Upload) { throw '-Publish requiere -Upload.' }
$buildRoot = (Resolve-Path -LiteralPath $GameBuild).Path
$stampPath = Join-Path $buildRoot 'riftwalker-build.json'
if (!(Test-Path -LiteralPath $stampPath)) { throw 'Generá el build con Riftwalker > Publicar versión; falta su sello de versión.' }
if ((Get-Content -Raw -Encoding UTF8 -LiteralPath $stampPath | ConvertFrom-Json).version -cne $Version) { throw 'El build y el release deben tener la misma versión.' }
if (!(Test-Path -LiteralPath (Join-Path $buildRoot 'Project Riftwalker.exe')) -or !(Test-Path -LiteralPath (Join-Path $buildRoot 'Project Riftwalker_Data'))) { throw 'Falta el build completo con su EXE en la raíz.' }
$notesPath = (Resolve-Path -LiteralPath $Notes).Path
$notesText = Get-Content -Raw -Encoding UTF8 -LiteralPath $notesPath
foreach ($heading in @('Qué trae', 'Cambios', 'Correcciones', 'Problemas conocidos')) {
    if ($notesText -notmatch [regex]::Escape($heading)) { throw "Falta la sección '$heading' en las notas." }
}
$launcherRoot = Split-Path $PSScriptRoot -Parent
$output = Join-Path $launcherRoot "artifacts/release-v$Version"
if (Test-Path -LiteralPath $output) { throw "Ya existe $output. Conservamos los artefactos anteriores: elegí otra versión o mové esa carpeta." }
New-Item -ItemType Directory -Path $output | Out-Null
$zipName = "Project-Riftwalker-v$Version.zip"
$zipPath = Join-Path $output $zipName
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
# Reject symlinks/junctions instead of packaging external files by accident.
$entries = Get-ChildItem -LiteralPath $buildRoot -Recurse -Force
if ($entries | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) { throw 'El build contiene enlaces; usá una carpeta de build normal.' }
if (Test-Path -LiteralPath (Join-Path $buildRoot 'installed.json')) { throw 'No publiques una instalación del launcher; usá el build original de Unity.' }
# Unity leaves debug folders next to the build that must not be shipped (Burst: *_BurstDebugInformation_DoNotShip;
# IL2CPP: *_BackUpThisFolder_ButDontShipItWithYourGame). They stay out of the ZIP and of the unpacked size.
$noShip = '^[^/]*_(BurstDebugInformation_DoNotShip|BackUpThisFolder_ButDontShipItWithYourGame)(/|$)'
$files = foreach ($file in $entries | Where-Object { !$_.PSIsContainer }) {
    $relative = $file.FullName.Substring($buildRoot.Length).TrimStart('\', '/').Replace('\', '/')
    if ($relative -notmatch $noShip) { [pscustomobject]@{ Path = $file.FullName; Entry = $relative; Length = $file.Length } }
}
$skipped = $entries | Where-Object { $_.PSIsContainer -and $_.Parent.FullName.TrimEnd('\', '/') -eq $buildRoot.TrimEnd('\', '/') -and $_.Name -match '_(BurstDebugInformation_DoNotShip|BackUpThisFolder_ButDontShipItWithYourGame)$' }
foreach ($folder in $skipped) { Write-Host "Se deja afuera del ZIP: $($folder.Name)" }
$unpacked = ($files | Measure-Object -Property Length -Sum).Sum
$zip = [IO.Compression.ZipFile]::Open($zipPath, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in $files) {
        [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file.Path, $file.Entry, [IO.Compression.CompressionLevel]::Optimal)
    }
}
finally { $zip.Dispose() }
$manifest = [ordered]@{
    schema = 1; version = $Version
    game = [ordered]@{ version = $Version; asset = $zipName; executable = 'Project Riftwalker.exe'; size = (Get-Item -LiteralPath $zipPath).Length; unpackedSize = [long]$unpacked; sha256 = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant() }
}
$manifestPath = Join-Path $output 'release.json'
# UTF-8 without BOM (Windows PowerShell's -Encoding utf8 adds one), so any JSON reader takes it.
[IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 5), (New-Object Text.UTF8Encoding $false))
# The release page ends with how to install it: from the launcher. The launcher cuts the notes at the mark, so this
# footer only shows on GitHub.
$experimental = $Version.Contains('-exp.')
$launcherUrl = 'https://github.com/DogeGGss/lcs-tp-principal/releases/download/launcher/Riftwalker-Launcher.exe'
$footer = "`n`n<!-- riftwalker:pie -->`n---`n**Esta versión se instala desde el launcher.** Descargá [Riftwalker-Launcher.exe]($launcherUrl) una sola vez: el launcher baja e instala esta versión y las siguientes."
if ($experimental) { $footer += ' Es experimental: activá «Versiones experimentales» en Ajustes.' }
$bodyPath = Join-Path $output 'notas.md'
[IO.File]::WriteAllText($bodyPath, $notesText.TrimEnd() + $footer + "`n", (New-Object Text.UTF8Encoding $false))
if ($Upload) {
    $ghArgs = @('release','create',"v$Version",$zipPath,$manifestPath,'--repo','DogeGGss/lcs-tp-principal','--title',"Project Riftwalker v$Version",'--notes-file',$bodyPath)
    if ($experimental) { $ghArgs += '--prerelease' }
    if (!$Publish) { $ghArgs += '--draft' }
    & gh @ghArgs
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo subir el release. Los archivos generados se conservaron.' }
}
Write-Host "Release preparado en $output"
if (!$Upload) { Write-Host 'Subí el ZIP y release.json juntos a un release con el tag indicado, con notas.md como descripción. Publicalo recién cuando estén los dos archivos.' }
