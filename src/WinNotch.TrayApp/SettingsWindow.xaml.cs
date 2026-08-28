// WinNotch.TrayApp/SettingsWindow.xaml.cs

using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using WinNotch.Common;
using WinNotch.UI;

namespace WinNotch.TrayApp;

public partial class SettingsWindow : Window
{
    private readonly ModuleSettings _settings;
    private System.Windows.Threading.DispatcherTimer? _statsTimer;
    private bool _isLoadingSettings;
    private TimeSpan _lastCpuTime;
    private DateTime _lastCpuSampleAt;

    public event EventHandler<ModuleSettings>? SettingsChanged;

    public SettingsWindow(ModuleSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        _settings.Appearance ??= new AppearanceSettings();
        AppearanceResolver.NormalizeInPlace(_settings.Appearance);

        double availableHeight = Math.Max(560, SystemParameters.WorkArea.Height - 32);
        MaxHeight = availableHeight;
        Height = Math.Min(720, availableHeight);

        LoadSettings();
        UpdateDiagnosticsState();
        SourceInitialized += SettingsWindow_SourceInitialized;
        Loaded += SettingsWindow_Loaded;
        Closed += SettingsWindow_Closed;
    }

    private void SettingsWindow_SourceInitialized(object? sender, EventArgs e)
    {
        SourceInitialized -= SettingsWindow_SourceInitialized;
        ApplyWindowBackdrop();
    }

    internal void RefreshSystemVisuals() => ApplyWindowBackdrop();

    private void ApplyWindowBackdrop()
    {
        bool darkTheme = !AppearanceThemeManager.IsLightTheme(_settings.Appearance);
        bool applied = WindowBackdrop.TryApply(this, darkTheme);
        RootChrome.SetResourceReference(
            Border.BackgroundProperty,
            applied ? "Brush.Window.BackdropTint" : "Brush.Window.Base");
    }

