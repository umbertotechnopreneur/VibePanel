// VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT. VBWR E
using System.Collections.Concurrent;
using System.Text.Json;
using VibePanel.Contracts;
namespace VibePanel.Studio;

/// <summary>UI-independent user choices. Views read copies and submit edits.</summary>
public sealed class StudioModel
{
    private readonly object sync = new();
    private readonly string path;
    private StudioPreferences preferences = new();
    private static readonly int[] RefreshIntervals = [1000, 500, 250, 100, 50, 25];
    public ConcurrentDictionary<string, string> ScreenThemes { get; } = new(StringComparer.Ordinal);
    public ConcurrentDictionary<string, bool> ScreenEnabled { get; } = new(new Dictionary<string, bool> { ["multimedia"] = true, ["codex"] = false, ["system"] = true }, StringComparer.Ordinal);
    public ConcurrentDictionary<string, ThemePalette> Palettes { get; } = new(StringComparer.Ordinal);
    public string SettingsPath => path;
    public StudioModel(string path) { this.path = path; Load(); }
    public StudioPreferences Preferences { get { lock (sync) { return preferences with { }; } } }
    public void Edit(Action<StudioPreferences> edit) { lock (sync) { var next = preferences with { }; edit(next); preferences = next; } }
    public ScreenSelectionDto[] Screens() => ScreenEnabled.OrderBy(p => p.Key).Select(p => new ScreenSelectionDto(p.Key, ScreenThemes.GetValueOrDefault(p.Key, ""), p.Value)).ToArray();
    public void Save()
    {
        lock (sync)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            var p = preferences;
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(new
            {
                theme = p.Appearance,
                category = p.SelectedCategory,
                themeId = p.SelectedTheme,
                profile = p.SelectedProfile,
                profileSelection = p.ProfileSelection,
                font = p.FontFamily,
                fontScale = p.FontScale,
                refreshMilliseconds = p.RefreshMilliseconds,
                jpegQuality = p.JpegQuality,
                saturationPercent = p.SaturationPercent,
                hueDegrees = p.HueDegrees,
                brightnessPercent = p.BrightnessPercent,
                contrastPercent = p.ContrastPercent,
                showSpectrum = p.ShowSpectrum,
                colorFiltersEnabled = p.ColorFiltersEnabled,
                cycleThemes = p.CycleThemes,
                screens = Screens(),
                deviceId = p.SelectedDeviceId,
                palettes = Palettes.ToDictionary()
            }, PanelContract.Json));
            File.Move(temporary, path, overwrite: true);
        }
    }
    private void Load()
    {
        try
        {
            if (!File.Exists(path)) { return; }
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var json = document.RootElement;
            if (json.TryGetProperty("palettes", out var palettes) && palettes.ValueKind == JsonValueKind.Object)
            {
                foreach (var item in palettes.EnumerateObject().Take(256))
                {
                    try { var palette = item.Value.Deserialize<ThemePalette>(PanelContract.Json); palette?.Validate(); if (palette is not null) { Palettes[item.Name] = palette; } }
                    catch (Exception error) when (error is JsonException or ArgumentException) { }
                }
            }
            if (json.TryGetProperty("theme", out var mode) && mode.TryGetInt32(out var value)) { preferences.Appearance = value is >= 0 and <= 2 ? value : 0; }
            if (json.TryGetProperty("category", out var category)) { preferences.SelectedCategory = category.GetString() ?? preferences.SelectedCategory; }
            if (json.TryGetProperty("themeId", out var theme)) { preferences.SelectedTheme = theme.GetString() ?? ""; }
            if (!string.IsNullOrEmpty(preferences.SelectedTheme)) { ScreenThemes[preferences.SelectedCategory] = preferences.SelectedTheme; }
            if (json.TryGetProperty("screens", out var screens) && screens.ValueKind == JsonValueKind.Array)
            {
                foreach (var screen in screens.EnumerateArray())
                {
                    if (screen.ValueKind != JsonValueKind.Object || !screen.TryGetProperty("category", out var screenCategory)
                        || screenCategory.ValueKind != JsonValueKind.String || screenCategory.GetString() is not { } categoryId
                        || !ScreenEnabled.ContainsKey(categoryId)) { continue; }
                    if (screen.TryGetProperty("enabled", out var enabled) && enabled.ValueKind is JsonValueKind.True or JsonValueKind.False)
                    { ScreenEnabled[categoryId] = enabled.GetBoolean(); }
                    if (screen.TryGetProperty("themeId", out var screenTheme) && screenTheme.ValueKind == JsonValueKind.String)
                    { ScreenThemes[categoryId] = screenTheme.GetString() ?? ""; }
                }
            }
            else if (preferences.SelectedCategory == "codex")
            {
                // Preserve a saved Codex session from settings predating screen toggles.
                ScreenEnabled["codex"] = true;
            }
            if (json.TryGetProperty("profile", out var profile)) { preferences.SelectedProfile = profile.GetString() ?? preferences.SelectedProfile; }
            if (json.TryGetProperty("profileSelection", out var profileChoice)) { preferences.ProfileSelection = profileChoice.GetString() ?? "auto"; }
            if (json.TryGetProperty("font", out var font)) { preferences.FontFamily = font.GetString() ?? preferences.FontFamily; }
            if (json.TryGetProperty("fontScale", out var scale) && scale.TryGetDouble(out var number) && double.IsFinite(number)) { preferences.FontScale = Math.Clamp(number, 0.7, 1.5); }
            if (json.TryGetProperty("refreshMilliseconds", out var interval) && interval.TryGetInt32(out var milliseconds) && RefreshIntervals.Contains(milliseconds)) { preferences.RefreshMilliseconds = milliseconds; }
            if (json.TryGetProperty("jpegQuality", out var quality) && quality.GetString() is "poor" or "low" or "medium" or "high") { preferences.JpegQuality = quality.GetString()!; }
            if (json.TryGetProperty("saturationPercent", out var saturation) && saturation.TryGetDouble(out var saturationValue) && double.IsFinite(saturationValue)) { preferences.SaturationPercent = Math.Clamp(saturationValue, 0, 200); }
            if (json.TryGetProperty("hueDegrees", out var hue) && hue.TryGetDouble(out var hueValue) && double.IsFinite(hueValue)) { preferences.HueDegrees = Math.Clamp(hueValue, -180, 180); }
            if (json.TryGetProperty("brightnessPercent", out var brightness) && brightness.TryGetDouble(out var brightnessValue) && double.IsFinite(brightnessValue)) { preferences.BrightnessPercent = Math.Clamp(brightnessValue, -100, 100); }
            if (json.TryGetProperty("contrastPercent", out var contrast) && contrast.TryGetDouble(out var contrastValue) && double.IsFinite(contrastValue)) { preferences.ContrastPercent = Math.Clamp(contrastValue, 0, 200); }
            if (json.TryGetProperty("showSpectrum", out var spectrum) && spectrum.ValueKind is JsonValueKind.True or JsonValueKind.False) { preferences.ShowSpectrum = spectrum.GetBoolean(); }
            if (json.TryGetProperty("colorFiltersEnabled", out var filters) && filters.ValueKind is JsonValueKind.True or JsonValueKind.False) { preferences.ColorFiltersEnabled = filters.GetBoolean(); }
            if (json.TryGetProperty("cycleThemes", out var cycle) && cycle.ValueKind is JsonValueKind.True or JsonValueKind.False) { preferences.CycleThemes = cycle.GetBoolean(); }
            if (json.TryGetProperty("deviceId", out var device) && device.ValueKind == JsonValueKind.String) { preferences.SelectedDeviceId = device.GetString(); }
        }
        catch (Exception error) when (error is IOException or JsonException or InvalidOperationException or UnauthorizedAccessException) { }
    }
}
