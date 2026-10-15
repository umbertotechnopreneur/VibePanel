// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

namespace VibePanel.Core;

/// <summary>Describes the pixel geometry and wire format used for a panel frame.</summary>
public sealed record DisplayProfile(string Id, string Name, int Width, int Height)
{
    /// <summary>Gets the required RGB565 payload size in bytes.</summary>
    public int FrameBytes => checked(Width * Height * 2);

    /// <summary>Returns a profile with caller-supplied geometry after validating safe limits.</summary>
    public static DisplayProfile CreateCustom(int width, int height)
    {
        if (width is < 32 or > 1024 || height is < 32 or > 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Panel dimensions must be between 32 and 1024 pixels.");
        }

        return new DisplayProfile($"custom-{width}x{height}", $"Custom {width} x {height}", width, height);
    }
}
