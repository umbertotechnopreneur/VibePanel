// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

namespace VibePanel.Core;

/// <summary>Represents a matching panel that Windows can see even when no user-mode WinUSB path is registered.</summary>
public sealed record DetectedPanel(string InstanceId)
{
    /// <summary>Gets the USB identifier shown to the user.</summary>
    public string Id => $"{Thermalright04165302Protocol.VendorId:X4}:{Thermalright04165302Protocol.ProductId:X4}";
}
