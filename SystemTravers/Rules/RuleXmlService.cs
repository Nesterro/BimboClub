using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Autodesk.Revit.DB;

namespace BimboClub.Rules
{
    public class RuleXmlService
    {
        public static RuleSet LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Файл правил не найден: {filePath}");
            }

            var doc = XDocument.Load(filePath);
            var root = doc.Root;
            if (root == null || root.Name.LocalName != "rules")
            {
                throw new InvalidOperationException("Неверный формат XML-файла правил. Корневой элемент должен быть <rules>.");
            }

            var ruleSet = new RuleSet
            {
                Comment = root.Element("comment")?.Value ?? string.Empty
            };

            int defaultIndex = 1;
            foreach (var ruleEl in root.Elements("rule"))
            {
                var ruleItem = new RuleItem();

                // Index and Enable attributes
                string? indexAttr = ruleEl.Attribute("index")?.Value;
                ruleItem.Index = int.TryParse(indexAttr, out int parsedIdx) ? parsedIdx : defaultIndex++;

                string? enableAttr = ruleEl.Attribute("enable")?.Value;
                ruleItem.IsEnabled = enableAttr == null || bool.Parse(enableAttr);

                ruleItem.Comment = ruleEl.Element("comment")?.Value ?? string.Empty;
                ruleItem.Name = !string.IsNullOrEmpty(ruleItem.Comment) ? ruleItem.Comment : $"Правило {ruleItem.Index}";

                // Action & Target Param
                var actionEl = ruleEl.Element("action");
                if (actionEl != null)
                {
                    var targetParamEl = actionEl.Element("paramId");
                    if (targetParamEl != null)
                    {
                        ruleItem.TargetParam = ParseParamIdentifier(targetParamEl);
                    }

                    // Fragments
                    var fragmentsEl = actionEl.Element("fragments");
                    if (fragmentsEl != null)
                    {
                        foreach (var fragEl in fragmentsEl.Elements("fragment"))
                        {
                            var frag = ParseFragment(fragEl);
                            if (frag != null)
                            {
                                ruleItem.Fragments.Add(frag);
                            }
                        }
                    }
                }

                // Filters
                var filtersEl = ruleEl.Element("filters");
                if (filtersEl != null)
                {
                    foreach (var filterEl in filtersEl.Elements("filter"))
                    {
                        var filter = ParseFilter(filterEl);
                        if (filter != null)
                        {
                            ruleItem.Filters.Add(filter);
                        }
                    }
                }

                ruleSet.Rules.Add(ruleItem);
            }

