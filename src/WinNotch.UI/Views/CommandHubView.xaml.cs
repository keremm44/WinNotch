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

public partial class CommandHubView : UserControl
{
    private AppearanceSettings _appearance = new();
    private LastMeaningfulClipboardContext? _clipboardContext;
    private string _smartClipboardSource = string.Empty;

    public event EventHandler? ClipboardRequested;
    public event EventHandler? ShelfRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? SmartClipboardRequested;
    public event EventHandler<ClipboardTextCopyRequestedEventArgs>? ClipboardTextCopyRequested;

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
        SmartClipboardStatusText.Text = "Panodan metin alınıyor";
        SmartClipboardRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SmartClipboardBackButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        SmartClipboardPanel.Visibility = Visibility.Collapsed;
        HubHomePanel.Visibility = Visibility.Visible;
        _smartClipboardSource = string.Empty;
        SmartClipboardInput.Clear();
        SmartClipboardOutput.Clear();
    }

    private void TransformKindCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsInitialized)
            ApplySelectedTransformation();
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

    private void CommandHubView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
            SurfaceMotion.Reveal(this, 1.5, 100);
    }

    private void CommandHubView_Unloaded(object sender, RoutedEventArgs e)
    {
        IsVisibleChanged -= CommandHubView_IsVisibleChanged;
        Unloaded -= CommandHubView_Unloaded;
        ClipboardRequested = null;
        ShelfRequested = null;
        SettingsRequested = null;
        SmartClipboardRequested = null;
        ClipboardTextCopyRequested = null;
        _clipboardContext = null;
        _smartClipboardSource = string.Empty;
    }
}
