// VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT. VBWR E
using VibePanel.Contracts;
namespace VibePanel.Studio;

/// <summary>One transaction owns preferences, screen choices and palettes. All reads are detached.</summary>
public sealed class StudioModel
{
    private readonly object sync = new();
    private readonly StudioSettingsStore store;
    private StudioState state;
    public StudioModel(string path) { store = new(path); state = store.Load(); }
    public string SettingsPath => store.Path;
    public string? PersistenceNotice => store.Notice;
    public StudioState Read() { lock (sync) { return state.Copy(); } }
    public StudioPreferences Preferences => Read().Preferences;
    public IReadOnlyDictionary<string, string> ScreenThemes => Read().ScreenThemes;
    public IReadOnlyDictionary<string, bool> ScreenEnabled => Read().ScreenEnabled;
    public IReadOnlyDictionary<string, ThemePalette> Palettes => Read().Palettes;
    public void Update(Action<StudioState> edit)
    {
        lock (sync)
        {
            var next = state.Copy();
            edit(next);
            // Detach again: a caller retaining its editor cannot change committed state.
            next.Revision = state.Revision + 1;
            state = next.Copy();
        }
    }
    public void Edit(Action<StudioPreferences> edit) => Update(s => edit(s.Preferences));
    public ScreenSelectionDto[] Screens() => Read().Screens();
    public void Save() { lock (sync) { store.Save(state); } }
}

public sealed class StudioState
{
    public long Revision { get; internal set; }
    public StudioPreferences Preferences { get; init; } = new();
    public Dictionary<string, string> ScreenThemes { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, bool> ScreenEnabled { get; init; } = new(StringComparer.Ordinal)
    { ["multimedia"] = true, ["codex"] = false, ["system"] = true };
    public Dictionary<string, ThemePalette> Palettes { get; init; } = new(StringComparer.Ordinal);
    public StudioState Copy() => new()
    {
        Revision = Revision,
        Preferences = Preferences with { },
        ScreenThemes = new(ScreenThemes, StringComparer.Ordinal),
        ScreenEnabled = new(ScreenEnabled, StringComparer.Ordinal),
        Palettes = new(Palettes, StringComparer.Ordinal)
    };
    public ScreenSelectionDto[] Screens() => ScreenEnabled.OrderBy(p => p.Key)
        .Select(p => new ScreenSelectionDto(p.Key, ScreenThemes.GetValueOrDefault(p.Key, ""), p.Value)).ToArray();
}
