// VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT. VBWR E
using System.Windows;
using VibePanel.Contracts;

namespace VibePanel.App;

public sealed partial class MainPage
{
    private string? editingPaletteKey;
    private void Palette_Click(object sender, RoutedEventArgs args)
    {
        // Editing a palette must not follow automatic theme rotation mid-edit.
        editingPaletteKey = selectedTheme + (IsLight ? "@light" : "@dark");
        UpdatePaletteEditor();
        PaletteOverlay.Visibility = Visibility.Visible;
    }

    private void UpdatePaletteEditor()
    {
        if (editingPaletteKey is null) { return; }
        var palette = session.Model.Palettes.GetValueOrDefault(editingPaletteKey) ?? new ThemePalette();
        PaletteHeading.Text = "Theme colors · " + editingPaletteKey;
        PalettePrimary.Text = palette.TextPrimary ?? "";
        PaletteSecondary.Text = palette.TextSecondary ?? "";
        PaletteAccent.Text = palette.Accent ?? "";
        PaletteWarning.Text = palette.Warning ?? "";
        PaletteInfo.Text = palette.Info ?? "";
        PaletteError.Text = "";
    }

    private static string? PaletteColor(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim().ToUpperInvariant();

    private void ApplyPalette_Click(object sender, RoutedEventArgs args)
    {
        if (editingPaletteKey is null) { return; }
        try
        {
            var palette = new ThemePalette(PaletteColor(PalettePrimary.Text), PaletteColor(PaletteSecondary.Text),
                PaletteColor(PaletteAccent.Text), PaletteColor(PaletteWarning.Text), PaletteColor(PaletteInfo.Text));
            palette.Validate();
            viewModel.SetPalette(editingPaletteKey, palette);
            SaveChoices(); session.Wake();
            PaletteOverlay.Visibility = Visibility.Collapsed;
            editingPaletteKey = null;
        }
        catch (ArgumentException error) { PaletteError.Text = error.Message; }
    }

    private void ResetPalette_Click(object sender, RoutedEventArgs args)
    {
        if (editingPaletteKey is null) { return; }
        viewModel.SetPalette(editingPaletteKey, null);
        SaveChoices(); session.Wake(); UpdatePaletteEditor();
    }

    private void ClosePalette_Click(object sender, RoutedEventArgs args)
    { PaletteOverlay.Visibility = Visibility.Collapsed; editingPaletteKey = null; }
}
