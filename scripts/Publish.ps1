# VBWR B
# Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
# Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
# VBWR E
param([ValidateSet('x64', 'ARM64')][string]$Architecture = 'x64')
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$runtime = 'win-' + $Architecture.ToLowerInvariant()
foreach ($target in @(@{ Project = 'Cli'; Folder = 'cli' }, @{ Project = 'App'; Folder = 'desktop' })) {
    $project = Join-Path $repo "src/VibePanel.$($target.Project)/VibePanel.$($target.Project).csproj"
    $destination = Join-Path $repo "artifacts/portable/$runtime/$($target.Folder)"
    dotnet publish $project --configuration Release --runtime $runtime --self-contained true -p:Platform=$Architecture --output $destination --warnaserror
    if ($LASTEXITCODE -ne 0) { throw "Publish failed: $($target.Project) / $runtime" }
}
Write-Host "Portable folders: $repo/artifacts/portable/$runtime (keep each folder intact). No tests executed."
