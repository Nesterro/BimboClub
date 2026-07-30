using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace BimboClub
{
    public class ExcelTable
    {
        public string SheetName { get; set; }
        public string Title { get; set; } = "";
        public List<string> Headers { get; set; } = new List<string>();
        public List<List<object>> Rows { get; set; } = new List<List<object>>();
        public HashSet<int> NumericSumColumns { get; set; } = new HashSet<int>(); // 0-based indices of columns to sum at the bottom
    }

    public static class ExcelWriter
    {
        public static void Write(string filePath, List<ExcelTable> tables)
        {
            using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, Encoding.UTF8))
            {
                writer.WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
                writer.WriteLine("<?mso-application progid=\"Excel.Sheet\"?>");
                writer.WriteLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\"");
                writer.WriteLine(" xmlns:o=\"urn:schemas-microsoft-com:office:office\"");
                writer.WriteLine(" xmlns:x=\"urn:schemas-microsoft-com:office:excel\"");
                writer.WriteLine(" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\"");
                writer.WriteLine(" xmlns:html=\"http://www.w3.org/TR/REC-html40\">");

                // Document metadata
                writer.WriteLine(" <DocumentProperties xmlns=\"urn:schemas-microsoft-com:office:office\">");
                writer.WriteLine("  <Author>BimboClub Tools</Author>");
                writer.WriteLine($"  <Created>{DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")}</Created>");
                writer.WriteLine(" </DocumentProperties>");

                // Styles
                writer.WriteLine(" <Styles>");
                writer.WriteLine("  <Style ss:ID=\"Default\" ss:Name=\"Normal\">");
                writer.WriteLine("   <Alignment ss:Vertical=\"Center\"/>");
                writer.WriteLine("   <Font ss:FontName=\"Segoe UI\" ss:Size=\"10\" ss:Color=\"#333333\"/>");
                writer.WriteLine("  </Style>");
                
                writer.WriteLine("  <Style ss:ID=\"TitleStyle\">");
                writer.WriteLine("   <Font ss:FontName=\"Segoe UI\" ss:Size=\"14\" ss:Bold=\"1\" ss:Color=\"#B30E2D\"/>");
                writer.WriteLine("   <Alignment ss:Horizontal=\"Center\" ss:Vertical=\"Center\"/>");
                writer.WriteLine("  </Style>");

                writer.WriteLine("  <Style ss:ID=\"Header\">");
                writer.WriteLine("   <Font ss:FontName=\"Segoe UI\" ss:Size=\"10\" ss:Bold=\"1\" ss:Color=\"#FFFFFF\"/>");
                writer.WriteLine("   <Interior ss:Color=\"#B30E2D\" ss:Pattern=\"Solid\"/>"); // Brand Crimson
                writer.WriteLine("   <Alignment ss:Horizontal=\"Center\" ss:Vertical=\"Center\" ss:WrapText=\"1\"/>");
                writer.WriteLine("   <Borders>");
                writer.WriteLine("    <Border ss:Position=\"Bottom\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#7A091E\"/>");
                writer.WriteLine("    <Border ss:Position=\"Left\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#D26D7E\"/>");
                writer.WriteLine("    <Border ss:Position=\"Right\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#D26D7E\"/>");
                writer.WriteLine("   </Borders>");
                writer.WriteLine("  </Style>");

                writer.WriteLine("  <Style ss:ID=\"Zebra\">");
                writer.WriteLine("   <Interior ss:Color=\"#F8F9FA\" ss:Pattern=\"Solid\"/>");
                writer.WriteLine("   <Alignment ss:Vertical=\"Center\"/>");
                writer.WriteLine("  </Style>");

                writer.WriteLine("  <Style ss:ID=\"Summary\">");
                writer.WriteLine("   <Font ss:FontName=\"Segoe UI\" ss:Size=\"10\" ss:Bold=\"1\" ss:Color=\"#000000\"/>");
                writer.WriteLine("   <Interior ss:Color=\"#E9ECEF\" ss:Pattern=\"Solid\"/>"); // Gray summary
                writer.WriteLine("   <Borders>");
                writer.WriteLine("    <Border ss:Position=\"Top\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#CCCCCC\"/>");
                writer.WriteLine("    <Border ss:Position=\"Bottom\" ss:LineStyle=\"Double\" ss:Weight=\"2\" ss:Color=\"#333333\"/>");
                writer.WriteLine("   </Borders>");
                writer.WriteLine("   <Alignment ss:Vertical=\"Center\"/>");
                writer.WriteLine("  </Style>");
                writer.WriteLine(" </Styles>");

                foreach (var table in tables)
                {
                    string safeSheetName = EscapeXmlAttribute(table.SheetName);
                    writer.WriteLine($" <Worksheet ss:Name=\"{safeSheetName}\">");
                    writer.WriteLine("  <Table>");

                    // 1. Column definitions with auto widths
                    int colCount = table.Headers.Count;
                    double[] colWidths = new double[colCount];
                    for (int i = 0; i < colCount; i++)
                    {
                        colWidths[i] = Math.Max(100.0, table.Headers[i].Length * 8.5);
                    }

                    foreach (var row in table.Rows)
                    {
                        for (int i = 0; i < Math.Min(colCount, row.Count); i++)
                        {
                            if (row[i] != null)
                            {
                                string textVal = row[i].ToString();
                                colWidths[i] = Math.Max(colWidths[i], textVal.Length * 7.5);
                            }
                        }
                    }

                    for (int i = 0; i < colCount; i++)
                    {
                        writer.WriteLine($"   <Column ss:Width=\"{Math.Min(350.0, colWidths[i])}\"/>");
                    }

                    // 2. Title Row
                    if (!string.IsNullOrEmpty(table.Title))
                    {
                        writer.WriteLine("   <Row ss:Height=\"30\">");
                        int mergeAcross = Math.Max(0, colCount - 1);
                        writer.WriteLine($"    <Cell ss:MergeAcross=\"{mergeAcross}\" ss:StyleID=\"TitleStyle\"><Data ss:Type=\"String\">{EscapeXmlText(table.Title)}</Data></Cell>");
                        writer.WriteLine("   </Row>");
                        writer.WriteLine("   <Row ss:Height=\"10\"></Row>");
                    }

                    // 3. Header Row
                    writer.WriteLine("   <Row ss:Height=\"26\">");
                    for (int i = 0; i < colCount; i++)
                    {
                        string escapedHeader = EscapeXmlText(table.Headers[i]);
                        writer.WriteLine($"    <Cell ss:StyleID=\"Header\"><Data ss:Type=\"String\">{escapedHeader}</Data></Cell>");
                    }
                    writer.WriteLine("   </Row>");

                    // 4. Data Rows
                    for (int r = 0; r < table.Rows.Count; r++)
                    {
                        var row = table.Rows[r];
                        bool isTotalRow = false;
                        foreach (var cellVal in row)
                        {
                            if (cellVal != null)
                            {
                                string strVal = cellVal.ToString().Trim().ToLower();
                                if (strVal.StartsWith("итого") || strVal.StartsWith("всего") || strVal.StartsWith("total"))
                                {
                                    isTotalRow = true;
                                    break;
                                }
                            }
                        }

                        string styleAttr = isTotalRow ? " ss:StyleID=\"Summary\"" : ((r % 2 != 0) ? " ss:StyleID=\"Zebra\"" : "");
                        writer.WriteLine($"   <Row ss:Height=\"20\"{styleAttr}>");
                        for (int c = 0; c < colCount; c++)
                        {
                            object val = (c < row.Count) ? row[c] : null;
                            WriteCell(writer, val);
                        }
                        writer.WriteLine("   </Row>");
                    }

                    // 4. Summary Row (if sum columns defined)
                    if (table.Rows.Count > 0 && table.NumericSumColumns.Count > 0)
                    {
                        writer.WriteLine("   <Row ss:Height=\"24\" ss:StyleID=\"Summary\">");
                        for (int c = 0; c < colCount; c++)
                        {
                            if (c == 0)
                            {
                                writer.WriteLine("    <Cell><Data ss:Type=\"String\">ИТОГО:</Data></Cell>");
                            }
                            else if (table.NumericSumColumns.Contains(c))
                            {
                                // Write SUM formula in R1C1 notation
                                // Formula sums from R[-RowCount] (header is row 1) to R[-1] (last data row)
                                int rowOffset = table.Rows.Count;
                                writer.WriteLine($"    <Cell ss:Formula=\"=SUM(R[-{rowOffset}]C:R[-1]C)\"><Data ss:Type=\"Number\">0</Data></Cell>");
                            }
                            else
                            {
                                writer.WriteLine("    <Cell><Data ss:Type=\"String\"></Data></Cell>");
                            }
                        }
                        writer.WriteLine("   </Row>");
                    }

                    writer.WriteLine("  </Table>");
                    writer.WriteLine(" </Worksheet>");
                }

                writer.WriteLine("</Workbook>");
            }
        }

        private static void WriteCell(StreamWriter writer, object val)
        {
            if (val == null)
            {
                writer.WriteLine("    <Cell><Data ss:Type=\"String\"></Data></Cell>");
                return;
            }

            if (val is int || val is long || val is short || val is byte)
            {
                writer.WriteLine($"    <Cell><Data ss:Type=\"Number\">{val}</Data></Cell>");
            }
            else if (val is double d)
            {
                writer.WriteLine($"    <Cell><Data ss:Type=\"Number\">{d.ToString(CultureInfo.InvariantCulture)}</Data></Cell>");
            }
            else if (val is float f)
            {
                writer.WriteLine($"    <Cell><Data ss:Type=\"Number\">{f.ToString(CultureInfo.InvariantCulture)}</Data></Cell>");
            }
            else if (val is decimal dec)
            {
                writer.WriteLine($"    <Cell><Data ss:Type=\"Number\">{dec.ToString(CultureInfo.InvariantCulture)}</Data></Cell>");
            }
            else if (val is bool b)
            {
                writer.WriteLine($"    <Cell><Data ss:Type=\"String\">{(b ? "Да" : "Нет")}</Data></Cell>");
            }
            else
            {
                string text = val.ToString() ?? string.Empty;
                writer.WriteLine($"    <Cell><Data ss:Type=\"String\">{EscapeXmlText(text)}</Data></Cell>");
            }
        }

        private static string EscapeXmlText(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace("&", "&amp;")
                       .Replace("<", "&lt;")
                       .Replace(">", "&gt;");
        }

        private static string EscapeXmlAttribute(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace("&", "&amp;")
                       .Replace("<", "&lt;")
                       .Replace(">", "&gt;")
                       .Replace("\"", "&quot;")
                       .Replace("'", "&apos;");
        }
    }
}
