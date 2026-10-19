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
using VibePanel.Studio;

namespace VibePanel.App;

/// <summary>A manifest-driven theme library and preview. The engine owns data and rendering.</summary>
public sealed partial class MainPage : Page
{
    private readonly IStudioWindowActions? windowActions;
    private readonly IStudioPreview? preview;
    private readonly StudioSession session;
    public StudioSession Session => session;
    private readonly bool ownsSession;
    private readonly DispatcherTimer refresh = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly DispatcherTimer colorChanges = new() { Interval = TimeSpan.FromMilliseconds(150) };
    private readonly List<CatalogRow> categoryRows = [];
    private readonly List<CatalogRow> themeRows = [];
    private StudioViewModel viewModel => session.ViewModel;
    private IReadOnlyDictionary<string, bool> screenEnabled => viewModel.Read().ScreenEnabled;
    private static readonly int[] RefreshIntervals = [1000, 500, 250, 100, 50, 25];
    private ThemeCatalogDto? catalog;
    private bool unloaded, ready, updatingChoices, catalogLoading;
    private bool displayActive => session.Snapshot.DisplayActive;
    private bool displayBusy => session.Snapshot.Busy;
    private bool startupRestorePending => session.Snapshot.StartupPending;
    private bool devicesLoading;
    private bool codexLoading, codexPreparing, diagnosticsChanging, diagnosticsEnabled, restartingAsAdministrator;
    private long codexStatusAt, appliedFrameRevision;
    private bool codexProcessRunning => session.Snapshot.Frame?.CodexProcessRunning == true;
    private bool frameSuppressed => session.Snapshot.Frame?.FrameSuppressed == true;
    private bool updatingColorControls, pendingColorSettings, updatingFonts;
    private PanelChoice[] fontChoices = [];
    private string? lastRenderedTheme;
    private byte[]? lastFrame;
    private bool previewUpdating;
    private int previewGeneration;
    private bool? appliedLight;
    private PanelChoice[]? appliedDevices;
    private int refreshMilliseconds { get => viewModel.Preferences.RefreshMilliseconds; set => viewModel.Edit(p => p.RefreshMilliseconds = value); }
    private string jpegQuality { get => viewModel.Preferences.JpegQuality; set => viewModel.Edit(p => p.JpegQuality = value); }
    private string profileSelection { get => viewModel.Preferences.ProfileSelection; set => viewModel.Edit(p => p.ProfileSelection = value); }
    private long lastChange { get => viewModel.Preferences.LastChange; set => viewModel.Edit(p => p.LastChange = value); }
    private string selectedCategory => viewModel.Preferences.SelectedCategory;
    private string selectedTheme => viewModel.Preferences.SelectedTheme;
    private string selectedProfile { get => viewModel.Preferences.SelectedProfile; set => viewModel.Edit(p => p.SelectedProfile = value); }
    private string fontFamily { get => viewModel.Preferences.FontFamily; set => viewModel.Edit(p => p.FontFamily = value); }
    private int appearance { get => viewModel.Preferences.Appearance; set => viewModel.Edit(p => p.Appearance = value); }
    private double fontScale { get => viewModel.Preferences.FontScale; set => viewModel.Edit(p => p.FontScale = value); }
    private double saturationPercent { get => viewModel.Preferences.SaturationPercent; set => viewModel.Edit(p => p.SaturationPercent = value); }
    private double hueDegrees { get => viewModel.Preferences.HueDegrees; set => viewModel.Edit(p => p.HueDegrees = value); }
    private double brightnessPercent { get => viewModel.Preferences.BrightnessPercent; set => viewModel.Edit(p => p.BrightnessPercent = value); }
    private double contrastPercent { get => viewModel.Preferences.ContrastPercent; set => viewModel.Edit(p => p.ContrastPercent = value); }
    private bool colorFiltersEnabled { get => viewModel.Preferences.ColorFiltersEnabled; set => viewModel.Edit(p => p.ColorFiltersEnabled = value); }
    private bool cycleThemes { get => viewModel.Preferences.CycleThemes; set => viewModel.Edit(p => p.CycleThemes = value); }
    private string? selectedDeviceId { get => viewModel.Preferences.SelectedDeviceId; set => viewModel.Edit(p => p.SelectedDeviceId = value); }
    private bool showSpectrum { get => viewModel.Preferences.ShowSpectrum; set => viewModel.Edit(p => p.ShowSpectrum = value); }

