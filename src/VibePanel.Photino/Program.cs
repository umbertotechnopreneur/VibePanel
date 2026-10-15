// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using OpenSilver.Photino;
using Photino.NET;

namespace VibePanel.Photino;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var client = new StdioPanelClient();
        var page = Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html");
        var window = new PhotinoWindow()
            .SetTitle("VibePanel — my music, my screen")
            .SetUseOsDefaultSize(false)
            .SetSize(1120, 650)
            .SetResizable(true)
            .SetLogVerbosity(0)
            .Center()
            .ConfigureOpenSilver(() => new App.App(client))
            .Load(page);
        window.WaitForClose();
    }
}
