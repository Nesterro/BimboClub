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

        internal static double Up(double value, double step)
        {
            Validate(value, step);
            if (step == 0) return value;
            return Math.Ceiling(Snap(value / step)) * step;
        }

        internal static double Down(double value, double step)
        {
            Validate(value, step);
            if (step == 0) return value;
            return Math.Floor(Snap(value / step)) * step;
        }

        // Avoid adding a whole step to an exact multiple after feet/mm conversion.
        private static double Snap(double quotient) => Math.Abs(quotient - Math.Round(quotient)) < 1e-9
            ? Math.Round(quotient) : quotient;

        private static void Validate(double value, double step)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || double.IsNaN(step) || double.IsInfinity(step) || step < 0)
                throw new ArgumentOutOfRangeException(nameof(step), "Шаг должен быть конечным неотрицательным числом.");
        }
    }
}
