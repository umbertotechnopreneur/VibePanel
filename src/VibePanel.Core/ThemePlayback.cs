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
        bool light = false, bool ambientDrift = false, string fontFamily = "Cascadia Mono", double fontScale = 1)
    {
        if (duration is { } requestedDuration && (requestedDuration <= TimeSpan.Zero || requestedDuration > TimeSpan.FromDays(1)))
        { throw new ArgumentOutOfRangeException(nameof(duration), "Duration must be between one second and one day."); }
        // Validate explicit geometry before opening hardware.
        var requestedProfile = profileId == "auto" ? null : DisplayProfileCatalog.GetRequired(profileId);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (duration is { } limit) { stop.CancelAfter(limit); }
        var token = stop.Token;
        using var service = new DashboardService();
        using var renderer = new PythonThemeRenderer();
        using var session = PanelSession.Open(device);
        try
        {
            progress?.Report($"Reading the selected panel's profile: {device.Id}.");
            var probe = await session.ProbeAsync(token);
            if (!probe.IsCompatible) { throw new IOException(probe.Message); }
            var profile = requestedProfile ?? probe.DetectedProfile ?? throw new NotSupportedException("Unknown panel geometry. Choose --profile from 'vp profiles'.");
            progress?.Report($"{theme.Name} · {profile.Name} · target 1 frame/second. Ctrl+C stops.");
            long frame = 0;
            while (!token.IsCancellationRequested)
            {
                var started = Environment.TickCount64;
                var snapshot = await service.ReadAsync(includeSystem: false, includeMedia: theme.Category != "codex");
                token.ThrowIfCancellationRequested();
                var jpeg = await renderer.RenderAsync(theme, snapshot, profile with { Encoding = PanelPixelEncoding.Jpeg, WireRotation = 0 },
                    light, ambientDrift, fontFamily, fontScale, token);
                var payload = profile is { Encoding: PanelPixelEncoding.Jpeg, WireRotation: 0 }
                    ? jpeg : DashboardRenderer.Fade(jpeg, jpeg, profile, 1);
                var packet = Thermalright04165302Protocol.CreatePicturePacket(profile, payload);
                await session.SendPacketAsync(packet, token);
                progress?.Report($"{theme.Name} · frame {++frame} submitted · {profile.Width} × {profile.Height} · {packet.Length:N0} bytes");
                await Task.Delay((int)Math.Max(1, 1000 - (Environment.TickCount64 - started)), token);
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        progress?.Report("Theme playback stopped.");
    }
}
