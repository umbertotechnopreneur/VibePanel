// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace VibePanel.Core;

/// <summary>Composes a music-first frame on a wallpaper palette cached as a JPEG, not sampled every tick.</summary>
public sealed class DashboardRenderer
{
    private readonly Dictionary<string, string> backgrounds = [];
    public string? BackgroundPath { get; private set; }

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
            DashboardScene.Music => track + volumes + metrics,
            _ => track
        });
    }

    public byte[] Render(DashboardSnapshot snapshot, DisplayProfile profile, DashboardScene scene = DashboardScene.Music)
    {
        var background = GetBackground(profile.Width, profile.Height);
        using var bitmap = new Bitmap(profile.Width, profile.Height, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(bitmap))
        using (var image = Image.FromFile(background))
        {
            graphics.DrawImage(image, 0, 0, bitmap.Width, bitmap.Height);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            // A logical canvas makes every supported geometry usable, including square panels.
            var wide = profile.Width >= profile.Height * 1.6;
            graphics.ScaleTransform(profile.Width / (wide ? 1280f : 640f), profile.Height / (wide ? 480f : 640f));
            if (scene == DashboardScene.Music) { Draw(graphics, snapshot, wide); }
            else { DrawScene(graphics, snapshot, wide, scene); }
        }
        return PanelFrameRenderer.Encode(bitmap, profile);
    }

    /// <summary>Blends two already-rendered, unrotated frames; only transitions pay this encoding cost.</summary>
    public static byte[] Fade(byte[] before, byte[] after, DisplayProfile profile, float progress)
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
        return PanelFrameRenderer.Encode(frame, profile);
    }

    private static void DrawScene(Graphics g, DashboardSnapshot s, bool wide, DashboardScene scene)
    {
        var width = wide ? 1280 : 640;
        var height = wide ? 480 : 640;
        using var white = new SolidBrush(Color.FromArgb(239, 240, 246));
        using var muted = new SolidBrush(Color.FromArgb(173, 183, 204));
        using var accent = new SolidBrush(Color.FromArgb(221, 176, 235));
        using var card = new SolidBrush(Color.FromArgb(120, 15, 20, 30));
        using var heading = new Font("Segoe UI", 18, FontStyle.Bold, GraphicsUnit.Pixel);
        using var large = new Font("Segoe UI", wide ? 60 : 44, FontStyle.Bold, GraphicsUnit.Pixel);
        using var body = new Font("Segoe UI", 26, FontStyle.Regular, GraphicsUnit.Pixel);
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
                    using var art = Image.FromStream(stream);
                    var edge = Math.Min(art.Width, art.Height);
                    g.DrawImage(art, new Rectangle(x, 76, size, size), new Rectangle((art.Width - edge) / 2, (art.Height - edge) / 2, edge, edge), GraphicsUnit.Pixel);
                }
                catch (ArgumentException) { g.DrawString("♪", large, accent, x + 120, 190); }
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
                g.DrawString(volume.Name + (volume.Muted ? " / MUTED" : ""), body, white, new RectangleF(40, y, width - 210, 35), format);
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
        else if (scene == DashboardScene.Clock)
        {
            using var clock = new Font("Segoe UI", wide ? 132 : 110, FontStyle.Bold, GraphicsUnit.Pixel);
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

    private static void Draw(Graphics g, DashboardSnapshot s, bool wide)
    {
        using var muted = new SolidBrush(Color.FromArgb(189, 193, 206));
        using var accent = new SolidBrush(Color.FromArgb(221, 176, 235));
        using var card = new SolidBrush(Color.FromArgb(135, 18, 21, 31));
        using var title = new Font("Segoe UI", wide ? 39 : 29, FontStyle.Bold, GraphicsUnit.Pixel);
        using var body = new Font("Segoe UI", 23, FontStyle.Regular, GraphicsUnit.Pixel);
        using var label = new Font("Segoe UI", 16, FontStyle.Bold, GraphicsUnit.Pixel);
        using var format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        g.DrawString("VIBE PANEL", label, Brushes.White, 32, 24);
        g.DrawString(s.Media.Playing ? "NOW PLAYING" : "MY MUSIC", label, accent, wide ? 350 : 32, wide ? 92 : 294);
        var artworkBounds = wide ? new Rectangle(32, 88, 280, 280) : new Rectangle(32, 76, 200, 200);
        g.FillRectangle(card, artworkBounds);
        if (s.Media.Artwork is { Length: > 0 })
        {
            try
            {
                using var stream = new MemoryStream(s.Media.Artwork);
                using var art = Image.FromStream(stream);
                var edge = Math.Min(art.Width, art.Height);
                g.DrawImage(art, artworkBounds, new Rectangle((art.Width - edge) / 2, (art.Height - edge) / 2, edge, edge), GraphicsUnit.Pixel);
            }
            catch (ArgumentException) { g.DrawString("♪", title, accent, artworkBounds.X + 70, artworkBounds.Y + 65); }
        }
        else { g.DrawString("♪", title, accent, artworkBounds.X + 70, artworkBounds.Y + 65); }
        var textX = wide ? 350 : 32;
        var textY = wide ? 126 : 322;
        g.DrawString(s.Media.Title, title, Brushes.White, new RectangleF(textX, textY, wide ? 565 : 570, 64), format);
        g.DrawString(s.Media.Artist, body, muted, new RectangleF(textX, textY + 67, wide ? 555 : 570, 40), format);
        g.DrawString(s.Media.Playing ? "●  Playing" : "Ⅱ  Paused / ready", label, accent, textX, textY + 125);
        if (wide)
        {
            g.FillRectangle(card, 944, 80, 304, 294);
            g.DrawString("APP LEVELS", label, accent, 964, 99);
            var y = 143;
            foreach (var volume in s.Volumes.Take(4))
            {
                g.DrawString($"{volume.Name}   {(volume.Muted ? "MUTE" : $"{volume.Percent:F0}%")}", label, Brushes.White, new RectangleF(964, y, 264, 26), format);
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
                g.DrawString($"{volume.Name}  {(volume.Muted ? "MUTE" : $"{volume.Percent:F0}%")}", label, Brushes.White, new RectangleF(270, y, 335, 30), format);
                y += 38;
            }
        }
        var bottom = wide ? 400 : 522;
        g.FillRectangle(card, 32, bottom, wide ? 1216 : 576, wide ? 49 : 69);
        g.DrawString($"CPU  {Metric(s.Metrics.CpuUsage, "%")}  ·  {Metric(s.Metrics.CpuTemperature, "°C")}", label, Brushes.White, 48, bottom + 15);
        g.DrawString($"GPU  {Metric(s.Metrics.GpuUsage, "%")}  ·  {Metric(s.Metrics.GpuTemperature, "°C")}", label, Brushes.White, wide ? 370 : 330, bottom + 15);
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

    private string GetBackground(int width, int height)
    {
        var dimensions = $"{width}x{height}";
        if (backgrounds.TryGetValue(dimensions, out var existing)) { BackgroundPath = existing; return existing; }
        using var desktop = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
        var wallpaper = desktop?.GetValue("WallPaper") as string;
        var modified = wallpaper is not null && File.Exists(wallpaper) ? File.GetLastWriteTimeUtc(wallpaper).Ticks : 0;
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"v1|{wallpaper}|{modified}|{dimensions}")))[..24];
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VibePanel", "cache");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"background-{key}.jpg");
        if (!File.Exists(path))
        {
            var palette = SamplePalette(wallpaper);
            using var image = new Bitmap(width, height);
            using (var g = Graphics.FromImage(image))
            {
                using var gradient = new LinearGradientBrush(new Rectangle(0, 0, width, height), palette[0], palette[3], 25f)
                {
                    InterpolationColors = new ColorBlend { Colors = palette, Positions = [0f, 0.35f, 0.7f, 1f] }
                };
                g.FillRectangle(gradient, 0, 0, width, height);
                using var shade = new SolidBrush(Color.FromArgb(155, 13, 17, 28));
                g.FillRectangle(shade, 0, 0, width, height);
            }
            using var output = new MemoryStream();
            image.Save(output, ImageFormat.Jpeg);
            try
            {
                using var file = new FileStream(path, FileMode.CreateNew);
                file.Write(output.ToArray());
            }
            catch (IOException) when (File.Exists(path)) { }
        }
        backgrounds[dimensions] = path;
        BackgroundPath = path;
        return path;
    }

    private static Color[] SamplePalette(string? path)
    {
        var fallback = new[] { Color.FromArgb(82, 63, 121), Color.FromArgb(40, 90, 109), Color.FromArgb(111, 61, 104), Color.FromArgb(43, 50, 76) };
        if (path is null || !File.Exists(path)) { return fallback; }
        try
        {
            using var source = Image.FromFile(path);
            using var tiny = new Bitmap(source, 64, 64);
            var histogram = new Dictionary<int, int>();
            for (var y = 0; y < 64; y++)
            {
                for (var x = 0; x < 64; x++)
                {
                    var color = tiny.GetPixel(x, y);
                    var bucket = ((color.R / 32) << 6) | ((color.G / 32) << 3) | (color.B / 32);
                    histogram[bucket] = histogram.GetValueOrDefault(bucket) + 1;
                }
            }
            var selected = new List<Color>();
            foreach (var bucket in histogram.OrderByDescending(item => item.Value).Select(item => item.Key))
            {
                var color = Color.FromArgb(((bucket >> 6) & 7) * 32 + 16, ((bucket >> 3) & 7) * 32 + 16, (bucket & 7) * 32 + 16);
                if (selected.All(previous => Math.Abs(previous.R - color.R) + Math.Abs(previous.G - color.G) + Math.Abs(previous.B - color.B) > 80)) { selected.Add(color); }
                if (selected.Count == 4) { break; }
            }
            return selected.Concat(fallback).Take(4).ToArray();
        }
        catch (Exception error) when (error is ArgumentException or IOException or UnauthorizedAccessException or OutOfMemoryException) { return fallback; }
    }
}
