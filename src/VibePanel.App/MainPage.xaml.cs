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
using System.Windows.Threading;
using OpenSilver.Themes.Modern;
using VibePanel.Contracts;

namespace VibePanel.App;

/// <summary>A manifest-driven theme library and preview. The engine owns data and rendering.</summary>
public sealed partial class MainPage : Page
{
    private readonly IPanelClient client;
    private readonly IStudioWindowActions? windowActions;
    private readonly IStudioPreview? preview;
    private readonly DispatcherTimer refresh = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer colorChanges = new() { Interval = TimeSpan.FromMilliseconds(150) };
    private readonly DispatcherTimer deviceRefresh = new() { Interval = TimeSpan.FromSeconds(10) };
    private readonly string settingsPath;
    private readonly string? startupDeviceId, startupThemeId;
    private bool startupRestorePending, startupReady;
    private readonly List<CatalogRow> categoryRows = [];
    private readonly List<CatalogRow> themeRows = [];
    private readonly Dictionary<string, string> screenThemes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> screenEnabled = new(StringComparer.Ordinal)
    {
        ["multimedia"] = true,
        ["codex"] = false,
        ["system"] = true
    };
    private static readonly int[] RefreshIntervals = [1000, 500, 250, 100, 50, 25];
    private ThemeCatalogDto? catalog;
    private bool refreshing, unloaded, ready, updatingChoices, refreshAgain, catalogLoading;
    private bool displayActive, displayBusy, devicesLoading;
    private bool codexLoading, codexPreparing, diagnosticsChanging, diagnosticsEnabled, restartingAsAdministrator;
    private long codexStatusAt;
    private int refreshMilliseconds = 1000;
    private string jpegQuality = "high";
    private string profileSelection = "auto";
    private long lastChange = Environment.TickCount64;
    private string selectedCategory = "multimedia", selectedTheme = "", selectedProfile = "wide-1280x480", fontFamily = "Cascadia Mono";
    private int appearance;
    private double fontScale = 1;
    private double saturationPercent = 100, hueDegrees;
    private double brightnessPercent, contrastPercent = 100;
    private bool colorFiltersEnabled, cycleThemes;
    private bool codexProcessRunning, frameSuppressed;
    private int screenRevision;
    private string? selectedDeviceId;
    private bool showSpectrum = true, updatingColorControls, pendingColorSettings, updatingFonts;
    private PanelChoice[] fontChoices = [];
    private string? lastRenderedTheme;
    private byte[]? lastFrame;
    private bool? appliedLight;

    public MainPage(IPanelClient client, IStudioWindowActions? windowActions = null, string? presentationSettingsPath = null, IStudioPreview? preview = null, bool restoreLastSession = false)
    {
        this.client = client;
        this.windowActions = windowActions;
        this.preview = preview;
        settingsPath = presentationSettingsPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VibePanel", "presentation.json");
        InitializeComponent();
        InitializePreviewAppearance();
        LoadChoices();
        if (restoreLastSession && !string.IsNullOrEmpty(selectedDeviceId) && !string.IsNullOrEmpty(selectedTheme))
        {
            startupDeviceId = selectedDeviceId;
            startupThemeId = selectedTheme;
            startupRestorePending = true;
        }
        RefreshBox.ItemsSource = RefreshIntervals.Select(value => new PanelChoice(value.ToString(), value == 1000 ? "1 s" : $"{value} ms")).ToArray();
        RefreshBox.SelectedItem = ((PanelChoice[])RefreshBox.ItemsSource).First(item => item.Id == refreshMilliseconds.ToString());
        QualityBox.ItemsSource = new[] { new PanelChoice("poor", "Poor"), new PanelChoice("low", "Low"), new PanelChoice("medium", "Medium"), new PanelChoice("high", "High") };
        QualityBox.SelectedItem = ((PanelChoice[])QualityBox.ItemsSource).First(item => item.Id == jpegQuality);
        refresh.Interval = TimeSpan.FromMilliseconds(refreshMilliseconds);
        FontBox.Text = fontFamily;
        UpdateFontScale();
        UpdateColorControls();
        FiltersBox.IsChecked = colorFiltersEnabled;
        UpdateFilterVisibility();
        SpectrumBox.IsChecked = showSpectrum;
        CycleBox.IsChecked = cycleThemes;
        MusicScreenBox.IsChecked = screenEnabled["multimedia"];
        CodexScreenBox.IsChecked = screenEnabled["codex"];
        SystemScreenBox.IsChecked = screenEnabled["system"];
        ready = true;
        ApplyAppearance();
        AboutVersion.Text = $"Codename · {typeof(App).Assembly.GetName().Version?.ToString(3)} · preview";
        Loaded += async (_, _) =>
        {
            unloaded = false;
            startupReady = false;
            await LoadCatalogAsync();
            await RefreshDevicesAsync();
            await LoadDiagnosticsAsync();
            await RefreshAsync();
            await LoadFontsAsync();
            startupReady = true;
            await RestoreStartupDisplayAsync();
            if (!unloaded)
            {
                refresh.Start();
                deviceRefresh.Start();
            }
        };
        Unloaded += (_, _) =>
        {
            unloaded = true; refresh.Stop(); deviceRefresh.Stop(); colorChanges.Stop();
            ClearPreview();
            if (pendingColorSettings) { CommitColorControls(); }
            SaveChoices();
        };
        refresh.Tick += async (_, _) => await RefreshAsync(queueIfBusy: false);
        deviceRefresh.Tick += async (_, _) => await RefreshDevicesAsync();
        colorChanges.Tick += async (_, _) =>
        {
            colorChanges.Stop(); CommitColorControls();
            SaveChoices(); await RefreshAsync();
        };
        KeyDown += (_, args) =>
        {
            if (CloseOverlay.Visibility == Visibility.Visible)
            { if (args.Key == Key.Escape) { CloseOverlay.Visibility = Visibility.Collapsed; args.Handled = true; } return; }
            if (AboutOverlay.Visibility == Visibility.Visible)
            { if (args.Key == Key.Escape) { CloseAbout(); args.Handled = true; } return; }
            if (ProfilesBox.IsDropDownOpen || DevicesBox.IsDropDownOpen || RefreshBox.IsDropDownOpen || QualityBox.IsDropDownOpen || FontsBox.IsDropDownOpen || args.OriginalSource is TextBox) { return; }
            if (args.Key == Key.PageUp) { Navigate(-1); args.Handled = true; }
            if (args.Key == Key.PageDown) { Navigate(1); args.Handled = true; }
        };
        SizeChanged += (_, args) => ResizeStudio(args.NewSize.Width, args.NewSize.Height);
    }

