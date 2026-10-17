// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using System.Text.Json;

namespace VibePanel.Core;

public sealed record ThemeDefinition(string Id, string Name, string Category, string Description, string PackId, string DirectoryPath, bool SupportsSpectrum = false);
public sealed record ThemeCatalogSnapshot(IReadOnlyList<ThemeDefinition> Themes, IReadOnlyList<string> Notices);

/// <summary>Finds bounded, manifest-backed theme folders; never imports or executes scripts while listing.</summary>
public static class ThemeCatalog
{
    public static string UserThemesPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VibePanel", "themes");

    public static string? FindContentDirectory(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        for (var depth = 0; directory is not null && depth < 9; depth++, directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, name);
            if (Directory.Exists(candidate)) { return candidate; }
        }
        return null;
    }

    public static ThemeCatalogSnapshot Read()
    {
        var notices = new List<string>();
        var themes = new Dictionary<string, ThemeDefinition>(StringComparer.OrdinalIgnoreCase);
        var bundled = FindContentDirectory("themes");
        var roots = new[] { bundled, UserThemesPath }.Where(path => path is not null).Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
        {
            if (!Directory.Exists(root)) { continue; }
            try
            {
                foreach (var pack in Directory.EnumerateDirectories(root!).Order(StringComparer.OrdinalIgnoreCase).Take(64))
                {
                    if ((File.GetAttributes(pack) & FileAttributes.ReparsePoint) != 0) { continue; }
                    var themeRoot = Path.Combine(pack, "themes");
                    if (!Directory.Exists(themeRoot)) { continue; }
                    var packId = Path.GetFileName(pack);
                    var packManifest = Path.Combine(pack, "pack.json");
                    if (File.Exists(packManifest))
                    {
                        try
                        {
                            using var json = ReadManifest(packManifest);
                            ValidateSchema(json.RootElement);
                            packId = Text(json.RootElement, "id", packId);
                            if (string.IsNullOrWhiteSpace(packId) || packId.Length > 100) { throw new ArgumentException("Pack ID must contain 1–100 characters."); }
                        }
                        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException)
                        { notices.Add($"Pack {packId}: {error.Message}"); continue; }
                    }
                    foreach (var folder in Directory.EnumerateDirectories(themeRoot).Order(StringComparer.OrdinalIgnoreCase).Take(256))
                    {
                        if (themes.Count >= 256) { notices.Add("Theme limit reached (256). Remaining folders were skipped."); return Result(); }
                        if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0) { continue; }
                        var manifest = Path.Combine(folder, "theme.json");
                        if (!File.Exists(manifest)) { continue; }
                        try
                        {
                            using var json = ReadManifest(manifest);
                            var entry = json.RootElement;
                            ValidateSchema(entry);
                            var id = Text(entry, "id");
                            var name = Text(entry, "name");
                            var category = Text(entry, "category");
                            var description = Text(entry, "description");
                            if (id.Length is 0 or > 100 || !id.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_'))
                            { throw new ArgumentException("Theme ID must contain 1–100 letters, digits, dots, hyphens or underscores."); }
                            if (category is not ("multimedia" or "codex")) { throw new ArgumentException("Category must be multimedia or codex."); }
                            if (string.IsNullOrWhiteSpace(name) || name.Length > 160) { throw new ArgumentException("Theme name must contain 1–160 characters."); }
                            if (description.Length > 1200) { throw new ArgumentException("Theme description exceeds 1200 characters."); }
                            var script = Text(entry, "entry", "render.py");
                            if (Path.GetFileName(script) != script || !script.EndsWith(".py", StringComparison.OrdinalIgnoreCase) || !File.Exists(Path.Combine(folder, script)))
                            { throw new ArgumentException("Entry must name an existing Python file in the theme folder."); }
                            var supportsSpectrum = entry.TryGetProperty("settings", out var settings) && settings.ValueKind == JsonValueKind.Object
                                && settings.TryGetProperty("showSpectrum", out var spectrum) && spectrum.ValueKind == JsonValueKind.Object
                                && Text(spectrum, "type") == "boolean";
                            if (!themes.TryAdd(id, new(id, name, category, description, packId, folder, supportsSpectrum)))
                            { notices.Add($"Duplicate theme ID {id} was skipped; the first installed definition wins."); }
                        }
                        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException)
                        { notices.Add($"Theme {Path.GetFileName(folder)}: {error.Message}"); }
                    }
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            { notices.Add($"Cannot read theme folder: {error.Message}"); }
        }
        if (themes.Count == 0) { notices.Add("No themes found. Add a pack under themes/<pack>/themes/<theme>/theme.json."); }
        return Result();

        ThemeCatalogSnapshot Result() => new(themes.Values.OrderBy(theme => theme.Category).ThenBy(theme => theme.Name, StringComparer.OrdinalIgnoreCase).ToArray(), notices);
    }

    private static JsonDocument ReadManifest(string path)
    {
        if (new FileInfo(path).Length > 64 * 1024) { throw new ArgumentException("Manifest exceeds 64 KiB."); }
        using var stream = File.OpenRead(path);
        return JsonDocument.Parse(stream, new JsonDocumentOptions { MaxDepth = 16 });
    }

    private static string Text(JsonElement element, string property, string fallback = "") =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? fallback : fallback;

    private static void ValidateSchema(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty("schema", out var schema)
            || schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out var version) || version != 1)
        { throw new ArgumentException("Manifest requires schema: 1."); }
    }
}
