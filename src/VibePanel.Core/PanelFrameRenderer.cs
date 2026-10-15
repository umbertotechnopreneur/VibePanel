// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using System.Buffers.Binary;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace VibePanel.Core;

/// <summary>Renders local images and a diagnostic chart using Windows GDI+.</summary>
public static class PanelFrameRenderer
{
    /// <summary>Produces a labelled picture for orientation, RGB order, and edge visibility checks.</summary>
    public static byte[] Pattern(DisplayProfile profile)
    {
        using var bitmap = new Bitmap(profile.Width, profile.Height, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.FromArgb(37, 46, 49));
            var colors = new[] { Color.Red, Color.Lime, Color.Blue, Color.White };
            for (var index = 0; index < colors.Length; index++)
            {
                using var brush = new SolidBrush(colors[index]);
                var left = index * profile.Width / colors.Length;
                var right = (index + 1) * profile.Width / colors.Length;
                graphics.FillRectangle(brush, left, profile.Height / 2, right - left, profile.Height / 3);
            }
            using var border = new Pen(Color.FromArgb(199, 123, 211), Math.Max(2, profile.Height / 80));
            graphics.DrawRectangle(border, 4, 4, profile.Width - 9, profile.Height - 9);
            using var font = new Font("Segoe UI", Math.Max(8, profile.Height / 12f), FontStyle.Bold, GraphicsUnit.Pixel);
            using var secondary = new Font("Segoe UI", Math.Max(7, profile.Height / 20f), FontStyle.Regular, GraphicsUnit.Pixel);
            graphics.DrawString("TOP  /  VIBE PANEL", font, Brushes.White, 12, 12);
            graphics.DrawString($"{profile.Width} x {profile.Height}   |   VibeWare", secondary, Brushes.White, 12, profile.Height / 3f);
        }
        return Encode(bitmap, profile);
    }

    /// <summary>Fits a local still image within the panel raster with black letterboxing and optional rotation.</summary>
    public static byte[] Image(string path, DisplayProfile profile, int rotation = 0)
    {
        if (rotation is not (0 or 90 or 180 or 270)) { throw new ArgumentOutOfRangeException(nameof(rotation)); }
        var file = new FileInfo(Path.GetFullPath(path));
        if (!file.Exists) { throw new FileNotFoundException("The selected image does not exist.", file.FullName); }
        if (file.Length > 50 * 1024 * 1024) { throw new IOException("Choose an image smaller than 50 MiB."); }
        using var input = System.Drawing.Image.FromFile(file.FullName);
        if ((long)input.Width * input.Height > 40_000_000) { throw new IOException("The source image exceeds 40 megapixels."); }
        input.RotateFlip(rotation switch { 90 => RotateFlipType.Rotate90FlipNone, 180 => RotateFlipType.Rotate180FlipNone, 270 => RotateFlipType.Rotate270FlipNone, _ => RotateFlipType.RotateNoneFlipNone });
        using var bitmap = new Bitmap(profile.Width, profile.Height, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Black);
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            var scale = Math.Min((double)profile.Width / input.Width, (double)profile.Height / input.Height);
            var width = Math.Max(1, (int)Math.Round(input.Width * scale));
            var height = Math.Max(1, (int)Math.Round(input.Height * scale));
            graphics.DrawImage(input, (profile.Width - width) / 2, (profile.Height - height) / 2, width, height);
        }
        return Encode(bitmap, profile);
    }

    internal static byte[] Encode(Bitmap bitmap, DisplayProfile profile)
    {
        if (profile.WireRotation == 90) { bitmap.RotateFlip(RotateFlipType.Rotate90FlipNone); }
        if (profile.Encoding == PanelPixelEncoding.Jpeg)
        {
            var codec = ImageCodecInfo.GetImageEncoders().Single(item => item.FormatID == ImageFormat.Jpeg.Guid);
            foreach (var quality in new long[] { 95, 85, 75, 60, 45, 30 })
            {
                using var output = new MemoryStream();
                using var options = new EncoderParameters(1);
                options.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
                bitmap.Save(output, codec, options);
                if (output.Length <= 450_000) { return output.ToArray(); }
            }
            throw new IOException("The picture exceeds the panel's 450 KB JPEG limit, even at reduced quality.");
        }
        var pixels = new byte[profile.FrameBytes];
        var bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            var row = new byte[bitmap.Width * 3];
            for (var y = 0; y < bitmap.Height; y++)
            {
                Marshal.Copy(bitmapData.Scan0 + y * bitmapData.Stride, row, 0, row.Length);
                for (var x = 0; x < bitmap.Width; x++)
                {
                    var color = (ushort)(((row[x * 3 + 2] & 0xF8) << 8) | ((row[x * 3 + 1] & 0xFC) << 3) | (row[x * 3] >> 3));
                    var destination = pixels.AsSpan((y * bitmap.Width + x) * 2, 2);
                    if (profile.Encoding == PanelPixelEncoding.Rgb565BigEndian) { BinaryPrimitives.WriteUInt16BigEndian(destination, color); }
                    else { BinaryPrimitives.WriteUInt16LittleEndian(destination, color); }
                }
            }
        }
        finally { bitmap.UnlockBits(bitmapData); }
        return pixels;
    }
}
