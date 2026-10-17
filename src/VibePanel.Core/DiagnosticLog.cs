// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using VibePanel.Contracts;

namespace VibePanel.Core;

/// <summary>Opt-in, bounded local diagnostics. Never writes protocol bodies or exception messages.</summary>
public static class DiagnosticLog
{
    private const long MaximumBytes = 512 * 1024;
    private static readonly object Sync = new();
    private static readonly string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VibePanel", "logs");
    public static string LogPath { get; } = Path.Combine(DirectoryPath, "engine.log");
    private static readonly string SettingsPath = Path.Combine(DirectoryPath, "diagnostics.json");
    private static bool enabled;
    private static bool initialized;

    public static DiagnosticsStatus Status()
    {
        lock (Sync) { return new(IsEnabledLocked(), LogPath); }
    }

    public static DiagnosticsStatus SetEnabled(bool value)
    {
        lock (Sync)
        {
            Directory.CreateDirectory(DirectoryPath);
            var temporary = Path.Combine(DirectoryPath, $"diagnostics.{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllText(temporary, value ? "{\"enabled\":true}\n" : "{\"enabled\":false}\n", new UTF8Encoding(false));
                File.Move(temporary, SettingsPath, overwrite: true);
            }
            finally { if (File.Exists(temporary)) { File.Delete(temporary); } }
            enabled = value;
            initialized = true;
            return new(enabled, LogPath);
        }
    }

    public static bool Open()
    {
        if (!File.Exists(LogPath)) { return false; }
        var start = new ProcessStartInfo("notepad.exe") { UseShellExecute = false, CreateNoWindow = false };
        start.ArgumentList.Add(LogPath);
        return Process.Start(start) is not null;
    }

    public static void Rpc(string method, long milliseconds, bool success, string? errorType, long submissionCount)
    {
        var name = KnownMethods.Contains(method) ? method : "unknown";
        var type = KnownErrorTypes.Contains(errorType ?? "") ? errorType : errorType is null ? null : "Other";
        Write(new
        {
            atUtc = DateTimeOffset.UtcNow,
            kind = "rpc",
            method = name,
            milliseconds = Math.Clamp(milliseconds, 0, 600_000),
            success,
            errorType = type,
            submissionCount = Math.Max(0, submissionCount)
        });
    }

    public static void ThemeRenderFailure(string? errorType)
    {
        var type = KnownErrorTypes.Contains(errorType ?? "") ? errorType : "Other";
        Write(new { atUtc = DateTimeOffset.UtcNow, kind = "theme.render", success = false, errorType = type });
    }

    private static void Write<T>(T entry)
    {
        try
        {
            lock (Sync)
            {
                if (!IsEnabledLocked()) { return; }
                Directory.CreateDirectory(DirectoryPath);
                var line = JsonSerializer.Serialize(entry, PanelContract.Json) + "\n";
                var bytes = Encoding.UTF8.GetBytes(line);
                if (bytes.Length > 1024) { return; }
                if (File.Exists(LogPath) && new FileInfo(LogPath).Length + bytes.Length > MaximumBytes)
                { File.Move(LogPath, LogPath + ".1", overwrite: true); }
                using var output = new FileStream(LogPath, FileMode.Append, FileAccess.Write, FileShare.Read);
                output.Write(bytes);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        { /* Diagnostics must never interrupt preview or USB output. */ }
    }

    private static bool IsEnabledLocked()
    {
        if (initialized) { return enabled; }
        initialized = true;
        enabled = false;
        try
        {
            var file = new FileInfo(SettingsPath);
            if (!file.Exists || file.Length > 256) { return false; }
            using var stream = file.OpenRead();
            using var settings = JsonDocument.Parse(stream);
            enabled = settings.RootElement.TryGetProperty("enabled", out var value) && value.ValueKind == JsonValueKind.True;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        { enabled = false; }
        return enabled;
    }

    private static readonly HashSet<string> KnownMethods = ["hello", "themes", "state", "present", "devices", "levels", "start", "stop", "media",
        "codex.status", "codex.prepare", "diagnostics.status", "diagnostics.set", "diagnostics.open"];
    private static readonly HashSet<string> KnownErrorTypes = ["ArgumentException", "InvalidOperationException", "IOException", "TimeoutException",
        "UnauthorizedAccessException", "FileNotFoundException", "JsonException", "OperationCanceledException", "Win32Exception"];
}
