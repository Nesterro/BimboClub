using System;
using System.Globalization;

namespace BimboClub
{
    public sealed class OpeningRoundingOptions
    {
        public double SizeStepMm { get; set; } = 10;
        public double BottomStepMm { get; set; } = 10;

        public static bool TryParseStep(string text, out double value) =>
            double.TryParse((text ?? "").Trim().Replace(',', '.'), NumberStyles.Float,
                CultureInfo.InvariantCulture, out value) && !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0;

        internal static double Up(double value, double step, double tolerance = 0)
        {
            Validate(value, step);
            if (step == 0) return value;
            return Math.Ceiling(Snap(value / step, tolerance / step)) * step;
        }

        internal static double Down(double value, double step, double tolerance = 0)
        {
            Validate(value, step);
            if (step == 0) return value;
            return Math.Floor(Snap(value / step, tolerance / step)) * step;
        }

        // Ignore conversion and model precision noise at a step boundary.
        private static double Snap(double quotient, double tolerance)
        {
            double nearest = Math.Round(quotient);
            return Math.Abs(quotient - nearest) <= Math.Max(1e-9, tolerance) ? nearest : quotient;
        }

        private static void Validate(double value, double step)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || double.IsNaN(step) || double.IsInfinity(step) || step < 0)
                throw new ArgumentOutOfRangeException(nameof(step), "Шаг должен быть конечным неотрицательным числом.");
        }
    }
}
