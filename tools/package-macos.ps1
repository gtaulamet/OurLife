<#
.SYNOPSIS
Exports OurLife for macOS and builds the release zip.

.DESCRIPTION
Exports the "macOS" preset (a universal app, Apple Silicon and Intel, with an ad hoc signature)
to builds/macos/OurLife.zip, writes the license notices of the engine and the runtime
(LICENSES.txt) with the Godot of this machine, which is the same engine, and adds them and
CREDITS.md to a copy of the zip: builds/OurLife-v<version>-macos-universal.zip. The files are
added to the zip as it is, so the app keeps the execute permissions Godot gave it. The version
comes from config/version in game/project.godot.

The app cannot be run here: the smoke test is run on a Mac. Godot's ad hoc signature from
Windows is not accepted on Apple Silicon: the README says how to sign the app again on the Mac.

Needs Godot 4.7.2 .NET (GODOT_BIN) and its macOS export template (macos.zip).
#>
param(
    [string]$Godot
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
$Godot = Resolve-Godot $Godot
$root = Split-Path $PSScriptRoot -Parent
$version = Get-GameVersion $root
$out = Join-Path $root 'builds/macos'
$export = Join-Path $out 'OurLife.zip'
$licenses = Join-Path $out 'LICENSES.txt'
$zip = Join-Path $root "builds/OurLife-v$version-macos-universal.zip"

Reset-Folder $out
Write-Host "Exporting OurLife $version for macOS..."
Invoke-GodotExport $Godot $root 'macOS' $export

# The engine and the runtime of this machine's Godot are those of the export: same notices.
& $Godot --headless --path (Join-Path $root 'game') -- --licenses $licenses
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $licenses)) { throw "The license notices could not be written: code $LASTEXITCODE." }

# Added in update mode: the entries Godot wrote keep their Unix permissions.
Copy-Item $export $zip -Force
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::Open($zip, [System.IO.Compression.ZipArchiveMode]::Update)
try {
    foreach ($file in @($licenses, (Join-Path $root 'CREDITS.md'))) {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file, (Split-Path $file -Leaf)) | Out-Null
    }
}
finally {
    $archive.Dispose()
}

Write-Host "Done: $zip"
