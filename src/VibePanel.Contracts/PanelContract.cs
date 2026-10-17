// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using System.Text.Json;

namespace VibePanel.Contracts;

/// <summary>Shows the engine's existing JPEG in the configuration UI; never renders or sends USB frames.</summary>
public interface IStudioPreview
{
    Task<PreviewSize> ShowAsync(byte[] jpeg, bool transition);
    void Clear();
}

public sealed record PreviewSize(int Width, int Height);

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
public sealed record ThemeChoiceDto(string Id, string Name, string Category, string Description, string PackId, bool SupportsSpectrum = false);
public sealed record ThemeCatalogDto(IReadOnlyList<PanelChoice> Categories, IReadOnlyList<ThemeChoiceDto> Themes, IReadOnlyList<string> Notices);
public sealed record FontCatalogDto(IReadOnlyList<string> Families, string? Notice = null);
public sealed record PreviewOptions(string ProfileId = "wide-1280x480", string Scene = "Music", bool IncludeFrame = true, bool Light = false, bool AmbientDrift = false, bool IncludeDevices = false,
    string? ThemeId = null, string FontFamily = "Cascadia Mono", double FontScale = 1, int RefreshIntervalMilliseconds = 1000,
    string JpegQuality = "high", double SaturationPercent = 100, double HueDegrees = 0, bool ShowSpectrum = true,
    double BrightnessPercent = 0, double ContrastPercent = 100, bool ColorFiltersEnabled = true);
public sealed record StartOptions(string DeviceId, string ProfileId = "auto", string? Scene = null, string? ThemeId = null);
public sealed record DisplayStartResult(bool Accepted, string? ProfileId = null);
public sealed record MediaOptions(string Action);
public sealed record CodexSetupStatus(bool Prepared, bool Listed, bool ActivitySeen, string Status, string? LastEvent, string? LastEventUtc, string Instructions);
public sealed record DiagnosticsStatus(bool Enabled, string LogPath);
public sealed record SetDiagnosticsOptions(bool Enabled);
public sealed record AudioLevelDto(string Id, string Name, double VolumePercent, bool Muted, double? Peak);
public sealed record SystemDto(double? CpuUsage, double? CpuTemperature, double? GpuUsage, double? GpuTemperature, string GpuName, string CpuTemperatureSource);
public sealed record NetworkDto(double? DownloadBytesPerSecond, double? UploadBytesPerSecond, long DownloadBytes, long UploadBytes, string Scope);
public sealed record PanelState(string Title, string Artist, string Album, bool Playing, string Source,
    IReadOnlyList<AudioLevelDto> Audio, SystemDto System, NetworkDto? Network,
    IReadOnlyList<PanelChoice> Devices, IReadOnlyList<PanelChoice> Profiles, IReadOnlyList<PanelChoice> Scenes,
    bool DisplayRunning, string Status, byte[]? FrameJpeg, IReadOnlyList<string> Notices,
    double? FrameMilliseconds = null, long SubmittedFrames = 0, string? AccentColor = null);

/// <summary>The presentation depends on this contract, not the USB driver or operating system APIs.</summary>
public interface IPanelClient
{
    Task<T> RequestAsync<T>(string method, object? parameters = null, CancellationToken cancellationToken = default);
}

/// <summary>Desktop window actions supplied by the host, separate from the USB engine.</summary>
public interface IStudioWindowActions
{
    bool CanMinimizeToTray { get; }
    void MinimizeToTray();
    void CloseApplication();
}
