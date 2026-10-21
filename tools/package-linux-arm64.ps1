<#
.SYNOPSIS
Exports OurLife for Linux on ARM64, like a Raspberry Pi 4, and builds the release zip.

.DESCRIPTION
Exports the "Linux ARM64" preset (ETC2/ASTC textures, which the Pi's GPU reads) to
builds/linux-arm64, writes the license notices of the engine and the runtime (LICENSES.txt) with
the Godot of this machine, which is the same engine, copies CREDITS.md, zips everything into
builds/OurLife-v<version>-linux-arm64.zip and marks its entries as made on Unix, with the game's
executable runnable. The version comes from config/version in game/project.godot.

The game cannot be run here: the smoke test is run on the Pi (see the README).

Needs PowerShell 7.4 or later, Godot 4.7.2 .NET (GODOT_BIN) and its Linux ARM64 export templates
(linux_release.arm64 and linux_debug.arm64).
#>
#Requires -Version 7.4
param(
    [string]$Godot
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
$Godot = Resolve-Godot $Godot
$root = Split-Path $PSScriptRoot -Parent
$version = Get-GameVersion $root
$out = Join-Path $root 'builds/linux-arm64'
$exe = Join-Path $out 'OurLife.arm64'
$licenses = Join-Path $out 'LICENSES.txt'
$zip = Join-Path $root "builds/OurLife-v$version-linux-arm64.zip"

# Without the template Godot fails late and says little: say what is missing first.
$templates = Join-Path ([System.Environment]::GetFolderPath('ApplicationData')) 'Godot/export_templates/4.7.2.stable.mono'
foreach ($template in 'linux_release.arm64', 'linux_debug.arm64') {
    if (-not (Test-Path (Join-Path $templates $template))) {
        throw "The export template $template is missing in $templates. It comes in the official .tpz of Godot 4.7.2 mono (see the README)."
    }
}

Reset-Folder $out
Write-Host "Exporting OurLife $version for Linux ARM64..."
Invoke-GodotExport $Godot $root 'Linux ARM64' $exe

# The engine and the runtime of this machine's Godot are those of the export: same notices.
& $Godot --headless --path (Join-Path $root 'game') -- --licenses $licenses
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $licenses)) { throw "The license notices could not be written: code $LASTEXITCODE." }
Copy-Item (Join-Path $root 'CREDITS.md') $out

if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $out '*') -DestinationPath $zip
Repair-UnixPermissions $zip 'OurLife.arm64'

Write-Host "Done: $zip"
