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
        int refreshIntervalMilliseconds = 1000, string jpegQuality = "high",
        double saturationPercent = 100, double hueDegrees = 0, bool showSpectrum = true,
        double brightnessPercent = 0, double contrastPercent = 100)
    {
        if (duration is { } requestedDuration && (requestedDuration <= TimeSpan.Zero || requestedDuration > TimeSpan.FromDays(1)))
        { throw new ArgumentOutOfRangeException(nameof(duration), "Duration must be between one second and one day."); }
        if (refreshIntervalMilliseconds is < 25 or > 1000)
        { throw new ArgumentOutOfRangeException(nameof(refreshIntervalMilliseconds), "Refresh interval must be between 25 and 1000 ms."); }
        if (jpegQuality is not ("poor" or "low" or "medium" or "high")) { throw new ArgumentException("JPEG quality must be poor, low, medium or high."); }
        PythonThemeRenderer.ValidateColorAdjustments(saturationPercent, hueDegrees, brightnessPercent, contrastPercent);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (duration is { } limit) { stop.CancelAfter(limit); }
        var token = stop.Token;
        using var service = new DashboardService(WindowsDashboardProviders.Create());
        using var renderer = new PythonThemeRenderer();
        var codexProcess = new CodexProcessMonitor();
        byte[]? inactiveFrame = null;
        try
        {
            progress?.Report($"Reading the selected panel's profile: {device.Id}.");
            using var output = await ThemePanelOutput.OpenAsync(device, profileId, token);
            var profile = output.Profile;
            progress?.Report($"{theme.Name} · {profile.Name} · requested {refreshIntervalMilliseconds} ms. Ctrl+C stops.");
            while (!token.IsCancellationRequested)
            {
                var started = Environment.TickCount64;
                if (theme.Category == "codex" && !codexProcess.IsRunning())
                {
                    inactiveFrame ??= PanelFrameRenderer.Blank(profile with { Encoding = PanelPixelEncoding.Jpeg, WireRotation = 0 });
                    if (await output.SendAsync(inactiveFrame, jpegQuality, token))
                    { progress?.Report("Codex is closed. Its screen is hidden until its process is running."); }
                    await Task.Delay(1000, token);
                    continue;
                }
                var snapshot = await service.ReadAsync(includeSystem: false, includeMedia: theme.Category == "multimedia",
                    analyzePlayback: theme.Category == "multimedia", includeSensors: theme.Category == "system");
                token.ThrowIfCancellationRequested();
                var jpeg = await renderer.RenderAsync(theme, snapshot, profile with { Encoding = PanelPixelEncoding.Jpeg, WireRotation = 0 },
                    light, ambientDrift, fontFamily, fontScale, jpegQuality, token, saturationPercent, hueDegrees, showSpectrum,
                    brightnessPercent, contrastPercent);
                if (await output.SendAsync(jpeg, jpegQuality, token))
                { progress?.Report($"{theme.Name} · frame {output.SubmittedFrames} submitted · {profile.Width} × {profile.Height}"); }
                // One in-flight frame only. Slow rendering/USB reduces the rate instead of building a queue.
                var interval = renderer.FrameIntervalMilliseconds(refreshIntervalMilliseconds);
                await Task.Delay((int)Math.Max(1, interval - (Environment.TickCount64 - started)), token);
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        progress?.Report("Theme playback stopped.");
    }
}
