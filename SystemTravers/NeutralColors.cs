using System.Windows.Media;

namespace BimboClub
{
    // Neutral graphite surfaces and a restrained blue action color, close to Revit's dark UI.
    internal static class NeutralColors
    {
        public static Color Window => Color.FromRgb(38, 38, 38);
        public static Color Surface => Color.FromRgb(47, 47, 47);
        public static Color Input => Color.FromRgb(56, 56, 56);
        public static Color Hover => Color.FromRgb(67, 67, 67);
        public static Color Border => Color.FromRgb(92, 92, 92);
        public static Color Muted => Color.FromRgb(187, 187, 187);
        public static Color Accent => Color.FromRgb(0, 120, 212);
        public static Color AccentHover => Color.FromRgb(30, 140, 225);
    }
}
