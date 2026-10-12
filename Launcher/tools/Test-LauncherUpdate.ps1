param([string]$Executable, [string]$PreviousExecutable, [switch]$SimulateFailedStartup)
$ErrorActionPreference = 'Stop'
$launcherRoot = Split-Path $PSScriptRoot -Parent
if (!$Executable) { $Executable = Join-Path $launcherRoot 'artifacts/publish/Riftwalker-Launcher.exe' }
$Executable = (Resolve-Path -LiteralPath $Executable).Path
$testRoot = Join-Path $launcherRoot ('artifacts/self-update-test-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $testRoot '.riftwalker') -Force | Out-Null
$target = Join-Path $testRoot 'Riftwalker-Launcher.exe'
$helper = Join-Path $testRoot '.riftwalker/launcher-update.exe'
if (!$PreviousExecutable) { $PreviousExecutable = $Executable }
Copy-Item -LiteralPath $PreviousExecutable -Destination $target
Copy-Item -LiteralPath $Executable -Destination $helper
Set-Content -LiteralPath (Join-Path $testRoot '.riftwalker/installation.json') -Value '{"schema":1}'
$hash = (Get-FileHash -LiteralPath $Executable -Algorithm SHA256).Hash
$previousHash = (Get-FileHash -LiteralPath $PreviousExecutable -Algorithm SHA256).Hash
$previousHook = $env:DOTNET_STARTUP_HOOKS
try {
    if ($SimulateFailedStartup) {
        $hookRoot = Join-Path $testRoot 'hook'
        New-Item -ItemType Directory -Path $hookRoot | Out-Null
        '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>' | Set-Content (Join-Path $hookRoot 'Hook.csproj')
        'public class StartupHook { public static void Initialize() { foreach (var arg in System.Environment.GetCommandLineArgs()) if (arg == "--updated") System.Environment.Exit(23); } }' | Set-Content (Join-Path $hookRoot 'StartupHook.cs')
        dotnet build (Join-Path $hookRoot 'Hook.csproj') -o (Join-Path $hookRoot 'out') --nologo
        if ($LASTEXITCODE -ne 0) { throw 'No se pudo preparar la falla simulada.' }
        $env:DOTNET_STARTUP_HOOKS = Join-Path $hookRoot 'out/Hook.dll'
    }
    # Nonexistent parent represents a process that has already finished.
    $process = Start-Process -FilePath $helper -ArgumentList @('--apply-update','2147483647',('"{0}"' -f $testRoot),$hash) -WindowStyle Hidden -PassThru
    if (!$process.WaitForExit(45000)) { throw 'El actualizador no terminó en 45 segundos.' }
    if ($SimulateFailedStartup) {
        if (!(Test-Path -LiteralPath (Join-Path $testRoot '.riftwalker/failed-update.json'))) { throw 'No se registró la recuperación tras la falla.' }
        if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -cne $previousHash) { throw 'No se restauró el EXE anterior.' }
        Write-Host 'PASS: falla de inicio simulada; se restauró el ejecutable anterior.'
        return
    }
    if (!(Test-Path -LiteralPath (Join-Path $testRoot '.riftwalker/update-ok'))) { throw 'La ventana nueva no confirmó el inicio.' }
    if (!(Test-Path -LiteralPath (Join-Path $testRoot '.riftwalker/launcher.previous.exe'))) { throw 'No se conservó la versión anterior.' }
    if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -cne $hash) { throw 'El EXE instalado no coincide.' }
    Write-Host 'PASS: helper reemplazó el EXE, conservó respaldo y recibió confirmación de inicio.'
}
finally {
    $env:DOTNET_STARTUP_HOOKS = $previousHook
    # Close only processes from this uniquely created fixture. Never close a user's launcher or Unity.
    foreach ($candidate in Get-Process -Name 'Riftwalker-Launcher','launcher-update' -ErrorAction SilentlyContinue) {
        try { $path = $candidate.MainModule.FileName } catch { continue }
        if ($path -eq $target -or $path -eq $helper) { Stop-Process -Id $candidate.Id -Force }
    }
}
