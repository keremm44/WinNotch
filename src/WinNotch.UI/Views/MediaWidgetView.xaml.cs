// WinNotch.UI/Views/MediaWidgetView.xaml.cs

using System.Windows;
using WinNotch.Core.Services;

using UserControl = System.Windows.Controls.UserControl;

namespace WinNotch.UI.Views;

public partial class MediaWidgetView : UserControl
{
    private readonly System.Windows.Threading.DispatcherTimer _progressTimer;
    private MediaSessionInfo? _currentSession;
    private TimeSpan _basePosition;
    private TimeSpan _timelineStart;
    private TimeSpan _timelineEnd;
    private DateTime _timelineCapturedAt;
    private bool _isPlaying;

    public event EventHandler? PlayPauseRequested;
    public event EventHandler? NextTrackRequested;
    public event EventHandler? PreviousTrackRequested;

    public MediaWidgetView()
    {
        InitializeComponent();

        _progressTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _progressTimer.Tick += ProgressTimer_Tick;
        IsVisibleChanged += MediaWidgetView_IsVisibleChanged;
        Unloaded += MediaWidgetView_Unloaded;
    }

    public void SetSessionInfo(MediaSessionInfo session)
    {
        _currentSession = session;

        Dispatcher.Invoke(() =>
        {
            _basePosition = session.Position;
            _timelineStart = session.TimelineStart;
            _timelineEnd = session.TimelineEnd;
            _timelineCapturedAt = DateTime.UtcNow;
            _isPlaying = session.IsPlaying;

            TitleText.Text = string.IsNullOrWhiteSpace(session.Title) ? "Medya" : session.Title;

            string creator = !string.IsNullOrWhiteSpace(session.Artist)
                ? session.Artist
                : session.AlbumTitle;
            string source = FormatSourceLabel(session.SourceAppId);
            SubtitleText.Text = JoinSecondary(creator, source);

            PlayPauseButton.Content = session.IsPlaying ? "⏸" : "▶";
            PlayPauseButton.IsEnabled = session.IsPlaying ? session.CanPause : session.CanPlay;
            PrevButton.IsEnabled = session.CanSkipPrevious;
            NextButton.IsEnabled = session.CanSkipNext;
            AlbumArtImage.Source = session.AlbumArt;

            UpdateProgressVisual();
            UpdateProgressTimerState();
        });
    }

    private MainWindow? GetHostWindow() => Window.GetWindow(this) as MainWindow;

    private void MediaWidgetView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
            SurfaceMotion.Reveal(this);

        UpdateProgressTimerState();
    }

    private void MediaWidgetView_Unloaded(object sender, RoutedEventArgs e)
    {
        _progressTimer.Stop();
        _progressTimer.Tick -= ProgressTimer_Tick;
        IsVisibleChanged -= MediaWidgetView_IsVisibleChanged;
        Unloaded -= MediaWidgetView_Unloaded;
    }

    private void ProgressTimer_Tick(object? sender, EventArgs e)
        => UpdateProgressVisual();

    private void ProgressTrack_SizeChanged(object sender, SizeChangedEventArgs e)
        => UpdateProgressVisual();

    private void UpdateProgressTimerState()
    {
        bool hasTimeline = _timelineEnd > _timelineStart;
        bool shouldRun = IsVisible && _isPlaying && hasTimeline;

        if (shouldRun)
            _progressTimer.Start();
        else
            _progressTimer.Stop();
    }

    private void UpdateProgressVisual()
    {
        TimeSpan duration = _timelineEnd - _timelineStart;
        if (duration <= TimeSpan.FromSeconds(1))
        {
            ProgressTrack.Visibility = Visibility.Collapsed;
            ProgressFill.Width = 0;
            return;
        }

        ProgressTrack.Visibility = Visibility.Visible;

        TimeSpan position = _basePosition;
        if (_isPlaying && _timelineCapturedAt != default)
            position += DateTime.UtcNow - _timelineCapturedAt;

        double elapsedMs = Math.Clamp(
            (position - _timelineStart).TotalMilliseconds,
            0,
            duration.TotalMilliseconds);
        double ratio = duration.TotalMilliseconds <= 0
            ? 0
            : elapsedMs / duration.TotalMilliseconds;

        ProgressFill.Width = Math.Max(0, ProgressTrack.ActualWidth * ratio);
    }

    private static string JoinSecondary(string creator, string source)
    {
        bool hasCreator = !string.IsNullOrWhiteSpace(creator);
        bool hasSource = !string.IsNullOrWhiteSpace(source);

        if (hasCreator && hasSource)
            return $"{creator} · {source}";
        if (hasCreator)
            return creator;
        if (hasSource)
            return source;
        return "Sistem medyası";
    }

    private static string FormatSourceLabel(string? sourceAppId)
    {
        if (string.IsNullOrWhiteSpace(sourceAppId))
            return string.Empty;

        string value = sourceAppId.ToLowerInvariant();
        if (value.Contains("spotify")) return "Spotify";
        if (value.Contains("chrome")) return "Chrome";
        if (value.Contains("msedge") || value.Contains("microsoftedge")) return "Edge";
        if (value.Contains("firefox")) return "Firefox";
        if (value.Contains("vlc")) return "VLC";
        if (value.Contains("applemusic") || value.Contains("itunes")) return "Apple Music";
        if (value.Contains("musicbee")) return "MusicBee";

        return "Medya";
    }

    private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
    {
        GetHostWindow()?.MediaService?.TogglePlayPause();
        PlayPauseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        GetHostWindow()?.MediaService?.NextTrack();
        NextTrackRequested?.Invoke(this, EventArgs.Empty);
    }

    private void PrevButton_Click(object sender, RoutedEventArgs e)
    {
        GetHostWindow()?.MediaService?.PreviousTrack();
        PreviousTrackRequested?.Invoke(this, EventArgs.Empty);
    }
}
