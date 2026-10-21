// VBWR B
// Project: ThermalrightVibeWare | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using System.Windows;
using System.Windows.Media;

namespace ThermalrightVibeWare.App;

/// <summary>Color controls and preview sizing; full-frame filtering stays in the engine.</summary>
public sealed partial class MainPage
{
    private int previewPixelWidth, previewPixelHeight;
    private Color themeAccent = Color.FromArgb(255, 138, 186, 213);

    /// <summary>Build the fixed hue guide.</summary>
    private void InitializePreviewAppearance()
    {
        string[] spectrum = ["#00D5D5", "#366EFF", "#C447ED", "#EE4D59", "#F3D45A", "#5BD06D", "#00D5D5"];
        var gradient = new LinearGradientBrush { StartPoint = new Point(0, .5), EndPoint = new Point(1, .5) };
        for (var index = 0; index < spectrum.Length; index++)
        {
            gradient.GradientStops.Add(new GradientStop
            {
                Color = (Color)ColorConverter.ConvertFromString(spectrum[index]),
                Offset = index / (double)(spectrum.Length - 1)
            });
        }
        HueRamp.Background = gradient;
    }

    /// <summary>Keep drafts when filters are disabled, then request a frame with the new bypass state.</summary>
    private void Filters_Changed(object sender, RoutedEventArgs args)
    {
        if (!ready) { return; }

        colorChanges.Stop();
        if (pendingColorSettings) { CommitColorControls(); }

        colorFiltersEnabled = FiltersBox.IsChecked == true;
        UpdateFilterVisibility();
        SaveChoices();
        _ = RefreshAsync();
    }

    /// <summary>Hide all adjustment controls while leaving their values ready for the next enable.</summary>
    private void UpdateFilterVisibility()
    {
        var visibility = colorFiltersEnabled ? Visibility.Visible : Visibility.Collapsed;
        ColorControls.Visibility = visibility;
        ResetColorsButton.Visibility = visibility;
        FiltersStatus.Text = colorFiltersEnabled ? "On · adjust below" : "Off · original colors";
        if (colorFiltersEnabled) { UpdateColorRamps(); }
    }

    /// <summary>Reuse the renderer's cached artwork/theme accent without decoding a second image.</summary>
    private void UpdatePreviewAccent(string? hex)
    {
        if (hex is not { Length: 7 } || hex[0] != '#' || !hex[1..].All(Uri.IsHexDigit)) { return; }

        var accent = (Color)ColorConverter.ConvertFromString(hex);
        if (accent.Equals(themeAccent)) { return; }

        themeAccent = accent;
        if (colorFiltersEnabled) { UpdateColorRamps(); }
    }

    /// <summary>Update tiny color guides on input or accent changes, never on each unchanged frame.</summary>
    private void UpdateColorRamps()
    {
        var saturation = SaturationSlider.Value;
        var hue = HueSlider.Value;
        var brightness = BrightnessSlider.Value;
        var contrast = ContrastSlider.Value;

        SaturationRamp.Background = CreateColorRamp(position => PreviewTone(themeAccent, position * 200, hue, brightness, contrast));
        BrightnessRamp.Background = CreateColorRamp(position => PreviewTone(themeAccent, saturation, hue, position * 200 - 100, contrast));
        ContrastRamp.Background = CreateColorRamp(position => PreviewTone(themeAccent, saturation, hue, brightness, position * 200));
    }

    /// <summary>Sample nine guide colors; this does not touch preview or panel pixels.</summary>
    private static LinearGradientBrush CreateColorRamp(Func<double, Color> sample)
    {
        var gradient = new LinearGradientBrush { StartPoint = new Point(0, .5), EndPoint = new Point(1, .5) };
        for (var index = 0; index <= 8; index++)
        {
            var position = index / 8d;
            gradient.GradientStops.Add(new GradientStop { Offset = position, Color = sample(position) });
        }
        return gradient;
    }

    /// <summary>Apply the engine's saturation, hue and tone order to a single guide color.</summary>
    private static Color PreviewTone(Color source, double saturationPercent, double hueDegrees, double brightnessPercent, double contrastPercent)
    {
        var saturation = saturationPercent / 100;
        var luminance = (.213 * source.R + .715 * source.G + .072 * source.B) / 255;
        var red = luminance + (source.R / 255d - luminance) * saturation;
        var green = luminance + (source.G / 255d - luminance) * saturation;
        var blue = luminance + (source.B / 255d - luminance) * saturation;
        var angle = hueDegrees * Math.PI / 180;
        var cosine = Math.Cos(angle);
        var sine = Math.Sin(angle);

        // Keep intermediate channels unclamped, like the composed Skia matrix.
        var rotatedRed = (.213 + .787 * cosine - .213 * sine) * red
            + (.715 - .715 * cosine - .715 * sine) * green + (.072 - .072 * cosine + .928 * sine) * blue;
        var rotatedGreen = (.213 - .213 * cosine + .143 * sine) * red
            + (.715 + .285 * cosine + .140 * sine) * green + (.072 - .072 * cosine - .283 * sine) * blue;
        var rotatedBlue = (.213 - .213 * cosine - .787 * sine) * red
            + (.715 - .715 * cosine + .715 * sine) * green + (.072 + .928 * cosine + .072 * sine) * blue;
        var contrast = contrastPercent / 100;
        var offset = .5 * (1 - contrast) + brightnessPercent / 100;

        return Color.FromArgb(255, ToneChannel(rotatedRed, contrast, offset),
            ToneChannel(rotatedGreen, contrast, offset), ToneChannel(rotatedBlue, contrast, offset));
    }

    /// <summary>Clip the final guide channel to the display's byte range.</summary>
    private static byte ToneChannel(double value, double contrast, double offset) =>
        (byte)Math.Round(Math.Clamp(value * contrast + offset, 0, 1) * 255);

    /// <summary>Refit the image when the available viewport changes.</summary>
    private void Preview_SizeChanged(object sender, SizeChangedEventArgs args) => FitPreviewSurface();

    /// <summary>Fit the preview and its solid backing, including portrait and square panels.</summary>
    private void FitPreviewSurface()
    {
        if (lastFrame is null || PreviewSurface is null) { return; }
        if (previewPixelWidth <= 0 || previewPixelHeight <= 0 || PreviewFrame.ActualWidth <= 0 || PreviewFrame.ActualHeight <= 0) { return; }

        var fit = Math.Min(PreviewFrame.ActualWidth / previewPixelWidth, PreviewFrame.ActualHeight / previewPixelHeight);
        var width = previewPixelWidth * fit;
        var height = previewPixelHeight * fit;
        if (PreviewSurface.Width != width) { PreviewSurface.Width = width; }
        if (PreviewSurface.Height != height) { PreviewSurface.Height = height; }
    }
}
