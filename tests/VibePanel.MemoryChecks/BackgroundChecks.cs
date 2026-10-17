// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using OpenSilver.Photino;
using Photino.NET;
using VibePanel.App;
using VibePanel.Contracts;

/// <summary>Offline output scheduling with a deliberately stalled preview and a fake engine.</summary>
internal static class BackgroundChecks
{
    internal static int Run()
    {
        var output = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../memory-checks",
            "background-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(output);
        using var lifetime = new CancellationTokenSource();
        var client = new Program.FixtureClient();
        var exitCode = 1;
        PhotinoWindow? window = null;
        window = new PhotinoWindow().SetTitle("Vibe Panel - offline background check")
            .SetLogVerbosity(0).SetSize(1100, 760)
            .ConfigureOpenSilver(() =>
            {
                var preview = new StalledPreview(() => IsWindowVisible(window!.WindowHandle) && !IsIconic(window.WindowHandle));
                var app = new App(client, presentationSettingsPath: Path.Combine(output, "settings.json"), preview: preview);
                var page = (MainPage)app.RootVisual;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ExerciseAsync(page, client, preview, window!);
                        Console.WriteLine("Background check passed: blocked UI dispatcher, palette editor, maximized window, stalled preview, hidden tray-style host, taskbar minimize, rotation, Stop, and one in-flight engine/preview frame. Fake client only; no sensors, audio or USB.");
                        exitCode = 0;
                    }
                    catch (Exception error) { Console.Error.WriteLine(error); }
                    finally { window!.Invoke(() => window.Close()); }
                });
                return app;
            }).Load(Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html"));
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(45), lifetime.Token);
                Console.Error.WriteLine("Offline background check timed out.");
                window.Invoke(() => window.Close());
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        });
        try { window.WaitForClose(); }
        finally { lifetime.Cancel(); }
        return exitCode;
    }

    private static async Task ExerciseAsync(MainPage page, Program.FixtureClient client, StalledPreview preview, PhotinoWindow window)
    {
        T Find<T>(string name) where T : class => page.FindName(name) as T ?? throw new InvalidOperationException(name);
        void Click(string name) => ((IInvokeProvider)new ButtonAutomationPeer(Find<Button>(name)).GetPattern(PatternInterface.Invoke)).Invoke();
        await WaitUntilAsync(() => client.FontRequests > 0);
        await OnPageAsync(page, () =>
        {
            Find<ComboBox>("RefreshBox").SelectedIndex = 3; // 100 ms, with no real hardware.
            Click("StartDisplayButton");
        });
        await WaitUntilAsync(() => client.PresentRequests >= 4 && preview.Calls == 1);
        var blockedAt = client.PresentRequests;
        await OnPageAsync(page, () => Thread.Sleep(1200));
        Require(client.PresentRequests >= blockedAt + 4, "Blocking the presentation dispatcher stopped output.");
        await OnPageAsync(page, () =>
        {
            Find<Slider>("BrightnessSlider").Value = 13;
            Require(page.Session.Model.Preferences.BrightnessPercent == 13,
                "A pending slider change remained in the UI instead of the model.");
            Click("PaletteButton");
            Find<TextBox>("PalettePrimary").Text = "#123456";
            Click("PaletteApply");
        });
        await WaitUntilAsync(() => client.LastPresent?.Palettes?.Values.Any(p => p.TextPrimary == "#123456") == true);
        window.Invoke(() => window.SetMaximized(true));
        blockedAt = client.PresentRequests;
        await Task.Delay(1100);
        Require(client.PresentRequests >= blockedAt + 4, "Maximized output stopped.");
        var before = client.PresentRequests;
        window.Invoke(() => ShowWindow(window.WindowHandle, 0));
        await Task.Delay(1100);
        Require(client.PresentRequests >= before + 4, "Output stopped while the host was hidden.");
        Require(preview.Calls == 1, "A second preview was queued behind the stalled bitmap.");

        var theme = client.LastPresent?.ThemeId;
        await OnPageAsync(page, () =>
        {
            Find<CheckBox>("CycleBox").IsChecked = true;
            page.Session.Model.Edit(p => p.LastChange = Environment.TickCount64 - 20001);
        });
        await WaitUntilAsync(() => client.LastPresent?.ThemeId != theme);

        // No browser preview reply for longer than PhotinoPreview's eight-second
        // deadline. Engine presents must remain independent of that deadline.
        before = client.PresentRequests;
        await Task.Delay(8500);
        Require(client.PresentRequests >= before + 20, "A stalled preview blocked background output.");
        window.Invoke(() => { ShowWindow(window.WindowHandle, 9); window.SetMinimized(false); });
        preview.Release();
        await WaitUntilAsync(() => preview.Calls >= 2);

        window.Invoke(() => window.SetMinimized(true));
        before = client.PresentRequests;
        await Task.Delay(1100);
        Require(client.PresentRequests >= before + 4, "Output stopped when minimized to the taskbar.");
        window.Invoke(() => { ShowWindow(window.WindowHandle, 9); window.SetMinimized(false); });
        await OnPageAsync(page, () => Click("StopDisplayButton"));
        await WaitUntilAsync(() => client.StopRequests == 1);
        // Let the last already-started request finish before counting Stop.
        await Task.Delay(250);
        before = client.PresentRequests;
        window.Invoke(() => ShowWindow(window.WindowHandle, 0));
        await Task.Delay(1100);
        Require(client.PresentRequests == before && client.StartRequests == 1, "Hidden output restarted after Stop.");
        Require(client.MaximumStateInFlight == 1 && preview.MaximumInFlight == 1, "Frame work overlapped.");
    }

    private static async Task OnPageAsync(MainPage page, Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        page.Dispatcher.BeginInvoke(() =>
        {
            try { action(); completion.TrySetResult(); }
            catch (Exception error) { completion.TrySetException(error); }
        });
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(8));
    }

    private static async Task WaitUntilAsync(Func<bool> ready)
    {
        var started = Stopwatch.GetTimestamp();
        while (!ready())
        {
            if (Stopwatch.GetElapsedTime(started) > TimeSpan.FromSeconds(10)) { throw new TimeoutException("Offline background fixture did not reach the expected state."); }
            await Task.Delay(50);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) { throw new InvalidOperationException(message); }
    }

    private sealed class StalledPreview(Func<bool> visible) : IStudioPreview
    {
        private readonly TaskCompletionSource<PreviewSize> first = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int inFlight, calls;
        public bool IsVisible => visible();
        public int Calls => Volatile.Read(ref calls);
        public int MaximumInFlight { get; private set; }

        public async Task<PreviewSize> ShowAsync(byte[] jpeg, bool transition)
        {
            var current = Interlocked.Increment(ref inFlight);
            MaximumInFlight = Math.Max(MaximumInFlight, current);
            try
            {
                return Interlocked.Increment(ref calls) == 1 ? await first.Task : new PreviewSize(1280, 480);
            }
            finally { Interlocked.Decrement(ref inFlight); }
        }

        public void Release() => first.TrySetResult(new(1280, 480));
        public void Clear() { if (Calls > 0) { first.TrySetCanceled(); } }
    }

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr window);
}
