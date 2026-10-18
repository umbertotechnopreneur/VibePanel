# VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT. VBWR E
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Path,
    [Parameter(Mandatory)][string]$Source
)
$ErrorActionPreference = 'Stop'
$target = (Resolve-Path -LiteralPath $Path).Path
$status = Get-MpComputerStatus
if (-not $status.AntivirusEnabled) { throw 'Microsoft Defender is unavailable or disabled.' }
$scanner = Join-Path $env:ProgramData 'Microsoft/Windows Defender/Platform/CurrentVersion/MpCmdRun.exe'
if (-not (Test-Path -LiteralPath $scanner)) { throw 'Microsoft Defender command-line scanner is unavailable.' }
Write-Host "Defender source: $Source; target: $target"
& $scanner -Scan -ScanType 3 -File $target -DisableRemediation
if ($LASTEXITCODE -ne 0) { throw "Defender did not complete cleanly for $target (exit $LASTEXITCODE). Import stopped." }
$files = @(if (Test-Path -LiteralPath $target -PathType Leaf) { Get-Item -LiteralPath $target }
    else { Get-ChildItem -LiteralPath $target -File -Recurse -Force })
$record = @{
    utc = [DateTime]::UtcNow.ToString('o'); source = $Source; target = $target
    command = "MpCmdRun.exe -Scan -ScanType 3 -File `"$target`" -DisableRemediation"
    result = 'Completed without detections (exit 0)'
    files = @($files | ForEach-Object { @{ path = $_.FullName; sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } })
}
$evidence = Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts/security'
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
$record | ConvertTo-Json -Depth 5 -Compress | Add-Content -LiteralPath (Join-Path $evidence 'scans.jsonl')
