# VBWR B
# Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
# Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
# VBWR E
param(
    [ValidateSet('x64', 'ARM64')][string]$Architecture = 'x64',
    [string]$HostPython = 'python'
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$runtime = 'win-' + $Architecture.ToLowerInvariant()
$packageName = "VibePanel-$runtime-preview"
$stagingRoot = Join-Path $repo 'artifacts/staging'
$destination = Join-Path $stagingRoot ("$packageName-" + [Guid]::NewGuid().ToString('N'))
$engine = Join-Path $destination 'engine'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $repo 'artifacts/nuget-http'
foreach ($target in @(@{ Project = 'Cli'; Folder = $engine }, @{ Project = 'Photino'; Folder = $destination })) {
    $project = Join-Path $repo "src/VibePanel.$($target.Project)/VibePanel.$($target.Project).csproj"
    dotnet publish $project --configuration Release --runtime $runtime --self-contained true -p:Platform=$Architecture --output $target.Folder --warnaserror
    if ($LASTEXITCODE -ne 0) { throw "Publish failed: $($target.Project) / $runtime" }
}
& (Join-Path $PSScriptRoot 'Bundle-Python.ps1') -Architecture $Architecture -Destination $destination -HostPython $HostPython
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Install-Local.ps1') -Destination (Join-Path $destination 'Install-VibePanel.ps1') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Uninstall-Local.ps1') -Destination (Join-Path $destination 'Uninstall-VibePanel.ps1') -Force
foreach ($document in @('README.md', 'LICENSE', 'THIRD_PARTY_NOTICES.md', 'TRADEMARKS.md', 'PORTABLE.md', 'cave.md', 'lesson.md')) {
    Copy-Item -LiteralPath (Join-Path $repo $document) -Destination $destination -Force
}
Copy-Item -LiteralPath (Join-Path $repo 'licenses') -Destination $destination -Recurse -Force
# Ship the exact modified MPL library sources with the binary, plus a standalone build project.
$ohmSourceBundle = Join-Path $stagingRoot ('ohm-source-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $ohmSourceBundle -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repo 'artifacts/native/openhardwaremonitor/source') -Destination $ohmSourceBundle -Recurse
[xml]$ohmProject = Get-Content -LiteralPath (Join-Path $repo 'native/OpenHardwareMonitor/OpenHardwareMonitor.csproj') -Raw
$ohmProject.Project.PropertyGroup.OhmSource = '$(MSBuildThisFileDirectory)source'
$ohmProject.Project.Target.RemoveChild($ohmProject.Project.Target.Exec) | Out-Null
$ohmProject.Save((Join-Path $ohmSourceBundle 'OpenHardwareMonitor.csproj'))
Copy-Item -LiteralPath (Join-Path $repo 'native/OpenHardwareMonitor/README.md') -Destination $ohmSourceBundle
Copy-Item -LiteralPath (Join-Path $repo 'artifacts/native/openhardwaremonitor/prepared.json') -Destination $ohmSourceBundle
Compress-Archive -Path (Join-Path $ohmSourceBundle '*') -DestinationPath (Join-Path $destination 'licenses/OpenHardwareMonitor-source.zip')
$pythonLicense = Join-Path $engine 'python/LICENSE.txt'
if (-not (Test-Path -LiteralPath $pythonLicense)) { throw 'Bundled Python license is missing.' }
Copy-Item -LiteralPath $pythonLicense -Destination (Join-Path $destination 'licenses/Python-PSF-2.0.txt') -Force
# The CLI publish already places the canonical theme catalog under engine/themes.
# Do not add a second root/themes copy that users might edit instead of the active catalog.
if (-not (Test-Path -LiteralPath (Join-Path $engine 'themes'))) {
    throw 'CLI publish did not include engine/themes.'
}
if (Test-Path -LiteralPath (Join-Path $destination 'themes')) {
    throw 'A stale root/themes folder duplicates engine/themes. Remove only that generated folder before repackaging.'
}
if (-not (Test-Path -LiteralPath (Join-Path $engine 'plugins/vibepanel-codex/install.py'))) {
    throw 'CLI publish did not include engine/plugins/vibepanel-codex.'
}
foreach ($folder in @('docs')) {
    $target = Join-Path $destination $folder
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    $source = Join-Path $repo ($folder + '/*')
    Copy-Item -Path $source -Destination $target -Recurse -Force
}
$packages = Join-Path $repo 'artifacts/packages'
New-Item -ItemType Directory -Path $packages -Force | Out-Null
$archive = Join-Path $packages "$packageName.zip"
Compress-Archive -LiteralPath $destination -DestinationPath $archive -Force
Get-FileHash -LiteralPath $archive -Algorithm SHA256 | Format-List
Write-Host "Portable ZIP: $archive. Staging: $destination. No tests or USB commands executed."
