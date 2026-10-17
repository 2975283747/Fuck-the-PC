namespace TrayTemps;

internal static class CurveMath
{
    public static double Evaluate(
        IReadOnlyList<CurvePoint> source,
        double temperature)
    {
        List<CurvePoint> points = Normalize(source);

        if (points.Count == 0)
        {
            return 0;
        }

        if (points.Count == 1)
        {
            return points[0].Percent;
        }

        if (temperature <= points[0].Temperature)
        {
            return points[0].Percent;
        }

        if (temperature >= points[^1].Temperature)
        {
            return points[^1].Percent;
        }

        int interval = 0;
        while (interval < points.Count - 2 &&
               temperature > points[interval + 1].Temperature)
        {
            interval++;
        }

        double[] slopes = CalculateSlopes(points);
        double x0 = points[interval].Temperature;
        double x1 = points[interval + 1].Temperature;
        double y0 = points[interval].Percent;
        double y1 = points[interval + 1].Percent;
        double h = x1 - x0;
        double t = (temperature - x0) / h;

        double h00 = (2 * t * t * t) - (3 * t * t) + 1;
        double h10 = (t * t * t) - (2 * t * t) + t;
        double h01 = (-2 * t * t * t) + (3 * t * t);
        double h11 = (t * t * t) - (t * t);

        return (h00 * y0) +
               (h10 * h * slopes[interval]) +
               (h01 * y1) +
               (h11 * h * slopes[interval + 1]);
    }

    public static List<CurvePoint> Normalize(IEnumerable<CurvePoint> source)
    {
        return source
            .Where(static point =>
                double.IsFinite(point.Temperature) &&
                double.IsFinite(point.Percent))
            .Select(static point => new CurvePoint
            {
                Temperature = Math.Clamp(point.Temperature, 0, 120),
                Percent = Math.Clamp(point.Percent, 0, 100)
            })
            .OrderBy(static point => point.Temperature)
            .ToList();
    }

    private static double[] CalculateSlopes(IReadOnlyList<CurvePoint> points)
    {
        int count = points.Count;
        var intervals = new double[count - 1];
        var deltas = new double[count - 1];
        var slopes = new double[count];

        for (int index = 0; index < count - 1; index++)
        {
            intervals[index] = Math.Max(
                0.001,
                points[index + 1].Temperature - points[index].Temperature);
            deltas[index] =
                (points[index + 1].Percent - points[index].Percent) /
                intervals[index];
        }

        slopes[0] = deltas[0];
        slopes[^1] = deltas[^1];

        for (int index = 1; index < count - 1; index++)
        {
            double previous = deltas[index - 1];
            double next = deltas[index];

            if (previous * next <= 0)
            {
                slopes[index] = 0;
                continue;
            }

            double w1 = (2 * intervals[index]) + intervals[index - 1];
            double w2 = intervals[index] + (2 * intervals[index - 1]);
            slopes[index] = (w1 + w2) /
                            ((w1 / previous) + (w2 / next));
        }

        return slopes;
    }
}
