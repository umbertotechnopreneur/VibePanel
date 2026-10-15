// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using Windows.Media.Control;
using Windows.Storage.Streams;

namespace VibePanel.Core;

/// <summary>Uses Windows' local media-session API, preferring Spotify over other players.</summary>
public sealed class WindowsMediaService
{
    private GlobalSystemMediaTransportControlsSessionManager? manager;
    private string? artworkKey;
    private byte[]? cachedArtwork;

    private async Task<GlobalSystemMediaTransportControlsSession?> SessionAsync(CancellationToken cancellationToken)
    {
        manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask(cancellationToken);
        return manager.GetSessions().FirstOrDefault(session => session.SourceAppUserModelId.Contains("spotify", StringComparison.OrdinalIgnoreCase))
            ?? manager.GetCurrentSession();
    }

    /// <summary>Reads current metadata; thumbnail bytes are reloaded only when the track identity changes.</summary>
    public async Task<NowPlaying> ReadAsync()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var session = await SessionAsync(deadline.Token);
        if (session is null) { return NowPlaying.Empty; }
        var properties = await session.TryGetMediaPropertiesAsync().AsTask(deadline.Token);
        var key = $"{session.SourceAppUserModelId}\n{properties.Title}\n{properties.Artist}\n{properties.AlbumTitle}";
        if (key != artworkKey)
        {
            cachedArtwork = null;
            if (properties.Thumbnail is not null)
            {
                using var stream = await properties.Thumbnail.OpenReadAsync().AsTask(deadline.Token);
                if (stream.Size is > 0 and <= 5 * 1024 * 1024)
                {
                    using var reader = new DataReader(stream.GetInputStreamAt(0));
                    var size = checked((uint)stream.Size);
                    await reader.LoadAsync(size).AsTask(deadline.Token);
                    cachedArtwork = new byte[size];
                    reader.ReadBytes(cachedArtwork);
                }
            }
            artworkKey = key;
        }
        var timeline = session.GetTimelineProperties();
        return new NowPlaying(properties.Title, properties.Artist, properties.AlbumTitle, session.SourceAppUserModelId,
            session.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
            cachedArtwork, Math.Max(0, (timeline.Position - timeline.StartTime).TotalSeconds),
            Math.Max(0, (timeline.EndTime - timeline.StartTime).TotalSeconds));
    }

    /// <summary>Controls the selected local player only after a user invokes a media command.</summary>
    public async Task ControlAsync(string action)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var session = await SessionAsync(deadline.Token) ?? throw new InvalidOperationException("No Windows media session is available. Open Spotify first.");
        var accepted = action switch
        {
            "toggle" => await session.TryTogglePlayPauseAsync().AsTask(deadline.Token),
            "play" => await session.TryPlayAsync().AsTask(deadline.Token),
            "pause" => await session.TryPauseAsync().AsTask(deadline.Token),
            "next" => await session.TrySkipNextAsync().AsTask(deadline.Token),
            "previous" => await session.TrySkipPreviousAsync().AsTask(deadline.Token),
            _ => throw new ArgumentException("Choose play, pause, toggle, next, or previous.", nameof(action))
        };
        if (!accepted) { throw new InvalidOperationException("The player declined this media command."); }
    }
}
