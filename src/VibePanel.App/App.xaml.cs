// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using System.Windows;
using VibePanel.Contracts;

namespace VibePanel.App;

public sealed partial class App : Application
{
    public App(IPanelClient client)
    {
        InitializeComponent();
        RootVisual = new MainPage(client);
    }
}
