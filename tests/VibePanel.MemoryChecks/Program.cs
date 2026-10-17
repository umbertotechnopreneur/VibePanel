// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
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
        if (args.FirstOrDefault() == WindowsElevation.Argument) { return AdministratorChecks.FinishHandoff(args); }
        if (args.Contains("--elevation-handoff-check")) { return AdministratorChecks.StartHandoff(); }
        if (args.Contains("--administrator-check")) { return AdministratorChecks.Run(); }
        if (args.Contains("--startup-check")) { return StartupChecks.Run(); }
        if (args.Contains("--visibility-check")) { return VisibilityChecks.Run(args.Contains("--preview-capture")); }
        if (args.Contains("--background-check")) { return BackgroundChecks.Run(); }
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
                    try
                    {
                        if (args.Contains("--preview-capture")) { await CheckVisiblePreviewAsync(page, client, window!, output); }
                        else { await ExerciseAsync(page, client); }
                        exitCode = 0;
                    }
                    catch (Exception error) { Console.Error.WriteLine(error); }
                    finally { window!.Invoke(() => window.Close()); }
                };
                timer.Start();
                return app;
            }).Load(Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html"));
        window.WaitForClose();
        return exitCode;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void CaptureCompleted(int result);

    [DllImport("Photino.Native.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
    private static extern int VibePanel_CapturePreview(IntPtr instance, string path, CaptureCompleted completed);

    private static async Task CheckVisiblePreviewAsync(FrameworkElement page, FixtureClient client, PhotinoWindow window, string output)
    {
        T Find<T>(string name) where T : class => page.FindName(name) as T ?? throw new InvalidOperationException(name);
        void Click(string name) => ((IInvokeProvider)new ButtonAutomationPeer(Find<Button>(name)).GetPattern(PatternInterface.Invoke)).Invoke();
        async Task CaptureAsync(string phase)
        {
            var pointer = (IntPtr)typeof(PhotinoWindow).GetField("_nativeInstance", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
            var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            CaptureCompleted done = result => completion.TrySetResult(result);
            var file = Path.Combine(output, phase + ".png");
            window.Invoke(() => Marshal.ThrowExceptionForHR(VibePanel_CapturePreview(pointer, file, done)));
            Marshal.ThrowExceptionForHR(await completion.Task.WaitAsync(TimeSpan.FromSeconds(10)));
            GC.KeepAlive(done);
            var json = OpenSilver.Interop.ExecuteJavaScriptGetResult<string>("JSON.stringify((()=>{"
                + "const root=document.getElementById('vp-preview'), c=root.querySelectorAll('canvas')[1], r=c.getBoundingClientRect();"
                + "let visible=r.width>16&&r.height>16&&r.right>0&&r.bottom>0&&r.left<innerWidth&&r.top<innerHeight;"
                + "for(let e=c;e;e=e.parentElement){const s=getComputedStyle(e);if(s.display==='none'||s.visibility==='hidden'||Number(s.opacity)===0)visible=false;}"
                + "const ctx=c.getContext('2d'), samples=[];for(const y of [.2,.5,.8])for(const x of [.2,.5,.8])samples.push(Array.from(ctx.getImageData(Math.floor(c.width*x),Math.floor(c.height*y),1,1).data));"
                + "return{visible,width:r.width,height:r.height,pixelWidth:c.width,pixelHeight:c.height,samples,frames:window.vibePanelPreview.stats.frames};})())");
            var state = JsonSerializer.Deserialize<JsonElement>(json);
            Console.WriteLine(JsonSerializer.Serialize(new { phase, file, preview = state, frames = client.Frames }));
            if (!state.GetProperty("visible").GetBoolean()) { throw new InvalidOperationException("The preview canvas has no visible on-screen rectangle."); }
            var aspect = state.GetProperty("width").GetDouble() / state.GetProperty("height").GetDouble();
            var pixelsAspect = state.GetProperty("pixelWidth").GetDouble() / state.GetProperty("pixelHeight").GetDouble();
            if (Math.Abs(aspect / pixelsAspect - 1) > .02) { throw new InvalidOperationException("The preview image is stretched."); }
            if (state.GetProperty("samples").EnumerateArray().Select(pixel => pixel.GetRawText()).Distinct().Count() < 3)
            { throw new InvalidOperationException("The visible preview fixture is blank or a flat color."); }
        }
        Find<ComboBox>("RefreshBox").SelectedIndex = 3;
        await Task.Delay(2500);
        await CaptureAsync("preview-wide");
        Click("LightButton");
        await Task.Delay(750);
        await CaptureAsync("preview-light");
        Click("DarkButton");
        window.Invoke(() => window.SetSize(1000, 740));
        await Task.Delay(1000);
        await CaptureAsync("preview-resized");
        var profiles = Find<ComboBox>("ProfilesBox");
        foreach (var id in new[] { "square-480", "portrait-240x320", "wide-1280x480" })
        {
            profiles.SelectedItem = profiles.Items.Cast<PanelChoice>().First(item => item.Id == id);
            await Task.Delay(1000);
            await CaptureAsync("preview-" + id);
        }
        Find<ListBox>("ThemesBox").SelectedIndex = 1;
        await Task.Delay(750);
        await CaptureAsync("preview-after-fade");
        Console.WriteLine("Visible preview checks passed: pixels, bounds, light/dark, window size, three aspect ratios and theme fade. No USB backend was used.");
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

    internal sealed class FixtureClient : IPanelClient
    {
        private static readonly string[] ProfileIds = ["wide-1280x480", "square-480", "portrait-240x320"];
        private readonly Dictionary<string, byte[]> jpegs = ProfileIds.ToDictionary(id => id, CreateFrame);
        private int stateInFlight;
        private bool active;
        public int Frames { get; private set; }
        public int MaximumStateInFlight { get; private set; }
        public bool RejectFrames { get; set; }
        public bool RejectStart { get; set; }
        public bool IncludeSystem { get; init; }
        public PanelChoice[] Devices { get; set; } = [new("offline-fixture", "Offline fixture - no USB")];
        public int DeviceRequests { get; private set; }
        public int FontRequests { get; private set; }
        public int StartRequests { get; private set; }
        public int StopRequests { get; private set; }
        public int PresentRequests { get; private set; }
        public StartOptions? LastStart { get; private set; }
        public PreviewOptions? LastPresent { get; private set; }
        public PreviewOptions? LastPreview { get; private set; }

        public async Task<T> RequestAsync<T>(string method, object? parameters = null, CancellationToken cancellationToken = default)
        {
            object result;
            switch (method)
            {
                case "themes":
                    result = new ThemeCatalogDto(IncludeSystem
                        ? [new("multimedia", "Multimedia"), new("codex", "OpenAI Codex"), new("system", "System")]
                        : [new("multimedia", "Multimedia"), new("codex", "OpenAI Codex")],
                        Enumerable.Range(0, IncludeSystem ? 30 : 20).Select(i => new ThemeChoiceDto("fixture-" + i, "Offline theme " + i,
                            i < 10 ? "multimedia" : i < 20 ? "codex" : "system", "Memory fixture", "fixture")).ToArray(), []);
                    break;
                case "devices":
                    DeviceRequests++;
                    if (active && !Devices.Any(device => device.Id == LastStart?.DeviceId)) { active = false; }
                    result = Devices;
                    break;
                case "diagnostics.status": result = new DiagnosticsStatus(false, ""); break;
                case "fonts": FontRequests++; result = new FontCatalogDto(["Cascadia Mono", "Consolas", "Courier New"], "Offline fixture"); break;
                case "codex.status": result = new CodexSetupStatus(false, false, false, "Offline fixture", null, null, ""); break;
                case "start":
                    StartRequests++;
                    LastStart = parameters as StartOptions ?? throw new InvalidOperationException("Missing fixture start options.");
                    if (RejectStart) { throw new IOException("Offline startup failure fixture."); }
                    active = true;
                    result = new DisplayStartResult(true, "wide-1280x480");
                    break;
                case "stop": StopRequests++; active = false; result = JsonSerializer.SerializeToElement(new { stopped = true }); break;
                case "state":
                case "present":
                    LastPreview = parameters as PreviewOptions;
                    if (method == "present")
                    {
                        PresentRequests++;
                        LastPresent = parameters as PreviewOptions;
                        if (!active) { throw new IOException("Offline display is stopped."); }
                    }
                    var current = Interlocked.Increment(ref stateInFlight);
                    MaximumStateInFlight = Math.Max(MaximumStateInFlight, current);
                    try
                    {
                        await Task.Delay(5, cancellationToken);
                        if (RejectFrames) { throw new IOException("Offline memory error fixture."); }
                        var profileId = parameters is PreviewOptions options && jpegs.ContainsKey(options.ProfileId) ? options.ProfileId : ProfileIds[0];
                        var frame = (byte[])jpegs[profileId].Clone();
                        BitConverter.TryWriteBytes(frame.AsSpan(6, 4), ++Frames);
                        result = new PanelState("Offline memory fixture", "", "", false, "Fixture", [],
                            new(null, null, null, null, "", "Unavailable"), null, [],
                            ProfileIds.Select(id => new PanelChoice(id, id)).ToArray(), [], active, "Offline fixture", frame, [], SubmittedFrames: 0, AccentColor: "#80A0D0");
                    }
                    finally { Interlocked.Decrement(ref stateInFlight); }
                    break;
                default: throw new InvalidOperationException("No real provider exists in this fixture: " + method);
            }
            return (T)result;
        }

        private static byte[] CreateFrame(string profileId)
        {
            var pattern = PanelFrameRenderer.Pattern(DisplayProfileCatalog.GetRequired(profileId) with { Encoding = PanelPixelEncoding.Jpeg });
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
