// VBWR B — Thermalright VibeWare. Copyright (c) 2026 Umberto Giacobbi. MIT. VBWR E
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using ThermalrightVibeWare.Contracts;

namespace ThermalrightVibeWare.App;

public sealed partial class MainPage
{
    private bool sensorDriverReading;
    private string? sensorDriverActionNotice;

    private async Task RefreshSensorDriverStatusAsync()
    {
        if (!ready || sensorDriverReading) { return; }
        sensorDriverReading = true;
        try { ApplySensorDriverStatus(await viewModel.ReadSensorDriverAsync()); }
        catch (Exception error)
        {
            SensorDriverText.Text = "Sensor driver status could not be read: " + error.Message;
            SensorSummaryText.Text = "Access check unavailable";
            SensorDriverFolderButton.IsEnabled = false;
        }
        finally { sensorDriverReading = false; }
    }

    private void ApplySensorDriverStatus(SensorDriverDto driver)
    {
        SensorSummaryText.Text = sensorDriverActionNotice is not null ? "Driver folder unavailable"
            : windowActions?.CanRestartAsAdministrator == true ? "Administrator access needed"
            : driver.Available ? "PawnIO found" : "PawnIO unavailable · see details";
        SensorDriverText.Text = sensorDriverActionNotice ?? driver.Notice ?? (driver.Available
            ? "PawnIO installation found. Individual sensor readings show what this computer exposes."
            : "PawnIO is unavailable. CPU temperature and motherboard/fan readings may be missing.");
        var path = driver.Path ?? "";
        if (SensorDriverPath.Text != path) { SensorDriverPath.Text = path; }
        SensorDriverPath.Visibility = path.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        SensorDriverFolderButton.IsEnabled = path.Length > 0;
        ToolTipService.SetToolTip(SensorDriverPath, driver.FileExists ? path : path + " (file not present)");
        ToolTipService.SetToolTip(SensorDriverText, SensorDriverText.Text);
    }

    private async void SensorDriverFolder_Click(object sender, RoutedEventArgs args)
    {
        SensorDriverFolderButton.IsEnabled = false;
        sensorDriverActionNotice = null;
        try
        {
            var result = await viewModel.OpenSensorDriverFolderAsync();
            if (!result.TryGetProperty("accepted", out var accepted) || accepted.ValueKind != JsonValueKind.True)
            { sensorDriverActionNotice = "The driver location is not available. No folder was opened; nothing was installed or allowed."; }
        }
        catch (Exception error) { sensorDriverActionNotice = "Could not open the driver folder: " + error.Message; }
        finally { await RefreshSensorDriverStatusAsync(); }
    }
}
