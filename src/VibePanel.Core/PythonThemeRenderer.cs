// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VibePanel.Core;

/// <summary>A persistent Python/Skia worker. It draws local previews and has no USB responsibilities.</summary>
public sealed class PythonThemeRenderer : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly ThemeEnvironment environment = new();
    private Process? worker;
    private string? sentArtworkKey;
    private byte[]? artwork;
    private string? artworkKey;
    private string lastDiagnostic = "";
    private bool disposed;

    public async Task<byte[]> RenderAsync(ThemeDefinition theme, DashboardSnapshot snapshot, DisplayProfile profile,
        bool light = false, bool ambientDrift = false, string fontFamily = "Cascadia Mono", double fontScale = 1,
        string jpegQuality = "high", CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fontFamily) || fontFamily.Length > 128 || fontFamily.Any(char.IsControl)) { throw new ArgumentException("Choose a font family of 1–128 printable characters."); }
        if (!double.IsFinite(fontScale) || fontScale is < 0.65 or > 1.6) { throw new ArgumentException("Font scale must be between 0.65 and 1.6."); }
        if (jpegQuality is not ("low" or "medium" or "high")) { throw new ArgumentException("JPEG quality must be low, medium or high."); }
        await gate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            EnsureWorker();
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(12));
            var token = deadline.Token;
            var id = Guid.NewGuid().ToString("N");
            var media = snapshot.Media;
            if (!ReferenceEquals(artwork, media.Artwork))
            {
                artwork = media.Artwork;
                artworkKey = artwork is null ? null : Convert.ToHexString(SHA256.HashData(artwork));
            }
            var mediaState = new Dictionary<string, object?>
            {
                ["title"] = media.Title,
                ["artist"] = media.Artist,
                ["album"] = media.Album,
                ["source"] = media.Source,
                ["playing"] = media.Playing,
                ["playbackStatus"] = media.PlaybackStatus,
                ["positionSeconds"] = media.PositionSeconds,
                ["durationSeconds"] = media.DurationSeconds,
                ["coverKey"] = artworkKey,
                ["quality"] = null,
                ["shuffle"] = media.Shuffle
            };
            if (sentArtworkKey != artworkKey || artworkKey is null) { mediaState["coverBase64"] = artwork is null ? null : Convert.ToBase64String(artwork); }
            var matchedVolumes = snapshot.Volumes.Where(volume => volume.Name.Length >= 3 && media.Source.Contains(volume.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
            // A source can expose multiple audio sessions; do not pass a different app's gain as media volume.
            var mediaVolume = matchedVolumes.Length == 1 ? matchedVolumes[0] : null;
            var now = DateTimeOffset.Now;
            var request = new
            {
                version = 1,
                id,
                method = "render",
                themePath = theme.DirectoryPath,
                width = profile.Width,
                height = profile.Height,
                light,
                ambientDrift,
                fontFamily,
                fontScale,
                jpegQuality,
                state = new
                {
                    media = mediaState,
                    audio = new
                    {
                        volumePercent = mediaVolume?.Percent,
                        peak = mediaVolume?.Peak,
                        muted = mediaVolume?.Muted,
                        analysis = snapshot.Playback,
                        sessions = snapshot.Volumes.Select(volume => new { name = volume.Name, volumePercent = volume.Percent, muted = volume.Muted, peak = volume.Peak })
                    },
                    clock = new { iso = now.ToString("O", CultureInfo.InvariantCulture), locale = CultureInfo.CurrentCulture.Name },
                    desktop = new { palette = environment.DesktopPalette() },
                    codex = environment.CodexActivity()
                }
            };
            try
            {
                await worker!.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request, Json).AsMemory(), token);
                await worker.StandardInput.FlushAsync(token);
                var line = await ReadResponseAsync(worker.StandardOutput.BaseStream, token);
                using var response = JsonDocument.Parse(line);
                var root = response.RootElement;
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("version", out var version)
                    || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number) || number != 1)
                { throw new IOException("Python worker response uses an unsupported protocol version."); }
                if (!root.TryGetProperty("id", out var responseId) || responseId.ValueKind != JsonValueKind.String || responseId.GetString() != id)
                { throw new IOException("Python worker response ID did not match."); }
                if (!root.TryGetProperty("ok", out var ok) || ok.ValueKind != JsonValueKind.True)
                { throw new IOException(root.TryGetProperty("error", out var error) ? error.GetString() : "Python theme failed."); }
                var jpeg = Convert.FromBase64String(root.GetProperty("jpegBase64").GetString() ?? "");
                if (jpeg.Length is < 4 or > 450_000 || jpeg[0] != 0xff || jpeg[1] != 0xd8 || jpeg[^2] != 0xff || jpeg[^1] != 0xd9)
                { throw new IOException("Python theme returned an invalid or oversized JPEG."); }
                sentArtworkKey = artworkKey;
                return jpeg;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            { StopWorker(); throw new TimeoutException("Theme rendering exceeded 12 seconds. The worker will restart on the next request."); }
            catch { StopWorker(); throw; }
        }
        finally { gate.Release(); }
    }

    private void EnsureWorker()
    {
        if (worker is { HasExited: false }) { return; }
        StopWorker();
        var runtime = ThemeCatalog.FindContentDirectory("theme-runtime") ?? throw new IOException("The theme-runtime folder is missing beside the engine.");
        var script = Path.Combine(runtime, "worker.py");
        if (!File.Exists(script)) { throw new IOException("The theme-runtime/worker.py file is missing."); }
        var bundledPython = Path.Combine(AppContext.BaseDirectory, "python", OperatingSystem.IsWindows() ? "python.exe" : "bin/python3");
        var configured = Environment.GetEnvironmentVariable("VIBEPANEL_PYTHON");
        var executable = !string.IsNullOrWhiteSpace(configured) ? configured : File.Exists(bundledPython) ? bundledPython : OperatingSystem.IsWindows() ? "python" : "python3";
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = runtime
        };
        start.ArgumentList.Add("-u");
        start.ArgumentList.Add(script);
        start.Environment["PYTHONIOENCODING"] = "utf-8";
        start.Environment["PYTHONDONTWRITEBYTECODE"] = "1";
        lastDiagnostic = "";
        try { worker = Process.Start(start) ?? throw new IOException("Python could not start."); }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or IOException)
        { throw new IOException("Python theme runtime unavailable. Set VIBEPANEL_PYTHON to Python 3.11+ with skia-python, or include python/python.exe in the portable folder. " + error.Message, error); }
        _ = ReadDiagnosticsAsync(worker);
    }

    private async Task<string> ReadResponseAsync(Stream stream, CancellationToken cancellationToken)
    {
        // Read pipe chunks directly: ReadLineAsync can allocate an unbounded unterminated line.
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0) { throw new IOException("Python worker closed its output. " + lastDiagnostic); }
            var newline = Array.IndexOf(buffer, (byte)'\n', 0, read);
            var count = newline >= 0 ? newline : read;
            if (output.Length + count > 900_000) { throw new IOException("Python worker response exceeds the frame limit."); }
            output.Write(buffer, 0, count);
            if (newline < 0) { continue; }
            for (var index = newline + 1; index < read; index++)
            { if (buffer[index] != (byte)'\r' && buffer[index] != (byte)'\n') { throw new IOException("Unexpected extra data in Python worker output."); } }
            return new UTF8Encoding(false, true).GetString(output.GetBuffer(), 0, checked((int)output.Length)).TrimEnd('\r');
        }
    }

    private async Task ReadDiagnosticsAsync(Process process)
    {
        try
        {
            var buffer = new byte[1024];
            while (true)
            {
                var read = await process.StandardError.BaseStream.ReadAsync(buffer.AsMemory());
                if (read == 0) { break; }
                // Theme prints are redirected here; a long unterminated print must not grow an unbounded line buffer.
                if (ReferenceEquals(worker, process))
                { Volatile.Write(ref lastDiagnostic, Encoding.UTF8.GetString(buffer, Math.Max(0, read - 512), Math.Min(read, 512)).Trim()); }
            }
        }
        catch (Exception error) when (error is IOException or InvalidOperationException or ObjectDisposedException) { }
    }

    private void StopWorker()
    {
        var process = worker;
        worker = null;
        sentArtworkKey = null;
        if (process is null) { return; }
        try { if (!process.HasExited) { process.Kill(entireProcessTree: true); } }
        catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception) { }
        process.Dispose();
    }

    public void Dispose() { disposed = true; StopWorker(); }
}
