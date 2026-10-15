// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

namespace VibePanel.Core;

/// <summary>Contains the decoded reply to the documented device-information handshake.</summary>
public sealed record PanelProbeResult(bool IsCompatible, byte ProductMode, byte SubMode, string Serial, string Message)
{
    /// <summary>Creates a result from the 512-byte reply used by the 0416:5302 panel profile.</summary>
    public static PanelProbeResult FromResponse(ReadOnlySpan<byte> response)
    {
        if (response.Length != Thermalright04165302Protocol.TransferSize)
        {
            return new PanelProbeResult(false, 0, 0, string.Empty, $"Expected a 512-byte reply but received {response.Length} bytes.");
        }

        var compatible = response[..4].SequenceEqual(Thermalright04165302Protocol.Magic)
            && response[12] == Thermalright04165302Protocol.DeviceInfoCommand
            && response[16] == Thermalright04165302Protocol.DevicePresentFlag;

        var serial = Convert.ToHexString(response.Slice(20, 16));
        var message = compatible
            ? "The panel accepted the documented device-information handshake."
            : "The panel replied, but the response did not match the documented 0416:5302 handshake.";

        return new PanelProbeResult(compatible, response[4], response[5], serial, message);
    }
}
