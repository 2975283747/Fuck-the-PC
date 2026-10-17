using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace TrayTemps;

public sealed class FanIcon : Grid
{
    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.Register(
            nameof(IsActive),
            typeof(bool),
            typeof(FanIcon),
            new PropertyMetadata(
                false,
                static (d, _) => ((FanIcon)d).OnStateChanged()));

    public static readonly DependencyProperty IsSpinningProperty =
        DependencyProperty.Register(
            nameof(IsSpinning),
            typeof(bool),
            typeof(FanIcon),
            new PropertyMetadata(
                false,
                static (d, _) => ((FanIcon)d).OnStateChanged()));

    public static readonly DependencyProperty SpeedPercentProperty =
        DependencyProperty.Register(
            nameof(SpeedPercent),
            typeof(double),
            typeof(FanIcon),
            new PropertyMetadata(
                50d,
                static (d, _) => ((FanIcon)d).RestartAnimation()));

    private readonly RotateTransform _rotation = new(0);
    private readonly FanRingVisual _ring = new();
    private readonly FanRotorVisual _rotor;
    private double _lastSpeedPercent = -1;

    public FanIcon()
    {
        SnapsToDevicePixels = true;
        _rotor = new FanRotorVisual(_rotation);
        Children.Add(_ring);
        Children.Add(_rotor);
        Effect = new DropShadowEffect
        {
            BlurRadius = 14,
            ShadowDepth = 3,
            Opacity = 0.24,
            Color = Colors.Black
        };
        Loaded += (_, _) =>
        {
            ThemeManager.ThemeChanged += OnThemeChanged;
            OnStateChanged();
        };
        Unloaded += (_, _) =>
        {
            ThemeManager.ThemeChanged -= OnThemeChanged;
            StopAnimation();
        };
    }

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public bool IsSpinning
    {
        get => (bool)GetValue(IsSpinningProperty);
        set => SetValue(IsSpinningProperty, value);
    }

    public double SpeedPercent
    {
        get => (double)GetValue(SpeedPercentProperty);
        set => SetValue(SpeedPercentProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = double.IsInfinity(availableSize.Width)
            ? 96
            : availableSize.Width;
        double height = double.IsInfinity(availableSize.Height)
            ? 96
            : availableSize.Height;
        var size = new Size(width, height);
        _ring.Measure(size);
        _rotor.Measure(size);
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var bounds = new Rect(0, 0, finalSize.Width, finalSize.Height);
        _ring.Arrange(bounds);
        _rotor.Arrange(bounds);
        return finalSize;
    }

    private void OnStateChanged()
    {
        _rotor.Opacity = IsActive ? 1 : 0.68;

        if (IsActive && IsSpinning)
        {
            RestartAnimation();
        }
        else
        {
            StopAnimation();
        }
    }

    private void RestartAnimation()
    {
        if (!IsActive || !IsSpinning)
        {
            StopAnimation();
            return;
        }

        double currentAngle = _rotation.Angle;
        double percent = Math.Clamp(SpeedPercent, 0, 100);

        if (_rotation.HasAnimatedProperties &&
            _lastSpeedPercent >= 0 &&
            Math.Abs(percent - _lastSpeedPercent) < 5)
        {
            return;
        }

        _lastSpeedPercent = percent;
        double secondsPerRotation = 2.4 - ((percent / 100) * 1.9);

        _rotation.BeginAnimation(RotateTransform.AngleProperty, null);
        _rotation.Angle = currentAngle;
        _rotation.BeginAnimation(
            RotateTransform.AngleProperty,
            new DoubleAnimation
            {
                From = currentAngle,
                To = currentAngle + 360,
                Duration = new Duration(TimeSpan.FromSeconds(secondsPerRotation)),
                RepeatBehavior = RepeatBehavior.Forever
            },
            HandoffBehavior.SnapshotAndReplace);
    }

    private void StopAnimation()
    {
        double frozenAngle = _rotation.Angle;
        _rotation.BeginAnimation(RotateTransform.AngleProperty, null);
        _rotation.Angle = frozenAngle % 360;
        _lastSpeedPercent = -1;
    }

    private void OnThemeChanged()
    {
        _ring.InvalidateVisual();
        _rotor.InvalidateVisual();
    }

    private static Color GetBladeColor(FrameworkElement element)
    {
        return element.TryFindResource("FanBladeBrush") is SolidColorBrush brush
            ? brush.Color
            : Color.FromRgb(10, 10, 10);
    }

    private static Brush FindBrush(
        FrameworkElement element,
        string key,
        Color fallback)
    {
        return element.TryFindResource(key) as Brush ??
               new SolidColorBrush(fallback);
    }

    private sealed class FanRingVisual : FrameworkElement
    {
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
        }
    }

    private sealed class FanRotorVisual : FrameworkElement
    {
        private readonly RotateTransform _rotation;

        public FanRotorVisual(RotateTransform rotation)
        {
            _rotation = rotation;
            RenderTransformOrigin = new Point(0.5, 0.5);
            RenderTransform = rotation;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            double size = Math.Min(RenderSize.Width, RenderSize.Height);
            if (size <= 8)
            {
                return;
            }

            var center = new Point(RenderSize.Width / 2, RenderSize.Height / 2);
            double radius = size * 0.42;
            var bladeGeometry = CreateBladeGeometry(center, radius);
            Color bladeColor = GetBladeColor(this);
            Brush innerBrush = FindBrush(this, "FanInnerBrush", Colors.White);
            var bladeBrush = new SolidColorBrush(bladeColor);

            for (int index = 0; index < 5; index++)
            {
                drawingContext.PushTransform(
                    new RotateTransform(index * 72, center.X, center.Y));
                drawingContext.DrawGeometry(bladeBrush, null, bladeGeometry);
                drawingContext.Pop();
            }

            double hubRadius = size * 0.155;
            drawingContext.DrawEllipse(
                innerBrush,
                null,
                center,
                hubRadius,
                hubRadius);

            double centerRadius = size * 0.085;
            drawingContext.DrawEllipse(
                new SolidColorBrush(bladeColor),
                null,
                center,
                centerRadius,
                centerRadius);
        }

        private static StreamGeometry CreateBladeGeometry(Point center, double radius)
        {
            Point rootInner = new(center.X + (radius * 0.03), center.Y - (radius * 0.18));
            Point leadingTip = new(center.X - (radius * 0.26), center.Y - (radius * 0.98));
            Point notch = new(center.X + (radius * 0.22), center.Y - (radius * 0.48));

            var geometry = new StreamGeometry();
            using (StreamGeometryContext context = geometry.Open())
            {
                context.BeginFigure(rootInner, isFilled: true, isClosed: true);
                context.BezierTo(
                    new Point(center.X - (radius * 0.52), center.Y - (radius * 0.36)),
                    new Point(center.X - (radius * 0.66), center.Y - (radius * 0.76)),
                    leadingTip,
                    isStroked: true,
                    isSmoothJoin: true);
                context.BezierTo(
                    new Point(center.X + (radius * 0.10), center.Y - (radius * 1.02)),
                    new Point(center.X + (radius * 0.42), center.Y - (radius * 0.76)),
                    notch,
                    isStroked: true,
                    isSmoothJoin: true);
                context.BezierTo(
                    new Point(center.X + (radius * 0.40), center.Y - (radius * 0.38)),
                    new Point(center.X + (radius * 0.25), center.Y - (radius * 0.20)),
                    rootInner,
                    isStroked: true,
                    isSmoothJoin: true);
            }

            geometry.Freeze();
            return geometry;
        }

    }
}
