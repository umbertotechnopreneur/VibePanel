// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace VibePanel.Core;

/// <summary>Composes a music-first frame with a bounded in-memory background cache.</summary>
public sealed class DashboardRenderer
{
    private const int MaximumBackgrounds = 12;
    private const int MaximumBackgroundBytes = 8 * 1024 * 1024;
    private readonly Dictionary<string, byte[]> backgrounds = [];
    private readonly LinkedList<string> backgroundOrder = [];
    private int backgroundBytes;
    private byte[]? lastArtwork;
    private string artworkKey = "";
    private string? wallpaper;
    private string wallpaperKey = "";
    private bool wallpaperRead;
    private Color[] artworkPalette = [];
    private readonly Dictionary<int, byte[]> shadows = [];
    internal int CachedBackgroundCount => backgrounds.Count;
    internal int CachedBackgroundBytes => backgroundBytes;

    public static string Fingerprint(DashboardSnapshot snapshot, DashboardScene scene)
    {
        var track = string.Join('|', snapshot.Media.Source, snapshot.Media.Title, snapshot.Media.Artist, snapshot.Media.Album, snapshot.Media.Playing);
        var volumes = string.Join(';', snapshot.Volumes.Select(item => $"{item.Key}:{item.Percent:F0}:{item.Muted}:{Math.Round(AudioLevel.Fraction(item.Peak) * 30)}"));
        var metrics = string.Join('|', Metric(snapshot.Metrics.CpuUsage, "%"), Metric(snapshot.Metrics.CpuTemperature, "C"),
            Metric(snapshot.Metrics.GpuUsage, "%"), Metric(snapshot.Metrics.GpuTemperature, "C"), snapshot.Metrics.GpuName, snapshot.Metrics.CpuTemperatureSource);
        return scene + ":" + (scene switch
        {
            DashboardScene.Mixer or DashboardScene.VolumeFocus => volumes,
            DashboardScene.System => metrics,
            DashboardScene.Clock => DateTime.Now.ToString("yyyyMMddHHmm") + track,
            DashboardScene.Network => snapshot.Network?.ToString() ?? "Network unavailable",
            DashboardScene.Music => track + volumes + metrics + snapshot.Network?.ToString(),
            _ => track
        });
    }

    public byte[] Render(DashboardSnapshot snapshot, DisplayProfile profile, DashboardScene scene = DashboardScene.Music, bool light = false, int? driftOffset = null)
    {
        var background = GetBackground(profile.Width, profile.Height, snapshot.Media, scene, light);
        using var bitmap = new Bitmap(profile.Width, profile.Height, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(bitmap))
        using (var backgroundStream = new MemoryStream(background, writable: false))
        using (var image = Image.FromStream(backgroundStream))
        {
            if (driftOffset is { } offset) { graphics.DrawImage(image, -32 + offset, -32 + offset / 2, bitmap.Width + 64, bitmap.Height + 64); }
            else { graphics.DrawImage(image, 0, 0, bitmap.Width, bitmap.Height); }
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            // A logical canvas makes every supported geometry usable, including square panels.
            var wide = profile.Width >= profile.Height * 1.6;
            graphics.ScaleTransform(profile.Width / (wide ? 1280f : 640f), profile.Height / (wide ? 480f : 640f));
            if (snapshot.Media.Artwork is not null && scene is DashboardScene.Music or DashboardScene.Artwork)
            {
                var bounds = scene == DashboardScene.Artwork ? new Rectangle(wide ? 60 : 160, 76, 320, 320)
                    : wide ? new Rectangle(32, 88, 280, 280) : new Rectangle(32, 76, 200, 200);
                using var shadowStream = new MemoryStream(GetCoverShadow(bounds.Width));
                using var shadow = Image.FromStream(shadowStream);
                graphics.DrawImage(shadow, bounds.X - 64, bounds.Y - 64, bounds.Width + 128, bounds.Height + 128);
            }
            if (scene == DashboardScene.Music) { Draw(graphics, snapshot, wide, light); }
            else { DrawScene(graphics, snapshot, wide, scene, light); }
        }
        return PanelFrameRenderer.Encode(bitmap, profile);
    }

