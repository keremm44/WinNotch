// WinNotch.Common/ModuleSettings.cs
// Tracks enabled/disabled state for each module and is persisted to settings.
// Expensive optional integrations should not be loaded on a clean install unless
// their everyday value justifies the resident cost.

namespace WinNotch.Common;

public sealed class ModuleSettings
{
    /// <summary>Module A: File Shelf / drag & drop transfer.</summary>
    public bool ModuleA_DragDrop { get; set; } = true;

    /// <summary>Module B: Event-driven clipboard companion.</summary>
    public bool ModuleB_Clipboard { get; set; } = true;

    /// <summary>
    /// Module C: SMTC media companion.
    /// Opt-in by default because initializing WinRT media management adds a
    /// measurable resident working-set cost even when no media is playing.
    /// </summary>
    public bool ModuleC_Media { get; set; } = false;

    /// <summary>Module D: Window pinner.</summary>
    public bool ModuleD_WindowPin { get; set; } = true;

    /// <summary>Module E: Screenshot Bridge.</summary>
    public bool ModuleE_Screenshot { get; set; } = true;

    public int TargetMonitorIndex { get; set; } = 0;
    public bool AutoStart { get; set; } = false;
    public bool DiagnosticsEnabled { get; set; } = false;

    /// <summary>Auto, AlwaysShow or Hidden.</summary>
    public string VisibilityMode { get; set; } = "Auto";

    /// <summary>Quiet, Balanced or Active.</summary>
    public string ReactionLevel { get; set; } = "Balanced";
}
