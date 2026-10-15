// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using VibePanel.Core;
using Windows.Graphics;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Media.Animation;

namespace VibePanel.App;

/// <summary>Provides the compact desktop surface for discovery, checks, and diagnostic frames.</summary>
public sealed partial class MainWindow : Window
{
    private readonly PanelApplication application = new();
    private PanelSettings settings = new();
    private CancellationTokenSource? playback;
    private readonly DashboardService dashboard = new();
    private readonly DashboardRenderer renderer = new();
    private readonly DispatcherTimer refreshTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly Dictionary<string, MeterVisual> meters = [];
    private readonly DispatcherTimer meterTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private bool refreshing, closing;
    private sealed record MeterVisual(Grid Track, Microsoft.UI.Xaml.Shapes.Rectangle Fill, TextBlock Label, TextBlock Level);
    private string? previewFingerprint;
    private long lastSceneChange = Environment.TickCount64;
    private DashboardScene visibleScene;
    /// <summary>Initializes the window with the VibeWare visual language and safe default state.</summary>
    public MainWindow()
    {
        InitializeComponent();
        SystemBackdrop = new MicaBackdrop();
        var titleBar = AppWindow.TitleBar;
        titleBar.BackgroundColor = titleBar.InactiveBackgroundColor = titleBar.ButtonBackgroundColor = titleBar.ButtonInactiveBackgroundColor = Windows.UI.Color.FromArgb(255, 21, 25, 31);
        titleBar.ForegroundColor = titleBar.ButtonForegroundColor = Microsoft.UI.Colors.White;
        AppWindow.Resize(new SizeInt32(1150, 970));
        ScenesBox.ItemsSource = Enum.GetValues<DashboardScene>().Select(DashboardScenes.Name).ToList();
        ScenesBox.SelectedIndex = 0;
        ProfilesBox.ItemsSource = new[] { new DisplayProfile("auto", "Automatic (from panel)", 0, 0) }.Concat(DisplayProfileCatalog.All).ToList();
        ProfilesBox.SelectedIndex = 0;
        try
        {
            settings = application.LoadSettings();
            ProfilesBox.SelectedItem = ((IEnumerable<DisplayProfile>)ProfilesBox.ItemsSource).First(item => item.Id == settings.ProfileId);
            RotationBox.SelectedIndex = settings.Rotation / 90;
            ImageLabel.Text = settings.ImagePath is null ? "No image selected" : System.IO.Path.GetFileName(settings.ImagePath);
        }
        catch (Exception error) { StatusText.Text = $"Could not load my settings: {error.Message}"; }
        refreshTimer.Tick += async (_, _) => await RefreshDashboardAsync();
        meterTimer.Tick += (_, _) => UpdateLevels(dashboard.Audio.ReadLevels());
        ((FrameworkElement)Content).Loaded += async (_, _) => { Refresh_Click(this, new RoutedEventArgs()); await RefreshDashboardAsync(); if (!closing) { refreshTimer.Start(); meterTimer.Start(); } };
        Closed += (_, _) =>
        {
            closing = true;
            refreshTimer.Stop();
            meterTimer.Stop();
            playback?.Cancel();
            if (!refreshing) { dashboard.Dispose(); }
        };
    }

    private void Scene_Changed(object sender, SelectionChangedEventArgs args)
    {
        lastSceneChange = Environment.TickCount64;
        if (refreshTimer.IsEnabled) { _ = RefreshDashboardAsync(); }
    }

