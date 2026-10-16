// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using System.Drawing;
using OpenSilver.Photino;
using Photino.NET;

namespace VibePanel.Photino;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var client = new StdioPanelClient();
        var page = Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html");
        var window = new PhotinoWindow()
            .SetTitle("Vibe Panel — theme studio")
            .SetUseOsDefaultSize(false)
            .SetUseOsDefaultLocation(false)
            .SetSize(1380, 960)
            .SetResizable(true)
            .SetLogVerbosity(0)
            .SetIconFile(Path.Combine(AppContext.BaseDirectory, "Assets", OperatingSystem.IsWindows() ? "vibepanel.ico" : "vibepanel-icon.png"))
            .Center()
            .ConfigureOpenSilver(() => new App.App(client))
            .RegisterWindowCreatedHandler((sender, _) => FitToWorkArea((PhotinoWindow)sender!))
            .Load(page);
        window.WaitForClose();
    }

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
            var width = Math.Min((int)(1380 * scale), Math.Max(1, area.Width - (int)(48 * scale)));
            var height = Math.Min(Math.Max((int)(960 * scale), (int)(area.Height * .90)), Math.Max(1, area.Height - (int)(64 * scale)));
            window.SetMinSize(Math.Min((int)(1040 * scale), width), Math.Min((int)(740 * scale), height));
            window.SetSize(width, height);
            window.SetLocation(new Point(area.Left + (area.Width - width) / 2, area.Top + (area.Height - height) / 2));
        }
        catch (Exception error) when (error is ApplicationException or InvalidOperationException or ArgumentException or NotSupportedException)
        { Console.Error.WriteLine("Vibe Panel: automatic window placement unavailable."); }
    }
}
