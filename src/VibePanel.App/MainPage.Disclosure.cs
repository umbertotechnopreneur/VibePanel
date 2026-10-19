// VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT. VBWR E
using System.Windows;
using System.Windows.Controls;

namespace VibePanel.App;

public sealed partial class MainPage
{
    private void ShowSessionStatus(string status)
    {
        TimingDetailsText.Text = status;
        StatusText.Text = viewModel.PersistenceNotice ?? (status.StartsWith("Display · requested ", StringComparison.Ordinal)
            ? "Display running" : status);
    }

    private void Disclosure_Click(object sender, RoutedEventArgs args)
    {
        if (sender is not Button selected) { return; }
        (Button Button, FrameworkElement Panel, string Title)[] sections =
        [
            (ScreensDetailsButton, ScreensDetails, "Screens on display"),
            (CodexDetailsButton, CodexDetails, "Codex connection"),
            (SensorDetailsButton, SensorDetails, "Sensor access"),
            (AppearanceDetailsButton, OptionsFrame, "Appearance"),
            (DisplayOptionsButton, DisplayOptionsPanel, "Display options"),
            (DiagnosticsDetailsButton, DiagnosticsDetails, "Diagnostics")
        ];
        var expand = sections.Any(section => section.Button == selected && section.Panel.Visibility != Visibility.Visible);
        var sidebar = selected == ScreensDetailsButton || selected == CodexDetailsButton || selected == SensorDetailsButton;
        foreach (var (button, panel, title) in sections)
        {
            if (button != selected && !(sidebar && (button == ScreensDetailsButton || button == CodexDetailsButton || button == SensorDetailsButton))) { continue; }
            var visible = button == selected && expand;
            panel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            button.Content = (visible ? "⌄ " : "› ") + title;
        }
    }
}
