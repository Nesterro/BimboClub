using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BimboClub
{
    internal static class RibbonIcons
    {
        private static readonly Dictionary<string, BitmapSource> Cache = new Dictionary<string, BitmapSource>();
        private static readonly Dictionary<string, string> Shapes = new Dictionary<string, string>
        {
            ["3d"] = "M4,9 L16,3 28,9 28,23 16,29 4,23 Z M4,9 L16,15 28,9 M16,15 L16,29",
            ["tags"] = "M4,6 L19,6 28,15 17,26 4,13 Z M9,10 L9,12 M17,12 L23,18",
            ["wall"] = "M4,4 L28,4 28,28 4,28 Z M12,28 L12,14 21,14 21,28 M4,10 L28,10",
            ["floor"] = "M3,13 L18,5 29,12 14,21 Z M3,19 L14,27 29,18 M11,13 L17,10 22,13 16,16 Z",
            ["copy"] = "M10,10 L26,10 26,28 10,28 Z M6,22 L3,22 3,3 21,3 21,6",
            ["print"] = "M8,11 L8,3 24,3 24,11 M8,23 L3,23 3,11 29,11 29,23 24,23 M8,19 L24,19 24,29 8,29 Z M23,15 L25,15",
            ["router"] = "M3,24 L11,24 11,8 22,8 22,24 29,24 M25,20 L29,24 25,28",
            ["filters"] = "M3,5 L29,5 20,16 20,25 12,29 12,16 Z",
            ["rename"] = "M4,26 L11,24 27,8 22,3 6,19 Z M6,19 L11,24 M18,7 L23,12 M16,28 L28,28",
            ["network"] = "M16,4 L16,14 M6,14 L26,14 M6,14 L6,24 M16,14 L16,24 M26,14 L26,24 M3,24 L9,24 9,29 3,29 Z M13,24 L19,24 19,29 13,29 Z M23,24 L29,24 29,29 23,29 Z",
            ["json"] = "M12,4 L8,4 8,12 4,16 8,20 8,28 12,28 M20,4 L24,4 24,12 28,16 24,20 24,28 20,28 M15,13 L15,15 M15,21 L15,23",
            ["excel"] = "M5,3 L27,3 27,29 5,29 Z M5,11 L27,11 M5,19 L27,19 M13,11 L13,29 M20,11 L20,29",
            ["riser"] = "M12,28 L12,4 M20,28 L20,4 M7,9 L12,4 17,9 M15,23 L20,28 25,23 M3,16 L29,16",
            ["connect"] = "M3,12 L12,12 12,20 3,20 M29,12 L20,12 20,20 29,20 M12,16 L20,16 M16,6 L16,10 M16,22 L16,26",
            ["crop"] = "M8,2 L8,24 30,24 M2,8 L24,8 24,30 M13,13 L19,19",
            ["param"] = "M4,7 L28,7 M4,16 L28,16 M4,25 L28,25 M10,3 L10,11 M22,12 L22,20 M14,21 L14,29",
            ["specs"] = "M5,3 L27,3 27,29 5,29 Z M10,9 L12,9 M16,9 L23,9 M10,16 L12,16 M16,16 L23,16 M10,23 L12,23 M16,23 L23,23",
            ["sizer"] = "M3,10 L29,10 29,24 3,24 Z M8,10 L8,16 M14,10 L14,19 M20,10 L20,16 M26,10 L26,19",
            ["heat"] = "M12,5 A4,4 0 0 1 20,5 L20,19 A7,7 0 1 1 12,19 Z M16,10 L16,23 M24,8 L29,8 M24,14 L28,14",
            ["rules"] = "M4,6 L11,6 M4,16 L11,16 M4,26 L11,26 M11,6 L17,16 11,26 M17,16 L28,16 M24,12 L28,16 24,20",
            ["fill_params"] = "M4,4 L22,4 22,12 M4,4 L4,28 22,28 22,23 M10,10 L16,10 M10,16 L16,16 M18,18 L28,18 M24,14 L28,18 24,22"
        };

        public static BitmapSource FromFileName(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            int size = name.EndsWith("_16", StringComparison.Ordinal) ? 16 : 32;
            string key = name.Replace("icon_", "").Replace("_16", "");
            if (!Shapes.TryGetValue(key, out string shape)) return null;
            string cacheKey = key + size;
            if (Cache.TryGetValue(cacheKey, out var cached)) return cached;
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.PushTransform(new ScaleTransform(size / 32.0, size / 32.0));
                // Pale backing stroke keeps the glyph legible on light and dark host themes.
                var geometry = Geometry.Parse(shape);
                var halo = new Pen(new SolidColorBrush(Color.FromRgb(235, 242, 248)), 3.8);
                var ink = new Pen(new SolidColorBrush(Color.FromRgb(0, 103, 176)), 2.0);
                halo.LineJoin = ink.LineJoin = PenLineJoin.Round;
                dc.DrawGeometry(null, halo, geometry);
                dc.DrawGeometry(null, ink, geometry);
                dc.Pop();
            }
            var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            Cache[cacheKey] = bitmap;
            return bitmap;
        }
    }
}
