// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using System.Security.Principal;

namespace VibePanel.Photino;

/// <summary>Explicit elevation for the current executable, always returning to local preview.</summary>
[SupportedOSPlatform("windows")]
internal static class WindowsElevation
{
    internal const string Argument = "--after-elevation";

    internal static bool IsAdministrator
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    internal static ProcessStartInfo CreateStartInfo(int previousId, long previousStart)
    {
        var executable = Environment.ProcessPath ?? throw new IOException("The app path is unavailable.");
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = AppContext.BaseDirectory
        };
        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        { start.ArgumentList.Add(typeof(WindowsElevation).Assembly.Location); }
        start.ArgumentList.Add(Argument);
        start.ArgumentList.Add(previousId.ToString(CultureInfo.InvariantCulture));
        start.ArgumentList.Add(previousStart.ToString(CultureInfo.InvariantCulture));
        return start;
    }

    internal static bool WaitForPreviousStudio(string[] args)
    {
        if (args.Length != 3 || args[0] != Argument || !IsAdministrator
            || !int.TryParse(args[1], CultureInfo.InvariantCulture, out var id) || id <= 0 || id == Environment.ProcessId
            || !long.TryParse(args[2], CultureInfo.InvariantCulture, out var startedAt) || startedAt <= 0) { return false; }
        try
        {
            using var previous = Process.GetProcessById(id);
            if (previous.StartTime.ToUniversalTime().Ticks != startedAt
                || !string.Equals(previous.MainModule?.FileName, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase))
            { return false; }
            // Acquire the single-instance mutex only after the former studio and its engine close.
            return previous.WaitForExit(30_000);
        }
        catch (ArgumentException) { return true; } // The validated launch can reach us after its parent exited.
        catch (InvalidOperationException) { return true; } // The retained parent exited while its metadata was being read.
        catch (Win32Exception) { return false; }
    }
}
