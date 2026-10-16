// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using System.ComponentModel;
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

/// <summary>A manifest-driven theme library and preview. The engine owns data and rendering.</summary>
public sealed partial class MainPage : Page
{
    private readonly IPanelClient client;
    private readonly DispatcherTimer refresh = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VibePanel", "presentation.json");
    private readonly List<CatalogRow> categoryRows = [];
    private readonly List<CatalogRow> themeRows = [];
    private ThemeCatalogDto? catalog;
    private bool refreshing, unloaded, ready, updatingChoices, refreshAgain, catalogLoading;
    private long lastChange = Environment.TickCount64;
    private string selectedCategory = "multimedia", selectedTheme = "", selectedProfile = "wide-1280x480", fontFamily = "Cascadia Mono";
    private int appearance;
    private double fontScale = 1;
    private string? lastRenderedTheme;
    private byte[]? lastFrame;
    private Storyboard? fade;
    private bool? appliedLight;

    public MainPage(IPanelClient client)
    {
        this.client = client;
        InitializeComponent();
        LoadChoices();
        FontBox.Text = fontFamily;
        UpdateFontScale();
        ready = true;
        ApplyAppearance();
        Loaded += async (_, _) =>
        {
            unloaded = false;
            await LoadCatalogAsync();
            await RefreshAsync();
            if (!unloaded) { refresh.Start(); }
        };
        Unloaded += (_, _) => { unloaded = true; refresh.Stop(); fade?.Stop(); };
        refresh.Tick += async (_, _) => await RefreshAsync();
        KeyDown += (_, args) =>
        {
            if (ProfilesBox.IsDropDownOpen || args.OriginalSource is TextBox) { return; }
            if (args.Key == Key.PageUp) { Navigate(-1); args.Handled = true; }
            if (args.Key == Key.PageDown) { Navigate(1); args.Handled = true; }
        };
        SizeChanged += (_, args) => LibraryColumn.Width = new GridLength(args.NewSize.Width < 1150 ? 244 : 284);
    }

    private async Task LoadCatalogAsync()
    {
        if (catalogLoading) { return; }
        catalogLoading = true;
        ReloadButton.IsEnabled = false;
        try
        {
            catalog = await client.RequestAsync<ThemeCatalogDto>("themes");
            if (unloaded) { return; }
            updatingChoices = true;
            categoryRows.Clear();
            foreach (var category in catalog.Categories) { categoryRows.Add(new(category.Id, category.Name)); }
            CategoriesBox.ItemsSource = categoryRows.ToArray();
            CategoriesBox.SelectedItem = categoryRows.FirstOrDefault(row => row.Id == selectedCategory) ?? categoryRows.FirstOrDefault();
            selectedCategory = (CategoriesBox.SelectedItem as CatalogRow)?.Id ?? "multimedia";
            PopulateThemes();
            CatalogText.Text = $"{catalog.Themes.Count} themes · local folders";
            if (catalog.Notices.Count > 0) { StatusText.Text = string.Join(" · ", catalog.Notices); }
        }
        catch (Exception error)
        {
            StatusText.Text = error.Message;
            LoadingText.Text = "The theme library is unavailable. Use Reload to try again.";
        }
        finally { updatingChoices = false; catalogLoading = false; ReloadButton.IsEnabled = true; }
    }

    private void PopulateThemes()
    {
        var wasUpdating = updatingChoices;
        updatingChoices = true;
        themeRows.Clear();
        foreach (var theme in catalog?.Themes.Where(theme => theme.Category == selectedCategory) ?? [])
        { themeRows.Add(new(theme.Id, theme.Name, theme.Description, (themeRows.Count + 1).ToString("00"))); }
        ThemesBox.ItemsSource = themeRows.ToArray();
        ThemesBox.SelectedItem = themeRows.FirstOrDefault(row => row.Id == selectedTheme) ?? themeRows.FirstOrDefault();
        selectedTheme = (ThemesBox.SelectedItem as CatalogRow)?.Id ?? "";
        UpdateSelection();
        updatingChoices = wasUpdating;
    }

