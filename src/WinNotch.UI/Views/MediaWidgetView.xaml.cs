// WinNotch.UI/Views/MediaWidgetView.xaml.cs
// WHY: Displays media session info from SMTC and provides playback controls.
// This view is ONLY visible when an active media session exists.
// When media stops, the notch returns to idle.
//
// PERFORMANCE: No timers, no polling. All updates come from
// MediaSessionService events (SMTC callbacks).
// When hidden (Collapsed), zero layout/render cost.

using System.Windows;
using WinNotch.Core.Services;

using UserControl = System.Windows.Controls.UserControl;

namespace WinNotch.UI.Views;

/// <summary>
/// Interaction logic for MediaWidgetView.xaml.
/// Displays album art, song info, and playback controls.
/// </summary>
public partial class MediaWidgetView : UserControl
{
    private MediaSessionInfo? _currentSession;

    public event EventHandler? PlayPauseRequested;
    public event EventHandler? NextTrackRequested;
    public event EventHandler? PreviousTrackRequested;

    public MediaWidgetView()
    {
        InitializeComponent();
    }

    public void SetSessionInfo(MediaSessionInfo session)
    {
        _currentSession = session;

        Dispatcher.Invoke(() =>
        {
            TitleText.Text = session.Title;
            ArtistText.Text = session.Artist;
            PlayPauseButton.Content = session.IsPlaying ? "⏸" : "▶";
            AlbumArtImage.Source = session.AlbumArt;
        });
    }

    private MainWindow? GetHostWindow() => Window.GetWindow(this) as MainWindow;

    private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
    {
        // Keep the public events for future consumers, but make the current
        // WinNotch view functional even when no external handler is attached.
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
