// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using VibePanel.Core;

return await VibePanelCli.RunAsync(args);

internal static class VibePanelCli
{
    /// <summary>Runs the small, explicit command surface for panel discovery and control.</summary>
    public static Task<int> RunAsync(string[] args)
    {
        try
        {
            return Task.FromResult(args.FirstOrDefault()?.ToLowerInvariant() switch
            {
                "discover" => Discover(),
                "profiles" => Profiles(),
                "probe" => Probe(args),
                "pattern" => Pattern(args),
                "help" or "--help" or "-h" or null => Help(),
                _ => Fail("I do not know that command. Run 'vp help'.")
            });
        }
        catch (Exception exception)
        {
            return Task.FromResult(Fail(exception.Message));
        }
    }

    private static int Discover()
    {
        var devices = WinUsbDisplayDiscovery.FindSupportedDevices();
        Console.WriteLine("VibePanel — my compatible USB panels");
        if (devices.Count == 0)
        {
            var attached = WinUsbDisplayDiscovery.FindAttachedPanels();
            if (attached.Count == 0)
            {
                Console.WriteLine("No 0416:5302 panel is connected.");
                return 1;
            }

            Console.WriteLine($"I found {attached.Count} matching panel(s), but Windows has not exposed a user-mode WinUSB path yet.");
            Console.WriteLine("I did not change the driver or registry. Direct control stays unavailable until a deliberate setup step.");
            return 0;
        }

        for (var index = 0; index < devices.Count; index++)
        {
            Console.WriteLine($"{index + 1}. {devices[index].Id}");
        }

        return 0;
    }

    private static int Profiles()
    {
        Console.WriteLine("VibePanel — included screen profiles");
        foreach (var profile in DisplayProfileCatalog.All)
        {
            Console.WriteLine($"{profile.Id,-20} {profile.Width} x {profile.Height}  RGB565");
        }

        Console.WriteLine("Custom geometry: --width <pixels> --height <pixels>");
        return 0;
    }

    private static int Probe(IReadOnlyList<string> args)
    {
        var device = GetDevice(args);
        Console.WriteLine($"Checking my {device.Id} panel. This sends only the device-information handshake.");
        using var transport = WinUsbPanelTransport.Open(device);
        var probe = transport.Probe();
        Console.WriteLine(probe.Message);
        Console.WriteLine($"Mode {probe.ProductMode:X2} / {probe.SubMode:X2} · serial {probe.Serial}");
        return probe.IsCompatible ? 0 : 2;
    }

    private static int Pattern(IReadOnlyList<string> args)
    {
        if (!args.Contains("--i-understand", StringComparer.Ordinal))
        {
            return Fail("A pattern changes the physical screen. Add --i-understand after checking the selected device and profile.");
        }

        if (args.Count < 3)
        {
            return Fail("Usage: vp pattern <device-number> <profile> --i-understand");
        }

        var device = GetDevice(args);
        var profile = GetProfile(args);
        Console.WriteLine($"Sending my diagnostic pattern to {device.Id} using {profile.Name}.");
        using var transport = WinUsbPanelTransport.Open(device);
        var probe = transport.Probe();
        if (!probe.IsCompatible)
        {
            return Fail("The panel did not confirm the documented handshake. No frame was sent.");
        }

        transport.SendDiagnosticPattern(profile, probe);
        Console.WriteLine("Frame submitted. Look at the physical panel to confirm the selected profile is correct.");
        return 0;
    }

    private static DisplayDevice GetDevice(IReadOnlyList<string> args)
    {
        if (args.Count < 2 || !int.TryParse(args[1], out var requested) || requested < 1)
        {
            throw new ArgumentException("Choose a device number from 'vp discover'.");
        }

        var devices = WinUsbDisplayDiscovery.FindSupportedDevices();
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
        Console.WriteLine("VibePanel keeps my USB display panel simple.");
        Console.WriteLine();
        Console.WriteLine("vp discover");
        Console.WriteLine("vp profiles");
        Console.WriteLine("vp probe <device-number>");
        Console.WriteLine("vp pattern <device-number> <profile> --i-understand");
        Console.WriteLine("vp pattern <device-number> custom --width 320 --height 320 --i-understand");
        return 0;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine($"VibePanel: {message}");
        return 1;
    }
}