    private void ResizeStudio(double width, double height)
    {
        if (width <= 0 || height <= 0) { return; }
        // Let the grid use the real viewport. Only the theme list scrolls;
        // the preview takes the remaining space and keeps its image aspect ratio.
        var compact = height < 740;
        var side = width < 1150 ? 20 : 28;
        HeaderArea.Margin = new Thickness(side, compact ? 10 : 16, side, compact ? 10 : 16);
        ContentArea.Margin = new Thickness(side, 0, side, 0);
        FooterArea.Margin = new Thickness(side, compact ? 8 : 12, side, compact ? 4 : 8);
        ThemeHeadingArea.Margin = new Thickness(2, 2, 2, compact ? 6 : 10);
        NavigationArea.Margin = new Thickness(0, compact ? 6 : 10, 0, compact ? 6 : 10);
        OptionsFrame.Margin = new Thickness(0, compact ? 2 : 6, 0, compact ? 10 : 16);
        FiltersPanel.Margin = new Thickness(0, compact ? 10 : 16, 0, 0);
        DisplayControls.Margin = new Thickness(0, compact ? 10 : 16, 0, 0);
        LibraryColumn.Width = new GridLength(width < 1150 ? 224 : 248);
        LibraryGap.Width = new GridLength(width < 1150 ? 20 : 24);

        // On high-DPI laptops the USB row needs two lines, not horizontal scrolling.
        var narrow = width < 1060;
        Grid.SetColumnSpan(DeviceField, narrow ? 5 : 1);
        Grid.SetColumn(RefreshDevicesButton, narrow ? 5 : 1);
        Grid.SetRow(RefreshField, narrow ? 1 : 0);
        Grid.SetColumn(RefreshField, narrow ? 0 : 2);
        Grid.SetColumnSpan(RefreshField, narrow ? 2 : 1);
        Grid.SetRow(QualityField, narrow ? 1 : 0);
        Grid.SetColumn(QualityField, narrow ? 2 : 3);
        Grid.SetRow(StartDisplayButton, narrow ? 1 : 0);
        Grid.SetColumn(StartDisplayButton, narrow ? 3 : 4);
        Grid.SetColumnSpan(StartDisplayButton, narrow ? 2 : 1);
        Grid.SetRow(StopDisplayButton, narrow ? 1 : 0);
        RefreshField.Margin = QualityField.Margin = new Thickness(0, narrow ? 8 : 0, 12, 0);
        StartDisplayButton.Margin = new Thickness(0, narrow ? 8 : 0, 6, 0);
        StopDisplayButton.Margin = new Thickness(0, narrow ? 8 : 0, 0, 0);

        // Keep a useful preview even when the monitor cannot fit the preferred height.
        var shortWindow = height < 600;
        ProductIcon.Width = ProductIcon.Height = shortWindow ? 36 : 62;
        Heading.FontSize = shortWindow ? 22 : 28;
        SelectedThemeText.FontSize = shortWindow ? 22 : 26;
        Eyebrow.Visibility = CategoryText.Visibility = shortWindow ? Visibility.Collapsed : Visibility.Visible;
        InitiativeImage.Width = InitiativeImage.Height = shortWindow ? 28 : 36;
        FooterLinks.Margin = new Thickness(0, shortWindow ? 2 : 10, 0, 0);
        foreach (var label in new[] { FontLabel, SizeLabel, GeometryLabel, DeviceLabel, RefreshLabel, QualityLabel })
        { label.Visibility = shortWindow ? Visibility.Collapsed : Visibility.Visible; }

        CodexSetupFrame.Margin = new Thickness(14, compact ? 10 : 28, 8, 0);
        ScreensFrame.Margin = new Thickness(14, compact ? 10 : 16, 8, 0);
        CodexHelpText.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        CodexStatusText.TextWrapping = CodexEventText.TextWrapping = compact ? TextWrapping.NoWrap : TextWrapping.Wrap;
        CodexStatusText.TextTrimming = CodexEventText.TextTrimming = TextTrimming.CharacterEllipsis;
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
            foreach (var category in catalog.Categories)
            {
                var remembered = screenThemes.GetValueOrDefault(category.Id);
                if (!catalog.Themes.Any(theme => theme.Category == category.Id && theme.Id == remembered))
                {
                    screenThemes[category.Id] = catalog.Themes.FirstOrDefault(theme => theme.Category == category.Id)?.Id ?? "";
                }
            }
            selectedTheme = screenThemes.GetValueOrDefault(selectedCategory, selectedTheme);
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
        RememberScreenTheme();
        UpdateSelection();
        updatingChoices = wasUpdating;
    }

