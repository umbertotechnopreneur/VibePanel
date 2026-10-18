// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using System.Diagnostics;
using System.Runtime.InteropServices;
using Ohm = OpenHardwareMonitor.Hardware;

namespace VibePanel.Core;

/// <summary>
/// Reads CPU, GPU and physical RAM through the original Open Hardware Monitor library only.
/// Construction does not open hardware. The library's driver lifecycle requires explicit consent.
/// </summary>
public sealed class WindowsMetricsService(bool allowDriverLifecycle = false) : IMetricsProvider
{
    private const int MaximumHardware = 64;
    private const int MaximumDepth = 8;
    private const int MaximumSensorsPerDevice = 1024;
    private const int MaximumGpus = 8;
    private readonly object sync = new();
    private Ohm.Computer? computer;
    private HardwareSensorSnapshot snapshot = HardwareSensorSnapshot.Empty;
    private bool initializationAttempted, disposed;
    private long lastRead;

    /// <summary>Local failure details, printed only by the explicitly requested CLI diagnostic command.</summary>
    public string? InitializationFailureDetails { get; private set; }

    /// <summary>Returns the last sample for one second, using monotonic time and no background worker.</summary>
    public HardwareSensorSnapshot ReadSensors()
    {
        lock (sync)
        {
            if (disposed) { return HardwareSensorSnapshot.Empty; }
            var now = Stopwatch.GetTimestamp();
            if (lastRead != 0 && Stopwatch.GetElapsedTime(lastRead, now) < TimeSpan.FromSeconds(1))
            {
                return snapshot;
            }
            lastRead = now;
            if (!EnsureComputer()) { return snapshot; }

            try
            {
                HardwareDeviceSensors? cpu = null;
                HardwareMemorySensors? memory = null;
                var gpus = new List<HardwareDeviceSensors>();
                var failed = false;
                foreach (var hardware in EnumerateHardware(computer!.Hardware))
                {
                    try
                    {
                        hardware.Update();
                        var sensors = hardware.Sensors.Take(MaximumSensorsPerDevice).ToArray();
                        switch (hardware.HardwareType)
                        {
                            case Ohm.HardwareType.CPU when cpu is null:
                                cpu = ReadCpu(hardware.Name, sensors);
                                break;
                            case Ohm.HardwareType.GpuNvidia or Ohm.HardwareType.GpuAti when gpus.Count < MaximumGpus:
                                gpus.Add(ReadGpu(hardware.Name, sensors));
                                break;
                            case Ohm.HardwareType.RAM when memory is null:
                                memory = ReadMemory(sensors);
                                break;
                        }
                    }
                    catch (Exception error) when (IsSensorFailure(error)) { failed = true; }
                }
                snapshot = new(cpu, gpus.ToArray(), memory, DateTimeOffset.UtcNow,
                    failed ? "Some Open Hardware Monitor measurements are unavailable." : null);
            }
            catch (Exception error) when (IsSensorFailure(error))
            {
                // Clear a failed sample rather than presenting our previous sample as current.
                snapshot = HardwareSensorSnapshot.Empty with { Notice = "Open Hardware Monitor readings are unavailable." };
            }
            lastRead = Stopwatch.GetTimestamp();
            return snapshot;
        }
    }

    /// <summary>Preserves the legacy scene contract, derived from the same cached library sample.</summary>
    public MachineMetrics Read()
    {
        var sensors = ReadSensors();
        var gpu = sensors.Gpus.FirstOrDefault();
        return new(sensors.Cpu?.UsagePercent, sensors.Cpu?.TemperatureC, gpu?.UsagePercent, gpu?.TemperatureC,
            gpu?.Name ?? "GPU",
            sensors.Cpu?.TemperatureC is null ? "Unavailable" : $"Open Hardware Monitor: {sensors.Cpu.TemperatureLabel}",
            gpu is null ? "Unavailable" : "Open Hardware Monitor (first detected GPU)");
    }

    private bool EnsureComputer()
    {
        if (computer is not null) { return true; }
        if (initializationAttempted) { return false; }
        initializationAttempted = true;
        if (!System.OperatingSystem.IsWindows())
        {
            snapshot = HardwareSensorSnapshot.Empty with { Notice = "Hardware sensors are not implemented for this system." };
            return false;
        }
        if (RuntimeInformation.OSArchitecture is not (Architecture.X64 or Architecture.X86)
            || RuntimeInformation.ProcessArchitecture is not (Architecture.X64 or Architecture.X86))
        {
            snapshot = HardwareSensorSnapshot.Empty with { Notice = "Open Hardware Monitor 0.9.6 does not support Windows ARM64." };
            return false;
        }
        if (!allowDriverLifecycle)
        {
            // Stock OHM has no public option to prevent WinRing0 installation or service removal.
            // Holding a preflight device handle also blocks its exclusive open and can trigger installation.
            snapshot = HardwareSensorSnapshot.Empty with
            {
                Notice = "Hardware sensors are disabled: Open Hardware Monitor's driver lifecycle has not been authorized."
            };
            return false;
        }

        Ohm.Computer? candidate = null;
        try
        {
            candidate = new Ohm.Computer { CPUEnabled = true, GPUEnabled = true, RAMEnabled = true };
            // This library may install/load WinRing0 internally. Only the explicit constructor opt-in permits this call.
            candidate.Open();
            computer = candidate;
            return true;
        }
        catch (Exception error) when (IsSensorFailure(error))
        {
            InitializationFailureDetails = error.ToString();
            try { candidate?.Close(); }
            catch (Exception closeError) when (IsSensorFailure(closeError)) { }
            snapshot = HardwareSensorSnapshot.Empty with
            {
                Notice = $"Open Hardware Monitor could not start ({error.GetBaseException().GetType().Name}). Run 'vp sensors --diagnose' for details."
            };
            return false;
        }
    }

