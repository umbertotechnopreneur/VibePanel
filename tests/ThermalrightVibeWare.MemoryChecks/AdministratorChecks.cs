// VBWR B
// Project: ThermalrightVibeWare | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using OpenSilver.Photino;
using Photino.NET;
using ThermalrightVibeWare.App;
using ThermalrightVibeWare.Contracts;
using ThermalrightVibeWare.Photino;

internal static class AdministratorChecks
{
    private static string HandoffPath => Path.Combine(AppContext.BaseDirectory, "elevation-handoff.json");

    // Separate opt-in: launches an elevated fixture, never an engine or a sensor reader.
    internal static int StartHandoff()
    {
        using var guard = WindowsMemoryGuard.Start();
        File.WriteAllText(HandoffPath, JsonSerializer.Serialize(new { stage = "waiting", parentId = Environment.ProcessId }));
        return guard.RestartAsAdministratorAsync().GetAwaiter().GetResult() ? 0 : 1;
    }

    internal static int FinishHandoff(string[] args)
    {
        if (!WindowsElevation.WaitForPreviousStudio(args)) { return 2; }
        using var guard = WindowsMemoryGuard.Start();
        // The old guard must have time to close its old process-family job.
        Thread.Sleep(2000);
        File.WriteAllText(HandoffPath, JsonSerializer.Serialize(new
        { stage = "complete", parentId = int.Parse(args[1]), childId = Environment.ProcessId, elevated = WindowsElevation.IsAdministrator, guarded = true }));
        return 0;
    }

    internal static int Run()
    {
        using var guard = WindowsMemoryGuard.Start();
        var settings = Path.Combine(AppContext.BaseDirectory, "administrator-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(settings, "{\"category\":\"system\",\"themeId\":\"fixture-20\"}");
        var client = new Program.FixtureClient { IncludeSystem = true };
        var actions = new RestartActions();
        PhotinoWindow? window = null;
        PhotinoPreview? preview = null;
        var exitCode = 1;
        var started = 0;
        window = new PhotinoWindow().SetTitle("Thermalright VibeWare - offline administrator checks")
            .SetLogVerbosity(0).SetSize(1200, 850)
            .ConfigureOpenSilver(() =>
            {
                preview = new PhotinoPreview(window!);
                var app = new App(client, actions, settings, preview);
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
                timer.Tick += async (_, _) =>
                {
                    if (client.FontRequests == 0 || client.Frames == 0 || Interlocked.Exchange(ref started, 1) != 0) { return; }
                    timer.Stop();
                    try { await ExerciseAsync((MainPage)app.RootVisual, actions, client, settings); exitCode = 0; }
                    catch (Exception error) { Console.Error.WriteLine(error); }
                    finally { window!.Invoke(() => window.Close()); }
                };
                timer.Start();
                return app;
            }).Load(Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html"));
        using var lifetime = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            try { await Task.Delay(30_000, lifetime.Token); window.Invoke(() => window.Close()); }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        });
        try { window.WaitForClose(); }
        finally { lifetime.Cancel(); preview?.Dispose(); }
        return exitCode;
    }

