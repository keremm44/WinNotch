// WinNotch.Common/ModuleSettings.cs
// WHY: Tracks which modules are enabled/disabled. Persisted to settings.
// Disabled modules consume ZERO resources — no event subscriptions at all.

namespace WinNotch.Common;

/// <summary>
/// Tracks enabled/disabled state for each module.
/// When a module is disabled, its service is never instantiated.
/// </summary>
public sealed class ModuleSettings
{
    /// <summary>Module A: Drag & Drop Path Extractor</summary>
    public bool ModuleA_DragDrop { get; set; } = true;

    /// <summary>Module B: Clipboard Sniffer</summary>
    public bool ModuleB_Clipboard { get; set; } = true;

    /// <summary>Module C: Media Companion (SMTC)</summary>
    public bool ModuleC_Media { get; set; } = true;

    /// <summary>Module D: Window Pinner</summary>
    public bool ModuleD_WindowPin { get; set; } = true;

    /// <summary>Module E: Screenshot Bridge</summary>
    public bool ModuleE_Screenshot { get; set; } = true;

    /// <summary>Target monitor index for notch positioning.</summary>
    public int TargetMonitorIndex { get; set; } = 0;

    /// <summary>Auto-start with Windows.</summary>
    public bool AutoStart { get; set; } = false;

    /// <summary>Enable diagnostics overlay in debug mode.</summary>
    public bool DiagnosticsEnabled { get; set; } = false;

    /// <summary>
    /// Visibility mode for fullscreen suppression.
    /// Auto = suppress in fullscreen apps (default).
    /// AlwaysShow = never suppress.
    /// Hidden = manually hidden until restored.
    /// </summary>
    public string VisibilityMode { get; set; } = "Auto";

    /// <summary>
    /// Reaction level: Quiet, Balanced, Active.
    /// Quiet = only direct interactions and screenshots.
    /// Balanced = interactions + useful clipboard types (default).
    /// Active = more clipboard/media feedback.
    /// </summary>
    public string ReactionLevel { get; set; } = "Balanced";
}
