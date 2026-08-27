using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace BimboClub
{
    /// <summary>
    /// Исправляет кавычки-ёлочки « » и спецсимволы перед печатью/экспортом в PDF,
    /// предотвращая отображение дробей 1/4 и 1/2 в шрифтах ГОСТ и PDF-просмотрщиках.
    /// </summary>
    public static class QuoteSanitizer
    {
        public static string SanitizeGostString(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            // Замена кавычек-ёлочек « » и типографских кавычек на стандартные ASCII-кавычки "
            string result = input
                .Replace("«", "\"")
                .Replace("»", "\"")
                .Replace("“", "\"")
                .Replace("”", "\"")
                .Replace("„", "\"")
                .Replace("‟", "\"")
                .Replace("’", "'")
                .Replace("‘", "'")
                .Replace("°", "\u02DA");

            return result;
        }

        public static bool ContainsQuotesOrSpecial(string input)
        {
            if (string.IsNullOrEmpty(input)) return false;
            return input.Contains("«") || input.Contains("»") ||
                   input.Contains("“") || input.Contains("”") ||
                   input.Contains("„") || input.Contains("‟") ||
                   input.Contains("’") || input.Contains("‘") ||
                   input.Contains("°");
        }

        public static void SanitizeDocument(
            Document doc,
            IEnumerable<ElementId> sheetOrViewIds,
            out Dictionary<ElementId, string> originalTextNotes,
            out Dictionary<ElementId, Dictionary<string, string>> originalParams)
        {
            originalTextNotes = new Dictionary<ElementId, string>();
            originalParams = new Dictionary<ElementId, Dictionary<string, string>>();

            if (doc == null) return;

            try
            {
                // 1. Все текстовые примечания (TextNote)
                var textNotes = new FilteredElementCollector(doc)
                    .OfClass(typeof(TextNote))
                    .Cast<TextNote>()
                    .ToList();

                foreach (var tn in textNotes)
                {
                    string text = tn.Text;
                    if (ContainsQuotesOrSpecial(text))
                    {
                        originalTextNotes[tn.Id] = text;
                        tn.Text = SanitizeGostString(text);
                    }
                }

                // 2. Рамки основных надписей (OST_TitleBlocks)
                var titleBlocks = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_TitleBlocks)
                    .WhereElementIsNotElementType()
                    .ToList();

                foreach (Element tb in titleBlocks)
                {
                    SanitizeElementParameters(tb, originalParams);
                }

                // 3. Выбранные листы и размещенные на них виды/видовые экраны
                var targetIds = new HashSet<ElementId>(sheetOrViewIds ?? Enumerable.Empty<ElementId>());
                foreach (ElementId id in targetIds)
                {
                    Element el = doc.GetElement(id);
                    if (el != null)
                    {
                        SanitizeElementParameters(el, originalParams);

                        if (el is ViewSheet vs)
                        {
                            var viewports = new FilteredElementCollector(doc, vs.Id)
                                .OfClass(typeof(Viewport))
                                .Cast<Viewport>()
                                .ToList();

                            foreach (var vp in viewports)
                            {
                                SanitizeElementParameters(vp, originalParams);
                                Element v = doc.GetElement(vp.ViewId);
                                if (v != null)
                                {
                                    SanitizeElementParameters(v, originalParams);
                                }
                            }
                        }
                    }
                }

                // 4. Информация о проекте (Project Information)
                if (doc.ProjectInformation != null)
                {
                    SanitizeElementParameters(doc.ProjectInformation, originalParams);
                }

                // 5. Аннотации и марки
                var annotations = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilyInstance))
                    .WhereElementIsNotElementType()
                    .Where(e => e.Category != null && e.Category.Id.IntegerValue == (int)BuiltInCategory.OST_GenericAnnotation)
                    .ToList();

                foreach (Element ann in annotations)
                {
                    SanitizeElementParameters(ann, originalParams);
                }

                var tags = new FilteredElementCollector(doc)
                    .OfClass(typeof(IndependentTag))
                    .WhereElementIsNotElementType()
                    .ToList();

                foreach (Element tag in tags)
                {
                    SanitizeElementParameters(tag, originalParams);
                }

                // 6. Размеры с переопределениями (Dimension)
                var dims = new FilteredElementCollector(doc)
                    .OfClass(typeof(Dimension))
                    .Cast<Dimension>()
                    .ToList();

                foreach (Dimension dim in dims)
                {
                    try
                    {
                        if (ContainsQuotesOrSpecial(dim.ValueOverride))
                        {
                            SaveAndSetParam(dim, "__ValueOverride", dim.ValueOverride, originalParams);
                            dim.ValueOverride = SanitizeGostString(dim.ValueOverride);
                        }
                        if (ContainsQuotesOrSpecial(dim.Prefix))
                        {
                            SaveAndSetParam(dim, "__Prefix", dim.Prefix, originalParams);
                            dim.Prefix = SanitizeGostString(dim.Prefix);
                        }
                        if (ContainsQuotesOrSpecial(dim.Suffix))
                        {
                            SaveAndSetParam(dim, "__Suffix", dim.Suffix, originalParams);
                            dim.Suffix = SanitizeGostString(dim.Suffix);
                        }
                        if (ContainsQuotesOrSpecial(dim.Above))
                        {
                            SaveAndSetParam(dim, "__Above", dim.Above, originalParams);
                            dim.Above = SanitizeGostString(dim.Above);
                        }
                        if (ContainsQuotesOrSpecial(dim.Below))
                        {
                            SaveAndSetParam(dim, "__Below", dim.Below, originalParams);
                            dim.Below = SanitizeGostString(dim.Below);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static void SanitizeElementParameters(Element el, Dictionary<ElementId, Dictionary<string, string>> originalParams)
        {
            if (el == null) return;
            foreach (Parameter p in el.Parameters)
            {
                if (p.IsReadOnly || p.StorageType != StorageType.String) continue;
                string val = p.AsString();
                if (ContainsQuotesOrSpecial(val))
                {
                    string paramKey = p.Definition?.Name ?? p.Id.ToString();
                    SaveAndSetParam(el, paramKey, val, originalParams);
                    try
                    {
                        p.Set(SanitizeGostString(val));
                    }
                    catch { }
                }
            }
        }

        private static void SaveAndSetParam(Element el, string paramKey, string originalVal, Dictionary<ElementId, Dictionary<string, string>> originalParams)
        {
            if (!originalParams.TryGetValue(el.Id, out var dict))
            {
                dict = new Dictionary<string, string>();
                originalParams[el.Id] = dict;
            }
            if (!dict.ContainsKey(paramKey))
            {
                dict[paramKey] = originalVal;
            }
        }

        public static void RestoreDocument(
            Document doc,
            Dictionary<ElementId, string> originalTextNotes,
            Dictionary<ElementId, Dictionary<string, string>> originalParams)
        {
            if (doc == null) return;

            try
            {
                if (originalTextNotes != null)
                {
                    foreach (var kvp in originalTextNotes)
                    {
                        TextNote tn = doc.GetElement(kvp.Key) as TextNote;
                        if (tn != null)
                        {
                            try { tn.Text = kvp.Value; } catch { }
                        }
                    }
                }

                if (originalParams != null)
                {
                    foreach (var elKvp in originalParams)
                    {
                        Element el = doc.GetElement(elKvp.Key);
                        if (el == null) continue;

                        foreach (var pKvp in elKvp.Value)
                        {
                            if (pKvp.Key.StartsWith("__") && el is Dimension dim)
                            {
                                try
                                {
                                    if (pKvp.Key == "__ValueOverride") dim.ValueOverride = pKvp.Value;
                                    else if (pKvp.Key == "__Prefix") dim.Prefix = pKvp.Value;
                                    else if (pKvp.Key == "__Suffix") dim.Suffix = pKvp.Value;
                                    else if (pKvp.Key == "__Above") dim.Above = pKvp.Value;
                                    else if (pKvp.Key == "__Below") dim.Below = pKvp.Value;
                                }
                                catch { }
                                continue;
                            }

                            foreach (Parameter p in el.Parameters)
                            {
                                if (p.IsReadOnly || p.StorageType != StorageType.String) continue;
                                string paramName = p.Definition?.Name ?? p.Id.ToString();
                                if (paramName == pKvp.Key)
                                {
                                    try { p.Set(pKvp.Value); } catch { }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
        }
    }
}
