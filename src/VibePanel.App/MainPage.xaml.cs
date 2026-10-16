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
    private readonly DispatcherTimer footerDelay = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VibePanel", "presentation.json");
    private readonly List<CatalogRow> categoryRows = [];
    private readonly List<CatalogRow> themeRows = [];
    private static readonly int[] RefreshIntervals = [1000, 500, 250, 100, 50];
    private ThemeCatalogDto? catalog;
    private bool refreshing, unloaded, ready, updatingChoices, refreshAgain, catalogLoading;
    private bool displayActive, displayBusy, devicesLoading;
    private bool codexLoading, codexPreparing, diagnosticsChanging, diagnosticsEnabled;
    private long codexStatusAt;
    private int refreshMilliseconds = 1000;
    private string jpegQuality = "high";
    private string profileSelection = "auto";
    private long lastChange = Environment.TickCount64;
    private string selectedCategory = "multimedia", selectedTheme = "", selectedProfile = "wide-1280x480", fontFamily = "Cascadia Mono";
    private int appearance;
    private double fontScale = 1;
    private string? lastRenderedTheme;
    private byte[]? lastFrame;
    private Storyboard? fade;
    private Storyboard? footerFade;
    private bool footerHovered;
    private bool? appliedLight;

    public MainPage(IPanelClient client)
    {
        this.client = client;
        InitializeComponent();
        LoadChoices();
        RefreshBox.ItemsSource = RefreshIntervals.Select(value => new PanelChoice(value.ToString(), value == 1000 ? "1 s" : $"{value} ms")).ToArray();
        RefreshBox.SelectedItem = ((PanelChoice[])RefreshBox.ItemsSource).First(item => item.Id == refreshMilliseconds.ToString());
        QualityBox.ItemsSource = new[] { new PanelChoice("low", "Low"), new PanelChoice("medium", "Medium"), new PanelChoice("high", "High") };
        QualityBox.SelectedItem = ((PanelChoice[])QualityBox.ItemsSource).First(item => item.Id == jpegQuality);
        refresh.Interval = TimeSpan.FromMilliseconds(refreshMilliseconds);
        FontBox.Text = fontFamily;
        UpdateFontScale();
        ready = true;
        ApplyAppearance();
        AboutVersion.Text = $"Codename · {typeof(App).Assembly.GetName().Version?.ToString(3)} · preview";
        Loaded += async (_, _) =>
        {
            unloaded = false;
            await LoadCatalogAsync();
            await RefreshDevicesAsync();
            await LoadDiagnosticsAsync();
            await RefreshAsync();
            if (!unloaded) { refresh.Start(); footerDelay.Start(); }
        };
        Unloaded += (_, _) => { unloaded = true; refresh.Stop(); footerDelay.Stop(); fade?.Stop(); footerFade?.Stop(); };
        refresh.Tick += async (_, _) => await RefreshAsync();
        footerDelay.Tick += (_, _) => FadeFooter();
        FooterLinks.MouseEnter += (_, _) => { footerHovered = true; RevealFooter(); };
        FooterLinks.MouseLeave += (_, _) => { footerHovered = false; footerDelay.Start(); };
        FooterLinks.GotFocus += (_, _) => RevealFooter();
        FooterLinks.LostFocus += (_, _) => footerDelay.Start();
        KeyDown += (_, args) =>
        {
            if (AboutOverlay.Visibility == Visibility.Visible)
            { if (args.Key == Key.Escape) { CloseAbout(); args.Handled = true; } return; }
            if (ProfilesBox.IsDropDownOpen || DevicesBox.IsDropDownOpen || RefreshBox.IsDropDownOpen || QualityBox.IsDropDownOpen || args.OriginalSource is TextBox) { return; }
            if (args.Key == Key.PageUp) { Navigate(-1); args.Handled = true; }
            if (args.Key == Key.PageDown) { Navigate(1); args.Handled = true; }
        };
        SizeChanged += (_, args) => LibraryColumn.Width = new GridLength(args.NewSize.Width < 1150 ? 232 : 264);
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
        if (unloaded || !ready || displayBusy || string.IsNullOrEmpty(selectedTheme)) { return; }
        if (refreshing) { refreshAgain = true; return; }
        refreshing = true;
        var requestedDisplay = displayActive;
        try
        {
            ApplyAppearance();
            if (selectedCategory == "codex") { await RefreshCodexStatusAsync(); }
            if (CycleBox.IsChecked == true && Environment.TickCount64 - lastChange >= 20000) { Navigate(1); }
            var requestedTheme = selectedTheme;
            var requestedProfile = selectedProfile;
            var requestedLight = IsLight;
            var requestedFont = fontFamily;
            var requestedScale = fontScale;
            var requestedInterval = refreshMilliseconds;
            var requestedQuality = jpegQuality;
            var state = await client.RequestAsync<PanelState>(requestedDisplay ? "present" : "state", new PreviewOptions(
                ProfileId: requestedProfile, Light: requestedLight, ThemeId: requestedTheme,
                FontFamily: requestedFont, FontScale: requestedScale, RefreshIntervalMilliseconds: requestedInterval,
                JpegQuality: requestedQuality));
            if (unloaded || displayBusy) { return; }
            // A slow frame may complete after the user has selected another theme or font.
            if (requestedTheme != selectedTheme || requestedProfile != selectedProfile || requestedLight != IsLight || requestedFont != fontFamily || requestedScale != fontScale
                || requestedInterval != refreshMilliseconds || requestedQuality != jpegQuality || requestedDisplay != displayActive)
            { refreshAgain = true; return; }
            displayActive = state.DisplayRunning;
            UpdateDisplayControls();
            UpdateProfiles(state.Profiles);
            SourceText.Text = selectedCategory == "codex" ? "OpenAI Codex · local activity"
                : string.IsNullOrWhiteSpace(state.Source) ? "System media session" : state.Source;
            StatusText.Text = state.Notices.Count > 0 ? string.Join(" · ", state.Notices)
                : displayActive ? state.Status : $"Preview · {refreshMilliseconds} ms · {jpegQuality} JPEG";
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
            if (requestedDisplay) { displayActive = false; UpdateDisplayControls(); }
            StatusText.Text = error.Message;
            ClearPreview();
            LoadingTitle.Text = requestedDisplay ? "Display stopped." : "Waiting for the local engine.";
            LoadingText.Text = error.Message;
        }
        finally
        {
            refreshing = false;
            if (refreshAgain && !unloaded && !displayBusy) { refreshAgain = false; Dispatcher.BeginInvoke(async () => await RefreshAsync()); }
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
        var choices = new[] { new PanelChoice("auto", "Auto size") }.Concat(profiles.Select(profile => new PanelChoice(profile.Id, ShortProfileName(profile.Id, profile.Name)))).ToArray();
        if (ProfilesBox.ItemsSource is IEnumerable<PanelChoice> existing && existing.SequenceEqual(choices)) { return; }
        updatingChoices = true;
        ProfilesBox.ItemsSource = choices;
        ProfilesBox.SelectedItem = choices.FirstOrDefault(profile => profile.Id == profileSelection) ?? choices.First();
        profileSelection = ((PanelChoice)ProfilesBox.SelectedItem).Id;
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
        Root.Background = Brush(light ? "#FAF9FC" : "#101218");
        var transparent = new SolidColorBrush(Colors.Transparent);
        var surface = Brush(light ? "#FFFFFF" : "#191C25");
        var line = Brush(light ? "#DDD9E7" : "#303441");
        var ink = Brush(light ? "#242231" : "#F1F1FA");
        var muted = Brush(light ? "#716B80" : "#9C9CAF");
        var accent = Brush(light ? "#7854B5" : "#C9AEF3");
        // Sections share the page, without cards. Only the modal needs its own surface.
        foreach (var section in new[] { LibraryFrame, AppearanceFrame, OptionsFrame, DisplayFrame, CodexSetupFrame, PreviewBadge, PreviewFrame }) { section.Background = transparent; }
        AboutFrame.Background = surface;
        AboutFrame.BorderBrush = line;
        DisplayDivider.BorderBrush = line;
        foreach (var text in new[] { Heading, SelectedThemeText, LoadingTitle, PositionText, FontScaleText, AboutHeading, AboutAuthor, AboutDisclosureHeading, CodexStatusText }) { text.Foreground = ink; }
        foreach (var text in new[] { Subtitle, Author, CatalogText, SourceText, StatusText, LoadingText, FontLabel, SizeLabel, GeometryLabel, DeviceLabel, RefreshLabel, QualityLabel, CodexEventText, CodexHelpText, FooterDot1, FooterDot2, AboutVersion, AboutDisclosure, AboutInitiative }) { text.Foreground = muted; }
        foreach (var text in new[] { Eyebrow, LibraryHeading, CategoryText, PreviewBadgeText, CodexSetupHeading }) { text.Foreground = accent; }
        foreach (var link in new[] { Manifesto, AboutLink, TermsLink, PrivacyLink, AboutManifesto, ProjectLink, InitiativeLink, WebsiteLink, AboutTerms, AboutPrivacy }) { link.Foreground = accent; }
        CategoriesBox.Background = ThemesBox.Background = transparent;
        FontBox.Background = ProfilesBox.Background = transparent;
        FontBox.Foreground = ProfilesBox.Foreground = ink;
        FontBox.BorderBrush = ProfilesBox.BorderBrush = line;
        FontBox.BorderThickness = ProfilesBox.BorderThickness = new Thickness(0, 0, 0, 1);
        foreach (var combo in new[] { DevicesBox, RefreshBox, QualityBox })
        { combo.Background = transparent; combo.Foreground = ink; combo.BorderBrush = line; combo.BorderThickness = new Thickness(0, 0, 0, 1); }
        CycleBox.Foreground = muted;
        LogErrorsBox.Foreground = muted;
        foreach (var button in new[] { ReloadButton, PreviousButton, NextButton, ApplyFontButton, SmallerButton, LargerButton, CloseAboutButton, RefreshDevicesButton, StartDisplayButton, StopDisplayButton, CodexRefreshButton, CodexPrepareButton, OpenLogButton })
        { button.Background = transparent; button.Foreground = ink; button.BorderThickness = new Thickness(0); }
        StartDisplayButton.Background = Brush(light ? "#68468F" : "#C5AAE8");
        StartDisplayButton.Foreground = Brush(light ? "#FFFFFF" : "#1C1428");
        UpdateRows();
        UpdateAppearanceButtons();
        UpdateDisplayControls();
    }

    private void UpdateAppearanceButtons()
    {
        var buttons = new[] { AutoButton, DarkButton, LightButton };
        for (var i = 0; i < buttons.Length; i++)
        {
            buttons[i].Background = new SolidColorBrush(Colors.Transparent);
            buttons[i].Foreground = Brush(i == appearance ? IsLight ? "#623A8D" : "#EBD6FF" : IsLight ? "#716B80" : "#9C9CAF");
            buttons[i].BorderBrush = i == appearance ? Brush(IsLight ? "#7854B5" : "#C9AEF3") : new SolidColorBrush(Colors.Transparent);
            buttons[i].BorderThickness = new Thickness(0, 0, 0, 2);
            buttons[i].FontWeight = i == appearance ? FontWeights.SemiBold : FontWeights.Normal;
        }
    }

    private void UpdateRows()
    {
        foreach (var row in categoryRows) { row.Update(IsLight, row.Id == selectedCategory); }
        foreach (var row in themeRows) { row.Update(IsLight, row.Id == selectedTheme); }
    }

    private void UpdateSelection()
    {
        CodexSetupFrame.Visibility = selectedCategory == "codex" ? Visibility.Visible : Visibility.Collapsed;
        CategoryText.Text = (categoryRows.FirstOrDefault(row => row.Id == selectedCategory)?.Name ?? selectedCategory).ToUpperInvariant();
        SelectedThemeText.Text = themeRows.FirstOrDefault(row => row.Id == selectedTheme)?.Name ?? "No themes installed";
        PositionText.Text = $"{ThemesBox.SelectedIndex + 1:00} / {themeRows.Count:00}";
        PreviousButton.IsEnabled = NextButton.IsEnabled = themeRows.Count > 1;
        UpdateDisplayControls();
        UpdateRows();
        if (themeRows.Count == 0) { ClearPreview(); LoadingTitle.Text = "This category is empty."; LoadingText.Text = "Add a theme pack and reload the library."; }
    }

    private void Navigate(int direction)
    {
        if (ThemesBox.Items.Count > 0) { ThemesBox.SelectedIndex = (ThemesBox.SelectedIndex + direction + ThemesBox.Items.Count) % ThemesBox.Items.Count; ThemesBox.ScrollIntoView(ThemesBox.SelectedItem); }
    }

    private void UpdateDisplayControls()
    {
        StartDisplayButton.IsEnabled = !displayBusy && !displayActive && !devicesLoading && DevicesBox.SelectedItem is PanelChoice && !string.IsNullOrEmpty(selectedTheme);
        StopDisplayButton.IsEnabled = !displayBusy && displayActive;
        DevicesBox.IsEnabled = !displayBusy && !displayActive;
        RefreshDevicesButton.IsEnabled = !displayBusy && !displayActive && !devicesLoading;
        ProfilesBox.IsEnabled = !displayBusy && !displayActive;
        PreviewBadgeText.Text = displayActive ? "LIVE DISPLAY" : "LOCAL PREVIEW";
    }

    private async Task RefreshDevicesAsync()
    {
        if (devicesLoading || displayActive || displayBusy) { return; }
        devicesLoading = true;
        UpdateDisplayControls();
        try
        {
            var previous = (DevicesBox.SelectedItem as PanelChoice)?.Id;
            var choices = await client.RequestAsync<PanelChoice[]>("devices");
            if (unloaded) { return; }
            DevicesBox.ItemsSource = choices;
            DevicesBox.SelectedItem = choices.FirstOrDefault(item => item.Id == previous) ?? choices.FirstOrDefault();
            if (choices.Length == 0) { StatusText.Text = "Connect a USB display, then press ↻."; }
        }
        catch (Exception error) { StatusText.Text = error.Message; }
        finally { devicesLoading = false; UpdateDisplayControls(); }
    }

    private async void StartDisplay_Click(object sender, RoutedEventArgs args)
    {
        if (displayBusy || displayActive || DevicesBox.SelectedItem is not PanelChoice device || string.IsNullOrEmpty(selectedTheme)) { return; }
        displayBusy = true;
        UpdateDisplayControls();
        StatusText.Text = "Connecting to the selected display…";
        try
        {
            var result = await client.RequestAsync<DisplayStartResult>("start", new StartOptions(device.Id, profileSelection, ThemeId: selectedTheme));
            displayActive = result.Accepted;
            if (result.ProfileId is { } detected)
            {
                updatingChoices = true;
                selectedProfile = detected;
                ProfilesBox.SelectedItem = (ProfilesBox.ItemsSource as IEnumerable<PanelChoice>)?.FirstOrDefault(item => item.Id == profileSelection);
                updatingChoices = false;
            }
            SaveChoices();
        }
        catch (Exception error) { StatusText.Text = error.Message; }
        finally { updatingChoices = false; displayBusy = false; UpdateDisplayControls(); }
        if (displayActive) { await RefreshAsync(); }
    }

    private async void StopDisplay_Click(object sender, RoutedEventArgs args)
    {
        if (displayBusy || !displayActive) { return; }
        displayBusy = true;
        UpdateDisplayControls();
        try
        {
            await client.RequestAsync<JsonElement>("stop");
            displayActive = false;
            StatusText.Text = "Display stopped.";
        }
        catch (Exception error) { StatusText.Text = error.Message; }
        finally { displayBusy = false; UpdateDisplayControls(); }
    }

    private async void RefreshDevices_Click(object sender, RoutedEventArgs args) => await RefreshDevicesAsync();

    private async Task RefreshCodexStatusAsync(bool force = false)
    {
        if (codexLoading || codexPreparing || !force && Environment.TickCount64 - codexStatusAt < 5000) { return; }
        codexLoading = true;
        CodexRefreshButton.IsEnabled = false;
        try
        {
            var status = await client.RequestAsync<CodexSetupStatus>("codex.status");
            if (unloaded) { return; }
            ShowCodexStatus(status);
            codexStatusAt = Environment.TickCount64;
        }
        catch (Exception error) { CodexStatusText.Text = error.Message; }
        finally { codexLoading = false; CodexRefreshButton.IsEnabled = !codexPreparing; }
    }

    private void ShowCodexStatus(CodexSetupStatus status)
    {
        CodexStatusText.Text = status.Status;
        CodexEventText.Text = status.LastEvent is { } last ? $"Last event: {last}\n{status.LastEventUtc}" : "No events received yet.";
        CodexHelpText.Text = status.Instructions;
        CodexPrepareButton.Content = status.Prepared && status.Listed ? "Plugin prepared" : "Prepare Codex plugin";
        CodexPrepareButton.IsEnabled = !codexPreparing && !(status.Prepared && status.Listed);
    }

    private async void CodexPrepare_Click(object sender, RoutedEventArgs args)
    {
        if (codexPreparing) { return; }
        string? preparationError = null;
        codexPreparing = true;
        CodexPrepareButton.IsEnabled = CodexRefreshButton.IsEnabled = false;
        CodexStatusText.Text = "Preparing the local plugin…";
        try
        {
            var status = await client.RequestAsync<CodexSetupStatus>("codex.prepare");
            if (!unloaded) { ShowCodexStatus(status); codexStatusAt = Environment.TickCount64; }
        }
        catch (Exception error) { preparationError = error.Message; }
        finally
        {
            codexPreparing = false;
            CodexRefreshButton.IsEnabled = true;
            await RefreshCodexStatusAsync(force: true);
            if (preparationError is not null) { CodexStatusText.Text = preparationError; }
        }
    }

    private async void CodexRefresh_Click(object sender, RoutedEventArgs args) => await RefreshCodexStatusAsync(force: true);

    private async Task LoadDiagnosticsAsync()
    {
        diagnosticsChanging = true;
        try
        {
            var status = await client.RequestAsync<DiagnosticsStatus>("diagnostics.status");
            diagnosticsEnabled = status.Enabled;
            LogErrorsBox.IsChecked = status.Enabled;
            OpenLogButton.IsEnabled = status.Enabled;
            ToolTipService.SetToolTip(OpenLogButton, status.LogPath);
        }
        catch (Exception error) { StatusText.Text = error.Message; }
        finally { diagnosticsChanging = false; }
    }

    private async void Diagnostics_Changed(object sender, RoutedEventArgs args)
    {
        if (!ready || diagnosticsChanging) { return; }
        diagnosticsChanging = true;
        LogErrorsBox.IsEnabled = false;
        try
        {
            var status = await client.RequestAsync<DiagnosticsStatus>("diagnostics.set", new SetDiagnosticsOptions(LogErrorsBox.IsChecked == true));
            diagnosticsEnabled = status.Enabled;
            LogErrorsBox.IsChecked = status.Enabled;
            OpenLogButton.IsEnabled = status.Enabled;
            ToolTipService.SetToolTip(OpenLogButton, status.LogPath);
        }
        catch (Exception error) { LogErrorsBox.IsChecked = diagnosticsEnabled; StatusText.Text = error.Message; }
        finally { diagnosticsChanging = false; LogErrorsBox.IsEnabled = true; }
    }

    private async void OpenLog_Click(object sender, RoutedEventArgs args)
    {
        try
        {
            var result = await client.RequestAsync<JsonElement>("diagnostics.open");
            if (!result.TryGetProperty("accepted", out var accepted) || accepted.ValueKind != JsonValueKind.True)
            { StatusText.Text = "No log file yet. Leave Log errors on, then try again."; }
        }
        catch (Exception error) { StatusText.Text = error.Message; }
    }

    private void Device_Changed(object sender, SelectionChangedEventArgs args) { if (ready) { UpdateDisplayControls(); } }
    private void Refresh_Changed(object sender, SelectionChangedEventArgs args)
    {
        if (!ready || RefreshBox.SelectedItem is not PanelChoice choice) { return; }
        refreshMilliseconds = int.Parse(choice.Id, System.Globalization.CultureInfo.InvariantCulture);
        refresh.Interval = TimeSpan.FromMilliseconds(refreshMilliseconds);
        SaveChoices();
        _ = RefreshAsync();
    }
    private void Quality_Changed(object sender, SelectionChangedEventArgs args)
    {
        if (!ready || QualityBox.SelectedItem is not PanelChoice choice) { return; }
        jpegQuality = choice.Id;
        SaveChoices();
        _ = RefreshAsync();
    }

    private void About_Click(object sender, RoutedEventArgs args)
    {
        AboutOverlay.Visibility = Visibility.Visible;
        CloseAboutButton.Focus();
        RevealFooter();
    }
    private void CloseAbout_Click(object sender, RoutedEventArgs args) => CloseAbout();
    private void CloseAbout() { AboutOverlay.Visibility = Visibility.Collapsed; AboutLink.Focus(); }
    private void RevealFooter() { footerDelay.Stop(); footerFade?.Stop(); FooterLinks.Opacity = 1; }
    private void FadeFooter()
    {
        footerDelay.Stop();
        if (footerHovered || AboutOverlay.Visibility == Visibility.Visible) { return; }
        var focus = FocusManager.GetFocusedElement() as DependencyObject;
        while (focus is not null)
        {
            if (focus == FooterLinks) { return; }
            focus = VisualTreeHelper.GetParent(focus);
        }
        var animation = new DoubleAnimation { From = 1, To = 0, Duration = new Duration(TimeSpan.FromSeconds(3)) };
        Storyboard.SetTarget(animation, FooterLinks);
        Storyboard.SetTargetProperty(animation, new PropertyPath("Opacity"));
        footerFade = new Storyboard(); footerFade.Children.Add(animation); footerFade.Begin();
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
        profileSelection = (ProfilesBox.SelectedItem as PanelChoice)?.Id ?? "auto";
        if (profileSelection != "auto") { selectedProfile = profileSelection; }
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
            if (json.TryGetProperty("profileSelection", out var profileChoice)) { profileSelection = profileChoice.GetString() ?? "auto"; }
            if (json.TryGetProperty("font", out var font)) { fontFamily = font.GetString() ?? fontFamily; }
            if (json.TryGetProperty("fontScale", out var scale) && scale.TryGetDouble(out var number) && double.IsFinite(number)) { fontScale = Math.Clamp(number, 0.7, 1.5); }
            if (json.TryGetProperty("refreshMilliseconds", out var interval) && interval.TryGetInt32(out var milliseconds) && RefreshIntervals.Contains(milliseconds)) { refreshMilliseconds = milliseconds; }
            if (json.TryGetProperty("jpegQuality", out var quality) && quality.GetString() is "low" or "medium" or "high") { jpegQuality = quality.GetString()!; }
        }
        catch (Exception error) when (error is IOException or JsonException or InvalidOperationException or UnauthorizedAccessException) { }
    }
    private void SaveChoices()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(settingsPath, JsonSerializer.Serialize(new { theme = appearance, category = selectedCategory, themeId = selectedTheme, profile = selectedProfile, profileSelection, font = fontFamily, fontScale, refreshMilliseconds, jpegQuality }));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { StatusText.Text = "The preference changed but could not be saved."; }
    }

    public sealed class CatalogRow(string id, string name, string description = "", string number = "") : INotifyPropertyChanged
    {
        public string Id { get; } = id;
        public string Name { get; } = name;
        public string Description { get; } = description;
        public string Number { get; } = number;
        public Brush Outline { get; private set; } = new SolidColorBrush(Colors.Transparent);
        public FontWeight Weight { get; private set; } = FontWeights.Normal;
        public Brush Ink { get; private set; } = MainPage.Brush("#F1F1FA");
        public Brush Muted { get; private set; } = MainPage.Brush("#9C9CAF");
        public Brush Accent { get; private set; } = MainPage.Brush("#C9AEF3");
        public event PropertyChangedEventHandler? PropertyChanged;
        public void Update(bool light, bool selected)
        {
            Outline = selected ? MainPage.Brush(light ? "#7854B5" : "#C9AEF3") : new SolidColorBrush(Colors.Transparent);
            Weight = selected ? FontWeights.SemiBold : FontWeights.Normal;
            Ink = MainPage.Brush(selected ? light ? "#68468F" : "#D8BAFA" : light ? "#242231" : "#F1F1FA");
            Muted = MainPage.Brush(light ? "#716B80" : "#9C9CAF");
            Accent = MainPage.Brush(selected ? light ? "#7854B5" : "#D8BAFA" : light ? "#A89BB7" : "#686B80");
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(""));
        }
    }
}
