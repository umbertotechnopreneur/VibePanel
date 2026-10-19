// VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT. VBWR E
using System.Globalization;
using System.Management;
using Lhm = LibreHardwareMonitor.Hardware;

namespace VibePanel.Core;

public sealed partial class WindowsMetricsService
{
    private string? memoryType, memoryMetadataNotice;
    private double? memoryConfiguredSpeed;
    private IReadOnlyList<HardwareMemoryModule> memoryModules = [];

    // Static SMBIOS metadata is read once when this reader opens, never every frame.
    private void ReadMemoryConfiguration()
    {
        try
        {
            var modules = new List<HardwareMemoryModule>();
            var types = new HashSet<string>();
            var speeds = new HashSet<double>();
            using var query = new ManagementObjectSearcher("SELECT Manufacturer, PartNumber, Capacity, SMBIOSMemoryType, ConfiguredClockSpeed FROM Win32_PhysicalMemory");
            using var results = query.Get();
            foreach (ManagementObject module in results)
            {
                using (module)
                {
                    if (modules.Count >= 16) { break; }
                    var type = Convert.ToInt32(module["SMBIOSMemoryType"] ?? 0, CultureInfo.InvariantCulture) switch
                    {
                        20 => "DDR", 21 => "DDR2", 24 => "DDR3", 26 => "DDR4", 27 => "LPDDR", 28 => "LPDDR2",
                        29 => "LPDDR3", 30 => "LPDDR4", 34 => "DDR5", 35 => "LPDDR5", _ => null
                    };
                    if (type is not null) { types.Add(type); }
                    var speed = Convert.ToDouble(module["ConfiguredClockSpeed"] ?? 0, CultureInfo.InvariantCulture);
                    if (speed > 0) { speeds.Add(speed); }
                    var bytes = Convert.ToDouble(module["Capacity"] ?? 0, CultureInfo.InvariantCulture);
                    var name = string.Join(" ", new[] { module["Manufacturer"]?.ToString()?.Trim(), module["PartNumber"]?.ToString()?.Trim() }
                        .Where(value => !string.IsNullOrWhiteSpace(value) && !value.Equals("Unknown", StringComparison.OrdinalIgnoreCase)));
                    modules.Add(new(string.IsNullOrWhiteSpace(name) ? "Memory module" : Limit(name), bytes > 0 ? bytes / (1024d * 1024 * 1024) : null));
                }
            }
            memoryModules = modules;
            memoryType = types.Count == 1 ? types.Single() : types.Count > 1 ? "Mixed" : null;
            // Preserve the SMBIOS configured DDR data rate; do not infer it from SPD timing limits.
            memoryConfiguredSpeed = speeds.Count == 1 ? speeds.Single() : null;
        }
        catch (Exception error) when (IsSensorFailure(error))
        { memoryMetadataNotice = "RAM type/configured speed metadata unavailable: " + error.GetType().Name; }
    }

    private static HardwareReading[] ReadThemeValues(IEnumerable<Lhm.ISensor> sensors, int maximum = 256) => sensors.Take(maximum)
        .Select(sensor => new HardwareReading(Limit(sensor.Identifier.ToString()), Limit(sensor.Name), sensor.SensorType.ToString(),
            Finite(sensor.Value), SensorUnit(sensor.SensorType))).ToArray();
}
