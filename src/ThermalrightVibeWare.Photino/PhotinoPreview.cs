// VBWR B
// Project: ThermalrightVibeWare | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E
using System.Text.Json;
using System.Runtime.InteropServices;
using Photino.NET;
using ThermalrightVibeWare.Contracts;

namespace ThermalrightVibeWare.Photino;

/// <summary>One JPEG in flight until the browser has drawn and released its decoded bitmap.</summary>
internal sealed class PhotinoPreview : IStudioPreview, IDisposable
{
    private readonly PhotinoWindow window;
    private readonly object sync = new();
    private TaskCompletionSource<PreviewSize>? pending;
    private long nextId, pendingId;
    private bool disposed;

    // Read native host visibility without asking the browser, which can pause
    // script and image callbacks while its controller is hidden or minimized.
    public bool IsVisible => !OperatingSystem.IsWindows()
        || IsWindowVisible(window.WindowHandle) && !IsIconic(window.WindowHandle);

    public PhotinoPreview(PhotinoWindow window)
    {
        this.window = window;
        window.RegisterWebMessageReceivedHandler(Receive);
    }

    public async Task<PreviewSize> ShowAsync(byte[] jpeg, bool transition)
    {
        if (jpeg.Length is 0 or > 450_000) { throw new ArgumentException("Preview JPEG exceeds the frame limit.", nameof(jpeg)); }
        var completion = new TaskCompletionSource<PreviewSize>(TaskCreationOptions.RunContinuationsAsynchronously);
        long id;
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (pending is not null) { throw new InvalidOperationException("A preview frame is already in flight."); }
            pending = completion;
            id = pendingId = ++nextId;
        }
        try
        {
            var message = JsonSerializer.Serialize(new { type = "trv.preview", version = 1, id, transition, jpeg = Convert.ToBase64String(jpeg) });
            window.Invoke(() => window.SendWebMessage(message));
            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(8)).ConfigureAwait(false);
        }
        finally { lock (sync) { if (ReferenceEquals(pending, completion)) { pending = null; } } }
    }

    public void Clear()
    {
        lock (sync) { if (disposed) { return; } pending?.TrySetCanceled(); }
        window.Invoke(() => window.SendWebMessage("{\"type\":\"trv.preview.clear\",\"version\":1}"));
    }

    private void Receive(object? sender, string message)
    {
        if (message.Length > 1024) { return; }
        try
        {
            using var json = JsonDocument.Parse(message);
            var value = json.RootElement;
            if (!value.TryGetProperty("type", out var type) || type.GetString() != "trv.preview.done") { return; }
            lock (sync)
            {
                if (disposed || pending is null || value.GetProperty("version").GetInt32() != 1 || value.GetProperty("id").GetInt64() != pendingId) { return; }
                if (value.TryGetProperty("error", out _)) { pending.TrySetException(new IOException("The browser could not show the preview frame.")); return; }
                var width = value.GetProperty("width").GetInt32();
                var height = value.GetProperty("height").GetInt32();
                if (width is < 128 or > 4096 || height is < 128 or > 4096 || (long)width * height > 8_388_608)
                { pending.TrySetException(new IOException("Invalid preview dimensions.")); return; }
                pending.TrySetResult(new(width, height));
            }
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException) { }
    }

    public void Dispose()
    {
        lock (sync) { disposed = true; pending?.TrySetCanceled(); pending = null; }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr handle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr handle);
}
