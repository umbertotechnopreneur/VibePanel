// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

namespace VibePanel.Core;

/// <summary>Describes an explicit picture request shared by the UI and CLI.</summary>
public sealed record PanelPictureRequest(string ProfileId = "auto", string? ImagePath = null, int Rotation = 0, DisplayProfile? CustomProfile = null);

/// <summary>Reports submission evidence without implying that the physical image was observed.</summary>
public sealed record PanelSubmission(DisplayProfile Profile, int Bytes, string Message);

/// <summary>Coordinates native sessions, rendering, saved preferences, and opt-in playback.</summary>
public sealed class PanelApplication
{
    private readonly PanelSettingsStore settingsStore;

    /// <summary>Creates the small application facade for either interactive surface.</summary>
    public PanelApplication(PanelSettingsStore? settingsStore = null)
    {
        this.settingsStore = settingsStore ?? new PanelSettingsStore();
    }

    /// <summary>Returns connected interfaces without sending picture or device-info commands.</summary>
    public IReadOnlyList<DisplayDevice> Discover() => DisplayDiscovery.FindDevices();

    /// <summary>Reads explicit choices for this user's next picture request.</summary>
    public PanelSettings LoadSettings() => settingsStore.Load();

    /// <summary>Saves explicit profile, rotation, and image selections.</summary>
    public void SaveSettings(PanelSettings settings) => settingsStore.Save(settings);

    /// <summary>Requests a single device-information reply on a background thread.</summary>
    public Task<PanelProbeResult> ProbeAsync(DisplayDevice device, CancellationToken cancellationToken = default) => Task.Run(async () =>
    {
        using var session = PanelSession.Open(device);
        return await session.ProbeAsync(cancellationToken).ConfigureAwait(false);
    }, cancellationToken);

    /// <summary>Renders and submits one picture using a verified handshake and a resolved profile.</summary>
    public Task<PanelSubmission> SendAsync(DisplayDevice device, PanelPictureRequest request,
        CancellationToken cancellationToken = default) => Task.Run(async () =>
    {
        using var session = PanelSession.Open(device);
        var probe = await session.ProbeAsync(cancellationToken).ConfigureAwait(false);
        var profile = ResolveProfile(request, probe);
        var packet = Prepare(request, profile);
        var bytes = await session.SendPacketAsync(packet, cancellationToken).ConfigureAwait(false);
        return new PanelSubmission(profile, bytes, $"Submitted {profile.Width} x {profile.Height} via {device.Transport}. Check the physical display.");
    }, cancellationToken);

    /// <summary>Repeats a cached still image at one frame per second until cancelled or disconnected.</summary>
    public Task PlayAsync(DisplayDevice device, PanelPictureRequest request, TimeSpan? duration,
        IProgress<string>? progress, CancellationToken cancellationToken) => Task.Run(async () =>
    {
        if (duration is { } supplied && supplied <= TimeSpan.Zero) { throw new ArgumentOutOfRangeException(nameof(duration)); }
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (duration is { } limit) { lifetime.CancelAfter(limit); }
        using var session = PanelSession.Open(device);
        var submitted = 0;
        try
        {
            var probe = await session.ProbeAsync(lifetime.Token).ConfigureAwait(false);
            var profile = ResolveProfile(request, probe);
            var packet = Prepare(request, profile);
            while (true)
            {
                lifetime.Token.ThrowIfCancellationRequested();
                await session.SendPacketAsync(packet, lifetime.Token).ConfigureAwait(false);
                progress?.Report($"Submitted frame {++submitted} · {profile.Width} x {profile.Height} · 1 fps");
                await Task.Delay(1000, lifetime.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
    }, cancellationToken);

    private static DisplayProfile ResolveProfile(PanelPictureRequest request, PanelProbeResult probe)
    {
        if (!probe.IsCompatible) { throw new IOException(probe.Message); }
        return request.CustomProfile ?? (request.ProfileId == "auto"
            ? probe.DetectedProfile ?? throw new NotSupportedException($"Unknown PM {probe.ProductMode:X2}/SUB {probe.SubMode:X2}. Choose an explicit screen profile.")
            : DisplayProfileCatalog.GetRequired(request.ProfileId));
    }

    private static byte[] Prepare(PanelPictureRequest request, DisplayProfile profile)
    {
        var payload = request.ImagePath is null ? PanelFrameRenderer.Pattern(profile)
            : PanelFrameRenderer.Image(request.ImagePath, profile, request.Rotation);
        return Thermalright04165302Protocol.CreatePicturePacket(profile, payload);
    }
}
