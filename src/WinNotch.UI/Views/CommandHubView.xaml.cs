using System.Windows;
using System.Windows.Controls;
using WinNotch.Common;

using UserControl = System.Windows.Controls.UserControl;

namespace WinNotch.UI.Views;

public partial class CommandHubView : UserControl
{
    private AppearanceSettings _appearance = new();
    private LastMeaningfulClipboardContext? _clipboardContext;

    public event EventHandler? ClipboardRequested;
    public event EventHandler? ShelfRequested;
    public event EventHandler? SettingsRequested;

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
        _clipboardContext = null;
    }
}
