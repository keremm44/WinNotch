// WinNotch.TrayApp/TrayIconManager.cs

using System.Diagnostics;
using System.Reflection;
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
        string version = ResolveProductVersion();

        _trayIcon = new Hardcodet.Wpf.TaskbarNotification.TaskbarIcon
        {
            ToolTipText = $"{Constants.AppName} {version} — sağ tıkla ayarları aç",
            Visibility = Visibility.Visible
        };

        _trayIcon.ContextMenu = CreateContextMenu(version);
        _trayIcon.DoubleClickCommand = new RelayCommand(OnSettingsClicked);
    }

    private ContextMenu CreateContextMenu(string version)
    {
        var menu = new ContextMenu();

        menu.Items.Add(new MenuItem
        {
            Header = $"{Constants.AppName} {version}",
            IsEnabled = false
        });
        menu.Items.Add(new Separator());

        var moduleMenu = new MenuItem { Header = "Özellikler" };
        moduleMenu.Items.Add(CreateModuleToggle("Dosya Rafı", nameof(ModuleSettings.ModuleA_DragDrop)));
        moduleMenu.Items.Add(CreateModuleToggle("Akıllı Pano", nameof(ModuleSettings.ModuleB_Clipboard)));
        moduleMenu.Items.Add(CreateModuleToggle("Medya Kontrolleri", nameof(ModuleSettings.ModuleC_Media)));
        moduleMenu.Items.Add(CreateModuleToggle("Ekran Görüntüleri", nameof(ModuleSettings.ModuleE_Screenshot)));
        menu.Items.Add(moduleMenu);

        var monitorMenu = new MenuItem { Header = "Monitör" };
        var screens = System.Windows.Forms.Screen.AllScreens;
        for (int i = 0; i < screens.Length; i++)
        {
            int index = i;
            var screen = screens[i];
            string label = screens.Length == 1
                ? $"Varsayılan · {screen.Bounds.Width}×{screen.Bounds.Height}"
                : $"Monitör {i + 1} · {screen.Bounds.Width}×{screen.Bounds.Height}";

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
            Header = "Teşhis paneli",
            IsCheckable = true,
            IsChecked = _settings.DiagnosticsEnabled
        };
        diagItem.Click += (_, _) =>
        {
            _settings.DiagnosticsEnabled = diagItem.IsChecked;
            OnSettingsChanged();
        };
        menu.Items.Add(diagItem);

        var settingsItem = new MenuItem { Header = "Ayarlar" };
        settingsItem.Click += (_, _) => OnSettingsClicked();
        menu.Items.Add(settingsItem);

        var startupItem = new MenuItem
        {
            Header = "Windows ile başlat",
            IsCheckable = true,
            IsChecked = _settings.AutoStart
        };
        startupItem.Click += (_, _) =>
        {
            _settings.AutoStart = startupItem.IsChecked;
            SetAutoStart(_settings.AutoStart);
            OnSettingsChanged();
        };
        menu.Items.Add(startupItem);

        menu.Items.Add(new Separator());

        var exitItem = new MenuItem { Header = "Çıkış" };
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
                case nameof(ModuleSettings.ModuleE_Screenshot):
                    _settings.ModuleE_Screenshot = newValue;
                    break;
            }

            OnSettingsChanged();
        };

        return item;
    }

    internal static void SetAutoStart(bool enable)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                Constants.RegistryRunPath, true);

            if (key == null) return;

            if (enable)
            {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(exePath))
                    key.SetValue(Constants.RegistryValueName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(Constants.RegistryValueName, false);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TrayIconManager] Auto-start update failed: {ex.Message}");
        }
    }

    private static string ResolveProductVersion()
    {
        Assembly assembly = typeof(TrayIconManager).Assembly;
        string? informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            int metadata = informational.IndexOf('+');
            return metadata > 0 ? informational[..metadata] : informational;
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    public void UpdateTooltip(string text) => _trayIcon.ToolTipText = text;

    private void OnSettingsClicked() => SettingsRequested?.Invoke(this, EventArgs.Empty);

    private void OnSettingsChanged() => SettingsChanged?.Invoke(this, _settings);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // TaskbarIcon is a managed WPF object and must only be disposed by the UI
        // lifecycle. A finalizer would run this code on the finalizer thread.
        _trayIcon.Dispose();
        GC.SuppressFinalize(this);
    }
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
