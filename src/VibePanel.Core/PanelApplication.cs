// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using System.ComponentModel;

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

    /// <summary>Repeats a cached still image at one frame per second, reconnecting only to the same selected path.</summary>
    public Task PlayAsync(DisplayDevice device, PanelPictureRequest request, TimeSpan? duration,
        IProgress<string>? progress, CancellationToken cancellationToken) => Task.Run(async () =>
    {
        if (duration is { } supplied && supplied <= TimeSpan.Zero) { throw new ArgumentOutOfRangeException(nameof(duration)); }
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (duration is { } limit) { lifetime.CancelAfter(limit); }
        PanelSession? session = null;
        byte[]? packet = null;
        DisplayProfile? selectedProfile = null;
        var submitted = 0;
        try
        {
            while (true)
            {
                lifetime.Token.ThrowIfCancellationRequested();
                try
                {
                    if (session is null)
                    {
                        var current = Discover().SingleOrDefault(item => string.Equals(item.DevicePath, device.DevicePath, StringComparison.OrdinalIgnoreCase));
                        if (current is null)
                        {
                            progress?.Report("Waiting for the selected panel. Reconnect it to the same USB port.");
                            await Task.Delay(1000, lifetime.Token).ConfigureAwait(false);
                            continue;
                        }
                        session = PanelSession.Open(current);
                        var probe = await session.ProbeAsync(lifetime.Token).ConfigureAwait(false);
                        var profile = ResolveProfile(request, probe);
                        if (selectedProfile is not null && selectedProfile != profile) { throw new IOException("The reconnected panel reports a different profile. Start playback again deliberately."); }
                        selectedProfile = profile;
                        packet ??= Prepare(request, profile);
                    }
                    await session.SendPacketAsync(packet!, lifetime.Token).ConfigureAwait(false);
                    progress?.Report($"Submitted frame {++submitted} · {selectedProfile!.Width} x {selectedProfile.Height} · 1 fps");
                }
                catch (Exception error) when (IsDisconnected(error))
                {
                    session?.Dispose();
                    session = null;
                    progress?.Report("The selected panel disconnected. Waiting for it on the same port.");
                }
                await Task.Delay(1000, lifetime.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        finally { session?.Dispose(); }
    }, cancellationToken);

    private static bool IsDisconnected(Exception exception)
    {
        if (exception is Win32Exception native) { return native.NativeErrorCode is 2 or 6 or 31 or 1167; }
        return exception is IOException && (exception.HResult & 0xFFFF) is 2 or 6 or 31 or 1167;
    }

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
