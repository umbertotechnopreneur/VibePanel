# VBWR B
# Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
# Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
# VBWR E
# Run only with the owner's explicit approval. The fixture cannot access USB.
#requires -Version 7.0
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$output = Join-Path $repo 'artifacts/memory-checks'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$exe = Join-Path $repo 'artifacts/bin/VibePanel.MemoryChecks/x64/Release/net10.0-windows10.0.19041.0/VibePanel.MemoryChecks.exe'
$process = Start-Process -FilePath $exe -PassThru -WindowStyle Hidden -RedirectStandardOutput "$output/ui.stdout.txt" -RedirectStandardError "$output/ui.stderr.txt"
$records = [System.Collections.Generic.List[object]]::new()
$failure = $null
try {
    for ($sample = 0; $sample -lt 160; $sample++) {
        if ($process.HasExited) { break }
        $all = Get-CimInstance Win32_Process
        $ids = [System.Collections.Generic.HashSet[int]]::new()
        $null = $ids.Add($process.Id)
        for ($depth = 0; $depth -lt 5; $depth++) {
            foreach ($item in $all) { if ($ids.Contains([int]$item.ParentProcessId)) { $null = $ids.Add([int]$item.ProcessId) } }
        }
        $parts = @(foreach ($item in $all) {
            if ($ids.Contains([int]$item.ProcessId)) {
                [pscustomobject]@{ id = [int]$item.ProcessId; parent = [int]$item.ParentProcessId; name = $item.Name; privateBytes = [long]$item.PrivatePageCount
                    role = if ($item.CommandLine -match '--type=([^ ]+)') { $Matches[1] } elseif ($item.CommandLine -match '--memory-guard') { 'guard' } else { 'main' } }
            }
        })
        $total = ($parts | Measure-Object -Property privateBytes -Sum).Sum
        $entry = [pscustomobject]@{ sample = $sample; utc = [datetime]::UtcNow; totalPrivate = $total; processes = $parts }
        $records.Add($entry)
        if ($sample % 10 -eq 0) { [pscustomobject]@{ sample = $sample; totalPrivate = $total } | ConvertTo-Json -Compress }
        if ($total -gt 900000000) { $failure = 'Offline check reached 900 MB; stopping before another guard popup.'; break }
        Start-Sleep -Seconds 1
    }
    if (-not $process.HasExited -and $null -eq $failure) { $failure = 'Offline UI check timed out.' }
} finally {
    if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
    $records | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath "$output/ui-processes.json" -Encoding utf8
}
Get-Content -LiteralPath "$output/ui.stdout.txt"
Get-Content -LiteralPath "$output/ui.stderr.txt"
if ($failure) { throw $failure }
if ($process.ExitCode -ne 0) { throw "Offline UI check exited with $($process.ExitCode)." }
$peak = ($records | Measure-Object -Property totalPrivate -Maximum).Maximum
Write-Output "Offline process-family peak private bytes: $peak"
