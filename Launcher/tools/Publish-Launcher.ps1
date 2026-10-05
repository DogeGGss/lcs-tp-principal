# Launcher release: one fixed release (tag "launcher") with only Riftwalker-Launcher.exe and launcher.json, apart from
# the game versions. Players download it once from a link that never changes; installed launchers update themselves
# from launcher.json. The release is never marked as "Latest": that is always the latest game version.
# Generates the files locally. -Upload creates the release the first time (a DRAFT unless -Publish) and afterwards
# replaces its files in place.
param(
    [Parameter(Mandatory)][string]$Version,
    [switch]$Upload,
    [switch]$Publish
)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$') { throw 'La versión del launcher debe ser X.Y.Z.' }
if ($Publish -and !$Upload) { throw '-Publish requiere -Upload.' }
$repo = 'DogeGGss/lcs-tp-principal'
$title = 'Project Riftwalker · Launcher'
$launcherRoot = Split-Path $PSScriptRoot -Parent
$output = Join-Path $launcherRoot "artifacts/launcher-v$Version"
if (Test-Path -LiteralPath $output) { throw "Ya existe $output. Elegí otra versión o mové esa carpeta." }
New-Item -ItemType Directory -Path $output | Out-Null
& (Join-Path $PSScriptRoot 'Build-Launcher.ps1') -Version $Version -Output (Join-Path $output 'build')
$exe = Join-Path $output 'Riftwalker-Launcher.exe'
Copy-Item -LiteralPath (Join-Path $output 'build/Riftwalker-Launcher.exe') -Destination $exe
$manifest = [ordered]@{
    version = $Version; asset = 'Riftwalker-Launcher.exe'
    size = (Get-Item -LiteralPath $exe).Length; sha256 = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant()
}
$manifestPath = Join-Path $output 'launcher.json'
# UTF-8 without BOM (Windows PowerShell's -Encoding utf8 adds one), so any JSON reader takes it.
[IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json), (New-Object Text.UTF8Encoding $false))
$notes = @'
## Project Riftwalker · Launcher {version}

Descargá **Riftwalker-Launcher.exe** una sola vez y abrilo: instala el juego, lo mantiene actualizado y muestra las novedades de cada versión. No hace falta instalar nada más.

- La primera vez propone una carpeta (`%LOCALAPPDATA%\Project Riftwalker`) y crea accesos directos en el escritorio y en el menú Inicio, sin pedir permisos de administrador.
- Si Windows muestra «Windows protegió su PC», es porque el ejecutable no tiene firma digital: **Más información → Ejecutar de todas formas**.
- El launcher se actualiza solo cuando sale una versión nueva; este link de descarga no cambia nunca.
- Para desinstalar, cerrá el juego y el launcher, y borrá esa carpeta y los dos accesos directos. Tus opciones y partidas se guardan aparte.

Las versiones del juego están en los demás releases, pero se instalan desde el launcher.
'@
$notesPath = Join-Path $output 'notas.md'
[IO.File]::WriteAllText($notesPath, $notes.Replace('{version}', $Version), (New-Object Text.UTF8Encoding $false))
if ($Upload) {
    # gh release list also shows drafts, unlike gh release view.
    $tags = & gh release list --repo $repo --limit 200 --json tagName --jq '.[].tagName'
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo consultar los releases con gh.' }
    if (@($tags) -contains 'launcher') {
        # Same release, same link. The EXE goes first: a launcher that checks in between still sees the previous
        # launcher.json and simply updates the next time.
        & gh release upload launcher $exe --repo $repo --clobber
        if ($LASTEXITCODE -ne 0) { throw 'No se pudo subir el EXE del launcher.' }
        & gh release upload launcher $manifestPath --repo $repo --clobber
        if ($LASTEXITCODE -ne 0) { throw 'No se pudo subir launcher.json.' }
        & gh release edit launcher --repo $repo --title $title --notes-file $notesPath
        if ($LASTEXITCODE -ne 0) { throw 'No se pudo actualizar la descripción del release del launcher.' }
    }
    else {
        $ghArgs = @('release', 'create', 'launcher', $exe, $manifestPath, '--repo', $repo, '--title', $title, '--notes-file', $notesPath, '--latest=false')
        if (!$Publish) { $ghArgs += '--draft' }
        & gh @ghArgs
        if ($LASTEXITCODE -ne 0) { throw 'No se pudo crear el release del launcher.' }
    }
}
Write-Host "Launcher $Version preparado en $output"
Write-Host "Link fijo de descarga: https://github.com/$repo/releases/download/launcher/Riftwalker-Launcher.exe"
