// VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT. VBWR E
using System.Text.Json;
using VibePanel.Contracts;
using VibePanel.Core;
using VibePanel.Studio;

internal static class ArchitectureChecks
{
    public static async Task RunAsync(string directory, Action<bool, string> check)
    {
        var path = Path.Combine(directory, "atomic.json");
        var model = new StudioModel(path);
        var vm = new StudioViewModel(model);
        var catalog = new ThemeCatalogDto([new("system", "System"), new("multimedia", "Music")],
            [new("system.a", "A", "system", "", "fixture"), new("music.a", "B", "multimedia", "", "fixture")], []);
        vm.NormalizeCatalog(catalog);
        var writer = Task.Run(() =>
        {
            for (var i = 0; i < 4000; i++) { vm.SelectTheme(i % 2 == 0 ? "system.a" : "music.a", catalog); }
        });
        var coherent = true;
        for (var i = 0; i < 4000; i++)
        {
            var state = model.Read(); var p = state.Preferences;
            coherent &= state.ScreenThemes[p.SelectedCategory] == p.SelectedTheme
                && catalog.Themes.Any(t => t.Id == p.SelectedTheme && t.Category == p.SelectedCategory);
        }
        await writer;
        check(coherent, "Concurrent selection reads are coherent.");
        StudioState? retained = null;
        model.Update(s => retained = s);
        retained!.Preferences.SelectedTheme = "escaped"; retained.ScreenThemes.Clear();
        var detached = model.Read(); detached.ScreenEnabled.Clear();
        check(model.Preferences.SelectedTheme != "escaped" && model.Screens().Length == 3, "Neither editor nor reader can mutate committed state.");
        var previous = model.Preferences.SelectedTheme;
        try { model.Update(s => { s.Preferences.SelectedTheme = "partial"; throw new IOException("fixture"); }); }
        catch (IOException) { }
        check(model.Preferences.SelectedTheme == previous, "Failed transaction commits nothing.");
        vm.SelectTheme("missing", catalog);
        check(model.Preferences.SelectedTheme == previous, "Unknown theme selection is ignored.");
        vm.SetScreens(false, true, true);
        check(!model.ScreenEnabled["multimedia"] && model.ScreenEnabled["codex"], "Screen toggles are one transaction.");
        model.Save();
        using (var json = JsonDocument.Parse(File.ReadAllText(path)))
        { check(json.RootElement.GetProperty("schemaVersion").GetInt32() == 1, "Saved settings carry a schema version."); }
        vm.SelectTheme("system.a", catalog); model.Save();
        File.WriteAllText(path, "{broken");
        var recovered = new StudioModel(path);
        check(recovered.PersistenceNotice?.Contains("recovered") == true && recovered.Preferences.SelectedTheme == previous, "Corrupt current settings recover the last complete backup.");
        recovered.Save();
        check(new StudioModel(path).PersistenceNotice is null && Directory.GetFiles(directory, "*.tmp").Length == 0, "Recovery restores valid settings without temporary files.");
        var legacyPath = Path.Combine(directory, "legacy.json");
        File.WriteAllText(legacyPath, """{"category":"codex","themeId":"codex.a","fontScale":9,"refreshMilliseconds":17}""");
        var legacy = new StudioModel(legacyPath);
        check(legacy.ScreenEnabled["codex"] && legacy.Preferences.FontScale == 1.5 && legacy.Preferences.RefreshMilliseconds == 1000, "Unversioned settings migrate with existing bounds.");
        legacy.Save();
        check(new StudioModel(legacyPath).Preferences.SelectedTheme == "codex.a", "Migrated settings survive reload.");
        var futurePath = Path.Combine(directory, "future.json");
        const string future = """{"schemaVersion":99,"themeId":"future"}""";
        File.WriteAllText(futurePath, future);
        var futureModel = new StudioModel(futurePath);
        try { futureModel.Save(); throw new InvalidOperationException("Future schema overwritten."); } catch (IOException) { }
        check(File.ReadAllText(futurePath) == future && futureModel.PersistenceNotice is not null, "Future settings cannot be overwritten by an older app.");
        var malformedPath = Path.Combine(directory, "malformed.json");
        const string malformed = """{"category":"system","themeId":"partial","fontScale":{}}""";
        File.WriteAllText(malformedPath, malformed);
        var malformedModel = new StudioModel(malformedPath);
        check(malformedModel.Preferences.SelectedTheme == "" && malformedModel.PersistenceNotice is not null, "Malformed settings cannot leave a partially loaded model.");
        try { malformedModel.Save(); throw new InvalidOperationException("Damaged settings overwritten."); } catch (IOException) { }
        check(File.ReadAllText(malformedPath) == malformed, "Unrecoverable settings are preserved.");
        var fake = new Providers();
        using (var service = new DashboardService(new(fake, fake, fake, fake, fake)))
        {
            await service.ReadAsync(includeSystem: false, includeMedia: false);
            check(fake.Reads == 0 && fake.Stops == 1, "Disabled features do not sample providers.");
            fake.PlaybackFails = true;
            var sample = await service.ReadAsync(includeSystem: false, includeMedia: true, analyzePlayback: true, includeSensors: true);
            check(sample.Sensors is not null && sample.Notices.Any(n => n.StartsWith("Playback unavailable")), "A failed playback provider cannot discard sensor data.");
            check(sample.Playback is null, "A failed provider never returns invented or stale analysis.");
            fake.SensorsFail = true;
            sample = await service.ReadAsync(includeSystem: false, includeMedia: false, includeSensors: true);
            check(sample.Sensors?.Cpu is null && sample.Notices.Any(n => n.StartsWith("Sensors unavailable")), "Unavailable sensors remain missing with a notice.");
        }
        check(fake.Disposals == 3, "All owned disposable providers are released.");
    }

    private sealed class Providers : IMediaProvider, IVolumeProvider, IMetricsProvider, IPlaybackProvider, INetworkProvider
    {
        public int Reads, Stops, Disposals;
        public bool PlaybackFails, SensorsFail;
        public Task<NowPlaying> ReadAsync() { Reads++; return Task.FromResult(NowPlaying.Empty); }
        public Task ControlAsync(string action) => Task.CompletedTask;
        IReadOnlyList<AppVolume> IVolumeProvider.Read() { Reads++; return []; }
        public IReadOnlyDictionary<string, double?> ReadLevels() => new Dictionary<string, double?>();
        public HardwareSensorSnapshot ReadSensors()
        { Reads++; if (SensorsFail) { throw new IOException("fixture sensors"); } return HardwareSensorSnapshot.Empty; }
        MachineMetrics IMetricsProvider.Read() { Reads++; return new(null, null, null, null, "", "", ""); }
        PlaybackAnalysis IPlaybackProvider.Read() { Reads++; if (PlaybackFails) { throw new IOException("fixture playback"); } return Snapshot(); }
        public PlaybackAnalysis Snapshot() => new(false, "fixture", 0, 0, 0, [], [], null, null, null, null, null, null, null, "unavailable");
        public void Stop() => Stops++;
        NetworkTraffic INetworkProvider.Read() { Reads++; return new(null, null, 0, 0); }
        public void Dispose() => Disposals++;
    }
}
