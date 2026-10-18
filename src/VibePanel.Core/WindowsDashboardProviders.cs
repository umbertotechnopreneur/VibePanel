// VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT. VBWR E
namespace VibePanel.Core;

/// <summary>Windows composition root. Construction does not sample devices.</summary>
public static class WindowsDashboardProviders
{
    public static DashboardProviders Create() => new(new WindowsMediaService(), new WindowsVolumeService(),
        new WindowsMetricsService(allowDriverLifecycle: true), new WindowsPlaybackAnalyzer(), new NetworkTrafficService());
}
