// VBWR B
// Project: ThermalrightVibeWare | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using System.Text.Json;
using ThermalrightVibeWare.Contracts;
using ThermalrightVibeWare.Core;

internal static class ScreenChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var music = new ThemeDefinition("music", "Music", "multimedia", "", "fixture", "unused");
        var codex = new ThemeDefinition("codex", "Codex", "codex", "", "fixture", "unused");
        var system = new ThemeDefinition("system", "System", "system", "", "fixture", "unused");
        ThemeDefinition[] catalog = [music, codex, system];
        ScreenSelectionDto[] choices = [new("multimedia", music.Id), new("codex", codex.Id), new("system", system.Id)];
        check(ScreenSelectionPolicy.Resolve(codex, catalog, choices, false) == music, "closed Codex falls back to an enabled screen");
        check(ScreenSelectionPolicy.Resolve(codex, catalog, choices, true) == codex, "running Codex remains eligible");
        check(ScreenSelectionPolicy.Resolve(codex, catalog, [new("codex", codex.Id, false), new("system", system.Id)], true) == system,
            "disabled Codex stays excluded even when its process runs");
        check(ScreenSelectionPolicy.Resolve(music, catalog, [new("system", system.Id)], false) == system,
            "a disabled selected category uses the chosen enabled category");
        check(ScreenSelectionPolicy.Resolve(codex, catalog, [new("codex", codex.Id)], false) is null,
            "only offline Codex leaves no active screen and requires a blank frame");
        check(ScreenSelectionPolicy.Resolve(music, catalog, [], true) is null, "all screens disabled leaves no active screen");
        check(ScreenSelectionPolicy.Resolve(codex, catalog, null, false) == music, "older clients also exclude offline Codex from output");
        check(ScreenSelectionPolicy.Resolve(music, catalog, [new("system", "removed-theme")], false) == system,
            "a removed theme falls back within its enabled category");
        var rejected = false;
        try { ScreenSelectionPolicy.Validate([new("codex", "codex"), new("codex", "codex")]); }
        catch (ArgumentException) { rejected = true; }
        check(rejected, "duplicate categories are rejected before display selection");
        var sample = new HardwareSensorSnapshot(new("CPU", 0, null, null), [], new(32, 0, 0), DateTimeOffset.UnixEpoch);
        var json = JsonSerializer.SerializeToElement(sample, PanelContract.Json);
        check(json.GetProperty("cpu").GetProperty("usagePercent").GetDouble() == 0
            && json.GetProperty("cpu").GetProperty("clockMHz").ValueKind == JsonValueKind.Null,
            "sensor JSON keeps real zero separate from unavailable readings");
        check(json.GetProperty("memory").GetProperty("totalGiB").GetDouble() == 32
            && !json.GetProperty("memory").TryGetProperty("swap", out _), "memory contract contains physical RAM and no swap");
    }
}
