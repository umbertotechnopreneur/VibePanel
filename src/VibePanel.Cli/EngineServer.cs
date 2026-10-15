// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VibePanel.Contracts;
using VibePanel.Core;

/// <summary>Small allow-listed local protocol. USB writes require an explicit start request.</summary>
internal sealed class EngineServer : IDisposable
{
    private readonly DashboardService dashboard = new();
    private readonly DashboardRenderer renderer = new();
    private readonly Dictionary<string, DisplayDevice> devices = [];
    private CancellationTokenSource? playbackCancellation;
    private Task? playback;
    private string status = "Local preview only. The USB display has not been started.";
    private string? previousFingerprint;
    private byte[]? preview;
    private long discoveredAt;
    private DashboardSnapshot? lastSnapshot;
    private long snapshotAt;

    public static async Task<int> RunAsync()
    {
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = new UTF8Encoding(false);
        using var engine = new EngineServer();
        try
        {
            while (await ReadRequestLineAsync() is { } line)
            {
                var id = "";
                EngineResponse response;
                try
                {
                    var request = JsonSerializer.Deserialize<EngineRequest>(line, PanelContract.Json) ?? throw new ArgumentException("Expected a request object.");
                    id = request.Id;
                    if (string.IsNullOrWhiteSpace(id) || id.Length > 64) { throw new ArgumentException("Request ID must contain 1 to 64 characters."); }
                    if (request.Version != PanelContract.Version) { throw new ArgumentException("Unsupported protocol version."); }
                    response = new(id, true, JsonSerializer.SerializeToElement(await engine.HandleAsync(request), PanelContract.Json));
                }
                catch (Exception error) when (error is not OutOfMemoryException)
                { response = new(id, false, Error: error.Message); }
                await Console.Out.WriteLineAsync(JsonSerializer.Serialize(response, PanelContract.Json));
                await Console.Out.FlushAsync();
            }
            await engine.StopAsync();
            return 0;
        }
        catch (Exception error)
        { await Console.Error.WriteLineAsync($"VibePanel engine: {error.Message}"); return 1; }
    }

    private static async Task<string?> ReadRequestLineAsync()
    {
        var text = new StringBuilder();
        var character = new char[1];
        while (await Console.In.ReadAsync(character.AsMemory()) != 0)
        {
            if (character[0] == '\n') { return text.ToString().TrimEnd('\r'); }
            if (text.Length >= PanelContract.MaximumRequestCharacters) { throw new IOException("Request exceeds the 16 KiB limit."); }
            text.Append(character[0]);
        }
        return text.Length == 0 ? null : text.ToString();
    }

    private async Task<object> HandleAsync(EngineRequest request)
    {
        switch (request.Method)
        {
            case "hello": return new { protocol = PanelContract.Version, platform = "windows", usbWritesRequireStart = true };
            case "state": return await StateAsync(Parameters<PreviewOptions>(request, new()));
            case "levels": return dashboard.Audio.ReadLevels();
            case "start":
                var options = Parameters<StartOptions>(request);
                if (playback is { IsCompleted: false }) { throw new InvalidOperationException("Stop the current display session first."); }
                if (!devices.TryGetValue(options.DeviceId, out var selected)) { throw new ArgumentException("Refresh and choose a connected panel."); }
                var connected = DisplayDiscovery.FindDevices().SingleOrDefault(item => item.DevicePath == selected.DevicePath && item.Transport == selected.Transport)
                    ?? throw new IOException("The selected panel disconnected. Refresh the device list.");
                if (options.ProfileId != "auto") { _ = DisplayProfileCatalog.GetRequired(options.ProfileId); }
                DashboardScene? scene = options.Scene is null ? null : DashboardScenes.Parse(options.Scene);
                playbackCancellation?.Dispose();
                playbackCancellation = new();
                var token = playbackCancellation.Token;
                Volatile.Write(ref status, "Starting the selected panel. Physical rendering is not confirmed by submission.");
                playback = RunDisplayAsync(connected, options.ProfileId, scene, token);
                return new { accepted = true };
            case "stop": await StopAsync(); return new { stopped = playback is null || playback.IsCompleted };
            case "media":
                await dashboard.Media.ControlAsync(Parameters<MediaOptions>(request).Action);
                return new { accepted = true };
            default: throw new ArgumentException("Unknown method. Use hello, state, levels, start, stop, or media.");
        }
    }