    private static HardwareDeviceSensors ReadCpu(string name, Ohm.ISensor[] sensors)
    {
        var usage = FindValue(sensors, Ohm.SensorType.Load, "CPU Total", percentage: true);
        var clocks = sensors.Where(sensor => sensor.SensorType == Ohm.SensorType.Clock
            && sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase)
            && !sensor.Name.Contains("Bus", StringComparison.OrdinalIgnoreCase))
            .Select(sensor => Value(sensor, nonnegative: true)).OfType<double>().ToArray();
        var temperatures = sensors.Where(sensor => sensor.SensorType == Ohm.SensorType.Temperature)
            .Select(sensor => (Sensor: sensor, Value: Value(sensor))).Where(item => item.Value is not null).ToArray();
        var package = temperatures.FirstOrDefault(item => item.Sensor.Name.Equals("CPU Package", StringComparison.OrdinalIgnoreCase));
        var temperature = package.Value;
        var temperatureLabel = temperature is not null ? "CPU package" : null;
        if (temperature is null && temperatures.Length > 0)
        {
            temperature = temperatures.Max(item => item.Value);
            temperatureLabel = "Hottest CPU sensor";
        }
        return new(name, usage, clocks.Length == 0 ? null : clocks.Average(), temperature,
            clocks.Length == 0 ? null : "Average clock", temperatureLabel);
    }

    private static HardwareDeviceSensors ReadGpu(string name, Ohm.ISensor[] sensors) => new(name,
        FindValue(sensors, Ohm.SensorType.Load, "GPU Core", percentage: true),
        FindValue(sensors, Ohm.SensorType.Clock, "GPU Core", nonnegative: true),
        FindValue(sensors, Ohm.SensorType.Temperature, "GPU Core"), "Core clock", "GPU core");

    private static HardwareMemorySensors? ReadMemory(Ohm.ISensor[] sensors)
    {
        // Original OHM's Data values are GiB, despite its GB display label.
        var used = FindValue(sensors, Ohm.SensorType.Data, "Used Memory", nonnegative: true);
        var available = FindValue(sensors, Ohm.SensorType.Data, "Available Memory", nonnegative: true);
        var usage = FindValue(sensors, Ohm.SensorType.Load, "Memory", percentage: true);
        double? total = used is not null && available is not null ? used + available : null;
        return total is null && used is null && usage is null ? null : new(total, used, usage);
    }

    private static double? FindValue(IEnumerable<Ohm.ISensor> sensors, Ohm.SensorType type, string name,
        bool percentage = false, bool nonnegative = false) => sensors
        .Where(sensor => sensor.SensorType == type && sensor.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
        .Select(sensor => Value(sensor, percentage, nonnegative)).FirstOrDefault(value => value is not null);

    private static double? Value(Ohm.ISensor sensor, bool percentage = false, bool nonnegative = false)
    {
        if (sensor.Value is not float value || !float.IsFinite(value)) { return null; }
        if (percentage && value is < 0 or > 100 || nonnegative && value < 0) { return null; }
        return value;
    }

    private static IEnumerable<Ohm.IHardware> EnumerateHardware(IEnumerable<Ohm.IHardware> roots)
    {
        var pending = new Queue<(Ohm.IHardware Hardware, int Depth)>();
        foreach (var root in roots.Take(MaximumHardware)) { pending.Enqueue((root, 0)); }
        var visited = new HashSet<Ohm.IHardware>(ReferenceEqualityComparer.Instance);
        while (pending.Count > 0 && visited.Count < MaximumHardware)
        {
            var (hardware, depth) = pending.Dequeue();
            if (!visited.Add(hardware)) { continue; }
            yield return hardware;
            if (depth >= MaximumDepth) { continue; }
            foreach (var child in hardware.SubHardware.Take(MaximumHardware - visited.Count - pending.Count))
            {
                pending.Enqueue((child, depth + 1));
            }
        }
    }

    private static bool IsSensorFailure(Exception error) => error is not (OutOfMemoryException or StackOverflowException or AccessViolationException);

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed) { return; }
            disposed = true;
            try { computer?.Close(); }
            catch (Exception error) when (IsSensorFailure(error)) { }
            finally { computer = null; snapshot = HardwareSensorSnapshot.Empty; }
        }
    }
}
