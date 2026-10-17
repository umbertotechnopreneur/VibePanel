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
    // The owner approved the original library's WinRing0 lifecycle. Construction still opens no hardware.
    private readonly WindowsMetricsService metrics = new(allowDriverLifecycle: true);
    private readonly NetworkTrafficService network = new();
    private readonly WindowsPlaybackAnalyzer playback = new();

    public async Task<DashboardSnapshot> ReadAsync(bool includeSystem = true, bool includeMedia = true, bool analyzePlayback = false,
        bool includeSensors = false)
    {
        var includePlayback = analyzePlayback && includeMedia;
        // The constructor does not open an audio device. Only this explicit multimedia path does.
        if (includePlayback) { await Task.Run(playback.Read); }
        else { await Task.Run(playback.Stop); }
        var notices = new List<string>();
        var media = NowPlaying.Empty;
        IReadOnlyList<AppVolume> volumes = [];
        MachineMetrics machine;
        HardwareSensorSnapshot? sensors = null;
        if (includeMedia)
        {
            try { media = await Media.ReadAsync(); }
            catch (Exception error) { notices.Add($"Media unavailable: {error.Message}"); }
            try { volumes = await Task.Run(Audio.Read); }
            catch (Exception error) { notices.Add($"Mixer unavailable: {error.Message}"); }
        }
        if (includeSensors)
        {
            try { sensors = await Task.Run(metrics.ReadSensors); }
            catch (Exception error)
            {
                sensors = HardwareSensorSnapshot.Empty with { Notice = "Sensors unavailable: " + error.Message };
            }
            if (!string.IsNullOrWhiteSpace(sensors.Notice)) { notices.Add(sensors.Notice); }
        }
        if (!includeSystem)
        {
            return new(media, volumes, new(null, null, null, null, "", "Not sampled for themes", "Not sampled for themes"), notices,
                Playback: includePlayback ? playback.Snapshot() : null, Sensors: sensors);
        }
        try { machine = await Task.Run(metrics.Read); }
        catch (Exception error)
        {
            notices.Add($"Sensors unavailable: {error.Message}");
            machine = new(null, null, null, null, "GPU", "Unavailable", "Unavailable");
        }
        NetworkTraffic? traffic = null;
        try { traffic = network.Read(); }
        catch (Exception error) { notices.Add($"Network counters unavailable: {error.Message}"); }
        return new(media, volumes, machine, notices, traffic, includePlayback ? playback.Snapshot() : null, sensors);
    }

    public void Dispose()
    {
        try { playback.Dispose(); }
        finally
        {
            try { Audio.Dispose(); }
            finally { metrics.Dispose(); }
        }
    }
}