    private async Task RefreshDashboardAsync()
    {
        if (refreshing || closing) { return; }
        refreshing = true;
        try
        {
            var snapshot = await dashboard.ReadAsync();
            if (closing) { return; }
            if (CycleSwitch.IsOn && Environment.TickCount64 - lastSceneChange >= 20000)
            {
                var current = Array.IndexOf(DashboardScenes.Rotation.ToArray(), (DashboardScene)ScenesBox.SelectedIndex);
                ScenesBox.SelectedIndex = (int)DashboardScenes.Rotation[(current + 1) % DashboardScenes.Rotation.Count];
                lastSceneChange = Environment.TickCount64;
            }
            var scene = (DashboardScene)Math.Max(0, ScenesBox.SelectedIndex);
            var previewProfile = ProfilesBox.SelectedItem is DisplayProfile { Width: > 0 } selected ? selected : DisplayProfileCatalog.GetRequired("wide-1280x480");
            previewProfile = previewProfile with { Encoding = PanelPixelEncoding.Jpeg, WireRotation = 0 };
            MediaSourceText.Text = string.IsNullOrEmpty(snapshot.Media.Source) ? "Open Spotify and play something"
                : snapshot.Media.Source.Contains("spotify", StringComparison.OrdinalIgnoreCase) ? "Spotify · Windows media session" : snapshot.Media.Source;
            ToggleButton.Content = snapshot.Media.Playing ? "Pause" : "Play";
            CpuUsageText.Text = DashboardRenderer.Metric(snapshot.Metrics.CpuUsage, "%");
            CpuTempText.Text = DashboardRenderer.Metric(snapshot.Metrics.CpuTemperature, "°C");
            GpuUsageText.Text = DashboardRenderer.Metric(snapshot.Metrics.GpuUsage, "%");
            GpuTempText.Text = DashboardRenderer.Metric(snapshot.Metrics.GpuTemperature, "°C");
            SensorSourceText.Text = $"{snapshot.Metrics.GpuName}\nCPU temperature: {snapshot.Metrics.CpuTemperatureSource}";
            UpdateMixer(snapshot.Volumes);
            var fingerprint = DashboardRenderer.Fingerprint(snapshot, scene) + previewProfile.Id;
            if (previewFingerprint != fingerprint)
            {
                var bytes = await Task.Run(() => renderer.Render(snapshot, previewProfile, scene));
                if (closing) { return; }
                using var stream = new InMemoryRandomAccessStream();
                using (var writer = new DataWriter(stream.GetOutputStreamAt(0))) { writer.WriteBytes(bytes); await writer.StoreAsync(); }
                stream.Seek(0);
                var image = new BitmapImage();
                await image.SetSourceAsync(stream);
                PreviousPreview.Source = PreviewImage.Source;
                PreviewImage.Source = image;
                if (previewFingerprint is not null && visibleScene != scene)
                {
                    PreviewImage.Opacity = 0;
                    var animation = new DoubleAnimation { From = 0, To = 1, Duration = new Duration(TimeSpan.FromMilliseconds(650)) };
                    Storyboard.SetTarget(animation, PreviewImage);
                    Storyboard.SetTargetProperty(animation, "Opacity");
                    var storyboard = new Storyboard();
                    storyboard.Children.Add(animation);
                    storyboard.Begin();
                }
                else { PreviewImage.Opacity = 1; }
                previewFingerprint = fingerprint;
                visibleScene = scene;
            }
            if (snapshot.Notices.Count > 0 && playback is null) { StatusText.Text = string.Join(" · ", snapshot.Notices); }
        }
        catch (Exception error) { if (!closing) { StatusText.Text = $"Preview unavailable: {error.Message}"; } }
        finally { refreshing = false; if (closing) { dashboard.Dispose(); } }
    }

