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
    public class RenameFamilyCommand : IExternalCommand
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
                Logger.Log("Запуск команды Переименование семейства (Family Renamer).");

                // 1. Сбор всех загружаемых семейств в проекте
                FilteredElementCollector familyCollector = new FilteredElementCollector(doc);
                List<Family> allFamilies = familyCollector
                    .OfClass(typeof(Family))
                    .Cast<Family>()
                    .Where(f => f.IsEditable) // IsEditable отсекает системные семейства
                    .OrderBy(f => f.Name)
                    .ToList();

                if (allFamilies.Count == 0)
                {
                    TaskDialog.Show("Внимание", "В текущем проекте не найдено загружаемых (компонентных) семейств.");
                    return Result.Cancelled;
                }

                // Строим список элементов для WPF
                List<FamilyItem> familiesList = allFamilies
                    .Select(f => new FamilyItem { Name = f.Name, RawFamily = f })
                    .ToList();

                // 2. Определяем семейство по умолчанию на основе выбора пользователя
                var selectedIds = uidoc.Selection.GetElementIds();
                Family preselectedFamily = null;

                if (selectedIds.Count > 0)
                {
                    // Ищем первое подходящее загружаемое семейство среди выделенных элементов
                    foreach (var id in selectedIds)
                    {
                        Element elem = doc.GetElement(id);
                        if (elem is FamilyInstance fi && fi.Symbol != null && fi.Symbol.Family != null && fi.Symbol.Family.IsEditable)
                        {
                            preselectedFamily = fi.Symbol.Family;
                            break;
                        }
                    }
                }

                // 3. Запускаем WPF-окно
                RenameFamilyWindow window = new RenameFamilyWindow(familiesList, preselectedFamily);
                var helper = new System.Windows.Interop.WindowInteropHelper(window);
                helper.Owner = uiapp.MainWindowHandle;

                bool? dialogResult = window.ShowDialog();

                if (dialogResult == true)
                {
                    if (window.IsRenameRequested)
                    {
                        Family familyToRename = window.SelectedFamily as Family;
                        string newName = window.NewName;

                        if (familyToRename == null)
                        {
                            TaskDialog.Show("Ошибка", "Не выбрано семейство для переименования.");
                            return Result.Failed;
                        }

                        // Валидация имени семейства
                        if (string.IsNullOrWhiteSpace(newName))
                        {
                            TaskDialog.Show("Внимание", "Имя семейства не может быть пустым.");
                            return Result.Cancelled;
                        }

                        // Проверяем наличие запрещенных символов в имени семейства в Revit
                        char[] invalidChars = new char[] { '\\', ':', '{', '}', '[', ']', '|', ';', '<', '>', '?', '`', '~' };
                        if (newName.Any(c => invalidChars.Contains(c)))
                        {
                            TaskDialog.Show("Внимание", "Имя семейства содержит недопустимые символы. Не используйте: \\ : { } [ ] | ; < > ? ` ~");
                            return Result.Cancelled;
                        }

                        // Если имя совпадает с текущим, ничего не делаем
                        if (familyToRename.Name.Equals(newName, StringComparison.Ordinal))
                        {
                            return Result.Cancelled;
                        }

                        // Проверка имени на уникальность
                        bool nameExists = allFamilies.Any(f => f.Id != familyToRename.Id && f.Name.Equals(newName, StringComparison.OrdinalIgnoreCase));
                        if (nameExists)
                        {
                            TaskDialog.Show("Внимание", $"Семейство с именем \"{newName}\" уже загружено в проект. Имя должно быть уникальным.");
                            return Result.Cancelled;
                        }

                        string oldName = familyToRename.Name;

                        // 4. Выполнение переименования в транзакции
                        using (Transaction tx = new Transaction(doc, "Переименование семейства BimboClub"))
                        {
                            tx.Start();
                            familyToRename.Name = newName;
                            tx.Commit();
                        }

                        Logger.Log($"Успешно переименовано семейство: \"{oldName}\" -> \"{newName}\".");
                        TaskDialog.Show("Успех", $"Семейство успешно переименовано!\nСтарое имя: {oldName}\nНовое имя: {newName}");
                    }
                    else if (window.IsDeleteRequested)
                    {
                        Family familyToDelete = window.SelectedFamily as Family;
                        if (familyToDelete == null)
                        {
                            TaskDialog.Show("Ошибка", "Не выбрано семейство для удаления.");
                            return Result.Failed;
                        }

                        TaskDialogResult confirm = TaskDialog.Show(
                            "Подтверждение удаления",
                            $"Вы действительно хотите удалить семейство \"{familyToDelete.Name}\" и ВСЕ его экземпляры из проекта?",
                            TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No
                        );

                        if (confirm == TaskDialogResult.Yes)
                        {
                            string famName = familyToDelete.Name;
                            using (Transaction tx = new Transaction(doc, "Удаление семейства BimboClub"))
                            {
                                tx.Start();
                                doc.Delete(familyToDelete.Id);
                                tx.Commit();
                            }

                            Logger.Log($"Успешно удалено семейство: \"{famName}\".");
                            TaskDialog.Show("Успех", $"Семейство \"{famName}\" и все его экземпляры успешно удалены из проекта.");
                        }
                    }
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.LogError("Ошибка во время выполнения RenameFamilyCommand", ex);
                message = ex.Message + "\n" + ex.StackTrace;
                return Result.Failed;
            }
        }
    }
}
