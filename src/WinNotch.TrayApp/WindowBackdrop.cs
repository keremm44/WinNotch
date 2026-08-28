using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace WinNotch.TrayApp;

/// <summary>
/// Applies Windows 11's compositor-backed Mica material to the long-lived settings
/// window only. Unsupported systems retain the fully opaque XAML fallback.
/// </summary>
internal static class WindowBackdrop
{
    private const uint DwmwaUseImmersiveDarkMode = 20;
    private const uint DwmwaWindowCornerPreference = 33;
    private const uint DwmwaSystemBackdropType = 38;
    private const int DwmwcpRound = 2;
    private const int DwmSbtMainWindow = 2;

    // Classic DllImport is sufficient for this blittable signature and avoids
    // requiring unsafe compilation solely for a source-generated P/Invoke stub.
    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        uint attribute,
        ref int value,
        uint valueSize);

    public static bool TryApply(Window window, bool darkTheme)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            return false;

        try
        {
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
                return false;

            if (SystemParameters.HighContrast)
            {
                int noBackdrop = 1;
                _ = DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref noBackdrop, sizeof(int));
                return false;
            }

            int corners = DwmwcpRound;
            _ = DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref corners, sizeof(int));

            int dark = darkTheme ? 1 : 0;
            _ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int));

            int backdrop = DwmSbtMainWindow;
            int result = DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref backdrop, sizeof(int));
            if (result < 0)
                return false;

            if (HwndSource.FromHwnd(hwnd)?.CompositionTarget is { } target)
                target.BackgroundColor = Colors.Transparent;
            window.Background = Brushes.Transparent;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
