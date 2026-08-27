// WinNotch.TrayApp/App.xaml.cs
// WHY: This is the application entry point.
// Key responsibilities:
// 1. Single Instance Enforcement — Mutex prevents duplicate instances
// 2. Service Lifecycle — Initialize and dispose all services on exit
// 3. Tray Icon Management — Create and manage the system tray
// 4. Error Recovery — try-catch with tray restart capability
// 5. Startup Registry — Manage auto-start with Windows
//
// CRITICAL: OnExit MUST clean up all Win32 hooks, unpin all windows,
// and dispose all services. Failure to do so leaves orphaned topmost
// windows and clipboard listeners.
//
// PERFORMANCE: Workstation GC + Concurrent mode (configured in runtimeconfig.json).
// GC.Collect() is NEVER called manually — .NET's GC is smart enough.

using System.Diagnostics;
using System.Threading;
using System.Windows;
using WinNotch.Common;
using WinNotch.Core.Services;
using WinNotch.UI;

namespace WinNotch.TrayApp;

/// <summary>
/// WinNotch application entry point.
/// Handles single instance, lifecycle, and service coordination.
/// </summary>
public partial class App : Application
{
    private Mutex? _mutex;
    private TrayIconManager? _trayIcon;
    private MainWindow? _mainWindow;
    private SettingsWindow? _settingsWindow;
    private ModuleSettings _settings = null!;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        try
        {
            Debug.WriteLine("[WinNotch] OnStartup starting...");

            _settings = SettingsStore.Load();
            Debug.WriteLine($"[WinNotch] Settings loaded. Clipboard={_settings.ModuleB_Clipboard}, Media={_settings.ModuleC_Media}");

            _mutex = new Mutex(true, Constants.MutexName, out bool createdNew);

            if (!createdNew)
            {
                Debug.WriteLine("[WinNotch] Another instance running, exiting.");
                Shutdown();
                return;
            }

            Debug.WriteLine("[WinNotch] Mutex acquired.");

            Debug.WriteLine("[WinNotch] Creating MainWindow...");
            _mainWindow = new MainWindow();
            _mainWindow.SetSettings(_settings);
            _mainWindow.SettingsRequested += OnSettingsRequested;
            Debug.WriteLine("[WinNotch] Showing MainWindow...");
            _mainWindow.Show();
            Debug.WriteLine("[WinNotch] MainWindow shown.");

            Debug.WriteLine("[WinNotch] Creating TrayIcon...");
            _trayIcon = new TrayIconManager(_settings);
            _trayIcon.SettingsChanged += OnTraySettingsChanged;
            _trayIcon.SettingsRequested += OnSettingsRequested;
            Debug.WriteLine("[WinNotch] TrayIcon created.");

            Debug.WriteLine("[WinNotch] Startup complete!");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WinNotch] FATAL STARTUP ERROR: {ex}");
            try
            {
                string logPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    Constants.AppName, "crash.log");
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)!);
                System.IO.File.AppendAllText(logPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] STARTUP ERROR: {ex}\n\n");
            }
            catch { }
            Shutdown();
        }
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        SettingsStore.Save(_settings);

        if (_trayIcon != null)
        {
            _trayIcon.SettingsChanged -= OnTraySettingsChanged;
            _trayIcon.SettingsRequested -= OnSettingsRequested;
            _trayIcon.Dispose();
            _trayIcon = null;
        }

        if (_settingsWindow != null)
        {
            _settingsWindow.SettingsChanged -= OnSettingsWindowChanged;
            _settingsWindow.Close();
            _settingsWindow = null;
        }

        if (_mainWindow != null)
        {
            _mainWindow.SettingsRequested -= OnSettingsRequested;
            _mainWindow.Close();
            _mainWindow = null;
        }

        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        _mutex = null;
    }

    private void OnTraySettingsChanged(object? sender, ModuleSettings settings)
    {
        ApplySettings(settings);
    }

    private void OnSettingsWindowChanged(object? sender, ModuleSettings settings)
    {
        ApplySettings(settings);
    }

    private void ApplySettings(ModuleSettings settings)
    {
        _settings = settings;
        SettingsStore.Save(_settings);
        _mainWindow?.OnSettingsChanged();
    }

    private void OnSettingsRequested(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(OpenSettingsWindow);
    }

    private void OpenSettingsWindow()
    {
        if (_settingsWindow != null)
        {
            if (_settingsWindow.WindowState == WindowState.Minimized)
                _settingsWindow.WindowState = WindowState.Normal;

            _settingsWindow.Show();
            _settingsWindow.Activate();
            _settingsWindow.Topmost = true;
            _settingsWindow.Topmost = false;
            _settingsWindow.Focus();
            return;
        }

        _settingsWindow = new SettingsWindow(_settings);
        _settingsWindow.SettingsChanged += OnSettingsWindowChanged;
        _settingsWindow.Closed += OnSettingsWindowClosed;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void OnSettingsWindowClosed(object? sender, EventArgs e)
    {
        if (_settingsWindow == null) return;

        _settingsWindow.SettingsChanged -= OnSettingsWindowChanged;
        _settingsWindow.Closed -= OnSettingsWindowClosed;
        _settingsWindow = null;
    }

    private void OnDispatcherUnhandledException(object sender,
        System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        Debug.WriteLine($"[WinNotch] Unhandled exception: {e.Exception}");

        try
        {
            string logPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Constants.AppName,
                "crash.log");

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)!);
            System.IO.File.AppendAllText(logPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {e.Exception}\n\n");
        }
        catch
        {
        }

        e.Handled = true;
    }

    private void RestartApplication()
    {
        try
        {
            Process.Start(Process.GetCurrentProcess().MainModule?.FileName ?? "");
            Shutdown();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WinNotch] Failed to restart: {ex.Message}");
        }
    }
}
