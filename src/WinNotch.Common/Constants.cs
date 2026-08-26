// WinNotch.Common/Constants.cs
// Central constants for geometry, timing and lightweight resource budgets.

namespace WinNotch.Common;

public static class Constants
{
    public const double NotchIdleWidth = 100;
    public const double NotchIdleHeight = 22;
    public const double NotchCornerRadius = 11;

    public const double NotchHoverWidth = 118;
    public const double NotchHoverHeight = 28;

    public const double NotchClipboardWidth = 220;
    public const double NotchClipboardHeight = 36;

    public const double NotchScreenshotWidth = 260;
    public const double NotchScreenshotHeight = 40;

    public const double NotchDropTargetWidth = 280;
    public const double NotchDropTargetHeight = 60;

    public const double NotchDropResultWidth = 320;
    public const double NotchDropResultHeight = 72;

    // Persistent file shelf states. Compact by default, actions on hover.
    public const double NotchShelfWidth = 220;
    public const double NotchShelfHeight = 36;
    public const double NotchShelfExpandedWidth = 330;
    public const double NotchShelfExpandedHeight = 66;

    public const double NotchMediaAmbientWidth = 120;
    public const double NotchMediaAmbientHeight = 28;
    public const double NotchMediaExpandedWidth = 300;
    public const double NotchMediaExpandedHeight = 64;

    public const double NotchPinnedWidth = 130;
    public const double NotchPinnedHeight = 28;

    public const int HitTestPadding = 4;

    public const string BackgroundHex = "#FF000000";
    public const string AccentHex = "#FF0078D4";
    public const string LightThemeBorderHex = "#FF404040";
    public const string TextPrimaryHex = "#FFFFFFFF";
    public const string ClipboardFlashHex = "#FFFFC107";

    public const int ExpandDurationMs = 250;
    public const int ContractDelayMs = 400;
    public const int ContractDurationMs = 200;
    public const int ClipboardFlashDurationMs = 2000;
    public const int ScreenshotFlashDurationMs = 2500;
    public const int DropResultDisplayDurationMs = 900;
    public const int MediaAmbientFlashDurationMs = 4000;
    public const int MaxAttentionEventsPerMinute = 5;
    public const int MinNotificationIntervalMs = 3000;

    public const int MaxHistoryEntries = 5;
    public const int MaxShelfItems = 20;

    public const string MutexName = "WinNotch_SingleInstance_{8A3F2B1C-5D4E-6F7A-8B9C-0D1E2F3A4B5C}";
    public const string RegistryRunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string RegistryValueName = "WinNotch";
    public const string AppName = "WinNotch";
    public const int TemporaryHideDurationMs = 3_600_000;

    public const long MaxRamBytes = 15 * 1024 * 1024;
    public const double MaxCpuPercent = 0.5;
}