    private async Task<PanelState> StateAsync(PreviewOptions options)
    {
        var scene = DashboardScenes.Parse(options.Scene);
        var profile = DisplayProfileCatalog.GetRequired(options.ProfileId) with { Encoding = PanelPixelEncoding.Jpeg, WireRotation = 0 };
        if (lastSnapshot is null || Environment.TickCount64 - snapshotAt >= 2000)
        { lastSnapshot = await dashboard.ReadAsync(); snapshotAt = Environment.TickCount64; }
        var snapshot = lastSnapshot;
        if (options.IncludeDevices && (discoveredAt == 0 || Environment.TickCount64 - discoveredAt >= 5000))
        {
            devices.Clear();
            foreach (var device in DisplayDiscovery.FindDevices())
            {
                var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(device.DevicePath + device.Transport)))[..16];
                devices.Add(key, device);
            }
            discoveredAt = Environment.TickCount64;
        }
        int? drift = options.AmbientDrift ? DashboardRenderer.AmbientOffset() : null;
        var fingerprint = DashboardRenderer.Fingerprint(snapshot, scene) + profile.Id + options.Light + drift;
        if (options.IncludeFrame && previousFingerprint != fingerprint)
        {
            preview = renderer.Render(snapshot, profile, scene, options.Light, drift);
            previousFingerprint = fingerprint;
        }
        var metrics = snapshot.Metrics;
        return new(snapshot.Media.Title, snapshot.Media.Artist, snapshot.Media.Album, snapshot.Media.Playing,
            snapshot.Media.Source.Contains("spotify", StringComparison.OrdinalIgnoreCase) ? "Spotify" : snapshot.Media.Source,
            snapshot.Volumes.Select(item => new AudioLevelDto(item.Key, item.Name, item.Percent, item.Muted, item.Peak)).ToArray(),
            new(metrics.CpuUsage, metrics.CpuTemperature, metrics.GpuUsage, metrics.GpuTemperature, metrics.GpuName, metrics.CpuTemperatureSource),
            snapshot.Network is { } network ? new(network.DownloadBytesPerSecond, network.UploadBytesPerSecond, network.DownloadBytes, network.UploadBytes, "Since engine start; adapter counters may overlap on VPN/bridges") : null,
            devices.Select(item => new PanelChoice(item.Key, $"{item.Value.Id} · {item.Value.Connection}")).ToArray(),
            DisplayProfileCatalog.All.Select(item => new PanelChoice(item.Id, item.Name)).ToArray(),
            Enum.GetValues<DashboardScene>().Select(item => new PanelChoice(item.ToString(), DashboardScenes.Name(item))).ToArray(),
            playback is { IsCompleted: false }, Volatile.Read(ref status), options.IncludeFrame ? preview : null, snapshot.Notices);
    }

    private async Task RunDisplayAsync(DisplayDevice device, string profile, DashboardScene? scene, CancellationToken cancellationToken)
    {
        try
        {
            await DashboardPlayback.RunAsync(device, profile, new StatusProgress(message => Volatile.Write(ref status, message)), cancellationToken, scene);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { Volatile.Write(ref status, "Display stopped."); }
        catch (Exception error) { Volatile.Write(ref status, $"Display stopped: {error.Message}"); }
    }

    private async Task StopAsync()
    {
        playbackCancellation?.Cancel();
        if (playback is not null) { await playback.WaitAsync(TimeSpan.FromSeconds(10)); }
    }

    private static T Parameters<T>(EngineRequest request, T? fallback = default) where T : class =>
        request.Parameters?.Deserialize<T>(PanelContract.Json) ?? fallback ?? throw new ArgumentException("Missing parameters.");

    public void Dispose() { playbackCancellation?.Cancel(); playbackCancellation?.Dispose(); dashboard.Dispose(); }
    private sealed class StatusProgress(Action<string> report) : IProgress<string> { public void Report(string value) => report(value); }
}
