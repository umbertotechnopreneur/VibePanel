// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using VibePanel.Core;
using Spectre.Console;
using System.Text;

return await VibePanelCli.RunAsync(args);

internal static class VibePanelCli
{
    /// <summary>Runs the small, explicit command surface for panel discovery and control.</summary>
    public static Task<int> RunAsync(string[] args)
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
            Banner();
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
                "scenes" => Scenes(),
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
        AnsiConsole.Write(new FigletText("VibePanel").Color(new Color(199, 123, 211)));
        AnsiConsole.MarkupLine("[bold white]My music. My apps. My little screen.[/]  [grey]by Umberto Giacobbi[/]");
        AnsiConsole.MarkupLine("[rgb(221,176,235)]I stand with the VibeWare movement.[/]");
        AnsiConsole.Write(new Rule().RuleStyle("grey"));
    }

    private static int Dashboard(IReadOnlyList<string> args)
    {
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancel = (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += cancel;
        try
        {
            var deviceNumber = GetOption(args, "--device");
            if (deviceNumber is not null)
            {
                var device = GetDevice(new[] { "dashboard", deviceNumber });
                AnsiConsole.WriteLine($"Sending the live dashboard to {device.Id}. Ctrl+C stops. No automatic restart.");
                var sceneOption = GetOption(args, "--scene");
                DashboardPlayback.RunAsync(device, GetOption(args, "--profile") ?? "auto", new Progress<string>(Console.WriteLine), cancellation.Token,
                    sceneOption is null ? null : DashboardScenes.Parse(sceneOption)).GetAwaiter().GetResult();
                return 0;
            }
            using var service = new DashboardService();
            var renderer = new DashboardRenderer();
            var export = GetOption(args, "--export");
            do
            {
                var snapshot = service.ReadAsync().GetAwaiter().GetResult();
                ShowDashboard(snapshot);
                if (export is not null)
                {
                    var profile = DisplayProfileCatalog.GetRequired(GetOption(args, "--profile") ?? "wide-1280x480");
                    if (profile.Encoding != PanelPixelEncoding.Jpeg) { throw new ArgumentException("Export needs a JPEG profile."); }
                    using var output = new FileStream(Path.GetFullPath(export), FileMode.CreateNew);
                    output.Write(renderer.Render(snapshot, profile with { WireRotation = 0 }, DashboardScenes.Parse(GetOption(args, "--scene") ?? "Music")));
                    AnsiConsole.WriteLine($"Preview saved: {Path.GetFullPath(export)}");
                    AnsiConsole.WriteLine($"Cached background: {renderer.BackgroundPath}");
                    break;
                }
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
        table.AddRow("CPU", DashboardRenderer.Metric(metrics.CpuUsage, "%"), DashboardRenderer.Metric(metrics.CpuTemperature, "°C"));
        table.AddRow(Markup.Escape(metrics.GpuName), DashboardRenderer.Metric(metrics.GpuUsage, "%"), DashboardRenderer.Metric(metrics.GpuTemperature, "°C"));
        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[grey]CPU temperature: {Markup.Escape(metrics.CpuTemperatureSource)}[/]");
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

    private static int Scenes()
    {
        var table = new Table().Border(TableBorder.Rounded).AddColumn("Scene ID").AddColumn("Layout").AddColumn("Default rotation");
        foreach (var scene in Enum.GetValues<DashboardScene>())
        {
            table.AddRow(scene.ToString(), Markup.Escape(DashboardScenes.Name(scene)), DashboardScenes.Rotation.Contains(scene) ? "Yes" : "Optional");
        }
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine("Five scenes, 20 seconds each, short crossfades. --scene Music locks a single layout.");
        return 0;
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

    private static int Help()
    {
        var commands = new Table().Border(TableBorder.Rounded).AddColumn("Command").AddColumn("What it does");
        commands.AddRow("[bold]vp dashboard[/]", "Now playing, app volumes, CPU / GPU. No USB writes.");
        commands.AddRow("vp dashboard --watch", "Refresh the terminal every 2 seconds. Ctrl+C stops.");
        commands.AddRow("vp dashboard --export frame.jpg", "Save a real-data JPEG preview; cache wallpaper palette once.");
        commands.AddRow("vp dashboard --device 1", "Explicitly stream to a USB panel. Optional --profile <id>.");
        commands.AddRow("vp media toggle|play|pause|next|previous", "Control Spotify, or the current Windows media session.");
        commands.AddRow("vp volumes", "Read-only app volume percentages and colored peak meters.");
        commands.AddRow("vp discover / vp profiles", "List connected panels / supported geometries.");
        commands.AddRow("vp scenes", "Eight layouts; five rotate with a short fade. --scene <ID> locks one.");
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
