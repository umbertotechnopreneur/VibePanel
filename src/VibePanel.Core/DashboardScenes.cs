// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

namespace VibePanel.Core;

public enum DashboardScene { Music, Artwork, Mixer, System, Clock, TrackDetails, VolumeFocus, Network }

/// <summary>Eight layouts; five rotate by default, with still frames between short fades.</summary>
public static class DashboardScenes
{
    public static IReadOnlyList<DashboardScene> Rotation { get; } = [DashboardScene.Music, DashboardScene.Artwork, DashboardScene.Mixer, DashboardScene.System, DashboardScene.Network];
    public static string Name(DashboardScene scene) => scene switch
    {
        DashboardScene.Music => "Now playing",
        DashboardScene.Artwork => "Album artwork",
        DashboardScene.Mixer => "App levels",
        DashboardScene.System => "CPU & GPU",
        DashboardScene.Clock => "Ambient clock",
        DashboardScene.TrackDetails => "Track details",
        DashboardScene.VolumeFocus => "VU focus",
        DashboardScene.Network => "Network traffic",
        _ => throw new ArgumentOutOfRangeException(nameof(scene))
    };
    public static DashboardScene Parse(string value) => Enum.TryParse<DashboardScene>(value, true, out var scene) && Enum.IsDefined(scene)
        ? scene : throw new ArgumentException("Unknown scene. Run 'vp scenes'.", nameof(value));
}
