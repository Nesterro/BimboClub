using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace BimboClub
{
    internal static class OpeningRoundingService
    {
        private const double Tolerance = 1e-6; // feet, about 0.0003 mm

        internal static Level FindLevel(Document doc, double z, ElementId preferred = null)
        {
            if (preferred != null && doc.GetElement(preferred) is Level selected) return selected;
            var levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>()
                .OrderBy(level => level.ProjectElevation).ToList();
            return levels.LastOrDefault(level => level.ProjectElevation <= z + Tolerance)
                ?? levels.FirstOrDefault() ?? throw new InvalidOperationException("В проекте нет уровня для размещения отверстия.");
        }

        internal static void Apply(Document doc, FamilyInstance opening, string widthName, string verticalSizeName,
            string otherSizeName, OpeningRoundingOptions options, Level referenceLevel)
        {
            if (options == null || (options.SizeStepMm == 0 && options.BottomStepMm == 0)) return;
            double sizeStep = options.SizeStepMm / 304.8;
            double bottomStep = options.BottomStepMm / 304.8;
            doc.Regenerate();
            var names = new[] { widthName, verticalSizeName, otherSizeName }.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();
            var parameters = names.ToDictionary(name => name, name => GetLength(opening, name));
            double originalBottom = 0, originalTop = 0, targetBottom = 0;
            if (bottomStep > 0)
            {
                GetBounds(opening, out originalBottom, out originalTop);
                var level = doc.GetElement(opening.LevelId) as Level ?? referenceLevel;
                if (level == null) throw new InvalidOperationException("Не определён уровень отверстия для округления низа.");
                targetBottom = level.ProjectElevation + OpeningRoundingOptions.Down(originalBottom - level.ProjectElevation, bottomStep, Tolerance);
                if (string.IsNullOrWhiteSpace(verticalSizeName) || !parameters.ContainsKey(verticalSizeName))
                    throw new InvalidOperationException("Для округления низа выберите параметр вертикальной высоты отверстия.");
            }

            double bottomDrop = originalBottom - targetBottom;
            var expectedValues = new Dictionary<string, double>();
            foreach (var pair in parameters)
            {
                double value = pair.Value.AsDouble();
                if (pair.Key == verticalSizeName && bottomDrop > Tolerance)
                    value += bottomDrop;
                expectedValues.Add(pair.Key, OpeningRoundingOptions.Up(value, sizeStep, Tolerance));
            }
            foreach (var pair in parameters)
            {
                double expected = expectedValues[pair.Key];
                if (Math.Abs(pair.Value.AsDouble() - expected) > Tolerance && !pair.Value.Set(expected))
                    throw new InvalidOperationException($"Не удалось записать размер «{pair.Key}».");
            }
            // Revit can defer dimension changes until regeneration, even when Set returns true.
            doc.Regenerate();
            if (bottomStep > 0)
            {
                GetBounds(opening, out double currentBottom, out _);
                double shift = targetBottom - currentBottom;
                if (Math.Abs(shift) > Tolerance)
                    ElementTransformUtils.MoveElement(doc, opening.Id, new XYZ(0, 0, shift));
                doc.Regenerate();
                GetBounds(opening, out double finalBottom, out double finalTop);
                if (Math.Abs(finalBottom - targetBottom) > Tolerance || finalTop < originalTop - Tolerance)
                    throw new InvalidOperationException("Семейство не сохранило требуемые границы отверстия. Проверьте вертикальный параметр высоты и ограничения размещения.");
            }
            foreach (var expected in expectedValues)
            {
                double actual = GetLength(opening, expected.Key).AsDouble();
                if (Math.Abs(actual - expected.Value) > Tolerance)
                    throw new InvalidOperationException($"Размер «{expected.Key}» не сохранил округление.");
            }
        }

        private static Parameter GetLength(FamilyInstance opening, string name)
        {
            var matches = opening.GetParameters(name).Where(p => p.StorageType == StorageType.Double).ToList();
            if (matches.Count != 1 || matches[0].IsReadOnly || !matches[0].HasValue || matches[0].Definition.GetDataType() != SpecTypeId.Length)
                throw new InvalidOperationException($"Параметр «{name}» должен быть однозначным доступным для записи размером экземпляра.");
            return matches[0];
        }

        private static void GetBounds(Element element, out double bottom, out double top)
        {
            var points = new List<XYZ>();
            Collect(element.get_Geometry(new Options { DetailLevel = ViewDetailLevel.Fine }), points);
            if (points.Count == 0) throw new InvalidOperationException("Не удалось определить фактический низ отверстия по геометрии семейства.");
            bottom = points.Min(p => p.Z); top = points.Max(p => p.Z);
        }

        private static void Collect(GeometryElement geometry, List<XYZ> points)
        {
            if (geometry == null) return;
            foreach (GeometryObject item in geometry)
            {
                if (item is GeometryInstance instance) Collect(instance.GetInstanceGeometry(), points);
                else if (item is Solid solid && solid.Volume > 1e-12)
                    foreach (Face face in solid.Faces) points.AddRange(face.Triangulate().Vertices);
                else if (item is Mesh mesh) points.AddRange(mesh.Vertices);
            }
        }
    }
}
