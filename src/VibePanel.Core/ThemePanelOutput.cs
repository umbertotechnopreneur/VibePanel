// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

namespace VibePanel.Core;

/// <summary>One explicitly opened panel. Reuses the already rendered preview, without another renderer.</summary>
public sealed class ThemePanelOutput : IDisposable
{
    private readonly PanelSession session;
    private byte[]? previousJpeg;
    private string? previousQuality;
    private bool disposed;

    private ThemePanelOutput(PanelSession session, DisplayProfile profile) { this.session = session; Profile = profile; }
    public DisplayProfile Profile { get; }
    public long SubmittedFrames { get; private set; }

    public static async Task<ThemePanelOutput> OpenAsync(DisplayDevice device, string profileId = "auto", CancellationToken cancellationToken = default)
    {
        var requested = profileId == "auto" ? null : DisplayProfileCatalog.GetRequired(profileId);
        var session = PanelSession.Open(device);
        try
        {
            var probe = await session.ProbeAsync(cancellationToken);
            if (!probe.IsCompatible) { throw new IOException(probe.Message); }
            var profile = requested ?? probe.DetectedProfile ?? throw new NotSupportedException("Unknown display size. Choose a profile first.");
            if (requested is not null && probe.DetectedProfile is { } detected && requested.Id == detected.Id)
            { profile = requested with { WireRotation = detected.WireRotation }; }
            return new(session, profile);
        }
        catch { session.Dispose(); throw; }
    }

    /// <summary>Sends a whole frame, or skips byte-identical images. Calls must be serialized by the owner.</summary>
    public async Task<bool> SendAsync(byte[] jpeg, string jpegQuality = "high", CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var quality = jpegQuality switch { "poor" => 20, "low" => 50, "medium" => 75, "high" => 90, _ => throw new ArgumentException("JPEG quality must be poor, low, medium or high.") };
        if (previousQuality == jpegQuality && previousJpeg is not null && previousJpeg.AsSpan().SequenceEqual(jpeg)) { return false; }
        var payload = Profile is { Encoding: PanelPixelEncoding.Jpeg, WireRotation: 0 }
            ? jpeg : DashboardRenderer.Fade(jpeg, jpeg, Profile, 1, quality);
        var packet = Thermalright04165302Protocol.CreatePicturePacket(Profile, payload);
        await session.SendPacketAsync(packet, cancellationToken);
        previousJpeg = jpeg;
        previousQuality = jpegQuality;
        SubmittedFrames++;
        return true;
    }

    public void Dispose()
    {
        if (disposed) { return; }
        disposed = true;
        previousJpeg = null;
        session.Dispose();
    }
}
