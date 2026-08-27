// WinNotch.TrayApp/TrayIconManager.cs
// WHY: System tray provides the primary user interaction point.
// Users can:
// - Enable/disable each module (A-E)
// - Open settings window
// - View diagnostics (debug mode)
// - Temporarily hide the notch
// - Exit the application
//
// Using Hardcodet.NotifyIcon.Wpf for native NotifyIcon support.

using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using WinNotch.Common;

namespace WinNotch.TrayApp;

public sealed class TrayIconManager : IDisposable
{
    private readonly Hardcodet.Wpf.TaskbarNotification.TaskbarIcon _trayIcon;
    private ModuleSettings _settings;
    private bool _disposed;

    public event EventHandler<ModuleSettings>? SettingsChanged;
    public event EventHandler? SettingsRequested;

    public TrayIconManager(ModuleSettings settings)
    {
        _settings = settings;

        _trayIcon = new Hardcodet.Wpf.TaskbarNotification.TaskbarIcon
        {
            ToolTipText = $"{Constants.AppName} — Sağ tıkla → Ayarlar",
            Visibility = Visibility.Visible
        };

        _trayIcon.ContextMenu = CreateContextMenu();
        _trayIcon.DoubleClickCommand = new RelayCommand(OnSettingsClicked);
    }

    private ContextMenu CreateContextMenu()
    {
        var menu = new ContextMenu();

        var titleItem = new MenuItem
        {
            Header = $"✦ {Constants.AppName}",
            IsEnabled = false
        };
        menu.Items.Add(titleItem);
        menu.Items.Add(new Separator());

        var moduleMenu = new MenuItem { Header = "📦 Modüller" };
        moduleMenu.Items.Add(CreateModuleToggle("Module A — Sürükle & Bırak", nameof(ModuleSettings.ModuleA_DragDrop)));
        moduleMenu.Items.Add(CreateModuleToggle("Module B — Clipboard Dinleyici", nameof(ModuleSettings.ModuleB_Clipboard)));
        moduleMenu.Items.Add(CreateModuleToggle("Module C — Medya Oynatıcı", nameof(ModuleSettings.ModuleC_Media)));
        moduleMenu.Items.Add(CreateModuleToggle("Module D — Pencere Sabitleyici", nameof(ModuleSettings.ModuleD_WindowPin)));
        moduleMenu.Items.Add(CreateModuleToggle("Module E — Ekran Görüntüsü", nameof(ModuleSettings.ModuleE_Screenshot)));
        menu.Items.Add(moduleMenu);

        var monitorMenu = new MenuItem { Header = "🖥️ Monitör" };
        var screens = System.Windows.Forms.Screen.AllScreens;
        for (int i = 0; i < screens.Length; i++)
        {
            int index = i;
            var screen = screens[i];
            string label = screens.Length == 1
                ? "Varsayılan monitör"
                : $"Monitör {i + 1}: {screen.DeviceName} ({screen.Bounds.Width}x{screen.Bounds.Height})";

            var item = new MenuItem
            {
                Header = label,
                IsCheckable = true,
                IsChecked = i == _settings.TargetMonitorIndex
            };
            item.Click += (_, _) =>
            {
                _settings.TargetMonitorIndex = index;
                OnSettingsChanged();
            };
            monitorMenu.Items.Add(item);
        }
        menu.Items.Add(monitorMenu);

        menu.Items.Add(new Separator());

        var diagItem = new MenuItem
        {
            Header = "📊 Teşhis Paneli",
            IsCheckable = true,
            IsChecked = _settings.DiagnosticsEnabled
        };
        diagItem.Click += (_, _) =>
        {
            // WPF already toggles IsChecked before Click fires.
            _settings.DiagnosticsEnabled = diagItem.IsChecked;
            OnSettingsChanged();
        };
        menu.Items.Add(diagItem);

        var settingsItem = new MenuItem { Header = "⚙️ Ayarlar" };
        settingsItem.Click += (_, _) => OnSettingsClicked();
        menu.Items.Add(settingsItem);

        var startupItem = new MenuItem
        {
            Header = "🚀 Başlangıca Ekle",
            IsCheckable = true,
            IsChecked = _settings.AutoStart
        };
        startupItem.Click += (_, _) =>
        {
            // WPF already toggles IsChecked before Click fires.
            _settings.AutoStart = startupItem.IsChecked;
            ToggleAutoStart(_settings.AutoStart);
            OnSettingsChanged();
        };
        menu.Items.Add(startupItem);

        menu.Items.Add(new Separator());

        var exitItem = new MenuItem { Header = "✕ Çıkış" };
        exitItem.Click += (_, _) => Application.Current.Shutdown();
        menu.Items.Add(exitItem);

        return menu;
    }

    private MenuItem CreateModuleToggle(string header, string propertyName)
    {
        bool isEnabled = propertyName switch
        {
            nameof(ModuleSettings.ModuleA_DragDrop) => _settings.ModuleA_DragDrop,
            nameof(ModuleSettings.ModuleB_Clipboard) => _settings.ModuleB_Clipboard,
            nameof(ModuleSettings.ModuleC_Media) => _settings.ModuleC_Media,
            nameof(ModuleSettings.ModuleD_WindowPin) => _settings.ModuleD_WindowPin,
            nameof(ModuleSettings.ModuleE_Screenshot) => _settings.ModuleE_Screenshot,
            _ => true
        };

        var item = new MenuItem
        {
            Header = header,
            IsCheckable = true,
            IsChecked = isEnabled,
            Tag = propertyName
        };

        item.Click += (_, _) =>
        {
            // IsCheckable MenuItem toggles itself before the Click handler runs.
            // Persist that actual value instead of inverting it a second time.
            bool newValue = item.IsChecked;

            switch (propertyName)
            {
                case nameof(ModuleSettings.ModuleA_DragDrop):
                    _settings.ModuleA_DragDrop = newValue;
                    break;
                case nameof(ModuleSettings.ModuleB_Clipboard):
                    _settings.ModuleB_Clipboard = newValue;
                    break;
                case nameof(ModuleSettings.ModuleC_Media):
                    _settings.ModuleC_Media = newValue;
                    break;
                case nameof(ModuleSettings.ModuleD_WindowPin):
                    _settings.ModuleD_WindowPin = newValue;
                    break;
                case nameof(ModuleSettings.ModuleE_Screenshot):
                    _settings.ModuleE_Screenshot = newValue;
                    break;
            }

            OnSettingsChanged();
        };

        return item;
    }

    private static void ToggleAutoStart(bool enable)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                Constants.RegistryRunPath, true);

            if (key == null) return;

            if (enable)
            {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                key.SetValue(Constants.RegistryValueName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(Constants.RegistryValueName, false);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TrayIconManager] Error toggling auto-start: {ex.Message}");
        }
    }

    public void UpdateTooltip(string text)
    {
        _trayIcon.ToolTipText = text;
    }

    private void OnSettingsClicked()
    {
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnSettingsChanged()
    {
        SettingsChanged?.Invoke(this, _settings);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _trayIcon.Dispose();
        GC.SuppressFinalize(this);
    }

    ~TrayIconManager() => Dispose();
}

internal sealed class RelayCommand : System.Windows.Input.ICommand
{
    private readonly Action _execute;

    public RelayCommand(Action execute) => _execute = execute;

#pragma warning disable CS0067
    public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => _execute();
}
