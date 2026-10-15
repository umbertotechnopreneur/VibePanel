// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

namespace VibePanel.Core;

/// <summary>Represents a supported USB panel exposed through its WinUSB interface.</summary>
public sealed record DisplayDevice(string DevicePath, ushort VendorId, ushort ProductId)
{
    /// <summary>Gets a compact identifier suitable for CLI and UI output.</summary>
    public string Id => $"{VendorId:X4}:{ProductId:X4}";
}
