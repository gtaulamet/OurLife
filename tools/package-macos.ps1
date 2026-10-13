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

The app cannot be run here: the smoke test is run on a Mac (see docs/ip/playtest-ip2.md).

Needs Godot 4.7.2 .NET (GODOT_BIN) and its macOS export template (macos.zip).
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
$out = Join-Path $root 'builds/macos'
$export = Join-Path $out 'OurLife.zip'
$licenses = Join-Path $out 'LICENSES.txt'
$zip = Join-Path $root "builds/OurLife-v$version-macos-universal.zip"

# A clean folder, so the zip carries nothing left from another export.
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Force $out | Out-Null

# MSBuild nodes and the compiler server outlive the build and keep the console of Godot's
# launcher open, so the export would never return: the export builds without them.
$env:MSBUILDDISABLENODEREUSE = '1'
$env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'
$env:UseSharedCompilation = 'false'

Write-Host "Exporting OurLife $version for macOS..."
& $Godot --headless --path (Join-Path $root 'game') --export-release 'macOS' $export
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $export)) { throw "The export failed with code $LASTEXITCODE." }

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
