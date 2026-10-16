// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using System.Text.Json;

namespace VibePanel.Contracts;

/// <summary>JSON Lines v1. Stdout contains responses only; diagnostics belong on stderr.</summary>
public static class PanelContract
{
    public const int Version = 1;
    public const int MaximumRequestCharacters = 16_384;
    public const int MaximumResponseCharacters = 2_000_000;
    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web);
}

public sealed record EngineRequest(string Id, string Method, JsonElement? Parameters = null, int Version = PanelContract.Version);
public sealed record EngineResponse(string Id, bool Ok, JsonElement? Result = null, string? Error = null, int Version = PanelContract.Version);
public sealed record PanelChoice(string Id, string Name);
public sealed record ThemeChoiceDto(string Id, string Name, string Category, string Description, string PackId);
public sealed record ThemeCatalogDto(IReadOnlyList<PanelChoice> Categories, IReadOnlyList<ThemeChoiceDto> Themes, IReadOnlyList<string> Notices);
public sealed record PreviewOptions(string ProfileId = "wide-1280x480", string Scene = "Music", bool IncludeFrame = true, bool Light = false, bool AmbientDrift = false, bool IncludeDevices = false,
    string? ThemeId = null, string FontFamily = "Cascadia Mono", double FontScale = 1);
public sealed record StartOptions(string DeviceId, string ProfileId = "auto", string? Scene = null);
public sealed record MediaOptions(string Action);
public sealed record AudioLevelDto(string Id, string Name, double VolumePercent, bool Muted, double? Peak);
public sealed record SystemDto(double? CpuUsage, double? CpuTemperature, double? GpuUsage, double? GpuTemperature, string GpuName, string CpuTemperatureSource);
public sealed record NetworkDto(double? DownloadBytesPerSecond, double? UploadBytesPerSecond, long DownloadBytes, long UploadBytes, string Scope);
public sealed record PanelState(string Title, string Artist, string Album, bool Playing, string Source,
    IReadOnlyList<AudioLevelDto> Audio, SystemDto System, NetworkDto? Network,
    IReadOnlyList<PanelChoice> Devices, IReadOnlyList<PanelChoice> Profiles, IReadOnlyList<PanelChoice> Scenes,
    bool DisplayRunning, string Status, byte[]? FrameJpeg, IReadOnlyList<string> Notices);

/// <summary>The presentation depends on this contract, not the USB driver or operating system APIs.</summary>
public interface IPanelClient
{
    Task<T> RequestAsync<T>(string method, object? parameters = null, CancellationToken cancellationToken = default);
}
