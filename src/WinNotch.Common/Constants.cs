// WinNotch.Common/Constants.cs
// Central constants for geometry, timing and lightweight runtime behavior.

namespace WinNotch.Common;

public static class Constants
{
    public const double NotchIdleWidth = 100;
    public const double NotchIdleHeight = 22;
    public const double NotchCornerRadius = 11;

    public const double NotchHoverWidth = 118;
    public const double NotchHoverHeight = 28;

    public const double NotchClipboardWidth = 260;
    public const double NotchClipboardHeight = 40;

    public const double NotchScreenshotWidth = 310;
    public const double NotchScreenshotHeight = 56;

    public const double NotchDropTargetWidth = 290;
    public const double NotchDropTargetHeight = 62;

    public const double NotchDropResultWidth = 340;
    public const double NotchDropResultHeight = 70;

    public const double NotchShelfWidth = 230;
    public const double NotchShelfHeight = 40;
    public const double NotchShelfExpandedWidth = 340;
    public const double NotchShelfExpandedHeight = 70;

    public const double NotchMediaAmbientWidth = 124;
    public const double NotchMediaAmbientHeight = 28;
    public const double NotchMediaExpandedWidth = 336;
    public const double NotchMediaExpandedHeight = 64;

    public const double NotchPinnedWidth = 150;
    public const double NotchPinnedHeight = 30;

    public const int HitTestPadding = 4;

    public const string BackgroundHex = "#FF0B0B0D";
    public const string AccentHex = "#FF2D7DFF";
    public const string LightThemeBorderHex = "#364A4A50";
    public const string TextPrimaryHex = "#FFFFFFFF";

    public const int ExpandDurationMs = 220;
    public const int ContractDelayMs = 350;
    public const int ContractDurationMs = 180;
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
}
