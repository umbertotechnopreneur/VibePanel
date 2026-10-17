// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using System.Text.Json;

namespace VibePanel.Core;

/// <summary>Stores only explicit local display choices; no USB serials or sensor history are saved.</summary>
public sealed record PanelSettings(string ProfileId = "auto", int Rotation = 0, string? ImagePath = null);

/// <summary>Persists panel choices atomically in the current user's application data directory.</summary>
public sealed class PanelSettingsStore
{
    private readonly string path;

    /// <summary>Uses the normal per-user destination, or a caller-supplied isolated destination for tests.</summary>
    public PanelSettingsStore(string? settingsPath = null)
    {
        path = settingsPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VibePanel", "settings.json");
    }

    /// <summary>Reads saved settings, returning defaults only when there is no saved file.</summary>
    public PanelSettings Load()
    {
        if (!File.Exists(path)) { return new PanelSettings(); }
        var settings = JsonSerializer.Deserialize<PanelSettings>(File.ReadAllText(path))
            ?? throw new IOException("The VibePanel settings file is empty or invalid.");
        Validate(settings);
        return settings;
    }

    /// <summary>Saves explicit choices without replacing the previous file until the new JSON is complete.</summary>
    public void Save(PanelSettings settings)
    {
        Validate(settings);
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, fullPath, overwrite: true);
        }
        finally { if (File.Exists(temporary)) { File.Delete(temporary); } }
    }

    private static void Validate(PanelSettings settings)
    {
        if (settings.Rotation is not (0 or 90 or 180 or 270)) { throw new ArgumentException("Rotation must be 0, 90, 180, or 270 degrees."); }
        if (settings.ProfileId != "auto") { DisplayProfileCatalog.GetRequired(settings.ProfileId); }
        if (settings.ImagePath is not null && !Path.IsPathFullyQualified(settings.ImagePath)) { throw new ArgumentException("The saved image path must be absolute."); }
    }
}
