using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using WPFColor = System.Windows.Media.Color;
using WPFGrid = System.Windows.Controls.Grid;
using WPFRowDefinition = System.Windows.Controls.RowDefinition;
using WPFColumnDefinition = System.Windows.Controls.ColumnDefinition;
using ComboBox = System.Windows.Controls.ComboBox;
using CheckBox = System.Windows.Controls.CheckBox;
using Button = System.Windows.Controls.Button;
using Grid = System.Windows.Controls.Grid;
using Cursors = System.Windows.Input.Cursors;

namespace BimboClub
{
    [Transaction(TransactionMode.Manual)]
    public class FillSystemParamsCommand : IExternalCommand
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
                Logger.Log("Запуск команды заполнения параметров системных элементов.");

                // Собираем все доступные параметры в проекте для выпадающих списков
                List<string> sourceParams;
                List<string> targetParams;
                
                var allCategories = new List<BuiltInCategory>
                {
                    BuiltInCategory.OST_DuctCurves,
                    BuiltInCategory.OST_PipeCurves,
                    BuiltInCategory.OST_CableTray
                };

                CollectParameters(doc, allCategories, out sourceParams, out targetParams);

                // Инициализируем и показываем WPF окно
                FillSystemParamsWindow window = new FillSystemParamsWindow(sourceParams, targetParams);
                var helper = new System.Windows.Interop.WindowInteropHelper(window);
                helper.Owner = uiapp.MainWindowHandle;

                if (window.ShowDialog() != true)
                {
                    return Result.Cancelled;
                }

                // Читаем настройки из окна
                bool processDucts = window.ProcessDucts;
                bool processPipes = window.ProcessPipes;
                bool processCableTrays = window.ProcessCableTrays;

                bool fillName = window.FillName;
                string nameSource = window.NameSource;
                string nameTarget = window.NameTarget;

                bool fillQuantity = window.FillQuantity;
                string qtySource = window.QtySource;
                string qtyTarget = window.QtyTarget;
                string qtyUnitMode = window.QtyUnitMode; // "Meters", "Millimeters", "Pieces", "None"

                bool currentViewOnly = window.CurrentViewOnly;

                // Собираем элементы по выбранным категориям
                List<BuiltInCategory> selectedCategories = new List<BuiltInCategory>();
                if (processDucts) selectedCategories.Add(BuiltInCategory.OST_DuctCurves);
                if (processPipes) selectedCategories.Add(BuiltInCategory.OST_PipeCurves);
                if (processCableTrays) selectedCategories.Add(BuiltInCategory.OST_CableTray);

                if (selectedCategories.Count == 0)
                {
                    TaskDialog.Show("Внимание", "Не выбрано ни одной категории элементов.");
                    return Result.Failed;
                }

                List<Element> elementsToProcess = new List<Element>();
                foreach (var bic in selectedCategories)
                {
                    FilteredElementCollector collector;
                    if (currentViewOnly)
                    {
                        collector = new FilteredElementCollector(doc, doc.ActiveView.Id);
                    }
                    else
                    {
                        collector = new FilteredElementCollector(doc);
                    }

                    var categoryElements = collector
                        .OfCategory(bic)
                        .WhereElementIsNotElementType()
                        .ToElements();

                    elementsToProcess.AddRange(categoryElements);
                }

                if (elementsToProcess.Count == 0)
                {
                    TaskDialog.Show("Информация", "Не найдено элементов выбранных категорий для обработки.");
                    return Result.Succeeded;
                }

                int processedCount = 0;
                int nameFilledCount = 0;
                int qtyFilledCount = 0;

