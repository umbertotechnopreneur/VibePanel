// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using System.Windows;
using VibePanel.Contracts;

namespace VibePanel.App;

public sealed partial class App : Application
{
    private readonly MainPage page;

    public App(IPanelClient client, IStudioWindowActions? windowActions = null, bool restoreLastSession = false, string? presentationSettingsPath = null, IStudioPreview? preview = null)
    {
        InitializeComponent();
        page = new MainPage(client, windowActions, restoreLastSession, presentationSettingsPath, preview);
        RootVisual = page;
    }

    /// <summary>Marshal a native title-bar close request onto the presentation dispatcher.</summary>
    public void RequestClose() => page.Dispatcher.BeginInvoke(page.ShowCloseChoice);
    public void RequestAbout() => page.Dispatcher.BeginInvoke(page.ShowAbout);
    public void RequestExit() => page.Dispatcher.BeginInvoke(page.ExitApplication);
}
