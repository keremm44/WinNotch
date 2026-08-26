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
    private ModuleSettings _settings = null!; // Loaded from disk in OnStartup

    /// <summary>
    /// Application startup — single instance check and initialization.
    /// </summary>
    private void OnStartup(object sender, StartupEventArgs e)
    {
        try
        {
            Debug.WriteLine("[WinNotch] OnStartup starting...");

            // ═══════════════════════════════════════════════════════════
            // STEP 0: Load settings from disk
            // WHY: Must load BEFORE creating MainWindow so module flags
            // are available for service initialization.
            // ═══════════════════════════════════════════════════════════
            _settings = SettingsStore.Load();
            Debug.WriteLine($"[WinNotch] Settings loaded. Clipboard={_settings.ModuleB_Clipboard}, Media={_settings.ModuleC_Media}");

            // ═══════════════════════════════════════════════════════════
            // STEP 1: Single Instance Enforcement
            // ═══════════════════════════════════════════════════════════
            _mutex = new Mutex(true, Constants.MutexName, out bool createdNew);

            if (!createdNew)
            {
                Debug.WriteLine("[WinNotch] Another instance running, exiting.");
                Shutdown();
                return;
            }

            Debug.WriteLine("[WinNotch] Mutex acquired.");

            // ═══════════════════════════════════════════════════════════
            // STEP 2: Create Main Window (Notch Widget)
            // ═══════════════════════════════════════════════════════════
            Debug.WriteLine("[WinNotch] Creating MainWindow...");
            _mainWindow = new MainWindow();
            _mainWindow.SetSettings(_settings);
            Debug.WriteLine("[WinNotch] Showing MainWindow...");
            _mainWindow.Show();
            Debug.WriteLine("[WinNotch] MainWindow shown.");

            // ═══════════════════════════════════════════════════════════
            // STEP 3: Create Tray Icon
            // ═══════════════════════════════════════════════════════════
            Debug.WriteLine("[WinNotch] Creating TrayIcon...");
            _trayIcon = new TrayIconManager(_settings);
            _trayIcon.SettingsChanged += OnTraySettingsChanged;
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

    /// <summary>
    /// Application exit — clean up ALL resources.
    /// WHY: MUST unpin all windows, remove all hooks, dispose all services.
    /// This is the last chance to clean up before process termination.
    /// </summary>
    private void OnExit(object sender, ExitEventArgs e)
    {
        // Save settings one final time
        SettingsStore.Save(_settings);

        // Dispose tray icon
        _trayIcon?.Dispose();

        // Close settings window if open
        _settingsWindow?.Close();
        _settingsWindow = null;

        // Close main window (triggers MainWindow_Closing which disposes services)
        _mainWindow?.Close();
        _mainWindow = null;

        // Release mutex
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        _mutex = null;

        // NOTE: We do NOT call GC.Collect() here.
        // .NET's Workstation GC + Concurrent mode handles cleanup efficiently.
        // Manual GC.Collect() would cause a noticeable pause.
    }

    /// <summary>
    /// Handles settings changes from the tray icon.
    /// </summary>
    private void OnTraySettingsChanged(object? sender, ModuleSettings settings)
    {
        _settings = settings;

        // Persist to disk immediately
        SettingsStore.Save(_settings);

        // Update main window settings
        if (_mainWindow != null)
        {
            // Reposition if monitor changed
            _mainWindow.PositionOnTargetMonitor();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // GLOBAL ERROR HANDLING
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Handles unhandled exceptions.
    /// WHY: Prevents the app from crashing silently.
    /// Shows a tray notification and attempts to continue.
    /// </summary>
    private void OnDispatcherUnhandledException(object sender,
        System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        Debug.WriteLine($"[WinNotch] Unhandled exception: {e.Exception}");

        // Log the error
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
            // If logging fails, just continue
        }

        // Mark as handled — don't crash the app
        e.Handled = true;

        // DON'T restart automatically — causes infinite loop
        // Just log and continue. User can restart from tray.
    }

    /// <summary>
    /// Attempts to restart the application after a crash.
    /// WHY: Power users expect resilience. If the notch crashes,
    /// automatically restart it rather than requiring manual intervention.
    /// </summary>
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