    public MainPage(IPanelClient client, IStudioWindowActions? windowActions = null, string? presentationSettingsPath = null, IStudioPreview? preview = null, bool restoreLastSession = false, StudioSession? session = null)
    {
        this.windowActions = windowActions;
        this.preview = preview;
        this.session = session ?? new StudioSession(client, presentationSettingsPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VibePanel", "presentation.json"), restoreLastSession);
        ownsSession = session is null;
        InitializeComponent();
        InitializePreviewAppearance();
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
            await LoadCatalogAsync();
            await RefreshDevicesAsync();
            await LoadDiagnosticsAsync();
            await RefreshAsync();
            await LoadFontsAsync();
            if (!unloaded)
            {
                refresh.Start();
            }
        };
        Unloaded += (_, _) =>
        {
            unloaded = true;
            if (ownsSession) { _ = this.session.ShutdownAsync(); }
            refresh.Stop(); colorChanges.Stop();
            ClearPreview();
            if (pendingColorSettings) { CommitColorControls(); }
            SaveChoices();
        };
        refresh.Tick += async (_, _) => await RefreshAsync(queueIfBusy: false);
        colorChanges.Tick += async (_, _) =>
        {
            colorChanges.Stop(); CommitColorControls();
            SaveChoices(); await RefreshAsync();
        };
        KeyDown += (_, args) =>
        {
            if (PaletteOverlay.Visibility == Visibility.Visible)
            { if (args.Key == Key.Escape) { ClosePalette_Click(this, args); args.Handled = true; } return; }
            if (CloseOverlay.Visibility == Visibility.Visible)
            { if (args.Key == Key.Escape) { CloseOverlay.Visibility = Visibility.Collapsed; args.Handled = true; } return; }
            if (AboutOverlay.Visibility == Visibility.Visible)
            { if (args.Key == Key.Escape) { CloseAbout(); args.Handled = true; } return; }
            if (SensorDebugPanel.Visibility == Visibility.Visible) { return; }
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
            if (session.Snapshot.Catalog is null) { await session.ReloadCatalogAsync(); }
            catalog = session.Snapshot.Catalog ?? throw new IOException("Theme catalog unavailable.");
            if (unloaded) { return; }
            updatingChoices = true;
            categoryRows.Clear();
            foreach (var category in catalog.Categories) { categoryRows.Add(new(category.Id, category.Name)); }
            CategoriesBox.ItemsSource = categoryRows.ToArray();
            CategoriesBox.SelectedItem = categoryRows.FirstOrDefault(row => row.Id == selectedCategory) ?? categoryRows.FirstOrDefault();
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
        UpdateSelection();
        updatingChoices = wasUpdating;
    }

    private Task RefreshAsync(bool queueIfBusy = true)
    {
        session.PreviewVisible = SensorDebugPanel.Visibility != Visibility.Visible && preview?.IsVisible != false;
        if (queueIfBusy) { session.Wake(); }
        if (unloaded || !ready || preview?.IsVisible == false) { lastFrame = null; return Task.CompletedTask; }
        var current = session.Snapshot;
        ApplyAppearance();
        if (SensorDebugPanel.Visibility == Visibility.Visible) { StatusText.Text = current.Status; return Task.CompletedTask; }
        if (selectedCategory == "codex") { _ = RefreshCodexStatusAsync(); }
        ApplyDevices(current.Devices);
        var p = viewModel.Preferences;
        if ((CategoriesBox.SelectedItem as CatalogRow)?.Id != p.SelectedCategory || (ThemesBox.SelectedItem as CatalogRow)?.Id != p.SelectedTheme)
        {
            updatingChoices = true;
            CategoriesBox.SelectedItem = categoryRows.FirstOrDefault(row => row.Id == p.SelectedCategory);
            PopulateThemes();
            updatingChoices = false;
        }
        UpdateDisplayControls();
        StatusText.Text = viewModel.PersistenceNotice ?? current.Status;
        if (current.Frame is not { } state || current.FrameRevision == appliedFrameRevision && lastFrame is not null) { return Task.CompletedTask; }
        appliedFrameRevision = current.FrameRevision;
        UpdateProfiles(state.Profiles);
        var renderedTheme = state.ActiveThemeId ?? selectedTheme;
        var category = catalog?.Themes.FirstOrDefault(theme => theme.Id == renderedTheme)?.Category ?? selectedCategory;
        UpdateFrameHeading(renderedTheme, state.FrameSuppressed);
        SourceText.Text = state.FrameSuppressed ? "No enabled screen is available." : category == "codex" ? "OpenAI Codex · local activity"
            : category == "system" ? "Local hardware sensors" : state.Source;
        if (state.System.Driver is { } driver) { ApplySensorDriverStatus(driver); }
        if (state.Notices.Count > 0) { StatusText.Text = string.Join(" · ", new[] { viewModel.PersistenceNotice }.Where(n => n is not null).Concat(state.Notices)); }
        ScreensHelpText.Text = screenEnabled["codex"] && !codexProcessRunning
            ? "Codex is closed, so its screen is skipped on the display. You can still preview it."
            : "Codex hides when its process is closed. You can preview every theme.";
        if (!state.FrameSuppressed && state.FrameJpeg is { Length: > 0 } frame)
        {
            UpdatePreviewAccent(state.AccentColor);
            if (!previewUpdating && (lastFrame is null || !lastFrame.AsSpan().SequenceEqual(frame))) { _ = UpdatePreviewAsync(frame, renderedTheme); }
        }
        else
        {
            ClearPreview();
            LoadingTitle.Text = state.FrameSuppressed ? "No active screens." : "The theme needs attention.";
            LoadingText.Text = state.Notices.Count > 0 ? string.Join(" · ", state.Notices) : "No frame is available yet.";
        }
        return Task.CompletedTask;
    }

    private void ApplyDevices(PanelChoice[]? choices)
    {
        if (choices is null || ReferenceEquals(choices, appliedDevices)) { return; }
        appliedDevices = choices;
        var wasUpdating = updatingChoices;
        updatingChoices = true;
        DevicesBox.ItemsSource = choices;
        DevicesBox.SelectedItem = choices.FirstOrDefault(item => item.Id == selectedDeviceId);
        updatingChoices = wasUpdating;
    }

    private async Task UpdatePreviewAsync(byte[] frame, string renderedTheme)
    {
        previewUpdating = true;
        var generation = previewGeneration;
        try
        {
            if (preview is null) { throw new InvalidOperationException("The desktop preview is unavailable."); }
            var themeChanged = lastRenderedTheme is not null && lastRenderedTheme != renderedTheme;
            var size = await preview.ShowAsync(frame, themeChanged);
            if (unloaded || generation != previewGeneration || !preview.IsVisible) { return; }
            previewPixelWidth = size.Width;
            previewPixelHeight = size.Height;
            lastFrame = frame;
            lastRenderedTheme = renderedTheme;
            FitPreviewSurface();
            LoadingPanel.Visibility = Visibility.Collapsed;
            PreviewBacking.Visibility = Visibility.Visible;
        }
        catch (Exception error)
        {
            if (unloaded || generation != previewGeneration || preview?.IsVisible == false) { return; }
            StatusText.Text = error.Message;
            ClearPreview();
            LoadingTitle.Text = "Preview unavailable.";
            LoadingText.Text = error.Message;
        }
        finally { previewUpdating = false; }
    }

    private void ClearPreview()
    {
        previewGeneration++;
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

    private bool IsLight => viewModel.Preferences.Light;

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
        SensorSetupFrame.Background = transparent;
        SensorSetupHeading.Foreground = accent;
        SensorDriverText.Foreground = ink;
        SensorDriverPath.Foreground = muted;
        SensorDriverHelp.Foreground = muted;
        SensorDriverFolderButton.Background = surface;
        SensorDriverFolderButton.Foreground = ink;
        SensorDriverFolderButton.BorderBrush = line;
        PaletteFrame.Background = surface; PaletteFrame.BorderBrush = line;
        foreach (var text in new[] { PaletteHeading, PalettePrimaryLabel, PaletteSecondaryLabel, PaletteAccentLabel, PaletteWarningLabel, PaletteInfoLabel }) { text.Foreground = ink; }
        PaletteHelp.Foreground = muted; PaletteError.Foreground = Brush(light ? "#A12D35" : "#FFADB3");
        foreach (var box in new[] { PalettePrimary, PaletteSecondary, PaletteAccent, PaletteWarning, PaletteInfo }) { box.Background = surface; box.Foreground = ink; box.BorderBrush = line; }
        foreach (var button in new[] { PaletteButton, PaletteReset, PaletteCancel, PaletteApply }) { button.Background = surface; button.Foreground = ink; button.BorderBrush = line; }
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
        ApplySensorDebugAppearance();
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
        SensorSetupFrame.Visibility = selectedCategory == "system" ? Visibility.Visible : Visibility.Collapsed;
        if (selectedCategory == "system") { _ = RefreshSensorDriverStatusAsync(); }
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

    private void Screen_Changed(object sender, RoutedEventArgs args)
    {
        if (!ready || updatingChoices) { return; }
        viewModel.SetScreens(MusicScreenBox.IsChecked == true, CodexScreenBox.IsChecked == true, SystemScreenBox.IsChecked == true);
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
        if (devicesLoading) { return; }
        devicesLoading = true;
        try { await session.RefreshDevicesAsync(); ApplyDevices(session.Snapshot.Devices); }
        catch (Exception error) { StatusText.Text = error.Message; }
        finally { devicesLoading = false; UpdateDisplayControls(); }
    }

    private async void StartDisplay_Click(object sender, RoutedEventArgs args)
    {
        if (displayBusy || displayActive || DevicesBox.SelectedItem is not PanelChoice device) { return; }
        await session.StartAsync(device.Id);
        await RefreshAsync();
    }

    private async void StopDisplay_Click(object sender, RoutedEventArgs args)
    {
        if (displayBusy || !displayActive && !startupRestorePending) { return; }
        await session.StopAsync();
        await RefreshAsync();
    }

    private async void RefreshDevices_Click(object sender, RoutedEventArgs args) => await RefreshDevicesAsync();

    private async Task RefreshCodexStatusAsync(bool force = false)
    {
        if (codexLoading || codexPreparing || !force && Environment.TickCount64 - codexStatusAt < 5000) { return; }
        codexLoading = true;
        CodexRefreshButton.IsEnabled = false;
        try
        {
            var status = await viewModel.ReadCodexAsync();
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
            var status = await viewModel.ReadCodexAsync(prepare: true);
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
            var status = await viewModel.ReadDiagnosticsAsync();
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
            var status = await viewModel.ReadDiagnosticsAsync(LogErrorsBox.IsChecked == true);
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
            var result = await viewModel.OpenDiagnosticsAsync();
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
            session.CancelStartup();
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
        AdministratorButton.IsEnabled = SensorDebugAdministrator.IsEnabled = false;
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
        finally { restartingAsAdministrator = false; AdministratorButton.IsEnabled = SensorDebugAdministrator.IsEnabled = true; }
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
        if (catalog is null) { return; }
        viewModel.SelectCategory((CategoriesBox.SelectedItem as CatalogRow)?.Id ?? "multimedia", catalog);
        PopulateThemes();
        lastChange = Environment.TickCount64;
        SaveChoices();
        _ = RefreshAsync();
    }
    private void Theme_Changed(object sender, SelectionChangedEventArgs args)
    {
        if (!ready || updatingChoices) { return; }
        if (catalog is null) { return; }
        viewModel.SelectTheme((ThemesBox.SelectedItem as CatalogRow)?.Id ?? "", catalog);
        lastChange = Environment.TickCount64;
        UpdateSelection();
        SaveChoices();
        _ = RefreshAsync();
    }
    private void Profile_Changed(object sender, SelectionChangedEventArgs args)
    {
        if (!ready || updatingChoices) { return; }
        var profile = (ProfilesBox.SelectedItem as PanelChoice)?.Id ?? "auto";
        viewModel.Edit(p => { p.ProfileSelection = profile; if (profile != "auto") { p.SelectedProfile = profile; } });
        SaveChoices();
        _ = RefreshAsync();
    }
    private async void Reload_Click(object sender, RoutedEventArgs args)
    {
        try { await session.ReloadCatalogAsync(); await LoadCatalogAsync(); await RefreshAsync(); }
        catch (Exception error) { StatusText.Text = error.Message; }
    }
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
            var fonts = await viewModel.ReadFontsAsync();
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
        // Keep current choices in the model immediately, including during native
        // tray/setup shutdown. Debounce only disk persistence and preview wakeups.
        CommitColorControls();
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
        viewModel.Edit(p =>
        {
            p.SaturationPercent = Math.Round(SaturationSlider.Value);
            p.HueDegrees = Math.Round(HueSlider.Value);
            p.BrightnessPercent = Math.Round(BrightnessSlider.Value);
            p.ContrastPercent = Math.Round(ContrastSlider.Value);
        });
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

    private void SaveChoices() => session.Save();

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
