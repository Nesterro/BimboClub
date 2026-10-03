using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace BimboClub.Analogs.FastViewCut
{
    public static class FastViewCutUtils
    {
        /// <summary>
        /// Устанавливает рамку подрезки 2D вида по двум точкам с учетом локальной системы координат вида.
        /// Подходит для планов, разрезов, фасадов и чертежных видов.
        /// </summary>
        public static bool SetCropBoxByPoints(View view, XYZ worldPt1, XYZ worldPt2)
        {
            if (view == null) return false;
            if (view is ViewSheet || view is ViewSchedule) return false;

            try
            {
                BoundingBoxXYZ cropBox = view.CropBox;
                if (cropBox == null) return false;

                Transform invTransform = cropBox.Transform.Inverse;

                XYZ localPt1 = invTransform.OfPoint(worldPt1);
                XYZ localPt2 = invTransform.OfPoint(worldPt2);

                double minX = Math.Min(localPt1.X, localPt2.X);
                double maxX = Math.Max(localPt1.X, localPt2.X);
                double minY = Math.Min(localPt1.Y, localPt2.Y);
                double maxY = Math.Max(localPt1.Y, localPt2.Y);

                // Защита от нулевого прямоугольника
                if (maxX - minX < 0.01 || maxY - minY < 0.01) return false;

                cropBox.Min = new XYZ(minX, minY, cropBox.Min.Z);
                cropBox.Max = new XYZ(maxX, maxY, cropBox.Max.Z);

                view.CropBox = cropBox;
                view.CropBoxActive = true;
                view.CropBoxVisible = true;

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка установки рамки подрезки вида {view.Name}", ex);
                return false;
            }
        }

        /// <summary>
        /// Создает или обновляет 3D-вид с секущим кубом по прямоугольнику на плане этажа.
        /// </summary>
        public static View3D CreateOrUpdateSectionBox3D(Document doc, View activeView, XYZ worldPt1, XYZ worldPt2)
        {
            if (doc == null) return null;

            double minX = Math.Min(worldPt1.X, worldPt2.X);
            double maxX = Math.Max(worldPt1.X, worldPt2.X);
            double minY = Math.Min(worldPt1.Y, worldPt2.Y);
            double maxY = Math.Max(worldPt1.Y, worldPt2.Y);

            if (maxX - minX < 0.1 || maxY - minY < 0.1) return null;

            double minZ = 0;
            double maxZ = 15.0; // ~4.5 метра по умолчанию

            if (activeView is ViewPlan viewPlan && viewPlan.GenLevel != null)
            {
                double baseElevation = viewPlan.GenLevel.Elevation;

                var allLevels = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(l => l.Elevation)
                    .ToList();

                Level nextLevel = allLevels
                    .Where(l => l.Elevation > baseElevation + (100.0 / 304.8))
                    .OrderBy(l => l.Elevation)
                    .FirstOrDefault();

                double topElevation = nextLevel != null
                    ? nextLevel.Elevation
                    : baseElevation + (3300.0 / 304.8);

                // Запас по высоте: -200 мм снизу и +300 мм сверху
                minZ = baseElevation - (200.0 / 304.8);
                maxZ = topElevation + (300.0 / 304.8);
            }
            else
            {
                minZ = Math.Min(worldPt1.Z, worldPt2.Z) - 2.0;
                maxZ = Math.Max(worldPt1.Z, worldPt2.Z) + 12.0;
            }

            // Ищем подходящий 3D-вид или создаем новый
            string targetViewName = "{3D - Подрезка}";
            View3D target3D = new FilteredElementCollector(doc)
                .OfClass(typeof(View3D))
                .Cast<View3D>()
                .FirstOrDefault(v => !v.IsTemplate && (v.Name == targetViewName || v.Name == "{3D}"));

            if (target3D == null || target3D.IsTemplate)
            {
                // Находим тип изометрического 3D-вида
                ViewFamilyType vft3D = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .FirstOrDefault(x => x.ViewFamily == ViewFamily.ThreeDimensional);

                if (vft3D != null)
                {
                    target3D = View3D.CreateIsometric(doc, vft3D.Id);
                    try
                    {
                        target3D.Name = targetViewName;
                    }
                    catch
                    {
                        // Если имя занято, оставляем стандартное
                    }
                }
            }

            if (target3D != null)
            {
                BoundingBoxXYZ sBox = new BoundingBoxXYZ
                {
                    Min = new XYZ(minX, minY, minZ),
                    Max = new XYZ(maxX, maxY, maxZ)
                };

                target3D.IsSectionBoxActive = true;
                target3D.SetSectionBox(sBox);
            }

            return target3D;
        }
    }
}
