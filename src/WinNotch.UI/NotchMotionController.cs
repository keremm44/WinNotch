using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using WinNotch.Common;

namespace WinNotch.UI;

/// <summary>
/// Finite, retargetable size motion for the notch.
/// The timer is stopped while idle, so there is no continuous animation loop.
/// Native HWND geometry is synchronized by MainWindow.OnRenderSizeChanged, which
/// keeps one DPI-aware region writer instead of a second legacy animation callback.
/// </summary>
internal sealed class NotchMotionController : IDisposable
{
    private readonly Window _window;
    private DispatcherTimer? _timer;

    private double _startWidth;
    private double _startHeight;
    private double _targetWidth;
    private double _targetHeight;
    private long _startTimestamp;
    private int _durationMs;
    private double _durationScale = 1.0;
    private bool _disposed;

    public NotchMotionController(Window window, Action syncNativeGeometry)
    {
        _window = window;
        // Kept in the signature for MainWindow binary/source compatibility while the
        // legacy callback path is retired. SizeChanged is now the sole runtime sync.
        _ = syncNativeGeometry;
    }

    public void Configure(AppearanceSettings settings)
        => _durationScale = AppearanceResolver.ResolveMotion(settings).ContainerDurationScale;

    public void Apply(double targetWidth, double targetHeight, bool immediate = false)
    {
        if (_disposed) return;

        // Windows accessibility preference has final authority over WinNotch settings.
        if (immediate || !SystemParameters.ClientAreaAnimation)
        {
            _timer?.Stop();
            _window.Width = targetWidth;
            _window.Height = targetHeight;
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
        int baseDuration = expanding ? Constants.ExpandDurationMs : Constants.ContractDurationMs;
        _durationMs = Math.Max(1, (int)Math.Round(baseDuration * _durationScale));
        _startTimestamp = Stopwatch.GetTimestamp();

        _timer ??= CreateTimer();
        if (!_timer.IsEnabled)
            _timer.Start();
    }

    private DispatcherTimer CreateTimer()
    {
        var timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        timer.Tick += OnTick;
        return timer;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        double elapsedMs = Stopwatch.GetElapsedTime(_startTimestamp).TotalMilliseconds;
        double progress = Math.Clamp(elapsedMs / _durationMs, 0.0, 1.0);

        double inv = 1.0 - progress;
        double eased = 1.0 - (inv * inv * inv);

        _window.Width = Lerp(_startWidth, _targetWidth, eased);
        _window.Height = Lerp(_startHeight, _targetHeight, eased);

        if (progress >= 1.0)
        {
            _timer?.Stop();
            _window.Width = _targetWidth;
            _window.Height = _targetHeight;
        }
    }

    private static double Lerp(double from, double to, double t)
        => from + ((to - from) * t);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_timer != null)
        {
            _timer.Stop();
            _timer.Tick -= OnTick;
            _timer = null;
        }
    }
}
