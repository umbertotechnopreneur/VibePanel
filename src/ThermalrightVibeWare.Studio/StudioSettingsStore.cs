// VBWR B — Thermalright VibeWare. Copyright (c) 2026 Umberto Giacobbi. MIT. VBWR E
using System.Text.Json;
using ThermalrightVibeWare.Contracts;
namespace ThermalrightVibeWare.Studio;

/// <summary>Versioned, bounded JSON settings with atomic replacement and last-good recovery.</summary>
public sealed class StudioSettingsStore(string path)
{
    private const int MaximumBytes = 1024 * 1024;
    private static readonly int[] RefreshIntervals = [1000, 500, 250, 100, 50, 25];
    private bool readOnly, recovered;
    public string Path { get; } = System.IO.Path.GetFullPath(path);
    public string? Notice { get; private set; }
    public StudioState Load()
    {
        try { return File.Exists(Path) ? Parse(Path) : new(); }
        catch (NotSupportedException error) { readOnly = true; Notice = error.Message; return new(); }
        catch (Exception error) when (error is IOException or JsonException or InvalidOperationException or UnauthorizedAccessException or FormatException)
        {
            Notice = "Settings could not be read. Defaults are in use.";
            try
            {
                if (File.Exists(Path + ".bak"))
                {
                    var backup = Parse(Path + ".bak"); recovered = true;
                    Notice = "Settings were recovered from the last saved backup.";
                    return backup;
                }
            }
            catch (Exception backupError) when (backupError is IOException or JsonException or InvalidOperationException or UnauthorizedAccessException or FormatException or NotSupportedException) { }
            readOnly = true; // Preserve damaged settings for recovery instead of overwriting them on exit.
            return new();
        }
    }
    public void Save(StudioState state)
    {
        if (readOnly) { throw new IOException(Notice); }
        var json = Serialize(state);
        if (System.Text.Encoding.UTF8.GetByteCount(json) > MaximumBytes) { throw new IOException("Settings exceed the size limit."); }
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        var temporary = Path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                file.Write(bytes); file.Flush(flushToDisk: true);
            }
            if (File.Exists(Path)) { File.Replace(temporary, Path, recovered ? null : Path + ".bak"); }
            else { File.Move(temporary, Path); }
            recovered = false;
        }
        finally { if (File.Exists(temporary)) { File.Delete(temporary); } }
    }
    private static string Serialize(StudioState state)
    {
        var p = state.Preferences;
        return JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
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
            screens = state.Screens(),
            deviceId = p.SelectedDeviceId,
            palettes = state.Palettes
        }, PanelContract.Json);
    }
    private static StudioState Parse(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > MaximumBytes) { throw new IOException("Settings exceed the size limit."); }
        using var document = JsonDocument.Parse(stream);
        var json = document.RootElement;
        if (json.ValueKind != JsonValueKind.Object) { throw new JsonException("Settings must be an object."); }
        if (!json.TryGetProperty("schemaVersion", out var version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var v) || v != 1)
        { throw new NotSupportedException("Settings use an unsupported version and will not be overwritten."); }
        var state = new StudioState();
        var preferences = state.Preferences;
        var ScreenThemes = state.ScreenThemes;
        var ScreenEnabled = state.ScreenEnabled;
        var Palettes = state.Palettes;
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
        return state;
    }
}
