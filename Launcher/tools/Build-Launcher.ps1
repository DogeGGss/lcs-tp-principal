param([string]$Version = '0.1.0', [string]$Output)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$') { throw 'La versión del launcher debe ser X.Y.Z.' }
$launcherRoot = Split-Path $PSScriptRoot -Parent
if (!$Output) { $Output = Join-Path $launcherRoot 'artifacts/publish' }
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
dotnet publish (Join-Path $launcherRoot 'App/Riftwalker.Launcher.csproj') -c Release -r win-x64 --self-contained true "-p:Version=$Version" -o $Output --nologo
if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación del launcher.' }
Write-Host "Launcher listo: $(Join-Path $Output 'Riftwalker-Launcher.exe')"
