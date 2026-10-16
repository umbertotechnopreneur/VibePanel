# VBWR B
# Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
# Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
# VBWR E
[CmdletBinding()]
param(
    [switch]$DisableStartup
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$sourceRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)
$sourceExecutable = Join-Path $sourceRoot 'VibePanel.exe'
if (-not (Test-Path -LiteralPath $sourceExecutable -PathType Leaf)) {
    throw 'Install-VibePanel.ps1 must be run from an extracted VibePanel package, next to VibePanel.exe.'
}

$localAppData = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
if ([string]::IsNullOrWhiteSpace($localAppData)) {
    throw 'The current user does not have a LocalAppData folder.'
}

$installRoot = Join-Path $localAppData 'Umberto Giacobbi.biz\Apps\VibePanel'
$binaryHash = (Get-FileHash -LiteralPath $sourceExecutable -Algorithm SHA256).Hash.ToLowerInvariant()
$releaseName = "VibePanel-$($binaryHash.Substring(0, 12))"
$releasePath = Join-Path $installRoot $releaseName
$installPrefix = [System.IO.Path]::TrimEndingDirectorySeparator([System.IO.Path]::GetFullPath($installRoot)) + [System.IO.Path]::DirectorySeparatorChar
if (-not ([System.IO.Path]::GetFullPath($releasePath).StartsWith($installPrefix, [StringComparison]::OrdinalIgnoreCase))) {
    throw 'The computed VibePanel installation path is outside its per-user install folder.'
}
if ([System.String]::Equals($sourceRoot, $releasePath, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The package is already running from its managed installation folder.'
}

New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
if (-not (Test-Path -LiteralPath $releasePath)) {
    $stagingPath = "$releasePath.staging.$([Guid]::NewGuid().ToString('N'))"
    try {
        New-Item -ItemType Directory -Path $stagingPath -Force | Out-Null
        Get-ChildItem -LiteralPath $sourceRoot -Force |
            ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $stagingPath -Recurse -Force }

        if (Test-Path -LiteralPath $releasePath) {
            throw 'Another VibePanel installation created this release while the package was being copied.'
        }
        Move-Item -LiteralPath $stagingPath -Destination $releasePath
    }
    finally {
        if (Test-Path -LiteralPath $stagingPath) {
            Remove-Item -LiteralPath $stagingPath -Recurse -Force
        }
    }
}

$startupDirectory = [Environment]::GetFolderPath([Environment+SpecialFolder]::Startup)
if ([string]::IsNullOrWhiteSpace($startupDirectory)) {
    throw 'The current user does not have a Windows Startup folder.'
}
New-Item -ItemType Directory -Path $startupDirectory -Force | Out-Null

$shortcutPath = Join-Path $startupDirectory 'VibePanel.lnk'
$shortcutMarker = 'VibePanel auto-start managed by Install-VibePanel.ps1'
$backupRoot = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::ApplicationData)) 'Umberto Giacobbi.biz\VibePanel\Startup backups'
New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null

$shell = New-Object -ComObject WScript.Shell
try {
    if (Test-Path -LiteralPath $shortcutPath) {
        $existingShortcut = $shell.CreateShortcut($shortcutPath)
        if ($existingShortcut.Description -ne $shortcutMarker) {
            throw "Startup shortcut '$shortcutPath' belongs to another configuration and was left unchanged."
        }
    }

    if ($DisableStartup) {
        if (Test-Path -LiteralPath $shortcutPath) {
            $backupPath = Join-Path $backupRoot ("VibePanel-$([Guid]::NewGuid().ToString('N')).lnk")
            Move-Item -LiteralPath $shortcutPath -Destination $backupPath
            Write-Host "VibePanel automatic startup disabled. Previous shortcut saved to: $backupPath"
        }
        else {
            Write-Host 'VibePanel automatic startup was already disabled.'
        }
        return
    }

    $pendingShortcutPath = Join-Path $backupRoot ("VibePanel-$([Guid]::NewGuid().ToString('N')).pending.lnk")
    $pendingShortcut = $shell.CreateShortcut($pendingShortcutPath)
    $pendingShortcut.TargetPath = Join-Path $releasePath 'VibePanel.exe'
    $pendingShortcut.WorkingDirectory = $releasePath
    $pendingShortcut.Arguments = ''
    $pendingShortcut.Description = $shortcutMarker
    $pendingShortcut.IconLocation = (Join-Path $releasePath 'VibePanel.exe') + ',0'
    $pendingShortcut.WindowStyle = 1
    $pendingShortcut.Save()

    if (Test-Path -LiteralPath $shortcutPath) {
        $backupPath = Join-Path $backupRoot ("VibePanel-$([Guid]::NewGuid().ToString('N')).lnk")
        [System.IO.File]::Replace($pendingShortcutPath, $shortcutPath, $backupPath)
    }
    else {
        Move-Item -LiteralPath $pendingShortcutPath -Destination $shortcutPath
    }

    Write-Host "VibePanel installed for the current user at: $releasePath"
    Write-Host "VibePanel will open its studio at the next Windows sign-in: $shortcutPath"
}
finally {
    if ($null -ne $shell -and [System.Runtime.InteropServices.Marshal]::IsComObject($shell)) {
        [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)
    }
}
