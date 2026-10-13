<#
.SYNOPSIS
Exports OurLife for Windows and builds the release zip.

.DESCRIPTION
Exports the "Windows Desktop" preset to builds/windows, runs the smoke test on the exported
executable, asks it for the license notices of the engine and the runtime (LICENSES.txt), copies
CREDITS.md and zips everything into builds/OurLife-v<version>-windows-x86_64.zip. The version
comes from config/version in game/project.godot.

Needs Godot 4.7.2 .NET (GODOT_BIN) and its Windows export templates.
#>
param(
    [string]$Godot
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
$Godot = Resolve-Godot $Godot
$root = Split-Path $PSScriptRoot -Parent
$version = Get-GameVersion $root
$out = Join-Path $root 'builds/windows'
$exe = Join-Path $out 'OurLife.exe'
$licenses = Join-Path $out 'LICENSES.txt'
$zip = Join-Path $root "builds/OurLife-v$version-windows-x86_64.zip"

Reset-Folder $out
Write-Host "Exporting OurLife $version..."
Invoke-GodotExport $Godot $root 'Windows Desktop' $exe

# The exported executable is a window application: wait for the process to read its exit code.
Write-Host 'Smoke test on the exported executable...'
$smoke = Start-Process -FilePath $exe -ArgumentList '--headless', '--', '--smoke' -Wait -PassThru
if ($smoke.ExitCode -ne 0) { throw "The smoke test of the exported executable failed with code $($smoke.ExitCode)." }

# Start-Process does not quote arguments: a path with spaces needs its own quotes.
$notices = Start-Process -FilePath $exe -ArgumentList '--headless', '--', '--licenses', "`"$licenses`"" -Wait -PassThru
if ($notices.ExitCode -ne 0 -or -not (Test-Path $licenses)) { throw "The license notices could not be written: code $($notices.ExitCode)." }
Copy-Item (Join-Path $root 'CREDITS.md') $out

if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $out '*') -DestinationPath $zip
Write-Host "Done: $zip"
