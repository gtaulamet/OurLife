<#
.SYNOPSIS
What the packaging scripts share: finding Godot, the version, and exporting without hanging.

.DESCRIPTION
Dot-sourced by package-windows.ps1 and package-macos.ps1.
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
