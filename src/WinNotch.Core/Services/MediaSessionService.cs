// WinNotch.Core/Services/MediaSessionService.cs
// Event-driven SMTC integration. Album artwork is decoded only to the size
// needed by the tiny WinNotch surface so large source images do not stay decoded.

using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Media.Control;
using Windows.Storage.Streams;
using System.Windows.Media.Imaging;

namespace WinNotch.Core.Services;

public sealed class MediaSessionInfo
{
    public string Title { get; init; } = string.Empty;
    public string Artist { get; init; } = string.Empty;
    public string AlbumTitle { get; init; } = string.Empty;
    public BitmapSource? AlbumArt { get; init; }
    public bool IsPlaying { get; init; }
    public bool HasSession { get; init; }
}

public sealed class MediaSessionChangedEventArgs : EventArgs
{
    public MediaSessionInfo Session { get; init; } = new() { HasSession = false };
}

public sealed class MediaSessionService : IDisposable
{
    // The UI renders artwork at a few dozen pixels. 96 px gives enough headroom
    // for DPI scaling without decoding arbitrary 1000+ px album covers.
    private const int AlbumArtDecodeWidth = 96;

    private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;
    private GlobalSystemMediaTransportControlsSession? _currentSession;
    private bool _disposed;
    private long _updateVersion;

    public event EventHandler<MediaSessionChangedEventArgs>? SessionChanged;

    public async Task InitializeAsync()
    {
        try
        {
            _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if (_disposed)
            {
                _sessionManager = null;
                return;
            }

            _sessionManager.CurrentSessionChanged += OnCurrentSessionChanged;

            var session = _sessionManager.GetCurrentSession();
            if (session != null)
                AttachSession(session);
            else
                NotifyNoSession();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[MediaSessionService] Failed to initialize SMTC: {ex.Message}");
            NotifyNoSession();
        }
    }

    private void OnCurrentSessionChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        CurrentSessionChangedEventArgs args)
    {
        if (_disposed) return;

        var session = sender.GetCurrentSession();
        if (session != null)
        {
            AttachSession(session);
        }
        else
        {
            DetachSession();
            NotifyNoSession();
        }
    }

    private void AttachSession(GlobalSystemMediaTransportControlsSession session)
    {
        DetachSession();

        _currentSession = session;
        _currentSession.MediaPropertiesChanged += OnSessionPropertyChanged;
        _currentSession.PlaybackInfoChanged += OnPlaybackInfoChanged;
        _ = UpdateSessionInfoAsync(session, Interlocked.Increment(ref _updateVersion));
    }

    private void DetachSession()
    {
        // Invalidate async work belonging to the old session before detaching it.
        Interlocked.Increment(ref _updateVersion);

        if (_currentSession != null)
        {
            _currentSession.MediaPropertiesChanged -= OnSessionPropertyChanged;
            _currentSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            _currentSession = null;
        }
    }

    private void OnSessionPropertyChanged(
        GlobalSystemMediaTransportControlsSession sender,
        MediaPropertiesChangedEventArgs args)
    {
        if (_disposed || !ReferenceEquals(sender, _currentSession)) return;
        _ = UpdateSessionInfoAsync(sender, Interlocked.Increment(ref _updateVersion));
    }

    private void OnPlaybackInfoChanged(
        GlobalSystemMediaTransportControlsSession sender,
        PlaybackInfoChangedEventArgs args)
    {
        if (_disposed || !ReferenceEquals(sender, _currentSession)) return;
        _ = UpdateSessionInfoAsync(sender, Interlocked.Increment(ref _updateVersion));
    }

    private async Task UpdateSessionInfoAsync(
        GlobalSystemMediaTransportControlsSession session,
        long version)
    {
        if (_disposed) return;

        try
        {
            var mediaProperties = await session.TryGetMediaPropertiesAsync();
            if (_disposed || version != Volatile.Read(ref _updateVersion) ||
                !ReferenceEquals(session, _currentSession))
                return;

            var playbackInfo = session.GetPlaybackInfo();

            BitmapSource? albumArt = null;
            if (mediaProperties.Thumbnail != null)
                albumArt = await ReadThumbnailAsync(mediaProperties.Thumbnail);

            // A newer metadata/playback event may have completed while artwork decoded.
            if (_disposed || version != Volatile.Read(ref _updateVersion) ||
                !ReferenceEquals(session, _currentSession))
                return;

            var info = new MediaSessionInfo
            {
                Title = mediaProperties.Title ?? "Unknown",
                Artist = mediaProperties.Artist ?? "Unknown",
                AlbumTitle = mediaProperties.AlbumTitle ?? "Unknown",
                AlbumArt = albumArt,
                IsPlaying = playbackInfo.PlaybackStatus ==
                    GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                HasSession = true
            };

            SessionChanged?.Invoke(this, new MediaSessionChangedEventArgs { Session = info });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[MediaSessionService] Error reading session info: {ex.Message}");
        }
    }

    private static async Task<BitmapSource?> ReadThumbnailAsync(IRandomAccessStreamReference streamRef)
    {
        try
        {
            using var stream = await streamRef.OpenReadAsync();
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = stream.AsStream();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = AlbumArtDecodeWidth;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    public void TogglePlayPause()
    {
        if (_currentSession == null) return;

        try
        {
            var playbackInfo = _currentSession.GetPlaybackInfo();
            if (playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                _ = _currentSession.TryPauseAsync().AsTask();
            else
                _ = _currentSession.TryPlayAsync().AsTask();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[MediaSessionService] Error toggling play/pause: {ex.Message}");
        }
    }

    public void NextTrack()
    {
        if (_currentSession != null)
            _ = _currentSession.TrySkipNextAsync().AsTask();
    }

    public void PreviousTrack()
    {
        if (_currentSession != null)
            _ = _currentSession.TrySkipPreviousAsync().AsTask();
    }

    private void NotifyNoSession()
    {
        if (_disposed) return;
        SessionChanged?.Invoke(this, new MediaSessionChangedEventArgs
        {
            Session = new MediaSessionInfo { HasSession = false }
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Interlocked.Increment(ref _updateVersion);

        DetachSession();

        if (_sessionManager != null)
        {
            _sessionManager.CurrentSessionChanged -= OnCurrentSessionChanged;
            _sessionManager = null;
        }

        SessionChanged = null;
    }
}
