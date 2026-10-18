// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VibePanel.Core;

/// <summary>Read-only playback-session volumes and peak meters. No capture clients or volume setters.</summary>
public sealed class WindowsVolumeService : IVolumeProvider
{
    private readonly object gate = new();
    private readonly List<MMDevice> endpoints = [];
    private readonly List<Session> sessions = [];
    private DateTime refreshed;
    private bool disposed;
    private sealed record Session(string Key, string Name, int ProcessId, AudioSessionControl Control);

    public IReadOnlyList<AppVolume> Read()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (DateTime.UtcNow - refreshed > TimeSpan.FromSeconds(3)) { Refresh(); }
            var result = new List<AppVolume>();
            foreach (var session in sessions)
            {
                try
                {
                    var volume = session.Control.SimpleAudioVolume;
                    result.Add(new(session.Key, session.Name, session.ProcessId, Math.Round(volume.Volume * 100), volume.Mute, Peak(session.Control)));
                }
                catch (COMException) { refreshed = DateTime.MinValue; }
            }
            return result.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }

    /// <summary>Samples existing meter handles only; no endpoint/process discovery at the UI meter rate.</summary>
    public IReadOnlyDictionary<string, double?> ReadLevels()
    {
        lock (gate)
        {
            if (disposed) { return new Dictionary<string, double?>(); }
            return sessions.ToDictionary(item => item.Key, item => Peak(item.Control));
        }
    }

    private static double? Peak(AudioSessionControl control)
    {
        try { return control.AudioMeterInformation is { } meter ? Math.Clamp(meter.MasterPeakValue, 0, 1) : null; }
        catch (COMException) { return null; }
    }

    private void Refresh()
    {
        Clear();
        using var devices = new MMDeviceEnumerator();
        foreach (var endpoint in devices.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            endpoints.Add(endpoint);
            var current = endpoint.AudioSessionManager.Sessions;
            for (var i = 0; i < current.Count; i++)
            {
                var control = current[i];
                try
                {
                    if (control.State == AudioSessionState.AudioSessionStateExpired) { control.Dispose(); continue; }
                    var pid = checked((int)control.GetProcessID);
                    string name;
                    try { using var process = Process.GetProcessById(pid); name = control.IsSystemSoundsSession || pid == 0 ? "System sounds" : process.ProcessName; }
                    catch (ArgumentException) { name = pid == 0 ? "System sounds" : $"Process {pid}"; }
                    sessions.Add(new(endpoint.ID + "|" + control.GetSessionInstanceIdentifier, name, pid, control));
                }
                catch (COMException) { control.Dispose(); }
            }
        }
        refreshed = DateTime.UtcNow;
    }

    private void Clear()
    {
        foreach (var session in sessions) { session.Control.Dispose(); }
        sessions.Clear();
        foreach (var endpoint in endpoints) { endpoint.Dispose(); }
        endpoints.Clear();
    }

    public void Dispose()
    {
        lock (gate) { if (disposed) { return; } Clear(); disposed = true; }
    }
}
