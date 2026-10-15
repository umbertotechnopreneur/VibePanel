// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using OpenSilver.Themes.Modern;
using VibePanel.Contracts;

namespace VibePanel.App;

/// <summary>A preview-only screen navigator. USB and media actions remain explicit CLI commands.</summary>
public sealed partial class MainPage : Page
{
    private readonly IPanelClient client;
    private readonly DispatcherTimer refresh = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly string[] rotation = ["Music", "Artwork", "Mixer", "System", "Network"];
    private bool refreshing, unloaded, ready, refreshAgain;
    private long lastChange = Environment.TickCount64;
    private string? lastScene;
    private byte[]? lastFrame;
    private Storyboard? fade;
    private bool? appliedLight;
    private readonly string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VibePanel", "presentation.json");

    public MainPage(IPanelClient client)
    {
        this.client = client;
        InitializeComponent();
        ScenesBox.ItemsSource = new[] { new PanelChoice("Music", "Now playing") };
        ScenesBox.SelectedIndex = 0;
        ThemeBox.SelectedIndex = 0;
        LoadChoices();
        ready = true;
        ApplyTheme();
        Loaded += async (_, _) => { unloaded = false; await RefreshAsync(); if (!unloaded) { refresh.Start(); } };
        Unloaded += (_, _) => { unloaded = true; refresh.Stop(); fade?.Stop(); };
        refresh.Tick += async (_, _) => await RefreshAsync();
        KeyDown += (_, args) =>
        {
            if (ScenesBox.IsDropDownOpen || ProfilesBox.IsDropDownOpen || ThemeBox.IsDropDownOpen) { return; }
            if (args.Key == Key.PageUp) { Navigate(-1); args.Handled = true; }
            if (args.Key == Key.PageDown) { Navigate(1); args.Handled = true; }
        };
    }

    private async Task RefreshAsync()
    {
        if (unloaded || !ready) { return; }
        if (refreshing) { refreshAgain = true; return; }
        refreshing = true;
        try
        {
            ApplyTheme();
            if (CycleBox.IsChecked == true && Environment.TickCount64 - lastChange >= 20000 && ScenesBox.ItemsSource is IReadOnlyList<PanelChoice> choices)
            {
                var current = (ScenesBox.SelectedItem as PanelChoice)?.Id ?? "Music";
                ScenesBox.SelectedItem = choices.FirstOrDefault(item => item.Id == rotation[(Array.IndexOf(rotation, current) + 1) % rotation.Length]);
                lastChange = Environment.TickCount64;
            }
            var scene = (ScenesBox.SelectedItem as PanelChoice)?.Id ?? "Music";
            var profile = (ProfilesBox.SelectedItem as PanelChoice)?.Id ?? "wide-1280x480";
            var state = await client.RequestAsync<PanelState>("state", new PreviewOptions(profile, scene, Light: IsLight, AmbientDrift: DriftBox.IsChecked == true));
            if (unloaded) { return; }
            UpdateChoices(ProfilesBox, state.Profiles, "wide-1280x480");
            UpdateChoices(ScenesBox, state.Scenes, "Music");
            StatusText.Text = $"{ScenesBox.SelectedIndex + 1:00} / {state.Scenes.Count:00} · preview only · no USB writes"
                + (state.Notices.Count > 0 ? " · " + string.Join(" · ", state.Notices) : "");
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
                    fade = new Storyboard(); fade.Children.Add(animation); fade.Begin();
                }
                lastFrame = frame; lastScene = scene;
            }
        }
        catch (Exception error) { StatusText.Text = error.Message; LoadingText.Text = "Local engine unavailable · see status"; }
        finally
        {
            refreshing = false;
            if (refreshAgain && !unloaded) { refreshAgain = false; Dispatcher.BeginInvoke(async () => await RefreshAsync()); }
        }
    }

    private bool IsLight => ThemeBox.SelectedIndex == 2 || (ThemeBox.SelectedIndex == 0 && DateTime.Now.Hour is >= 7 and < 19);

    private void ApplyTheme()
    {
        var light = IsLight;
        if (appliedLight == light) { return; }
        appliedLight = light;
        ((ModernTheme)Application.Current.Theme).CurrentPalette = light ? ModernTheme.Palettes.Light : ModernTheme.Palettes.Dark;
        Root.Background = Brush(light ? "#F4F3F8" : "#15191F");
        PreviewFrame.Background = Brush(light ? "#E7E5EF" : "#202631");
        PreviewFrame.BorderBrush = Brush(light ? "#CFC8DC" : "#393346");
        Heading.Foreground = LoadingText.Foreground = Brush(light ? "#252335" : "#EFF0F6");
        Subtitle.Foreground = Author.Foreground = StatusText.Foreground = Brush(light ? "#625B72" : "#AEB5C2");
        Brand.Foreground = Manifesto.Foreground = Brush(light ? "#704484" : "#DDB0EB");
    }

    private static void UpdateChoices(ComboBox box, IReadOnlyList<PanelChoice> choices, string fallback)
    {
        if (box.ItemsSource is IEnumerable<PanelChoice> current && current.SequenceEqual(choices)) { return; }
        var id = (box.SelectedItem as PanelChoice)?.Id ?? fallback;
        box.ItemsSource = choices;
        box.SelectedItem = choices.FirstOrDefault(item => item.Id == id) ?? choices.FirstOrDefault();
    }
    private void Navigate(int direction)
    {
        if (ScenesBox.Items.Count > 0) { ScenesBox.SelectedIndex = (ScenesBox.SelectedIndex + direction + ScenesBox.Items.Count) % ScenesBox.Items.Count; }
    }
    private void Previous_Click(object sender, RoutedEventArgs args) => Navigate(-1);
    private void Next_Click(object sender, RoutedEventArgs args) => Navigate(1);
    private void Scene_Changed(object sender, SelectionChangedEventArgs args) { lastChange = Environment.TickCount64; if (ready) { _ = RefreshAsync(); } }
    private void Profile_Changed(object sender, SelectionChangedEventArgs args) { if (ready) { _ = RefreshAsync(); } }
    private void Theme_Changed(object sender, SelectionChangedEventArgs args) { if (ready) { ApplyTheme(); SaveChoices(); _ = RefreshAsync(); } }
    private static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));

    private void LoadChoices()
    {
        try
        {
            if (File.Exists(settingsPath))
            { using var json = JsonDocument.Parse(File.ReadAllText(settingsPath)); var mode = json.RootElement.GetProperty("theme").GetInt32(); ThemeBox.SelectedIndex = mode is >= 0 and <= 2 ? mode : 0; }
        }
        catch (Exception error) when (error is IOException or JsonException or KeyNotFoundException or InvalidOperationException or UnauthorizedAccessException) { }
    }
    private void SaveChoices()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(settingsPath, JsonSerializer.Serialize(new { theme = ThemeBox.SelectedIndex }));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { StatusText.Text = "Theme changed; could not save the preference."; }
    }
}
