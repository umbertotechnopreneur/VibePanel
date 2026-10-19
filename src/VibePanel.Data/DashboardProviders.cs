// VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT. VBWR E
namespace VibePanel.Core;

public interface IMediaProvider
{
    Task<NowPlaying> ReadAsync();
    Task ControlAsync(string action);
}
public interface IVolumeProvider : IDisposable
{
    IReadOnlyList<AppVolume> Read();
    IReadOnlyDictionary<string, double?> ReadLevels();
}
public interface IMetricsProvider : IDisposable
{
    HardwareSensorSnapshot ReadSensors();
    MachineMetrics Read();
}
public interface IPlaybackProvider : IDisposable
{
    PlaybackAnalysis Read();
    PlaybackAnalysis Snapshot();
    void Stop();
}
public interface IArtistProvider : IDisposable { ArtistProfile? Read(NowPlaying media); }
public interface INetworkProvider { NetworkTraffic Read(); }

/// <summary>The host supplies platform implementations; the data layer never selects or opens hardware.</summary>
public sealed record DashboardProviders(IMediaProvider Media, IVolumeProvider Audio, IMetricsProvider Metrics,
    IPlaybackProvider Playback, INetworkProvider Network, IArtistProvider? Artists = null);
