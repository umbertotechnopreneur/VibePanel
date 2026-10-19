// VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT. VBWR E
namespace VibePanel.Contracts;

/// <summary>Optional semantic overrides. Null inherits the theme's light/dark value.</summary>
public sealed record ThemePalette(string? TextPrimary = null, string? TextSecondary = null,
    string? Accent = null, string? Warning = null, string? Info = null)
{
    public void Validate()
    {
        foreach (var color in new[] { TextPrimary, TextSecondary, Accent, Warning, Info })
        {
            if (color is not null && (color.Length != 7 || color[0] != '#' || !color[1..].All(Uri.IsHexDigit)))
            { throw new ArgumentException("Palette colors must be #RRGGBB, or empty to inherit."); }
        }
    }
}
