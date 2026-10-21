// VBWR B
// Project: ThermalrightVibeWare | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using System.Security.Cryptography;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ThermalrightVibeWare.Contracts;
using ThermalrightVibeWare.Core;

/// <summary>Small allow-listed local protocol. USB writes require an explicit start request.</summary>
internal sealed class EngineServer : IDisposable
{
    private readonly WindowsMetricsService metrics = new(allowHardwareAccess: true);
    private readonly DashboardService dashboard;
    public EngineServer() => dashboard = new(WindowsDashboardProviders.Create(metrics));
    private readonly PythonThemeRenderer themeRenderer = new();
    private readonly CodexProcessMonitor codexProcess = new();
    private readonly Dictionary<string, DisplayDevice> devices = [];
    private HardwareDriverStatus? sensorDriver;
    private string status = "Local preview only. The USB display has not been started.";
    private string? previousFingerprint;
    private byte[]? preview;
    private long discoveredAt;
    private DashboardSnapshot? lastSnapshot;
    private long snapshotAt;
    private bool snapshotIncludesSystem;
    private bool snapshotIncludesMedia;
    private bool snapshotIncludesSensors;
    private ThemeCatalogSnapshot? themeCatalog;
    private long previewAt;
    private string? previewNotice;
    private ThemePanelOutput? themeOutput;
    private string? themeDeviceId;
    private double? frameMilliseconds;

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
                var method = "unknown";
                string? errorType = null;
                var started = Stopwatch.GetTimestamp();
                EngineResponse response;
                try
                {
                    var request = JsonSerializer.Deserialize<EngineRequest>(line, PanelContract.Json) ?? throw new ArgumentException("Expected a request object.");
                    id = request.Id;
                    method = request.Method;
                    if (string.IsNullOrWhiteSpace(id) || id.Length > 64) { throw new ArgumentException("Request ID must contain 1 to 64 characters."); }
                    if (request.Version != PanelContract.Version) { throw new ArgumentException("Unsupported protocol version."); }
                    response = new(id, true, JsonSerializer.SerializeToElement(await engine.HandleAsync(request), PanelContract.Json));
                }
                catch (Exception error) when (error is not OutOfMemoryException)
                { errorType = error.GetType().Name; response = new(id, false, Error: error.Message); }
                DiagnosticLog.Rpc(method, (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds, response.Ok, errorType,
                    engine.themeOutput?.SubmittedFrames ?? 0);
                await Console.Out.WriteLineAsync(JsonSerializer.Serialize(response, PanelContract.Json));
                await Console.Out.FlushAsync();
            }
            await engine.StopAsync();
            return 0;
        }
        catch (Exception error)
        { await Console.Error.WriteLineAsync($"ThermalrightVibeWare engine: {error.Message}"); return 1; }
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
            case "hello": return new { protocol = PanelContract.Version, platform = "windows", usbWritesRequireStart = true, themeMirror = true };
            case "themes":
                themeCatalog = ThemeCatalog.Read();
                // An explicit refresh can replace a pack at the same ID; discard frames from the previous catalog.
                previousFingerprint = null;
                preview = null;
                previewNotice = null;
                return new ThemeCatalogDto([new("multimedia", "Music"), new("codex", "Codex"), new("system", "System")],
                    themeCatalog.Themes.Select(theme => new ThemeChoiceDto(theme.Id, theme.Name, theme.Category, theme.Description, theme.PackId, theme.SupportsSpectrum, theme.PageCount, theme.PageSeconds)).ToArray(), themeCatalog.Notices);
            case "fonts":
                var fonts = await themeRenderer.ListFontsAsync();
                return new FontCatalogDto(fonts.Families, fonts.Notice);
            case "state": return await StateAsync(Parameters<PreviewOptions>(request, new()));
            case "present": return await PresentAsync(Parameters<PreviewOptions>(request));
            case "devices":
                DiscoverDevices();
                return DeviceChoices();
            case "levels": return dashboard.Audio.ReadLevels();
            case "start":
                var options = Parameters<StartOptions>(request);
                if (themeOutput is not null) { throw new InvalidOperationException("Stop the current display session first."); }
                if (!devices.TryGetValue(options.DeviceId, out var selected)) { throw new ArgumentException("Refresh and choose a connected panel."); }
                var connected = DisplayDiscovery.FindDevices().SingleOrDefault(item => item.DevicePath == selected.DevicePath && item.Transport == selected.Transport)
                    ?? throw new IOException("The selected panel disconnected. Refresh the device list.");
                if (options.ProfileId != "auto") { _ = DisplayProfileCatalog.GetRequired(options.ProfileId); }
                _ = FindTheme(options.ThemeId ?? throw new ArgumentException("Choose a theme before starting the display."));
                themeOutput = await ThemePanelOutput.OpenAsync(connected, options.ProfileId);
                themeDeviceId = options.DeviceId;
                frameMilliseconds = null;
                previousFingerprint = null;
                preview = null;
                previewNotice = null;
                Volatile.Write(ref status, $"Display ready · {themeOutput.Profile.Width} × {themeOutput.Profile.Height}");
                return new DisplayStartResult(true, themeOutput.Profile.Id);
            case "stop": await StopAsync(); return new { stopped = true };
            case "media":
                await dashboard.Media.ControlAsync(Parameters<MediaOptions>(request).Action);
                return new { accepted = true };
            case "codex.status": return CodexIntegration.Status();
            case "codex.prepare": return await CodexIntegration.PrepareAsync();
            case "diagnostics.status": return DiagnosticLog.Status();
            case "diagnostics.set": return DiagnosticLog.SetEnabled(Parameters<SetDiagnosticsOptions>(request).Enabled);
            case "diagnostics.open": return new { accepted = DiagnosticLog.Open() };
            case "sensors.driver.status": return SensorDriverStatus();
            case "sensors.debug": return await Task.Run(metrics.ReadSensorDebug);
            case "sensors.driver.open": return new { accepted = OpenSensorDriverFolder() };
            default: throw new ArgumentException("Unknown method. Use hello, themes, fonts, devices, state, present, levels, start, stop, media, codex.status, codex.prepare, diagnostics.*, sensors.debug, or sensors.driver.*.");
        }
    }

    private SensorDriverDto SensorDriverStatus()
    {
        // Registry/cached metadata only. Opening this panel must not open hardware.
        var driver = sensorDriver ?? WindowsMetricsService.ReadDriverInstallationStatus();
        return new(driver.Available, driver.Path, driver.Notice, driver.Path is not null && File.Exists(driver.Path));
    }

    private bool OpenSensorDriverFolder()
    {
        var driver = SensorDriverStatus();
        if (driver.Path is not { } path || !Path.IsPathFullyQualified(path)) { return false; }
        var directory = Path.GetDirectoryName(path);
        if (directory is null || !Directory.Exists(directory)) { return false; }
        var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"))
        { UseShellExecute = false };
        start.ArgumentList.Add(driver.FileExists ? "/select," + path : directory);
        return Process.Start(start) is not null;
    }

    private async Task<PanelState> StateAsync(PreviewOptions options)
    {
        ValidatePreviewOptions(options);
        var theme = FindTheme(options.ThemeId ?? throw new ArgumentException("Choose a theme for the preview."));
        var profile = DisplayProfileCatalog.GetRequired(options.ProfileId) with { Encoding = PanelPixelEncoding.Jpeg, WireRotation = 0 };
        const bool includeSystem = false;
        var includeMedia = theme.Category == "multimedia";
        var includeSensors = theme.Category == "system";
        if (lastSnapshot is null || snapshotIncludesSystem != includeSystem || snapshotIncludesMedia != includeMedia || snapshotIncludesSensors != includeSensors
            || Environment.TickCount64 - snapshotAt >= options.RefreshIntervalMilliseconds)
        {
            lastSnapshot = await dashboard.ReadAsync(includeSystem, includeMedia, analyzePlayback: theme.Category == "multimedia", includeSensors: includeSensors);
            snapshotAt = Environment.TickCount64;
            snapshotIncludesSystem = includeSystem;
            snapshotIncludesMedia = includeMedia;
            snapshotIncludesSensors = includeSensors;
        }
        var snapshot = lastSnapshot;
        if (options.IncludeDevices && (discoveredAt == 0 || Environment.TickCount64 - discoveredAt >= 5000))
        {
            DiscoverDevices();
        }
        var fingerprint = $"{theme.Id}|{profile.Id}|{options.Light}|{options.AmbientDrift}|{options.FontFamily}|{options.FontScale}|{options.JpegQuality}|{options.SaturationPercent}|{options.HueDegrees}|{options.ShowSpectrum}|{options.BrightnessPercent}|{options.ContrastPercent}|{options.ColorFiltersEnabled}|{options.PageIndex}|{options.PageRevision}";
        var paletteKey = theme.Id + (options.Light ? "@light" : "@dark");
        var palette = options.Palettes?.GetValueOrDefault(paletteKey) ?? options.Palettes?.GetValueOrDefault(theme.Id);
        fingerprint += "|" + JsonSerializer.Serialize(palette, PanelContract.Json);
        var configurationChanged = previousFingerprint != fingerprint;
        var refreshInterval = previewNotice is null ? themeRenderer.FrameIntervalMilliseconds(options.RefreshIntervalMilliseconds) : 5000;
        if (options.IncludeFrame && (configurationChanged || Environment.TickCount64 - previewAt >= refreshInterval))
        {
            var renderingStartedAt = Environment.TickCount64;
            if (configurationChanged) { preview = null; }
            try
            {
                preview = await themeRenderer.RenderAsync(theme, snapshot, profile, options.Light, options.AmbientDrift, options.FontFamily, options.FontScale, options.JpegQuality,
                    saturationPercent: options.SaturationPercent, hueDegrees: options.HueDegrees, showSpectrum: options.ShowSpectrum,
                    brightnessPercent: options.BrightnessPercent, contrastPercent: options.ContrastPercent, colorFiltersEnabled: options.ColorFiltersEnabled, palette: palette,
                    pageIndex: options.PageIndex, pageRevision: options.PageRevision);
                previewNotice = null;
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            { DiagnosticLog.ThemeRenderFailure(error.GetType().Name); previewNotice = "Theme preview: " + error.Message; }
            previousFingerprint = fingerprint;
            previewAt = previewNotice is null ? renderingStartedAt : Environment.TickCount64;
        }
        var metrics = snapshot.Metrics;
        if (snapshot.Sensors?.Driver is { } driver) { sensorDriver = driver; }
        return new(snapshot.Media.Title, snapshot.Media.Artist, snapshot.Media.Album, snapshot.Media.Playing,
            snapshot.Media.Source,
            snapshot.Volumes.Select(item => new AudioLevelDto(item.Key, item.Name, item.Percent, item.Muted, item.Peak)).ToArray(),
            new(metrics.CpuUsage, metrics.CpuTemperature, metrics.GpuUsage, metrics.GpuTemperature, metrics.GpuName, metrics.CpuTemperatureSource,
                sensorDriver is null ? null : SensorDriverStatus()),
            snapshot.Network is { } network ? new(network.DownloadBytesPerSecond, network.UploadBytesPerSecond, network.DownloadBytes, network.UploadBytes, "Since engine start; adapter counters may overlap on VPN/bridges") : null,
            DeviceChoices(),
            DisplayProfileCatalog.All.Select(item => new PanelChoice(item.Id, item.Name)).ToArray(),
            themeOutput is not null, Volatile.Read(ref status), options.IncludeFrame ? preview : null,
            previewNotice is null ? snapshot.Notices : snapshot.Notices.Concat([previewNotice]).ToArray(),
            frameMilliseconds, themeOutput?.SubmittedFrames ?? 0,
            options.IncludeFrame && preview is not null ? themeRenderer.AccentColor : null,
            options.IncludeFrame && preview is not null && themeRenderer.IsTransitioning
                ? themeRenderer.FrameIntervalMilliseconds(options.RefreshIntervalMilliseconds) : null,
            theme.Id, codexProcess.IsRunning(), ArtistSources: snapshot.ArtistProfile is { } artist
                ? new(artist.SourceUrl, artist.PhotoSourceUrl, artist.AlbumSales?.SourceUrl) : null,
            ActivePageId: themeRenderer.ActivePageId, ActivePageTitle: themeRenderer.ActivePageTitle,
            ActivePageIndex: themeRenderer.ActivePageIndex, ActivePageCount: themeRenderer.ActivePageCount);
    }

    private static void ValidatePreviewOptions(PreviewOptions options)
    {
        if (options.RefreshIntervalMilliseconds is < 25 or > 1000) { throw new ArgumentException("Refresh interval must be between 25 and 1000 ms."); }
        if (options.PageIndex is < 0 or > 7 || options.PageRevision < 0) { throw new ArgumentException("Invalid theme page selection."); }
        if (options.JpegQuality is not ("poor" or "low" or "medium" or "high")) { throw new ArgumentException("JPEG quality must be Poor, Low, Medium or High."); }
        PythonThemeRenderer.ValidateColorAdjustments(options.SaturationPercent, options.HueDegrees, options.BrightnessPercent, options.ContrastPercent);
        ScreenSelectionPolicy.Validate(options.Screens);
        if (options.Palettes is { } palettes)
        {
            if (palettes.Count > 16 || palettes.Keys.Any(key => key.Length > 110)) { throw new ArgumentException("Too many palette overrides."); }
            foreach (var palette in palettes.Values) { if (palette is null) { throw new ArgumentException("Invalid palette."); } palette.Validate(); }
        }
    }

    private ThemeDefinition FindTheme(string id) => (themeCatalog ??= ThemeCatalog.Read()).Themes.FirstOrDefault(item => item.Id == id)
        ?? throw new ArgumentException("Theme not found. Refresh the theme catalog.");

    private void DiscoverDevices()
    {
        // Build a complete snapshot before replacing the last successful enumeration.
        var found = new Dictionary<string, DisplayDevice>();
        foreach (var device in DisplayDiscovery.FindDevices())
        {
            var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(device.DevicePath + device.Transport)))[..16];
            found.TryAdd(key, device);
        }
        devices.Clear();
        foreach (var entry in found) { devices.Add(entry.Key, entry.Value); }
        discoveredAt = Environment.TickCount64;
        if (themeOutput is not null && (themeDeviceId is null || !devices.ContainsKey(themeDeviceId)))
        {
            CloseThemeOutput();
            Volatile.Write(ref status, "USB display disconnected. Reconnect it, then press Show on display.");
        }
    }

    private PanelChoice[] DeviceChoices() => devices.Select(item => new PanelChoice(item.Key, $"{item.Value.Id} · {item.Value.Connection}")).ToArray();

    private async Task<PanelState> PresentAsync(PreviewOptions options)
    {
        var output = themeOutput ?? throw new InvalidOperationException("Choose a panel and press Show on display first.");
        try
        {
            if (options.ThemeId is null || !options.IncludeFrame) { throw new ArgumentException("Display output requires a theme frame."); }
            if (options.ProfileId != output.Profile.Id) { throw new ArgumentException("Use the connected display's detected profile."); }
            ValidatePreviewOptions(options);
            var started = Stopwatch.GetTimestamp();
            var processRunning = codexProcess.IsRunning();
            var requested = FindTheme(options.ThemeId);
            var chosen = ScreenSelectionPolicy.Resolve(requested, (themeCatalog ??= ThemeCatalog.Read()).Themes, options.Screens, processRunning);
            if (chosen is null)
            {
                var fingerprint = $"blank|{output.Profile.Id}|{options.JpegQuality}";
                if (previousFingerprint != fingerprint || preview is null)
                {
                    var quality = options.JpegQuality switch { "poor" => 20, "low" => 50, "medium" => 75, _ => 90 };
                    preview = PanelFrameRenderer.Blank(output.Profile with { Encoding = PanelPixelEncoding.Jpeg, WireRotation = 0 }, quality);
                    previousFingerprint = fingerprint;
                    previewNotice = null;
                }
                await output.SendAsync(preview, options.JpegQuality);
                frameMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                Volatile.Write(ref status, "No active screens. Enable a screen to resume.");
                return new("", "", "", false, "", [], new(null, null, null, null, "", ""), null,
                    DeviceChoices(), DisplayProfileCatalog.All.Select(item => new PanelChoice(item.Id, item.Name)).ToArray(),
                    true, Volatile.Read(ref status), preview, [], frameMilliseconds, output.SubmittedFrames,
                    CodexProcessRunning: processRunning, FrameSuppressed: true);
            }
            var state = await StateAsync(options with { ThemeId = chosen.Id });
            if (previewNotice is not null || state.FrameJpeg is null) { throw new IOException(previewNotice ?? "The theme did not produce a frame."); }
            await output.SendAsync(state.FrameJpeg, options.JpegQuality);
            frameMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            Volatile.Write(ref status, $"Display · requested {options.RefreshIntervalMilliseconds} ms · last update {frameMilliseconds:F0} ms · {options.JpegQuality} JPEG");
            return state with { Status = Volatile.Read(ref status), FrameMilliseconds = frameMilliseconds, SubmittedFrames = output.SubmittedFrames };
        }
        catch
        {
            CloseThemeOutput();
            Volatile.Write(ref status, "Display stopped. Check the connection and press Show on display to try again.");
            throw;
        }
    }

    private Task StopAsync()
    {
        CloseThemeOutput();
        Volatile.Write(ref status, "Display stopped.");
        return Task.CompletedTask;
    }

    private void CloseThemeOutput()
    {
        themeOutput?.Dispose();
        themeOutput = null;
        themeDeviceId = null;
        frameMilliseconds = null;
    }

    private static T Parameters<T>(EngineRequest request, T? fallback = default) where T : class =>
        request.Parameters?.Deserialize<T>(PanelContract.Json) ?? fallback ?? throw new ArgumentException("Missing parameters.");

    public void Dispose() { CloseThemeOutput(); themeRenderer.Dispose(); dashboard.Dispose(); }
}
