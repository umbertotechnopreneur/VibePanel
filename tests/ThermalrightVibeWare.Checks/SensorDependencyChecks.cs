// VBWR B
// Project: ThermalrightVibeWare | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using System.Reflection;
using System.Runtime.Versioning;

internal static class SensorDependencyChecks
{
    internal static void Run(Action<bool, string> check)
    {
        // Read assembly metadata/resources only. Do not construct Computer or open hardware.
        var assembly = typeof(LibreHardwareMonitor.Hardware.Computer).Assembly;
        check(assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName
            == ".NETCoreApp,Version=v10.0", "the official sensor package supplies its compiled .NET 10 library");
        check(assembly.GetName().Name == "LibreHardwareMonitorLib" && assembly.GetName().Version is { Major: 0, Minor: 9, Build: 6 },
            "the selected sensor dependency is Libre Hardware Monitor 0.9.6");
        check(!assembly.GetManifestResourceNames().Any(name => name.EndsWith(".sys", StringComparison.OrdinalIgnoreCase)
            || name.Contains("WinRing0", StringComparison.OrdinalIgnoreCase)),
            "the selected sensor library embeds no WinRing0 driver binary");
    }
}
