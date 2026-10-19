// VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT. VBWR E
using VibePanel.Contracts;

namespace VibePanel.Studio;

/// <summary>One immutable observation, replaced atomically; views never hold up the worker.</summary>
public sealed record StudioSnapshot(ThemeCatalogDto? Catalog = null, PanelChoice[]? Devices = null,
    PanelState? Frame = null, string Status = "Local preview", bool DisplayActive = false,
    bool Busy = false, bool StartupPending = false, long Revision = 0, long FrameRevision = 0);

/// <summary>Owns output scheduling and startup independently of every window/dispatcher.</summary>
public sealed class StudioSession : IPanelClient, IAsyncDisposable, IDisposable
{
    private readonly IPanelClient client;
    private readonly SemaphoreSlim operation = new(1, 1), wake = new(0, 1);
    private readonly CancellationTokenSource lifetime = new();
    private readonly object snapshotSync = new();
    private StudioSnapshot snapshot = new();
    private readonly Task worker;
    private readonly string? startupDevice, startupTheme;
    private int stopping, previewVisible, stopRequests;
    private bool outputFaulted;
    private long devicesAt;
    private Task? shutdown;
    public StudioModel Model { get; }
    public StudioViewModel ViewModel { get; }
    public StudioSnapshot Snapshot => Volatile.Read(ref snapshot);
    public bool PreviewVisible { set { Interlocked.Exchange(ref previewVisible, value ? 1 : 0); } }

    public StudioSession(IPanelClient client, string settingsPath, bool restoreLastSession = false)
    {
        this.client = client;
        Model = new(settingsPath);
        ViewModel = new(Model, this);
        if (Model.PersistenceNotice is { } notice) { snapshot = snapshot with { Status = notice }; }
        var p = Model.Preferences;
        if (restoreLastSession && !string.IsNullOrEmpty(p.SelectedDeviceId) && !string.IsNullOrEmpty(p.SelectedTheme))
        {
            startupDevice = p.SelectedDeviceId; startupTheme = p.SelectedTheme;
            snapshot = snapshot with { StartupPending = true };
        }
        worker = Task.Run(RunAsync);
    }

    private void Publish(Func<StudioSnapshot, StudioSnapshot> edit)
    {
        lock (snapshotSync) { var current = snapshot; Volatile.Write(ref snapshot, edit(current) with { Revision = current.Revision + 1 }); }
    }

    private bool ConsumeStartup()
    {
        lock (snapshotSync)
        {
            if (!snapshot.StartupPending || Volatile.Read(ref stopRequests) != 0) { return false; }
            Volatile.Write(ref snapshot, snapshot with { StartupPending = false, Revision = snapshot.Revision + 1 });
            return true;
        }
    }

    public void CancelStartup() => Publish(s => s with { StartupPending = false });

    public void Wake() { if (wake.CurrentCount == 0) { try { wake.Release(); } catch (SemaphoreFullException) { } } }

