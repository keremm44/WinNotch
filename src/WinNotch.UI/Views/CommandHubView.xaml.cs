using System.Windows;
using System.Windows.Controls;
using WinNotch.Common;

using UserControl = System.Windows.Controls.UserControl;

namespace WinNotch.UI.Views;

public sealed class ClipboardTextCopyRequestedEventArgs : EventArgs
{
    public required string Text { get; init; }
    public bool Succeeded { get; set; }
}

public sealed class TemporaryNoteChangedEventArgs : EventArgs
{
    public required string Text { get; init; }
}

public sealed class CommandHubEditorModeEventArgs : EventArgs
{
    public required bool IsActive { get; init; }
}

public sealed class TimerStartRequestedEventArgs : EventArgs
{
    public required TimeSpan Duration { get; init; }
}

public sealed class QrClipboardTextRequestedEventArgs : EventArgs
{
    public string? Text { get; set; }
}

public sealed class QrImageActionRequestedEventArgs : EventArgs
{
    public required byte[] PngBytes { get; init; }
    public required bool SaveToFile { get; init; }
    public bool Succeeded { get; set; }
}

public sealed class CommandHubSizeChangedEventArgs : EventArgs
{
    public required double Height { get; init; }
}

public partial class CommandHubView : UserControl
{
    private AppearanceSettings _appearance = new();
    private LastMeaningfulClipboardContext? _clipboardContext;
    private string _smartClipboardSource = string.Empty;
    private bool _updatingTemporaryNote;
    private bool _editorModeActive;
    private byte[]? _qrPngBytes;

    public double PreferredHeight { get; private set; } = Constants.NotchCommandHubHeight;
    public bool HasOpenPopup => TransformKindCombo.IsDropDownOpen || TimerDurationCombo.IsDropDownOpen;

    public event EventHandler? ClipboardRequested;
    public event EventHandler? ShelfRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? SmartClipboardRequested;
    public event EventHandler<ClipboardTextCopyRequestedEventArgs>? ClipboardTextCopyRequested;
    public event EventHandler? TemporaryNoteRequested;
    public event EventHandler<TemporaryNoteChangedEventArgs>? TemporaryNoteChanged;
    public event EventHandler<CommandHubEditorModeEventArgs>? EditorModeChanged;
    public event EventHandler? TimerRequested;
    public event EventHandler<TimerStartRequestedEventArgs>? TimerStartRequested;
    public event EventHandler? TimerPauseResumeRequested;
    public event EventHandler? TimerCancelRequested;
    public event EventHandler<QrClipboardTextRequestedEventArgs>? QrClipboardTextRequested;
    public event EventHandler<QrImageActionRequestedEventArgs>? QrImageActionRequested;
    public event EventHandler<CommandHubSizeChangedEventArgs>? PreferredSizeChanged;

    public CommandHubView()
    {
        InitializeComponent();
        IsVisibleChanged += CommandHubView_IsVisibleChanged;
        Unloaded += CommandHubView_Unloaded;
    }

    public void ApplyAppearance(AppearanceSettings settings)
    {
        _appearance = settings ?? new AppearanceSettings();
        AppearanceResolver.NormalizeInPlace(_appearance);

        DensityProfile density = AppearanceResolver.ResolveDensity(_appearance);
        ClipboardHintText.FontSize = 8.5 * density.FontScale;
        ShelfHintText.FontSize = 8.5 * density.FontScale;
        RenderClipboardContext();
    }

    public void SetClipboardContext(LastMeaningfulClipboardContext? context)
    {
        _clipboardContext = context;
        RenderClipboardContext();
    }

    public void SetShelfItemCount(int itemCount)
    {
        int safeCount = Math.Clamp(itemCount, 0, Constants.MaxShelfItems);
        ShelfButton.IsEnabled = safeCount > 0;
        ShelfHintText.Text = safeCount > 0 ? $"{safeCount} öğe" : "Raf boş";
    }

    public void SetSmartClipboardText(string? text)
    {
        _smartClipboardSource = text ?? string.Empty;
        SmartClipboardInput.Text = _smartClipboardSource;
        ApplySelectedTransformation();
    }

