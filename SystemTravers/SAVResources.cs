using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace SAV
{
    public static class Resources
    {
        public static string readme => "BimboClub Parameter Filler Rules Engine";

        private static Bitmap _edit16;
        private static Bitmap _editGrey16;
        private static Bitmap _delete16;
        private static Bitmap _deleteGrey16;
        private static Bitmap _add32;
        private static Bitmap _addGrey32;
        private static Bitmap _add16;
        private static Bitmap _run16;
        private static Bitmap _run32;
        private static Bitmap _settings32;
        private static Bitmap _up16;
        private static Bitmap _down16;
        private static Bitmap _copy16;
        private static Bitmap _paste16;
        private static Bitmap _save16;
        private static Bitmap _open16;
        private static Bitmap _info16;
        private static Bitmap _help16;
        private static Bitmap _plus32;
        private static Bitmap _minus32;
        private static Bitmap _mult32;
        private static Bitmap _div32;
        private static Bitmap _clear32;
        private static Bitmap _backspace32;

        public static Bitmap EditL_16 => Edit16;
        public static Bitmap Edit16 => _edit16 ??= CreateEditIcon(Color.FromArgb(59, 130, 246));
        public static Bitmap Edit_16 => Edit16;
        public static Bitmap Edit_Grey_16 => _editGrey16 ??= CreateEditIcon(Color.FromArgb(100, 116, 139));

        public static Bitmap Delete16 => _delete16 ??= CreateDeleteIcon(Color.FromArgb(239, 68, 68));
        public static Bitmap Delete_16 => Delete16;
        public static Bitmap Delete_Grey_16 => _deleteGrey16 ??= CreateDeleteIcon(Color.FromArgb(148, 163, 184));
        public static Bitmap Delete_Grey16 => Delete_Grey_16;

        public static Bitmap Add_32 => _add32 ??= CreateAddButton(32, 32, Color.FromArgb(79, 70, 229));
        public static Bitmap Add_Grey32 => _addGrey32 ??= CreateAddButton(32, 32, Color.FromArgb(100, 116, 139));
        public static Bitmap Add_16 => _add16 ??= CreateAddButton(16, 16, Color.FromArgb(79, 70, 229));

        public static Bitmap Run_16 => _run16 ??= CreateRunIcon(16, 16, Color.FromArgb(16, 185, 129));
        public static Bitmap Run_32 => _run32 ??= CreateRunButton(32, 32, Color.FromArgb(16, 185, 129));

        public static Bitmap Settings2_32 => _settings32 ??= CreateSettingsIcon(32, 32, Color.FromArgb(71, 85, 105));

        public static Bitmap Up_16 => _up16 ??= CreateArrowIcon(16, 16, true, Color.FromArgb(71, 85, 105));
        public static Bitmap Down_16 => _down16 ??= CreateArrowIcon(16, 16, false, Color.FromArgb(71, 85, 105));

        public static Bitmap Copy16 => _copy16 ??= CreateTextIcon(16, 16, "⧉", Color.FromArgb(71, 85, 105), 10f);
        public static Bitmap Paste16 => _paste16 ??= CreateTextIcon(16, 16, "📋", Color.FromArgb(71, 85, 105), 9f);
        public static Bitmap Save16 => _save16 ??= CreateTextIcon(16, 16, "💾", Color.FromArgb(71, 85, 105), 9f);
        public static Bitmap Open16 => _open16 ??= CreateTextIcon(16, 16, "📂", Color.FromArgb(71, 85, 105), 9f);
        public static Bitmap Info_16 => _info16 ??= CreateTextIcon(16, 16, "ℹ", Color.FromArgb(59, 130, 246), 10f);
        public static Bitmap Help_16 => _help16 ??= CreateTextIcon(16, 16, "?", Color.FromArgb(59, 130, 246), 10f);

        public static Bitmap Plus_32_L => _plus32 ??= CreateCalcButton(32, 32, "+", Color.FromArgb(51, 65, 85));
        public static Bitmap Minus_32_L => _minus32 ??= CreateCalcButton(32, 32, "−", Color.FromArgb(51, 65, 85));
        public static Bitmap Multiply_32_L => _mult32 ??= CreateCalcButton(32, 32, "×", Color.FromArgb(51, 65, 85));
        public static Bitmap Divide_32_L => _div32 ??= CreateCalcButton(32, 32, "÷", Color.FromArgb(51, 65, 85));
        public static Bitmap Clear_32_L => _clear32 ??= CreateCalcButton(32, 32, "C", Color.FromArgb(220, 38, 38));
        public static Bitmap Backspace3_32x38_L => _backspace32 ??= CreateCalcButton(38, 32, "⌫", Color.FromArgb(51, 65, 85));

        private static Bitmap CreateEditIcon(Color color)
        {
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var pen = new Pen(color, 2.2f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawLine(pen, 3f, 13f, 12f, 4f);
                }

                using (var brush = new SolidBrush(color))
                {
                    PointF[] tip = { new PointF(2f, 14f), new PointF(5f, 13f), new PointF(3f, 11f) };
                    g.FillPolygon(brush, tip);

                    g.FillEllipse(brush, 11f, 2f, 3f, 3f);
                    g.FillRectangle(brush, 2f, 14f, 12f, 1.5f);
                }
            }
            return bmp;
        }

        private static Bitmap CreateDeleteIcon(Color color)
        {
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var pen = new Pen(color, 2f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawLine(pen, 4f, 4f, 12f, 12f);
                    g.DrawLine(pen, 12f, 4f, 4f, 12f);
                }
            }
            return bmp;
        }

        private static Bitmap CreateAddButton(int w, int h, Color color)
        {
            var bmp = new Bitmap(w, h);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var path = CreateRoundedRectangle(new RectangleF(1, 1, w - 2, h - 2), 4))
                using (var brush = new SolidBrush(color))
                {
                    g.FillPath(brush, path);
                }

                float midX = w / 2f;
                float midY = h / 2f;
                float arm = (w >= 32) ? 6f : 4f;
                float thick = (w >= 32) ? 2.5f : 1.8f;

                using (var pen = new Pen(Color.White, thick))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawLine(pen, midX - arm, midY, midX + arm, midY);
                    g.DrawLine(pen, midX, midY - arm, midX, midY + arm);
                }
            }
            return bmp;
        }

        private static Bitmap CreateRunButton(int w, int h, Color color)
        {
            var bmp = new Bitmap(w, h);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var path = CreateRoundedRectangle(new RectangleF(1, 1, w - 2, h - 2), 4))
                using (var brush = new SolidBrush(color))
                {
                    g.FillPath(brush, path);
                }

                float midX = w / 2f + 1f;
                float midY = h / 2f;
                float size = (w >= 32) ? 7f : 4f;

                PointF[] pts = {
                    new PointF(midX - size * 0.7f, midY - size),
                    new PointF(midX + size, midY),
                    new PointF(midX - size * 0.7f, midY + size)
                };

                using (var brush = new SolidBrush(Color.White))
                {
                    g.FillPolygon(brush, pts);
                }
            }
            return bmp;
        }

        private static Bitmap CreateRunIcon(int w, int h, Color color)
        {
            var bmp = new Bitmap(w, h);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                float midX = w / 2f + 0.5f;
                float midY = h / 2f;
                float size = 4.5f;

                PointF[] pts = {
                    new PointF(midX - size * 0.8f, midY - size),
                    new PointF(midX + size, midY),
                    new PointF(midX - size * 0.8f, midY + size)
                };

                using (var brush = new SolidBrush(color))
                {
                    g.FillPolygon(brush, pts);
                }
            }
            return bmp;
        }

        private static Bitmap CreateSettingsIcon(int w, int h, Color color)
        {
            var bmp = new Bitmap(w, h);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                using (var font = new Font("Segoe UI", w >= 32 ? 16f : 9f, FontStyle.Regular))
                using (var brush = new SolidBrush(color))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("⚙", font, brush, new RectangleF(0, 0, w, h), sf);
                }
            }
            return bmp;
        }

        private static Bitmap CreateArrowIcon(int w, int h, bool up, Color color)
        {
            var bmp = new Bitmap(w, h);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                float midX = w / 2f;
                float midY = h / 2f;
                float size = 4f;

                PointF[] pts = up
                    ? new[] { new PointF(midX, midY - size), new PointF(midX + size, midY + size), new PointF(midX - size, midY + size) }
                    : new[] { new PointF(midX, midY + size), new PointF(midX + size, midY - size), new PointF(midX - size, midY - size) };

                using (var brush = new SolidBrush(color))
                {
                    g.FillPolygon(brush, pts);
                }
            }
            return bmp;
        }

        private static Bitmap CreateCalcButton(int w, int h, string text, Color textColor)
        {
            var bmp = new Bitmap(w, h);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                using (var path = CreateRoundedRectangle(new RectangleF(1, 1, w - 2, h - 2), 3))
                using (var brush = new SolidBrush(Color.FromArgb(241, 245, 249)))
                using (var pen = new Pen(Color.FromArgb(203, 213, 225), 1))
                {
                    g.FillPath(brush, path);
                    g.DrawPath(pen, path);
                }

                using (var font = new Font("Segoe UI", 12f, FontStyle.Bold))
                using (var brush = new SolidBrush(textColor))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(text, font, brush, new RectangleF(0, 0, w, h), sf);
                }
            }
            return bmp;
        }

        private static Bitmap CreateTextIcon(int w, int h, string text, Color textColor, float fontSize)
        {
            var bmp = new Bitmap(w, h);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                using (var font = new Font("Segoe UI", fontSize, FontStyle.Bold))
                using (var brush = new SolidBrush(textColor))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(text, font, brush, new RectangleF(0, 0, w, h), sf);
                }
            }
            return bmp;
        }

        private static GraphicsPath CreateRoundedRectangle(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
