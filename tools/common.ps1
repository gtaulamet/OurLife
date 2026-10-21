<#
.SYNOPSIS
What the packaging scripts share: finding Godot, the version, and exporting without hanging.

.DESCRIPTION
Dot-sourced by package-windows.ps1, package-macos.ps1 and package-linux-arm64.ps1.
#>

# The console executable of Godot 4.7.2 .NET: the given path, else GODOT_BIN of the session or the user.
function Resolve-Godot([string]$Godot) {
    if (-not $Godot) {
        $Godot = if ($env:GODOT_BIN) { $env:GODOT_BIN } else { [System.Environment]::GetEnvironmentVariable('GODOT_BIN', 'User') }
    }

    if (-not $Godot -or -not (Test-Path $Godot)) {
        throw "Godot was not found. Set GODOT_BIN to the console executable of Godot 4.7.2 .NET."
    }

    return $Godot
}

# The game's version, from config/version in game/project.godot: the presets take it from there too.
function Get-GameVersion([string]$Root) {
    return (Select-String -Path (Join-Path $Root 'game/project.godot') -Pattern '^config/version="(.+)"').Matches[0].Groups[1].Value
}

# An empty folder, so a package carries nothing left from another export.
function Reset-Folder([string]$Path) {
    if (Test-Path $Path) { Remove-Item $Path -Recurse -Force }
    New-Item -ItemType Directory -Force $Path | Out-Null
}

# Runs Godot's export of a preset. MSBuild nodes and the compiler server outlive the build and keep
# the console of Godot's launcher open, so the export would never return: it builds without them,
# and the session gets its own settings back afterwards.
function Invoke-GodotExport([string]$Godot, [string]$Root, [string]$Preset, [string]$Path) {
    $names = 'MSBUILDDISABLENODEREUSE', 'DOTNET_CLI_USE_MSBUILD_SERVER', 'UseSharedCompilation'
    $saved = @{}
    foreach ($name in $names) { $saved[$name] = [System.Environment]::GetEnvironmentVariable($name, 'Process') }
    try {
        $env:MSBUILDDISABLENODEREUSE = '1'
        $env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'
        $env:UseSharedCompilation = 'false'
        & $Godot --headless --path (Join-Path $Root 'game') --export-release $Preset $Path
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path $Path)) { throw "The export of '$Preset' failed with code $LASTEXITCODE." }
    }
    finally {
        foreach ($name in $names) { [System.Environment]::SetEnvironmentVariable($name, $saved[$name], 'Process') }
    }
}

# A zip made on Windows marks every entry as made on MS-DOS, and then macOS and Linux ignore its
# permissions: the executable cannot run. This marks the entries as made on Unix again, gives those
# without a mode rw-r--r-- (folders rwxr-xr-x), and makes the game's executable runnable. Needs
# PowerShell 7.4 or later (Stream.ReadExactly).
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

            # The executable may run for its owner, its group and the others (the x bits).
            if ($name -eq $Executable) {
                $mode = $mode -bor 0x49
                [BitConverter]::GetBytes([uint16]$mode).CopyTo($directory, $at + 40)
                $executableMode = $mode
            }

            $at += 46 + $nameLength + [BitConverter]::ToUInt16($directory, $at + 30) + [BitConverter]::ToUInt16($directory, $at + 32)
        }

        if ($executableMode -eq 0) { throw "$Executable is not in $Path." }
        $stream.Position = $offset
        $stream.Write($directory, 0, $size)
    }
    finally {
        $stream.Dispose()
    }
}
