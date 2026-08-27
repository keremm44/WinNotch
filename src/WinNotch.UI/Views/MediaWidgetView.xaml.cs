// WinNotch.UI/Views/MediaWidgetView.xaml.cs

using System.Windows;
using WinNotch.Core.Services;

using UserControl = System.Windows.Controls.UserControl;

namespace WinNotch.UI.Views;

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
            TitleText.Text = string.IsNullOrWhiteSpace(session.Title) ? "Medya" : session.Title;
            ArtistText.Text = string.IsNullOrWhiteSpace(session.Artist)
                ? session.AlbumTitle
                : session.Artist;

            PlayPauseButton.Content = session.IsPlaying ? "⏸" : "▶";
            PlayPauseButton.IsEnabled = session.IsPlaying ? session.CanPause : session.CanPlay;
            PrevButton.IsEnabled = session.CanSkipPrevious;
            NextButton.IsEnabled = session.CanSkipNext;
            AlbumArtImage.Source = session.AlbumArt;
        });
    }

    private MainWindow? GetHostWindow() => Window.GetWindow(this) as MainWindow;

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
