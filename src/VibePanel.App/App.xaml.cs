// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using Microsoft.UI.Xaml;

namespace VibePanel.App;

/// <summary>Starts the VibePanel desktop application.</summary>
public partial class App : Application
{
    private Window? window;
    /// <summary>Initializes the application instance.</summary>
    public App()
    {
        InitializeComponent();
    }

    /// <summary>Creates the main window when the application launches.</summary>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        window = new MainWindow();
        window.Activate();
    }
}
