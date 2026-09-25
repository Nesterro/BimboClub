using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BimboClub
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ConnectConnectorsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null || uidoc.Document == null)
            {
                message = "Нет активного документа Revit.";
                return Result.Failed;
            }

            Document doc = uidoc.Document;
            MepElementSelectionFilter filter = new MepElementSelectionFilter();

            int connectedCount = 0;

            try
            {
                while (true)
                {
                    // 1. Выберите присоединяемый элемент (который будет перемещаться и поворачиваться)
                    Reference ref1;
                    try
                    {
                        ref1 = uidoc.Selection.PickObject(
                            ObjectType.Element, 
                            filter, 
                            "Выберите присоединяемый элемент (фитинг/участок) [ESC для завершения]");
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        break;
                    }

                    if (ref1 == null) break;
                    Element elem1 = doc.GetElement(ref1);
                    XYZ pickPoint1 = ref1.GlobalPoint;

                    if (elem1.Pinned)
                    {
                        TaskDialog.Show("Соединение коннекторов", "Присоединяемый элемент закреплен (Pinned). Открепите его перед перемещением.");
                        continue;
                    }

                    List<Connector> freeConnectors1 = GetFreeConnectors(elem1);
                    if (freeConnectors1.Count == 0)
                    {
                        TaskDialog.Show("Соединение коннекторов", "У выбранного присоединяемого элемента нет свободных коннекторов.");
                        continue;
                    }

                    Connector conn1 = GetClosestConnector(freeConnectors1, pickPoint1);
                    if (conn1 == null)
                    {
                        TaskDialog.Show("Соединение коннекторов", "Не удалось определить коннектор у присоединяемого элемента.");
                        continue;
                    }

                    // 2. Выберите элемент, к которому присоединить (базовый элемент, остается неподвижным)
                    Reference ref2;
                    try
                    {
                        ref2 = uidoc.Selection.PickObject(
                            ObjectType.Element, 
                            filter, 
                            "Выберите элемент, к которому присоединить [ESC для завершения]");
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        break;
                    }

                    if (ref2 == null) break;

                    if (ref2.ElementId == ref1.ElementId)
                    {
                        TaskDialog.Show("Соединение коннекторов", "Нельзя присоединить элемент к самому себе. Выберите другой элемент.");
                        continue;
                    }

                    Element elem2 = doc.GetElement(ref2);
                    XYZ pickPoint2 = ref2.GlobalPoint;

                    List<Connector> freeConnectors2 = GetFreeConnectors(elem2);
                    if (freeConnectors2.Count == 0)
                    {
                        TaskDialog.Show("Соединение коннекторов", "У целевого элемента нет свободных коннекторов.");
                        continue;
                    }

                    Connector conn2 = GetClosestConnector(freeConnectors2, pickPoint2);
                    if (conn2 == null)
                    {
                        TaskDialog.Show("Соединение коннекторов", "Не удалось определить коннектор у целевого элемента.");
                        continue;
                    }

                    // 3. Выравнивание, перемещение и соединение
                    using (Transaction trans = new Transaction(doc, "Соединить коннекторы"))
                    {
                        trans.Start();

                        try
                        {
                            int conn1Id = conn1.Id;
                            int conn2Id = conn2.Id;

                            // Векторы направлений коннекторов (BasisZ смотрит наружу от элемента)
                            XYZ dir1 = conn1.CoordinateSystem.BasisZ.Normalize();
                            XYZ dir2 = conn2.CoordinateSystem.BasisZ.Normalize();

                            // Для стыковки dir1 должен смотреть прямо навстречу dir2 (dir1 == -dir2)
                            XYZ targetDir1 = -dir2;

                            double dot = Math.Max(-1.0, Math.Min(1.0, dir1.DotProduct(targetDir1)));
                            double angle = Math.Acos(dot);

                            // Если направления не совпадают (угол > ~0.05 градуса), поворачиваем присоединяемый элемент
                            if (angle > 0.001)
                            {
                                XYZ rotAxis = dir1.CrossProduct(targetDir1);
                                if (rotAxis.GetLength() < 0.001)
                                {
                                    // dir1 и targetDir1 противоположны (т.е. dir1 == dir2, смотрят в одну сторону, угол 180 градусов)
                                    // Находим любой перпендикулярный вектор
                                    if (Math.Abs(dir1.Z) < 0.85)
                                    {
                                        rotAxis = dir1.CrossProduct(XYZ.BasisZ).Normalize();
                                    }
                                    else
                                    {
                                        rotAxis = dir1.CrossProduct(XYZ.BasisX).Normalize();
                                    }
                                    angle = Math.PI;
                                }
                                else
                                {
                                    rotAxis = rotAxis.Normalize();
                                }

                                Line rotAxisLine = Line.CreateUnbound(conn1.Origin, rotAxis);
                                ElementTransformUtils.RotateElement(doc, elem1.Id, rotAxisLine, angle);
                                doc.Regenerate();
                            }

                            // Обновляем ссылку на коннектор conn1 после вращения
                            conn1 = GetConnectorById(elem1, conn1Id) ?? conn1;

                            // Перемещаем элемент 1, чтобы коннекторы совпали
                            XYZ translation = conn2.Origin - conn1.Origin;
                            if (translation.GetLength() > 0.0001)
                            {
                                ElementTransformUtils.MoveElement(doc, elem1.Id, translation);
                                doc.Regenerate();
                            }

                            // Обновляем коннекторы и соединяем
                            conn1 = GetConnectorById(elem1, conn1Id) ?? conn1;
                            conn2 = GetConnectorById(elem2, conn2Id) ?? conn2;

                            try
                            {
                                conn1.ConnectTo(conn2);
                            }
                            catch (Exception exConn)
                            {
                                Logger.Log($"ConnectTo warning: {exConn.Message}", "WARN");
                            }

                            trans.Commit();
                            connectedCount++;
                        }
                        catch (Exception ex)
                        {
                            trans.RollBack();
                            Logger.LogError("Ошибка при соединении коннекторов", ex);
                            TaskDialog.Show("Ошибка соединения", $"Не удалось соединить элементы: {ex.Message}");
                        }
                    }
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.LogError("Ошибка в команде Соединить коннекторы", ex);
                message = ex.Message;
                return Result.Failed;
            }
        }

        private static List<Connector> GetFreeConnectors(Element element)
        {
            List<Connector> freeConnectors = new List<Connector>();
            if (element == null) return freeConnectors;

            ConnectorSet connectors = null;
            if (element is MEPCurve mepCurve)
            {
                connectors = mepCurve.ConnectorManager?.Connectors;
            }
            else if (element is FamilyInstance fi && fi.MEPModel != null)
            {
                connectors = fi.MEPModel.ConnectorManager?.Connectors;
            }

            if (connectors != null)
            {
                foreach (Connector c in connectors)
                {
                    if (c == null) continue;
                    if (c.ConnectorType == ConnectorType.Logical) continue;
                    if (!c.IsConnected)
                    {
                        freeConnectors.Add(c);
                    }
                }
            }

            return freeConnectors;
        }

        private static Connector GetClosestConnector(List<Connector> connectors, XYZ pickPoint)
        {
            if (connectors == null || connectors.Count == 0) return null;
            if (pickPoint == null) return connectors[0];

            Connector closest = null;
            double minDist = double.MaxValue;

            foreach (var c in connectors)
            {
                double dist = c.Origin.DistanceTo(pickPoint);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = c;
                }
            }

            return closest;
        }

        private static Connector GetConnectorById(Element element, int connectorId)
        {
            if (element == null) return null;

            ConnectorSet connectors = null;
            if (element is MEPCurve mepCurve)
            {
                connectors = mepCurve.ConnectorManager?.Connectors;
            }
            else if (element is FamilyInstance fi && fi.MEPModel != null)
            {
                connectors = fi.MEPModel.ConnectorManager?.Connectors;
            }

            if (connectors != null)
            {
                foreach (Connector c in connectors)
                {
                    if (c != null && c.Id == connectorId)
                    {
                        return c;
                    }
                }
            }

            return null;
        }

        private class MepElementSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                if (elem == null) return false;
                if (elem is MEPCurve) return true;
                if (elem is FamilyInstance fi && fi.MEPModel != null) return true;
                return false;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return true;
            }
        }
    }
}
