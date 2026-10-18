# VBWR B
# Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
# Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
# VBWR E
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-NoReparsePath([string]$Path) {
    $current = [System.IO.Path]::GetFullPath($Path).TrimEnd([char[]]@('\', '/'))
    while (-not [string]::IsNullOrWhiteSpace($current)) {
        if (Test-Path -LiteralPath $current) {
            $item = Get-Item -LiteralPath $current -Force
            if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "A link or junction was found at '$current'. Nothing at that path was removed."
            }
        }
        $parent = [System.IO.Path]::GetDirectoryName($current)
        if ($parent -eq $current) { break }
        $current = $parent
    }
}

function Assert-ReleasePath([string]$Path, [string]$Root) {
    $fullPath = [System.IO.Path]::GetFullPath($Path).TrimEnd('\')
    if (-not [string]::Equals([System.IO.Path]::GetDirectoryName($fullPath), $Root, [StringComparison]::OrdinalIgnoreCase) -or
        [System.IO.Path]::GetFileName($fullPath) -notmatch '^VibePanel-[0-9a-f]{12}$') {
        throw "The folder '$fullPath' is not a managed VibePanel release. It was left unchanged."
    }
    Assert-NoReparsePath $fullPath
    if (-not (Test-Path -LiteralPath (Join-Path $fullPath 'VibePanel.exe') -PathType Leaf) -or
        -not (Test-Path -LiteralPath (Join-Path $fullPath 'Install-VibePanel.ps1') -PathType Leaf)) {
        throw "The folder '$fullPath' does not contain a VibePanel package. It was left unchanged."
    }
    if (Get-ChildItem -LiteralPath $fullPath -Recurse -Force | Where-Object { ($_.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0 } | Select-Object -First 1) {
        throw "The folder '$fullPath' contains a link or junction. It was left unchanged."
    }
}

function Assert-AppStopped([string]$Root) {
    $prefix = $Root.TrimEnd('\') + '\'
    foreach ($process in (Get-CimInstance Win32_Process -Filter "Name='VibePanel.exe' OR Name='vp.exe' OR Name='python.exe' OR Name='dotnet.exe'")) {
        if ([string]::IsNullOrWhiteSpace($process.ExecutablePath)) {
            if ($process.Name -in @('VibePanel.exe', 'vp.exe')) { throw 'I could not check a running Vibe Panel process. Close it and try again.' }
            continue
        }
        if ([System.IO.Path]::GetFullPath($process.ExecutablePath).StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Close Vibe Panel from its tray menu before uninstalling it. No app was stopped.'
        }
    }
}

$localAppData = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
$roamingAppData = [Environment]::GetFolderPath([Environment+SpecialFolder]::ApplicationData)
$startupDirectory = [Environment]::GetFolderPath([Environment+SpecialFolder]::Startup)
$programsDirectory = [Environment]::GetFolderPath([Environment+SpecialFolder]::Programs)
foreach ($folder in @($localAppData, $roamingAppData, $startupDirectory, $programsDirectory)) {
    if ([string]::IsNullOrWhiteSpace($folder)) { throw 'A required current-user Windows folder is unavailable.' }
    Assert-NoReparsePath $folder
}
$installRoot = [System.IO.Path]::GetFullPath((Join-Path $localAppData 'Umberto Giacobbi.biz\Apps\VibePanel'))
Assert-NoReparsePath $installRoot
$launcherPath = Join-Path $installRoot 'VibePanel.cmd'
$launcherIcon = Join-Path $installRoot 'VibePanel.ico'
$installPrefix = $installRoot.TrimEnd('\') + '\'
foreach ($path in @($launcherPath, $launcherIcon)) {
    $fullPath = [System.IO.Path]::GetFullPath($path)
    if (-not $fullPath.StartsWith($installPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "The managed launcher file '$fullPath' is outside the VibePanel install folder."
    }
    Assert-NoReparsePath $fullPath
}
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\VibePanel'
$registered = $null
$registeredPath = ''
if (Test-Path -LiteralPath $uninstallKey) {
    $registered = Get-ItemProperty -LiteralPath $uninstallKey
    if ($registered.PSObject.Properties.Name -notcontains 'VibePanelManaged' -or $registered.VibePanelManaged -ne 1) {
        throw 'This uninstall entry belongs to another configuration and was left unchanged.'
    }
    if ($registered.PSObject.Properties.Name -notcontains 'InstallLocation') { throw 'The install entry has no folder. It was left unchanged.' }
    $registeredPath = [System.IO.Path]::GetFullPath($registered.InstallLocation)
    if (-not $registeredPath.StartsWith($installRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The registered install folder is outside the VibePanel install folder. It was left unchanged.'
    }
    if (Test-Path -LiteralPath $registeredPath) { Assert-ReleasePath $registeredPath $installRoot }
    $registryItem = Get-Item -LiteralPath $uninstallKey
    try { if ($registryItem.SubKeyCount -ne 0) { throw 'The uninstall entry contains unexpected subkeys and was left unchanged.' } }
    finally { $registryItem.Close() }
}

# Keep removed releases recoverable, outside Apps\VibePanel and on the same volume.
$backupParent = Join-Path $localAppData 'Umberto Giacobbi.biz\VibePanel\Uninstall backups'
$backupRoot = Join-Path $backupParent ([Guid]::NewGuid().ToString('N'))
Assert-NoReparsePath $backupRoot
if (-not ([System.IO.Path]::GetFullPath($backupRoot).StartsWith($localAppData.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase))) {
    throw 'The uninstall backup is outside the current user LocalAppData folder.'
}
$releases = @()
$launcherFiles = @(@($launcherPath, $launcherIcon) | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf })
if (Test-Path -LiteralPath $installRoot) {
    $releases = @(Get-ChildItem -LiteralPath $installRoot -Directory -Force |
        Where-Object { $_.Name -match '^VibePanel-[0-9a-f]{12}$' } |
        Sort-Object @{ Expression = { [string]::Equals($_.FullName, $registeredPath, [StringComparison]::OrdinalIgnoreCase) } }, Name)
    foreach ($release in $releases) { Assert-ReleasePath $release.FullName $installRoot }
}
Assert-AppStopped $installRoot
$shell = New-Object -ComObject WScript.Shell
try {
    $shortcuts = @(
        @{ Path = (Join-Path $startupDirectory 'VibePanel.lnk'); Root = $startupDirectory; Marker = 'VibePanel auto-start managed by Install-VibePanel.ps1'; Backup = 'Startup-VibePanel.lnk' },
        @{ Path = (Join-Path $programsDirectory 'Vibe Panel.lnk'); Root = $programsDirectory; Marker = 'VibePanel Start menu managed by Install-VibePanel.ps1'; Backup = 'Start-VibePanel.lnk' }
    )
    $ownedShortcuts = @()
    foreach ($entry in $shortcuts) {
        $fullPath = [System.IO.Path]::GetFullPath($entry.Path)
        $prefix = [System.IO.Path]::GetFullPath($entry.Root).TrimEnd('\') + '\'
        if (-not $fullPath.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'The shortcut is outside its Windows folder.' }
        Assert-NoReparsePath $fullPath
        if (-not (Test-Path -LiteralPath $fullPath)) { continue }
        $shortcut = $shell.CreateShortcut($fullPath)
        try {
            if ($shortcut.Description -ne $entry.Marker) {
                throw "Shortcut '$fullPath' belongs to another configuration and was left unchanged."
            }
            $targetPath = [System.IO.Path]::GetFullPath($shortcut.TargetPath)
            if (-not [string]::Equals($targetPath, $launcherPath, [StringComparison]::OrdinalIgnoreCase)) {
                throw "Shortcut '$fullPath' does not point to the stable VibePanel launcher and was left unchanged."
            }
            $ownedShortcuts += $entry
        }
        finally { [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($shortcut) }
    }
    if ($ownedShortcuts.Count -gt 0 -or $null -ne $registered -or $releases.Count -gt 0 -or $launcherFiles.Count -gt 0) {
        New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
        if ($null -ne $registered) { $registered | Export-Clixml -LiteralPath (Join-Path $backupRoot 'Uninstall-entry.xml') }
        $movedReleases = @()
        try {
            foreach ($release in $releases) {
                Assert-AppStopped $installRoot
                Assert-ReleasePath $release.FullName $installRoot
                $archivePath = Join-Path $backupRoot $release.Name
                Assert-NoReparsePath $archivePath
                if (-not ([System.IO.Path]::GetFullPath($archivePath).StartsWith($backupRoot + '\', [StringComparison]::OrdinalIgnoreCase)) -or
                    (Test-Path -LiteralPath $archivePath)) {
                    throw 'The release backup path is outside the backup folder or already exists.'
                }
                [System.IO.Directory]::Move($release.FullName, $archivePath)
                $movedReleases += @{ Source = $release.FullName; Archive = $archivePath }
            }
        }
        catch {
            $archiveError = $_
            [array]::Reverse($movedReleases)
            foreach ($moved in $movedReleases) {
                try {
                    if (-not [string]::Equals([System.IO.Path]::GetDirectoryName($moved.Source), $installRoot, [StringComparison]::OrdinalIgnoreCase)) {
                        throw 'The original release path is outside the install folder.'
                    }
                    Assert-NoReparsePath $moved.Source
                    Assert-ReleasePath $moved.Archive $backupRoot
                    if (Test-Path -LiteralPath $moved.Source) { throw 'The original release path is now occupied.' }
                    [System.IO.Directory]::Move($moved.Archive, $moved.Source)
                }
                catch { Write-Warning "A release is still recoverable at '$($moved.Archive)': $($_.Exception.Message)" }
            }
            throw $archiveError
        }
        foreach ($entry in $ownedShortcuts) {
            Assert-NoReparsePath $entry.Path
            $backupPath = Join-Path $backupRoot $entry.Backup
            Assert-NoReparsePath $backupPath
            if (-not ([System.IO.Path]::GetFullPath($backupPath).StartsWith($backupRoot + '\', [StringComparison]::OrdinalIgnoreCase))) {
                throw 'The shortcut backup is outside its backup folder.'
            }
            Move-Item -LiteralPath $entry.Path -Destination $backupPath
        }
        foreach ($file in $launcherFiles) {
            Assert-NoReparsePath $file
            $backupPath = Join-Path $backupRoot ([System.IO.Path]::GetFileName($file))
            Assert-NoReparsePath $backupPath
            if (-not ([System.IO.Path]::GetFullPath($backupPath).StartsWith($backupRoot + '\', [StringComparison]::OrdinalIgnoreCase)) -or
                (Test-Path -LiteralPath $backupPath)) {
                throw 'The launcher backup path is outside the backup folder or already exists.'
            }
            Move-Item -LiteralPath $file -Destination $backupPath
        }
        if ($null -ne $registered) { Remove-Item -LiteralPath $uninstallKey }
        Write-Host "Vibe Panel was removed from Start, startup and its install folder. Backup: $backupRoot"
    }
    else { Write-Host 'Vibe Panel was already removed from Start and Windows startup.' }
    Write-Host 'Your settings, user themes and Codex plugin were kept. Removed app files are in the backup.'
}
finally {
    if ($null -ne $shell -and [System.Runtime.InteropServices.Marshal]::IsComObject($shell)) {
        [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)
    }
}