    private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= SettingsWindow_Loaded;
        Button? closeButton = FindButtonByContent(this, "×");
        if (closeButton != null)
            ApplyCloseButtonVisual(closeButton);
    }

    private static Button? FindButtonByContent(DependencyObject root, string content)
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is Button button && string.Equals(button.Content?.ToString(), content, StringComparison.Ordinal))
                return button;

            Button? nested = FindButtonByContent(child, content);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private static void ApplyCloseButtonVisual(Button button)
    {
        button.Width = 28;
        button.Height = 28;
        button.Padding = new Thickness(0);
        button.FontSize = 14;
        button.FontWeight = FontWeights.Normal;
        button.HorizontalAlignment = HorizontalAlignment.Center;
        button.VerticalAlignment = VerticalAlignment.Center;
        button.BorderThickness = new Thickness(0);
        button.Background = Brushes.Transparent;
        button.SetResourceReference(ForegroundProperty, "Brush.Text.Secondary");

        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "CloseSurface";
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        border.SetValue(Border.BackgroundProperty, Brushes.Transparent);

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        presenter.SetValue(ContentPresenter.ContentProperty, new System.Windows.Data.Binding("Content")
        {
            RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
        });
        presenter.SetValue(System.Windows.Documents.TextElement.ForegroundProperty, new System.Windows.Data.Binding("Foreground")
        {
            RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
        });
        border.AppendChild(presenter);
        template.VisualTree = border;

        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty,
            new DynamicResourceExtension("Brush.Surface.Hover"), "CloseSurface"));
        template.Triggers.Add(hover);

        var pressed = new Trigger { Property = ButtonBase.IsPressedProperty, Value = true };
        pressed.Setters.Add(new Setter(Border.BackgroundProperty,
            new DynamicResourceExtension("Brush.Surface.Pressed"), "CloseSurface"));
        template.Triggers.Add(pressed);

        button.Template = template;
    }

    private void SettingsWindow_Closed(object? sender, EventArgs e)
    {
        ReleaseStatsTimer();
        Closed -= SettingsWindow_Closed;
    }

    private void LoadSettings()
    {
        _isLoadingSettings = true;
        try
        {
            ModuleACheckBox.IsChecked = _settings.ModuleA_DragDrop;
            ModuleBCheckBox.IsChecked = _settings.ModuleB_Clipboard;
            ModuleCCheckBox.IsChecked = _settings.ModuleC_Media;
            ModuleECheckBox.IsChecked = _settings.ModuleE_Screenshot;
            AutoStartCheckBox.IsChecked = _settings.AutoStart;
            DiagnosticsCheckBox.IsChecked = _settings.DiagnosticsEnabled;

            SelectTaggedRadio(
                _settings.VisibilityMode,
                "Auto",
                VisibilityAutoRadio,
                VisibilityAlwaysRadio,
                VisibilityHiddenRadio);
            SelectTaggedRadio(
                _settings.ReactionLevel,
                "Balanced",
                ReactionQuietRadio,
                ReactionBalancedRadio,
                ReactionActiveRadio);

            AppearanceCard.LoadAppearance(_settings.Appearance);

            MonitorComboBox.Items.Clear();
            var screens = System.Windows.Forms.Screen.AllScreens;
            for (int i = 0; i < screens.Length; i++)
            {
                var screen = screens[i];
                string label = screens.Length == 1
                    ? $"Varsayılan monitör · {screen.Bounds.Width}×{screen.Bounds.Height}"
                    : $"Monitör {i + 1} · {screen.Bounds.Width}×{screen.Bounds.Height}";

                MonitorComboBox.Items.Add(new ComboBoxItem
                {
                    Content = label,
                    Tag = i
                });
            }

            if (MonitorComboBox.Items.Count > 0)
                MonitorComboBox.SelectedIndex = Math.Clamp(
                    _settings.TargetMonitorIndex,
                    0,
                    MonitorComboBox.Items.Count - 1);
        }
        finally
        {
            _isLoadingSettings = false;
        }
    }

    private static void SelectTaggedRadio(
        string? value,
        string fallback,
        params RadioButton[] radios)
    {
        string target = string.IsNullOrWhiteSpace(value) ? fallback : value;
        RadioButton selected = radios.FirstOrDefault(r =>
            string.Equals(r.Tag?.ToString(), target, StringComparison.OrdinalIgnoreCase))
            ?? radios[0];
        selected.IsChecked = true;
    }

    private void ModuleCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingSettings) return;

        _settings.ModuleA_DragDrop = ModuleACheckBox.IsChecked == true;
        _settings.ModuleB_Clipboard = ModuleBCheckBox.IsChecked == true;
        _settings.ModuleC_Media = ModuleCCheckBox.IsChecked == true;
        _settings.ModuleE_Screenshot = ModuleECheckBox.IsChecked == true;
        OnSettingsChanged();
    }

    private void VisibilityModeRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoadingSettings || sender is not RadioButton { IsChecked: true } radio)
            return;

        _settings.VisibilityMode = radio.Tag?.ToString() ?? "Auto";
        OnSettingsChanged();
    }

    private void ReactionLevelRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoadingSettings || sender is not RadioButton { IsChecked: true } radio)
            return;

        _settings.ReactionLevel = radio.Tag?.ToString() ?? "Balanced";
        OnSettingsChanged();
    }

    private void AppearanceCard_AppearanceChanged(object? sender, AppearanceSettings appearance)
    {
        if (_isLoadingSettings) return;
        _settings.Appearance = appearance;
        ApplyWindowBackdrop();
        OnSettingsChanged();
    }

    private void MonitorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingSettings || MonitorComboBox.SelectedIndex < 0) return;
        _settings.TargetMonitorIndex = MonitorComboBox.SelectedIndex;
        OnSettingsChanged();
    }

    private void AutoStartCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingSettings) return;
        _settings.AutoStart = AutoStartCheckBox.IsChecked == true;
        TrayIconManager.SetAutoStart(_settings.AutoStart);
        OnSettingsChanged();
    }

    private void DiagnosticsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingSettings) return;
        _settings.DiagnosticsEnabled = DiagnosticsCheckBox.IsChecked == true;
        UpdateDiagnosticsState();
        OnSettingsChanged();
    }

    private void UpdateDiagnosticsState()
    {
        if (_settings.DiagnosticsEnabled)
        {
            PerformancePanel.Visibility = Visibility.Visible;
            using var process = Process.GetCurrentProcess();
            _lastCpuTime = process.TotalProcessorTime;
            _lastCpuSampleAt = DateTime.UtcNow;
            StatsTimer_Tick(null, EventArgs.Empty);
            _statsTimer ??= CreateStatsTimer();
            _statsTimer.Start();
        }
        else
        {
            ReleaseStatsTimer();
            PerformancePanel.Visibility = Visibility.Collapsed;
        }
    }

    private System.Windows.Threading.DispatcherTimer CreateStatsTimer()
    {
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        timer.Tick += StatsTimer_Tick;
        return timer;
    }

    private void ReleaseStatsTimer()
    {
        if (_statsTimer == null) return;
        _statsTimer.Stop();
        _statsTimer.Tick -= StatsTimer_Tick;
        _statsTimer = null;
    }

    private void StatsTimer_Tick(object? sender, EventArgs e)
    {
        if (!_settings.DiagnosticsEnabled) return;

        try
        {
            using var process = Process.GetCurrentProcess();
            process.Refresh();
            const double bytesPerMegabyte = 1024.0 * 1024.0;
            double workingSetMB = process.WorkingSet64 / bytesPerMegabyte;
            double privateBytesMB = process.PrivateMemorySize64 / bytesPerMegabyte;
            double managedHeapMB = GC.GetTotalMemory(forceFullCollection: false) / bytesPerMegabyte;

            DateTime now = DateTime.UtcNow;
            TimeSpan cpuNow = process.TotalProcessorTime;
            double elapsedMs = Math.Max(1, (now - _lastCpuSampleAt).TotalMilliseconds);
            double cpuDeltaMs = Math.Max(0, (cpuNow - _lastCpuTime).TotalMilliseconds);
            double cpuPercent = cpuDeltaMs / (elapsedMs * Environment.ProcessorCount) * 100.0;

            _lastCpuTime = cpuNow;
            _lastCpuSampleAt = now;

            RamUsageText.Text = $"{workingSetMB:F1} / {privateBytesMB:F1} / {managedHeapMB:F1} MB";
            CpuUsageText.Text = $"{cpuPercent:F2}%";
            RuntimeUsageText.Text = $"{process.Threads.Count} / {process.HandleCount}";
            RamUsageText.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Text.Primary");
            RuntimeUsageText.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Text.Primary");
            CpuUsageText.SetResourceReference(
                TextBlock.ForegroundProperty,
                cpuPercent <= 1.0 ? "Brush.Semantic.Success" : "Brush.Text.Primary");
        }
        catch
        {
            RamUsageText.Text = "—";
            CpuUsageText.Text = "—";
            RuntimeUsageText.Text = "—";
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        try { DragMove(); } catch { }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void OnSettingsChanged()
        => SettingsChanged?.Invoke(this, _settings);
}
