// VBWR B — Thermalright VibeWare. Copyright (c) 2026 Umberto Giacobbi. MIT. VBWR E
using Lhm = LibreHardwareMonitor.Hardware;

namespace ThermalrightVibeWare.Core;

public sealed partial class WindowsMetricsService
{
    internal static IReadOnlyList<HardwareReading> ProjectMotherboardReadings(
        Lhm.Motherboard.Manufacturer manufacturer, Lhm.Motherboard.Model model,
        string hardwareId, IReadOnlyList<HardwareReading> readings)
    {
        if (manufacturer != Lhm.Motherboard.Manufacturer.ASRock || model != Lhm.Motherboard.Model.B550M_ITX_AC
            || hardwareId != "/lpc/nct6792d/0")
        { return readings; }

        // Owner-approved display labels for this board and chip instance only.
        // Raw library sensors remain intact for Sensor Debug; no calibration is applied.
        var projected = new List<HardwareReading>(readings.Count);
        foreach (var reading in readings)
        {
            if (reading.Type != "Temperature")
            {
                projected.Add(reading);
                continue;
            }
            var label = reading.Id switch
            {
                "/lpc/nct6792d/0/temperature/1" => "Mainboard",
                "/lpc/nct6792d/0/temperature/3" => "Southbridge",
                "/lpc/nct6792d/0/temperature/17" => "CPU",
                _ => null
            };
            // Unidentified inputs stay in diagnostics, regardless of their numeric value.
            if (label is not null) { projected.Add(reading with { Name = label }); }
        }
        return projected.ToArray();
    }
}
