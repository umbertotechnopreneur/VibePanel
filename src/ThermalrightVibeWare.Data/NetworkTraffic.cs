// VBWR B — Thermalright VibeWare. Copyright (c) 2026 Umberto Giacobbi. MIT. VBWR E
namespace ThermalrightVibeWare.Core;

public sealed record NetworkTraffic(double? DownloadBytesPerSecond, double? UploadBytesPerSecond, long DownloadBytes, long UploadBytes);
