using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BimboClub
{
    [Transaction(TransactionMode.Manual)]
    public class PlacementCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                Logger.Log("Запуск команды Расстановка элементов по сети (Network Placer).");

                // 1. Собираем все типоразмеры семейств в проекте
                FilteredElementCollector symbolCollector = new FilteredElementCollector(doc);
                List<FamilySymbol> allSymbols = symbolCollector
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .Where(s => s.Family != null && s.Family.IsEditable) // Только загружаемые семейства
                    .OrderBy(s => s.Family.Name)
                    .ThenBy(s => s.Name)
                    .ToList();

                if (allSymbols.Count == 0)
                {
                    TaskDialog.Show("Внимание", "В проекте не найдено загружаемых семейств для расстановки.");
                    return Result.Cancelled;
                }

                List<SymbolItem> symbolsList = allSymbols
                    .Select(s => new SymbolItem { DisplayName = $"{s.Family.Name} : {s.Name}", RawSymbol = s })
                    .ToList();

                // 2. Открываем окно в цикле для поддержки выбора элементов на чертеже без крашей
                List<Autodesk.Revit.DB.Reference> selectedRefs = new List<Autodesk.Revit.DB.Reference>();
                PlacementState savedState = null;
                FamilySymbol selectedSymbol = null;
                bool processDucts = true;
                bool processPipes = true;
                bool processTrays = true;
                bool onlyCurrentView = true;
                double spacingMm = 1500;
                double hOffsetMm = 0;
                double vOffsetMm = 0;
                int orientationFilterIndex = 0;

                bool showWindow = true;
                while (showWindow)
                {
                    PlacementWindow window = new PlacementWindow(uidoc, symbolsList);
                    var helper = new System.Windows.Interop.WindowInteropHelper(window);
                    helper.Owner = uiapp.MainWindowHandle;

                    if (savedState != null)
                    {
                        window.LoadState(savedState);
                    }
                    window.SelectedRefs.Clear();
                    window.SelectedRefs.AddRange(selectedRefs);
                    window.SetSelectedCount(selectedRefs.Count);

                    bool? dialogResult = window.ShowDialog();

                    if (dialogResult == true && window.IsApplied)
                    {
                        selectedSymbol = window.SelectedSymbol as FamilySymbol;
                        processDucts = window.ProcessDucts;
                        processPipes = window.ProcessPipes;
                        processTrays = window.ProcessTrays;
                        onlyCurrentView = window.OnlyCurrentView;
                        spacingMm = window.SpacingMm;
                        hOffsetMm = window.HOffsetMm;
                        vOffsetMm = window.VOffsetMm;
                        orientationFilterIndex = window.OrientationFilterIndex;
                        showWindow = false;
                    }
                    else if (window.SelectionRequested)
                    {
                        // Сохраняем текущее состояние перед закрытием для выбора
                        savedState = window.SaveState();

                        try
                        {
                            IList<Reference> refs = uidoc.Selection.PickObjects(
                                Autodesk.Revit.UI.Selection.ObjectType.Element,
                                "Выберите элементы инженерных сетей для расстановки и нажмите Готово (Finish)"
                            );
                            if (refs != null)
                            {
                                selectedRefs = refs.ToList();
                            }
                        }
                        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                        {
                            // Оставляем прошлый выбор при отмене
                        }
                        // Цикл продолжится, окно откроется снова
                    }
                    else
                    {
                        // Пользователь нажал Отмена или закрыл окно крестиком
                        return Result.Cancelled;
                    }
                }

                if (selectedSymbol == null) return Result.Cancelled;

                // 3. Собираем кривые сетей на основе выбора
                List<Element> curveElements = new List<Element>();

                if (selectedRefs.Count > 0)
                {
                    foreach (var r in selectedRefs)
                    {
                        Element elem = doc.GetElement(r.ElementId);
                        if (elem == null) continue;

                        if (elem is Autodesk.Revit.DB.Mechanical.Duct && processDucts)
                        {
                            curveElements.Add(elem);
                        }
                        else if (elem is Autodesk.Revit.DB.Plumbing.Pipe && processPipes)
                        {
                            curveElements.Add(elem);
                        }
                        else if (elem is Autodesk.Revit.DB.Electrical.CableTray && processTrays)
                        {
                            curveElements.Add(elem);
                        }
                    }
                }
                else
                {
                    if (processDucts)
                    {
                        FilteredElementCollector ductColl = onlyCurrentView 
                            ? new FilteredElementCollector(doc, doc.ActiveView.Id) 
                            : new FilteredElementCollector(doc);
                        curveElements.AddRange(ductColl.OfClass(typeof(Autodesk.Revit.DB.Mechanical.Duct)).Cast<Element>());
                    }
                    if (processPipes)
                    {
                        FilteredElementCollector pipeColl = onlyCurrentView 
                            ? new FilteredElementCollector(doc, doc.ActiveView.Id) 
                            : new FilteredElementCollector(doc);
                        curveElements.AddRange(pipeColl.OfClass(typeof(Autodesk.Revit.DB.Plumbing.Pipe)).Cast<Element>());
                    }
                    if (processTrays)
                    {
                        FilteredElementCollector trayColl = onlyCurrentView 
                            ? new FilteredElementCollector(doc, doc.ActiveView.Id) 
                            : new FilteredElementCollector(doc);
                        curveElements.AddRange(trayColl.OfClass(typeof(Autodesk.Revit.DB.Electrical.CableTray)).Cast<Element>());
                    }
                }

                if (curveElements.Count == 0)
                {
                    TaskDialog.Show("Внимание", "Не найдено элементов выбранных сетей для расстановки.");
                    return Result.Cancelled;
                }

                double spacingFeet = spacingMm / 304.8;
                double hOffsetFeet = hOffsetMm / 304.8;
                double vOffsetFeet = vOffsetMm / 304.8;

                    int placedCount = 0;

                    // 4. Расстановка в транзакции
                    using (Transaction tx = new Transaction(doc, "Расстановка элементов по сети BimboClub"))
                    {
                        tx.Start();

                        // Активируем символ
                        if (!selectedSymbol.IsActive)
                        {
                            selectedSymbol.Activate();
                            doc.Regenerate();
                        }

                        foreach (Element elem in curveElements)
                        {
                            LocationCurve locCurve = elem.Location as LocationCurve;
                            if (locCurve == null || locCurve.Curve == null) continue;

                            Curve curve = locCurve.Curve;
                            double length = curve.Length;

                            if (length <= 0.001) continue;

                            // Вектор направления сегмента
                            XYZ startPt = curve.GetEndPoint(0);
                            XYZ endPt = curve.GetEndPoint(1);
                            XYZ dir = (endPt - startPt).Normalize();

                            // Фильтр по ориентации: 0 = Все, 1 = Горизонтальные, 2 = Вертикальные
                            bool isVertical = Math.Abs(dir.Z) > 0.999;
                            if (orientationFilterIndex == 1 && isVertical) continue;
                            if (orientationFilterIndex == 2 && !isVertical) continue;

                            // Определение векторов смещения Г и В
                            XYZ hDir;
                            XYZ vDir;

                            if (Math.Abs(dir.Z) > 0.999) // Вертикальный сегмент
                            {
                                hDir = XYZ.BasisX;
                                vDir = XYZ.BasisY;
                            }
                            else // Горизонтальный или наклонный сегмент
                            {
                                hDir = new XYZ(-dir.Y, dir.X, 0).Normalize();
                                vDir = XYZ.BasisZ;
                            }

                            // Алгоритм центрирования шага расстановки
                            if (length <= spacingFeet)
                            {
                                // Размещаем одну точку по центру
                                XYZ pBase = curve.Evaluate(0.5, true);
                                XYZ p = pBase + hDir * hOffsetFeet + vDir * vOffsetFeet;
                                try
                                {
                                    doc.Create.NewFamilyInstance(p, selectedSymbol, elem, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                                    placedCount++;
                                }
                                catch (Exception ex)
                                {
                                    Logger.LogError($"Не удалось разместить элемент на сети {elem.Id}", ex);
                                }
                            }
                            else
                            {
                                int count = (int)Math.Floor(length / spacingFeet);
                                if (count <= 0) count = 1;

                                double remainder = length - (count * spacingFeet);
                                double startDist = (spacingFeet / 2) + (remainder / 2);

                                for (int i = 0; i < count; i++)
                                {
                                    double dist = startDist + i * spacingFeet;
                                    double t = dist / length;

                                    XYZ pBase = curve.Evaluate(t, true);
                                    XYZ p = pBase + hDir * hOffsetFeet + vDir * vOffsetFeet;

                                    try
                                    {
                                        doc.Create.NewFamilyInstance(p, selectedSymbol, elem, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                                        placedCount++;
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.LogError($"Не удалось разместить элемент на сети {elem.Id}", ex);
                                    }
                                }
                            }
                        }

                        tx.Commit();
                    }

                    Logger.Log($"Успешно расставлено элементов по сети: {placedCount}.");
                    TaskDialog.Show("Успех", $"Расстановка завершена!\nРазмещено элементов: {placedCount}");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.LogError("Ошибка во время выполнения PlacementCommand", ex);
                message = ex.Message + "\n" + ex.StackTrace;
                return Result.Failed;
            }
        }
    }
}
