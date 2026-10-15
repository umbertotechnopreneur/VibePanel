// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

namespace VibePanel.Core;

/// <summary>Shared local data sources for both desktop and CLI. No Spotify API, login, or telemetry.</summary>
public sealed class DashboardService : IDisposable
{
    public WindowsMediaService Media { get; } = new();
    public WindowsVolumeService Audio { get; } = new();
    private readonly WindowsMetricsService metrics = new();

    public async Task<DashboardSnapshot> ReadAsync()
    {
        var notices = new List<string>();
        var media = NowPlaying.Empty;
        IReadOnlyList<AppVolume> volumes = [];
        MachineMetrics machine;
        try { media = await Media.ReadAsync(); }
        catch (Exception error) { notices.Add($"Media unavailable: {error.Message}"); }
        try { volumes = await Task.Run(Audio.Read); }
        catch (Exception error) { notices.Add($"Mixer unavailable: {error.Message}"); }
        try { machine = await Task.Run(metrics.Read); }
        catch (Exception error)
        {
            notices.Add($"Sensors unavailable: {error.Message}");
            machine = new(null, null, null, null, "GPU", "Unavailable", "Unavailable");
        }
        return new(media, volumes, machine, notices);
    }

    public void Dispose() { Audio.Dispose(); metrics.Dispose(); }
}