    /// <summary>Blends two already-rendered, unrotated frames; only transitions pay this encoding cost.</summary>
    public static byte[] Fade(byte[] before, byte[] after, DisplayProfile profile, float progress, int jpegQuality = 95)
    {
        using var firstStream = new MemoryStream(before);
        using var lastStream = new MemoryStream(after);
        using var first = Image.FromStream(firstStream);
        using var last = Image.FromStream(lastStream);
        using var frame = new Bitmap(profile.Width, profile.Height, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(frame))
        using (var attributes = new ImageAttributes())
        {
            g.DrawImage(first, 0, 0, frame.Width, frame.Height);
            attributes.SetColorMatrix(new ColorMatrix { Matrix33 = Math.Clamp(progress, 0, 1) });
            g.DrawImage(last, new Rectangle(0, 0, frame.Width, frame.Height), 0, 0, last.Width, last.Height, GraphicsUnit.Pixel, attributes);
        }
        return PanelFrameRenderer.Encode(frame, profile, jpegQuality);
    }

    private static void DrawScene(Graphics g, DashboardSnapshot s, bool wide, DashboardScene scene, bool light)
    {
        var width = wide ? 1280 : 640;
        var height = wide ? 480 : 640;
        using var white = new SolidBrush(light ? Color.FromArgb(32, 31, 48) : Color.FromArgb(239, 240, 246));
        using var muted = new SolidBrush(light ? Color.FromArgb(76, 72, 95) : Color.FromArgb(173, 183, 204));
        using var accent = new SolidBrush(light ? Color.FromArgb(109, 61, 130) : Color.FromArgb(221, 176, 235));
        using var card = new SolidBrush(light ? Color.FromArgb(170, 255, 255, 255) : Color.FromArgb(120, 15, 20, 30));
        using var heading = new Font("Consolas", 18, FontStyle.Bold, GraphicsUnit.Pixel);
        using var large = new Font("Consolas", wide ? 60 : 44, FontStyle.Bold, GraphicsUnit.Pixel);
        using var body = new Font("Consolas", 26, FontStyle.Regular, GraphicsUnit.Pixel);
        using var format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        g.DrawString(DashboardScenes.Name(scene).ToUpperInvariant(), heading, accent, 32, 25);
        g.DrawString("VibePanel / VibeWare", heading, muted, 32, height - 40);
        if (scene == DashboardScene.Artwork)
        {
            var size = wide ? 320 : 320;
            var x = wide ? 60 : (width - size) / 2;
            g.FillRectangle(card, x, 76, size, size);
            if (s.Media.Artwork is { Length: > 0 })
            {
                try
                {
                    using var stream = new MemoryStream(s.Media.Artwork);
                    using var art = RasterImage.Open(stream);
                    var edge = Math.Min(art.Width, art.Height);
                    g.DrawImage(art, new Rectangle(x, 76, size, size), new Rectangle((art.Width - edge) / 2, (art.Height - edge) / 2, edge, edge), GraphicsUnit.Pixel);
                }
                catch (Exception error) when (error is ArgumentException or IOException or OutOfMemoryException) { g.DrawString("♪", large, accent, x + 120, 190); }
            }
            else { g.DrawString("♪", large, accent, x + 120, 190); }
            var left = wide ? 440 : 32;
            var top = wide ? 140 : 425;
            g.DrawString(s.Media.Title, large, white, new RectangleF(left, top, width - left - 32, 80), format);
            g.DrawString(s.Media.Artist, body, muted, new RectangleF(left, top + 90, width - left - 32, 40), format);
        }
        else if (scene is DashboardScene.Mixer or DashboardScene.VolumeFocus)
        {
            var volumes = scene == DashboardScene.VolumeFocus ? s.Volumes.Take(1) : s.Volumes.Take(5);
            var y = 95;
            foreach (var volume in volumes)
            {
                g.DrawString(volume.Name + (volume.Muted ? " / MUTED" : ""), body, white, new RectangleF(40, y, width - 380, 35), format);
                g.DrawString($"{volume.Percent:F0}%  /  {AudioLevel.Label(volume.Peak)}", heading, accent, width - 300, y + 8);
                DrawMeter(g, new RectangleF(40, y + 43, width - 80, 10), volume.Peak);
                y += wide ? 64 : 85;
            }
            if (s.Volumes.Count == 0) { g.DrawString("Start an audio app", body, muted, 40, 150); }
        }
        else if (scene == DashboardScene.System)
        {
            var m = s.Metrics;
            g.DrawString("CPU", heading, muted, 40, 100);
            g.DrawString(Metric(m.CpuUsage, "%"), large, white, 40, 140);
            g.DrawString(Metric(m.CpuTemperature, "°C"), body, accent, 40, 225);
            g.DrawString("GPU", heading, muted, wide ? 680 : 340, 100);
            g.DrawString(Metric(m.GpuUsage, "%"), large, white, wide ? 680 : 340, 140);
            g.DrawString(Metric(m.GpuTemperature, "°C"), body, accent, wide ? 680 : 340, 225);
            g.DrawString(m.GpuName, body, muted, new RectangleF(40, 310, width - 80, 40), format);
            g.DrawString("CPU temperature: " + m.CpuTemperatureSource, heading, muted, new RectangleF(40, 365, width - 80, 35), format);
        }
        else if (scene == DashboardScene.Network)
        {
            var network = s.Network;
            g.DrawString("DOWNLOAD", heading, muted, 40, 105);
            g.DrawString(NetworkTrafficService.Size(network?.DownloadBytesPerSecond) + "/s", large, white, 40, 145);
            g.DrawString("UPLOAD", heading, muted, wide ? 680 : 40, wide ? 105 : 280);
            g.DrawString(NetworkTrafficService.Size(network?.UploadBytesPerSecond) + "/s", large, white, wide ? 680 : 40, wide ? 145 : 320);
            g.DrawString("TOTAL ↓ " + NetworkTrafficService.Size(network?.DownloadBytes), body, accent, 40, wide ? 290 : 440);
            g.DrawString("TOTAL ↑ " + NetworkTrafficService.Size(network?.UploadBytes), body, accent, wide ? 680 : 40, wide ? 290 : 485);
            g.DrawString("Since VibePanel started · adapter counters, not billing totals", heading, muted, new RectangleF(40, height - 90, width - 80, 30), format);
        }
        else if (scene == DashboardScene.Clock)
        {
            using var clock = new Font("Consolas", wide ? 132 : 110, FontStyle.Bold, GraphicsUnit.Pixel);
            g.DrawString(DateTime.Now.ToString("HH:mm"), clock, white, 40, wide ? 90 : 130);
            g.DrawString(DateTime.Now.ToString("dddd, dd MMMM"), body, accent, 48, wide ? 255 : 310);
            g.DrawString(s.Media.Title + " / " + s.Media.Artist, heading, muted, new RectangleF(48, wide ? 330 : 415, width - 96, 40), format);
        }
        else
        {
            g.DrawString(s.Media.Title, large, white, new RectangleF(40, 120, width - 80, 80), format);
            g.DrawString(s.Media.Artist, body, accent, new RectangleF(40, 215, width - 80, 45), format);
            if (scene == DashboardScene.TrackDetails)
            {
                g.DrawString(s.Media.Album, body, muted, new RectangleF(40, 280, width - 80, 40), format);
                g.DrawString(s.Media.Playing ? "PLAYING / Windows media session" : "PAUSED / Windows media session", heading, muted, 40, 355);
            }
        }
    }

