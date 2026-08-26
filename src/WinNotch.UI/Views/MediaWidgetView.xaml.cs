// WinNotch.UI/Views/MediaWidgetView.xaml.cs
// WHY: Displays media session info from SMTC and provides playback controls.
// This view is ONLY visible when an active media session exists.
// When media stops, the notch returns to idle.
//
// PERFORMANCE: No timers, no polling. All updates come from
// MediaSessionService events (SMTC callbacks).
// When hidden (Collapsed), zero layout/render cost.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using WinNotch.Core.Services;

namespace WinNotch.UI.Views;

/// <summary>
/// Interaction logic for MediaWidgetView.xaml.
/// Displays album art, song info, and playback controls.
/// </summary>
public partial class MediaWidgetView : UserControl
{
    private MediaSessionInfo? _currentSession;

    /// <summary>
    /// Fired when user clicks play/pause.
    /// </summary>
    public event EventHandler? PlayPauseRequested;

    /// <summary>
    /// Fired when user clicks next track.
    /// </summary>
    public event EventHandler? NextTrackRequested;

    /// <summary>
    /// Fired when user clicks previous track.
    /// </summary>
    public event EventHandler? PreviousTrackRequested;

    public MediaWidgetView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Updates the display with new session info.
    /// </summary>
    public void SetSessionInfo(MediaSessionInfo session)
    {
        _currentSession = session;

        Dispatcher.Invoke(() =>
        {
            TitleText.Text = session.Title;
            ArtistText.Text = session.Artist;

            // Update play/pause button icon
            PlayPauseButton.Content = session.IsPlaying ? "⏸" : "▶";

            // Update album art
            if (session.AlbumArt != null)
            {
                AlbumArtImage.Source = session.AlbumArt;
            }
            else
            {
                // Default placeholder
                AlbumArtImage.Source = null;
            }
        });
    }

    private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
    {
        PlayPauseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        NextTrackRequested?.Invoke(this, EventArgs.Empty);
    }

    private void PrevButton_Click(object sender, RoutedEventArgs e)
    {
        PreviousTrackRequested?.Invoke(this, EventArgs.Empty);
    }
}
