using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Point = System.Windows.Point;

namespace TrayTemps;

internal static class WindowAnimator
{
    public static void Attach(Window window)
    {
        window.Loaded += (_, _) => AnimateIn(window);
    }

    private static void AnimateIn(Window window)
    {
        if (window.Content is not FrameworkElement root)
        {
            return;
        }

        var scale = new ScaleTransform(0.94, 0.94);
        var translate = new TranslateTransform(0, 10);
        var transform = new TransformGroup();
        transform.Children.Add(scale);
        transform.Children.Add(translate);

        root.RenderTransformOrigin = new Point(0.5, 0.5);
        root.RenderTransform = transform;
        root.Opacity = 0;

        var duration = new Duration(TimeSpan.FromMilliseconds(210));
        var easing = new CubicEase
        {
            EasingMode = EasingMode.EaseOut
        };

        root.BeginAnimation(
            UIElement.OpacityProperty,
            new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = duration,
                EasingFunction = easing
            },
            HandoffBehavior.SnapshotAndReplace);

        scale.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            new DoubleAnimation
            {
                From = 0.94,
                To = 1,
                Duration = duration,
                EasingFunction = easing
            },
            HandoffBehavior.SnapshotAndReplace);
        scale.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            new DoubleAnimation
            {
                From = 0.94,
                To = 1,
                Duration = duration,
                EasingFunction = easing
            },
            HandoffBehavior.SnapshotAndReplace);
        translate.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation
            {
                From = 10,
                To = 0,
                Duration = duration,
                EasingFunction = easing
            },
            HandoffBehavior.SnapshotAndReplace);
    }
}