    public void SetTemporaryNote(string? text)
    {
        _updatingTemporaryNote = true;
        TemporaryNoteTextBox.Text = text ?? string.Empty;
        TemporaryNoteTextBox.CaretIndex = TemporaryNoteTextBox.Text.Length;
        _updatingTemporaryNote = false;
        UpdateTemporaryNoteStatus();
        Dispatcher.BeginInvoke(() => TemporaryNoteTextBox.Focus());
    }

    public void SetTimerState(CountdownTimerStatus status, TimeSpan remaining)
    {
        TimeSpan safe = remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
        int totalHours = (int)safe.TotalHours;
        TimerRemainingText.Text = totalHours > 0
            ? $"{totalHours:00}:{safe.Minutes:00}:{safe.Seconds:00}"
            : $"{safe.Minutes:00}:{safe.Seconds:00}";
        TimerHomeHintText.Text = status switch
        {
            CountdownTimerStatus.Running => TimerRemainingText.Text,
            CountdownTimerStatus.Paused => "Duraklatıldı",
            CountdownTimerStatus.Completed => "Tamamlandı",
            _ => "Hazır"
        };
        TimerStatusText.Text = status switch
        {
            CountdownTimerStatus.Running => "Çalışıyor",
            CountdownTimerStatus.Paused => "Duraklatıldı",
            CountdownTimerStatus.Completed => "Süre doldu",
            _ => "Hazır"
        };
        TimerStartButton.Content = status == CountdownTimerStatus.Idle ? "Başlat" : "Yeniden başlat";
        TimerPauseButton.IsEnabled = status is CountdownTimerStatus.Running or CountdownTimerStatus.Paused;
        TimerPauseButton.Content = status == CountdownTimerStatus.Paused ? "Devam et" : "Duraklat";
        TimerCancelButton.IsEnabled = status != CountdownTimerStatus.Idle;
    }

    private void RenderClipboardContext()
    {
        ClipboardButton.IsEnabled = _clipboardContext != null;
        ClipboardHintText.Text = _clipboardContext == null
            ? "Bağlam yok"
            : _clipboardContext.ContentType switch
            {
                ClipboardContentType.Url => "Son bağlantı",
                ClipboardContentType.FilePath => "Son dosya yolu",
                ClipboardContentType.Email => "Son e-posta",
                _ => "Son pano"
            };
    }

