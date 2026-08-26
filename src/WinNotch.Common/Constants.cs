// WinNotch.Common/Constants.cs
// WHY: Centralized constants prevent magic numbers scattered across code.
// Every dimension, delay, and color lives here — one source of truth.

namespace WinNotch.Common;

/// <summary>
/// Central constants for the WinNotch application.
/// All magic numbers live here — dimensions, colors, delays, API strings.
/// </summary>
public static class Constants
{
    // ═══════════════════════════════════════════════════════════════
    // WINDOW GEOMETRY — Content-driven dimensions
    // WHY: Each state gets the minimum practical size.
    // Idle must be nearly invisible. Expanded only when useful.
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Idle notch width — minimal, nearly invisible.</summary>
    public const double NotchIdleWidth = 100;

    /// <summary>Idle notch height — minimal.</summary>
    public const double NotchIdleHeight = 22;

    /// <summary>Corner radius for rounded-rect region clipping.</summary>
    public const double NotchCornerRadius = 11;

    /// <summary>Hover state — slightly larger for visual feedback.</summary>
    public const double NotchHoverWidth = 118;
    public const double NotchHoverHeight = 28;

    /// <summary>Clipboard notification — compact, only for actionable content.</summary>
    public const double NotchClipboardWidth = 220;
    public const double NotchClipboardHeight = 36;

    /// <summary>Screenshot notification — slightly wider for actions.</summary>
    public const double NotchScreenshotWidth = 260;
    public const double NotchScreenshotHeight = 40;

    /// <summary>Drop target — focused on current item, no history.</summary>
    public const double NotchDropTargetWidth = 280;
    public const double NotchDropTargetHeight = 60;

    /// <summary>Drop result — compact actions row.</summary>
    public const double NotchDropResultWidth = 320;
    public const double NotchDropResultHeight = 72;

    /// <summary>Media ambient — tiny indicator when media playing.</summary>
    public const double NotchMediaAmbientWidth = 120;
    public const double NotchMediaAmbientHeight = 28;

    /// <summary>Media expanded — full controls on hover.</summary>
    public const double NotchMediaExpandedWidth = 300;
    public const double NotchMediaExpandedHeight = 64;

    /// <summary>Window pinned — small badge.</summary>
    public const double NotchPinnedWidth = 130;
    public const double NotchPinnedHeight = 28;

    /// <summary>Hit-test padding outside visible area.</summary>
    public const int HitTestPadding = 4;

    // ═══════════════════════════════════════════════════════════════
    // COLORS
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>Native black background. 100% opaque — NO Opacity property usage!</summary>
    public const string BackgroundHex = "#FF000000";
    
    /// <summary>Accent color for hover states and highlights.</summary>
    public const string AccentHex = "#FF0078D4";
    
    /// <summary>Light theme border color (for when AppsUseLightTheme = 1).</summary>
    public const string LightThemeBorderHex = "#FF404040";
    
    /// <summary>Text color for light-on-dark (idle state).</summary>
    public const string TextPrimaryHex = "#FFFFFFFF";
    
    /// <summary>Clipboard notification flash color.</summary>
    public const string ClipboardFlashHex = "#FFFFC107";

    // ═══════════════════════════════════════════════════════════════
    // ANIMATION DURATIONS (milliseconds)
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>Expansion animation duration.</summary>
    public const int ExpandDurationMs = 250;
    
    /// <summary>Contraction delay before shrinking (debounce).</summary>
    public const int ContractDelayMs = 400;
    
    /// <summary>Contraction animation duration.</summary>
    public const int ContractDurationMs = 200;
    
    /// <summary>Clipboard notification duration — short, only for actionable content.</summary>
    public const int ClipboardFlashDurationMs = 2000;

    /// <summary>Screenshot notification duration.</summary>
    public const int ScreenshotFlashDurationMs = 2500;

    /// <summary>How long to show drop result actions before auto-dismissing.</summary>
    public const int DropResultDisplayDurationMs = 3000;

    /// <summary>Media ambient display duration before collapsing (track change flash).</summary>
    public const int MediaAmbientFlashDurationMs = 4000;

    /// <summary>Attention budget: max visual interruptions per minute.</summary>
    public const int MaxAttentionEventsPerMinute = 5;

    /// <summary>Attention budget: minimum interval between visual notifications.</summary>
    public const int MinNotificationIntervalMs = 3000;

    // ═══════════════════════════════════════════════════════════════
    // HISTORY
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>Maximum drag-drop history entries kept in memory.</summary>
    public const int MaxHistoryEntries = 5;

    // ═══════════════════════════════════════════════════════════════
    // APPLICATION
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>Mutex name for single-instance enforcement.</summary>
    public const string MutexName = "WinNotch_SingleInstance_{8A3F2B1C-5D4E-6F7A-8B9C-0D1E2F3A4B5C}";
    
    /// <summary>Registry path for auto-start.</summary>
    public const string RegistryRunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    
    /// <summary>Registry value name for WinNotch.</summary>
    public const string RegistryValueName = "WinNotch";
    
    /// <summary>Application display name.</summary>
    public const string AppName = "WinNotch";
    
    /// <summary>Temporary hide duration (1 hour in milliseconds).</summary>
    public const int TemporaryHideDurationMs = 3_600_000;

    // ═══════════════════════════════════════════════════════════════
    // PERFORMANCE
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>Maximum allowed RAM in bytes (15 MB).</summary>
    public const long MaxRamBytes = 15 * 1024 * 1024;
    
    /// <summary>Maximum allowed CPU percentage (0.5%).</summary>
    public const double MaxCpuPercent = 0.5;
}
