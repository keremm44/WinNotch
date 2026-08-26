// WinNotch.Core/Services/PowerMonitorService.cs
// WHY: Monitors battery/power status to implement adaptive power mode.
// When on battery with Battery Saver active, we disable all animations
// and keep the notch as a static frame. This is "truly resource-friendly."
//
// Uses SystemParametersInfo for battery status — no polling required.
// We subscribe to PowerSettingRegisterNotification for event-driven updates.
//
// PERFORMANCE NOTE: On desktop (no battery), this service does nothing.
// On laptops, it only activates on power state changes.

using System.Runtime.InteropServices;

namespace WinNotch.Core.Services;

/// <summary>
/// Power status information.
/// </summary>
public enum PowerMode
{
    /// <summary>AC power, animations allowed.</summary>
    HighPerformance,

    /// <summary>Battery, Battery Saver active — disable animations.</summary>
    BatterySaver,

    /// <summary>Battery, but Battery Saver not active — animations OK.</summary>
    BatteryNormal
}

/// <summary>
/// Event args for power mode changes.
/// </summary>
public sealed class PowerModeChangedEventArgs : EventArgs
{
    public PowerMode PreviousMode { get; init; }
    public PowerMode CurrentMode { get; init; }
}

/// <summary>
/// Monitors power status and provides adaptive power mode information.
/// </summary>
public sealed partial class PowerMonitorService : IDisposable
{
    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;       // 0=Offline, 1=Online, 255=Unknown
        public byte BatteryFlag;        // Bit flags: 1=High, 2=Low, 4=Critical, 8=Charging, 127=NoBattery, 255=Unknown
        public byte BatteryLifePercent; // 0-100, 255=Unknown
        public byte Reserved1;
        public int BatteryLifeTime;     // Seconds remaining, -1=Unknown
        public int BatteryFullLifeTime; // Seconds to full charge, -1=Unknown
    }

    private PowerMode _currentMode = PowerMode.HighPerformance;
    private bool _disposed;

    /// <summary>
    /// Fired when power mode changes (AC ↔ Battery, Battery Saver toggle).
    /// </summary>
    public event EventHandler<PowerModeChangedEventArgs>? PowerModeChanged;

    /// <summary>
    /// Gets the current power mode.
    /// </summary>
    public PowerMode CurrentMode => _currentMode;

    /// <summary>
    /// Gets whether animations should be disabled (BatterySaver mode).
    /// </summary>
    public bool ShouldDisableAnimations => _currentMode == PowerMode.BatterySaver;

    /// <summary>
    /// Initializes power monitoring by reading current status.
    /// </summary>
    public void Initialize()
    {
        UpdatePowerStatus();
    }

    /// <summary>
    /// Checks current power status and updates mode if changed.
    /// Call this when you receive a power state change notification.
    /// </summary>
    public void UpdatePowerStatus()
    {
        if (_disposed) return;

        var previousMode = _currentMode;

        try
        {
            if (GetSystemPowerStatus(out var status))
            {
                bool isOnBattery = status.ACLineStatus == 0;
                bool isBatterySaver = (status.BatteryFlag & 0x10) != 0; // Bit 4 = Battery Saver active

                _currentMode = (!isOnBattery) ? PowerMode.HighPerformance
                             : (isBatterySaver) ? PowerMode.BatterySaver
                             : PowerMode.BatteryNormal;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[PowerMonitorService] Error reading power status: {ex.Message}");
            // Default to high performance if we can't read status
            _currentMode = PowerMode.HighPerformance;
        }

        if (_currentMode != previousMode)
        {
            PowerModeChanged?.Invoke(this, new PowerModeChangedEventArgs
            {
                PreviousMode = previousMode,
                CurrentMode = _currentMode
            });
        }
    }

    /// <summary>
    /// Checks if the system is currently running on battery.
    /// </summary>
    public bool IsOnBattery()
    {
        try
        {
            if (GetSystemPowerStatus(out var status))
            {
                return status.ACLineStatus == 0;
            }
        }
        catch { }
        return false; // Assume AC power if unknown
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}
