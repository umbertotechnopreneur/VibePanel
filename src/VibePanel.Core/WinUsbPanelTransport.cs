// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace VibePanel.Core;

/// <summary>Owns a short-lived WinUSB session for a selected display panel.</summary>
public sealed class WinUsbPanelTransport : IDisposable
{
    private readonly SafeFileHandle deviceHandle;
    private readonly IntPtr interfaceHandle;
    private bool disposed;

    private WinUsbPanelTransport(SafeFileHandle deviceHandle, IntPtr interfaceHandle)
    {
        this.deviceHandle = deviceHandle;
        this.interfaceHandle = interfaceHandle;
    }

    /// <summary>Opens the WinUSB interface exposed by a discovered panel.</summary>
    public static WinUsbPanelTransport Open(DisplayDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        var fileHandle = Native.CreateFile(device.DevicePath, Native.GenericRead | Native.GenericWrite, Native.FileShareRead | Native.FileShareWrite, IntPtr.Zero, Native.OpenExisting, Native.FileAttributeNormal, IntPtr.Zero);
        if (fileHandle.IsInvalid)
        {
            fileHandle.Dispose();
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not open the panel WinUSB interface.");
        }

        if (!Native.WinUsb_Initialize(fileHandle, out var winUsbHandle))
        {
            var error = Marshal.GetLastWin32Error();
            fileHandle.Dispose();
            throw new Win32Exception(error, "Could not initialize the panel WinUSB session.");
        }

        return new WinUsbPanelTransport(fileHandle, winUsbHandle);
    }

    /// <summary>Sends only the documented device-information handshake and returns the decoded reply.</summary>
    public PanelProbeResult Probe()
    {
        ThrowIfDisposed();
        var request = Thermalright04165302Protocol.CreateDeviceInfoRequest();
        WriteExact(request);
        var response = ReadExact(Thermalright04165302Protocol.TransferSize);
        return PanelProbeResult.FromResponse(response);
    }

    /// <summary>Sends an explicitly requested diagnostic RGB565 frame using the selected profile.</summary>
    public void SendDiagnosticPattern(DisplayProfile profile, PanelProbeResult probe)
    {
        ThrowIfDisposed();
        var packet = Thermalright04165302Protocol.CreatePicturePacket(profile, probe);
        WriteExact(packet);
    }

    /// <summary>Closes the underlying WinUSB and device handles.</summary>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        Native.WinUsb_Free(interfaceHandle);
        deviceHandle.Dispose();
        disposed = true;
    }

    private void WriteExact(byte[] buffer)
    {
        if (!Native.WinUsb_WritePipe(interfaceHandle, Thermalright04165302Protocol.WriteEndpoint, buffer, checked((uint)buffer.Length), out var written, IntPtr.Zero) || written != checked((uint)buffer.Length))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Panel write failed after {written} of {buffer.Length} bytes.");
        }
    }

    private byte[] ReadExact(int expectedLength)
    {
        var response = new byte[expectedLength];
        if (!Native.WinUsb_ReadPipe(interfaceHandle, Thermalright04165302Protocol.ReadEndpoint, response, checked((uint)response.Length), out var read, IntPtr.Zero))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Panel read failed.");
        }

        return read == expectedLength ? response : response[..checked((int)read)];
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}
