// WinNotch.TrayApp/SettingsWindow.xaml.cs

using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WinNotch.Common;

namespace WinNotch.TrayApp;

public partial class SettingsWindow : Window
{
    private readonly ModuleSettings _settings;
    private readonly System.Windows.Threading.DispatcherTimer _statsTimer;
    private bool _isLoadingSettings;
    private TimeSpan _lastCpuTime;
    private DateTime _lastCpuSampleAt;

    public event EventHandler<ModuleSettings>? SettingsChanged;

    public SettingsWindow(ModuleSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        _statsTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _statsTimer.Tick += StatsTimer_Tick;

        LoadSettings();
        UpdateDiagnosticsState();

        Closed += (_, _) =>
        {
            _statsTimer.Stop();
            _statsTimer.Tick -= StatsTimer_Tick;
        };
    }

    private void LoadSettings()
    {
        _isLoadingSettings = true;
        try
        {
            ModuleACheckBox.IsChecked = _settings.ModuleA_DragDrop;
            ModuleBCheckBox.IsChecked = _settings.ModuleB_Clipboard;
            ModuleCCheckBox.IsChecked = _settings.ModuleC_Media;
            ModuleDCheckBox.IsChecked = _settings.ModuleD_WindowPin;
            ModuleECheckBox.IsChecked = _settings.ModuleE_Screenshot;
            AutoStartCheckBox.IsChecked = _settings.AutoStart;
            DiagnosticsCheckBox.IsChecked = _settings.DiagnosticsEnabled;

            SelectTaggedItem(VisibilityModeComboBox, _settings.VisibilityMode, "Auto");
            SelectTaggedItem(ReactionLevelComboBox, _settings.ReactionLevel, "Balanced");

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

    private static void SelectTaggedItem(ComboBox comboBox, string? value, string fallback)
    {
        string target = string.IsNullOrWhiteSpace(value) ? fallback : value;
        for (int i = 0; i < comboBox.Items.Count; i++)
        {
            if (comboBox.Items[i] is ComboBoxItem item &&
                string.Equals(item.Tag?.ToString(), target, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedIndex = i;
                return;
            }
        }

        comboBox.SelectedIndex = 0;
    }

    private static string SelectedTag(ComboBox comboBox, string fallback)
        => (comboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? fallback;

    private void ModuleCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingSettings) return;

        _settings.ModuleA_DragDrop = ModuleACheckBox.IsChecked == true;
        _settings.ModuleB_Clipboard = ModuleBCheckBox.IsChecked == true;
        _settings.ModuleC_Media = ModuleCCheckBox.IsChecked == true;
        _settings.ModuleD_WindowPin = ModuleDCheckBox.IsChecked == true;
        _settings.ModuleE_Screenshot = ModuleECheckBox.IsChecked == true;
        OnSettingsChanged();
    }

    private void VisibilityModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingSettings) return;
        _settings.VisibilityMode = SelectedTag(VisibilityModeComboBox, "Auto");
        OnSettingsChanged();
    }

    private void ReactionLevelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingSettings) return;
        _settings.ReactionLevel = SelectedTag(ReactionLevelComboBox, "Balanced");
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
            _statsTimer.Start();
        }
        else
        {
            _statsTimer.Stop();
            PerformancePanel.Visibility = Visibility.Collapsed;
        }
    }

    private void StatsTimer_Tick(object? sender, EventArgs e)
    {
        if (!_settings.DiagnosticsEnabled) return;

        try
        {
            using var process = Process.GetCurrentProcess();
            double ramMB = process.WorkingSet64 / (1024.0 * 1024.0);

            DateTime now = DateTime.UtcNow;
            TimeSpan cpuNow = process.TotalProcessorTime;
            double elapsedMs = Math.Max(1, (now - _lastCpuSampleAt).TotalMilliseconds);
            double cpuDeltaMs = Math.Max(0, (cpuNow - _lastCpuTime).TotalMilliseconds);
            double cpuPercent = cpuDeltaMs / (elapsedMs * Environment.ProcessorCount) * 100.0;

            _lastCpuTime = cpuNow;
            _lastCpuSampleAt = now;

            RamUsageText.Text = $"{ramMB:F1} MB";
            CpuUsageText.Text = $"{cpuPercent:F2}%";
            RamUsageText.Foreground = System.Windows.Media.Brushes.WhiteSmoke;
            CpuUsageText.Foreground = cpuPercent <= 1.0
                ? System.Windows.Media.Brushes.LightGreen
                : System.Windows.Media.Brushes.WhiteSmoke;
        }
        catch
        {
            RamUsageText.Text = "—";
            CpuUsageText.Text = "—";
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
