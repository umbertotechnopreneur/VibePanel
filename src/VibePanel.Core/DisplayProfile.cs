// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

namespace VibePanel.Core;

/// <summary>Describes the pixel geometry and wire format used for a panel frame.</summary>
public sealed record DisplayProfile(string Id, string Name, int Width, int Height,
    PanelPixelEncoding Encoding = PanelPixelEncoding.Rgb565BigEndian, int WireRotation = 0)
{
    /// <summary>Gets the required RGB565 payload size in bytes.</summary>
    public int FrameBytes => checked(Width * Height * 2);

    /// <summary>Returns a profile with caller-supplied geometry after validating safe limits.</summary>
    public static DisplayProfile CreateCustom(int width, int height, PanelPixelEncoding encoding = PanelPixelEncoding.Rgb565BigEndian)
    {
        if (width is < 32 or > 2048 || height is < 32 or > 2048)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Panel dimensions must be between 32 and 2048 pixels.");
        }

        return new DisplayProfile($"custom-{width}x{height}", $"Custom {width} x {height}", width, height, encoding);
    }
}

/// <summary>Specifies the on-wire picture encoding; geometry alone cannot select a compatible encoding.</summary>
public enum PanelPixelEncoding
{
    Rgb565BigEndian,
    Rgb565LittleEndian,
    Jpeg
}
