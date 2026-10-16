// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

namespace VibePanel.Core;

/// <summary>Explicit theme output to one selected panel; never reconnects or starts from a preview request.</summary>
public static class ThemePlayback
{
    public static async Task RunAsync(ThemeDefinition theme, DisplayDevice device, string profileId,
        TimeSpan? duration, IProgress<string>? progress, CancellationToken cancellationToken,
        bool light = false, bool ambientDrift = false, string fontFamily = "Cascadia Mono", double fontScale = 1,
        int refreshIntervalMilliseconds = 1000, string jpegQuality = "high")
    {
        if (duration is { } requestedDuration && (requestedDuration <= TimeSpan.Zero || requestedDuration > TimeSpan.FromDays(1)))
        { throw new ArgumentOutOfRangeException(nameof(duration), "Duration must be between one second and one day."); }
        if (refreshIntervalMilliseconds is < 50 or > 1000)
        { throw new ArgumentOutOfRangeException(nameof(refreshIntervalMilliseconds), "Refresh interval must be between 50 and 1000 ms."); }
        if (jpegQuality is not ("low" or "medium" or "high")) { throw new ArgumentException("JPEG quality must be low, medium or high."); }
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (duration is { } limit) { stop.CancelAfter(limit); }
        var token = stop.Token;
        using var service = new DashboardService();
        using var renderer = new PythonThemeRenderer();
        try
        {
            progress?.Report($"Reading the selected panel's profile: {device.Id}.");
            using var output = await ThemePanelOutput.OpenAsync(device, profileId, token);
            var profile = output.Profile;
            progress?.Report($"{theme.Name} · {profile.Name} · requested {refreshIntervalMilliseconds} ms. Ctrl+C stops.");
            while (!token.IsCancellationRequested)
            {
                var started = Environment.TickCount64;
                var snapshot = await service.ReadAsync(includeSystem: false, includeMedia: theme.Category != "codex");
                token.ThrowIfCancellationRequested();
                var jpeg = await renderer.RenderAsync(theme, snapshot, profile with { Encoding = PanelPixelEncoding.Jpeg, WireRotation = 0 },
                    light, ambientDrift, fontFamily, fontScale, jpegQuality, token);
                if (await output.SendAsync(jpeg, jpegQuality, token))
                { progress?.Report($"{theme.Name} · frame {output.SubmittedFrames} submitted · {profile.Width} × {profile.Height}"); }
                // One in-flight frame only. Slow rendering/USB reduces the rate instead of building a queue.
                await Task.Delay((int)Math.Max(1, refreshIntervalMilliseconds - (Environment.TickCount64 - started)), token);
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        progress?.Report("Theme playback stopped.");
    }
}
