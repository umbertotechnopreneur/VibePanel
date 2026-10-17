// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

namespace VibePanel.Core;

/// <summary>Explicit, cancellable dashboard output. Reuses the packet until visible values change.</summary>
public static class DashboardPlayback
{
    public static Task RunAsync(DisplayDevice device, string profileId, IProgress<string>? progress, CancellationToken cancellationToken,
        DashboardScene? fixedScene = null) => Task.Run(async () =>
    {
        using var service = new DashboardService();
        using var session = PanelSession.Open(device);
        var probe = await session.ProbeAsync(cancellationToken);
        if (!probe.IsCompatible) { throw new IOException(probe.Message); }
        var profile = profileId == "auto" ? probe.DetectedProfile ?? throw new NotSupportedException("Choose an explicit profile.") : DisplayProfileCatalog.GetRequired(profileId);
        var renderer = new DashboardRenderer();
        string? previous = null;
        byte[]? packet = null;
        byte[]? lastImage = null;
        var visibleScene = fixedScene ?? DashboardScenes.Rotation[0];
        var rotationStarted = Environment.TickCount64;
        while (!cancellationToken.IsCancellationRequested)
        {
            var started = Environment.TickCount64;
            var snapshot = await service.ReadAsync();
            cancellationToken.ThrowIfCancellationRequested();
            var scene = fixedScene ?? DashboardScenes.Rotation[(int)((Environment.TickCount64 - rotationStarted) / 20000 % DashboardScenes.Rotation.Count)];
            var fingerprint = DashboardRenderer.Fingerprint(snapshot, scene);
            if (previous != fingerprint)
            {
                var jpeg = renderer.Render(snapshot, profile with { Encoding = PanelPixelEncoding.Jpeg, WireRotation = 0 }, scene);
                if (scene != visibleScene && lastImage is not null)
                {
                    for (var step = 1; step < 6; step++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var frame = DashboardRenderer.Fade(lastImage, jpeg, profile, step / 6f);
                        await session.SendPacketAsync(Thermalright04165302Protocol.CreatePicturePacket(profile, frame), cancellationToken);
                        await Task.Delay(100, cancellationToken);
                    }
                }
                var payload = profile is { Encoding: PanelPixelEncoding.Jpeg, WireRotation: 0 } ? jpeg : DashboardRenderer.Fade(jpeg, jpeg, profile, 1);
                packet = Thermalright04165302Protocol.CreatePicturePacket(profile, payload);
                lastImage = jpeg;
                visibleScene = scene;
                previous = fingerprint;
            }
            await session.SendPacketAsync(packet!, cancellationToken);
            progress?.Report($"{DashboardScenes.Name(scene)} · {snapshot.Media.Title} · {profile.Width} × {profile.Height} · {packet!.Length:N0} bytes");
            await Task.Delay((int)Math.Max(1, 2000 - (Environment.TickCount64 - started)), cancellationToken);
        }
    }, cancellationToken);
}
