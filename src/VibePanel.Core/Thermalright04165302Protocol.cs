// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using System.Buffers.Binary;

namespace VibePanel.Core;

/// <summary>Defines the independently documented wire constants for Thermalright USB ID 0416:5302.</summary>
public static class Thermalright04165302Protocol
{
    /// <summary>Gets the vendor ID advertised by the supported panel family.</summary>
    public const ushort VendorId = 0x0416;

    /// <summary>Gets the product ID advertised by the currently connected panel family.</summary>
    public const ushort ProductId = 0x5302;

    /// <summary>Gets the documented bulk OUT endpoint.</summary>
    public const byte WriteEndpoint = 0x02;

    /// <summary>Gets the documented bulk IN endpoint.</summary>
    public const byte ReadEndpoint = 0x81;

    /// <summary>Gets the fixed transfer size required by the discovery handshake.</summary>
    public const int TransferSize = 512;

    /// <summary>Gets the device-information command identifier.</summary>
    public const byte DeviceInfoCommand = 0x01;

    /// <summary>Gets the picture command identifier.</summary>
    public const byte PictureCommand = 0x02;

    /// <summary>Gets the documented device-present byte in a successful reply.</summary>
    public const byte DevicePresentFlag = 0x10;

    /// <summary>Gets the four-byte protocol preamble.</summary>
    public static ReadOnlySpan<byte> Magic => [0xDA, 0xDB, 0xDC, 0xDD];

    /// <summary>Creates the non-destructive device-information request packet.</summary>
    public static byte[] CreateDeviceInfoRequest()
    {
        var request = new byte[TransferSize];
        Magic.CopyTo(request);
        request[12] = DeviceInfoCommand;
        return request;
    }

    /// <summary>Creates an experimental RGB565 picture packet for an explicitly selected profile.</summary>
    public static byte[] CreatePicturePacket(DisplayProfile profile, PanelProbeResult probe)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(probe);

        var packet = new byte[checked(20 + profile.FrameBytes)];
        Magic.CopyTo(packet);
        packet[4] = probe.ProductMode;
        packet[5] = probe.SubMode;
        packet[12] = PictureCommand;
        BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(16, 4), profile.FrameBytes);
        FillDiagnosticPattern(packet.AsSpan(20), profile);
        return packet;
    }

    private static void FillDiagnosticPattern(Span<byte> pixels, DisplayProfile profile)
    {
        for (var y = 0; y < profile.Height; y++)
        {
            for (var x = 0; x < profile.Width; x++)
            {
                var red = (byte)(x * 31 / Math.Max(profile.Width - 1, 1));
                var green = (byte)(y * 63 / Math.Max(profile.Height - 1, 1));
                var blue = (byte)(((x / 24 + y / 24) % 2) == 0 ? 31 : 0);
                var rgb565 = (ushort)((red << 11) | (green << 5) | blue);
                BinaryPrimitives.WriteUInt16BigEndian(pixels.Slice((y * profile.Width + x) * 2, 2), rgb565);
            }
        }
    }
}
