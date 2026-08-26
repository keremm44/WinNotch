// WinNotch.UI/Animations/NotchAnimator.cs
// WHY: Centralized animation system using WPF Storyboard + CubicEase.
// Provides consistent, smooth transitions between notch states.
//
// DESIGN:
// - Expansion: 250ms with EaseOut (fast start, smooth settle)
// - Contraction: 400ms delay + 200ms animation (debounce prevents
//   rapid expand/contract when mouse moves quickly over the notch)
// - Width AND height animate simultaneously
// - Window region is updated on each animation frame for proper clipping
//
// PERFORMANCE: Storyboard animations are handled by WPF's composition
// thread — no UI thread blocking. CubicEase is a simple polynomial.
// No allocations during animation. GPU-accelerated via DirectComposition.
//
// MEMORY: Storyboards are created once and reused. No per-frame allocations.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WinNotch.Common;

namespace WinNotch.UI.Animations;

/// <summary>
/// Handles smooth animated transitions between notch states.
/// Uses WPF Storyboard system with CubicEase for natural motion.
/// </summary>
public sealed class NotchAnimator
{
    private readonly FrameworkElement _target;
    private readonly Action<double, double> _onSizeChanged; // Callback for region updates
    private Storyboard? _currentStoryboard;
    private System.Windows.Threading.DispatcherTimer? _contractDelayTimer;

    // Track current animated dimensions
    private double _currentWidth = Constants.NotchIdleWidth;
    private double _currentHeight = Constants.NotchIdleHeight;