    private async Task RefreshAsync()
    {
        if (unloaded || !ready || string.IsNullOrEmpty(selectedTheme)) { return; }
        if (refreshing) { refreshAgain = true; return; }
        refreshing = true;
        try
        {
            ApplyAppearance();
            if (CycleBox.IsChecked == true && Environment.TickCount64 - lastChange >= 20000) { Navigate(1); }
            var requestedTheme = selectedTheme;
            var requestedProfile = selectedProfile;
            var requestedLight = IsLight;
            var requestedFont = fontFamily;
            var requestedScale = fontScale;
            var state = await client.RequestAsync<PanelState>("state", new PreviewOptions(
                ProfileId: requestedProfile, Light: requestedLight, ThemeId: requestedTheme,
                FontFamily: requestedFont, FontScale: requestedScale));
            if (unloaded) { return; }
            // A slow frame may complete after the user has selected another theme or font.
            if (requestedTheme != selectedTheme || requestedProfile != selectedProfile || requestedLight != IsLight || requestedFont != fontFamily || requestedScale != fontScale)
            { refreshAgain = true; return; }
            UpdateProfiles(state.Profiles);
            SourceText.Text = selectedCategory == "codex" ? "OpenAI Codex · local activity"
                : string.IsNullOrWhiteSpace(state.Source) ? "System media session" : state.Source;
            StatusText.Text = state.Notices.Count > 0 ? string.Join(" · ", state.Notices) : "Local preview · 1-second updates";
            if (state.FrameJpeg is { Length: > 0 } frame)
            {
                if (lastFrame is null || !lastFrame.AsSpan().SequenceEqual(frame))
                {
                    fade?.Stop();
                    PreviousPreview.Source = PreviewImage.Source;
                    using var stream = new MemoryStream(frame);
                    var image = new BitmapImage();
                    image.SetSource(stream);
                    PreviewImage.Source = image;
                    PreviewImage.Opacity = 1;
                    if (lastRenderedTheme is not null && lastRenderedTheme != requestedTheme)
                    {
                        var animation = new DoubleAnimation { From = 0, To = 1, Duration = new Duration(TimeSpan.FromMilliseconds(350)) };
                        Storyboard.SetTarget(animation, PreviewImage);
                        Storyboard.SetTargetProperty(animation, new PropertyPath("Opacity"));
                        fade = new Storyboard(); fade.Children.Add(animation); fade.Begin();
                    }
                    lastFrame = frame;
                    lastRenderedTheme = requestedTheme;
                }
                LoadingPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                // Never label a previous theme's frame as the newly selected theme.
                ClearPreview();
                LoadingTitle.Text = "The theme needs attention.";
                LoadingText.Text = state.Notices.Count > 0 ? string.Join(" · ", state.Notices) : "No frame is available yet.";
            }
        }
        catch (Exception error)
        {
            StatusText.Text = error.Message;
            ClearPreview();
            LoadingTitle.Text = "Waiting for the local engine.";
            LoadingText.Text = error.Message;
        }
        finally
        {
            refreshing = false;
            if (refreshAgain && !unloaded) { refreshAgain = false; Dispatcher.BeginInvoke(async () => await RefreshAsync()); }
        }
    }

    private void ClearPreview()
    {
        fade?.Stop();
        PreviewImage.Source = PreviousPreview.Source = null;
        lastFrame = null;
        lastRenderedTheme = null;
        LoadingPanel.Visibility = Visibility.Visible;
    }

    private void UpdateProfiles(IReadOnlyList<PanelChoice> profiles)
    {
        var choices = profiles.Select(profile => new PanelChoice(profile.Id, ShortProfileName(profile.Id, profile.Name))).ToArray();
        if (ProfilesBox.ItemsSource is IEnumerable<PanelChoice> existing && existing.SequenceEqual(choices)) { return; }
        updatingChoices = true;
        ProfilesBox.ItemsSource = choices;
        ProfilesBox.SelectedItem = choices.FirstOrDefault(profile => profile.Id == selectedProfile) ?? choices.FirstOrDefault();
        selectedProfile = (ProfilesBox.SelectedItem as PanelChoice)?.Id ?? "wide-1280x480";
        updatingChoices = false;
    }

    private static string ShortProfileName(string id, string name)
    {
        var dimensions = id.Replace("wide-", "").Replace("landscape-", "").Replace("portrait-", "");
        if (dimensions.StartsWith("square-", StringComparison.Ordinal)) { dimensions = dimensions[7..] + " × " + dimensions[7..]; }
        return dimensions.Any(char.IsDigit) ? dimensions.Replace("x", " × ") : name;
    }

    private bool IsLight => appearance == 2 || (appearance == 0 && DateTime.Now.Hour is >= 7 and < 19);

