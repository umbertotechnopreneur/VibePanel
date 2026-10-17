# VBWR B
# Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
# Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
# VBWR E
#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PayloadPath,
    [ValidateSet('x64', 'ARM64')][string]$Architecture = 'x64',
    [string]$Version = '0.1.1'
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$payload = (Resolve-Path -LiteralPath $PayloadPath).Path
if (-not (Test-Path -LiteralPath (Join-Path $payload 'VibePanel.exe'))) { throw 'The published studio is missing.' }
$toolRoot = Join-Path $repo 'artifacts/tools/wix4'
$wix = Join-Path $toolRoot 'wix.exe'
if (-not (Test-Path -LiteralPath $wix)) {
    dotnet tool install wix --version 4.0.6 --tool-path $toolRoot --allow-roll-forward
    if ($LASTEXITCODE -ne 0) { throw 'Could not restore the pinned MSI build tool.' }
}
$generated = Join-Path $repo ('artifacts/msi/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $generated -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $generated 'VibePanel.cmd'), "@echo off`r`nstart `"`" `"%~dp0MSI\VibePanel.exe`" %*`r`n", [Text.Encoding]::ASCII)
$namespace = 'http://wixtoolset.org/schemas/v4/wxs'
$document = [Xml.XmlDocument]::new()
function Add-Element($Parent, [string]$Name, [hashtable]$Attributes) {
    $element = $document.CreateElement($Name, $namespace)
    foreach ($attribute in $Attributes.GetEnumerator()) { $element.SetAttribute($attribute.Key, [string]$attribute.Value) }
    [void]$Parent.AppendChild($element)
    return $element
}
function Get-Identifier([string]$Text) {
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($Text.ToLowerInvariant()))).Substring(0, 32)
}
$root = Add-Element $document 'Wix' @{}
$fragment = Add-Element $root 'Fragment' @{}
$directoryRoot = Add-Element $fragment 'DirectoryRef' @{ Id = 'INSTALLFOLDER' }
$group = Add-Element $fragment 'ComponentGroup' @{ Id = 'Payload' }
$directories = @{ '' = $directoryRoot }
foreach ($file in (Get-ChildItem -LiteralPath $payload -File -Recurse | Sort-Object FullName)) {
    $relative = [IO.Path]::GetRelativePath($payload, $file.FullName)
    if ($relative -in @('Install-VibePanel.ps1', 'Uninstall-VibePanel.ps1')) { continue }
    $folder = [IO.Path]::GetDirectoryName($relative)
    $current = ''
    $parent = $directoryRoot
    foreach ($part in ($folder -split '[\\/]' | Where-Object { $_ })) {
        $current = if ($current) { "$current/$part" } else { $part }
        if (-not $directories.ContainsKey($current)) {
            $directories[$current] = Add-Element $parent 'Directory' @{ Id = 'D' + (Get-Identifier $current); Name = $part }
        }
        $parent = $directories[$current]
    }
    $id = Get-Identifier $relative
    $guidBytes = [Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes("VibePanel.$Architecture.$relative"))
    $component = Add-Element $parent 'Component' @{ Id = "C$id"; Guid = [Guid]::new([byte[]]$guidBytes[0..15]).ToString(); Bitness = 'always64' }
    Add-Element $component 'File' @{ Id = "F$id"; Source = $file.FullName; Name = $file.Name } | Out-Null
    Add-Element $component 'RegistryValue' @{ Root = 'HKCU'; Key = 'Software\Umberto Giacobbi.biz\VibePanel\MSI\Files'; Name = "C$id"; Type = 'integer'; Value = '1'; KeyPath = 'yes' } | Out-Null
    Add-Element $group 'ComponentRef' @{ Id = "C$id" } | Out-Null
}
$source = Join-Path $generated 'Payload.wxs'
$document.Save($source)
$revision = (git -C $repo rev-parse --short HEAD).Trim()
$output = Join-Path $repo "artifacts/packages/VibePanel-win-$($Architecture.ToLowerInvariant())-$Version.msi"
# Build only. Do not invoke MSI validation or test commands.
& $wix build (Join-Path $repo 'installer/Package.wxs') $source -arch $Architecture.ToLowerInvariant() -d "PayloadRoot=$payload" -d "GeneratedRoot=$generated" -d "ProductVersion=$Version" -d "SourceRevision=$revision" -pdbtype none -out $output
if ($LASTEXITCODE -ne 0) { throw 'MSI compilation failed.' }
Write-Host "MSI: $output"
