using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace TrayTemps;

public sealed class CurveEditor : FrameworkElement
{
    private const double MinTemperature = 0;
    private const double MaxTemperature = 120;
    private const double MinPercent = 0;
    private const double MaxPercent = 100;
    private const double LeftMargin = 46;
    private const double RightMargin = 16;
    private const double TopMargin = 18;
    private const double BottomMargin = 34;

    private readonly List<CurvePoint> _points = [];
    private readonly Typeface _typeface = new("Microsoft YaHei UI");
    private CurvePoint? _draggedPoint;

    public CurveEditor()
    {
        Focusable = true;
        Cursor = Cursors.Cross;
        MinHeight = 240;
    }

    public event EventHandler? PointsChanged;

    public IReadOnlyList<CurvePoint> Points =>
        _points
            .OrderBy(static point => point.Temperature)
            .Select(static point => new CurvePoint
            {
                Temperature = point.Temperature,
                Percent = point.Percent
            })
            .ToList();

    public void SetPoints(IEnumerable<CurvePoint> points)
    {
        _points.Clear();
        _points.AddRange(CurveMath.Normalize(points));

        if (_points.Count == 0)
        {
            _points.Add(new CurvePoint { Temperature = 40, Percent = 30 });
            _points.Add(new CurvePoint { Temperature = 80, Percent = 100 });
        }

        InvalidateVisual();
        PointsChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = double.IsInfinity(availableSize.Width)
            ? 560
            : availableSize.Width;
        double height = double.IsInfinity(availableSize.Height)
            ? 280
            : availableSize.Height;
        return new Size(width, height);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        double width = RenderSize.Width;
        double height = RenderSize.Height;
        if (width < 80 || height < 80)
        {
            return;
        }

        var background = new SolidColorBrush(Color.FromRgb(255, 255, 255));
        var borderPen = new Pen(
            new SolidColorBrush(Color.FromRgb(216, 216, 222)),
            1);
        drawingContext.DrawRoundedRectangle(
            background,
            borderPen,
            new Rect(0.5, 0.5, width - 1, height - 1),
            12,
            12);

        Rect plot = GetPlotRect();
        DrawGrid(drawingContext, plot);
        DrawCurve(drawingContext, plot);
        DrawPoints(drawingContext, plot);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Point mouse = e.GetPosition(this);
        CurvePoint? point = HitTestPoint(mouse);

        if (point is null)
        {
            point = FromPixel(mouse);
            _points.Add(point);
            SortPoints();
            PointsChanged?.Invoke(this, EventArgs.Empty);
        }

        _draggedPoint = point;
        CaptureMouse();
        Cursor = Cursors.Hand;
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Point mouse = e.GetPosition(this);

        if (_draggedPoint is not null && e.LeftButton == MouseButtonState.Pressed)
        {
            UpdatePointFromPixel(_draggedPoint, mouse);
            SortPoints();
            InvalidateVisual();
            PointsChanged?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
            return;
        }

        Cursor = HitTestPoint(mouse) is not null
            ? Cursors.Hand
            : Cursors.Cross;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);

