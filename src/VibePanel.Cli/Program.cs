// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using VibePanel.Core;
using Spectre.Console;
using System.Text;
using System.Globalization;

return args is ["--stdio"] ? await EngineServer.RunAsync() : await VibePanelCli.RunAsync(args);

internal static class VibePanelCli
{
    /// <summary>Runs the small, explicit command surface for panel discovery and control.</summary>
    public static Task<int> RunAsync(string[] args)
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
            if (!string.Equals(args.FirstOrDefault(), "sensors", StringComparison.OrdinalIgnoreCase)) { Banner(); }
            return Task.FromResult(args.FirstOrDefault()?.ToLowerInvariant() switch
            {
                "discover" => Discover(),
                "profiles" => Profiles(),
                "probe" => Probe(args),
                "pattern" => Pattern(args),
                "hold" => Hold(args),
                "image" => Image(args, false),
                "play" => Image(args, true),
                "settings" => Settings(args),
                "preview" => Preview(args),
                "dashboard" => Dashboard(args),
                "media" => Media(args),
                "volumes" => Volumes(),
                "themes" => Themes(),
                "fonts" => Fonts(),
                "theme" => Theme(args),
                "codex" => Codex(args),
                "diagnostics" => Diagnostics(args),
                "sensors" => Sensors(args),
                "gui" => Gui(args),
                "help" or "--help" or "-h" or null => Help(),
                _ => Fail("I do not know that command. Run 'vp help'.")
            });
        }
        catch (Exception exception)
        {
            return Task.FromResult(Fail(exception.Message));
        }
    }

    private static void Banner()
    {
        AnsiConsole.Write(new FigletText("Vibe Panel").Color(new Color(199, 123, 211)));
        AnsiConsole.MarkupLine("[grey]by Umberto Giacobbi[/]");
        AnsiConsole.Write(new Rule().RuleStyle("grey"));
    }

    private static int Dashboard(IReadOnlyList<string> args)
    {
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancel = (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += cancel;
        try
        {
            if (args.Skip(1).Any(argument => argument != "--watch")) { throw new ArgumentException("Usage: vp dashboard [--watch]. Use vp theme <id> for rendering and display output."); }
            using var service = new DashboardService(WindowsDashboardProviders.Create());
            do
            {
                var snapshot = service.ReadAsync().GetAwaiter().GetResult();
                ShowDashboard(snapshot);
                if (!args.Contains("--watch")) { break; }
                Task.Delay(2000, cancellation.Token).GetAwaiter().GetResult();
            } while (!cancellation.IsCancellationRequested);
            return 0;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { return 0; }
        finally { Console.CancelKeyPress -= cancel; }
    }

    private static void ShowDashboard(DashboardSnapshot snapshot)
    {
        var media = snapshot.Media;
        AnsiConsole.Write(new Panel(new Markup($"[bold white]{Markup.Escape(media.Title)}[/]\n[grey]{Markup.Escape(media.Artist)}[/]\n\n[rgb(221,176,235)]{(media.Playing ? "● Playing" : "Ⅱ Paused / ready")}[/]"))
            .Header("[bold] NOW PLAYING [/]").BorderColor(new Color(199, 123, 211)).Padding(2, 1));
        ShowVolumes(snapshot.Volumes);
        var metrics = snapshot.Metrics;
        var table = new Table().Border(TableBorder.Rounded).AddColumn("System").AddColumn("Usage").AddColumn("Temperature");
        static string Metric(double? value, string unit) => value is { } number ? $"{number:F0}{unit}" : "—";
        table.AddRow("CPU", Metric(metrics.CpuUsage, "%"), Metric(metrics.CpuTemperature, "°C"));
        table.AddRow(Markup.Escape(metrics.GpuName), Metric(metrics.GpuUsage, "%"), Metric(metrics.GpuTemperature, "°C"));
        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[grey]CPU temperature: {Markup.Escape(metrics.CpuTemperatureSource)}[/]");
        if (snapshot.Network is { } network)
        { AnsiConsole.WriteLine($"Network ↓ {NetworkTrafficService.Size(network.DownloadBytesPerSecond)}/s · ↑ {NetworkTrafficService.Size(network.UploadBytesPerSecond)}/s · total ↓ {NetworkTrafficService.Size(network.DownloadBytes)}, ↑ {NetworkTrafficService.Size(network.UploadBytes)} since start."); }
        foreach (var notice in snapshot.Notices) { AnsiConsole.WriteLine(notice); }
    }

    private static void ShowVolumes(IReadOnlyList<AppVolume> volumes)
    {
        var table = new Table().Border(TableBorder.Rounded).AddColumn("Application / audio session").AddColumn("Volume").AddColumn("Level / -60..0 dBFS");
        for (var i = 0; i < volumes.Count; i++)
        {
            var volume = volumes[i];
            var filled = (int)Math.Round(AudioLevel.Fraction(volume.Peak) * 16);
            table.AddRow(Markup.Escape(volume.Name), volume.Muted ? $"{volume.Percent:F0}% · muted" : $"{volume.Percent:F0}%",
                $"[green]{new string('━', Math.Min(filled, 10))}[/][yellow]{new string('━', Math.Clamp(filled - 10, 0, 3))}[/][magenta]{new string('━', Math.Max(0, filled - 13))}[/][grey]{new string('─', 16 - filled)}[/] {AudioLevel.Label(volume.Peak)}");
        }
        AnsiConsole.Write(table);
    }

    private static int Media(IReadOnlyList<string> args)
    {
        if (args.Count != 2) { return Fail("Usage: vp media toggle|play|pause|next|previous"); }
        new WindowsMediaService().ControlAsync(args[1]).GetAwaiter().GetResult();
        AnsiConsole.WriteLine("Media command accepted by the selected Windows session.");
        return 0;
    }

    private static int Gui(IReadOnlyList<string> args)
    {
        if (args.Count > 2 || args.Count == 2 && args[1] != "--start-minimized")
        { return Fail("Usage: vp gui [--start-minimized]"); }
        var executable = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "VibePanel.exe"));
        if (!File.Exists(executable)) { throw new FileNotFoundException("Open the GUI from the portable folder. VibePanel.exe is missing beside engine/.", executable); }
        var start = new System.Diagnostics.ProcessStartInfo(executable)
        { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(executable)! };
        if (args.Contains("--start-minimized")) { start.ArgumentList.Add("--start-minimized"); }
        using var process = System.Diagnostics.Process.Start(start);
        if (process is null) { throw new IOException("The Vibe Panel window could not open."); }
        AnsiConsole.WriteLine("Vibe Panel opened. Its window owns one hidden CLI engine.");
        return 0;
    }

    private static int Themes()
    {
        var catalog = ThemeCatalog.Read();
        var table = new Table().Border(TableBorder.Rounded).AddColumn("Theme ID").AddColumn("Name").AddColumn("Category").AddColumn("Pack");
        foreach (var theme in catalog.Themes)
        { table.AddRow(Markup.Escape(theme.Id), Markup.Escape(theme.Name), theme.Category == "codex" ? "OpenAI Codex" : "Multimedia", Markup.Escape(theme.PackId)); }
        AnsiConsole.Write(table);
        foreach (var notice in catalog.Notices) { AnsiConsole.WriteLine(notice); }
        AnsiConsole.WriteLine($"My packs: {ThemeCatalog.UserThemesPath}");
        AnsiConsole.WriteLine("Theme folders are listed from manifests. Scripts are only loaded for an explicit preview.");
        return catalog.Themes.Count > 0 ? 0 : 1;
    }

    private static int Fonts()
    {
        using var renderer = new PythonThemeRenderer();
        var catalog = renderer.ListFontsAsync().GetAwaiter().GetResult();
        var table = new Table().Border(TableBorder.Rounded).AddColumn("Monospace font family");
        foreach (var family in catalog.Families) { table.AddRow(Markup.Escape(family)); }
        AnsiConsole.Write(table);
        if (catalog.Notice is not null) { AnsiConsole.WriteLine(catalog.Notice); }
        AnsiConsole.WriteLine("Local renderer fonts. No theme, audio source or USB panel was opened.");
        return 0;
    }

    private static int Codex(IReadOnlyList<string> args)
    {
        if (args.Count != 2 || args[1] is not ("status" or "prepare"))
        { return Fail("Usage: vp codex status|prepare"); }
        var result = args[1] == "prepare" ? CodexIntegration.PrepareAsync().GetAwaiter().GetResult() : CodexIntegration.Status();
        AnsiConsole.MarkupLine($"[bold]Vibe Panel Codex:[/] {Markup.Escape(result.Status)}");
        AnsiConsole.WriteLine($"Plugin files: {(result.Prepared ? "prepared" : "not prepared")}; personal marketplace: {(result.Listed ? "listed" : "not listed")}.");
        if (result.ActivitySeen) { AnsiConsole.WriteLine($"Last local event: {result.LastEvent} at {result.LastEventUtc}"); }
        AnsiConsole.WriteLine(result.Instructions);
        return 0;
    }

    private static int Diagnostics(IReadOnlyList<string> args)
    {
        if (args.Count != 2 || args[1] is not ("status" or "on" or "off" or "open"))
        { return Fail("Usage: vp diagnostics status|on|off|open"); }
        if (args[1] == "open")
        {
            if (!DiagnosticLog.Open()) { return Fail("No local engine log exists yet."); }
            AnsiConsole.WriteLine("Opened the local engine log.");
            return 0;
        }
        var result = args[1] switch
        {
            "on" => DiagnosticLog.SetEnabled(true),
            "off" => DiagnosticLog.SetEnabled(false),
            _ => DiagnosticLog.Status()
        };
        AnsiConsole.WriteLine($"Error logging: {(result.Enabled ? "on" : "off")}. {result.LogPath}");
        AnsiConsole.WriteLine("Local only. The log omits prompts, chat titles, commands, tool arguments and results.");
        return 0;
    }

    private static int Sensors(IReadOnlyList<string> args)
    {
        if (args.Count > 2 || args.Count == 2 && args[1] is not ("--diagnose" or "--all"))
        { return Fail("Usage: vp sensors [--diagnose|--all]"); }
        using var sensors = new WindowsMetricsService(allowHardwareAccess: true);
        if (args.Contains("--all"))
        {
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(sensors.ReadSensorDebug(), VibePanel.Contracts.PanelContract.Json));
            return sensors.InitializationFailureDetails is null ? 0 : 1;
        }
        var snapshot = sensors.ReadSensors();
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(snapshot, VibePanel.Contracts.PanelContract.Json));
        if (snapshot.Notice is { } notice) { Console.Error.WriteLine(notice); }
        if (args.Contains("--diagnose") && sensors.InitializationFailureDetails is { } details)
        { Console.Error.WriteLine(details); }
        return sensors.InitializationFailureDetails is null ? 0 : 1;
    }

    private static int Theme(IReadOnlyList<string> args)
    {
        var export = GetOption(args, "--export");
        var deviceNumber = GetOption(args, "--device");
        if (args.Count < 2 || args[1].StartsWith("--", StringComparison.Ordinal) || (export is null) == (deviceNumber is null))
        { return Fail("Usage: vp theme <id> (--export <new.jpg> | --device <number>) [--profile <id>] [--font \"Cascadia Mono\"] [--font-scale 1] [--light] [--saturation 100] [--hue 0] [--brightness 0] [--contrast 100] [--no-spectrum] [--seconds N] [--interval-ms 1000] [--quality high]"); }
        var theme = ThemeCatalog.Read().Themes.FirstOrDefault(item => item.Id == args[1])
            ?? throw new ArgumentException("Theme not found. Run 'vp themes'.");
        var font = GetOption(args, "--font") ?? "Cascadia Mono";
        var scaleText = GetOption(args, "--font-scale") ?? "1";
        var intervalText = GetOption(args, "--interval-ms") ?? "1000";
        var quality = (GetOption(args, "--quality") ?? "high").ToLowerInvariant();
        var saturation = GetColorOption(args, "--saturation", 100);
        var hue = GetColorOption(args, "--hue", 0);
        var brightness = GetColorOption(args, "--brightness", 0);
        var contrast = GetColorOption(args, "--contrast", 100);
        PythonThemeRenderer.ValidateColorAdjustments(saturation, hue, brightness, contrast);
        var showSpectrum = !args.Contains("--no-spectrum");
        if (quality is not ("poor" or "low" or "medium" or "high")) { throw new ArgumentException("--quality must be poor, low, medium or high."); }
        if (!int.TryParse(intervalText, out var interval) || interval is < 25 or > 1000)
        { throw new ArgumentException("--interval-ms must be between 25 and 1000."); }
        if (!double.TryParse(scaleText, NumberStyles.Float, CultureInfo.InvariantCulture, out var scale))
        { throw new ArgumentException("--font-scale must be a number from 0.65 to 1.6 (use a decimal point)."); }
        if (deviceNumber is not null)
        {
            if (!double.IsFinite(scale) || scale is < 0.65 or > 1.6 || font.Length is 0 or > 128 || font.Any(char.IsControl))
            { throw new ArgumentException("Choose a printable font name and --font-scale between 0.65 and 1.6."); }
            var device = GetDevice(new[] { "theme", deviceNumber });
            var profileId = GetOption(args, "--profile") ?? "auto";
            TimeSpan? duration = null;
            if (GetOption(args, "--seconds") is { } secondsText)
            {
                if (!int.TryParse(secondsText, out var seconds) || seconds is < 1 or > 86400)
                { throw new ArgumentException("--seconds must be between 1 and 86400."); }
                duration = TimeSpan.FromSeconds(seconds);
            }
            using var cancellation = new CancellationTokenSource();
            ConsoleCancelEventHandler cancel = (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
            Console.CancelKeyPress += cancel;
            try
            {
                AnsiConsole.WriteLine($"Sending theme {theme.Name} to device {device.Id}. Profile: {profileId}.");
                ThemePlayback.RunAsync(theme, device, profileId, duration, new ThemeProgress(), cancellation.Token,
                    args.Contains("--light"), args.Contains("--ambient-drift"), font, scale, interval, quality,
                    saturation, hue, showSpectrum, brightness, contrast).GetAwaiter().GetResult();
            }
            finally { Console.CancelKeyPress -= cancel; }
            return 0;
        }
        if (GetOption(args, "--seconds") is not null) { throw new ArgumentException("--seconds applies only to explicit --device playback."); }
        var profile = DisplayProfileCatalog.GetRequired(GetOption(args, "--profile") ?? "wide-1280x480") with { Encoding = PanelPixelEncoding.Jpeg, WireRotation = 0 };
        var path = Path.GetFullPath(export!);
        if (File.Exists(path)) { throw new IOException("The export file already exists. Choose a new path."); }
        using var service = new DashboardService(WindowsDashboardProviders.Create());
        using var renderer = new PythonThemeRenderer();
        var snapshot = service.ReadAsync(includeSystem: false, includeMedia: theme.Category == "multimedia",
            includeSensors: theme.Category == "system").GetAwaiter().GetResult();
        var frame = renderer.RenderAsync(theme, snapshot, profile, args.Contains("--light"), args.Contains("--ambient-drift"), font, scale, quality,
            saturationPercent: saturation, hueDegrees: hue, showSpectrum: showSpectrum,
            brightnessPercent: brightness, contrastPercent: contrast).GetAwaiter().GetResult();
        using var output = new FileStream(path, FileMode.CreateNew);
        output.Write(frame);
        AnsiConsole.WriteLine($"{theme.Name}: {profile.Width} × {profile.Height} preview saved to {path}");
        foreach (var notice in snapshot.Notices) { AnsiConsole.WriteLine(notice); }
        return 0;
    }

    private sealed class ThemeProgress : IProgress<string>
    {
        public void Report(string value) => AnsiConsole.WriteLine(value);
    }

    private static int Volumes()
    {
        using var service = new WindowsVolumeService();
        ShowVolumes(service.Read());
        AnsiConsole.WriteLine("Read-only. Volume is the configured gain; the meter shows instantaneous playback level.");
        return 0;
    }

    private static int Discover()
    {
        var devices = DisplayDiscovery.FindDevices();
        Console.WriteLine("VibePanel — my compatible USB panels");
        if (devices.Count == 0)
        {
            var attached = WinUsbDisplayDiscovery.FindAttachedPanels();
            if (attached.Count == 0)
            {
                Console.WriteLine("No 0416:5302 panel is connected.");
                return 1;
            }

            Console.WriteLine($"I found {attached.Count} attached panel(s), but no supported HID or WinUSB interface is available.");
            return 2;
        }

        for (var index = 0; index < devices.Count; index++)
        {
            Console.WriteLine($"{index + 1}. {devices[index].Id} · {devices[index].Connection}");
        }

        return 0;
    }

    private static int Profiles()
    {
        Console.WriteLine("VibePanel — included screen profiles");
        foreach (var profile in DisplayProfileCatalog.All)
        {
            Console.WriteLine($"{profile.Id,-20} {profile.Width} x {profile.Height}  {profile.Encoding}");
        }

        Console.WriteLine("Custom geometry: --width <pixels> --height <pixels>");
        return 0;
    }

    private static int Probe(IReadOnlyList<string> args)
    {
        var device = GetDevice(args);
        Console.WriteLine($"Checking my {device.Id} panel. This sends only the device-information handshake.");
        using var transport = PanelSession.Open(device);
        var probe = transport.ProbeAsync().GetAwaiter().GetResult();
        Console.WriteLine(probe.Message);
        Console.WriteLine($"PM {probe.ProductMode:X2} / SUB {probe.SubMode:X2} · serial {probe.Serial}");
        Console.WriteLine($"Profile: {probe.DetectedProfile?.Name ?? "unknown; choose an explicit profile"}");
        Console.WriteLine($"Reply: {probe.ResponseHex}");
        return probe.IsCompatible ? 0 : 2;
    }

    private static int Pattern(IReadOnlyList<string> args)
    {
        var device = GetDevice(args);
        var profileId = args.Count > 2 && !args[2].StartsWith("--", StringComparison.Ordinal) ? args[2] : "auto";
        var custom = profileId == "custom" ? GetProfile(args) : null;
        Console.WriteLine($"Sending a diagnostic pattern to {device.Id}. Profile: {profileId}.");
        var result = new PanelApplication().SendAsync(device, new PanelPictureRequest(profileId, CustomProfile: custom)).GetAwaiter().GetResult();
        Console.WriteLine(result.Message);
        return 0;
    }

    private static int Hold(IReadOnlyList<string> args)
    {
        var device = GetDevice(args);
        var profileId = args.Count > 2 ? args[2] : "auto";
        Console.WriteLine($"Sending a diagnostic chart to {device.Id} for 20 seconds at 1 fps. Profile: {profileId}.");
        var progress = new Progress<string>(message => Console.WriteLine(message));
        new PanelApplication().PlayAsync(device, new PanelPictureRequest(profileId), TimeSpan.FromSeconds(20), progress, CancellationToken.None).GetAwaiter().GetResult();
        Console.WriteLine("Diagnostic playback ended.");
        return 0;
    }

    private static int Image(IReadOnlyList<string> args, bool play)
    {
        if (args.Count < 3 || args[2].StartsWith("--", StringComparison.Ordinal))
        {
            return Fail("Usage: vp image|play <device-number> <image-path> [profile] [--rotate 0|90|180|270]");
        }
        var app = new PanelApplication();
        var defaults = app.LoadSettings();
        var device = GetDevice(args);
        var profile = args.Count > 3 && !args[3].StartsWith("--", StringComparison.Ordinal) ? args[3] : defaults.ProfileId;
        var rotation = GetRotation(args, defaults.Rotation);
        var request = new PanelPictureRequest(profile, Path.GetFullPath(args[2]), rotation);
        Console.WriteLine($"{(play ? "Displaying" : "Sending")} {Path.GetFileName(request.ImagePath)} on {device.Id}. Profile: {profile}, rotation: {rotation}.");
        if (!play)
        {
            Console.WriteLine(app.SendAsync(device, request).GetAwaiter().GetResult().Message);
            return 0;
        }
        var secondsOption = GetOption(args, "--seconds");
        TimeSpan? duration = null;
        if (secondsOption is not null)
        {
            if (!int.TryParse(secondsOption, out var seconds) || seconds is < 1 or > 86400)
            { throw new ArgumentException("--seconds must be between 1 and 86400."); }
            duration = TimeSpan.FromSeconds(seconds);
        }
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancel = (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += cancel;
        Console.WriteLine("1 fps. Press Ctrl+C to stop. The image is encoded once and reused.");
        try
        {
            app.PlayAsync(device, request, duration, new Progress<string>(Console.WriteLine), cancellation.Token).GetAwaiter().GetResult();
        }
        finally { Console.CancelKeyPress -= cancel; }
        return 0;
    }

    private static int Settings(IReadOnlyList<string> args)
    {
        var app = new PanelApplication();
        var current = app.LoadSettings();
        if (args.Count > 1)
        {
            var image = GetOption(args, "--image");
            current = current with
            {
                ProfileId = GetOption(args, "--profile") ?? current.ProfileId,
                Rotation = GetRotation(args, current.Rotation),
                ImagePath = image is null ? current.ImagePath : Path.GetFullPath(image)
            };
            app.SaveSettings(current);
        }
        Console.WriteLine($"Profile: {current.ProfileId}; rotation: {current.Rotation}; image: {current.ImagePath ?? "none"}");
        return 0;
    }

    private static int Preview(IReadOnlyList<string> args)
    {
        if (args.Count != 3) { return Fail("Usage: vp preview <jpeg-profile> <output.jpg>"); }
        var profile = DisplayProfileCatalog.GetRequired(args[1]);
        if (profile.Encoding != PanelPixelEncoding.Jpeg) { return Fail("Choose a JPEG profile for this preview command."); }
        // CreateNew avoids replacing an existing user image during a preview operation.
        using var output = new FileStream(Path.GetFullPath(args[2]), FileMode.CreateNew);
        output.Write(PanelFrameRenderer.Pattern(profile));
        Console.WriteLine($"Saved {profile.Width} x {profile.Height} JPEG preview. No panel was opened.");
        return 0;
    }

    private static int GetRotation(IReadOnlyList<string> args, int defaultRotation)
    {
        var value = GetOption(args, "--rotate");
        if (value is null) { return defaultRotation; }
        if (!int.TryParse(value, out var rotation) || rotation is not (0 or 90 or 180 or 270))
        { throw new ArgumentException("--rotate must be 0, 90, 180, or 270."); }
        return rotation;
    }

    private static DisplayDevice GetDevice(IReadOnlyList<string> args)
    {
        if (args.Count < 2 || !int.TryParse(args[1], out var requested) || requested < 1)
        {
            throw new ArgumentException("Choose a device number from 'vp discover'.");
        }

        var devices = DisplayDiscovery.FindDevices();
        return devices.ElementAtOrDefault(requested - 1)
            ?? throw new ArgumentException($"Device {requested} is not connected. Run 'vp discover'.");
    }

    private static DisplayProfile GetProfile(IReadOnlyList<string> args)
    {
        var customWidth = GetOption(args, "--width");
        var customHeight = GetOption(args, "--height");
        if (customWidth is not null || customHeight is not null)
        {
            if (!int.TryParse(customWidth, out var width) || !int.TryParse(customHeight, out var height))
            {
                throw new ArgumentException("Custom geometry needs both --width and --height.");
            }

            return DisplayProfile.CreateCustom(width, height);
        }

        return DisplayProfileCatalog.GetRequired(args[2]);
    }

    private static string? GetOption(IReadOnlyList<string> args, string option)
    {
        for (var index = 0; index < args.Count - 1; index++)
        {
            if (string.Equals(args[index], option, StringComparison.Ordinal))
            {
                return args[index + 1];
            }
        }

        return null;
    }

    private static double GetColorOption(IReadOnlyList<string> args, string option, double fallback)
    {
        var text = GetOption(args, option);
        if (text is null && !args.Contains(option)) { return fallback; }
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value))
        { throw new ArgumentException($"{option} needs a finite number (use a decimal point)."); }
        return value;
    }

    private static int Help()
    {
        var commands = new Table().Border(TableBorder.Rounded).AddColumn("Command").AddColumn("What it does");
        commands.AddRow("[bold]vp dashboard[/]", "Now playing, app volumes, CPU / GPU. No USB writes.");
        commands.AddRow("vp dashboard --watch", "Refresh the terminal every 2 seconds. Ctrl+C stops.");
        commands.AddRow("vp media toggle|play|pause|next|previous", "Control the current generic Windows media session.");
        commands.AddRow("vp volumes", "Read-only app volume percentages and colored peak meters.");
        commands.AddRow("vp discover / vp profiles", "List connected panels / supported geometries.");
        commands.AddRow("vp themes", "List installed Multimedia and OpenAI Codex themes from their folders.");
        commands.AddRow("vp fonts", "List installed monospace families available to the Python/Skia renderer.");
        commands.AddRow("vp codex status|prepare", "Check or prepare my optional Codex plugin. Trust its hooks in Codex.");
        commands.AddRow("vp diagnostics status|on|off|open", "Opt-in local error log, capped at 512 KiB plus one backup.");
        commands.AddRow("vp sensors [--diagnose|--all]", "Read hardware once. --all returns the raw device/sensor tree; --diagnose prints initialization details. No audio or USB.");
        commands.AddRow("vp gui", "Open the desktop window from the portable folder.");
        commands.AddRow("vp gui --start-minimized", "Start in the Windows tray. Reuses the existing studio instance.");
        commands.AddRow("vp theme media.aurora --export frame.jpg", "Render a Python/Skia theme to a new file. No USB writes.");
        commands.AddRow("vp theme <id> --export frame.jpg --font <name> --light", "Use the same font and light-mode options as the UI.");
        commands.AddRow("vp theme media.aurora --device 1 --seconds 60", "Explicitly stream a theme to one panel at a target 1 fps; Ctrl+C stops.");
        commands.AddRow("vp theme media.aurora --device 1 --interval-ms 250", "Request updates every 250 ms; rendering and USB can be slower.");
        commands.AddRow("vp theme media.aurora --device 1 --quality medium", "JPEG quality: poor, low, medium or high. The panel size limit still applies.");
        commands.AddRow("vp theme media.aurora --device 1 --interval-ms 25 --quality poor", "Aggressive requested interval, very strong JPEG compression. Actual updates can be slower.");
        commands.AddRow("vp theme <id> --export frame.jpg --saturation 120 --hue -20", "Adjust saturation (0–200%) and hue (-180–180°). Also works with --device.");
        commands.AddRow("vp theme <id> --export frame.jpg --brightness 10 --contrast 115", "Adjust brightness (-100–100%) and contrast (0–200%). Themes can override color values in Python.");
        commands.AddRow("vp theme <id> --device 1 --no-spectrum", "Hide FFT bars in themes that support it. Keep VU meters and decibel values.");
        commands.AddRow("vp --stdio", "JSON Lines engine v1. No banner, no network listener, explicit USB start.");
        AnsiConsole.Write(commands);
        AnsiConsole.MarkupLine("[grey]Panel tools (these touch hardware only when explicitly invoked):[/]");
        Console.WriteLine("vp discover");
        Console.WriteLine("vp profiles");
        Console.WriteLine("vp probe <device-number>");
        Console.WriteLine("vp pattern <device-number> [auto|profile]");
        Console.WriteLine("vp hold <device-number> [auto|profile]  (20-second diagnostic playback)");
        Console.WriteLine("vp pattern <device-number> custom --width 320 --height 320");
        Console.WriteLine("vp image <device-number> <image-path> [profile] [--rotate 0|90|180|270]");
        Console.WriteLine("vp play <device-number> <image-path> [profile] [--seconds 20]");
        Console.WriteLine("vp settings [--profile auto|profile] [--rotate 0|90|180|270] [--image path]");
        Console.WriteLine("vp preview <jpeg-profile> <output.jpg>");
        return 0;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine($"VibePanel: {message}");
        return 1;
    }
}
