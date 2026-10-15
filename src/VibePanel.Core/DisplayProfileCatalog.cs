// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

namespace VibePanel.Core;

/// <summary>Provides conservative starting profiles instead of assuming every panel is 360 by 360.</summary>
public static class DisplayProfileCatalog
{
    private static readonly IReadOnlyList<DisplayProfile> Profiles =
    [
        new("square-240", "Square 240 x 240", 240, 240),
        new("square-320", "Square 320 x 320", 320, 320),
        new("square-360", "Square 360 x 360", 360, 360),
        new("square-480", "Square 480 x 480", 480, 480),
        new("portrait-240x320", "Portrait 240 x 320", 240, 320),
        new("landscape-320x240", "Landscape 320 x 240", 320, 240)
    ];

    /// <summary>Gets all included profiles.</summary>
    public static IReadOnlyList<DisplayProfile> All => Profiles;

    /// <summary>Finds a built-in profile by its stable CLI identifier.</summary>
    public static DisplayProfile GetRequired(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return Profiles.FirstOrDefault(profile => string.Equals(profile.Id, id, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"Unknown profile '{id}'. Run 'vp profiles' to list the included profiles.", nameof(id));
    }
}
