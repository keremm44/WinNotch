// WinNotch.Core/Services/MediaSessionService.cs
// WHY: Uses Windows.Media.Control (WinRT) for SMTC integration.
// This is the modern, supported way to interact with media sessions.
// No COM interop hacks or polling — pure event-driven.
//
// The GlobalSystemMediaTransportControlsSessionManager provides:
// - Current session (album art, title, artist)
// - Play/Pause/Next/Previous controls
// - Session change events (new media starts/stops)
//
// PERFORMANCE NOTE: Only active when media is playing. When no session
// exists, we unsubscribe all events. Zero idle cost.
//
// NOTE: Requires Windows 10 1809+ (build 17763).
// The WinRT APIs are accessible via .NET 8's built-in WinRT support.

using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Media.Control;
using Windows.Storage.Streams;
using System.Windows.Media.Imaging;

namespace WinNotch.Core.Services;

/// <summary>
/// Data model for current media session info.
/// </summary>
public sealed class MediaSessionInfo
{
    public string Title { get; init; } = string.Empty;
    public string Artist { get; init; } = string.Empty;
    public string AlbumTitle { get; init; } = string.Empty;
    public BitmapSource? AlbumArt { get; init; }
    public bool IsPlaying { get; init; }
    public bool HasSession { get; init; }
}

/// <summary>
/// Event args for media session changes.
/// </summary>
public sealed class MediaSessionChangedEventArgs : EventArgs
{
    public MediaSessionInfo Session { get; init; } = new() { HasSession = false };
}

/// <summary>
/// High-level media session service using WinRT SMTC APIs.
/// Provides album art, playback controls, and session state.
/// </summary>
public sealed class MediaSessionService : IDisposable
{
    private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;
    private GlobalSystemMediaTransportControlsSession? _currentSession;
    private bool _disposed;

    /// <summary>
    /// Fired when the media session changes (new song, playback state, etc.).
    /// </summary>
    public event EventHandler<MediaSessionChangedEventArgs>? SessionChanged;

    /// <summary>
    /// Initializes the SMTC session manager.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _sessionManager.CurrentSessionChanged += OnCurrentSessionChanged;

            // WHY GetCurrentSession(): The WinRT projection exposes this as a method, not a property.
            var session = _sessionManager.GetCurrentSession();
            if (session != null)
            {
                AttachSession(session);
            }
            else
            {
                NotifyNoSession();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[MediaSessionService] Failed to initialize SMTC: {ex.Message}");
            NotifyNoSession();
        }
    }

    /// <summary>
    /// Handles session manager's CurrentSessionChanged event.
    /// </summary>
    private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender,
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

    /// <summary>
    /// Attaches event handlers to a new media session.
    /// </summary>
    private void AttachSession(GlobalSystemMediaTransportControlsSession session)
    {
        DetachSession(); // Detach from previous session first

        _currentSession = session;
        _currentSession.MediaPropertiesChanged += OnSessionPropertyChanged;
        _currentSession.PlaybackInfoChanged += OnPlaybackInfoChanged;

        // Get initial state
        _ = UpdateSessionInfoAsync();
    }

    /// <summary>
    /// Detaches event handlers from the current session.
    /// </summary>
    private void DetachSession()
    {
        if (_currentSession != null)
        {
            _currentSession.MediaPropertiesChanged -= OnSessionPropertyChanged;
            _currentSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            _currentSession = null;
        }
    }

    private void OnSessionPropertyChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
    {
        _ = UpdateSessionInfoAsync();
    }

    private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
    {
        _ = UpdateSessionInfoAsync();
    }

    /// <summary>
    /// Reads current session state and fires SessionChanged event.
    /// </summary>
    private async Task UpdateSessionInfoAsync()
    {
        if (_disposed || _currentSession == null) return;

        try
        {
            var mediaProperties = await _currentSession.TryGetMediaPropertiesAsync();
            var playbackInfo = _currentSession.GetPlaybackInfo();

            // Read album art
            BitmapSource? albumArt = null;
            if (mediaProperties.Thumbnail != null)
            {
                albumArt = await ReadThumbnailAsync(mediaProperties.Thumbnail);
            }

            var info = new MediaSessionInfo
            {
                Title = mediaProperties.Title ?? "Unknown",
                Artist = mediaProperties.Artist ?? "Unknown",
                AlbumTitle = mediaProperties.AlbumTitle ?? "Unknown",
                AlbumArt = albumArt,
                IsPlaying = playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
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

    /// <summary>
    /// Reads a WinRT IRandomAccessStreamReference and converts to BitmapSource.
    /// WHY: AsStreamForRead() is from System.Runtime.InteropServices.WindowsRuntime.
    /// </summary>
    private static async Task<BitmapSource?> ReadThumbnailAsync(IRandomAccessStreamReference streamRef)
    {
        try
        {
            using var stream = await streamRef.OpenReadAsync();
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = stream.AsStream();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze(); // Freeze for cross-thread access
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Sends play/pause toggle command to current session.
    /// </summary>
    public void TogglePlayPause()
    {
        if (_currentSession == null) return;

        try
        {
            var playbackInfo = _currentSession.GetPlaybackInfo();
            if (playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
            {
                _ = _currentSession.TryPauseAsync().AsTask();
            }
            else
            {
                _ = _currentSession.TryPlayAsync().AsTask();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[MediaSessionService] Error toggling play/pause: {ex.Message}");
        }
    }

    /// <summary>
    /// Skips to next track.
    /// </summary>
    public void NextTrack()
    {
        if (_currentSession != null)
            _ = _currentSession.TrySkipNextAsync().AsTask();
    }

    /// <summary>
    /// Skips to previous track.
    /// </summary>
    public void PreviousTrack()
    {
        if (_currentSession != null)
            _ = _currentSession.TrySkipPreviousAsync().AsTask();
    }

    /// <summary>
    /// Fires a "no session" event.
    /// </summary>
    private void NotifyNoSession()
    {
        SessionChanged?.Invoke(this, new MediaSessionChangedEventArgs
        {
            Session = new MediaSessionInfo { HasSession = false }
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        DetachSession();

        if (_sessionManager != null)
        {
            _sessionManager.CurrentSessionChanged -= OnCurrentSessionChanged;
            _sessionManager = null;
        }

    }
}