    private void ApplyAppearance()
    {
        var light = IsLight;
        if (appliedLight == light) { UpdateAppearanceButtons(); return; }
        appliedLight = light;
        ((ModernTheme)Application.Current.Theme).CurrentPalette = light ? ModernTheme.Palettes.Light : ModernTheme.Palettes.Dark;
        OpenSilver.Interop.ExecuteJavaScript("document.documentElement.style.colorScheme = $0", light ? "light" : "dark");
        Root.Background = Brush(light ? "#F3F2F7" : "#101218");
        var surface = Brush(light ? "#FFFFFF" : "#191C25");
        var line = Brush(light ? "#DDD9E7" : "#303441");
        var ink = Brush(light ? "#242231" : "#F1F1FA");
        var muted = Brush(light ? "#716B80" : "#9C9CAF");
        var accent = Brush(light ? "#7854B5" : "#C9AEF3");
        foreach (var border in new[] { LibraryFrame, AppearanceFrame, OptionsFrame, PreviewBadge }) { border.Background = surface; border.BorderBrush = line; }
        PreviewFrame.Background = Brush(light ? "#E6E3EE" : "#0A0D13");
        PreviewFrame.BorderBrush = line;
        foreach (var text in new[] { Heading, SelectedThemeText, LoadingTitle, PositionText, FontScaleText }) { text.Foreground = ink; }
        foreach (var text in new[] { Subtitle, Author, CatalogText, SourceText, StatusText, LoadingText, FontLabel, SizeLabel, GeometryLabel }) { text.Foreground = muted; }
        foreach (var text in new[] { Eyebrow, LibraryHeading, CategoryText, PreviewBadgeText }) { text.Foreground = accent; }
        Manifesto.Foreground = accent;
        CategoriesBox.Background = ThemesBox.Background = surface;
        FontBox.Background = ProfilesBox.Background = Brush(light ? "#F6F4FA" : "#11141D");
        FontBox.Foreground = ProfilesBox.Foreground = ink;
        FontBox.BorderBrush = ProfilesBox.BorderBrush = line;
        CycleBox.Foreground = muted;
        foreach (var button in new[] { ReloadButton, PreviousButton, NextButton, ApplyFontButton, SmallerButton, LargerButton })
        { button.Background = surface; button.Foreground = ink; button.BorderBrush = line; }
        UpdateRows();
        UpdateAppearanceButtons();
    }

    private void UpdateAppearanceButtons()
    {
        var buttons = new[] { AutoButton, DarkButton, LightButton };
        for (var i = 0; i < buttons.Length; i++)
        {
            buttons[i].Background = Brush(i == appearance ? IsLight ? "#E9DFF7" : "#40324E" : IsLight ? "#FFFFFF" : "#191C25");
            buttons[i].Foreground = Brush(i == appearance ? IsLight ? "#623A8D" : "#EBD6FF" : IsLight ? "#716B80" : "#9C9CAF");
            buttons[i].BorderThickness = new Thickness(0);
        }
    }

    private void UpdateRows()
    {
        foreach (var row in categoryRows) { row.Update(IsLight, row.Id == selectedCategory); }
        foreach (var row in themeRows) { row.Update(IsLight, row.Id == selectedTheme); }
    }

    private void UpdateSelection()
    {
        CategoryText.Text = (categoryRows.FirstOrDefault(row => row.Id == selectedCategory)?.Name ?? selectedCategory).ToUpperInvariant();
        SelectedThemeText.Text = themeRows.FirstOrDefault(row => row.Id == selectedTheme)?.Name ?? "No themes installed";
        PositionText.Text = $"{ThemesBox.SelectedIndex + 1:00} / {themeRows.Count:00}";
        PreviousButton.IsEnabled = NextButton.IsEnabled = themeRows.Count > 1;
        UpdateRows();
        if (themeRows.Count == 0) { ClearPreview(); LoadingTitle.Text = "This category is empty."; LoadingText.Text = "Add a theme pack and reload the library."; }
    }

    private void Navigate(int direction)
    {
        if (ThemesBox.Items.Count > 0) { ThemesBox.SelectedIndex = (ThemesBox.SelectedIndex + direction + ThemesBox.Items.Count) % ThemesBox.Items.Count; ThemesBox.ScrollIntoView(ThemesBox.SelectedItem); }
    }

