// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VibePanel.Contracts;

namespace VibePanel.App;

public sealed partial class MainPage : Page
{
    private readonly IPanelClient client;
    private readonly DispatcherTimer refresh = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly DispatcherTimer levels = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly Dictionary<string, MeterVisual> meters = [];
    private readonly string[] rotation = ["Music", "Artwork", "Mixer", "System", "Network"];
    private bool refreshing, readingLevels, unloaded;
    private long lastChange = Environment.TickCount64;
    private string? lastScene;
    private byte[]? lastFrame;
    private Storyboard? fade;
    private sealed record MeterVisual(Grid Track, System.Windows.Shapes.Rectangle Fill, TextBlock Name, TextBlock Level);

    public MainPage(IPanelClient client)
    {
        this.client = client;
        InitializeComponent();
        ProfilesBox.ItemsSource = new[] { new PanelChoice("auto", "Automatic (from panel)") };
        ProfilesBox.SelectedIndex = 0;
        ScenesBox.ItemsSource = new[] { new PanelChoice("Music", "Now playing") };
        ScenesBox.SelectedIndex = 0;
        Loaded += async (_, _) => { unloaded = false; await RefreshAsync(); if (!unloaded) { refresh.Start(); levels.Start(); } };
        Unloaded += (_, _) => { unloaded = true; refresh.Stop(); levels.Stop(); fade?.Stop(); };
        refresh.Tick += async (_, _) => await RefreshAsync();
        levels.Tick += async (_, _) => await LevelsAsync();
    }

    private async Task RefreshAsync()
    {
        if (refreshing || unloaded) { return; }
        refreshing = true;
        try
        {
            if (CycleBox.IsChecked == true && Environment.TickCount64 - lastChange >= 20000 && ScenesBox.ItemsSource is IReadOnlyList<PanelChoice> choices)
            {
                var current = (ScenesBox.SelectedItem as PanelChoice)?.Id ?? "Music";
                var next = rotation[(Array.IndexOf(rotation, current) + 1) % rotation.Length];
                ScenesBox.SelectedItem = choices.FirstOrDefault(item => item.Id == next);
                lastChange = Environment.TickCount64;
            }
            var scene = (ScenesBox.SelectedItem as PanelChoice)?.Id ?? "Music";
            var profile = (ProfilesBox.SelectedItem as PanelChoice)?.Id ?? "auto";
            var state = await client.RequestAsync<PanelState>("state", new PreviewOptions(profile == "auto" ? "wide-1280x480" : profile, scene));
            if (unloaded) { return; }
            UpdateChoices(DevicesBox, state.Devices);
            UpdateChoices(ProfilesBox, new[] { new PanelChoice("auto", "Automatic (from panel)") }.Concat(state.Profiles).ToArray());
            UpdateChoices(ScenesBox, state.Scenes);
            MediaSourceText.Text = string.IsNullOrWhiteSpace(state.Source) ? "Open Spotify and play something" : state.Source + " · local media session";
            ToggleButton.Content = state.Playing ? "Pause" : "Play";
            CpuUsageText.Text = Metric(state.System.CpuUsage, "%");
            CpuTempText.Text = Metric(state.System.CpuTemperature, "°C");
            GpuUsageText.Text = Metric(state.System.GpuUsage, "%");
            GpuTempText.Text = Metric(state.System.GpuTemperature, "°C");
            SensorSourceText.Text = state.System.GpuName + "\nCPU temperature: " + state.System.CpuTemperatureSource;
            NetworkText.Text = state.Network is { } network
                ? $"↓ {Size(network.DownloadBytesPerSecond)}/s    ↑ {Size(network.UploadBytesPerSecond)}/s    ·    total ↓ {Size(network.DownloadBytes)}    ↑ {Size(network.UploadBytes)}" : "Network counters unavailable";
            StartButton.IsEnabled = !state.DisplayRunning && state.Devices.Count > 0;
            StopButton.IsEnabled = state.DisplayRunning;
            ProfilesBox.IsEnabled = ScenesBox.IsEnabled = CycleBox.IsEnabled = !state.DisplayRunning;
            StatusText.Text = state.Status + (state.Notices.Count > 0 ? " · " + string.Join(" · ", state.Notices) : "");
            UpdateMeters(state.Audio);
            if (state.FrameJpeg is { } frame && (lastFrame is null || !lastFrame.AsSpan().SequenceEqual(frame)))
            {
                fade?.Stop();
                PreviousPreview.Source = PreviewImage.Source;
                using var stream = new MemoryStream(frame);
                var image = new BitmapImage();
                image.SetSource(stream);
                PreviewImage.Source = image;
                PreviewImage.Opacity = 1;
                LoadingText.Visibility = Visibility.Collapsed;
                if (lastScene is not null && lastScene != scene)
                {
                    var animation = new DoubleAnimation { From = 0, To = 1, Duration = new Duration(TimeSpan.FromMilliseconds(650)) };
                    Storyboard.SetTarget(animation, PreviewImage);
                    Storyboard.SetTargetProperty(animation, new PropertyPath("Opacity"));
                    fade = new Storyboard();
                    fade.Children.Add(animation);
                    fade.Begin();
                }
                lastFrame = frame;
                lastScene = scene;
            }
        }
        catch (Exception error) { StatusText.Text = error.Message; LoadingText.Text = "Local engine unavailable. See the message below."; }
        finally { refreshing = false; }
    }

