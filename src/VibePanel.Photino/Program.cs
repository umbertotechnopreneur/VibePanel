// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using System.Drawing;
using OpenSilver.Photino;
using Photino.NET;

namespace VibePanel.Photino;

/// <summary>Starts one studio window sized to the current monitor's work area.</summary>
internal static class Program
{
    /// <summary>Connect the local engine client and open the desktop presentation.</summary>
    [STAThread]
    private static void Main()
    {
        using var client = new StdioPanelClient();
        var page = Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html");
        var window = new PhotinoWindow()
            .SetTitle("Vibe Panel — theme studio")
            .SetUseOsDefaultSize(false)
            .SetUseOsDefaultLocation(false)
            .SetSize(1400, 940)
            .SetResizable(true)
            .SetLogVerbosity(0)
            .SetIconFile(Path.Combine(AppContext.BaseDirectory, "Assets", OperatingSystem.IsWindows() ? "vibepanel.ico" : "vibepanel-icon.png"))
            .Center()
            .ConfigureOpenSilver(() => new App.App(client))
            .RegisterWindowCreatedHandler((sender, _) => FitToWorkArea((PhotinoWindow)sender!))
            .Load(page);
        window.WaitForClose();
    }

    /// <summary>Choose the initial window size once, while respecting display scaling and taskbars.</summary>
    private static void FitToWorkArea(PhotinoWindow window)
    {
        // Monitor information is available only after Photino creates its native window.
        // Run once at startup, never recenter while the user moves or resizes the window.
        try
        {
            var monitors = window.Monitors;
            if (monitors.Count == 0) { return; }
            var currentCenter = new Point(window.Location.X + window.Size.Width / 2, window.Location.Y + window.Size.Height / 2);
            var monitor = monitors[0];
            foreach (var candidate in monitors)
            {
                if (candidate.MonitorArea.Contains(currentCenter)) { monitor = candidate; break; }
            }
            var area = monitor.WorkArea;
            if (area.Width <= 0 || area.Height <= 0) { return; }
            // Windows reports physical pixels: preserve usable space at larger display scaling too.
            var scale = OperatingSystem.IsWindows() ? Math.Clamp(window.ScreenDpi / 96d, 1, 3) : 1;
            // Cap the preferred size, rather than making a large screen fill up to 90%.
            var width = Math.Max(1, Math.Min((int)(1400 * scale), (int)(area.Width * .92)));
            var height = Math.Max(1, Math.Min((int)(940 * scale), (int)(area.Height * .92)));
            window.SetMinSize(Math.Min((int)(1040 * scale), width), Math.Min((int)(640 * scale), height));
            window.SetSize(width, height);
            window.SetLocation(new Point(area.Left + (area.Width - width) / 2, area.Top + (area.Height - height) / 2));
        }
        catch (Exception error) when (error is ApplicationException or InvalidOperationException or ArgumentException or NotSupportedException)
        { Console.Error.WriteLine("Vibe Panel: automatic window placement unavailable."); }
    }
}