    private void Category_Changed(object sender, SelectionChangedEventArgs args)
    {
        if (!ready || updatingChoices) { return; }
        selectedCategory = (CategoriesBox.SelectedItem as CatalogRow)?.Id ?? "multimedia";
        selectedTheme = "";
        PopulateThemes();
        lastChange = Environment.TickCount64;
        SaveChoices();
        _ = RefreshAsync();
    }
    private void Theme_Changed(object sender, SelectionChangedEventArgs args)
    {
        if (!ready || updatingChoices) { return; }
        selectedTheme = (ThemesBox.SelectedItem as CatalogRow)?.Id ?? "";
        lastChange = Environment.TickCount64;
        UpdateSelection();
        SaveChoices();
        _ = RefreshAsync();
    }
    private void Profile_Changed(object sender, SelectionChangedEventArgs args)
    {
        if (!ready || updatingChoices) { return; }
        selectedProfile = (ProfilesBox.SelectedItem as PanelChoice)?.Id ?? "wide-1280x480";
        SaveChoices();
        _ = RefreshAsync();
    }
    private async void Reload_Click(object sender, RoutedEventArgs args) { await LoadCatalogAsync(); await RefreshAsync(); }
    private void Previous_Click(object sender, RoutedEventArgs args) => Navigate(-1);
    private void Next_Click(object sender, RoutedEventArgs args) => Navigate(1);
    private void Auto_Click(object sender, RoutedEventArgs args) => SetAppearance(0);
    private void Dark_Click(object sender, RoutedEventArgs args) => SetAppearance(1);
    private void Light_Click(object sender, RoutedEventArgs args) => SetAppearance(2);
    private void SetAppearance(int value) { appearance = value; ApplyAppearance(); SaveChoices(); _ = RefreshAsync(); }
    private void Font_Click(object sender, RoutedEventArgs args) => ApplyFont();
    private void Font_KeyDown(object sender, KeyEventArgs args) { if (args.Key == Key.Enter) { ApplyFont(); args.Handled = true; } }
    private void ApplyFont()
    {
        var value = FontBox.Text.Trim();
        fontFamily = string.IsNullOrWhiteSpace(value) ? "Cascadia Mono" : value[..Math.Min(value.Length, 128)];
        FontBox.Text = fontFamily;
        SaveChoices();
        _ = RefreshAsync();
    }
    private void Smaller_Click(object sender, RoutedEventArgs args) => ChangeFontScale(-0.1);
    private void Larger_Click(object sender, RoutedEventArgs args) => ChangeFontScale(0.1);
    private void ChangeFontScale(double change) { fontScale = Math.Round(Math.Clamp(fontScale + change, 0.7, 1.5), 1); UpdateFontScale(); SaveChoices(); _ = RefreshAsync(); }
    private void UpdateFontScale() { FontScaleText.Text = $"{fontScale:P0}"; }
    private static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));

    private void LoadChoices()
    {
        try
        {
            if (!File.Exists(settingsPath)) { return; }
            using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
            var json = document.RootElement;
            if (json.TryGetProperty("theme", out var mode) && mode.TryGetInt32(out var value)) { appearance = value is >= 0 and <= 2 ? value : 0; }
            if (json.TryGetProperty("category", out var category)) { selectedCategory = category.GetString() ?? selectedCategory; }
            if (json.TryGetProperty("themeId", out var theme)) { selectedTheme = theme.GetString() ?? ""; }
            if (json.TryGetProperty("profile", out var profile)) { selectedProfile = profile.GetString() ?? selectedProfile; }
            if (json.TryGetProperty("font", out var font)) { fontFamily = font.GetString() ?? fontFamily; }
            if (json.TryGetProperty("fontScale", out var scale) && scale.TryGetDouble(out var number) && double.IsFinite(number)) { fontScale = Math.Clamp(number, 0.7, 1.5); }
        }
        catch (Exception error) when (error is IOException or JsonException or InvalidOperationException or UnauthorizedAccessException) { }
    }
    private void SaveChoices()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(settingsPath, JsonSerializer.Serialize(new { theme = appearance, category = selectedCategory, themeId = selectedTheme, profile = selectedProfile, font = fontFamily, fontScale }));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { StatusText.Text = "The preference changed but could not be saved."; }
    }

    public sealed class CatalogRow(string id, string name, string description = "", string number = "") : INotifyPropertyChanged
    {
        public string Id { get; } = id;
        public string Name { get; } = name;
        public string Description { get; } = description;
        public string Number { get; } = number;
        public Brush Background { get; private set; } = MainPage.Brush("#191C25");
        public Brush Outline { get; private set; } = MainPage.Brush("#303441");
        public Brush Ink { get; private set; } = MainPage.Brush("#F1F1FA");
        public Brush Muted { get; private set; } = MainPage.Brush("#9C9CAF");
        public Brush Accent { get; private set; } = MainPage.Brush("#C9AEF3");
        public event PropertyChangedEventHandler? PropertyChanged;
        public void Update(bool light, bool selected)
        {
            Background = MainPage.Brush(selected ? light ? "#F0E8FC" : "#30263F" : light ? "#FFFFFF" : "#191C25");
            Outline = MainPage.Brush(selected ? light ? "#C4A4E4" : "#795C9D" : light ? "#ECE8F1" : "#242833");
            Ink = MainPage.Brush(light ? "#242231" : "#F1F1FA");
            Muted = MainPage.Brush(light ? "#716B80" : "#9C9CAF");
            Accent = MainPage.Brush(selected ? light ? "#7854B5" : "#D8BAFA" : light ? "#A89BB7" : "#686B80");
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(""));
        }
    }
}
