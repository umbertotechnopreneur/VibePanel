# VBWR B
# Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
# Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
# VBWR E
param([ValidateSet('x64', 'ARM64')][string]$Architecture = 'x64')
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$runtime = 'win-' + $Architecture.ToLowerInvariant()
$destination = Join-Path $repo "artifacts/portable/VibePanel-$runtime"
$engine = Join-Path $destination 'engine'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $repo 'artifacts/nuget-http'
foreach ($target in @(@{ Project = 'Cli'; Folder = $engine }, @{ Project = 'Photino'; Folder = $destination })) {
    $project = Join-Path $repo "src/VibePanel.$($target.Project)/VibePanel.$($target.Project).csproj"
    dotnet publish $project --configuration Release --runtime $runtime --self-contained true -p:Platform=$Architecture --output $target.Folder --warnaserror
    if ($LASTEXITCODE -ne 0) { throw "Publish failed: $($target.Project) / $runtime" }
}
foreach ($document in @('LICENSE', 'THIRD_PARTY_NOTICES.md', 'TRADEMARKS.md', 'PORTABLE.md')) {
    Copy-Item -LiteralPath (Join-Path $repo $document) -Destination $destination -Force
}
Copy-Item -LiteralPath (Join-Path $repo 'licenses') -Destination $destination -Recurse -Force
$packages = Join-Path $repo 'artifacts/packages'
New-Item -ItemType Directory -Path $packages -Force | Out-Null
$archive = Join-Path $packages "VibePanel-$runtime-preview.zip"
Compress-Archive -LiteralPath $destination -DestinationPath $archive -Force
Get-FileHash -LiteralPath $archive -Algorithm SHA256 | Format-List
Write-Host "Portable ZIP: $archive. No tests or USB commands executed."
