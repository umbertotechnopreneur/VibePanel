// VBWR B
// Project: ThermalrightVibeWare | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows.Threading;
using OpenSilver.Photino;
using Photino.NET;
using ThermalrightVibeWare.Photino;

/// <summary>Opt-in restoration check with a fake engine, isolated settings and no USB backend.</summary>
internal static class VisibilityChecks
{
    internal static int Run(bool capture)
    {
        var output = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../memory-checks", "visibility-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(output);
        using var guard = WindowsMemoryGuard.Start();
        using var lifetime = new CancellationTokenSource();
        var client = new Program.FixtureClient();
        var exitCode = 1;
        var started = 0;
        var startupRestored = 0;
        PhotinoWindow? window = null;
        PhotinoPreview? preview = null;
        window = new PhotinoWindow().SetTitle("Thermalright VibeWare - offline visibility check")
            .SetLogVerbosity(0).SetSize(1100, 760).SetMinimized(true)
            .RegisterWindowCreatedHandler((sender, eventArgs) =>
            {
                var created = (PhotinoWindow)sender!;
                ShowWindow(created.WindowHandle, 0);
                // Restore independently of OpenSilver/frame callbacks: a hidden WebView
                // may throttle those callbacks before the first frame is available.
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(1000, lifetime.Token);
                        created.Invoke(() =>
                        {
                            CheckHidden(created);
                            ShowWindow(created.WindowHandle, 9);
                            created.SetMinimized(false);
                            Volatile.Write(ref startupRestored, 1);
                        });
                    }
                    catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
                    catch (Exception error) { Console.Error.WriteLine(error); created.Invoke(() => created.Close()); }
                });
            })
            .ConfigureOpenSilver(() =>
            {
                preview = new PhotinoPreview(window!);
                var app = new ThermalrightVibeWare.App.App(client, presentationSettingsPath: Path.Combine(output, "settings.json"), preview: preview, restoreLastSession: false);
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
                timer.Tick += async (_, _) =>
                {
                    if (Volatile.Read(ref startupRestored) == 0 || client.Frames == 0 || Interlocked.Exchange(ref started, 1) != 0) { return; }
                    timer.Stop();
                    try
                    {
                        await ExerciseAsync(window!, output, capture);
                        if (client.StartRequests != 0 || client.PresentRequests != 0)
                        { throw new InvalidOperationException("The preview-only fixture requested display output."); }
                        Console.WriteLine("Visibility check passed: hidden startup, tray-style restore and minimize/restore. Only a fake client was used; no engine, sensors or USB backend ran.");
                        exitCode = 0;
                    }
                    catch (Exception error) { Console.Error.WriteLine(error); }
                    finally { window!.Invoke(() => window.Close()); }
                };
                timer.Start();
                return app;
            }).Load(Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html"));
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), lifetime.Token);
                Console.Error.WriteLine("Offline visibility check timed out.");
                window.Invoke(() => window.Close());
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        });
        try { window.WaitForClose(); }
        finally { lifetime.Cancel(); preview?.Dispose(); }
        return exitCode;
    }

    private static async Task ExerciseAsync(PhotinoWindow window, string output, bool capture)
    {
        void Restore() => window.Invoke(() => { ShowWindow(window.WindowHandle, 9); window.SetMinimized(false); });
        await CheckVisibleAsync(window, output, "startup-restored", capture);
        window.Invoke(() => ShowWindow(window.WindowHandle, 0));
        CheckHidden(window);
        Restore();
        await CheckVisibleAsync(window, output, "tray-restored", capture);
        window.Invoke(() => window.SetMinimized(true));
        CheckHidden(window);
        Restore();
        await CheckVisibleAsync(window, output, "minimize-restored", capture);
    }

    private static void CheckHidden(PhotinoWindow window)
    {
        if (IsWindowVisible(window.WindowHandle) && !IsIconic(window.WindowHandle))
        { throw new InvalidOperationException("The fixture window did not hide or minimize."); }
        if (Children(window.WindowHandle).Any(child => IsBrowserWindow(child) && child.Visible))
        { throw new InvalidOperationException("The WebView remained visible while its host was hidden."); }
    }

    private static bool IsBrowserWindow(ChildWindow child) => child.ClassName.StartsWith("Chrome_WidgetWin_", StringComparison.Ordinal)
        || child.ClassName == "Chrome_RenderWidgetHostHWND";

    private static async Task CheckVisibleAsync(PhotinoWindow window, string output, string phase, bool capture)
    {
        // Let the compositor draw after the native restore; no live provider is involved.
        await Task.Delay(350);
        var children = Children(window.WindowHandle);
        File.WriteAllText(Path.Combine(output, phase + ".json"), JsonSerializer.Serialize(children));
        if (!IsWindowVisible(window.WindowHandle) || IsIconic(window.WindowHandle)
            || !children.Any(child => child.ClassName.StartsWith("Chrome_WidgetWin_", StringComparison.Ordinal) && child.Visible && child.Width > 0 && child.Height > 0)
            || !children.Any(child => child.ClassName == "Chrome_RenderWidgetHostHWND" && child.Visible && child.Width > 0 && child.Height > 0))
        { throw new InvalidOperationException("The restored host has no visible WebView renderer: " + phase); }
        var contentVisible = OpenSilver.Interop.ExecuteJavaScriptGetResult<bool>(
            "(()=>{const root=document.getElementById('opensilver-root'); const r=root?.getBoundingClientRect();"
            + "return !!(r&&r.width>0&&r.height>0&&root.innerText.includes('Thermalright VibeWare'));})()");
        if (!contentVisible) { throw new InvalidOperationException("The restored WebView has no studio content: " + phase); }
        if (capture)
        {
            var pointer = (IntPtr)typeof(PhotinoWindow).GetField("_nativeInstance", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
            var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            CaptureCompleted done = result => completion.TrySetResult(result);
            var file = Path.Combine(output, phase + ".png");
            window.Invoke(() => Marshal.ThrowExceptionForHR(ThermalrightVibeWare_CapturePreview(pointer, file, done)));
            Marshal.ThrowExceptionForHR(await completion.Task.WaitAsync(TimeSpan.FromSeconds(5)));
            GC.KeepAlive(done);
            Console.WriteLine("Restored studio capture: " + file);
        }
    }

    private static List<ChildWindow> Children(IntPtr parent)
    {
        var children = new List<ChildWindow>();
        EnumCallback read = (window, data) =>
        {
            var name = new StringBuilder(256);
            _ = GetClassName(window, name, name.Capacity);
            _ = GetClientRect(window, out var bounds);
            children.Add(new(name.ToString(), IsWindowVisible(window), bounds.Right - bounds.Left, bounds.Bottom - bounds.Top));
            return true;
        };
        _ = EnumChildWindows(parent, read, IntPtr.Zero);
        GC.KeepAlive(read);
        return children;
    }

    private sealed record ChildWindow(string ClassName, bool Visible, int Width, int Height);
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
    private delegate bool EnumCallback(IntPtr window, IntPtr data);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void CaptureCompleted(int result);
    [DllImport("Photino.Native.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
    private static extern int ThermalrightVibeWare_CapturePreview(IntPtr instance, string path, CaptureCompleted completed);
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr parent, EnumCallback callback, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr window, StringBuilder name, int capacity);
    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr window, out NativeRect bounds);
}
