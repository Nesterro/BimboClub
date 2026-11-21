using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using HostKind = DuctWallOpenings.OpeningsRunner.HostKind;

namespace DuctWallOpenings
{
    internal static class OpeningMergeBatch
    {
        private const double Tolerance = 1e-6;

        internal static List<FamilyInstance> GetOpenings(Document doc, HostKind kind, bool currentViewOnly, ElementId typeId = null)
        {
            View activeView = doc.ActiveView;
            if (currentViewOnly && (activeView == null || !FilteredElementCollector.IsViewValidForElementIteration(doc, activeView.Id)))
                return new List<FamilyInstance>();

            using (var collector = currentViewOnly ? new FilteredElementCollector(doc, activeView.Id) : new FilteredElementCollector(doc))
            {
                return collector.OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>()
                    .Where(instance => typeId == null || instance.GetTypeId() == typeId)
                    .Where(instance => instance.Location is LocationPoint && MatchesHostKind(instance, kind))
                    .ToList();
            }
        }

        private static bool MatchesHostKind(FamilyInstance instance, HostKind kind)
        {
            Element host = instance.Host;
            Reference face = instance.HostFace;
            if (face != null && face.LinkedElementId != ElementId.InvalidElementId)
            {
                var link = instance.Document.GetElement(face.ElementId) as RevitLinkInstance;
                host = link?.GetLinkDocument()?.GetElement(face.LinkedElementId) ?? host;
            }

            ElementId category = host?.Category?.Id;
            if (category == new ElementId(BuiltInCategory.OST_Walls)) return kind == HostKind.Wall;
            if (category == new ElementId(BuiltInCategory.OST_Floors) ||
                category == new ElementId(BuiltInCategory.OST_Ceilings) ||
                category == new ElementId(BuiltInCategory.OST_StructuralFoundation))
                return kind == HostKind.Floor;

            string name = (instance.Symbol.Family.Name + " " + instance.Symbol.Name).ToLowerInvariant();
            bool wall = name.Contains("стен") || name.Contains("wall");
            bool floor = name.Contains("перекрыт") || name.Contains("потол") || name.Contains("floor") ||
                name.Contains("slab") || name.Contains("ceiling") ||
                Regex.IsMatch(name, @"(^|[^\p{L}])пол(?:а|у|е|ы|ом|ов|ам|ами|ах)?(?=$|[^\p{L}])");

            // Level-based wall and floor openings can share the same facing orientation.
            // Allow ambiguous types to be explicitly selected in either command.
            return wall == floor || (kind == HostKind.Wall ? wall : floor);
        }

        internal static List<List<FamilyInstance>> FindGroups(List<FamilyInstance> instances, HostKind kind, double maxGap)
        {
            var openings = instances.Select(instance => new Opening(instance, kind))
                .Where(opening => opening.Points.Count > 0).ToList();
            return OpeningsRunner.GetConnectedComponents(openings, (first, second) => AreClose(first, second, maxGap))
                .Where(group => group.Count > 1)
                .Select(group => group.Select(opening => opening.Instance).ToList())
                .ToList();
        }

        private static bool AreClose(Opening first, Opening second, double maxGap)
        {
            if (first.HostId != ElementId.InvalidElementId && second.HostId != ElementId.InvalidElementId &&
                (first.HostId != second.HostId || first.LinkedHostId != second.LinkedHostId))
                return false;

            if (Math.Abs(first.Normal.DotProduct(second.Normal)) < 0.99 ||
                ProjectedGap(first.Points, second.Points, first.Normal) > Tolerance)
                return false;

            double along = ProjectedGap(first.Points, second.Points, first.Along);
            double across = ProjectedGap(first.Points, second.Points, first.Normal.CrossProduct(first.Along));
            return Math.Sqrt(along * along + across * across) <= maxGap + Tolerance;
        }

        private static double ProjectedGap(List<XYZ> first, List<XYZ> second, XYZ axis)
        {
            var firstRange = ProjectionRange(first, axis);
            var secondRange = ProjectionRange(second, axis);
            return Math.Max(0, Math.Max(firstRange.Min - secondRange.Max, secondRange.Min - firstRange.Max));
        }

        private static (double Min, double Max) ProjectionRange(List<XYZ> points, XYZ axis)
        {
            double min = double.PositiveInfinity;
            double max = double.NegativeInfinity;
            foreach (XYZ point in points)
            {
                double projection = point.DotProduct(axis);
                min = Math.Min(min, projection);
                max = Math.Max(max, projection);
            }
            return (min, max);
        }

        private static XYZ HorizontalAxis(XYZ direction, XYZ fallback)
        {
            var horizontal = new XYZ(direction.X, direction.Y, 0);
            return horizontal.GetLength() > Tolerance ? horizontal.Normalize() : fallback;
        }

        private sealed class Opening
        {
            internal FamilyInstance Instance { get; }
            internal List<XYZ> Points { get; }
            internal XYZ Normal { get; }
            internal XYZ Along { get; }
            internal ElementId HostId { get; }
            internal ElementId LinkedHostId { get; }

            internal Opening(FamilyInstance instance, HostKind kind)
            {
                Instance = instance;
                Points = OpeningsRunner.GetOpeningGeometryPoints(instance);
                Reference face = instance.HostFace;
                HostId = instance.Host?.Id ?? face?.ElementId ?? ElementId.InvalidElementId;
                LinkedHostId = face?.LinkedElementId ?? ElementId.InvalidElementId;
                Normal = kind == HostKind.Wall ? HorizontalAxis(instance.FacingOrientation, XYZ.BasisY) : XYZ.BasisZ;
                Along = kind == HostKind.Wall ? XYZ.BasisZ.CrossProduct(Normal) : HorizontalAxis(instance.HandOrientation, XYZ.BasisX);
            }
        }
    }
}
