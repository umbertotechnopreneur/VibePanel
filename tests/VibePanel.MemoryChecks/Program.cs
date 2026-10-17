// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
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
using VibePanel.Contracts;
using VibePanel.Core;
using VibePanel.Photino;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.FirstOrDefault() == WindowsMemoryGuard.Argument) { return WindowsMemoryGuard.Run(args); }
        var output = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../memory-checks/ui"));
        Directory.CreateDirectory(output);
        using var guard = WindowsMemoryGuard.Start();
        var client = new FixtureClient();
        var exitCode = 1;
        var started = 0;
        PhotinoWindow? window = null;
        window = new PhotinoWindow().SetTitle("Vibe Panel - offline memory checks")
            .SetLogVerbosity(0).SetSize(1400, 940)
            .SetBrowserControlInitParameters("--enable-precise-memory-info")
            .ConfigureOpenSilver(() =>
            {
                var app = new VibePanel.App.App(client, presentationSettingsPath: Path.Combine(output, Guid.NewGuid().ToString("N") + ".json"), preview: new PhotinoPreview(window!));
                var page = (FrameworkElement)app.RootVisual;
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
                timer.Tick += async (_, _) =>
                {
                    if (client.Frames == 0 || Interlocked.Exchange(ref started, 1) != 0) { return; }
                    timer.Stop();
                    try { await ExerciseAsync(page, client); exitCode = 0; }
                    catch (Exception error) { Console.Error.WriteLine(error); }
                    finally { window!.Invoke(() => window.Close()); }
                };
                timer.Start();
                return app;
            }).Load(Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html"));
        window.WaitForClose();
        return exitCode;
    }

    private static async Task ExerciseAsync(FrameworkElement page, FixtureClient client)
    {
        T Find<T>(string name) where T : class => page.FindName(name) as T ?? throw new InvalidOperationException(name);
        void Click(string name) => ((IInvokeProvider)new ButtonAutomationPeer(Find<Button>(name)).GetPattern(PatternInterface.Invoke)).Invoke();
        void Report(string phase)
        {
            using var process = Process.GetCurrentProcess();
            var browser = OpenSilver.Interop.ExecuteJavaScriptGetResult<string>(
                "JSON.stringify({heap:performance.memory?.usedJSHeapSize,heapTotal:performance.memory?.totalJSHeapSize,images:document.images.length,refs:Object.keys(document.jsObjRef).length,preview:window.vibePanelPreview.stats})");
            var browserState = JsonSerializer.Deserialize<JsonElement>(browser);
            var stats = browserState.GetProperty("preview");
            if (stats.GetProperty("decodedBitmaps").GetInt32() > 1 || stats.GetProperty("maximumInFlight").GetInt32() != 1)
            { throw new InvalidOperationException("Decoded preview frames were not released."); }
            if (stats.GetProperty("frames").GetInt32() < client.Frames - 30)
            { throw new InvalidOperationException("The browser did not draw the preview frames."); }
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                phase,
                frames = client.Frames,
                privateBytes = process.PrivateMemorySize64,
                managedBytes = GC.GetTotalMemory(false),
                maxStateInFlight = client.MaximumStateInFlight,
                browser = browserState
            }));
        }
        var refresh = Find<ComboBox>("RefreshBox");
        refresh.SelectedIndex = refresh.Items.Count - 1; // Requested 25 ms, never a hardware rate claim.
        await Task.Delay(10_000);
        Report("warmup");
        using var process = Process.GetCurrentProcess();
        var before = process.PrivateMemorySize64;
        await Task.Delay(60_000);
        Report("steady-preview");
        var categories = Find<ListBox>("CategoriesBox");
        var themes = Find<ListBox>("ThemesBox");
        for (var category = 0; category < categories.Items.Count; category++)
        {
            categories.SelectedIndex = category;
            for (var theme = 0; theme < themes.Items.Count; theme++) { themes.SelectedIndex = theme; await Task.Delay(400); }
        }
        Report("theme-switches-and-fades");
        foreach (var name in new[] { "LightButton", "DarkButton", "AutoButton" }) { Click(name); await Task.Delay(350); }
        var filters = Find<CheckBox>("FiltersBox");
        filters.IsChecked = true;
        foreach (var name in new[] { "SaturationSlider", "HueSlider", "BrightnessSlider", "ContrastSlider" })
        {
            var slider = Find<Slider>(name);
            slider.Value = slider.Maximum;
            await Task.Delay(200);
            slider.Value = slider.Minimum;
            await Task.Delay(200);
        }
        filters.IsChecked = false;
        Click("ResetColorsButton");
        Find<CheckBox>("SpectrumBox").IsChecked = false;
        Find<ComboBox>("QualityBox").SelectedIndex = 0;
        Find<ComboBox>("FontsBox").SelectedIndex = 1;
        await Task.Delay(500);
        Report("appearance-colors-fonts-quality");
        Click("StartDisplayButton"); // Fixture client only; it cannot open a USB handle.
        await Task.Delay(5000);
        Click("StopDisplayButton");
        Report("simulated-output-start-stop");
        client.RejectFrames = true;
        await Task.Delay(1000);
        client.RejectFrames = false;
        Click("ReloadButton");
        await Task.Delay(1000);
        Report("error-clear-and-reload");
        categories.SelectedIndex = 0;
        await Task.Delay(10_000);
        Report("final-preview");
        process.Refresh();
        if (process.PrivateMemorySize64 > before + 128 * 1024 * 1024) { throw new InvalidOperationException("UI retained over 128 MiB beyond warmup."); }
        if (client.MaximumStateInFlight != 1 || client.Frames < 500) { throw new InvalidOperationException("Preview framing or backpressure check failed."); }
        Console.WriteLine("Offline UI memory paths passed. No USB backend was used.");
    }

    private sealed class FixtureClient : IPanelClient
    {
        private readonly byte[] jpeg = CreateFrame();
        private int stateInFlight;
        private bool active;
        public int Frames { get; private set; }
        public int MaximumStateInFlight { get; private set; }
        public bool RejectFrames { get; set; }

        public async Task<T> RequestAsync<T>(string method, object? parameters = null, CancellationToken cancellationToken = default)
        {
            object result;
            switch (method)
            {
                case "themes":
                    result = new ThemeCatalogDto([new("multimedia", "Multimedia"), new("codex", "OpenAI Codex")],
                        Enumerable.Range(0, 20).Select(i => new ThemeChoiceDto("fixture-" + i, "Offline theme " + i, i < 10 ? "multimedia" : "codex", "Memory fixture", "fixture")).ToArray(), []);
                    break;
                case "devices": result = new PanelChoice[] { new("offline-fixture", "Offline fixture - no USB") }; break;
                case "diagnostics.status": result = new DiagnosticsStatus(false, ""); break;
                case "fonts": result = new FontCatalogDto(["Cascadia Mono", "Consolas", "Courier New"], "Offline fixture"); break;
                case "codex.status": result = new CodexSetupStatus(false, false, false, "Offline fixture", null, null, ""); break;
                case "start": active = true; result = new DisplayStartResult(true, "wide-1280x480"); break;
                case "stop": active = false; result = JsonSerializer.SerializeToElement(new { stopped = true }); break;
                case "state":
                case "present":
                    var current = Interlocked.Increment(ref stateInFlight);
                    MaximumStateInFlight = Math.Max(MaximumStateInFlight, current);
                    try
                    {
                        await Task.Delay(5, cancellationToken);
                        if (RejectFrames) { throw new IOException("Offline memory error fixture."); }
                        var frame = (byte[])jpeg.Clone();
                        BitConverter.TryWriteBytes(frame.AsSpan(6, 4), ++Frames);
                        result = new PanelState("Offline memory fixture", "", "", false, "Fixture", [],
                            new(null, null, null, null, "", "Unavailable"), null, [],
                            [new("wide-1280x480", "1280 x 480")], [], active, "Offline fixture", frame, [], SubmittedFrames: 0, AccentColor: "#80A0D0");
                    }
                    finally { Interlocked.Decrement(ref stateInFlight); }
                    break;
                default: throw new InvalidOperationException("No real provider exists in this fixture: " + method);
            }
            return (T)result;
        }

        private static byte[] CreateFrame()
        {
            var pattern = PanelFrameRenderer.Pattern(DisplayProfileCatalog.GetRequired("wide-1280x480"));
            using var stream = new MemoryStream();
            stream.Write(new byte[] { 0xff, 0xd8, 0xff, 0xfe, 0, 6, 0, 0, 0, 0 });
            var comment = new byte[60_000];
            for (var i = 0; i < 5; i++)
            {
                stream.Write(new byte[] { 0xff, 0xfe, 0xea, 0x62 }); // 60,002 bytes including the length.
                stream.Write(comment);
            }
            stream.Write(pattern.AsSpan(2));
            var frame = stream.ToArray();
            if (frame.Length > 450_000) { throw new InvalidOperationException("Fixture exceeds the real JPEG cap."); }
            return frame;
        }
    }
}
