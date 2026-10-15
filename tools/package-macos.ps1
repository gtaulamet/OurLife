<#
.SYNOPSIS
Exports OurLife for macOS and builds the release zip.

.DESCRIPTION
Exports the "macOS" preset (a universal app, Apple Silicon and Intel, with an ad hoc signature)
to builds/macos/OurLife.zip, writes the license notices of the engine and the runtime
(LICENSES.txt) with the Godot of this machine, which is the same engine, and adds them and
CREDITS.md to a copy of the zip: builds/OurLife-v<version>-macos-universal.zip. Adding them makes
.NET mark every entry as made on MS-DOS, so the script marks them as made on Unix again: without
it, the app has no executable on the Mac. The version comes from config/version in
game/project.godot.

The app cannot be run here: the smoke test is run on a Mac. Godot's ad hoc signature from
Windows is not accepted on Apple Silicon: the README says how to sign the app again on the Mac.

Needs PowerShell 7.4 or later (Stream.ReadExactly), Godot 4.7.2 .NET (GODOT_BIN) and its macOS export template (macos.zip).
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

# .NET's update mode keeps the permissions Godot wrote but marks every entry as made on MS-DOS, and
# then macOS ignores them: the app has no executable. This marks the entries as made on Unix again,
# gives the added files rw-r--r--, and checks that the app's executable can run.
function Repair-UnixPermissions([string]$Path, [string]$Executable) {
    $stream = [System.IO.File]::Open($Path, 'Open', 'ReadWrite')
    try {
        $tailLength = [int][Math]::Min($stream.Length, 65557)
        $tail = New-Object byte[] $tailLength
        $stream.Position = $stream.Length - $tailLength
        $stream.ReadExactly($tail, 0, $tailLength)
        $end = -1
        for ($i = $tailLength - 22; $i -ge 0; $i--) {
            if ([BitConverter]::ToUInt32($tail, $i) -eq 0x06054b50) { $end = $i; break }
        }
        if ($end -lt 0) { throw "The end of the central directory of $Path was not found." }

        $count = [BitConverter]::ToUInt16($tail, $end + 10)
        $size = [BitConverter]::ToUInt32($tail, $end + 12)
        $offset = [BitConverter]::ToUInt32($tail, $end + 16)
        $zip64Locator = $end -ge 20 -and [BitConverter]::ToUInt32($tail, $end - 20) -eq 0x07064b50
        if ($zip64Locator -or $count -eq 0xFFFF -or $size -eq 0xFFFFFFFF -or $offset -eq 0xFFFFFFFF) {
            throw "$Path is a Zip64 archive, which this repair does not read."
        }

        $directory = New-Object byte[] $size
        $stream.Position = $offset
        $stream.ReadExactly($directory, 0, $size)
        $executableMode = 0
        $at = 0
        for ($n = 0; $n -lt $count; $n++) {
            if ([BitConverter]::ToUInt32($directory, $at) -ne 0x02014b50) { throw "The central directory of $Path is damaged." }
            $directory[$at + 5] = 3
            $nameLength = [BitConverter]::ToUInt16($directory, $at + 28)
            $name = [System.Text.Encoding]::UTF8.GetString($directory, $at + 46, $nameLength)

            # The Unix mode is the high half of the external attributes.
            $mode = [BitConverter]::ToUInt16($directory, $at + 40)
            if ($mode -eq 0) {
                # A folder is rwxr-xr-x and a file rw-r--r--.
                $mode = if ($name.EndsWith('/')) { 0x41ED } else { 0x81A4 }
                [BitConverter]::GetBytes([uint16]$mode).CopyTo($directory, $at + 40)
            }

            if ($name -eq $Executable) { $executableMode = $mode }
            $at += 46 + $nameLength + [BitConverter]::ToUInt16($directory, $at + 30) + [BitConverter]::ToUInt16($directory, $at + 32)
        }

        if (($executableMode -band 0x49) -ne 0x49) { throw "$Executable is not executable in $Path." }
        $stream.Position = $offset
        $stream.Write($directory, 0, $size)
    }
    finally {
        $stream.Dispose()
    }
}

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

Repair-UnixPermissions $zip 'OurLife.app/Contents/MacOS/OurLife'

Write-Host "Done: $zip"
