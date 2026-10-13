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
    [string]$Godot = $(if ($env:GODOT_BIN) { $env:GODOT_BIN } else { [System.Environment]::GetEnvironmentVariable('GODOT_BIN', 'User') })
)

$ErrorActionPreference = 'Stop'
if (-not $Godot -or -not (Test-Path $Godot)) {
    throw "Godot was not found. Set GODOT_BIN to the console executable of Godot 4.7.2 .NET."
}

$root = Split-Path $PSScriptRoot -Parent
$version = (Select-String -Path (Join-Path $root 'game/project.godot') -Pattern '^config/version="(.+)"').Matches[0].Groups[1].Value
$out = Join-Path $root 'builds/windows'
$exe = Join-Path $out 'OurLife.exe'
$licenses = Join-Path $out 'LICENSES.txt'
$zip = Join-Path $root "builds/OurLife-v$version-windows-x86_64.zip"

# A clean folder, so the zip carries nothing left from another export.
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Force $out | Out-Null

# MSBuild nodes and the compiler server outlive the build and keep the console of Godot's
# launcher open, so the export would never return: the export builds without them.
$env:MSBUILDDISABLENODEREUSE = '1'
$env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'
$env:UseSharedCompilation = 'false'

Write-Host "Exporting OurLife $version..."
& $Godot --headless --path (Join-Path $root 'game') --export-release 'Windows Desktop' $exe
if ($LASTEXITCODE -ne 0) { throw "The export failed with code $LASTEXITCODE." }

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