                using (Transaction t = new Transaction(doc, "Заполнение параметров системных элементов"))
                {
                    t.Start();

                    foreach (Element elem in elementsToProcess)
                    {
                        bool isModified = false;

                        // 1. Заполнение Наименования
                        if (fillName && !string.IsNullOrEmpty(nameSource) && !string.IsNullOrEmpty(nameTarget))
                        {
                            string sourceValue = GetParameterValue(elem, nameSource);
                            if (!string.IsNullOrEmpty(sourceValue))
                            {
                                if (WriteParameterValue(elem, nameTarget, sourceValue))
                                {
                                    nameFilledCount++;
                                    isModified = true;
                                }
                            }
                        }

                        // 2. Заполнение Количества
                        if (fillQuantity && !string.IsNullOrEmpty(qtySource) && !string.IsNullOrEmpty(qtyTarget))
                        {
                            double? rawDouble = GetParameterValueDouble(elem, qtySource);
                            if (rawDouble.HasValue)
                            {
                                object valueToWrite = null;

                                if (qtyUnitMode == "Meters")
                                {
                                    // Конвертируем футы в метры (1 фут = 0.3048 метра)
                                    double meters = rawDouble.Value * 0.3048;
                                    valueToWrite = Math.Round(meters, 2);
                                }
                                else if (qtyUnitMode == "Millimeters")
                                {
                                    // Конвертируем футы в миллиметры
                                    double mm = rawDouble.Value * 304.8;
                                    valueToWrite = Math.Round(mm, 0);
                                }
                                else if (qtyUnitMode == "Pieces")
                                {
                                    valueToWrite = 1.0;
                                }
                                else
                                {
                                    valueToWrite = rawDouble.Value;
                                }

                                if (WriteParameterValue(elem, qtyTarget, valueToWrite))
                                {
                                    qtyFilledCount++;
                                    isModified = true;
                                }
                            }
                        }

                        if (isModified)
                        {
                            processedCount++;
                        }
                    }

                    t.Commit();
                }