    private static void UpdateChoices(ComboBox box, IReadOnlyList<PanelChoice> choices)
    {
        if (box.ItemsSource is IEnumerable<PanelChoice> current && current.SequenceEqual(choices)) { return; }
        var id = (box.SelectedItem as PanelChoice)?.Id;
        box.ItemsSource = choices;
        box.SelectedItem = choices.FirstOrDefault(item => item.Id == id) ?? choices.FirstOrDefault();
    }

    private void UpdateMeters(IReadOnlyList<AudioLevelDto> audio)
    {
        if (!meters.Keys.SequenceEqual(audio.Select(item => item.Id)))
        {
            meters.Clear();
            MeterRows.Children.Clear();
            foreach (var session in audio)
            {
                var row = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
                var header = new Grid();
                header.ColumnDefinitions.Add(new ColumnDefinition());
                header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var name = new TextBlock { FontSize = 12, Foreground = Brush("#EFF0F6"), TextTrimming = TextTrimming.CharacterEllipsis };
                var level = new TextBlock { FontSize = 12, Foreground = Brush("#BFC4D0"), Margin = new Thickness(12, 0, 0, 0), FontFamily = new FontFamily("Consolas") };
                Grid.SetColumn(level, 1);
                header.Children.Add(name); header.Children.Add(level);
                var track = new Grid { Height = 16, Margin = new Thickness(0, 6, 0, 0), Background = Brush("#1C3529") };
                var gradient = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };
                gradient.GradientStops.Add(new GradientStop { Offset = 0, Color = Color.FromArgb(255, 98, 199, 119) });
                gradient.GradientStops.Add(new GradientStop { Offset = 0.55, Color = Color.FromArgb(255, 181, 220, 106) });
                gradient.GradientStops.Add(new GradientStop { Offset = 0.8, Color = Color.FromArgb(255, 246, 197, 105) });
                gradient.GradientStops.Add(new GradientStop { Offset = 1, Color = Color.FromArgb(255, 224, 131, 177) });
                var fill = new System.Windows.Shapes.Rectangle { Fill = gradient, RadiusX = 4, RadiusY = 4 };
                track.Children.Add(fill);
                meters[session.Id] = new(track, fill, name, level);
                row.Children.Add(header); row.Children.Add(track); MeterRows.Children.Add(row);
            }
            if (audio.Count == 0) { MeterRows.Children.Add(new TextBlock { Text = "Start an audio app to see its level.", Foreground = Brush("#AEB5C2"), FontSize = 12 }); }
        }
        foreach (var session in audio)
        {
            meters[session.Id].Name.Text = $"{session.Name} · {session.VolumePercent:F0}%{(session.Muted ? " · muted" : "")}";
            SetLevel(meters[session.Id], session.Peak);
        }
    }

    private async Task LevelsAsync()
    {
        if (readingLevels || refreshing || meters.Count == 0 || unloaded) { return; }
        readingLevels = true;
        try
        {
            var peaks = await client.RequestAsync<Dictionary<string, double?>>("levels");
            if (!unloaded) { foreach (var (id, peak) in peaks) { if (meters.TryGetValue(id, out var meter)) { SetLevel(meter, peak); } } }
        }
        catch (Exception error) { StatusText.Text = error.Message; }
        finally { readingLevels = false; }
    }

    private static void SetLevel(MeterVisual meter, double? peak)
    {
        var fraction = peak is > 0 ? Math.Clamp((20 * Math.Log10(peak.Value) + 60) / 60, 0, 1) : 0;
        meter.Fill.Clip = new RectangleGeometry { Rect = new Rect(0, 0, Math.Max(0, meter.Track.ActualWidth * fraction), 16) };
        meter.Level.Text = peak is null ? "— dBFS" : peak <= 0 ? "−∞ dBFS" : $"{20 * Math.Log10(peak.Value):F1} dBFS";
    }

    private async void Refresh_Click(object sender, RoutedEventArgs args) => await RefreshAsync();
    private void Scene_Changed(object sender, SelectionChangedEventArgs args) { lastChange = Environment.TickCount64; if (refresh.IsEnabled) { _ = RefreshAsync(); } }
    private async void Media_Click(object sender, RoutedEventArgs args)
    {
        try { await client.RequestAsync<object>("media", new MediaOptions((string)((Button)sender).Tag)); await RefreshAsync(); }
        catch (Exception error) { StatusText.Text = error.Message; }
    }
    private async void Start_Click(object sender, RoutedEventArgs args)
    {
        if (DevicesBox.SelectedItem is not PanelChoice device) { StatusText.Text = "Choose a connected panel first."; return; }
        StartButton.IsEnabled = false;
        try
        {
            await client.RequestAsync<object>("start", new StartOptions(device.Id, (ProfilesBox.SelectedItem as PanelChoice)?.Id ?? "auto", CycleBox.IsChecked == true ? null : (ScenesBox.SelectedItem as PanelChoice)?.Id ?? "Music"));
            await RefreshAsync();
        }
        catch (Exception error) { StatusText.Text = error.Message; StartButton.IsEnabled = true; }
    }
    private async void Stop_Click(object sender, RoutedEventArgs args)
    {
        try { await client.RequestAsync<object>("stop"); await RefreshAsync(); }
        catch (Exception error) { StatusText.Text = error.Message; }
    }
    private static string Metric(double? value, string unit) => value is null ? "—" : $"{value:F0}{unit}";
    private static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
    private static string Size(double? bytes)
    {
        if (bytes is null) { return "—"; }
        string[] units = ["B", "KiB", "MiB", "GiB", "TiB"];
        var value = bytes.Value; var unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return $"{value:F1} {units[unit]}";
    }
}