        if (_draggedPoint is not null)
        {
            _draggedPoint = null;
            ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonDown(e);

        CurvePoint? point = HitTestPoint(e.GetPosition(this));
        if (point is null || _points.Count <= 2)
        {
            return;
        }

        _points.Remove(point);
        InvalidateVisual();
        PointsChanged?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private Rect GetPlotRect()
    {
        return new Rect(
            LeftMargin,
            TopMargin,
            Math.Max(10, RenderSize.Width - LeftMargin - RightMargin),
            Math.Max(10, RenderSize.Height - TopMargin - BottomMargin));
    }

    private void DrawGrid(DrawingContext drawingContext, Rect plot)
    {
        var gridPen = new Pen(
            new SolidColorBrush(Color.FromRgb(235, 235, 240)),
            1);
        var axisBrush = new SolidColorBrush(Color.FromRgb(110, 110, 115));
        double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        for (int percent = 0; percent <= 100; percent += 20)
        {
            double y = PercentToY(percent, plot);
            drawingContext.DrawLine(
                gridPen,
                new Point(plot.Left, y),
                new Point(plot.Right, y));
            DrawText(
                drawingContext,
                $"{percent}%",
                new Point(4, y - 8),
                10,
                axisBrush,
                pixelsPerDip);
        }

        for (int temperature = 0; temperature <= 120; temperature += 20)
        {
            double x = TemperatureToX(temperature, plot);
            drawingContext.DrawLine(
                gridPen,
                new Point(x, plot.Top),
                new Point(x, plot.Bottom));
            DrawText(
                drawingContext,
                $"{temperature}\u00B0",
                new Point(x - 9, plot.Bottom + 7),
                10,
                axisBrush,
                pixelsPerDip);
        }

        DrawText(
            drawingContext,
            "\u8f6c\u901f",
            new Point(4, 2),
            11,
            axisBrush,
            pixelsPerDip);
        DrawText(
            drawingContext,
            "\u6e29\u5ea6 \u00B0C",
            new Point(plot.Right - 52, plot.Bottom + 15),
            11,
            axisBrush,
            pixelsPerDip);
    }

    private void DrawCurve(DrawingContext drawingContext, Rect plot)
    {
        if (_points.Count == 0)
        {
            return;
        }

        var accent = Color.FromRgb(0, 113, 227);
        var curvePen = new Pen(new SolidColorBrush(accent), 3)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };

        var curveGeometry = new StreamGeometry();
        var fillGeometry = new StreamGeometry();
        List<CurvePoint> points = CurveMath.Normalize(_points);

        using (StreamGeometryContext curveContext = curveGeometry.Open())
        using (StreamGeometryContext fillContext = fillGeometry.Open())
        {
            bool first = true;

            for (int sample = 0; sample <= 240; sample++)
            {
                double temperature =
                    MinTemperature +
                    ((MaxTemperature - MinTemperature) * sample / 240.0);
                double percent = CurveMath.Evaluate(points, temperature);
                Point point = new(
                    TemperatureToX(temperature, plot),
                    PercentToY(percent, plot));

                if (first)
                {
                    curveContext.BeginFigure(point, isFilled: false, isClosed: false);
                    fillContext.BeginFigure(
                        new Point(point.X, plot.Bottom),
                        isFilled: true,
                        isClosed: true);
                    fillContext.LineTo(point, isStroked: false, isSmoothJoin: true);
                    first = false;
                }
                else
                {
                    curveContext.LineTo(point, isStroked: true, isSmoothJoin: true);
                    fillContext.LineTo(point, isStroked: false, isSmoothJoin: true);
                }
            }

            fillContext.LineTo(
                new Point(plot.Right, plot.Bottom),
                isStroked: false,
                isSmoothJoin: true);
        }

        var fillBrush = new LinearGradientBrush(
            Color.FromArgb(42, accent.R, accent.G, accent.B),
            Color.FromArgb(0, accent.R, accent.G, accent.B),
            new Point(0, 0),
            new Point(0, 1));
        drawingContext.DrawGeometry(fillBrush, null, fillGeometry);
        drawingContext.DrawGeometry(null, curvePen, curveGeometry);
    }

    private void DrawPoints(DrawingContext drawingContext, Rect plot)
    {
        var fill = new SolidColorBrush(Colors.White);
        var stroke = new Pen(
            new SolidColorBrush(Color.FromRgb(0, 113, 227)),
            2);

        foreach (CurvePoint point in _points)
        {
            Point center = ToPixel(point, plot);
            double radius = ReferenceEquals(point, _draggedPoint) ? 8 : 6;
            drawingContext.DrawEllipse(fill, stroke, center, radius, radius);
        }
    }

    private void DrawText(
        DrawingContext drawingContext,
        string text,
        Point origin,
        double fontSize,
        Brush brush,
        double pixelsPerDip)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            System.Windows.FlowDirection.LeftToRight,
            _typeface,
            fontSize,
            brush,
            pixelsPerDip);
        drawingContext.DrawText(formatted, origin);
    }

    private CurvePoint? HitTestPoint(Point mouse)
    {
        Rect plot = GetPlotRect();

        return _points
            .OrderBy(point => (ToPixel(point, plot) - mouse).Length)
            .FirstOrDefault(point => (ToPixel(point, plot) - mouse).Length <= 13);
    }

    private Point ToPixel(CurvePoint point, Rect plot)
    {
        return new Point(
            TemperatureToX(point.Temperature, plot),
            PercentToY(point.Percent, plot));
    }

    private CurvePoint FromPixel(Point mouse)
    {
        Rect plot = GetPlotRect();
        double temperature =
            MinTemperature +
            ((Math.Clamp(mouse.X, plot.Left, plot.Right) - plot.Left) /
             plot.Width * (MaxTemperature - MinTemperature));
        double percent =
            MaxPercent -
            ((Math.Clamp(mouse.Y, plot.Top, plot.Bottom) - plot.Top) /
             plot.Height * (MaxPercent - MinPercent));

        return new CurvePoint
        {
            Temperature = Math.Clamp(temperature, MinTemperature, MaxTemperature),
            Percent = Math.Clamp(percent, MinPercent, MaxPercent)
        };
    }

    private void UpdatePointFromPixel(CurvePoint point, Point mouse)
    {
        CurvePoint updated = FromPixel(mouse);
        point.Temperature = updated.Temperature;
        point.Percent = updated.Percent;
    }

    private void SortPoints()
    {
        List<CurvePoint> ordered = _points
            .OrderBy(static point => point.Temperature)
            .ToList();
        _points.Clear();
        _points.AddRange(ordered);
    }

    private double TemperatureToX(double temperature, Rect plot)
    {
        return plot.Left +
               ((Math.Clamp(temperature, MinTemperature, MaxTemperature) -
                 MinTemperature) /
                (MaxTemperature - MinTemperature) * plot.Width);
    }

    private double PercentToY(double percent, Rect plot)
    {
        return plot.Bottom -
               ((Math.Clamp(percent, MinPercent, MaxPercent) - MinPercent) /
                (MaxPercent - MinPercent) * plot.Height);
    }
}
