// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using System.Management;
using System.Runtime.InteropServices;
using System.Text;

namespace VibePanel.Core;

/// <summary>Reads existing Windows/NVIDIA facilities; never installs a sensor driver.</summary>
public sealed class WindowsMetricsService : IDisposable
{
    private ulong previousIdle, previousTotal;
    private DateTime nextSensorRead;
    private double? cpuTemperature;
    private string cpuSource = "No existing sensor provider";
    private bool nvmlInitialized, nvmlUnavailable;

    public MachineMetrics Read()
    {
        double? cpu = null;
        if (GetSystemTimes(out var idle, out var kernel, out var user))
        {
            var total = kernel + user;
            if (previousTotal != 0 && total > previousTotal)
            {
                cpu = Math.Clamp(100.0 * (1 - (double)(idle - previousIdle) / (total - previousTotal)), 0, 100);
            }
            previousIdle = idle;
            previousTotal = total;
        }
        if (DateTime.UtcNow >= nextSensorRead)
        {
            nextSensorRead = DateTime.UtcNow.AddSeconds(30);
            ReadExistingCpuSensor();
        }
        double? gpu = null, temperature = null;
        var gpuName = "GPU";
        var gpuSource = "No supported driver sensor";
        if (!nvmlUnavailable)
        {
            try
            {
                if (!nvmlInitialized) { nvmlInitialized = nvmlInit_v2() == 0; }
                if (nvmlInitialized && nvmlDeviceGetHandleByIndex_v2(0, out var device) == 0)
                {
                    var name = new StringBuilder(96);
                    if (nvmlDeviceGetName(device, name, 96) == 0) { gpuName = name.ToString(); }
                    if (nvmlDeviceGetUtilizationRates(device, out var utilization) == 0) { gpu = utilization.Gpu; }
                    if (nvmlDeviceGetTemperature(device, 0, out var degrees) == 0) { temperature = degrees; }
                    gpuSource = "NVIDIA driver / NVML (GPU 0)";
                }
                else { nvmlUnavailable = true; }
            }
            catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
            { nvmlUnavailable = true; }
        }
        return new(cpu, cpuTemperature, gpu, temperature, gpuName, cpuSource, gpuSource);
    }

    private void ReadExistingCpuSensor()
    {
        cpuTemperature = null;
        cpuSource = "No existing sensor provider";
        foreach (var provider in new[] { "LibreHardwareMonitor", "OpenHardwareMonitor" })
        {
            try
            {
                var options = new System.Management.EnumerationOptions { Timeout = TimeSpan.FromSeconds(1), ReturnImmediately = false };
                using var query = new ManagementObjectSearcher(new ManagementScope($"root\\{provider}"), new ObjectQuery("SELECT Name, Value FROM Sensor WHERE SensorType = 'Temperature'"), options);
                using var sensors = query.Get();
                foreach (ManagementObject sensor in sensors)
                {
                    using (sensor)
                    {
                        var name = sensor["Name"]?.ToString() ?? "";
                        if (!name.Contains("CPU", StringComparison.OrdinalIgnoreCase)) { continue; }
                        var value = Convert.ToDouble(sensor["Value"], System.Globalization.CultureInfo.InvariantCulture);
                        if (value is > 0 and < 150 && (cpuTemperature is null || value > cpuTemperature))
                        {
                            cpuTemperature = value;
                            cpuSource = $"{provider}: hottest CPU sensor";
                        }
                    }
                }
                if (cpuTemperature is not null) { return; }
            }
            catch (Exception error) when (error is ManagementException or COMException or UnauthorizedAccessException) { }
        }
    }

    public void Dispose()
    {
        if (nvmlInitialized) { _ = nvmlShutdown(); nvmlInitialized = false; }
    }

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);

    [StructLayout(LayoutKind.Sequential)]
    private struct Utilization { public uint Gpu; public uint Memory; }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlInit_v2();
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlShutdown();
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceGetHandleByIndex_v2(uint index, out nint device);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern int nvmlDeviceGetName(nint device, StringBuilder name, uint length);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceGetTemperature(nint device, uint sensor, out uint temperature);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceGetUtilizationRates(nint device, out Utilization utilization);
}
