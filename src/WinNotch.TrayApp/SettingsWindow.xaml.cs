// WinNotch.TrayApp/SettingsWindow.xaml.cs
// WHY: Provides a GUI for all settings that the tray menu offers.
// More user-friendly than the context menu for complex configurations.
// Also shows real-time performance stats (RAM/CPU) in debug mode.
//
// PERFORMANCE: This window only exists while open.
// Closing it releases all resources. No background activity.

using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using WinNotch.Common;

namespace WinNotch.TrayApp;

/// <summary>
/// Interaction logic for SettingsWindow.xaml.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly ModuleSettings _settings;
    private readonly System.Windows.Threading.DispatcherTimer _statsTimer;
    private bool _isLoadingSettings;

    /// <summary>
    /// Fired when settings are changed.
    /// </summary>
    public event EventHandler<ModuleSettings>? SettingsChanged;

    public SettingsWindow(ModuleSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        // Loading IsChecked/SelectedIndex raises WPF change events. Guard those
        // events so opening the settings window can never mutate persisted state.
        LoadSettings();

        // Setup performance stats timer (only updates while window is visible)
        _statsTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _statsTimer.Tick += StatsTimer_Tick;
        _statsTimer.Start();

        Closed += (_, _) => _statsTimer.Stop();
    }

    /// <summary>
    /// Loads current settings into the UI controls without writing them back.
    /// </summary>
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

            MonitorComboBox.Items.Clear();
            var screens = System.Windows.Forms.Screen.AllScreens;
            for (int i = 0; i < screens.Length; i++)
            {
                var screen = screens[i];
                string label = screens.Length == 1
                    ? "Varsayılan monitör"
                    : $"Monitör {i + 1}: {screen.DeviceName} ({screen.Bounds.Width}x{screen.Bounds.Height})";

                MonitorComboBox.Items.Add(new ComboBoxItem
                {
                    Content = label,
                    Tag = i
                });
            }

            if (MonitorComboBox.Items.Count > _settings.TargetMonitorIndex)
                MonitorComboBox.SelectedIndex = _settings.TargetMonitorIndex;
            else if (MonitorComboBox.Items.Count > 0)
                MonitorComboBox.SelectedIndex = 0;
        }
        finally
        {
            _isLoadingSettings = false;
        }
    }

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

    private void MonitorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingSettings) return;

        if (MonitorComboBox.SelectedIndex >= 0)
        {
            _settings.TargetMonitorIndex = MonitorComboBox.SelectedIndex;
            OnSettingsChanged();
        }
    }

    private void AutoStartCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingSettings) return;
        _settings.AutoStart = AutoStartCheckBox.IsChecked == true;
        OnSettingsChanged();
    }

    private void DiagnosticsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingSettings) return;
        _settings.DiagnosticsEnabled = DiagnosticsCheckBox.IsChecked == true;
        OnSettingsChanged();
    }

    /// <summary>
    /// Updates performance stats display every second.
    /// </summary>
    private void StatsTimer_Tick(object? sender, EventArgs e)
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            long ramBytes = process.WorkingSet64;
            double ramMB = ramBytes / (1024.0 * 1024.0);

            RamUsageText.Text = $"RAM: {ramMB:F1} MB / 15 MB";
            RamUsageText.Foreground = ramMB > 15
                ? System.Windows.Media.Brushes.Red
                : System.Windows.Media.Brushes.LightGreen;

            var cpuTime = process.TotalProcessorTime;
            var uptime = DateTime.Now - process.StartTime;
            double cpuPercent = uptime.TotalMilliseconds > 0
                ? (cpuTime.TotalMilliseconds / uptime.TotalMilliseconds) * 100.0
                : 0;

            CpuUsageText.Text = $"CPU (ortalama): {cpuPercent:F2}%";
            CpuUsageText.Foreground = cpuPercent > Constants.MaxCpuPercent
                ? System.Windows.Media.Brushes.Red
                : System.Windows.Media.Brushes.LightGreen;
        }
        catch
        {
            // Ignore errors in stats collection
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnSettingsChanged()
    {
        SettingsChanged?.Invoke(this, _settings);
    }
}
