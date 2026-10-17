# VBWR B
# Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
# Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
# VBWR E
#requires -Version 7.0
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$commit = '09c16898a4a84c36644e7c07baebf1efd299691e'
$archiveHash = '8E2B61432779871C03C3225F72F8BA936E27B33916C3F8DEA6C062B54D73ACD2'
$root = Join-Path $repo 'artifacts/native/openhardwaremonitor'
$source = Join-Path $root 'source'
$manifest = Join-Path $root 'prepared.json'
$patchHash = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash
if (Test-Path -LiteralPath $manifest) {
    $previous = Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
    if ($previous.patchHash -eq $patchHash -and (Test-Path -LiteralPath (Join-Path $source 'Hardware/Computer.cs'))) { return }
}
New-Item -ItemType Directory -Path $root -Force | Out-Null
$archive = Join-Path $root 'upstream.zip'
if (-not (Test-Path -LiteralPath $archive)) {
    Invoke-WebRequest -Uri "https://codeload.github.com/openhardwaremonitor/openhardwaremonitor/zip/$commit" -OutFile $archive
}
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $archiveHash) { throw 'Open Hardware Monitor source checksum mismatch.' }
$unpacked = Join-Path $root ('unpack-' + [Guid]::NewGuid().ToString('N'))
[IO.Compression.ZipFile]::ExtractToDirectory($archive, $unpacked)
$original = Join-Path $unpacked "openhardwaremonitor-$commit"
New-Item -ItemType Directory -Path $source -Force | Out-Null
# Only the library source is copied. Upstream MPL notices remain intact.
foreach ($folder in @('Hardware', 'Collections', 'Properties', 'Licenses')) {
    Copy-Item -LiteralPath (Join-Path $original $folder) -Destination $source -Recurse -Force
}
Copy-Item -LiteralPath (Join-Path $original 'OpenHardwareMonitorLib.csproj') -Destination (Join-Path $source 'Upstream.csproj') -Force
function Replace-Required([string]$Text, [string]$Old, [string]$New) {
    if (-not $Text.Contains($Old)) { throw "Pinned Open Hardware Monitor patch no longer matches: $Old" }
    return $Text.Replace($Old, $New)
}
function Save-Source([string]$Path, [string]$Text) {
    $notice = "// Modified for Vibe Panel: .NET 10 API compatibility, 17 October 2026. Original MPL-2.0 terms apply.`n"
    [IO.File]::WriteAllText($Path, $notice + $Text, [Text.UTF8Encoding]::new($false))
}
function Read-Source([string]$Path) {
    $bytes = [IO.File]::ReadAllBytes($Path)
    try { return [Text.UTF8Encoding]::new($false, $true).GetString($bytes) }
    catch [Text.DecoderFallbackException] { return [Text.Encoding]::Latin1.GetString($bytes) }
}
$path = Join-Path $source 'Hardware/Ring0.cs'
$text = Read-Source $path
$text = Replace-Required $text 'Mutex.OpenExisting(' 'MutexAcl.OpenExisting('
Save-Source $path $text
$path = Join-Path $source 'Hardware/KernelDriver.cs'
$text = Read-Source $path
$text = Replace-Required $text 'File.GetAccessControl(@"\\.\" + id)' '(new FileInfo(@"\\.\" + id)).GetAccessControl()'
$text = Replace-Required $text 'File.SetAccessControl(@"\\.\" + id, fileSecurity)' '(new FileInfo(@"\\.\" + id)).SetAccessControl(fileSecurity)'
Save-Source $path $text
$path = Join-Path $source 'Hardware/Computer.cs'
$text = Read-Source $path
$text = Replace-Required $text 'using System.Security.Permissions;' ''
# Code access security is unsupported in modern .NET. This declarative LinkDemand
# does not replace Windows access checks, which remain in the original driver code.
$text = Replace-Required $text '[SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]' ''
Save-Source $path $text
$path = Join-Path $source 'Hardware/PInvokeDelegateFactory.cs'
$text = Read-Source $path
$text = Replace-Required $text 'AppDomain.CurrentDomain.DefineDynamicAssembly(' 'AssemblyBuilder.DefineDynamicAssembly('
Save-Source $path $text
@{ commit = $commit; archiveHash = $archiveHash; patchHash = $patchHash } | ConvertTo-Json | Set-Content -LiteralPath $manifest