    public async Task<T> RequestAsync<T>(string method, object? parameters = null, CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellationToken);
        await operation.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref stopping) != 0) { throw new OperationCanceledException("The studio is closing."); }
            return await client.RequestAsync<T>(method, parameters, linked.Token).ConfigureAwait(false);
        }
        finally { operation.Release(); }
    }

    public async Task ReloadCatalogAsync()
    {
        await operation.WaitAsync(lifetime.Token).ConfigureAwait(false);
        try { await LoadCatalogAsync().ConfigureAwait(false); }
        finally { operation.Release(); Wake(); }
    }

    private async Task LoadCatalogAsync()
    {
        var catalog = await client.RequestAsync<ThemeCatalogDto>("themes", cancellationToken: lifetime.Token).ConfigureAwait(false);
        ViewModel.NormalizeCatalog(catalog);
        Publish(s => s with { Catalog = catalog });
    }

    public async Task RefreshDevicesAsync()
    {
        await operation.WaitAsync(lifetime.Token).ConfigureAwait(false);
        try { await DiscoverAsync().ConfigureAwait(false); }
        finally { operation.Release(); Wake(); }
    }

    private async Task DiscoverAsync()
    {
        var devices = await client.RequestAsync<PanelChoice[]>("devices", cancellationToken: lifetime.Token).ConfigureAwait(false);
        devicesAt = Environment.TickCount64;
        var p = Model.Preferences;
        var lost = Snapshot.DisplayActive && !devices.Any(d => d.Id == p.SelectedDeviceId);
        if (!Snapshot.StartupPending && !devices.Any(d => d.Id == p.SelectedDeviceId) && (string.IsNullOrEmpty(p.SelectedDeviceId) || devices.Length == 1))
        { Model.Edit(next => next.SelectedDeviceId = devices.FirstOrDefault()?.Id); }
        Publish(s => s with
        {
            Devices = devices,
            DisplayActive = lost ? false : s.DisplayActive,
            Status = lost ? "USB display disconnected. Press Show on display to reconnect." : s.Status
        });
    }

    public async Task StartAsync(string device)
    {
        Publish(s => s with { StartupPending = false });
        await operation.WaitAsync(lifetime.Token).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref stopping) != 0 || Snapshot.DisplayActive) { return; }
            await StartCoreAsync(device).ConfigureAwait(false);
        }
        finally { operation.Release(); Wake(); }
    }

    private async Task StartCoreAsync(string device)
    {
        var p = Model.Preferences;
        if (string.IsNullOrEmpty(p.SelectedTheme)) { return; }
        Publish(s => s with { Busy = true });
        try
        {
            var result = await client.RequestAsync<DisplayStartResult>("start", new StartOptions(device, p.ProfileSelection, ThemeId: p.SelectedTheme), lifetime.Token).ConfigureAwait(false);
            if (result.Accepted) { outputFaulted = false; Model.Edit(next => { next.SelectedDeviceId = device; next.SelectedProfile = result.ProfileId ?? next.SelectedProfile; }); Save(); }
            Publish(s => s with { DisplayActive = result.Accepted, Status = result.Accepted ? "Display running." : "The display could not start." });
        }
        catch (Exception error) { Publish(s => s with { DisplayActive = false, Status = error.Message }); }
        finally { Publish(s => s with { Busy = false }); }
    }

    public async Task StopAsync()
    {
        // Consume startup before waiting for an in-flight request. Stop is never lost.
        Interlocked.Increment(ref stopRequests);
        Publish(s => s with { StartupPending = false, Busy = true });
        var acquired = false;
        try
        {
            await operation.WaitAsync(lifetime.Token).ConfigureAwait(false);
            acquired = true;
            await client.RequestAsync<object>("stop", cancellationToken: lifetime.Token).ConfigureAwait(false);
            Publish(s => s with { DisplayActive = false, Status = "Display stopped." });
        }
        catch (Exception error) { Publish(s => s with { DisplayActive = false, Status = error.Message }); }
        finally { Interlocked.Decrement(ref stopRequests); Publish(s => s with { Busy = false }); if (acquired) { operation.Release(); } Wake(); }
    }

    public void Save()
    {
        try { Model.Save(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { Publish(s => s with { Status = "The preference changed but could not be saved." }); }
    }

    private void Rotate()
    {
        var state = Model.Read();
        var p = state.Preferences;
        if (!p.CycleThemes || Environment.TickCount64 - p.LastChange < 20000 || Snapshot.Catalog is not { } catalog) { return; }
        var screens = state.Screens().Where(s => s.Enabled && !string.IsNullOrEmpty(s.ThemeId)
            && (!Snapshot.DisplayActive || s.Category != "codex" || Snapshot.Frame?.CodexProcessRunning == true)).ToArray();
        if (screens.Length == 0) { return; }
        var index = Array.FindIndex(screens, s => s.Category == p.SelectedCategory);
        var next = screens[(index + 1) % screens.Length];
        if (screens.Length == 1 && next.Category == p.SelectedCategory)
        {
            var themes = catalog.Themes.Where(t => t.Category == next.Category).ToArray();
            if (themes.Length > 0)
            {
                var current = Array.FindIndex(themes, t => t.Id == p.SelectedTheme);
                next = next with { ThemeId = themes[(current + 1) % themes.Length].Id };
            }
        }
        Model.Update(latest =>
        {
            // A user edit made during rotation wins over this stale observation.
            if (latest.Revision != state.Revision) { return; }
            StudioViewModel.Select(latest, next.Category, next.ThemeId);
        });
        Save();
    }

    private async Task RunAsync()
    {
        try
        {
            while (Volatile.Read(ref stopping) == 0)
            {
                var delay = Model.Preferences.RefreshMilliseconds;
                await operation.WaitAsync(lifetime.Token).ConfigureAwait(false);
                try
                {
                    if (Volatile.Read(ref stopping) != 0) { break; }
                    if (Snapshot.Catalog is null) { await LoadCatalogAsync().ConfigureAwait(false); }
                    if (devicesAt == 0 || Environment.TickCount64 - devicesAt >= 10000) { await DiscoverAsync().ConfigureAwait(false); }
                    if (Snapshot.StartupPending)
                    {
                        if (!Snapshot.Catalog!.Themes.Any(t => t.Id == startupTheme) || Model.Preferences.SelectedTheme != startupTheme)
                        { Publish(s => s with { StartupPending = false, Status = "The saved startup theme changed or is unavailable." }); }
                        else if (Snapshot.Devices!.Any(d => d.Id == startupDevice))
                        {
                            // Consume before I/O; an ambiguous start is never replayed.
                            if (ConsumeStartup()) { await StartCoreAsync(startupDevice!).ConfigureAwait(false); }
                        }
                        else { Publish(s => s with { Status = "Waiting for the saved USB display. Stop cancels startup output for this run." }); }
                    }
                    if (Volatile.Read(ref stopRequests) != 0 || Snapshot.Busy || !Snapshot.DisplayActive && Volatile.Read(ref previewVisible) == 0) { goto WaitForNext; }
                    Rotate();
                    var state = Model.Read();
                    var p = state.Preferences;
                    var screens = state.Screens();
                    var theme = string.IsNullOrEmpty(p.SelectedTheme) ? screens.FirstOrDefault(s => s.Enabled && s.ThemeId.Length > 0)?.ThemeId ?? Snapshot.Catalog?.Themes.FirstOrDefault()?.Id : p.SelectedTheme;
                    if (string.IsNullOrEmpty(theme)) { goto WaitForNext; }
                    var light = p.Light;
                    var options = new PreviewOptions(ProfileId: p.SelectedProfile, Light: light, ThemeId: theme,
                        FontFamily: p.FontFamily, FontScale: p.FontScale, RefreshIntervalMilliseconds: p.RefreshMilliseconds,
                        JpegQuality: p.JpegQuality, SaturationPercent: p.SaturationPercent, HueDegrees: p.HueDegrees,
                        ShowSpectrum: p.ShowSpectrum, BrightnessPercent: p.BrightnessPercent, ContrastPercent: p.ContrastPercent,
                        ColorFiltersEnabled: p.ColorFiltersEnabled, Screens: screens, Palettes: state.Palettes.Where(pair => new[] { theme }.Concat(screens.Select(screen => screen.ThemeId))
                            .Any(id => pair.Key == id || pair.Key == id + (light ? "@light" : "@dark"))).ToDictionary());
                    var active = Snapshot.DisplayActive;
                    var frame = await client.RequestAsync<PanelState>(active ? "present" : "state", options, lifetime.Token).ConfigureAwait(false);
                    delay = frame.NextFrameMilliseconds is >= 25 and <= 1000 ? Math.Min(delay, frame.NextFrameMilliseconds.Value) : delay;
                    Publish(s => s with { Frame = frame, FrameRevision = s.FrameRevision + 1, DisplayActive = active && !outputFaulted && frame.DisplayRunning, Status = outputFaulted ? s.Status : frame.Status });
                }
                catch (Exception error) when (error is not OperationCanceledException)
                { if (Snapshot.DisplayActive) { outputFaulted = true; } Publish(s => s with { DisplayActive = false, Status = error.Message }); delay = 1000; }
                finally { operation.Release(); }
            WaitForNext:
                // Native thread-pool wait: never depends on WebView, XAML or window events.
                await wake.WaitAsync(Math.Clamp(delay, 25, 1000), lifetime.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception error) { Publish(s => s with { DisplayActive = false, Status = error.Message }); }
    }

    public Task ShutdownAsync()
    {
        lock (snapshotSync) { return shutdown ??= ShutdownCoreAsync(); }
    }

    private async Task ShutdownCoreAsync()
    {
        Interlocked.Exchange(ref stopping, 1);
        Wake();
        Save();
        try { await worker.WaitAsync(TimeSpan.FromSeconds(40)).ConfigureAwait(false); }
        catch (TimeoutException) { lifetime.Cancel(); }
        finally { lifetime.Cancel(); }
        // The host next closes engine stdin. EOF lets the engine dispose USB,
        // Python, audio and sensors before the host's final bounded kill fallback.
    }

    public void Dispose() => ShutdownAsync().GetAwaiter().GetResult();

    public async ValueTask DisposeAsync() => await ShutdownAsync().ConfigureAwait(false);
}
