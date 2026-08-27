// WinNotch.Core/Interop/DwmApi.cs
// WHY: DwmApi.dll provides DWM (Desktop Window Manager) composition.
// DwmExtendFrameIntoClientArea is KEY for our transparency approach:
// - We set background to opaque black (#FF000000)
// - Extend glass frame into client area (margin = -1 for full extension)
// - DWM handles the compositing — no WPF AllowsTransparency needed
// - Result: Sharp rounded-rect silhouette with zero GPU overhead
//
// PERFORMANCE NOTE: DWM calls are lightweight — just DWM bookkeeping.
// No per-frame cost. The GPU compositor handles blending natively.

using System.Runtime.InteropServices;

namespace WinNotch.Core.Interop;

/// <summary>
/// P/Invoke declarations for dwmapi.dll.
/// Provides DWM composition for native transparency without AllowsTransparency="True".
/// </summary>
internal static partial class DwmApi
{
    private const string DllName = "dwmapi.dll";

    // ═══════════════════════════════════════════════════════════════
    // DWM EXTEND FRAME (The magic transparency call)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// DWMWINDOWATTRIBUTE for DWMWA_NCRENDERING_POLICY.
    /// Controls non-client rendering — we disable it for clean appearance.
    /// </summary>
    public const int DWMWA_NCRENDERING_POLICY = 2;

    /// <summary>Non-client rendering policy: Never render.</summary>
    public const int DWMNCRP_DISABLED = 1;

    /// <summary>DWMWINDOWATTRIBUTE for disabling transitions.</summary>
    public const int DWMWA_TRANSITIONS_FORCEDISABLED = 3;

    /// <summary>Disable DWM live/thumbnail transitions.</summary>
    public const int DWMWA_TRANSITIONS_DISABLED = 1;

    [LibraryImport(DllName)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMargins);

    [LibraryImport(DllName)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DwmSetWindowAttribute(
        IntPtr hWnd,
        uint dwAttribute,
        ref int pvAttribute,
        uint cbAttribute);

    // DWM APIs return HRESULT (S_OK is zero), not a Win32 BOOL.
    [LibraryImport(DllName)]
    public static partial int DwmGetWindowAttribute(
        IntPtr hWnd,
        uint dwAttribute,
        out RECT pvAttribute,
        uint cbAttribute);

    /// <summary>DWMWA_EXTENDED_FRAME_BOUNDS attribute value.</summary>
    public const uint DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    // ═══════════════════════════════════════════════════════════════
    // STRUCTURES
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Margins for DwmExtendFrameIntoClientArea.
    /// Setting all to -1 extends glass into entire client area.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MARGINS
    {
        public int LeftWidth;
        public int RightWidth;
        public int TopHeight;
        public int BottomHeight;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    // ═══════════════════════════════════════════════════════════════
    // HELPER METHODS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Extends the glass frame into the entire client area.
    /// This creates the "frosted glass" effect where DWM handles compositing.
    /// Combined with opaque black background + SetWindowRgn, we get a clean pill shape.
    /// </summary>
    public static void ExtendGlassFrame(IntPtr hWnd)
    {
        // -1 for all margins = extend glass into entire client area
        var margins = new MARGINS
        {
            LeftWidth = -1,
            RightWidth = -1,
            TopHeight = -1,
            BottomHeight = -1
        };

        DwmExtendFrameIntoClientArea(hWnd, ref margins);

        // Disable non-client rendering for clean appearance
        int policy = DWMNCRP_DISABLED;
        DwmSetWindowAttribute(hWnd, DWMWA_NCRENDERING_POLICY, ref policy, sizeof(int));

        // Disable DWM transitions (prevents flicker during resize/animation)
        int transitionPolicy = DWMWA_TRANSITIONS_DISABLED;
        DwmSetWindowAttribute(hWnd, DWMWA_TRANSITIONS_FORCEDISABLED, ref transitionPolicy, sizeof(int));
    }
}