    private static async Task ExerciseAsync(MainPage page, RestartActions actions, Program.FixtureClient client, string settings)
    {
        var count = 0;
        void Check(bool value, string text)
        {
            if (!value) { throw new InvalidOperationException(text); }
            Console.WriteLine($"Administrator check {++count}: {text}");
        }
        var panel = (System.Windows.FrameworkElement)page.FindName("AdministratorPanel");
        var button = (Button)page.FindName("AdministratorButton");
        var details = (System.Windows.FrameworkElement)page.FindName("SensorDetails");
        var disclosure = (Button)page.FindName("SensorDetailsButton");
        Check(details.Visibility == Visibility.Collapsed, "sensor details start closed");
        ((IInvokeProvider)new ButtonAutomationPeer(disclosure).GetPattern(PatternInterface.Invoke)).Invoke();
        Check(details.Visibility == Visibility.Visible, "sensor details open on request");
        void Click() => ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
        Check(panel.Visibility == Visibility.Visible, "System offers administrator restart without opening another provider");
        ((Slider)page.FindName("BrightnessSlider")).Value = 23;
        Click();
        await WaitAsync(() => actions.Requests == 1);
        using (var saved = JsonDocument.Parse(File.ReadAllText(settings)))
        {
            Check(saved.RootElement.GetProperty("themeId").GetString() == "fixture-20"
                && saved.RootElement.GetProperty("brightnessPercent").GetDouble() == 23,
                "pending settings and System theme are saved before requesting elevation");
        }
        Check(!button.IsEnabled && actions.CloseRequests == 0, "pending elevation keeps the studio open and blocks duplicate requests");
        actions.Result.SetResult(false);
        await WaitAsync(() => button.IsEnabled);
        Check(actions.CloseRequests == 0, "canceling elevation keeps the original studio");
        actions.Result = new();
        Click();
        await WaitAsync(() => actions.Requests == 2);
        actions.Result.SetException(new IOException("Offline launch failure"));
        await WaitAsync(() => button.IsEnabled);
        Check(actions.CloseRequests == 0, "a failed launch keeps the studio and restores the button");
        actions.Result = new();
        Click();
        await WaitAsync(() => actions.Requests == 3);
        actions.Result.SetResult(true);
        await WaitAsync(() => actions.CloseRequests == 1);
        Check(client.StartRequests == 0 && client.PresentRequests == 0, "administrator restart never starts output in the fixture");
        actions.CanRestartAsAdministrator = false;
        ((ListBox)page.FindName("CategoriesBox")).SelectedIndex = 0;
        ((ListBox)page.FindName("CategoriesBox")).SelectedIndex = 2;
        Check(panel.Visibility == Visibility.Collapsed, "already elevated or unsupported hosts hide the restart control");
        actions.CanRestartAsAdministrator = true;
        ((ListBox)page.FindName("CategoriesBox")).SelectedIndex = 0;
        Check(panel.Visibility == Visibility.Collapsed, "Music does not show the hardware elevation control");
        var launch = WindowsElevation.CreateStartInfo(Environment.ProcessId, Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks);
        Check(launch.UseShellExecute && launch.Verb == "runas"
            && !launch.ArgumentList.Contains("--restore-last-session") && !launch.ArgumentList.Contains("--start-minimized"),
            "manual elevation requests a foreground launch");
        var startup = WindowsElevation.CreateStartInfo(Environment.ProcessId, Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks,
            ["--start-minimized", "--restore-last-session"]);
        Check(startup.UseShellExecute && startup.Verb == "runas"
            && startup.ArgumentList.TakeLast(2).SequenceEqual(new[] { "--start-minimized", "--restore-last-session" }),
            "startup elevation retains both saved-output and tray arguments");
        Console.WriteLine($"{count} offline administrator checks passed. No engine, sensor or USB backend was opened.");
    }

    private static async Task WaitAsync(Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while (!condition())
        {
            if (watch.Elapsed > TimeSpan.FromSeconds(5)) { throw new TimeoutException("Administrator fixture timed out."); }
            await Task.Delay(25);
        }
    }

    private sealed class RestartActions : IStudioWindowActions
    {
        public bool CanMinimizeToTray => false;
        public bool CanRestartAsAdministrator { get; set; } = true;
        public int Requests { get; private set; }
        public int CloseRequests { get; private set; }
        public TaskCompletionSource<bool> Result { get; set; } = new();
        public Task<bool> RestartAsAdministratorAsync() { Requests++; return Result.Task; }
        public void CloseApplication() => CloseRequests++;
        public void MinimizeToTray() => throw new InvalidOperationException("No tray in this fixture.");
    }
}
