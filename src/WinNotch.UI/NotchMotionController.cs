using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using WinNotch.Common;

namespace WinNotch.UI;

/// <summary>
/// Finite, retargetable size motion for the notch.
/// The timer is stopped while idle, so there is no continuous animation loop.
/// </summary>
internal sealed class NotchMotionController : IDisposable
{
    private readonly Window _window;
    private readonly Action _syncNativeGeometry;
    private readonly DispatcherTimer _timer;

    private double _startWidth;
    private double _startHeight;
    private double _targetWidth;
    private double _targetHeight;
    private long _startTimestamp;
    private int _durationMs;
    private bool _disposed;

    public NotchMotionController(Window window, Action syncNativeGeometry)
    {
        _window = window;
        _syncNativeGeometry = syncNativeGeometry;
        _timer = new DispatcherTimer(DispatcherPriority.Render, window.Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _timer.Tick += OnTick;
    }

    public void Apply(double targetWidth, double targetHeight, bool immediate = false)
    {
        if (_disposed) return;

        if (immediate)
        {
            _timer.Stop();
            _window.Width = targetWidth;
            _window.Height = targetHeight;
            _syncNativeGeometry();
            return;
        }

        double currentWidth = double.IsNaN(_window.Width) || _window.Width <= 0
            ? Math.Max(1, _window.ActualWidth)
            : _window.Width;
        double currentHeight = double.IsNaN(_window.Height) || _window.Height <= 0
            ? Math.Max(1, _window.ActualHeight)
            : _window.Height;

        _startWidth = currentWidth;
        _startHeight = currentHeight;
        _targetWidth = targetWidth;
        _targetHeight = targetHeight;

        bool expanding = targetWidth > currentWidth || targetHeight > currentHeight;
        _durationMs = expanding ? Constants.ExpandDurationMs : Constants.ContractDurationMs;
        _durationMs = Math.Max(1, _durationMs);
        _startTimestamp = Stopwatch.GetTimestamp();

        if (!_timer.IsEnabled)
            _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        double elapsedMs = Stopwatch.GetElapsedTime(_startTimestamp).TotalMilliseconds;
        double progress = Math.Clamp(elapsedMs / _durationMs, 0.0, 1.0);

        // Cubic ease-out: fast response, soft landing, no overshoot.
        double inv = 1.0 - progress;
        double eased = 1.0 - (inv * inv * inv);

        _window.Width = Lerp(_startWidth, _targetWidth, eased);
        _window.Height = Lerp(_startHeight, _targetHeight, eased);
        _syncNativeGeometry();

        if (progress >= 1.0)
        {
            _timer.Stop();
            _window.Width = _targetWidth;
            _window.Height = _targetHeight;
            _syncNativeGeometry();
        }
    }

    private static double Lerp(double from, double to, double t)
        => from + ((to - from) * t);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
    }
}
