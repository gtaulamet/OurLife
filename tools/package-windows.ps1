<#
.SYNOPSIS
Exporta OurLife para Windows y arma el zip de la versión.

.DESCRIPTION
Exporta el preset "Windows Desktop" a builds/windows, corre el smoke sobre el ejecutable
exportado, le pide los avisos de licencia del motor y del runtime (LICENSES.txt), copia
CREDITS.md y comprime todo en builds/OurLife-v<versión>-windows-x86_64.zip. La versión sale de
config/version en game/project.godot.

Necesita Godot 4.7.2 .NET (variable GODOT_BIN) y sus plantillas de exportación de Windows.
#>
param(
    [string]$Godot = $(if ($env:GODOT_BIN) { $env:GODOT_BIN } else { [System.Environment]::GetEnvironmentVariable('GODOT_BIN', 'User') })
)

$ErrorActionPreference = 'Stop'
if (-not $Godot -or -not (Test-Path $Godot)) {
    throw "No se encontró Godot. Define GODOT_BIN con la ruta del ejecutable de consola de Godot 4.7.2 .NET."
}

$root = Split-Path $PSScriptRoot -Parent
$version = (Select-String -Path (Join-Path $root 'game/project.godot') -Pattern '^config/version="(.+)"').Matches[0].Groups[1].Value
$out = Join-Path $root 'builds/windows'
$exe = Join-Path $out 'OurLife.exe'
$zip = Join-Path $root "builds/OurLife-v$version-windows-x86_64.zip"

# Una carpeta limpia, para que el zip no arrastre restos de otra exportación.
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Force $out | Out-Null

Write-Host "Exportando OurLife $version..."
& $Godot --headless --path (Join-Path $root 'game') --export-release 'Windows Desktop' $exe
if ($LASTEXITCODE -ne 0) { throw "La exportación falló con el código $LASTEXITCODE." }

# El ejecutable exportado es una aplicación de ventana: se espera al proceso para leer su código.
Write-Host 'Smoke sobre el ejecutable exportado...'
$smoke = Start-Process -FilePath $exe -ArgumentList '--headless', '--', '--smoke' -Wait -PassThru
if ($smoke.ExitCode -ne 0) { throw "El smoke del ejecutable exportado falló con el código $($smoke.ExitCode)." }

$licenses = Start-Process -FilePath $exe -ArgumentList '--headless', '--', '--licenses', (Join-Path $out 'LICENSES.txt') -Wait -PassThru
if ($licenses.ExitCode -ne 0) { throw "No se pudieron escribir las licencias: código $($licenses.ExitCode)." }
Copy-Item (Join-Path $root 'CREDITS.md') $out

if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $out '*') -DestinationPath $zip
Write-Host "Listo: $zip"