            return ruleSet;
        }

        public static void SaveToFile(RuleSet ruleSet, string filePath)
        {
            var root = new XElement("rules",
                new XElement("comment", ruleSet.Comment ?? string.Empty)
            );

            for (int i = 0; i < ruleSet.Rules.Count; i++)
            {
                var item = ruleSet.Rules[i];
                var ruleEl = new XElement("rule",
                    new XAttribute("index", i + 1),
                    new XAttribute("enable", item.IsEnabled.ToString().ToLowerInvariant()),
                    new XElement("comment", item.Comment ?? item.Name)
                );

                // Action
                var actionEl = new XElement("action",
                    new XAttribute("actionType", "SetParamValue"),
                    new XElement("errorReport"),
                    SerializeParamIdentifier(item.TargetParam)
                );

                // Fragments
                var fragmentsEl = new XElement("fragments");
                foreach (var frag in item.Fragments)
                {
                    fragmentsEl.Add(SerializeFragment(frag));
                }
                actionEl.Add(fragmentsEl);
                ruleEl.Add(actionEl);

                // Filters
                var filtersEl = new XElement("filters");
                foreach (var filter in item.Filters)
                {
                    filtersEl.Add(SerializeFilter(filter));
                }
                ruleEl.Add(filtersEl);

                root.Add(ruleEl);
            }

            var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
            doc.Save(filePath);
        }

        private static ParamIdentifier ParseParamIdentifier(XElement el)
        {
            var param = new ParamIdentifier
            {
                Name = el.Element("name")?.Value ?? string.Empty
            };

            string? guidStr = el.Element("guid")?.Value;
            if (!string.IsNullOrEmpty(guidStr) && Guid.TryParse(guidStr, out Guid g))
            {
                param.Guid = g;
            }

            string? bipStr = el.Element("bip")?.Value;
            if (!string.IsNullOrEmpty(bipStr) && int.TryParse(bipStr, out int bip))
            {
                param.BuiltInParamId = bip;
            }

            return param;
        }

        private static XElement SerializeParamIdentifier(ParamIdentifier param)
        {
            var el = new XElement("paramId",
                new XElement("name", param.Name ?? string.Empty)
            );

            if (param.Guid.HasValue)
            {
                el.Add(new XElement("guid", param.Guid.Value.ToString()));
            }

            if (param.BuiltInParamId.HasValue)
            {
                el.Add(new XElement("bip", param.BuiltInParamId.Value.ToString()));
            }

            return el;
        }

        private static RuleFragment? ParseFragment(XElement el)
        {
            string type = el.Attribute("type")?.Value ?? "String";
            var frag = new RuleFragment();

            if (type == "DuctThickness")
            {
                frag.FragmentType = "DuctThickness";
                var table = new DuctThicknessTable();

                var rectEl = el.Element("rectangle");
                if (rectEl != null)
                {
                    foreach (var sizeEl in rectEl.Elements("ductSize"))
                    {
                        table.Rectangle.Add(ParseDuctSizeRow(sizeEl));
                    }
                }

                var roundEl = el.Element("round");
                if (roundEl != null)
                {
                    foreach (var sizeEl in roundEl.Elements("ductSize"))
                    {
                        table.Round.Add(ParseDuctSizeRow(sizeEl));
                    }
                }

                frag.DuctThicknessData = table;
                return frag;
            }

            if (type == "Param" || type == "Parameter")
            {
                frag.FragmentType = "Parameter";
                var paramEl = el.Element("paramId");
                if (paramEl != null)
                {
                    frag.ParamId = ParseParamIdentifier(paramEl);
                }

                string? modeStr = el.Element("mode")?.Value;
                if (!string.IsNullOrEmpty(modeStr) && Enum.TryParse(modeStr, true, out ParamGettingMode mode))
                {
                    frag.ParamMode = mode;
                }

                string? convStr = el.Element("convertToMillimeters")?.Value;
                frag.ConvertToMillimeters = convStr != null && bool.Parse(convStr);

                string? roundStr = el.Element("roundDecimal")?.Value;
                if (!string.IsNullOrEmpty(roundStr) && int.TryParse(roundStr, out int rd))
                {
                    frag.RoundDecimal = rd;
                }

                return frag;
            }

            // Fallback to StaticText / String
            frag.FragmentType = "StaticText";
            frag.StaticText = el.Element("value")?.Value ?? el.Value ?? string.Empty;
            return frag;
        }

        private static DuctSizeRow ParseDuctSizeRow(XElement el)
        {
            var row = new DuctSizeRow();
            string? minMm = el.Element("min")?.Attribute("mm")?.Value;
            if (!string.IsNullOrEmpty(minMm) && double.TryParse(minMm, NumberStyles.Any, CultureInfo.InvariantCulture, out double min))
            {
                row.MinMm = min;
            }

            string? maxMm = el.Element("max")?.Attribute("mm")?.Value;
            if (!string.IsNullOrEmpty(maxMm))
            {
                if (maxMm.Equals("MaxValue", StringComparison.OrdinalIgnoreCase))
                    row.MaxMm = double.MaxValue;
                else if (double.TryParse(maxMm, NumberStyles.Any, CultureInfo.InvariantCulture, out double max))
                    row.MaxMm = max;
            }

            string? thMm = el.Element("thickness")?.Attribute("mm")?.Value;
            if (!string.IsNullOrEmpty(thMm) && double.TryParse(thMm, NumberStyles.Any, CultureInfo.InvariantCulture, out double th))
            {
                row.ThicknessMm = th;
            }

            return row;
        }

        private static XElement SerializeFragment(RuleFragment frag)
        {
            if (frag.FragmentType == "DuctThickness" && frag.DuctThicknessData != null)
            {
                var el = new XElement("fragment", new XAttribute("type", "DuctThickness"));
                var rectEl = new XElement("rectangle");
                foreach (var r in frag.DuctThicknessData.Rectangle)
                {
                    rectEl.Add(new XElement("ductSize",
                        new XElement("min", new XAttribute("mm", r.MinMm.ToString(CultureInfo.InvariantCulture)), (r.MinMm / 304.8).ToString(CultureInfo.InvariantCulture)),
                        new XElement("max", new XAttribute("mm", r.MaxMm == double.MaxValue ? "MaxValue" : r.MaxMm.ToString(CultureInfo.InvariantCulture)), (r.MaxMm == double.MaxValue ? double.MaxValue : r.MaxMm / 304.8).ToString(CultureInfo.InvariantCulture)),
                        new XElement("thickness", new XAttribute("mm", r.ThicknessMm.ToString(CultureInfo.InvariantCulture)), (r.ThicknessMm / 304.8).ToString(CultureInfo.InvariantCulture))
                    ));
                }
                el.Add(rectEl);

                var roundEl = new XElement("round");
                foreach (var r in frag.DuctThicknessData.Round)
                {
                    roundEl.Add(new XElement("ductSize",
                        new XElement("min", new XAttribute("mm", r.MinMm.ToString(CultureInfo.InvariantCulture)), (r.MinMm / 304.8).ToString(CultureInfo.InvariantCulture)),
                        new XElement("max", new XAttribute("mm", r.MaxMm == double.MaxValue ? "MaxValue" : r.MaxMm.ToString(CultureInfo.InvariantCulture)), (r.MaxMm == double.MaxValue ? double.MaxValue : r.MaxMm / 304.8).ToString(CultureInfo.InvariantCulture)),
                        new XElement("thickness", new XAttribute("mm", r.ThicknessMm.ToString(CultureInfo.InvariantCulture)), (r.ThicknessMm / 304.8).ToString(CultureInfo.InvariantCulture))
                    ));
                }
                el.Add(roundEl);
                el.Add(new XElement("convertToMillimeters", "true"));
                return el;
            }

            if (frag.FragmentType == "Parameter")
            {
                var el = new XElement("fragment", new XAttribute("type", "Param"));
                el.Add(SerializeParamIdentifier(frag.ParamId));
                el.Add(new XElement("mode", frag.ParamMode.ToString()));
                el.Add(new XElement("convertToMillimeters", frag.ConvertToMillimeters.ToString().ToLowerInvariant()));
                if (frag.RoundDecimal.HasValue)
                {
                    el.Add(new XElement("roundDecimal", frag.RoundDecimal.Value));
                }
                return el;
            }

            // Static text
            return new XElement("fragment",
                new XAttribute("type", "String"),
                new XElement("value", frag.StaticText ?? string.Empty)
            );
        }

        private static RuleFilter? ParseFilter(XElement el)
        {
            string filterType = el.Attribute("filterType")?.Value ?? "ByCategories";
            var filter = new RuleFilter
            {
                FilterType = filterType
            };

            string? invAttr = el.Attribute("inverted")?.Value;
            filter.Inverted = invAttr != null && bool.Parse(invAttr);

            if (filterType == "ByCategories")
            {
                var catsEl = el.Element("categories");
                if (catsEl != null)
                {
                    foreach (var cat in catsEl.Elements("category"))
                    {
                        if (!string.IsNullOrWhiteSpace(cat.Value))
                        {
                            filter.Categories.Add(cat.Value.Trim());
                        }
                    }
                }
                return filter;
            }

            if (filterType == "ElementIsElementType")
            {
                return filter;
            }

            if (filterType == "ByParameterValue" || filterType == "ByInsulationParameterValue")
            {
                var paramEl = el.Element("paramId");
                if (paramEl != null)
                {
                    filter.ParamId = ParseParamIdentifier(paramEl);
                }

                string? matchStr = el.Element("matchType")?.Value;
                if (!string.IsNullOrEmpty(matchStr) && Enum.TryParse(matchStr, true, out FilterMatchType mt))
                {
                    filter.MatchType = mt;
                }

                filter.Value = el.Element("value")?.Value ?? string.Empty;
                return filter;
            }

            return filter;
        }

        private static XElement SerializeFilter(RuleFilter filter)
        {
            var el = new XElement("filter", new XAttribute("filterType", filter.FilterType));
            if (filter.Inverted)
            {
                el.Add(new XAttribute("inverted", "true"));
            }

            if (filter.FilterType == "ByCategories")
            {
                var catsEl = new XElement("categories");
                foreach (var cat in filter.Categories)
                {
                    catsEl.Add(new XElement("category", cat));
                }
                el.Add(catsEl);
                return el;
            }

            if (filter.FilterType == "ElementIsElementType")
            {
                return el;
            }

            if (filter.FilterType == "ByParameterValue" || filter.FilterType == "ByInsulationParameterValue")
            {
                el.Add(SerializeParamIdentifier(filter.ParamId));
                el.Add(new XElement("storageType", filter.ParamId.StorageType.ToString()));
                el.Add(new XElement("matchType", filter.MatchType.ToString()));
                el.Add(new XElement("value", filter.Value ?? string.Empty));
                return el;
            }

            return el;
        }
    }
}
