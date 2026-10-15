// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

namespace VibePanel.Core;

/// <summary>Maps the Windows peak meter to a -60..0 dBFS display. Volume gain is a different number.</summary>
public static class AudioLevel
{
    public static double Fraction(double? peak) => peak is > 0 && double.IsFinite(peak.Value)
        ? Math.Clamp((20 * Math.Log10(Math.Clamp(peak.Value, 0, 1)) + 60) / 60, 0, 1) : 0;
    public static string Label(double? peak) => peak switch
    {
        null => "— dBFS",
        <= 0 => "−∞ dBFS",
        < 0.001 => "< −60 dBFS",
        _ => $"{20 * Math.Log10(Math.Clamp(peak.Value, 0.000001, 1)):F1} dBFS"
    };
}
