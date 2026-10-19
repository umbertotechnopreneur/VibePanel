// VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT. VBWR E
using VibePanel.Contracts;
namespace VibePanel.Studio;

/// <summary>UI-independent selection commands shared by the view and background session.</summary>
public sealed class StudioViewModel(StudioModel model, IPanelClient? client = null)
{
    private readonly object auxiliarySync = new();
    private StudioAuxiliaryState auxiliary = new();
    public StudioAuxiliaryState Auxiliary => Volatile.Read(ref auxiliary);
    public string? PersistenceNotice => model.PersistenceNotice;
    private async Task<T> RequestAsync<T>(string method, object? options = null)
        => await (client ?? throw new InvalidOperationException("No engine client is attached.")).RequestAsync<T>(method, options).ConfigureAwait(false);
    private void Publish(Func<StudioAuxiliaryState, StudioAuxiliaryState> update)
    { lock (auxiliarySync) { Volatile.Write(ref auxiliary, update(auxiliary)); } }
    public async Task<CodexSetupStatus> ReadCodexAsync(bool prepare = false)
    {
        var value = await RequestAsync<CodexSetupStatus>(prepare ? "codex.prepare" : "codex.status").ConfigureAwait(false);
        Publish(s => s with { Codex = value }); return value;
    }
    public async Task<DiagnosticsStatus> ReadDiagnosticsAsync(bool? enabled = null)
    {
        var value = await RequestAsync<DiagnosticsStatus>(enabled.HasValue ? "diagnostics.set" : "diagnostics.status",
            enabled.HasValue ? new SetDiagnosticsOptions(enabled.Value) : null).ConfigureAwait(false);
        Publish(s => s with { Diagnostics = value }); return value;
    }
    public Task<System.Text.Json.JsonElement> OpenDiagnosticsAsync() => RequestAsync<System.Text.Json.JsonElement>("diagnostics.open");
    public Task<SensorDriverDto> ReadSensorDriverAsync() => RequestAsync<SensorDriverDto>("sensors.driver.status");
    public Task<SensorDebugDto> ReadSensorDebugAsync() => RequestAsync<SensorDebugDto>("sensors.debug");
    public Task<System.Text.Json.JsonElement> OpenSensorDriverFolderAsync() => RequestAsync<System.Text.Json.JsonElement>("sensors.driver.open");
    public async Task<FontCatalogDto> ReadFontsAsync()
    {
        var value = await RequestAsync<FontCatalogDto>("fonts").ConfigureAwait(false);
        Publish(s => s with { Fonts = value }); return value;
    }
    public StudioPreferences Preferences => model.Preferences;
    public StudioState Read() => model.Read();
    public void Edit(Action<StudioPreferences> edit) => model.Edit(edit);
    public void NormalizeCatalog(ThemeCatalogDto catalog) => model.Update(s =>
    {
        foreach (var category in catalog.Categories)
        {
            var remembered = s.ScreenThemes.GetValueOrDefault(category.Id);
            if (!catalog.Themes.Any(t => t.Category == category.Id && t.Id == remembered))
            { s.ScreenThemes[category.Id] = catalog.Themes.FirstOrDefault(t => t.Category == category.Id)?.Id ?? ""; }
        }
        var p = s.Preferences;
        if (!catalog.Categories.Any(c => c.Id == p.SelectedCategory))
        { p.SelectedCategory = catalog.Categories.FirstOrDefault()?.Id ?? "multimedia"; }
        if (!catalog.Themes.Any(t => t.Category == p.SelectedCategory && t.Id == p.SelectedTheme))
        { p.SelectedTheme = s.ScreenThemes.GetValueOrDefault(p.SelectedCategory, ""); }
        s.ScreenThemes[p.SelectedCategory] = p.SelectedTheme;
    });
    public void SelectCategory(string category, ThemeCatalogDto catalog) => model.Update(s =>
    {
        if (!catalog.Categories.Any(c => c.Id == category)) { return; }
        Select(s, category, s.ScreenThemes.GetValueOrDefault(category, ""));
    });
    public void SelectTheme(string theme, ThemeCatalogDto catalog) => model.Update(s =>
    {
        var selected = catalog.Themes.FirstOrDefault(t => t.Id == theme);
        if (selected is not null) { Select(s, selected.Category, selected.Id); }
    });
    internal static void Select(StudioState state, string category, string theme)
    {
        state.Preferences.SelectedCategory = category; state.Preferences.SelectedTheme = theme;
        state.Preferences.LastChange = Environment.TickCount64; state.ScreenThemes[category] = theme;
    }
    public void SetScreens(bool multimedia, bool codex, bool system) => model.Update(s =>
    {
        s.ScreenEnabled["multimedia"] = multimedia; s.ScreenEnabled["codex"] = codex; s.ScreenEnabled["system"] = system;
        s.Preferences.LastChange = Environment.TickCount64;
    });
    public void SetPalette(string key, ThemePalette? palette)
    {
        palette?.Validate();
        model.Update(s => { if (palette is null) { s.Palettes.Remove(key); } else { s.Palettes[key] = palette; } });
    }
}

public sealed record StudioAuxiliaryState(CodexSetupStatus? Codex = null, DiagnosticsStatus? Diagnostics = null, FontCatalogDto? Fonts = null);