                TaskDialog.Show("Готово", $"Обработка завершена.\nОбработано элементов: {processedCount}\nЗаполнено наименований: {nameFilledCount}\nЗаполнено количеств: {qtyFilledCount}");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                Logger.LogError("Ошибка при заполнении параметров системных элементов", ex);
                return Result.Failed;
            }
        }

        private static void CollectParameters(
            Document doc,
            List<BuiltInCategory> categories,
            out List<string> sourceParams,
            out List<string> targetParams)
        {
            sourceParams = new List<string>();
            targetParams = new List<string>();

            // Русские и английские дефолтные параметры
            var fallbackSources = new List<string> { 
                "Имя типа", "Семейство и типоразмер", "Семейство", "Размер", "Длина", "Ширина", "Высота", "Диаметр", 
                "Type Name", "Family and Type", "Family", "Size", "Length", "Width", "Height", "Diameter" 
            };
            var fallbackTargets = new List<string> { 
                "ADSK_Наименование", "ADSK_Количество", "Наименование", "Количество",
                "ADSK_Title", "ADSK_Quantity", "Title", "Quantity"
            };

            HashSet<string> sourcesSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> targetsSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var bic in categories)
            {
                var elements = new FilteredElementCollector(doc)
                    .OfCategory(bic)
                    .WhereElementIsNotElementType()
                    .Take(50) // Достаточно просканировать первые 50 элементов для списка параметров
                    .ToList();

                foreach (var elem in elements)
                {
                    // Параметры экземпляра
                    foreach (Parameter p in elem.Parameters)
                    {
                        if (p.StorageType == StorageType.None) continue;
                        string name = p.Definition.Name;
                        sourcesSet.Add(name);
                        if (!p.IsReadOnly)
                        {
                            targetsSet.Add(name);
                        }
                    }

                    // Параметры типа
                    ElementId typeId = elem.GetTypeId();
                    if (typeId != ElementId.InvalidElementId)
                    {
                        Element typeElem = doc.GetElement(typeId);
                        if (typeElem != null)
                        {
                            foreach (Parameter p in typeElem.Parameters)
                            {
                                if (p.StorageType == StorageType.None) continue;
                                string name = p.Definition.Name;
                                sourcesSet.Add(name);
                            }
                        }
                    }
                }
            }

            foreach (var s in fallbackSources) sourcesSet.Add(s);
            foreach (var t in fallbackTargets) targetsSet.Add(t);

            sourceParams = sourcesSet.OrderBy(x => x).ToList();
            targetParams = targetsSet.OrderBy(x => x).ToList();
        }

        private static string GetParameterValue(Element elem, string paramName)
        {
            if (paramName.Equals("Имя типа", StringComparison.OrdinalIgnoreCase) || 
                paramName.Equals("Type Name", StringComparison.OrdinalIgnoreCase))
            {
                ElementId typeId = elem.GetTypeId();
                if (typeId != ElementId.InvalidElementId)
                {
                    Element typeElem = elem.Document.GetElement(typeId);
                    if (typeElem != null) return typeElem.Name;
                }
            }
            if (paramName.Equals("Семейство", StringComparison.OrdinalIgnoreCase) || 
                paramName.Equals("Family", StringComparison.OrdinalIgnoreCase))
            {
                ElementId typeId = elem.GetTypeId();
                if (typeId != ElementId.InvalidElementId)
                {
                    if (elem.Document.GetElement(typeId) is ElementType typeElem)
                    {
                        return typeElem.FamilyName;
                    }
                }
            }
            if (paramName.Equals("Семейство и типоразмер", StringComparison.OrdinalIgnoreCase) || 
                paramName.Equals("Family and Type", StringComparison.OrdinalIgnoreCase))
            {
                ElementId typeId = elem.GetTypeId();
                if (typeId != ElementId.InvalidElementId)
                {
                    if (elem.Document.GetElement(typeId) is ElementType typeElem)
                    {
                        return typeElem.FamilyName + " : " + typeElem.Name;
                    }
                }
            }

            Parameter p = elem.LookupParameter(paramName);
            if (p == null || !p.HasValue)
            {
                ElementId typeId = elem.GetTypeId();
                if (typeId != ElementId.InvalidElementId)
                {
                    Element typeElem = elem.Document.GetElement(typeId);
                    if (typeElem != null)
                    {
                        p = typeElem.LookupParameter(paramName);
                    }
                }
            }

            if (p != null && p.HasValue)
            {
                switch (p.StorageType)
                {
                    case StorageType.String:
                        return p.AsString();
                    case StorageType.Integer:
                        return p.AsValueString() ?? p.AsInteger().ToString();
                    case StorageType.Double:
                        return p.AsValueString() ?? p.AsDouble().ToString("G");
                    case StorageType.ElementId:
                        ElementId id = p.AsElementId();
                        if (id != ElementId.InvalidElementId)
                        {
                            Element referenced = elem.Document.GetElement(id);
                            if (referenced != null) return referenced.Name;
                        }
                        return id.ToString();
                }
            }

            return string.Empty;
        }

        private static double? GetParameterValueDouble(Element elem, string paramName)
        {
            Parameter p = elem.LookupParameter(paramName);
            if (p == null || !p.HasValue)
            {
                ElementId typeId = elem.GetTypeId();
                if (typeId != ElementId.InvalidElementId)
                {
                    Element typeElem = elem.Document.GetElement(typeId);
                    if (typeElem != null)
                    {
                        p = typeElem.LookupParameter(paramName);
                    }
                }
            }

            if (p != null && p.HasValue)
            {
                if (p.StorageType == StorageType.Double)
                {
                    return p.AsDouble();
                }
                if (p.StorageType == StorageType.Integer)
                {
                    return (double)p.AsInteger();
                }
                if (p.StorageType == StorageType.String)
                {
                    string str = p.AsString();
                    if (double.TryParse(str.Replace(',', '.'), out double dVal))
                    {
                        return dVal;
                    }
                }
            }

            return null;
        }

        private static bool WriteParameterValue(Element elem, string paramName, object value)
        {
            Parameter p = elem.LookupParameter(paramName);
            if (p == null || p.IsReadOnly) return false;

            try
            {
                if (p.StorageType == StorageType.String)
                {
                    string strVal = value?.ToString() ?? string.Empty;
                    return p.Set(strVal);
                }
                if (p.StorageType == StorageType.Double)
                {
                    if (value is double dVal)
                    {
                        return p.Set(dVal);
                    }
                    if (double.TryParse(value?.ToString()?.Replace(',', '.'), out double parsedDouble))
                    {
                        return p.Set(parsedDouble);
                    }
                }
                if (p.StorageType == StorageType.Integer)
                {
                    if (value is int iVal)
                    {
                        return p.Set(iVal);
                    }
                    if (value is double dVal)
                    {
                        return p.Set((int)Math.Round(dVal));
                    }
                    if (int.TryParse(value?.ToString(), out int parsedInt))
                    {
                        return p.Set(parsedInt);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка записи параметра {paramName} у элемента {elem.Id}", ex);
            }

            return false;
        }
    }

    public class FillSystemParamsWindow : Window
    {
        private CheckBox _chkDucts;
        private CheckBox _chkPipes;
        private CheckBox _chkCableTrays;

        private CheckBox _chkName;
        private ComboBox _cbNameSource;
        private ComboBox _cbNameTarget;

        private CheckBox _chkQty;
        private ComboBox _cbQtySource;
        private ComboBox _cbQtyTarget;
        private ComboBox _cbQtyUnit;

        private RadioButton _rbActiveView;

        public bool ProcessDucts => _chkDucts.IsChecked == true;
        public bool ProcessPipes => _chkPipes.IsChecked == true;
        public bool ProcessCableTrays => _chkCableTrays.IsChecked == true;

        public bool FillName => _chkName.IsChecked == true;
        public string NameSource => _cbNameSource.SelectedItem?.ToString();
        public string NameTarget => _cbNameTarget.SelectedItem?.ToString();

        public bool FillQuantity => _chkQty.IsChecked == true;
        public string QtySource => _cbQtySource.SelectedItem?.ToString();
        public string QtyTarget => _cbQtyTarget.SelectedItem?.ToString();
        
        public string QtyUnitMode
        {
            get
            {
                int index = _cbQtyUnit.SelectedIndex;
                if (index == 0) return "Meters";
                if (index == 1) return "Millimeters";
                if (index == 2) return "Pieces";
                return "None";
            }
        }

        public bool CurrentViewOnly => _rbActiveView.IsChecked == true;

        public FillSystemParamsWindow(List<string> sourceParams, List<string> targetParams)
        {
            Title = "Параметры системных элементов";
            Width = 480;
            Height = 490;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(WPFColor.FromRgb(30, 30, 34)); // Dark slate
            Foreground = Brushes.White;
            ResizeMode = ResizeMode.NoResize;
            WindowStyle = WindowStyle.ToolWindow;
            FontFamily = new FontFamily("Segoe UI");

            try
            {
                string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string logoPath = Path.Combine(assemblyDir, "icon32.png");
                if (File.Exists(logoPath))
                {
                    this.Icon = new BitmapImage(new Uri(logoPath));
                }
            }
            catch { }

            Grid mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.Margin = new Thickness(15);

            StackPanel contentPanel = new StackPanel { Orientation = Orientation.Vertical };

            // 1. Категории элементов
            GroupBox gbCategories = CreateGroupBox("1. Категории элементов");
            StackPanel spCats = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(5) };
            _chkDucts = CreateCheckBox("Воздуховоды", true);
            _chkPipes = CreateCheckBox("Трубы", true);
            _chkCableTrays = CreateCheckBox("Кабельные лотки", true);
            _chkDucts.Margin = new Thickness(0, 0, 15, 0);
            _chkPipes.Margin = new Thickness(0, 0, 15, 0);
            spCats.Children.Add(_chkDucts);
            spCats.Children.Add(_chkPipes);
            spCats.Children.Add(_chkCableTrays);
            gbCategories.Content = spCats;
            contentPanel.Children.Add(gbCategories);

            // 2. Заполнение наименования
            GroupBox gbName = CreateGroupBox("2. Заполнение наименований");
            StackPanel spName = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(5) };
            
            _chkName = CreateCheckBox("Заполнять Наименование", true);
            _chkName.Margin = new Thickness(0, 0, 0, 8);
            spName.Children.Add(_chkName);

            WPFGrid gridNameMapping = new WPFGrid();
            gridNameMapping.ColumnDefinitions.Add(new WPFColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            gridNameMapping.ColumnDefinitions.Add(new WPFColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            
            StackPanel spNameSource = new StackPanel { Margin = new Thickness(0, 0, 5, 0) };
            spNameSource.Children.Add(CreateLabel("Откуда (системный параметр):"));
            _cbNameSource = CreateComboBox(sourceParams, "Размер");
            spNameSource.Children.Add(_cbNameSource);
            WPFGrid.SetColumn(spNameSource, 0);
            gridNameMapping.Children.Add(spNameSource);

            StackPanel spNameTarget = new StackPanel { Margin = new Thickness(5, 0, 0, 0) };
            spNameTarget.Children.Add(CreateLabel("Куда (заполняемый параметр):"));
            _cbNameTarget = CreateComboBox(targetParams, "ADSK_Наименование");
            spNameTarget.Children.Add(_cbNameTarget);
            WPFGrid.SetColumn(spNameTarget, 1);
            gridNameMapping.Children.Add(spNameTarget);

            spName.Children.Add(gridNameMapping);
            gbName.Content = spName;
            contentPanel.Children.Add(gbName);

            // 3. Заполнение количества
            GroupBox gbQty = CreateGroupBox("3. Заполнение количества");
            StackPanel spQty = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(5) };

            _chkQty = CreateCheckBox("Заполнять Количество", true);
            _chkQty.Margin = new Thickness(0, 0, 0, 8);
            spQty.Children.Add(_chkQty);

            WPFGrid gridQtyMapping = new WPFGrid();
            gridQtyMapping.ColumnDefinitions.Add(new WPFColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
            gridQtyMapping.ColumnDefinitions.Add(new WPFColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
            gridQtyMapping.ColumnDefinitions.Add(new WPFColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            StackPanel spQtySource = new StackPanel { Margin = new Thickness(0, 0, 5, 0) };
            spQtySource.Children.Add(CreateLabel("Откуда:"));
            _cbQtySource = CreateComboBox(sourceParams, "Длина");
            spQtySource.Children.Add(_cbQtySource);
            WPFGrid.SetColumn(spQtySource, 0);
            gridQtyMapping.Children.Add(spQtySource);

            StackPanel spQtyTarget = new StackPanel { Margin = new Thickness(5, 0, 5, 0) };
            spQtyTarget.Children.Add(CreateLabel("Куда:"));
            _cbQtyTarget = CreateComboBox(targetParams, "ADSK_Количество");
            spQtyTarget.Children.Add(_cbQtyTarget);
            WPFGrid.SetColumn(spQtyTarget, 1);
            gridQtyMapping.Children.Add(spQtyTarget);

            StackPanel spQtyUnit = new StackPanel { Margin = new Thickness(5, 0, 0, 0) };
            spQtyUnit.Children.Add(CreateLabel("Конвертировать:"));
            var unitOptions = new List<string> { "В метры (м)", "В миллиметры (мм)", "В штуки (1)", "Без конвертации" };
            _cbQtyUnit = CreateComboBox(unitOptions, "В метры (м)");
            spQtyUnit.Children.Add(_cbQtyUnit);
            WPFGrid.SetColumn(spQtyUnit, 2);
            gridQtyMapping.Children.Add(spQtyUnit);

            spQty.Children.Add(gridQtyMapping);
            gbQty.Content = spQty;
            contentPanel.Children.Add(gbQty);

            // Связываем чекбоксы активности полей
            _chkName.Checked += (s, e) => { _cbNameSource.IsEnabled = true; _cbNameTarget.IsEnabled = true; };
            _chkName.Unchecked += (s, e) => { _cbNameSource.IsEnabled = false; _cbNameTarget.IsEnabled = false; };
            _chkQty.Checked += (s, e) => { _cbQtySource.IsEnabled = true; _cbQtyTarget.IsEnabled = true; _cbQtyUnit.IsEnabled = true; };
            _chkQty.Unchecked += (s, e) => { _cbQtySource.IsEnabled = false; _cbQtyTarget.IsEnabled = false; _cbQtyUnit.IsEnabled = false; };

            // 4. Область обработки
            GroupBox gbScope = CreateGroupBox("4. Область обработки");
            StackPanel spScope = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(5) };
            _rbActiveView = new RadioButton
            {
                Content = "Только активный вид",
                Foreground = Brushes.White,
                IsChecked = true,
                Margin = new Thickness(0, 0, 20, 0)
            };
            RadioButton rbProject = new RadioButton
            {
                Content = "Во всем проекте",
                Foreground = Brushes.White
            };
            spScope.Children.Add(_rbActiveView);
            spScope.Children.Add(rbProject);
            gbScope.Content = spScope;
            contentPanel.Children.Add(gbScope);

            Grid.SetRow(contentPanel, 0);
            mainGrid.Children.Add(contentPanel);

            // Кнопки
            WPFGrid buttonsGrid = new WPFGrid { Margin = new Thickness(0, 15, 0, 0) };
            buttonsGrid.ColumnDefinitions.Add(new WPFColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            buttonsGrid.ColumnDefinitions.Add(new WPFColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Button btnOk = new Button
            {
                Content = "Заполнить",
                Background = new SolidColorBrush(WPFColor.FromRgb(179, 14, 45)), // Crimson
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Height = 28,
                Margin = new Thickness(0, 0, 5, 0),
                Cursor = Cursors.Hand,
                IsDefault = true
            };
            btnOk.Click += (s, e) => { DialogResult = true; Close(); };
            btnOk.MouseEnter += (s, e) => btnOk.Background = new SolidColorBrush(WPFColor.FromRgb(200, 20, 50));
            btnOk.MouseLeave += (s, e) => btnOk.Background = new SolidColorBrush(WPFColor.FromRgb(179, 14, 45));
            WPFGrid.SetColumn(btnOk, 0);
            buttonsGrid.Children.Add(btnOk);

            Button btnCancel = new Button
            {
                Content = "Отмена",
                Background = new SolidColorBrush(WPFColor.FromRgb(68, 68, 80)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Height = 28,
                Margin = new Thickness(5, 0, 0, 0),
                Cursor = Cursors.Hand,
                IsCancel = true
            };
            btnCancel.Click += (s, e) => { DialogResult = false; Close(); };
            btnCancel.MouseEnter += (s, e) => btnCancel.Background = new SolidColorBrush(WPFColor.FromRgb(85, 85, 102));
            btnCancel.MouseLeave += (s, e) => btnCancel.Background = new SolidColorBrush(WPFColor.FromRgb(68, 68, 80));
            WPFGrid.SetColumn(btnCancel, 1);
            buttonsGrid.Children.Add(btnCancel);

            Grid.SetRow(buttonsGrid, 1);
            mainGrid.Children.Add(buttonsGrid);

            Content = mainGrid;
        }

        private GroupBox CreateGroupBox(string header)
        {
            return new GroupBox
            {
                Header = header,
                Foreground = Brushes.LightGray,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(WPFColor.FromRgb(60, 60, 68)),
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(8)
            };
        }

        private CheckBox CreateCheckBox(string content, bool isChecked)
        {
            return new CheckBox
            {
                Content = content,
                Foreground = Brushes.White,
                IsChecked = isChecked,
                VerticalContentAlignment = VerticalAlignment.Center
            };
        }

        private Label CreateLabel(string content)
        {
            return new Label
            {
                Content = content,
                Foreground = Brushes.LightGray,
                FontSize = 11,
                Padding = new Thickness(0, 2, 0, 2)
            };
        }

        private ComboBox CreateComboBox(List<string> items, string defaultItem)
        {
            ComboBox cb = new ComboBox
            {
                Background = new SolidColorBrush(WPFColor.FromRgb(45, 45, 53)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(WPFColor.FromRgb(85, 85, 102)),
                Height = 26,
                Margin = new Thickness(0, 2, 0, 8),
                VerticalContentAlignment = VerticalAlignment.Center
            };

            foreach (var item in items)
            {
                cb.Items.Add(item);
            }

            if (items.Contains(defaultItem))
            {
                cb.SelectedItem = defaultItem;
            }
            else if (cb.Items.Count > 0)
            {
                cb.SelectedIndex = 0;
            }

            UiThemeHelper.StyleComboBoxDark(cb, new SolidColorBrush(WPFColor.FromRgb(45, 45, 53)), new SolidColorBrush(WPFColor.FromRgb(85, 85, 102)));

            return cb;
        }
    }
}