    private void UpdateMixer(IReadOnlyList<AppVolume> volumes)
    {
        if (!meters.Keys.SequenceEqual(volumes.Select(item => item.Key)))
        {
            meters.Clear();
            MixerRows.Children.Clear();
            foreach (var volume in volumes)
            {
                var row = new StackPanel { Spacing = 5 };
                var labels = new Grid();
                var label = new TextBlock { FontSize = 12, MaxWidth = 300, TextTrimming = TextTrimming.CharacterEllipsis, HorizontalAlignment = HorizontalAlignment.Left };
                var level = new TextBlock { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Right, FontFamily = new FontFamily("Cascadia Mono") };
                labels.Children.Add(label);
                labels.Children.Add(level);
                var track = new Grid { Height = 16, Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 28, 53, 41)), CornerRadius = new CornerRadius(4) };
                var gradient = new LinearGradientBrush { StartPoint = new Windows.Foundation.Point(0, 0.5), EndPoint = new Windows.Foundation.Point(1, 0.5) };
                gradient.GradientStops.Add(new GradientStop { Offset = 0, Color = Windows.UI.Color.FromArgb(255, 98, 199, 119) });
                gradient.GradientStops.Add(new GradientStop { Offset = 0.55, Color = Windows.UI.Color.FromArgb(255, 181, 220, 106) });
                gradient.GradientStops.Add(new GradientStop { Offset = 0.8, Color = Windows.UI.Color.FromArgb(255, 246, 197, 105) });
                gradient.GradientStops.Add(new GradientStop { Offset = 1, Color = Windows.UI.Color.FromArgb(255, 224, 131, 177) });
                var fill = new Microsoft.UI.Xaml.Shapes.Rectangle { Fill = gradient, RadiusX = 4, RadiusY = 4 };
                track.Children.Add(fill);
                meters[volume.Key] = new(track, fill, label, level);
                row.Children.Add(labels);
                row.Children.Add(track);
                MixerRows.Children.Add(row);
            }
            if (volumes.Count == 0) { MixerRows.Children.Add(new TextBlock { Text = "No audio sessions yet. Start a player.", FontSize = 12 }); }
        }
        foreach (var volume in volumes)
        { meters[volume.Key].Label.Text = $"{volume.Name} · {volume.Percent:F0}%{(volume.Muted ? " · muted" : "")}"; }
        UpdateLevels(volumes.ToDictionary(item => item.Key, item => item.Peak));
    }

    private void UpdateLevels(IReadOnlyDictionary<string, double?> levels)
    {
        foreach (var (key, peak) in levels)
        {
            if (!meters.TryGetValue(key, out var meter)) { continue; }
            meter.Fill.Clip = new RectangleGeometry { Rect = new Windows.Foundation.Rect(0, 0, meter.Track.ActualWidth * AudioLevel.Fraction(peak), 16) };
            meter.Level.Text = AudioLevel.Label(peak);
        }
    }

    private async void DashboardRefresh_Click(object sender, RoutedEventArgs args) => await RefreshDashboardAsync();

    private async void Media_Click(object sender, RoutedEventArgs args)
    {
        try { await dashboard.Media.ControlAsync((string)((Button)sender).Tag); await RefreshDashboardAsync(); }
        catch (Exception error) { StatusText.Text = error.Message; }
    }

    private async void DashboardStart_Click(object sender, RoutedEventArgs args)
    {
        if (!TryGetSelectedDevice(out var device)) { return; }
        var profile = ((DisplayProfile)ProfilesBox.SelectedItem).Id;
        DashboardScene? scene = CycleSwitch.IsOn ? null : (DashboardScene)ScenesBox.SelectedIndex;
        await RunAsync(async () =>
        {
            using var lifetime = new CancellationTokenSource();
            playback = lifetime;
            StopButton.IsEnabled = true;
            try { await DashboardPlayback.RunAsync(device, profile, new Progress<string>(message => StatusText.Text = message), lifetime.Token, scene); }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { StatusText.Text = "Dashboard stopped."; }
            finally { playback = null; StopButton.IsEnabled = false; }
        });
    }

    private void Refresh_Click(object sender, RoutedEventArgs args)
    {
        try
        {
            var devices = application.Discover();
            DevicesList.ItemsSource = devices;
            DevicesList.SelectedIndex = devices.Count > 0 ? 0 : -1;
            if (devices.Count > 0)
            {
                StatusText.Text = $"I found {devices.Count} compatible panel(s).";
                return;
            }

            var attached = WinUsbDisplayDiscovery.FindAttachedPanels();
            StatusText.Text = attached.Count > 0
                ? "The panel is attached, but no supported HID or WinUSB path is available."
                : "I did not find a compatible 0416:5302 panel.";
        }
        catch (Exception error) { StatusText.Text = error.Message; }
    }

    private async void Probe_Click(object sender, RoutedEventArgs args)
    {
        if (!TryGetSelectedDevice(out var device))
        {
            return;
        }

        await RunAsync(async () =>
        {
            var probe = await application.ProbeAsync(device);
            StatusText.Text = $"{probe.Message} PM {probe.ProductMode:X2}, SUB {probe.SubMode:X2}. {probe.DetectedProfile?.Name ?? "Unknown profile"}";
        });
    }

    private async void Pattern_Click(object sender, RoutedEventArgs args)
    {
        if (!TryGetSelectedDevice(out var device) || ProfilesBox.SelectedItem is not DisplayProfile profile)
        {
            return;
        }

        await RunAsync(async () => StatusText.Text = (await application.SendAsync(device, new PanelPictureRequest(profile.Id))).Message);
    }

    private async void Image_Click(object sender, RoutedEventArgs args)
    {
        if (!TryGetSelectedDevice(out var device)) { return; }
        await RunAsync(async () =>
        {
            var picker = new FileOpenPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            foreach (var extension in new[] { ".png", ".jpg", ".jpeg", ".bmp" }) { picker.FileTypeFilter.Add(extension); }
            var file = await picker.PickSingleFileAsync();
            if (file is null) { return; }
            settings = settings with { ImagePath = file.Path };
            ImageLabel.Text = file.Name;
            var profileId = ((DisplayProfile)ProfilesBox.SelectedItem).Id;
            StatusText.Text = (await application.SendAsync(device, new PanelPictureRequest(profileId, file.Path, RotationBox.SelectedIndex * 90))).Message;
        });
    }

    private void Save_Click(object sender, RoutedEventArgs args)
    {
        try
        {
            settings = settings with { ProfileId = ((DisplayProfile)ProfilesBox.SelectedItem).Id, Rotation = RotationBox.SelectedIndex * 90 };
            application.SaveSettings(settings);
            StatusText.Text = "My profile, rotation, and image choice are saved on this PC.";
        }
        catch (Exception error) { StatusText.Text = error.Message; }
    }

    private async void Play_Click(object sender, RoutedEventArgs args)
    {
        if (!TryGetSelectedDevice(out var device)) { return; }
        await RunAsync(async () =>
        {
            using var lifetime = new CancellationTokenSource();
            playback = lifetime;
            StopButton.IsEnabled = true;
            try
            {
                var request = new PanelPictureRequest(((DisplayProfile)ProfilesBox.SelectedItem).Id, settings.ImagePath, RotationBox.SelectedIndex * 90);
                await application.PlayAsync(device, request, null, new Progress<string>(message => StatusText.Text = message), lifetime.Token);
                StatusText.Text = "Playback stopped.";
            }
            finally { playback = null; StopButton.IsEnabled = false; }
        });
    }

    private void Stop_Click(object sender, RoutedEventArgs args) => playback?.Cancel();

    private async Task RunAsync(Func<Task> operation)
    {
        SetControlsEnabled(PanelControls, false);
        SetControlsEnabled(FrameControls, false);
        ScenesBox.IsEnabled = CycleSwitch.IsEnabled = false;
        try { await operation(); }
        catch (Exception error) { StatusText.Text = $"Display operation failed: {error.Message}"; }
        finally { SetControlsEnabled(PanelControls, true); SetControlsEnabled(FrameControls, true); ScenesBox.IsEnabled = CycleSwitch.IsEnabled = true; }
    }

    private static void SetControlsEnabled(Panel panel, bool enabled)
    {
        foreach (var child in panel.Children)
        {
            if (child is Control control) { control.IsEnabled = enabled; }
            else if (child is Panel nested) { SetControlsEnabled(nested, enabled); }
        }
    }

    private bool TryGetSelectedDevice(out DisplayDevice device)
    {
        if (DevicesList.SelectedItem is DisplayDevice selected)
        {
            device = selected;
            return true;
        }

        StatusText.Text = "Choose a panel first.";
        device = null!;
        return false;
    }
}
