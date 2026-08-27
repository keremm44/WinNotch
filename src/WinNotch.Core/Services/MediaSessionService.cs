// WinNotch.Core/Services/MediaSessionService.cs
// Event-driven SMTC integration with stable, playback-aware session selection.

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
    public string SourceAppId { get; init; } = string.Empty;
    public BitmapSource? AlbumArt { get; init; }
    public bool IsPlaying { get; init; }
    public bool HasSession { get; init; }
    public bool CanPlay { get; init; }
    public bool CanPause { get; init; }
    public bool CanSkipNext { get; init; }
    public bool CanSkipPrevious { get; init; }
    public TimeSpan TimelineStart { get; init; }
    public TimeSpan TimelineEnd { get; init; }
    public TimeSpan Position { get; init; }
}

public sealed class MediaSessionChangedEventArgs : EventArgs
{
    public MediaSessionInfo Session { get; init; } = new() { HasSession = false };
}

public sealed class MediaSessionService : IDisposable
{
    private const int AlbumArtDecodeWidth = 96;

    private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;
    private GlobalSystemMediaTransportControlsSession? _currentSession;
    private MediaSessionInfo? _lastInfo;
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

            _sessionManager.CurrentSessionChanged += OnManagerSessionChanged;
            _sessionManager.SessionsChanged += OnManagerSessionsChanged;
            ReselectSession(_sessionManager, clearWhenEmpty: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[MediaSessionService] Failed to initialize SMTC: {ex.Message}");
            NotifyNoSession();
        }
    }

    private void OnManagerSessionChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        CurrentSessionChangedEventArgs args)
    {
        // CurrentSessionChanged is also raised for foreground/focus churn. During
        // that transition GetSessions can briefly be empty, which is not evidence
        // that the selected media ended.
        ReselectSession(sender, clearWhenEmpty: false);
    }

    private void OnManagerSessionsChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        SessionsChangedEventArgs args)
        => ReselectSession(sender, clearWhenEmpty: true);

    private void ReselectSession(
        GlobalSystemMediaTransportControlsSessionManager manager,
        bool clearWhenEmpty)
    {
        if (_disposed) return;

        GlobalSystemMediaTransportControlsSession? session = SelectBestSession(manager);
        if (session != null)
        {
            AttachSession(session);
            return;
        }

        if (!clearWhenEmpty && _currentSession != null)
            return;

        DetachSession();
        NotifyNoSession();
    }

    private GlobalSystemMediaTransportControlsSession? SelectBestSession(
        GlobalSystemMediaTransportControlsSessionManager manager)
    {
        try
        {
            IReadOnlyList<GlobalSystemMediaTransportControlsSession> sessions = manager.GetSessions();
            if (sessions.Count == 0) return null;

            GlobalSystemMediaTransportControlsSession? managerCurrent = manager.GetCurrentSession();
            GlobalSystemMediaTransportControlsSession? retained = FindSession(sessions, _currentSession);
            GlobalSystemMediaTransportControlsSession? current = FindSession(sessions, managerCurrent);

            // Foreground changes also change SMTC's "current" session. They must not
            // evict media which is still playing. Prefer playing sessions, retaining
            // the selected one to avoid title/art flicker between equivalent sessions.
            if (current != null && IsPlaying(current)) return current;
            if (retained != null && IsPlaying(retained)) return retained;

            foreach (GlobalSystemMediaTransportControlsSession candidate in sessions)
            {
                if (IsPlaying(candidate)) return candidate;
            }

            // A paused session is still actionable media. Keep it selected until it
            // actually disappears from the manager rather than tying it to focus.
            return retained ?? current ?? sessions[0];
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[MediaSessionService] Session selection failed: {ex.Message}");
            return _currentSession;
        }
    }

    private static GlobalSystemMediaTransportControlsSession? FindSession(
        IReadOnlyList<GlobalSystemMediaTransportControlsSession> sessions,
        GlobalSystemMediaTransportControlsSession? wanted)
    {
        if (wanted == null) return null;

        foreach (GlobalSystemMediaTransportControlsSession candidate in sessions)
        {
            if (ReferenceEquals(candidate, wanted) || candidate.Equals(wanted))
                return candidate;
        }

        return null;
    }

    private static bool IsPlaying(GlobalSystemMediaTransportControlsSession session)
    {
        try
        {
            return session.GetPlaybackInfo().PlaybackStatus ==
                   GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        }
        catch
        {
            return false;
        }
    }

    private void AttachSession(GlobalSystemMediaTransportControlsSession session)
    {
        if (ReferenceEquals(_currentSession, session) || _currentSession?.Equals(session) == true)
        {
            _ = UpdateSessionInfoAsync(_currentSession, Interlocked.Increment(ref _updateVersion));
            return;
        }

        DetachSession();

        _currentSession = session;
        _currentSession.MediaPropertiesChanged += OnSessionPropertyChanged;
        _currentSession.PlaybackInfoChanged += OnPlaybackInfoChanged;
        _currentSession.TimelinePropertiesChanged += OnTimelinePropertiesChanged;
        _ = UpdateSessionInfoAsync(session, Interlocked.Increment(ref _updateVersion));
    }

    private void DetachSession()
    {
        Interlocked.Increment(ref _updateVersion);

        if (_currentSession != null)
        {
            _currentSession.MediaPropertiesChanged -= OnSessionPropertyChanged;
            _currentSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            _currentSession.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
            _currentSession = null;
        }

        _lastInfo = null;
    }

    private void OnSessionPropertyChanged(
        GlobalSystemMediaTransportControlsSession sender,
        MediaPropertiesChangedEventArgs args)
    {
        if (_disposed || !IsSelected(sender)) return;
        _ = UpdateSessionInfoAsync(sender, Interlocked.Increment(ref _updateVersion));
    }

    private void OnPlaybackInfoChanged(
        GlobalSystemMediaTransportControlsSession sender,
        PlaybackInfoChangedEventArgs args)
    {
        if (_disposed || !IsSelected(sender)) return;

        // A stop/pause can make another session the best candidate. Re-evaluate the
        // manager rather than blindly publishing the formerly selected session.
        if (_sessionManager != null)
            ReselectSession(_sessionManager, clearWhenEmpty: false);
        else
            _ = UpdateSessionInfoAsync(sender, Interlocked.Increment(ref _updateVersion));
    }

    private bool IsSelected(GlobalSystemMediaTransportControlsSession session)
        => ReferenceEquals(session, _currentSession) || session.Equals(_currentSession);

    private void OnTimelinePropertiesChanged(
        GlobalSystemMediaTransportControlsSession sender,
        TimelinePropertiesChangedEventArgs args)
    {
        if (_disposed || !IsSelected(sender) || _lastInfo == null) return;

        try
        {
            var timeline = sender.GetTimelineProperties();
            MediaSessionInfo previous = _lastInfo;
            var updated = new MediaSessionInfo
            {
                Title = previous.Title,
                Artist = previous.Artist,
                AlbumTitle = previous.AlbumTitle,
                SourceAppId = previous.SourceAppId,
                AlbumArt = previous.AlbumArt,
                IsPlaying = previous.IsPlaying,
                HasSession = previous.HasSession,
                CanPlay = previous.CanPlay,
                CanPause = previous.CanPause,
                CanSkipNext = previous.CanSkipNext,
                CanSkipPrevious = previous.CanSkipPrevious,
                TimelineStart = timeline.StartTime,
                TimelineEnd = timeline.EndTime,
                Position = timeline.Position
            };

            _lastInfo = updated;
            SessionChanged?.Invoke(this, new MediaSessionChangedEventArgs { Session = updated });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[MediaSessionService] Timeline update failed: {ex.Message}");
        }
    }

    private async Task UpdateSessionInfoAsync(
        GlobalSystemMediaTransportControlsSession session,
        long version)
    {
        if (_disposed) return;

        try
        {
            var mediaProperties = await session.TryGetMediaPropertiesAsync();
            if (!CanPublish(session, version)) return;

            var playbackInfo = session.GetPlaybackInfo();
            var controls = playbackInfo.Controls;
            var timeline = session.GetTimelineProperties();

            BitmapSource? albumArt = null;
            if (mediaProperties.Thumbnail != null)
                albumArt = await ReadThumbnailAsync(mediaProperties.Thumbnail);

            if (!CanPublish(session, version)) return;

            var info = new MediaSessionInfo
            {
                Title = mediaProperties.Title ?? "Bilinmeyen medya",
                Artist = mediaProperties.Artist ?? string.Empty,
                AlbumTitle = mediaProperties.AlbumTitle ?? string.Empty,
                SourceAppId = session.SourceAppUserModelId ?? string.Empty,
                AlbumArt = albumArt,
                IsPlaying = playbackInfo.PlaybackStatus ==
                    GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                HasSession = true,
                CanPlay = controls?.IsPlayEnabled == true,
                CanPause = controls?.IsPauseEnabled == true,
                CanSkipNext = controls?.IsNextEnabled == true,
                CanSkipPrevious = controls?.IsPreviousEnabled == true,
                TimelineStart = timeline.StartTime,
                TimelineEnd = timeline.EndTime,
                Position = timeline.Position
            };

            _lastInfo = info;
            SessionChanged?.Invoke(this, new MediaSessionChangedEventArgs { Session = info });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[MediaSessionService] Error reading session info: {ex.Message}");

            // SessionsChanged/CurrentSessionChanged will reselect if this WinRT
            // object was removed. Keep the last published info during transient
            // property-read failures instead of flashing a false no-session state.
        }
    }

    private bool CanPublish(GlobalSystemMediaTransportControlsSession session, long version)
        => !_disposed && version == Volatile.Read(ref _updateVersion) && IsSelected(session);

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
            var controls = playbackInfo.Controls;
            bool isPlaying = playbackInfo.PlaybackStatus ==
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

            if (isPlaying)
            {
                if (controls?.IsPauseEnabled == true)
                    _ = _currentSession.TryPauseAsync().AsTask();
            }
            else if (controls?.IsPlayEnabled == true)
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

    public void NextTrack()
    {
        if (_currentSession == null) return;
        try
        {
            if (_currentSession.GetPlaybackInfo().Controls?.IsNextEnabled == true)
                _ = _currentSession.TrySkipNextAsync().AsTask();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MediaSessionService] Next failed: {ex.Message}");
        }
    }

    public void PreviousTrack()
    {
        if (_currentSession == null) return;
        try
        {
            if (_currentSession.GetPlaybackInfo().Controls?.IsPreviousEnabled == true)
                _ = _currentSession.TrySkipPreviousAsync().AsTask();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MediaSessionService] Previous failed: {ex.Message}");
        }
    }

    private void NotifyNoSession()
    {
        if (_disposed) return;
        _lastInfo = null;
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
            _sessionManager.CurrentSessionChanged -= OnManagerSessionChanged;
            _sessionManager.SessionsChanged -= OnManagerSessionsChanged;
            _sessionManager = null;
        }

        SessionChanged = null;
    }
}
