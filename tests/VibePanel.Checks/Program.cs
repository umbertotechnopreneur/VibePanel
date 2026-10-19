// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using System.Buffers.Binary;
using System.Drawing;
using VibePanel.Core;
using VibePanel.Contracts;
using System.Text.Json;

var count = 0;
void Check(bool condition, string name)
{
    if (!condition) { throw new InvalidOperationException(name); }
    count++;
}

// Fixed wire fixtures exercise documented offsets, rather than serialising our own objects as expectations.
var info = Thermalright04165302Protocol.CreateDeviceInfoRequest();
Check(info.Length == 512 && info[12] == 1 && info[4] == 0, "device-info command layout");
var fixture = Convert.FromHexString("DADBDCDD0180000000000000010000001000000042503030303030300000000000000000");
var result = PanelProbeResult.FromHidResponse(fixture);
Check(result.IsCompatible && result.ProductMode == 128 && result.SubMode == 1, "HID PM/SUB order");
Check(result.Serial.Length == 32, "36-byte optional serial");
Check(result.DetectedProfile is { Width: 1280, Height: 480, Encoding: PanelPixelEncoding.Jpeg }, "observed PM=128 geometry and JPEG");
Check(!PanelProbeResult.FromHidResponse(fixture.AsSpan(0, 7)).IsCompatible, "truncated HID reply");
Check(PanelProbeResult.FromHidResponse(Convert.FromHexString("DADBDCDD003A0000")).DetectedProfile is { Width: 240, Height: 320 }, "short Warframe reply");
Check(DisplayProfileCatalog.FromProductMode(254, 0) is null, "unknown model has no guessed profile");
Check(DisplayProfileCatalog.FromProductMode(54, 0)?.Encoding == PanelPixelEncoding.Jpeg, "360-square is JPEG");
Check(DisplayProfileCatalog.FromProductMode(1, 48)?.Width == 1600, "compound PM/SUB mapping");
var wrongCommand = (byte[])fixture.Clone();
wrongCommand[12] = 2;
Check(!PanelProbeResult.FromHidResponse(wrongCommand).IsCompatible, "reject wrong device-info command");
var profile = DisplayProfileCatalog.GetRequired("wide-1280x480");
var image = PanelFrameRenderer.Pattern(profile);
var packet = Thermalright04165302Protocol.CreatePicturePacket(profile, image);
Check(packet.AsSpan(0, 16).SequenceEqual(Convert.FromHexString("DADBDCDD020000000005E00102000000")), "1280x480 JPEG header golden bytes");
Check(BinaryPrimitives.ReadInt32LittleEndian(packet.AsSpan(16, 4)) == image.Length, "payload length excludes header and padding");
Check(packet.Length % 512 == 0 && packet.Length >= image.Length + 20 && packet.Length < image.Length + 532, "512 alignment");
Check(packet.AsSpan(20, image.Length).SequenceEqual(image), "JPEG payload unmodified");
Check(packet.AsSpan(20 + image.Length).IndexOfAnyExcept((byte)0) == -1, "zero padding");
using (var stream = new MemoryStream(image))
using (var bitmap = new Bitmap(stream))
{
    Check(bitmap.Width == 1280 && bitmap.Height == 480, "JPEG decoder confirms actual dimensions");
    var red = bitmap.GetPixel(160, 300);
    var green = bitmap.GetPixel(480, 300);
    var blue = bitmap.GetPixel(800, 300);
    Check(red.R > 240 && red.G < 10 && red.B < 10, "diagnostic red bar");
    Check(green.G > 240 && green.R < 10 && green.B < 10, "diagnostic green bar");
    Check(blue.B > 240 && blue.R < 10 && blue.G < 10, "diagnostic blue bar");
}
foreach (var encoding in new[] { PanelPixelEncoding.Rgb565LittleEndian, PanelPixelEncoding.Rgb565BigEndian })
{
    var rgbProfile = new DisplayProfile("test", "test", 240, 240, encoding);
    var rgb = PanelFrameRenderer.Pattern(rgbProfile);
    var redOffset = (160 * 240 + 30) * 2;
    Check(rgb.AsSpan(redOffset, 2).SequenceEqual(encoding == PanelPixelEncoding.Rgb565BigEndian ? new byte[] { 0xF8, 0 } : new byte[] { 0, 0xF8 }), "RGB565 endianness");
    var rgbPacket = Thermalright04165302Protocol.CreatePicturePacket(rgbProfile, rgb);
    Check(rgbPacket[6] == 1 && BinaryPrimitives.ReadUInt16LittleEndian(rgbPacket.AsSpan(8, 2)) == 240 && BinaryPrimitives.ReadUInt16LittleEndian(rgbPacket.AsSpan(10, 2)) == 320, "RGB565 legacy header");
}
var rejected = false;
try { Thermalright04165302Protocol.CreatePicturePacket(profile, new byte[] { 1, 2, 3 }); }
catch (ArgumentException) { rejected = true; }
Check(rejected, "reject invalid JPEG before transport");
var previewOptions = new PreviewOptions(Light: true, AmbientDrift: true);
var requestJson = JsonSerializer.Serialize(new EngineRequest("fixture", "state", JsonSerializer.SerializeToElement(previewOptions, PanelContract.Json)), PanelContract.Json);
var parsedRequest = JsonSerializer.Deserialize<EngineRequest>(requestJson, PanelContract.Json)!;
Check(parsedRequest.Version == 1 && parsedRequest.Id == "fixture", "JSON Lines version and correlation survive serialization");
Check(parsedRequest.Parameters!.Value.Deserialize<PreviewOptions>(PanelContract.Json) is { Light: true, AmbientDrift: true, IncludeDevices: false }, "preview options cannot implicitly enumerate USB");
Check(AudioLevel.Fraction(0) == 0 && AudioLevel.Fraction(1) == 1, "VU scale covers silence through digital full scale");
using (var stream = new MemoryStream(PanelFrameRenderer.PrepareJpeg(image, profile)))
using (var bitmap = new Bitmap(stream))
{
    Check(bitmap.Width == profile.Width && bitmap.Height == profile.Height, "panel conversion preserves native geometry");
    Check(bitmap.GetPixel(160, 300).R > 235, "panel conversion preserves the red bar");
}
ResourceChecks.Run(Check);
ScreenChecks.Run(Check);
SensorDependencyChecks.Run(Check);
SensorProjectionChecks.Run(Check);
Console.WriteLine($"{count} offline checks passed. No USB handles were opened.");