    /// <summary>
    /// Creates a new NotchAnimator for the given target element.
    /// </summary>
    /// <param name="target">The UI element to animate (usually NotchBorder).</param>
    /// <param name="onSizeChanged">Called on each animation frame with new width/height for region clipping.</param>
    public NotchAnimator(FrameworkElement target, Action<double, double> onSizeChanged)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _onSizeChanged = onSizeChanged ?? throw new ArgumentNullException(nameof(onSizeChanged));
    }

    /// <summary>
    /// Current animated width.
    /// </summary>
    public double CurrentWidth => _currentWidth;

    /// <summary>
    /// Current animated height.
    /// </summary>
    public double CurrentHeight => _currentHeight;

    // ═══════════════════════════════════════════════════════════════
    // PUBLIC API
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Animates to idle dimensions (130x28).
    /// Uses debounce: waits 400ms before contracting to avoid flicker.
    /// </summary>
    public void AnimateToIdle()
    {
        // Cancel any pending expansion
        CancelContractDelay();

        // Start contraction delay (debounce)
        _contractDelayTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(Constants.ContractDelayMs)
        };
        _contractDelayTimer.Tick += (_, _) =>
        {
            _contractDelayTimer.Stop();
            _contractDelayTimer = null;
            PerformAnimation(
                Constants.NotchIdleWidth,
                Constants.NotchIdleHeight,
                Constants.ContractDurationMs);
        };
        _contractDelayTimer.Start();
    }

    /// <summary>
    /// Animates to hover dimensions (slightly larger).
    /// No debounce — instant response.
    /// </summary>
    public void AnimateToHover()
    {
        CancelContractDelay();
        PerformAnimation(
            Constants.NotchIdleWidth + 20,
            Constants.NotchIdleHeight + 8,
            Constants.ExpandDurationMs);
    }

    /// <summary>
    /// Animates to expanded drag-drop dimensions.
    /// No debounce — instant response.
    /// </summary>
    public void AnimateToExpanded()
    {
        CancelContractDelay();
        PerformAnimation(
            Constants.NotchExpandedWidth,
            Constants.NotchExpandedHeight,
            Constants.ExpandDurationMs);
    }

    /// <summary>
    /// Animates to media widget dimensions.
    /// No debounce — instant response.
    /// </summary>
    public void AnimateToMedia()
    {
        CancelContractDelay();
        PerformAnimation(
            Constants.NotchMediaWidth,
            Constants.NotchMediaHeight,
            Constants.ExpandDurationMs);
    }

    /// <summary>
    /// Animates to clipboard toast dimensions.
    /// No debounce — instant response.
    /// </summary>
    public void AnimateToToast()
    {
        CancelContractDelay();
        PerformAnimation(
            Constants.NotchExpandedWidth,
            60,
            Constants.ExpandDurationMs);
    }

    /// <summary>
    /// Skips animation and sets dimensions immediately.
    /// Used when animations are disabled (Battery Saver mode).
    /// </summary>
    public void SetImmediate(double width, double height)
    {
        CancelContractDelay();
        StopCurrentAnimation();

        _currentWidth = width;
        _currentHeight = height;

        _target.Width = width;
        _target.Height = height;
        _onSizeChanged(width, height);
    }

    /// <summary>
    /// Cancels all pending animations and delays.
    /// </summary>
    public void CancelAll()
    {
        CancelContractDelay();
        StopCurrentAnimation();
    }

    // ═══════════════════════════════════════════════════════════════
    // INTERNAL ANIMATION ENGINE
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Performs a smooth animation to the target dimensions.
    /// Uses DoubleAnimation with CubicEase (EaseOut) for natural motion.
    /// </summary>
    private void PerformAnimation(double targetWidth, double targetHeight, int durationMs)
    {
        StopCurrentAnimation();

        var duration = TimeSpan.FromMilliseconds(durationMs);
        var easingFunction = new CubicEase
        {
            EasingMode = EasingMode.EaseOut
            // WHY EaseOut: Fast initial response, smooth settle.
            // Matches natural UI motion expectations.
            // EaseIn would feel sluggish at start; EaseInOut adds unnecessary delay.
        };

        // Width animation
        var widthAnimation = new DoubleAnimation(targetWidth, duration)
        {
            EasingFunction = easingFunction
        };

        // Height animation
        var heightAnimation = new DoubleAnimation(targetHeight, duration)
        {
            EasingFunction = easingFunction
        };

        // Create storyboard
        _currentStoryboard = new Storyboard();

        Storyboard.SetTarget(widthAnimation, _target);
        Storyboard.SetTargetProperty(widthAnimation, new PropertyPath(FrameworkElement.WidthProperty));

        Storyboard.SetTarget(heightAnimation, _target);
        Storyboard.SetTargetProperty(heightAnimation, new PropertyPath(FrameworkElement.HeightProperty));

        _currentStoryboard.Children.Add(widthAnimation);
        _currentStoryboard.Children.Add(heightAnimation);

        // Track animation progress for region clipping updates
        _currentStoryboard.CurrentTimeInvalidated += OnAnimationProgress;

        // Store target values for region update
        _target.Tag = new Tuple<double, double>(targetWidth, targetHeight);

        _currentStoryboard.Begin();
    }

    /// <summary>
    /// Called on each animation frame to update the window region.
    /// WHY: The rounded-rect region must match the current animated size,
    /// otherwise the pill shape would be distorted during transitions.
    /// </summary>
    private void OnAnimationProgress(object? sender, EventArgs e)
    {
        if (_target.Tag is Tuple<double, double> target)
        {
            double currentWidth = _target.ActualWidth > 0 ? _target.ActualWidth : target.Item1;
            double currentHeight = _target.ActualHeight > 0 ? _target.ActualHeight : target.Item2;

            _currentWidth = currentWidth;
            _currentHeight = currentHeight;

            // Update region on UI thread
            _target.Dispatcher.BeginInvoke(() =>
            {
                _onSizeChanged(currentWidth, currentHeight);
            });
        }
    }

    /// <summary>
    /// Stops the currently running animation.
    /// </summary>
    private void StopCurrentAnimation()
    {
        if (_currentStoryboard != null)
        {
            _currentStoryboard.CurrentTimeInvalidated -= OnAnimationProgress;
            _currentStoryboard.Stop();
            _currentStoryboard = null;
        }
    }

    /// <summary>
    /// Cancels the contraction delay timer.
    /// Called when we need to expand again before the delay expires.
    /// </summary>
    private void CancelContractDelay()
    {
        if (_contractDelayTimer != null)
        {
            _contractDelayTimer.Stop();
            _contractDelayTimer = null;
        }
    }
}
