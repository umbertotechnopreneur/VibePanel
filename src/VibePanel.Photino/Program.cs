// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using System.Drawing;
using OpenSilver.Photino;
using Photino.NET;
using VibePanel.Contracts;

namespace VibePanel.Photino;

/// <summary>Starts one studio window sized to the current monitor's work area.</summary>
internal static class Program
{
    /// <summary>Connect the local engine client and open the desktop presentation.</summary>
    [STAThread]
    private static int Main(string[] args)
    {
        if (OperatingSystem.IsWindows() && args.FirstOrDefault() == WindowsMemoryGuard.Argument)
        { return WindowsMemoryGuard.Run(args); }
        if (args.Any(argument => argument is not "--start-minimized" and not "--restore-last-session"))
        { Console.Error.WriteLine("Usage: VibePanel.exe [--start-minimized] [--restore-last-session]"); return 2; }
        var restoreLastSession = args.Contains("--restore-last-session");
        var startMinimized = args.Contains("--start-minimized") || restoreLastSession;
        using var instance = new SingleInstanceService();
        if (!instance.IsPrimary)
        {
            if (!startMinimized) { instance.RequestActivation(); }
            return 0;
        }

        using var memoryGuard = StartMemoryGuard();
        if (OperatingSystem.IsWindows() && memoryGuard is null) { return 1; }
        using var client = new StdioPanelClient();
        PhotinoWindow? window = null;
        WindowsTrayService? tray = null;
        App.App? application = null;
        PhotinoPreview? preview = null;
        var exiting = 0;
        var actions = new StudioWindowActions(
            () => OperatingSystem.IsWindows() && tray is { Ready: true },
            () => window?.Invoke(() => { if (OperatingSystem.IsWindows()) { tray?.HideWindow(); } }),
            () => window?.Invoke(() =>
            {
                if (Interlocked.Exchange(ref exiting, 1) != 0) { return; }
                instance.Dispose();
                if (OperatingSystem.IsWindows()) { tray?.Dispose(); }
                window?.Close();
            }));

        void ShowStudio()
        {
            if (Volatile.Read(ref exiting) != 0 || window is null) { return; }
            window.Invoke(() =>
            {
                if (Volatile.Read(ref exiting) != 0) { return; }
                if (OperatingSystem.IsWindows()) { tray?.ShowWindow(); }
                window.SetMinimized(false);
                CenterOnWorkArea(window);
            });
        }

        var page = Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html");
        window = new PhotinoWindow()
            .SetTitle("Vibe Panel — theme studio")
            .SetUseOsDefaultSize(false)
            .SetUseOsDefaultLocation(false)
            .SetSize(1400, 940)
            .SetResizable(true)
            .SetMinimized(startMinimized && OperatingSystem.IsWindows())
            .SetLogVerbosity(0)
            .SetIconFile(Path.Combine(AppContext.BaseDirectory, "Assets", OperatingSystem.IsWindows() ? "vibepanel.ico" : "vibepanel-icon.png"))
            .Center()
            .ConfigureOpenSilver(() => application = new App.App(client, actions, restoreLastSession, preview: preview = new PhotinoPreview(window!)))
            .RegisterWindowClosingHandler((_, _) =>
            {
                if (Volatile.Read(ref exiting) != 0 || application is null) { return false; }
                ShowStudio();
                application.RequestClose();
                return true;
            })
            .RegisterWindowCreatedHandler((sender, _) =>
            {
                var created = (PhotinoWindow)sender!;
                FitToWorkArea(created);
                if (OperatingSystem.IsWindows())
                {
                    try
                    {
                        tray = new WindowsTrayService(created.WindowHandle, Path.Combine(AppContext.BaseDirectory, "Assets", "vibepanel.ico"),
                            ShowStudio,
                            () => { if (application is not null) { application.RequestExit(); } else { actions.CloseApplication(); } },
                            showAbout: () => { ShowStudio(); application?.RequestAbout(); },
                            openManifesto: () => OpenWebsite("https://umbertogiacobbi.biz/vibeware/manifesto"),
                            openWebsite: () => OpenWebsite("https://umbertogiacobbi.biz"),
                            openGitHub: () => OpenWebsite("https://github.com/umbertotechnopreneur/VibePanel"));
                        tray.Error += error => Console.Error.WriteLine("Vibe Panel tray: " + error.Message);
                        if (startMinimized) { tray.HideWindow(); }
                    }
                    catch (Exception error) when (error is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
                    { Console.Error.WriteLine("Vibe Panel tray: " + error.Message); created.SetMinimized(false); }
                }
                instance.Listen(action =>
                {
                    if (Volatile.Read(ref exiting) == 0) { created.Invoke(action); }
                }, ShowStudio);
            })
            .Load(page);
        try { window.WaitForClose(); }
        finally
        {
            Interlocked.Exchange(ref exiting, 1);
            preview?.Dispose();
            if (OperatingSystem.IsWindows()) { tray?.Dispose(); }
        }
        return 0;
    }

    private static System.Diagnostics.Process? StartMemoryGuard()
    {
        if (!OperatingSystem.IsWindows()) { return null; }
        try { return WindowsMemoryGuard.Start(); }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or IOException or InvalidOperationException or OperationCanceledException)
        { WindowsMemoryGuard.ShowUnavailable(); return null; }
    }

    /// <summary>Reopen an existing studio centered without discarding its chosen size.</summary>
    private static void CenterOnWorkArea(PhotinoWindow window)
    {
        var monitors = window.Monitors;
        if (monitors.Count == 0) { return; }
        var center = new Point(window.Location.X + window.Size.Width / 2, window.Location.Y + window.Size.Height / 2);
        var monitor = monitors[0];
        foreach (var candidate in monitors)
        {
            if (candidate.MonitorArea.Contains(center)) { monitor = candidate; break; }
        }
        var area = monitor.WorkArea;
        if (area.Width <= 0 || area.Height <= 0) { return; }
        window.SetLocation(new Point(area.Left + Math.Max(0, (area.Width - window.Size.Width) / 2),
            area.Top + Math.Max(0, (area.Height - window.Size.Height) / 2)));
    }

    private sealed class StudioWindowActions(Func<bool> canMinimize, Action minimize, Action close) : IStudioWindowActions
    {
        public bool CanMinimizeToTray => canMinimize();
        public void MinimizeToTray() => minimize();
        public void CloseApplication() => close();
    }

    private static void OpenWebsite(string url)
    {
        using var browser = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
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
