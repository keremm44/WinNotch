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
    // WINDOW GEOMETRY
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>Idle notch width in pixels. Small footprint on screen.</summary>
    public const double NotchIdleWidth = 130;
    
    /// <summary>Idle notch height in pixels.</summary>
    public const double NotchIdleHeight = 28;
    
    /// <summary>Corner radius for rounded-rect region clipping.</summary>
    public const double NotchCornerRadius = 14;
    
    /// <summary>Expanded width when hovering or dragging files.</summary>
    public const double NotchExpandedWidth = 400;
    
    /// <summary>Expanded height when hovering.</summary>
    public const double NotchExpandedHeight = 120;
    
    /// <summary>Media widget expanded width.</summary>
    public const double NotchMediaWidth = 350;
    
    /// <summary>Media widget expanded height.</summary>
    public const double NotchMediaHeight = 80;

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
    
    /// <summary>Clipboard flash animation duration.</summary>
    public const int ClipboardFlashDurationMs = 1500;

    /// <summary>How long to show drop result actions before auto-dismissing.</summary>
    public const int DropResultDisplayDurationMs = 3000;

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
