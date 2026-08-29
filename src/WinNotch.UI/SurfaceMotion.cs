using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WinNotch.Common;

namespace WinNotch.UI;

/// <summary>
/// Shared micro-motion for content appearing inside the notch.
/// Windows accessibility settings always take precedence over the user preset.
/// </summary>
internal static class SurfaceMotion
{
    private static MotionProfile s_profile = new(1.0, 167, 2.0);

    public static void Configure(AppearanceSettings settings)
        => s_profile = AppearanceResolver.ResolveMotion(settings);

    public static void Reveal(FrameworkElement element, double? offsetY = null, int? durationMs = null)
    {
        if (!SystemParameters.ClientAreaAnimation)
        {
            Reset(element);
            return;
        }

        bool reduced = s_profile.ContentOffsetY <= 0.01;
        double effectiveOffset = reduced ? 0 : offsetY ?? s_profile.ContentOffsetY;
        int effectiveDuration = Math.Max(1, reduced
            ? s_profile.ContentDurationMs
            : durationMs ?? s_profile.ContentDurationMs);

        var transform = element.RenderTransform as TranslateTransform ?? new TranslateTransform();
        element.RenderTransform = transform;
        element.Opacity = 0;
        transform.Y = effectiveOffset;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(effectiveDuration);

        var fade = new DoubleAnimation(0, 1, duration)
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.Stop
        };

        DoubleAnimation? slide = null;
        if (effectiveOffset > 0.01)
        {
            slide = new DoubleAnimation(effectiveOffset, 0, duration)
            {
                EasingFunction = ease,
                FillBehavior = FillBehavior.Stop
            };
        }

        fade.Completed += (_, _) => Reset(element);

        element.BeginAnimation(UIElement.OpacityProperty, fade);
        if (slide != null)
            transform.BeginAnimation(TranslateTransform.YProperty, slide);
        else
            transform.Y = 0;
    }

    private static void Reset(FrameworkElement element)
    {
        element.BeginAnimation(UIElement.OpacityProperty, null);
        element.Opacity = 1;
        if (element.RenderTransform is TranslateTransform transform)
        {
            transform.BeginAnimation(TranslateTransform.YProperty, null);
            transform.Y = 0;
        }
    }
}