    private void ClipboardButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (_clipboardContext != null)
            ClipboardRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ShelfButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (ShelfButton.IsEnabled)
            ShelfRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SmartClipboardButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        HubHomePanel.Visibility = Visibility.Collapsed;
        SmartClipboardPanel.Visibility = Visibility.Visible;
        SetPreferredHeight(200);
        SetEditorMode(true);
        SmartClipboardStatusText.Text = "Panodan metin alınıyor";
        SmartClipboardRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SmartClipboardBackButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        SmartClipboardPanel.Visibility = Visibility.Collapsed;
        HubHomePanel.Visibility = Visibility.Visible;
        SetPreferredHeight(Constants.NotchCommandHubHeight);
        SetEditorMode(false);
        _smartClipboardSource = string.Empty;
        SmartClipboardInput.Clear();
        SmartClipboardOutput.Clear();
    }

    private void TransformKindCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsInitialized)
            ApplySelectedTransformation();
    }

    private void ToolComboBox_DropDownClosed(object sender, EventArgs e)
    {
        if (sender is System.Windows.Controls.ComboBox combo)
            Dispatcher.BeginInvoke(() => combo.Focus());
    }

    private void ApplySelectedTransformation()
    {
        ClipboardTransformKind kind = ResolveSelectedTransform();
        ClipboardTransformResult result = ClipboardTransformService.Transform(
            _smartClipboardSource,
            kind);

        SmartClipboardOutput.Text = result.Output;
        CopyTransformButton.IsEnabled = result.Success && result.Output.Length > 0;
        SmartClipboardStatusText.Text = result.Success
            ? $"{result.Output.Length:N0} karakter"
            : result.Error ?? "Dönüşüm başarısız";
    }

    private ClipboardTransformKind ResolveSelectedTransform()
    {
        if (TransformKindCombo.SelectedItem is ComboBoxItem item &&
            item.Tag is string value &&
            Enum.TryParse(value, ignoreCase: true, out ClipboardTransformKind kind))
        {
            return kind;
        }

        return ClipboardTransformKind.CleanWhitespace;
    }

    private void CopyTransformButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (!CopyTransformButton.IsEnabled || string.IsNullOrEmpty(SmartClipboardOutput.Text))
            return;

        var request = new ClipboardTextCopyRequestedEventArgs
        {
            Text = SmartClipboardOutput.Text
        };
        ClipboardTextCopyRequested?.Invoke(this, request);
        SmartClipboardStatusText.Text = request.Succeeded
            ? "Panoya kopyalandı"
            : "Pano meşgul, tekrar deneyin";
    }

    private void QrButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        HubHomePanel.Visibility = Visibility.Collapsed;
        QrPanel.Visibility = Visibility.Visible;
        SetPreferredHeight(300);
        SetEditorMode(true);
        Dispatcher.BeginInvoke(() => QrInputTextBox.Focus());
    }

    private void QrBackButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        ClearQrResult(clearInput: true);
        QrPanel.Visibility = Visibility.Collapsed;
        HubHomePanel.Visibility = Visibility.Visible;
        SetPreferredHeight(Constants.NotchCommandHubHeight);
        SetEditorMode(false);
    }

    private void QrPasteButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        var request = new QrClipboardTextRequestedEventArgs();
        QrClipboardTextRequested?.Invoke(this, request);
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            QrStatusText.Text = "Panoda metin yok";
            return;
        }
        QrInputTextBox.Text = request.Text.Length <= QrInputTextBox.MaxLength
            ? request.Text
            : request.Text[..QrInputTextBox.MaxLength];
        QrInputTextBox.CaretIndex = QrInputTextBox.Text.Length;
        QrStatusText.Text = "Panodan alındı";
    }

    private void QrGenerateButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        ClearQrResult(clearInput: false);
        QrCodeRenderResult result = QrCodeGeneratorService.Generate(QrInputTextBox.Text);
        if (!result.Success || result.PngBytes == null || result.Image == null)
        {
            QrStatusText.Text = result.Error ?? "QR oluşturulamadı";
            return;
        }

        _qrPngBytes = result.PngBytes;
        QrPreviewImage.Source = result.Image;
        QrCopyButton.IsEnabled = true;
        QrSaveButton.IsEnabled = true;
        QrStatusText.Text = "QR hazır";
    }

    private void QrCopyButton_Click(object sender, RoutedEventArgs e)
        => RequestQrImageAction(e, saveToFile: false);

    private void QrSaveButton_Click(object sender, RoutedEventArgs e)
        => RequestQrImageAction(e, saveToFile: true);

    private void RequestQrImageAction(RoutedEventArgs e, bool saveToFile)
    {
        e.Handled = true;
        if (_qrPngBytes == null)
            return;

        var request = new QrImageActionRequestedEventArgs
        {
            PngBytes = _qrPngBytes,
            SaveToFile = saveToFile
        };
        QrImageActionRequested?.Invoke(this, request);
        QrStatusText.Text = request.Succeeded
            ? saveToFile ? "PNG kaydedildi" : "Görsel panoya kopyalandı"
            : saveToFile ? "Dosya kaydedilmedi" : "Pano meşgul";
    }

    private void ClearQrResult(bool clearInput)
    {
        _qrPngBytes = null;
        QrPreviewImage.Source = null;
        QrCopyButton.IsEnabled = false;
        QrSaveButton.IsEnabled = false;
        if (clearInput)
            QrInputTextBox.Clear();
    }

    private void SetPreferredHeight(double height)
    {
        if (Math.Abs(PreferredHeight - height) < 0.5)
            return;
        PreferredHeight = height;
        PreferredSizeChanged?.Invoke(this, new CommandHubSizeChangedEventArgs { Height = height });
    }

    private void TimerButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        HubHomePanel.Visibility = Visibility.Collapsed;
        TimerPanel.Visibility = Visibility.Visible;
        SetPreferredHeight(180);
        SetEditorMode(true);
        TimerRequested?.Invoke(this, EventArgs.Empty);
    }

    private void TimerBackButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        TimerPanel.Visibility = Visibility.Collapsed;
        HubHomePanel.Visibility = Visibility.Visible;
        SetPreferredHeight(Constants.NotchCommandHubHeight);
        SetEditorMode(false);
    }

    private void TimerStartButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        int minutes;
        if (TimerDurationCombo.SelectedItem is ComboBoxItem item &&
            item.Tag is string preset &&
            int.TryParse(preset, out int presetMinutes))
        {
            minutes = presetMinutes;
        }
        else if (!int.TryParse(TimerDurationCombo.Text.Trim(), out minutes) ||
                 minutes is < 1 or > 1440)
        {
            TimerStatusText.Text = "1–1440 dakika girin";
            return;
        }

        TimerStartRequested?.Invoke(this, new TimerStartRequestedEventArgs
        {
            Duration = TimeSpan.FromMinutes(minutes)
        });
    }

    private void TimerPauseButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        TimerPauseResumeRequested?.Invoke(this, EventArgs.Empty);
    }

    private void TimerCancelButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        TimerCancelRequested?.Invoke(this, EventArgs.Empty);
    }

    private void TemporaryNoteButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        HubHomePanel.Visibility = Visibility.Collapsed;
        TemporaryNotePanel.Visibility = Visibility.Visible;
        SetPreferredHeight(210);
        SetEditorMode(true);
        TemporaryNoteRequested?.Invoke(this, EventArgs.Empty);
    }

    private void TemporaryNoteBackButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        TemporaryNotePanel.Visibility = Visibility.Collapsed;
        HubHomePanel.Visibility = Visibility.Visible;
        SetPreferredHeight(Constants.NotchCommandHubHeight);
        SetEditorMode(false);
    }

    private void TemporaryNoteTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingTemporaryNote)
            return;

        UpdateTemporaryNoteStatus();
        TemporaryNoteChanged?.Invoke(this, new TemporaryNoteChangedEventArgs
        {
            Text = TemporaryNoteTextBox.Text
        });
    }

    private void ClearTemporaryNoteButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        TemporaryNoteTextBox.Clear();
        TemporaryNoteTextBox.Focus();
    }

    private void CopyTemporaryNoteButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (string.IsNullOrEmpty(TemporaryNoteTextBox.Text))
        {
            TemporaryNoteStatusText.Text = "Not boş";
            return;
        }

        var request = new ClipboardTextCopyRequestedEventArgs
        {
            Text = TemporaryNoteTextBox.Text
        };
        ClipboardTextCopyRequested?.Invoke(this, request);
        TemporaryNoteStatusText.Text = request.Succeeded
            ? "Panoya kopyalandı"
            : "Pano meşgul, tekrar deneyin";
    }

    private void UpdateTemporaryNoteStatus()
    {
        TemporaryNoteStatusText.Text = TemporaryNoteTextBox.Text.Length == 0
            ? "Bu oturumda saklanır"
            : $"{TemporaryNoteTextBox.Text.Length:N0} / 10.000";
    }

    private void SetEditorMode(bool active)
    {
        if (_editorModeActive == active)
            return;

        _editorModeActive = active;
        EditorModeChanged?.Invoke(this, new CommandHubEditorModeEventArgs
        {
            IsActive = active
        });
    }

    private void CommandHubView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
            SurfaceMotion.Reveal(this, 1.5, 100);
    }

    private void CommandHubView_Unloaded(object sender, RoutedEventArgs e)
    {
        SetEditorMode(false);
        IsVisibleChanged -= CommandHubView_IsVisibleChanged;
        Unloaded -= CommandHubView_Unloaded;
        ClipboardRequested = null;
        ShelfRequested = null;
        SettingsRequested = null;
        SmartClipboardRequested = null;
        ClipboardTextCopyRequested = null;
        TemporaryNoteRequested = null;
        TemporaryNoteChanged = null;
        EditorModeChanged = null;
        TimerRequested = null;
        TimerStartRequested = null;
        TimerPauseResumeRequested = null;
        TimerCancelRequested = null;
        QrClipboardTextRequested = null;
        QrImageActionRequested = null;
        PreferredSizeChanged = null;
        ClearQrResult(clearInput: true);
        _clipboardContext = null;
        _smartClipboardSource = string.Empty;
    }
}
