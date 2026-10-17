// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using System.ComponentModel;
using System.Diagnostics;
using VibePanel.Contracts;

namespace VibePanel.Core;

/// <summary>Chooses a display screen without changing the user's independent preview selection.</summary>
public static class ScreenSelectionPolicy
{
    public static void Validate(IReadOnlyList<ScreenSelectionDto>? screens)
    {
        if (screens is null) { return; }
        if (screens.Count > 3) { throw new ArgumentException("Choose at most three screen categories."); }
        var categories = new HashSet<string>(StringComparer.Ordinal);
        foreach (var screen in screens)
        {
            if (screen is null || screen.Category is not ("multimedia" or "codex" or "system") || !categories.Add(screen.Category))
            { throw new ArgumentException("Each screen category must be known and appear once."); }
            if (screen.ThemeId is null || screen.ThemeId.Length > 100)
            { throw new ArgumentException("A screen theme ID must contain at most 100 characters."); }
        }
    }

    public static ThemeDefinition? Resolve(ThemeDefinition requested, IReadOnlyList<ThemeDefinition> catalog,
        IReadOnlyList<ScreenSelectionDto>? screens, bool codexProcessRunning)
    {
        Validate(screens);
        bool Available(string category) => category != "codex" || codexProcessRunning;
        if (screens is null)
        {
            return Available(requested.Category) ? requested
                : catalog.FirstOrDefault(theme => theme.Category == "multimedia")
                    ?? catalog.FirstOrDefault(theme => theme.Category == "system");
        }
        if (Available(requested.Category) && screens.Any(screen => screen.Category == requested.Category && screen.Enabled))
        { return requested; }
        foreach (var screen in screens)
        {
            if (!screen.Enabled || !Available(screen.Category)) { continue; }
            var chosen = catalog.FirstOrDefault(theme => theme.Category == screen.Category && theme.Id == screen.ThemeId)
                ?? catalog.FirstOrDefault(theme => theme.Category == screen.Category);
            if (chosen is not null) { return chosen; }
        }
        return null;
    }
}

/// <summary>Checks process names only, at most once every two seconds. No hook or account access is required.</summary>
public sealed class CodexProcessMonitor
{
    private long checkedAt;
    private bool initialized, running;

    public bool IsRunning()
    {
        var now = Environment.TickCount64;
        if (initialized && now - checkedAt < 2000) { return running; }
        checkedAt = now;
        initialized = true;
        running = false;
        try
        {
            var processes = Process.GetProcessesByName("codex");
            try { running = processes.Length > 0; }
            finally { foreach (var process in processes) { process.Dispose(); } }
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException or NotSupportedException) { }
        return running;
    }
}
