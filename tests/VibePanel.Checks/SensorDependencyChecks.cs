// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using System.Reflection;
using System.Runtime.Versioning;
using System.Security.Cryptography;

internal static class SensorDependencyChecks
{
    internal static void Run(Action<bool, string> check)
    {
        // Read assembly metadata/resources only. Do not construct Computer or open hardware.
        var assembly = typeof(OpenHardwareMonitor.Hardware.Computer).Assembly;
        check(assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName
            == ".NETCoreApp,Version=v10.0", "sensor library targets .NET 10 rather than the incompatible net45 binary");
        VerifyResource("WinRing0.sys", "206EE7A7C3F4D9496F742CCB84718F556ECB4BA2A95FE7E0CDF3A003FFBE4597");
        VerifyResource("WinRing0x64.sys", "11BD2C9F9E2397C9A16E0990E4ED2CF0679498FE0FD418A3DFDAC60B5C160EE5");

        void VerifyResource(string name, string expectedHash)
        {
            using var stream = assembly.GetManifestResourceStream("OpenHardwareMonitor.Hardware." + name);
            check(stream is not null && Convert.ToHexString(SHA256.HashData(stream)) == expectedHash,
                $"original {name} bytes remain embedded without changes or execution");
        }
    }
}
