// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

namespace VibePanel.Core;

/// <summary>One bounded sensor sample. Missing measurements stay null; zero is a valid measurement.</summary>
public sealed record HardwareSensorSnapshot(HardwareDeviceSensors? Cpu, IReadOnlyList<HardwareDeviceSensors> Gpus,
    HardwareMemorySensors? Memory, DateTimeOffset? UpdatedAtUtc, string? Notice = null)
{
    public static HardwareSensorSnapshot Empty { get; } = new(null, [], null, null);
}

/// <summary>Measurements supplied by the selected hardware library, in percent, MHz, and degrees Celsius.</summary>
public sealed record HardwareDeviceSensors(string Name, double? UsagePercent, double? ClockMHz, double? TemperatureC,
    string? ClockLabel = null, string? TemperatureLabel = null);

/// <summary>Physical memory in GiB (2^30 bytes); excludes swap and committed virtual memory.</summary>
public sealed record HardwareMemorySensors(double? TotalGiB, double? UsedGiB, double? UsagePercent);