    private static void Draw(Graphics g, DashboardSnapshot s, bool wide, bool light)
    {
        using var ink = new SolidBrush(light ? Color.FromArgb(32, 31, 48) : Color.FromArgb(239, 240, 246));
        using var muted = new SolidBrush(light ? Color.FromArgb(76, 72, 95) : Color.FromArgb(189, 193, 206));
        using var accent = new SolidBrush(light ? Color.FromArgb(109, 61, 130) : Color.FromArgb(221, 176, 235));
        using var card = new SolidBrush(light ? Color.FromArgb(170, 255, 255, 255) : Color.FromArgb(135, 18, 21, 31));
        using var title = new Font("Consolas", wide ? 39 : 29, FontStyle.Bold, GraphicsUnit.Pixel);
        using var body = new Font("Consolas", 23, FontStyle.Regular, GraphicsUnit.Pixel);
        using var label = new Font("Consolas", 16, FontStyle.Bold, GraphicsUnit.Pixel);
        using var format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        g.DrawString("V I B E P A N E L", label, ink, 32, 24);
        g.DrawString(s.Media.Playing ? "NOW PLAYING" : "MY MUSIC", label, accent, wide ? 350 : 32, wide ? 92 : 294);
        var artworkBounds = wide ? new Rectangle(32, 88, 280, 280) : new Rectangle(32, 76, 200, 200);
        g.FillRectangle(card, artworkBounds);
        if (s.Media.Artwork is { Length: > 0 })
        {
            try
            {
                using var stream = new MemoryStream(s.Media.Artwork);
                using var art = RasterImage.Open(stream);
                var edge = Math.Min(art.Width, art.Height);
                g.DrawImage(art, artworkBounds, new Rectangle((art.Width - edge) / 2, (art.Height - edge) / 2, edge, edge), GraphicsUnit.Pixel);
            }
            catch (Exception error) when (error is ArgumentException or IOException or OutOfMemoryException) { g.DrawString("♪", title, accent, artworkBounds.X + 70, artworkBounds.Y + 65); }
        }
        else { g.DrawString("♪", title, accent, artworkBounds.X + 70, artworkBounds.Y + 65); }
        var textX = wide ? 350 : 32;
        var textY = wide ? 126 : 322;
        g.DrawString(s.Media.Title, title, ink, new RectangleF(textX, textY, wide ? 565 : 570, 64), format);
        g.DrawString(s.Media.Artist, body, muted, new RectangleF(textX, textY + 67, wide ? 555 : 570, 40), format);
        g.DrawString(s.Media.Playing ? "●  Playing" : "Ⅱ  Paused / ready", label, accent, textX, textY + 125);
        if (wide)
        {
            g.FillRectangle(card, 944, 80, 304, 294);
            g.DrawString("APP LEVELS", label, accent, 964, 99);
            var y = 143;
            foreach (var volume in s.Volumes.Take(4))
            {
                g.DrawString($"{volume.Name}   {(volume.Muted ? "MUTE" : $"{volume.Percent:F0}%")}", label, ink, new RectangleF(964, y, 264, 26), format);
                DrawMeter(g, new RectangleF(964, y + 32, 260, 10), volume.Peak);
                y += 55;
            }
            if (s.Volumes.Count == 0) { g.DrawString("No audio sessions", label, muted, 964, 150); }
        }
        else
        {
            g.DrawString("MIXER", label, accent, 270, 87);
            var y = 123;
            foreach (var volume in s.Volumes.Take(4))
            {
                g.DrawString($"{volume.Name}  {(volume.Muted ? "MUTE" : $"{volume.Percent:F0}%")}", label, ink, new RectangleF(270, y, 335, 30), format);
                y += 38;
            }
        }
        var bottom = wide ? 400 : 522;
        g.FillRectangle(card, 32, bottom, wide ? 1216 : 576, wide ? 49 : 69);
        g.DrawString($"CPU  {Metric(s.Metrics.CpuUsage, "%")}  ·  {Metric(s.Metrics.CpuTemperature, "°C")}", label, ink, 48, bottom + 15);
        g.DrawString($"GPU  {Metric(s.Metrics.GpuUsage, "%")}  ·  {Metric(s.Metrics.GpuTemperature, "°C")}", label, ink, wide ? 370 : 330, bottom + 15);
        if (s.Network is { } network)
        {
            g.DrawString($"↓ {NetworkTrafficService.Size(network.DownloadBytesPerSecond)}/s   ↑ {NetworkTrafficService.Size(network.UploadBytesPerSecond)}/s", label, ink, wide ? 680 : 48, bottom + (wide ? 7 : 41));
            if (wide) { g.DrawString($"Total ↓ {NetworkTrafficService.Size(network.DownloadBytes)}  ↑ {NetworkTrafficService.Size(network.UploadBytes)}", label, muted, 680, bottom + 27); }
        }
        g.DrawString("VibeWare / Umberto", label, accent, wide ? 1028 : 32, wide ? 32 : 608);
    }

