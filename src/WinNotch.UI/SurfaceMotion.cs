using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace WinNotch.UI;

/// <summary>
/// Shared micro-motion for content appearing inside the notch.
/// Container geometry remains owned by NotchMotionController.
/// Respects the Windows client-area animation accessibility preference.
/// </summary>
internal static class SurfaceMotion
{
    public static void Reveal(FrameworkElement element, double offsetY = 2, int durationMs = 110)
    {
        if (!SystemParameters.ClientAreaAnimation)
        {
            element.Opacity = 1;
            if (element.RenderTransform is TranslateTransform disabledTransform)
                disabledTransform.Y = 0;
            return;
        }

        var transform = element.RenderTransform as TranslateTransform ?? new TranslateTransform();
        element.RenderTransform = transform;
        element.Opacity = 0;
        transform.Y = offsetY;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(durationMs);

        var fade = new DoubleAnimation(0, 1, duration)
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.Stop
        };
        var slide = new DoubleAnimation(offsetY, 0, duration)
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.Stop
        };

        fade.Completed += (_, _) =>
        {
            element.BeginAnimation(UIElement.OpacityProperty, null);
            transform.BeginAnimation(TranslateTransform.YProperty, null);
            element.Opacity = 1;
            transform.Y = 0;
        };

        element.BeginAnimation(UIElement.OpacityProperty, fade);
        transform.BeginAnimation(TranslateTransform.YProperty, slide);
    }
}
