using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using Definition = Autodesk.Revit.DB.Definition;
using DefinitionGroup = Autodesk.Revit.DB.DefinitionGroup;
using DefinitionFile = Autodesk.Revit.DB.DefinitionFile;
using ExternalDefinition = Autodesk.Revit.DB.ExternalDefinition;

using ForgeTypeId = Autodesk.Revit.DB.ForgeTypeId;
using SpecTypeId = Autodesk.Revit.DB.SpecTypeId;
using GroupTypeId = Autodesk.Revit.DB.GroupTypeId;

namespace BimboClub
{
    [Transaction(TransactionMode.Manual)]
    public class AddParameterCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null)
            {
                TaskDialog.Show("Ошибка", "Активный документ не найден.");
                return Result.Failed;
            }
            Document doc = uidoc.Document;

            try
            {
                Logger.Log($"Запуск инструмента добавления параметров BimboClub. Документ: '{doc.Title}'.");

                // Открываем диалоговое окно
                AddParameterWindow window = new AddParameterWindow(uiapp, doc);
                var helper = new System.Windows.Interop.WindowInteropHelper(window);
                helper.Owner = uiapp.MainWindowHandle;

                if (window.ShowDialog() != true)
                {
                    Logger.Log("Добавление параметров отменено пользователем.");
                    return Result.Cancelled;
                }

                // 1. Проверяем, существует ли уже параметр с таким именем
                bool parameterExists = false;
                if (doc.IsFamilyDocument)
                {
                    foreach (FamilyParameter fp in doc.FamilyManager.Parameters)
                    {
                        if (fp.Definition.Name.Equals(window.ParameterName, StringComparison.OrdinalIgnoreCase))
                        {
                            parameterExists = true;
                            break;
                        }
                    }
                    if (parameterExists)
                    {
                        TaskDialog.Show("Предупреждение", $"Параметр с именем '{window.ParameterName}' уже добавлен в это семейство.");
                        return Result.Cancelled;
                    }
                }
                else
                {
                    if (window.SelectedCategories.Count > 0)
                    {
                        BindingMap bindingMap = doc.ParameterBindings;
                        DefinitionBindingMapIterator it = bindingMap.ForwardIterator();
                        while (it.MoveNext())
                        {
                            Definition def = it.Key;
                            if (def.Name.Equals(window.ParameterName, StringComparison.OrdinalIgnoreCase))
                            {
                                parameterExists = true;
                                break;
                            }
                        }
                        if (parameterExists)
                        {
                            TaskDialog.Show("Предупреждение", $"Параметр с именем '{window.ParameterName}' уже добавлен как проектный параметр.");
                            return Result.Cancelled;
                        }
                    }
                }

                // 2. Выполняем добавление параметра в рамках транзакции
                using (Transaction tx = new Transaction(doc, "Добавление параметра"))
                {
                    tx.Start();

                    if (doc.IsFamilyDocument)
                    {
                        // Добавление в Семейство
                        FamilyManager fm = doc.FamilyManager;
                        FamilyParameter famParam = null;

                        if (window.IsSharedParameter)
                        {
                            // Общий параметр
                            string originalFop = doc.Application.SharedParametersFilename;
                            try
                            {
                                doc.Application.SharedParametersFilename = window.SelectedFopPath;
                                DefinitionFile defFile = doc.Application.OpenSharedParameterFile();
                                if (defFile == null)
                                {
                                    throw new InvalidOperationException("Не удалось открыть файл общих параметров.");
                                }

                                DefinitionGroup dg = defFile.Groups.get_Item(window.SelectedFopGroupName);
                                ExternalDefinition extDef = dg.Definitions.get_Item(window.SelectedFopParamName) as ExternalDefinition;
                                if (extDef == null)
                                {
                                    throw new InvalidOperationException("Выбранный общий параметр не найден в ФОП.");
                                }

                                famParam = fm.AddParameter(extDef, window.SelectedParameterGroup, window.IsInstanceBinding);
                            }
                            finally
                            {
                                doc.Application.SharedParametersFilename = originalFop;
                            }
                        }
                        else
                        {
                            // Локальный параметр семейства
                            famParam = fm.AddParameter(
                                window.ParameterName,
                                window.SelectedParameterGroup,
                                window.SelectedParameterType,
                                window.IsInstanceBinding);
                        }

                        if (famParam == null)
                        {
                            throw new Exception("Не удалось создать параметр в семействе.");
                        }
                    }
                    else
                    {
                        if (window.SelectedCategories.Count > 0)
                        {
                            // Добавление в Проект
                            Definition def = null;
                            string originalFop = doc.Application.SharedParametersFilename;

                            if (window.IsSharedParameter)
                            {
                                try
                                {
                                    doc.Application.SharedParametersFilename = window.SelectedFopPath;
                                    DefinitionFile defFile = doc.Application.OpenSharedParameterFile();
                                    if (defFile == null)
                                    {
                                        throw new InvalidOperationException("Не удалось открыть файл общих параметров.");
                                    }

                                    DefinitionGroup dg = defFile.Groups.get_Item(window.SelectedFopGroupName);
                                    def = dg.Definitions.get_Item(window.SelectedFopParamName);
                                    if (def == null)
                                    {
                                        throw new InvalidOperationException("Выбранный общий параметр не найден в ФОП.");
                                    }
                                }
                                finally
                                {
                                    doc.Application.SharedParametersFilename = originalFop;
                                }
                            }
                            else
                            {
                                // Обычный проектный параметр (через создание временной записи в ФОП)
                                string fopDataType = "TEXT";
                                ForgeTypeId t = window.SelectedParameterType;
                                if (t == SpecTypeId.String.Text) fopDataType = "TEXT";
                                else if (t == SpecTypeId.Length) fopDataType = "LENGTH";
                                else if (t == SpecTypeId.Int.Integer) fopDataType = "INTEGER";
                                else if (t == SpecTypeId.Angle) fopDataType = "ANGLE";
                                else if (t == SpecTypeId.Number) fopDataType = "NUMBER";
                                else if (t == SpecTypeId.Boolean.YesNo) fopDataType = "YESNO";

                                string err;
                                def = GetOrCreateTemporarySharedParameter(doc.Application, window.ParameterName, fopDataType, out err);
                                if (def == null)
                                {
                                    throw new Exception("Не удалось создать временный параметр во ФОП: " + err);
                                }
                            }

                            // Создаем биндинг категорий
                            CategorySet catSet = doc.Application.Create.NewCategorySet();
                            foreach (Category cat in window.SelectedCategories)
                            {
                                catSet.Insert(cat);
                            }

                            Binding binding = window.IsInstanceBinding
                                ? (Binding)doc.Application.Create.NewInstanceBinding(catSet)
                                : (Binding)doc.Application.Create.NewTypeBinding(catSet);

                            // Привязываем к проекту
                            bool bindResult = doc.ParameterBindings.Insert(def, binding, window.SelectedParameterGroup);
                            if (!bindResult)
                            {
                                // Пробуем перезаписать
                                doc.ParameterBindings.ReInsert(def, binding, window.SelectedParameterGroup);
                            }
                        }
                    }

                    tx.Commit();
                }

                // 3. Добавление параметра в выбранные семейства проекта
                if (!doc.IsFamilyDocument && window.SelectedFamilies.Count > 0)
                {
                    int addedCount = 0;
                    foreach (var family in window.SelectedFamilies)
                    {
                        try
                        {
                            Document famDoc = doc.EditFamily(family);
                            if (famDoc != null)
                            {
                                try
                                {
                                    bool paramExistsInFam = false;
                                    foreach (FamilyParameter fp in famDoc.FamilyManager.Parameters)
                                    {
                                        if (fp.Definition.Name.Equals(window.ParameterName, StringComparison.OrdinalIgnoreCase))
                                        {
                                            paramExistsInFam = true;
                                            break;
                                        }
                                    }

                                    if (!paramExistsInFam)
                                    {
                                        using (Transaction famTx = new Transaction(famDoc, "Добавление параметра в семейство"))
                                        {
                                            famTx.Start();
                                            FamilyManager fm = famDoc.FamilyManager;
                                            FamilyParameter famParam = null;

                                            if (window.IsSharedParameter)
                                            {
                                                string originalFop = doc.Application.SharedParametersFilename;
                                                try
                                                {
                                                    doc.Application.SharedParametersFilename = window.SelectedFopPath;
                                                    DefinitionFile defFile = doc.Application.OpenSharedParameterFile();
                                                    if (defFile != null)
                                                    {
                                                        DefinitionGroup dg = defFile.Groups.get_Item(window.SelectedFopGroupName);
                                                        ExternalDefinition extDef = dg.Definitions.get_Item(window.SelectedFopParamName) as ExternalDefinition;
                                                        if (extDef != null)
                                                        {
                                                            famParam = fm.AddParameter(extDef, window.SelectedParameterGroup, window.IsInstanceBinding);
                                                        }
                                                    }
                                                }
                                                finally
                                                {
                                                    doc.Application.SharedParametersFilename = originalFop;
                                                }
                                            }
                                            else
                                            {
                                                famParam = fm.AddParameter(
                                                    window.ParameterName,
                                                    window.SelectedParameterGroup,
                                                    window.SelectedParameterType,
                                                    window.IsInstanceBinding);
                                            }

                                            famTx.Commit();
                                            if (famParam != null)
                                            {
                                                addedCount++;
                                            }
                                        }
                                    }

                                    // Загружаем обратно в проект (Revit API сам управляет транзакцией внутри LoadFamily)
                                    famDoc.LoadFamily(doc, new BimboFamilyLoadOptions());
                                }
                                finally
                                {
                                    famDoc.Close(false);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Не удалось добавить параметр в семейство '{family.Name}'", ex);
                        }
                    }
                    Logger.Log($"Параметр добавлен в {addedCount} семейств.");
                }

                Logger.Log($"Успешно добавлен параметр '{window.ParameterName}'.");
                TaskDialog.Show("Успех", $"Параметр '{window.ParameterName}' успешно добавлен!");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.LogError("Ошибка во время создания параметра", ex);
                message = ex.Message + "\n" + ex.StackTrace;
                return Result.Failed;
            }
        }

        private Definition GetOrCreateTemporarySharedParameter(Autodesk.Revit.ApplicationServices.Application app, string name, string fopDataTypeString, out string error)
        {
            error = null;
            string tempFop = Path.Combine(Path.GetTempPath(), "BimboClub_Temp_FOP.txt");
            string originalFop = app.SharedParametersFilename;

            try
            {
                // Гарантируем корректный заголовок временного ФОП
                if (!File.Exists(tempFop))
                {
                    string[] headers = new string[]
                    {
                        "# This is a Revit shared parameter file.",
                        "# Do not edit manually.",
                        "*META\tVERSION\tMINVERSION",
                        "META\t2\t1",
                        "*GROUP\tID\tNAME",
                        "GROUP\t1\tBimboClub Project Parameters",
                        "*PARAM\tGUID\tNAME\tDATATYPE\tDATACATEGORY\tGROUP\tVISIBLE\tDESCRIPTION\tUSERMODIFIABLE"
                    };
                    File.WriteAllLines(tempFop, headers, System.Text.Encoding.UTF8);
                }

                app.SharedParametersFilename = tempFop;
                DefinitionFile defFile = app.OpenSharedParameterFile();
                if (defFile == null)
                {
                    throw new InvalidOperationException("Не удалось открыть временный файл ФОП.");
                }

                DefinitionGroup dg = defFile.Groups.get_Item("BimboClub Project Parameters");
                if (dg == null)
                {
                    dg = defFile.Groups.Create("BimboClub Project Parameters");
                }

                Definition def = dg.Definitions.get_Item(name);
                if (def != null)
                {
                    return def;
                }

                // Создаем новый параметр во временном ФОП через API
                ForgeTypeId specId = SpecTypeId.String.Text;
                if (fopDataTypeString == "TEXT") specId = SpecTypeId.String.Text;
                else if (fopDataTypeString == "LENGTH") specId = SpecTypeId.Length;
                else if (fopDataTypeString == "INTEGER") specId = SpecTypeId.Int.Integer;
                else if (fopDataTypeString == "ANGLE") specId = SpecTypeId.Angle;
                else if (fopDataTypeString == "NUMBER") specId = SpecTypeId.Number;
                else if (fopDataTypeString == "YESNO") specId = SpecTypeId.Boolean.YesNo;

                ExternalDefinitionCreationOptions options = new ExternalDefinitionCreationOptions(name, specId);

                def = dg.Definitions.Create(options);
                return def;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return null;
            }
            finally
            {
                app.SharedParametersFilename = originalFop;
            }
        }
    }

    public class BimboFamilyLoadOptions : IFamilyLoadOptions
    {
        public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
        {
            overwriteParameterValues = true;
            return true;
        }

        public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse, out FamilySource source, out bool overwriteParameterValues)
        {
            source = FamilySource.Family;
            overwriteParameterValues = true;
            return true;
        }
    }
}
