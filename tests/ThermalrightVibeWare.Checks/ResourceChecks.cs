// VBWR B
// Project: ThermalrightVibeWare | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using System.Buffers.Binary;
using System.Drawing;
using System.Drawing.Imaging;
using ThermalrightVibeWare.Core;

internal static class ResourceChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var root = Path.Combine(AppContext.BaseDirectory, "resource-fixtures-" + Guid.NewGuid().ToString("N"));
        var engine = Path.Combine(root, "engine");
        Directory.CreateDirectory(engine);
        try
        {
            foreach (var name in new[] { "themes", "theme-runtime", "plugins" })
            {
                Directory.CreateDirectory(Path.Combine(root, name));
                check(ThemeCatalog.FindContentDirectory(name, engine) is null, "missing bundled " + name + " cannot load from an ancestor");
                var bundled = Path.Combine(engine, name);
                Directory.CreateDirectory(bundled);
                check(ThemeCatalog.FindContentDirectory(name, engine) == bundled, "bundled " + name + " resolves beside the engine");
                Directory.Delete(bundled);
                Directory.Delete(Path.Combine(root, name));
            }
            var traversalRejected = false;
            try { ThemeCatalog.FindContentDirectory("../themes", engine); }
            catch (ArgumentException) { traversalRejected = true; }
            check(traversalRejected, "content lookup rejects traversal");
        }
        finally
        {
            foreach (var name in new[] { "themes", "theme-runtime", "plugins" })
            {
                foreach (var parent in new[] { engine, root })
                {
                    var folder = Path.Combine(parent, name);
                    if (Directory.Exists(folder)) { Directory.Delete(folder); }
                }
            }
            Directory.Delete(engine);
            Directory.Delete(root);
        }

        void RejectBeforeDecode(byte[] header, string name)
        {
            var rejected = false;
            using var stream = new MemoryStream(header);
            try { using var image = RasterImage.Open(stream); }
            catch (IOException error) { rejected = error.Message.Contains("16 megapixels", StringComparison.Ordinal); }
            check(rejected, name + " is rejected from its header before the pixel decoder");
        }
        var png = new byte[33];
        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82 }.CopyTo(png, 0);
        BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(16), 32768);
        BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(20), 32768);
        RejectBeforeDecode(png, "large compressed PNG");
        var jpeg = new byte[32];
        new byte[] { 0xFF, 0xD8, 0xFF, 0xC0, 0, 17, 8 }.CopyTo(jpeg, 0);
        BinaryPrimitives.WriteUInt16BigEndian(jpeg.AsSpan(7), 32768);
        BinaryPrimitives.WriteUInt16BigEndian(jpeg.AsSpan(9), 32768);
        RejectBeforeDecode(jpeg, "large JPEG");
        var gif = new byte[35];
        "GIF89a"u8.CopyTo(gif);
        BinaryPrimitives.WriteUInt16LittleEndian(gif.AsSpan(6), 32768);
        BinaryPrimitives.WriteUInt16LittleEndian(gif.AsSpan(8), 32768);
        RejectBeforeDecode(gif, "large GIF canvas");
        var bmp = new byte[54];
        "BM"u8.CopyTo(bmp);
        BinaryPrimitives.WriteUInt32LittleEndian(bmp.AsSpan(14), 40);
        BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(18), 32768);
        BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(22), int.MinValue);
        RejectBeforeDecode(bmp, "large top-down BMP without integer overflow");
        using (var tooLarge = new DeclaredLengthStream(RasterImage.MaximumBytes + 1))
        {
            var rejected = false;
            try { using var image = RasterImage.Open(tooLarge); }
            catch (IOException) { rejected = true; }
            check(rejected && tooLarge.Position == 0, "oversized compressed input is rejected before reading");
        }
        using var bitmap = new Bitmap(32, 24);
        using (var graphics = Graphics.FromImage(bitmap)) { graphics.Clear(Color.MediumPurple); }
        foreach (var format in new[] { ImageFormat.Png, ImageFormat.Jpeg, ImageFormat.Gif, ImageFormat.Bmp })
        {
            using var stream = new MemoryStream();
            bitmap.Save(stream, format);
            using var image = RasterImage.Open(stream);
            check(image.Width == 32 && image.Height == 24, "normal " + format + " still decodes");
        }
        using (var truncated = new MemoryStream(new byte[] { 137, 80, 78, 71 }))
        {
            var rejected = false;
            try { using var image = RasterImage.Open(truncated); }
            catch (IOException) { rejected = true; }
            check(rejected, "truncated raster header is rejected");
        }
    }

    private sealed class DeclaredLengthStream(long length) : MemoryStream
    {
        public override long Length => length;
    }
}