    public static string Metric(double? value, string unit) => value is { } number ? $"{number:F0}{unit}" : "—";

    private static void DrawMeter(Graphics graphics, RectangleF track, double? peak)
    {
        using var background = new SolidBrush(Color.FromArgb(28, 53, 41));
        graphics.FillRectangle(background, track);
        using var gradient = new LinearGradientBrush(track, Color.Green, Color.Pink, 0f)
        {
            InterpolationColors = new ColorBlend { Colors = [Color.FromArgb(98, 199, 119), Color.FromArgb(181, 220, 106), Color.FromArgb(246, 197, 105), Color.FromArgb(224, 131, 177)], Positions = [0, 0.55f, 0.8f, 1] }
        };
        graphics.FillRectangle(gradient, track.X, track.Y, (float)(track.Width * AudioLevel.Fraction(peak)), track.Height);
    }

    private byte[] GetBackground(int width, int height, NowPlaying media, DashboardScene scene, bool light)
    {
        var cover = scene is DashboardScene.Music or DashboardScene.Artwork or DashboardScene.TrackDetails ? media.Artwork : null;
        if (cover is not null && !ReferenceEquals(lastArtwork, cover))
        { lastArtwork = cover; artworkKey = Convert.ToHexString(SHA256.HashData(cover))[..24]; artworkPalette = SamplePalette(null, cover); shadows.Clear(); }
        if (!wallpaperRead)
        {
            using var desktop = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
            wallpaper = desktop?.GetValue("WallPaper") as string;
            var modified = wallpaper is not null && File.Exists(wallpaper) ? File.GetLastWriteTimeUtc(wallpaper).Ticks : 0;
            wallpaperKey = $"{wallpaper}|{modified}";
            wallpaperRead = true;
        }
        var dimensions = $"{width}x{height}|{light}|{(cover is null ? wallpaperKey : artworkKey)}";
        if (backgrounds.TryGetValue(dimensions, out var existing))
        { backgroundOrder.Remove(dimensions); backgroundOrder.AddLast(dimensions); return existing; }
        var palette = cover is null ? SamplePalette(wallpaper, null) : artworkPalette;
        using var small = new Bitmap(256, 256);
        using (var blobs = Graphics.FromImage(small))
        {
            blobs.Clear(light ? Color.FromArgb(244, 243, 248) : Color.FromArgb(17, 21, 31));
            var centers = new[] { new PointF(35, 30), new PointF(220, 50), new PointF(45, 220), new PointF(215, 215) };
            for (var i = 0; i < palette.Length; i++)
            {
                var bounds = new RectangleF(centers[i].X - 180, centers[i].Y - 180, 360, 360);
                using var pathShape = new GraphicsPath();
                pathShape.AddEllipse(bounds);
                using var blob = new PathGradientBrush(pathShape) { CenterColor = Color.FromArgb(light ? 95 : 170, palette[i]), SurroundColors = [Color.FromArgb(0, palette[i])] };
                blobs.FillEllipse(blob, bounds);
            }
        }
        using var image = new Bitmap(width, height);
        using (var g = Graphics.FromImage(image))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(small, 0, 0, width, height);
            using var shade = new SolidBrush(light ? Color.FromArgb(85, 250, 248, 255) : Color.FromArgb(65, 13, 17, 28));
            g.FillRectangle(shade, 0, 0, width, height);
        }
        using var output = new MemoryStream();
        image.Save(output, ImageFormat.Jpeg);
        var jpeg = output.ToArray();
        if (jpeg.Length > MaximumBackgroundBytes) { return jpeg; }
        while (backgrounds.Count >= MaximumBackgrounds || backgroundBytes + jpeg.Length > MaximumBackgroundBytes)
        {
            var oldest = backgroundOrder.First!.Value;
            backgroundBytes -= backgrounds[oldest].Length;
            backgrounds.Remove(oldest);
            backgroundOrder.RemoveFirst();
        }
        backgrounds.Add(dimensions, jpeg);
        backgroundOrder.AddLast(dimensions);
        backgroundBytes += jpeg.Length;
        return jpeg;
    }

    private static Color[] SamplePalette(string? path, byte[]? artwork)
    {
        var fallback = new[] { Color.FromArgb(82, 63, 121), Color.FromArgb(40, 90, 109), Color.FromArgb(111, 61, 104), Color.FromArgb(43, 50, 76) };
        if (artwork is null && (path is null || !File.Exists(path))) { return fallback; }
        try
        {
            using var stream = artwork is null ? (Stream)File.OpenRead(path!) : new MemoryStream(artwork, writable: false);
            using var source = RasterImage.Open(stream);
            using var tiny = new Bitmap(source, 64, 64);
            if (artwork is not null)
            {
                var buckets = new Dictionary<int, (int Count, int Red, int Green, int Blue)>();
                for (var y = 0; y < 64; y++)
                {
                    for (var x = 0; x < 64; x++)
                    {
                        var pixel = tiny.GetPixel(x, y);
                        var key = ((pixel.R / 32) << 6) | ((pixel.G / 32) << 3) | (pixel.B / 32);
                        var item = buckets.GetValueOrDefault(key);
                        buckets[key] = (item.Count + 1, item.Red + pixel.R, item.Green + pixel.G, item.Blue + pixel.B);
                    }
                }
                var dominant = buckets.Values.OrderByDescending(item => item.Count).Select(item => Color.FromArgb(item.Red / item.Count, item.Green / item.Count, item.Blue / item.Count)).Take(4).ToList();
                while (dominant.Count < 4) { dominant.Add(dominant[0]); }
                return dominant.ToArray();
            }
            var colors = new List<Color>();
            foreach (var point in new[] { new Point(8, 8), new Point(40, 8), new Point(8, 40), new Point(40, 40) })
            {
                var red = 0; var green = 0; var blue = 0;
                for (var y = point.Y; y < point.Y + 16; y++)
                {
                    for (var x = point.X; x < point.X + 16; x++)
                    { var pixel = tiny.GetPixel(x, y); red += pixel.R; green += pixel.G; blue += pixel.B; }
                }
                colors.Add(Color.FromArgb(Math.Clamp(red / 256, 35, 230), Math.Clamp(green / 256, 35, 230), Math.Clamp(blue / 256 + 8, 45, 240)));
            }
            return colors.ToArray();
        }
        catch (Exception error) when (error is ArgumentException or IOException or UnauthorizedAccessException or OutOfMemoryException) { return fallback; }
    }

    /// <summary>Nominal one-pixel-per-second ping-pong drift; frames remain low-rate snapshots.</summary>
    public static int AmbientOffset()
    {
        var phase = (int)(Environment.TickCount64 / 1000 % 96);
        return (phase <= 48 ? phase : 96 - phase) - 24;
    }

    private byte[] GetCoverShadow(int edge)
    {
        if (shadows.TryGetValue(edge, out var cached)) { return cached; }
        var side = edge + 128;
        var alpha = new int[side * side];
        var temporary = new int[alpha.Length];
        for (var y = 80; y < edge + 80; y++) { Array.Fill(alpha, 195, y * side + 64, edge); }
        // Three separable box passes approximate a soft Gaussian. This runs once per artwork/size.
        const int radius = 16;
        const int diameter = radius * 2 + 1;
        for (var pass = 0; pass < 3; pass++)
        {
            for (var y = 0; y < side; y++)
            {
                var sum = 0;
                for (var x = 0; x <= radius; x++) { sum += alpha[y * side + x]; }
                for (var x = 0; x < side; x++)
                {
                    temporary[y * side + x] = sum / diameter;
                    if (x - radius >= 0) { sum -= alpha[y * side + x - radius]; }
                    if (x + radius + 1 < side) { sum += alpha[y * side + x + radius + 1]; }
                }
            }
            for (var x = 0; x < side; x++)
            {
                var sum = 0;
                for (var y = 0; y <= radius; y++) { sum += temporary[y * side + x]; }
                for (var y = 0; y < side; y++)
                {
                    alpha[y * side + x] = sum / diameter;
                    if (y - radius >= 0) { sum -= temporary[(y - radius) * side + x]; }
                    if (y + radius + 1 < side) { sum += temporary[(y + radius + 1) * side + x]; }
                }
            }
        }
        var tint = artworkPalette[0];
        var pixels = new byte[alpha.Length * 4];
        for (var i = 0; i < alpha.Length; i++) { pixels[i * 4] = tint.B; pixels[i * 4 + 1] = tint.G; pixels[i * 4 + 2] = tint.R; pixels[i * 4 + 3] = (byte)alpha[i]; }
        using var bitmap = new Bitmap(side, side, PixelFormat.Format32bppArgb);
        var data = bitmap.LockBits(new Rectangle(0, 0, side, side), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try { Marshal.Copy(pixels, 0, data.Scan0, pixels.Length); }
        finally { bitmap.UnlockBits(data); }
        using var output = new MemoryStream();
        bitmap.Save(output, ImageFormat.Png);
        cached = output.ToArray(); shadows[edge] = cached; return cached;
    }
}