    private async Task RefreshAsync(bool queueIfBusy = true)
    {
        if (unloaded || !ready || displayBusy || devicesLoading
            || string.IsNullOrEmpty(selectedTheme) && (!displayActive || (catalog?.Themes.Count ?? 0) == 0)) { return; }
        if (refreshing) { if (queueIfBusy) { refreshAgain = true; } return; }
        refreshing = true;
        var requestedDisplay = displayActive;
        var engineReplied = false;
        try
        {
            ApplyAppearance();
            if (selectedCategory == "codex") { await RefreshCodexStatusAsync(); }
            if (CycleBox.IsChecked == true && Environment.TickCount64 - lastChange >= 20000) { CycleScreen(); }
            if (string.IsNullOrEmpty(selectedTheme) && (catalog?.Themes.Count ?? 0) == 0) { return; }
            var requestedSelection = selectedTheme;
            // An empty library category can still be browsed while an existing USB session
            // needs its eligible fallback (or a black frame when every screen is disabled).
            var requestedTheme = string.IsNullOrEmpty(selectedTheme)
                ? ScreenSelections().FirstOrDefault(screen => screen.Enabled && !string.IsNullOrEmpty(screen.ThemeId))?.ThemeId ?? catalog!.Themes[0].Id
                : selectedTheme;
            var requestedProfile = selectedProfile;
            var requestedLight = IsLight;
            var requestedFont = fontFamily;
            var requestedScale = fontScale;
            var requestedInterval = refreshMilliseconds;
            var requestedQuality = jpegQuality;
            var requestedSaturation = saturationPercent;
            var requestedHue = hueDegrees;
            var requestedSpectrum = showSpectrum;
            var requestedBrightness = brightnessPercent;
            var requestedContrast = contrastPercent;
            var requestedFilters = colorFiltersEnabled;
            var requestedScreenRevision = screenRevision;
            var requestedScreens = ScreenSelections();
            var state = await client.RequestAsync<PanelState>(requestedDisplay ? "present" : "state", new PreviewOptions(
                ProfileId: requestedProfile, Light: requestedLight, ThemeId: requestedTheme,
                FontFamily: requestedFont, FontScale: requestedScale, RefreshIntervalMilliseconds: requestedInterval,
                JpegQuality: requestedQuality, SaturationPercent: requestedSaturation, HueDegrees: requestedHue,
                ShowSpectrum: requestedSpectrum, BrightnessPercent: requestedBrightness, ContrastPercent: requestedContrast,
                ColorFiltersEnabled: requestedFilters, Screens: requestedScreens));
            engineReplied = true;
            if (unloaded || displayBusy) { return; }
            // A slow frame may complete after the user has selected another theme or font.
            if (requestedSelection != selectedTheme || requestedProfile != selectedProfile || requestedLight != IsLight || requestedFont != fontFamily || requestedScale != fontScale
                || requestedInterval != refreshMilliseconds || requestedQuality != jpegQuality || requestedDisplay != displayActive
                || requestedSaturation != saturationPercent || requestedHue != hueDegrees || requestedSpectrum != showSpectrum
                || requestedBrightness != brightnessPercent || requestedContrast != contrastPercent || requestedFilters != colorFiltersEnabled
                || requestedScreenRevision != screenRevision)
            { refreshAgain = true; return; }
            displayActive = state.DisplayRunning;
            codexProcessRunning = state.CodexProcessRunning;
            frameSuppressed = state.FrameSuppressed;
            var renderedTheme = state.ActiveThemeId ?? requestedTheme;
            var renderedCategory = catalog?.Themes.FirstOrDefault(theme => theme.Id == renderedTheme)?.Category ?? selectedCategory;
            var nextInterval = state.NextFrameMilliseconds is >= 25 and <= 1000
                ? Math.Min(refreshMilliseconds, state.NextFrameMilliseconds.Value) : refreshMilliseconds;
            refresh.Interval = TimeSpan.FromMilliseconds(nextInterval);
            UpdateDisplayControls();
            UpdateProfiles(state.Profiles);
            UpdateFrameHeading(renderedTheme, state.FrameSuppressed);
            SourceText.Text = state.FrameSuppressed ? "No enabled screen is available."
                : renderedCategory == "codex" ? "OpenAI Codex · local activity"
                : renderedCategory == "system" ? "Local hardware sensors"
                : string.IsNullOrWhiteSpace(state.Source) ? "System media session" : state.Source;
            StatusText.Text = state.Notices.Count > 0 ? string.Join(" · ", state.Notices)
                : displayActive ? state.Status : $"Preview · {refreshMilliseconds} ms · {jpegQuality} JPEG";
            if (requestedDisplay && !state.FrameSuppressed && renderedTheme != requestedSelection)
            {
                var chosenName = catalog?.Themes.FirstOrDefault(theme => theme.Id == requestedSelection)?.Name ?? "No theme";
                var shownName = catalog?.Themes.FirstOrDefault(theme => theme.Id == renderedTheme)?.Name ?? renderedTheme;
                StatusText.Text = $"{chosenName} selected · display uses {shownName}. {StatusText.Text}";
            }
            ScreensHelpText.Text = screenEnabled["codex"] && !codexProcessRunning
                ? "Codex is closed, so its screen is skipped on the display. You can still preview it."
                : "Codex hides when its process is closed. You can preview every theme.";
            if (!state.FrameSuppressed && state.FrameJpeg is { Length: > 0 } frame)
            {
                UpdatePreviewAccent(state.AccentColor);
                if (lastFrame is null || !lastFrame.AsSpan().SequenceEqual(frame))
                {
                    var themeChanged = lastRenderedTheme is not null && lastRenderedTheme != renderedTheme;
                    if (preview is null) { throw new InvalidOperationException("The desktop preview is unavailable."); }
                    // The JPEG is data, never an eval string. Wait for decode/draw before another
                    // frame can start, including at the requested 25 ms refresh interval.
                    var size = await preview.ShowAsync(frame, themeChanged);
                    if (unloaded) { return; }
                    previewPixelWidth = size.Width;
                    previewPixelHeight = size.Height;
                    lastFrame = frame;
                    FitPreviewSurface();
                }
                lastRenderedTheme = renderedTheme;
                LoadingPanel.Visibility = Visibility.Collapsed;
                PreviewBacking.Visibility = Visibility.Visible;
            }
            else
            {
                // Never label a previous theme's frame as the newly selected theme.
                ClearPreview();
                LoadingTitle.Text = state.FrameSuppressed ? "No active screens." : "The theme needs attention.";
                LoadingText.Text = state.FrameSuppressed
                    ? "Enable a screen to resume display updates."
                    : state.Notices.Count > 0 ? string.Join(" · ", state.Notices) : "No frame is available yet.";
            }
        }
        catch (Exception error)
        {
            // A browser decode/draw failure says nothing about the open USB session.
            refresh.Interval = TimeSpan.FromMilliseconds(refreshMilliseconds);
            if (requestedDisplay && !engineReplied) { displayActive = false; UpdateDisplayControls(); }
            StatusText.Text = error.Message;
            ClearPreview();
            LoadingTitle.Text = engineReplied ? "Preview unavailable." : requestedDisplay ? "Display stopped." : "Waiting for the local engine.";
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
        preview?.Clear();
        lastFrame = null;
        lastRenderedTheme = null;
        PreviewBacking.Visibility = Visibility.Collapsed;
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
        if (appliedLight == light) { return; }
        appliedLight = light;
        ((ModernTheme)Application.Current.Theme).CurrentPalette = light ? ModernTheme.Palettes.Light : ModernTheme.Palettes.Dark;
        OpenSilver.Interop.ExecuteJavaScriptVoid("document.documentElement.style.colorScheme = $0", light ? "light" : "dark");
        Root.Background = Brush(light ? "#FAF9FC" : "#101218");
        var transparent = new SolidColorBrush(Colors.Transparent);
        var surface = Brush(light ? "#FFFFFF" : "#191C25");
        var line = Brush(light ? "#DDD9E7" : "#303441");
        var ink = Brush(light ? "#242231" : "#F1F1FA");
        var muted = Brush(light ? "#716B80" : "#9C9CAF");
        var accent = Brush(light ? "#7854B5" : "#C9AEF3");
        // Sections share the page, without cards. Only the modal needs its own surface.
        foreach (var section in new[] { LibraryFrame, AppearanceFrame, OptionsFrame, DisplayFrame, ScreensFrame, CodexSetupFrame, PreviewBadge, PreviewFrame }) { section.Background = transparent; }
        AboutFrame.Background = surface;
        AboutFrame.BorderBrush = line;
        CloseFrame.Background = surface;
        CloseFrame.BorderBrush = line;
        CloseHeading.Foreground = ink;
        CloseDescription.Foreground = muted;
        foreach (var button in new[] { CancelCloseButton, ExitButton, MinimizeTrayButton })
        { button.Background = surface; button.Foreground = ink; button.BorderBrush = line; button.BorderThickness = new Thickness(1); }
        FiltersPanel.Background = Brush(light ? "#F0EDF5" : "#171A23");
        DisplayDivider.BorderBrush = line;
        foreach (var text in new[] { Heading, SelectedThemeText, LoadingTitle, PositionText, FontScaleText, SaturationText, HueText, BrightnessText, ContrastText, AboutHeading, AboutAuthor, AboutDisclosureHeading, CodexStatusText }) { text.Foreground = ink; }
        foreach (var text in new[] { Subtitle, Author, HeaderDot, CatalogText, SourceText, StatusText, LoadingText, FontLabel, SizeLabel, GeometryLabel, DeviceLabel, RefreshLabel, QualityLabel, SaturationLabel, HueLabel, BrightnessLabel, ContrastLabel, FiltersStatus, ScreensHelpText, CodexEventText, CodexHelpText, FooterDot2, AboutVersion, AboutDisclosure, AboutInitiative, AdministratorHelp }) { text.Foreground = muted; }
        foreach (var text in new[] { Eyebrow, LibraryHeading, CategoryText, PreviewBadgeText, ScreensHeading, CodexSetupHeading }) { text.Foreground = accent; }
        foreach (var link in new[] { Manifesto, AboutLink, TermsLink, PrivacyLink, AboutManifesto, ProjectLink, InitiativeLink, WebsiteLink, AboutTerms, AboutPrivacy }) { link.Foreground = accent; }
        CategoriesBox.Background = ThemesBox.Background = transparent;
        FontBox.Background = surface;
        FontBox.Foreground = ink;
        FontBox.BorderBrush = line;
        FontBox.BorderThickness = new Thickness(1);
        // Modern's PopupBorder binds to this same background. Keep it opaque in both palettes.
        foreach (var combo in new[] { ProfilesBox, DevicesBox, RefreshBox, QualityBox, FontsBox })
        {
            combo.Background = surface;
            combo.Foreground = ink;
            combo.BorderBrush = line;
            combo.BorderThickness = new Thickness(1);
        }
        CycleBox.Foreground = muted;
        LogErrorsBox.Foreground = muted;
        SpectrumBox.Foreground = muted;
        FiltersBox.Foreground = ink;
        foreach (var box in new[] { MusicScreenBox, CodexScreenBox, SystemScreenBox }) { box.Foreground = ink; }
        foreach (var button in new[] { ReloadButton, PreviousButton, NextButton, ApplyFontButton, FontListButton, ResetColorsButton, SmallerButton, LargerButton, CloseAboutButton, RefreshDevicesButton, StartDisplayButton, StopDisplayButton, CodexRefreshButton, CodexPrepareButton, OpenLogButton, AdministratorButton })
        { button.Background = transparent; button.Foreground = ink; button.BorderThickness = new Thickness(0); }
        StartDisplayButton.Background = Brush(light ? "#68468F" : "#C5AAE8");
        StartDisplayButton.Foreground = Brush(light ? "#FFFFFF" : "#1C1428");
        foreach (var button in new[] { SmallerButton, LargerButton, ResetColorsButton, StopDisplayButton, ApplyFontButton })
        {
            button.Background = surface;
            button.BorderBrush = line;
            button.BorderThickness = new Thickness(1);
        }
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
        SpectrumBox.IsEnabled = catalog?.Themes.FirstOrDefault(theme => theme.Id == selectedTheme)?.SupportsSpectrum == true;
        CodexSetupFrame.Visibility = selectedCategory == "codex" ? Visibility.Visible : Visibility.Collapsed;
        AdministratorPanel.Visibility = selectedCategory == "system" && windowActions?.CanRestartAsAdministrator == true
            ? Visibility.Visible : Visibility.Collapsed;
        CategoryText.Text = (categoryRows.FirstOrDefault(row => row.Id == selectedCategory)?.Name ?? selectedCategory).ToUpperInvariant();
        SelectedThemeText.Text = themeRows.FirstOrDefault(row => row.Id == selectedTheme)?.Name ?? "No themes installed";
        ToolTipService.SetToolTip(SelectedThemeText, null);
        PositionText.Text = $"{ThemesBox.SelectedIndex + 1:00} / {themeRows.Count:00}";
        PreviousButton.IsEnabled = NextButton.IsEnabled = themeRows.Count > 1;
        CycleBox.IsEnabled = (catalog?.Themes.Count ?? 0) > 1;
        UpdateDisplayControls();
        UpdateRows();
        if (themeRows.Count == 0) { ClearPreview(); LoadingTitle.Text = "This category is empty."; LoadingText.Text = "Add a theme pack and reload the library."; }
    }

    private void Navigate(int direction)
    {
        if (ThemesBox.Items.Count > 0) { ThemesBox.SelectedIndex = (ThemesBox.SelectedIndex + direction + ThemesBox.Items.Count) % ThemesBox.Items.Count; ThemesBox.ScrollIntoView(ThemesBox.SelectedItem); }
    }

    /// <summary>Keep one chosen theme per category, independent of screen eligibility.</summary>
    private void RememberScreenTheme()
    {
        if (string.IsNullOrEmpty(selectedTheme) || screenThemes.GetValueOrDefault(selectedCategory) == selectedTheme) { return; }
        screenThemes[selectedCategory] = selectedTheme;
        screenRevision++;
    }

    private ScreenSelectionDto[] ScreenSelections() => screenEnabled.Select(pair =>
        new ScreenSelectionDto(pair.Key, screenThemes.GetValueOrDefault(pair.Key, ""), pair.Value)).ToArray();

    /// <summary>Rotate chosen screens; a single enabled screen keeps the former theme rotation.</summary>
    private void CycleScreen()
    {
        lastChange = Environment.TickCount64;
        var screens = ScreenSelections().Where(screen => screen.Enabled && !string.IsNullOrEmpty(screen.ThemeId)
            && (!displayActive || screen.Category != "codex" || codexProcessRunning)).ToArray();
        if (screens.Length == 0) { return; }
        if (screens.Length == 1 && selectedCategory == screens[0].Category) { Navigate(1); return; }
        var current = Array.FindIndex(screens, screen => screen.Category == selectedCategory);
        var next = screens[(current + 1) % screens.Length];
        CategoriesBox.SelectedItem = categoryRows.FirstOrDefault(row => row.Id == next.Category);
    }

    private void Screen_Changed(object sender, RoutedEventArgs args)
    {
        if (!ready || updatingChoices) { return; }
        screenEnabled["multimedia"] = MusicScreenBox.IsChecked == true;
        screenEnabled["codex"] = CodexScreenBox.IsChecked == true;
        screenEnabled["system"] = SystemScreenBox.IsChecked == true;
        screenRevision++;
        lastChange = Environment.TickCount64;
        SaveChoices();
        _ = RefreshAsync();
    }

    /// <summary>Label the JPEG actually returned without changing the user's chosen theme.</summary>
    private void UpdateFrameHeading(string themeId, bool suppressed)
    {
        if (suppressed)
        {
            CategoryText.Text = "DISPLAY PAUSED";
            SelectedThemeText.Text = "No active screens";
            ToolTipService.SetToolTip(SelectedThemeText, null);
            return;
        }
        var theme = catalog?.Themes.FirstOrDefault(item => item.Id == themeId);
        if (theme is null) { return; }
        var category = categoryRows.FirstOrDefault(item => item.Id == theme.Category)?.Name ?? theme.Category;
        CategoryText.Text = category.ToUpperInvariant();
        SelectedThemeText.Text = theme.Name;
        ToolTipService.SetToolTip(SelectedThemeText, themeId != selectedTheme ? "The selected screen is unavailable. Showing this enabled screen on the display." : null);
    }

    private void UpdateDisplayControls()
    {
        StartDisplayButton.IsEnabled = !displayBusy && !displayActive && !devicesLoading && DevicesBox.SelectedItem is PanelChoice && !string.IsNullOrEmpty(selectedTheme);
        StopDisplayButton.IsEnabled = !displayBusy && (displayActive || startupRestorePending);
        DevicesBox.IsEnabled = !displayBusy && !displayActive && !devicesLoading;
        RefreshDevicesButton.IsEnabled = !displayBusy && !displayActive && !devicesLoading;
        ProfilesBox.IsEnabled = !displayBusy && !displayActive;
        PreviewBadgeText.Text = displayActive ? frameSuppressed ? "DISPLAY PAUSED" : "LIVE DISPLAY" : "LOCAL PREVIEW";
    }

    private async Task RefreshDevicesAsync()
    {
        if (unloaded || devicesLoading || displayBusy) { return; }
        devicesLoading = true;
        var discovered = false;
        UpdateDisplayControls();
        try
        {
            var choices = await client.RequestAsync<PanelChoice[]>("devices");
            if (unloaded) { return; }
            discovered = true;
            var previous = selectedDeviceId ?? (DevicesBox.SelectedItem as PanelChoice)?.Id;
            // The engine closes a missing session during discovery, even for a still frame.
            var disconnected = displayActive && !choices.Any(item => item.Id == selectedDeviceId);
            if (disconnected) { displayActive = false; }
            var selected = choices.FirstOrDefault(item => item.Id == previous);
            // An old interface path can change after a driver reinstall. With one compatible
            // screen, choosing it is unambiguous; with several, keep waiting for the saved one.
            if (selected is null && !startupRestorePending && (string.IsNullOrEmpty(previous) || choices.Length == 1)) { selected = choices.FirstOrDefault(); }
            var wasUpdating = updatingChoices;
            updatingChoices = true;
            DevicesBox.ItemsSource = choices;
            DevicesBox.SelectedItem = selected;
            updatingChoices = wasUpdating;
            if (disconnected) { StatusText.Text = "USB display disconnected. Reconnect it, then press Show on display."; }
            else if (choices.Length == 0 && !displayActive) { StatusText.Text = "Connect a USB display. The list refreshes every 10 seconds."; }
        }
        catch (Exception error) { StatusText.Text = error.Message; }
        finally { devicesLoading = false; UpdateDisplayControls(); }
        if (discovered) { await RestoreStartupDisplayAsync(); }
    }

    private async void StartDisplay_Click(object sender, RoutedEventArgs args)
    {
        startupRestorePending = false;
        if (displayBusy || displayActive || DevicesBox.SelectedItem is not PanelChoice device || string.IsNullOrEmpty(selectedTheme)) { return; }
        await StartDisplayAsync(device);
    }

    private async Task StartDisplayAsync(PanelChoice device)
    {
        if (displayBusy || displayActive || devicesLoading || string.IsNullOrEmpty(selectedTheme)) { return; }
        displayBusy = true;
        UpdateDisplayControls();
        StatusText.Text = "Connecting to the selected display…";
        try
        {
            var result = await client.RequestAsync<DisplayStartResult>("start", new StartOptions(device.Id, profileSelection, ThemeId: selectedTheme));
            displayActive = result.Accepted;
            if (displayActive)
            {
                selectedDeviceId = device.Id;
            }
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
        if (displayBusy || !displayActive && !startupRestorePending) { return; }
        startupRestorePending = false;
        displayBusy = true;
        UpdateDisplayControls();
        try
        {
            await client.RequestAsync<JsonElement>("stop");
            displayActive = false;
            SaveChoices();
            StatusText.Text = "Display stopped.";
        }
        catch (Exception error) { StatusText.Text = error.Message; }
        finally { displayBusy = false; UpdateDisplayControls(); }
    }

    private async void RefreshDevices_Click(object sender, RoutedEventArgs args) => await RefreshDevicesAsync();

    /// <summary>One Windows-startup start, for the saved device and theme only.</summary>
    private async Task RestoreStartupDisplayAsync()
    {
        if (!startupRestorePending || !startupReady || unloaded || displayBusy || displayActive || devicesLoading || catalog is null) { return; }
        if (!catalog.Themes.Any(theme => theme.Id == startupThemeId) || selectedTheme != startupThemeId)
        {
            startupRestorePending = false;
            StatusText.Text = "The saved startup theme is unavailable or changed. Press Show on display to choose again.";
            UpdateDisplayControls();
            return;
        }
        var device = (DevicesBox.ItemsSource as IEnumerable<PanelChoice>)?.FirstOrDefault(item => item.Id == startupDeviceId);
        if (device is null)
        {
            StatusText.Text = "Waiting for the saved USB display. Stop cancels startup output for this run.";
            return;
        }
        // Consume before the I/O: a failed or ambiguous start must never be replayed.
        startupRestorePending = false;
        UpdateDisplayControls();
        await StartDisplayAsync(device);
    }

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
        ToolTipService.SetToolTip(CodexStatusText, status.Status);
        ToolTipService.SetToolTip(CodexEventText, CodexEventText.Text);
        ToolTipService.SetToolTip(CodexPrepareButton, status.Instructions);
        ToolTipService.SetToolTip(CodexSetupFrame, status.Instructions);
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

    private void Device_Changed(object sender, SelectionChangedEventArgs args)
    {
        if (!ready || updatingChoices) { return; }
        if (DevicesBox.SelectedItem is PanelChoice device)
        {
            if (device.Id != startupDeviceId) { startupRestorePending = false; }
            selectedDeviceId = device.Id;
            SaveChoices();
        }
        UpdateDisplayControls();
    }
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

    private void About_Click(object sender, RoutedEventArgs args) => ShowAbout();

    /// <summary>Open About from the page or the native notification-area menu.</summary>
    public void ShowAbout()
    {
        CloseOverlay.Visibility = Visibility.Collapsed;
        AboutOverlay.Visibility = Visibility.Visible;
        CloseAboutButton.Focus();
    }
    private void CloseAbout_Click(object sender, RoutedEventArgs args) => CloseAbout();

    /// <summary>Offer an explicit exit or background choice for the native close button.</summary>
    public void ShowCloseChoice()
    {
        if (unloaded) { return; }
        AboutOverlay.Visibility = Visibility.Collapsed;
        MinimizeTrayButton.IsEnabled = windowActions?.CanMinimizeToTray == true;
        CloseOverlay.Visibility = Visibility.Visible;
        CancelCloseButton.Focus();
    }

    private void CancelClose_Click(object sender, RoutedEventArgs args) => CloseOverlay.Visibility = Visibility.Collapsed;

    private void Exit_Click(object sender, RoutedEventArgs args) => ExitApplication();

    private async void Administrator_Click(object sender, RoutedEventArgs args)
    {
        if (restartingAsAdministrator || windowActions?.CanRestartAsAdministrator != true) { return; }
        restartingAsAdministrator = true;
        AdministratorButton.IsEnabled = false;
        // Keep the existing session alive until Windows accepts the elevated launch.
        try
        {
            colorChanges.Stop();
            if (pendingColorSettings) { CommitColorControls(); }
            SaveChoices();
            if (await windowActions.RestartAsAdministratorAsync()) { ExitApplication(); }
            else { StatusText.Text = "Administrator restart canceled. The current session is still running."; }
        }
        catch (Exception error) { StatusText.Text = "Could not reopen as administrator: " + error.Message; }
        finally { restartingAsAdministrator = false; AdministratorButton.IsEnabled = true; }
    }

    /// <summary>Flush pending choices before asking the desktop host to stop.</summary>
    public void ExitApplication()
    {
        colorChanges.Stop();
        if (pendingColorSettings) { CommitColorControls(); }
        SaveChoices();
        windowActions?.CloseApplication();
    }

    private void MinimizeTray_Click(object sender, RoutedEventArgs args)
    {
        if (windowActions?.CanMinimizeToTray != true) { return; }
        try
        {
            colorChanges.Stop();
            if (pendingColorSettings) { CommitColorControls(); }
            SaveChoices();
            CloseOverlay.Visibility = Visibility.Collapsed;
            windowActions.MinimizeToTray();
        }
        catch (Exception error) { StatusText.Text = "Could not minimize to tray: " + error.Message; }
    }
    private void CloseAbout() { AboutOverlay.Visibility = Visibility.Collapsed; AboutLink.Focus(); }

    private void Category_Changed(object sender, SelectionChangedEventArgs args)
    {
        if (!ready || updatingChoices) { return; }
        selectedCategory = (CategoriesBox.SelectedItem as CatalogRow)?.Id ?? "multimedia";
        selectedTheme = screenThemes.GetValueOrDefault(selectedCategory, "");
        PopulateThemes();
        lastChange = Environment.TickCount64;
        SaveChoices();
        _ = RefreshAsync();
    }
    private void Theme_Changed(object sender, SelectionChangedEventArgs args)
    {
        if (!ready || updatingChoices) { return; }
        selectedTheme = (ThemesBox.SelectedItem as CatalogRow)?.Id ?? "";
        RememberScreenTheme();
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
    private void SetAppearance(int value) { appearance = value; ApplyAppearance(); UpdateAppearanceButtons(); SaveChoices(); _ = RefreshAsync(); }
    private void Font_Click(object sender, RoutedEventArgs args) => ApplyFont();
    private void Font_KeyDown(object sender, KeyEventArgs args) { if (args.Key == Key.Enter) { ApplyFont(); args.Handled = true; } }
    private void ApplyFont()
    {
        var value = FontBox.Text.Trim();
        fontFamily = string.IsNullOrWhiteSpace(value) ? "Cascadia Mono" : value[..Math.Min(value.Length, 128)];
        FontBox.Text = fontFamily;
        SelectCurrentFont();
        SaveChoices();
        _ = RefreshAsync();
    }
    private async Task LoadFontsAsync()
    {
        try
        {
            var fonts = await client.RequestAsync<FontCatalogDto>("fonts");
            if (unloaded) { return; }
            fontChoices = fonts.Families.Select(name => new PanelChoice(name, name)).Append(new PanelChoice("", "Custom font…")).ToArray();
            updatingFonts = true;
            FontsBox.ItemsSource = fontChoices;
            updatingFonts = false;
            FontListButton.IsEnabled = fonts.Families.Count > 0;
            ToolTipService.SetToolTip(FontsBox, fonts.Notice ?? "Installed monospace fonts. Choose Custom font to type a name.");
            SelectCurrentFont();
        }
        catch (Exception error)
        {
            ToolTipService.SetToolTip(FontListButton, "Font list unavailable: " + error.Message);
            ShowCustomFont();
        }
    }

    private void SelectCurrentFont()
    {
        var choice = fontChoices.FirstOrDefault(font => string.Equals(font.Id, fontFamily, StringComparison.OrdinalIgnoreCase));
        updatingFonts = true;
        FontsBox.SelectedItem = choice;
        updatingFonts = false;
        if (choice is null) { ShowCustomFont(); return; }
        FontsBox.Visibility = Visibility.Visible;
        FontBox.Visibility = ApplyFontButton.Visibility = FontListButton.Visibility = Visibility.Collapsed;
        FontLabel.Text = "FONT · MONOSPACE";
    }

    private void ShowCustomFont()
    {
        FontsBox.Visibility = Visibility.Collapsed;
        FontBox.Visibility = ApplyFontButton.Visibility = FontListButton.Visibility = Visibility.Visible;
        FontLabel.Text = "FONT FAMILY";
    }

    private void FontList_Click(object sender, RoutedEventArgs args)
    {
        updatingFonts = true;
        FontsBox.SelectedItem = fontChoices.LastOrDefault();
        updatingFonts = false;
        FontsBox.Visibility = Visibility.Visible;
        FontBox.Visibility = ApplyFontButton.Visibility = FontListButton.Visibility = Visibility.Collapsed;
        FontLabel.Text = "FONT · MONOSPACE";
        FontsBox.IsDropDownOpen = true;
    }

    private void FontMenu_Closed(object? sender, EventArgs args)
    {
        if (!updatingFonts && FontsBox.SelectedItem is PanelChoice { Id.Length: 0 }) { ShowCustomFont(); }
    }

    private void FontSelection_Changed(object sender, SelectionChangedEventArgs args)
    {
        if (!ready || updatingFonts || FontsBox.SelectedItem is not PanelChoice choice) { return; }
        if (choice.Id.Length == 0) { ShowCustomFont(); FontBox.Focus(); return; }
        fontFamily = choice.Id;
        FontBox.Text = fontFamily;
        SaveChoices(); _ = RefreshAsync();
    }

    private void UpdateColorControls()
    {
        updatingColorControls = true;
        SaturationSlider.Value = saturationPercent;
        HueSlider.Value = hueDegrees;
        BrightnessSlider.Value = brightnessPercent;
        ContrastSlider.Value = contrastPercent;
        SaturationText.Text = $"{saturationPercent:0}%";
        HueText.Text = $"{hueDegrees:0}°";
        BrightnessText.Text = $"{brightnessPercent:+0;-0;0}%";
        ContrastText.Text = $"{contrastPercent:0}%";
        updatingColorControls = false;
        UpdateColorRamps();
    }

    private void Color_Changed(object sender, RoutedPropertyChangedEventArgs<double> args)
    {
        if (!ready || updatingColorControls) { return; }
        // Slider values are drafts until the debounce expires; the refresh loop
        // keeps using the applied values instead of rebuilding a filter for every move.
        SaturationText.Text = $"{Math.Round(SaturationSlider.Value):0}%";
        HueText.Text = $"{Math.Round(HueSlider.Value):0}°";
        BrightnessText.Text = $"{Math.Round(BrightnessSlider.Value):+0;-0;0}%";
        ContrastText.Text = $"{Math.Round(ContrastSlider.Value):0}%";
        UpdateColorRamps();
        pendingColorSettings = true;
        colorChanges.Stop(); colorChanges.Start();
    }

    private void CommitColorControls()
    {
        saturationPercent = Math.Round(SaturationSlider.Value);
        hueDegrees = Math.Round(HueSlider.Value);
        brightnessPercent = Math.Round(BrightnessSlider.Value);
        contrastPercent = Math.Round(ContrastSlider.Value);
        pendingColorSettings = false;
    }

    private void ResetColors_Click(object sender, RoutedEventArgs args)
    {
        colorChanges.Stop(); pendingColorSettings = false;
        saturationPercent = contrastPercent = 100;
        hueDegrees = brightnessPercent = 0;
        UpdateColorControls(); SaveChoices(); _ = RefreshAsync();
    }

    private void Spectrum_Changed(object sender, RoutedEventArgs args)
    {
        if (!ready) { return; }
        showSpectrum = SpectrumBox.IsChecked == true;
        SaveChoices(); _ = RefreshAsync();
    }

    /// <summary>Start a fresh twenty-second interval when automatic theme cycling is enabled.</summary>
    private void Cycle_Changed(object sender, RoutedEventArgs args)
    {
        if (!ready) { return; }
        cycleThemes = CycleBox.IsChecked == true;
        lastChange = Environment.TickCount64;
        SaveChoices();
    }
    private void Smaller_Click(object sender, RoutedEventArgs args) => ChangeFontScale(-0.1);
    private void Larger_Click(object sender, RoutedEventArgs args) => ChangeFontScale(0.1);
    private void ChangeFontScale(double change) { fontScale = Math.Round(Math.Clamp(fontScale + change, 0.7, 1.5), 1); UpdateFontScale(); SaveChoices(); _ = RefreshAsync(); }
    /// <summary>Show the applied text scale and make its limits visible on the step buttons.</summary>
    private void UpdateFontScale()
    {
        FontScaleText.Text = $"{fontScale:P0}";
        SmallerButton.IsEnabled = fontScale > .7;
        LargerButton.IsEnabled = fontScale < 1.5;
    }
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
            if (!string.IsNullOrEmpty(selectedTheme)) { screenThemes[selectedCategory] = selectedTheme; }
            if (json.TryGetProperty("screens", out var screens) && screens.ValueKind == JsonValueKind.Array)
            {
                foreach (var screen in screens.EnumerateArray())
                {
                    if (screen.ValueKind != JsonValueKind.Object || !screen.TryGetProperty("category", out var screenCategory)
                        || screenCategory.ValueKind != JsonValueKind.String || screenCategory.GetString() is not { } categoryId
                        || !screenEnabled.ContainsKey(categoryId)) { continue; }
                    if (screen.TryGetProperty("enabled", out var enabled) && enabled.ValueKind is JsonValueKind.True or JsonValueKind.False)
                    { screenEnabled[categoryId] = enabled.GetBoolean(); }
                    if (screen.TryGetProperty("themeId", out var screenTheme) && screenTheme.ValueKind == JsonValueKind.String)
                    { screenThemes[categoryId] = screenTheme.GetString() ?? ""; }
                }
            }
            else if (selectedCategory == "codex")
            {
                // Preserve a saved Codex session from settings predating screen toggles.
                screenEnabled["codex"] = true;
            }
            if (json.TryGetProperty("profile", out var profile)) { selectedProfile = profile.GetString() ?? selectedProfile; }
            if (json.TryGetProperty("profileSelection", out var profileChoice)) { profileSelection = profileChoice.GetString() ?? "auto"; }
            if (json.TryGetProperty("font", out var font)) { fontFamily = font.GetString() ?? fontFamily; }
            if (json.TryGetProperty("fontScale", out var scale) && scale.TryGetDouble(out var number) && double.IsFinite(number)) { fontScale = Math.Clamp(number, 0.7, 1.5); }
            if (json.TryGetProperty("refreshMilliseconds", out var interval) && interval.TryGetInt32(out var milliseconds) && RefreshIntervals.Contains(milliseconds)) { refreshMilliseconds = milliseconds; }
            if (json.TryGetProperty("jpegQuality", out var quality) && quality.GetString() is "poor" or "low" or "medium" or "high") { jpegQuality = quality.GetString()!; }
            if (json.TryGetProperty("saturationPercent", out var saturation) && saturation.TryGetDouble(out var saturationValue) && double.IsFinite(saturationValue)) { saturationPercent = Math.Clamp(saturationValue, 0, 200); }
            if (json.TryGetProperty("hueDegrees", out var hue) && hue.TryGetDouble(out var hueValue) && double.IsFinite(hueValue)) { hueDegrees = Math.Clamp(hueValue, -180, 180); }
            if (json.TryGetProperty("brightnessPercent", out var brightness) && brightness.TryGetDouble(out var brightnessValue) && double.IsFinite(brightnessValue)) { brightnessPercent = Math.Clamp(brightnessValue, -100, 100); }
            if (json.TryGetProperty("contrastPercent", out var contrast) && contrast.TryGetDouble(out var contrastValue) && double.IsFinite(contrastValue)) { contrastPercent = Math.Clamp(contrastValue, 0, 200); }
            if (json.TryGetProperty("showSpectrum", out var spectrum) && spectrum.ValueKind is JsonValueKind.True or JsonValueKind.False) { showSpectrum = spectrum.GetBoolean(); }
            if (json.TryGetProperty("colorFiltersEnabled", out var filters) && filters.ValueKind is JsonValueKind.True or JsonValueKind.False) { colorFiltersEnabled = filters.GetBoolean(); }
            if (json.TryGetProperty("cycleThemes", out var cycle) && cycle.ValueKind is JsonValueKind.True or JsonValueKind.False) { cycleThemes = cycle.GetBoolean(); }
            if (json.TryGetProperty("deviceId", out var device) && device.ValueKind == JsonValueKind.String) { selectedDeviceId = device.GetString(); }
        }
        catch (Exception error) when (error is IOException or JsonException or InvalidOperationException or UnauthorizedAccessException) { }
    }
    private void SaveChoices()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            var temporary = settingsPath + ".tmp";
            var screens = ScreenSelections().Select(screen => new { category = screen.Category, themeId = screen.ThemeId, enabled = screen.Enabled }).ToArray();
            File.WriteAllText(temporary, JsonSerializer.Serialize(new { theme = appearance, category = selectedCategory, themeId = selectedTheme, profile = selectedProfile, profileSelection, font = fontFamily, fontScale, refreshMilliseconds, jpegQuality, saturationPercent, hueDegrees, brightnessPercent, contrastPercent, showSpectrum, colorFiltersEnabled, cycleThemes, screens, deviceId = selectedDeviceId }));
            File.Move(temporary, settingsPath, overwrite: true);
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
