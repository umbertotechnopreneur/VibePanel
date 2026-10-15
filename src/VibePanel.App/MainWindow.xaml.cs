// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using VibePanel.Core;
using Windows.Graphics;

namespace VibePanel.App;

/// <summary>Provides the compact desktop surface for discovery, checks, and diagnostic frames.</summary>
public sealed partial class MainWindow : Window
{
    /// <summary>Initializes the window with the VibeWare visual language and safe default state.</summary>
    public MainWindow()
    {
        InitializeComponent();
        SystemBackdrop = new MicaBackdrop();
        AppWindow.Resize(new SizeInt32(820, 620));
        ProfilesBox.ItemsSource = DisplayProfileCatalog.All;
        ProfilesBox.SelectedIndex = 2;
    }

    private void Refresh_Click(object sender, RoutedEventArgs args)
    {
        var devices = WinUsbDisplayDiscovery.FindSupportedDevices();
        DevicesList.ItemsSource = devices;
        DevicesList.SelectedIndex = devices.Count > 0 ? 0 : -1;
        if (devices.Count > 0)
        {
            StatusText.Text = $"I found {devices.Count} compatible panel(s).";
            return;
        }

        var attached = WinUsbDisplayDiscovery.FindAttachedPanels();
        StatusText.Text = attached.Count > 0
            ? "I can see the panel, but Windows has not exposed its WinUSB path. I changed nothing."
            : "I did not find a compatible 0416:5302 panel.";
    }

    private void Probe_Click(object sender, RoutedEventArgs args)
    {
        if (!TryGetSelectedDevice(out var device))
        {
            return;
        }

        using var transport = WinUsbPanelTransport.Open(device);
        var probe = transport.Probe();
        StatusText.Text = $"{probe.Message} Mode {probe.ProductMode:X2}/{probe.SubMode:X2}.";
    }

    private void Pattern_Click(object sender, RoutedEventArgs args)
    {
        if (!TryGetSelectedDevice(out var device) || ProfilesBox.SelectedItem is not DisplayProfile profile)
        {
            return;
        }

        using var transport = WinUsbPanelTransport.Open(device);
        var probe = transport.Probe();
        if (!probe.IsCompatible)
        {
            StatusText.Text = "The panel did not confirm the documented handshake. I sent no frame.";
            return;
        }

        transport.SendDiagnosticPattern(profile, probe);
        StatusText.Text = $"I submitted the {profile.Name} diagnostic frame. Check the physical panel now.";
    }

    private bool TryGetSelectedDevice(out DisplayDevice device)
    {
        if (DevicesList.SelectedItem is DisplayDevice selected)
        {
            device = selected;
            return true;
        }

        StatusText.Text = "Choose a panel first.";
        device = null!;
        return false;
    }
}
