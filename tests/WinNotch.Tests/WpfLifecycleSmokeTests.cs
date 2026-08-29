using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using WinNotch.Common;
using WinNotch.TrayApp;
using WinNotch.UI;
using WinNotch.UI.Views;
using Xunit;

namespace WinNotch.Tests;

public class WpfLifecycleSmokeTests
{
    [Fact]
    public void CoreWindowsAndViews_ConstructLoadAndClose_OnSta()
    {
        Exception? failure = null;
        using var completed = new ManualResetEventSlim(false);

        var thread = new Thread(() =>
        {
            Application? app = null;
            try
            {
                app = new Application
                {
                    ShutdownMode = ShutdownMode.OnExplicitShutdown
                };
                app.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri(
                        "pack://application:,,,/WinNotch.UI;component/Themes/WinNotchTheme.xaml",
                        UriKind.Absolute)
                });

                var appearance = new AppearanceSettings();
                AppearanceThemeManager.Apply(app.Resources, appearance);

                var settings = new ModuleSettings
                {
                    ModuleA_DragDrop = false,
                    ModuleB_Clipboard = false,
                    ModuleC_Media = false,
                    ModuleE_Screenshot = false,
                    VisibilityMode = "AlwaysShow",
                    Appearance = appearance
                };

                // Repeated construction catches stale event/resource assumptions that
                // pure Common/Core unit tests cannot see.
                for (int i = 0; i < 8; i++)
                {
                    var main = new MainWindow
                    {
                        ShowActivated = false
                    };
                    main.SetSettings(settings);
                    main.ApplyAppearanceSettings();
                    main.Show();
                    main.UpdateLayout();
                    main.Close();

                    var settingsWindow = new SettingsWindow(settings)
                    {
                        ShowActivated = false
                    };
                    settingsWindow.Show();
                    settingsWindow.UpdateLayout();
                    settingsWindow.Close();
                }

                // Child surfaces are lazy in production. Construct each one directly so
                // StaticResource/DynamicResource and XAML template regressions fail CI.
                _ = new CommandHubView();
                _ = new DropZoneView();
                _ = new ClipboardToastView();
                _ = new MediaWidgetView();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                try { app?.Shutdown(); } catch { }
                completed.Set();
            }
        })
        {
            IsBackground = true,
            Name = "WinNotch.WpfLifecycleSmoke"
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(completed.Wait(TimeSpan.FromSeconds(30)),
            "WPF lifecycle smoke did not complete within 30 seconds.");
        thread.Join(TimeSpan.FromSeconds(2));

        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
