// VBWR B — Thermalright VibeWare. Copyright (c) 2026 Umberto Giacobbi. MIT. VBWR E
namespace ThermalrightVibeWare.Core;

/// <summary>Windows composition root. Construction does not sample devices.</summary>
public static class WindowsDashboardProviders
{
    public static DashboardProviders Create(IMetricsProvider? metrics = null) => new(new WindowsMediaService(), new WindowsVolumeService(),
        metrics ?? new WindowsMetricsService(allowHardwareAccess: true), new WindowsPlaybackAnalyzer(), new NetworkTrafficService(), new OnlineArtistProvider());
}
