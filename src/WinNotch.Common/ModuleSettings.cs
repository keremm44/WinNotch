// WinNotch.Common/ModuleSettings.cs
// Tracks enabled/disabled state for each optional WinNotch capability.

namespace WinNotch.Common;

public sealed class ModuleSettings
{
    /// <summary>File Shelf / drag & drop transfer.</summary>
    public bool ModuleA_DragDrop { get; set; } = true;

    /// <summary>Event-driven clipboard companion.</summary>
    public bool ModuleB_Clipboard { get; set; } = true;

    /// <summary>
    /// SMTC media companion. Opt-in because initializing WinRT media management
    /// adds a measurable resident working-set cost even when no media is playing.
    /// </summary>
    public bool ModuleC_Media { get; set; } = false;

    /// <summary>Screenshot bridge.</summary>
    public bool ModuleE_Screenshot { get; set; } = true;

    public int TargetMonitorIndex { get; set; } = 0;
    public bool AutoStart { get; set; } = false;
    public bool DiagnosticsEnabled { get; set; } = false;

    /// <summary>Auto, AlwaysShow or Hidden.</summary>
    public string VisibilityMode { get; set; } = "Auto";

    /// <summary>Quiet, Balanced or Active.</summary>
    public string ReactionLevel { get; set; } = "Balanced";

    /// <summary>
    /// Controlled visual personalization. Kept as a nested object so older settings.json
    /// files that do not contain Appearance continue to deserialize with safe defaults.
    /// </summary>
    public AppearanceSettings Appearance { get; set; } = new();
}
