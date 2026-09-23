using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Autodesk.Revit.DB;

namespace BimboClub.ExtraScheduleItems
{
    public partial class ExtraScheduleItemsWindow : Window
    {
        private readonly Document _doc;
        private readonly ObservableCollection<ExtraScheduleItem> _allItems = new ObservableCollection<ExtraScheduleItem>();
        private readonly List<int> _deletedIds = new List<int>();
        private ObservableCollection<ExtraScheduleCatalogCategory> _catalog;
        private ICollectionView _itemsView;

        public ExtraScheduleItemsWindow(Document doc)
        {
            InitializeComponent();
            _doc = doc;

            DocInfoTextBlock.Text = $"Документ: {_doc?.Title ?? "Без имени"}";

            InitializeData();
        }

        private void InitializeData()
        {
            try
            {
                // 1. Загрузка элементов из проекта Revit
                var existing = ExtraScheduleService.ScanExistingItems(_doc);
                _allItems.Clear();
                foreach (var item in existing)
                {
                    _allItems.Add(item);
                }

                // Настройка фильтрации DataGrid
                _itemsView = CollectionViewSource.GetDefaultView(_allItems);
                _itemsView.Filter = FilterScheduleItem;
                ScheduleDataGrid.ItemsSource = _itemsView;

                UpdateSectionFilterOptions();
                UpdateCounters();

                // 2. Загрузка каталога расходников
                LoadCatalogTree();

                StatusMessageTextBlock.Text = $"Загружено позиций из модели Revit: {existing.Count}";
            }
            catch (Exception ex)
            {
                Logger.LogError("Ошибка инициализации окна ExtraScheduleItemsWindow", ex);
                MessageBox.Show($"Ошибка при загрузке данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadCatalogTree(string search = null)
        {
            if (CatalogTreeView == null) return;

            _catalog = ExtraScheduleCatalogManager.LoadCatalog() ?? new ObservableCollection<ExtraScheduleCatalogCategory>();
            CatalogTreeView.Items.Clear();

            string q = search?.Trim().ToLowerInvariant() ?? "";

            foreach (var cat in _catalog)
            {
                if (cat == null) continue;
                var items = cat.Items ?? new ObservableCollection<ExtraScheduleCatalogItem>();
                var matchingItems = string.IsNullOrEmpty(q)
                    ? items
                    : new ObservableCollection<ExtraScheduleCatalogItem>(
                        items.Where(i => i != null && ((i.Name ?? "") + " " + (i.Mark ?? "") + " " + (i.Code ?? "")).ToLowerInvariant().Contains(q)));

                if (matchingItems.Count == 0 && !string.IsNullOrEmpty(q))
                {
                    continue;
                }

                var catItem = new TreeViewItem
                {
                    Header = $"{cat.Icon}  {cat.Title} ({matchingItems.Count})",
                    FontWeight = FontWeights.Bold,
                    IsExpanded = true
                };

                foreach (var it in matchingItems)
                {
                    if (it == null) continue;
                    var leaf = new TreeViewItem
                    {
                        Header = $"{it.Name} {(string.IsNullOrEmpty(it.Mark) ? "" : $"[{it.Mark}]")}",
                        FontWeight = FontWeights.Normal,
                        Tag = it
                    };
                    leaf.MouseDoubleClick += (s, e) =>
                    {
                        e.Handled = true;
                        AddCatalogItem(it);
                    };
                    catItem.Items.Add(leaf);
                }

                CatalogTreeView.Items.Add(catItem);
            }
        }

        private void UpdateSectionFilterOptions()
        {
            string currentSelected = SectionFilterComboBox.SelectedItem as string;

            var sections = _allItems
                .Select(i => i.Group)
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .Distinct()
                .OrderBy(g => g)
                .ToList();

            SectionFilterComboBox.Items.Clear();
            SectionFilterComboBox.Items.Add("Все разделы");

            foreach (var s in sections)
            {
                SectionFilterComboBox.Items.Add(s);
            }

            if (!string.IsNullOrEmpty(currentSelected) && SectionFilterComboBox.Items.Contains(currentSelected))
            {
                SectionFilterComboBox.SelectedItem = currentSelected;
            }
            else
            {
                SectionFilterComboBox.SelectedIndex = 0;
            }
        }

        private void UpdateCounters()
        {
            TotalItemsCountText.Text = _allItems.Count.ToString();
            InModelCountText.Text = _allItems.Count(i => i.Id > 0).ToString();
        }

        private bool FilterScheduleItem(object obj)
        {
            if (obj is not ExtraScheduleItem item) return false;

            // 1. Фильтр раздела
            string selectedSection = SectionFilterComboBox.SelectedItem as string;
            if (!string.IsNullOrEmpty(selectedSection) && selectedSection != "Все разделы")
            {
                if (!item.Group.Equals(selectedSection, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            // 2. Строковый поиск
            string query = SearchTextBox.Text?.Trim().ToLowerInvariant() ?? "";
            if (string.IsNullOrEmpty(query) || query == "поиск по наименованию / марке...")
            {
                return true;
            }

            string combined = $"{item.Group} {item.Position} {item.Name} {item.Mark} {item.Code} {item.Manufacturer} {item.Note}".ToLowerInvariant();
            return combined.Contains(query);
        }

        #region Обработчики поиска и фильтрации

        private void SearchTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (SearchTextBox.Text == "Поиск по наименованию / марке...")
            {
                SearchTextBox.Text = "";
                SearchTextBox.Foreground = (SolidColorBrush)FindResource("TextWhiteBrush");
            }
        }

        private void SearchTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchTextBox.Text))
            {
                SearchTextBox.Text = "Поиск по наименованию / марке...";
                SearchTextBox.Foreground = (SolidColorBrush)FindResource("TextMutedBrush");
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _itemsView?.Refresh();
        }

        private void SectionFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _itemsView?.Refresh();
        }

        private void ResetFilters_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Text = "Поиск по наименованию / марке...";
            SearchTextBox.Foreground = (SolidColorBrush)FindResource("TextMutedBrush");
            SectionFilterComboBox.SelectedIndex = 0;
            _itemsView?.Refresh();
        }

        private void CatalogSearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (CatalogSearchBox.Text == "Поиск в библиотеке...")
            {
                CatalogSearchBox.Text = "";
                CatalogSearchBox.Foreground = (SolidColorBrush)FindResource("TextWhiteBrush");
            }
        }

        private void CatalogSearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(CatalogSearchBox.Text))
            {
                CatalogSearchBox.Text = "Поиск в библиотеке...";
                CatalogSearchBox.Foreground = (SolidColorBrush)FindResource("TextMutedBrush");
            }
        }

        private void CatalogSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (CatalogTreeView == null || CatalogSearchBox == null) return;
            string txt = CatalogSearchBox.Text?.Trim() ?? "";
            if (txt == "Поиск в библиотеке...") txt = "";
            LoadCatalogTree(txt);
        }

        #endregion

        #region Действия с таблицей

        private void AddRow_Click(object sender, RoutedEventArgs e)
        {
            string defaultGroup = (SectionFilterComboBox.SelectedItem as string != "Все разделы")
                ? SectionFilterComboBox.SelectedItem as string ?? ""
                : (_allItems.LastOrDefault()?.Group ?? "ОВ1");

            var newItem = new ExtraScheduleItem
            {
                Id = -1,
                IsNew = true,
                Group = defaultGroup,
                Name = "Новый немоделируемый элемент",
                Unit = "шт",
                Count = 1.0
            };

            _allItems.Add(newItem);
            ScheduleDataGrid.SelectedItem = newItem;
            ScheduleDataGrid.ScrollIntoView(newItem);
            UpdateCounters();
            UpdateSectionFilterOptions();
            StatusMessageTextBlock.Text = "Добавлена новая строка в таблицу";
        }

        private void DuplicateRow_Click(object sender, RoutedEventArgs e)
        {
            if (ScheduleDataGrid.SelectedItem is ExtraScheduleItem selected)
            {
                var clone = selected.Clone();
                _allItems.Add(clone);
                ScheduleDataGrid.SelectedItem = clone;
                ScheduleDataGrid.ScrollIntoView(clone);
                UpdateCounters();
                StatusMessageTextBlock.Text = $"Строка «{selected.Name}» продублирована";
            }
            else
            {
                MessageBox.Show("Выберите строку в таблице для дублирования.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void DeleteRow_Click(object sender, RoutedEventArgs e)
        {
            var selectedList = ScheduleDataGrid.SelectedItems.Cast<ExtraScheduleItem>().ToList();
            if (selectedList.Count == 0)
            {
                MessageBox.Show("Выберите строки в таблице для удаления.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show(
                $"Удалить выбранные позиции ({selectedList.Count} шт.)?",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            foreach (var item in selectedList)
            {
                if (item.Id > 0)
                {
                    _deletedIds.Add(item.Id);
                }
                _allItems.Remove(item);
            }

            UpdateCounters();
            UpdateSectionFilterOptions();
            StatusMessageTextBlock.Text = $"Удалено строк: {selectedList.Count}";
        }

        private void ScheduleDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.Row.Item is ExtraScheduleItem item)
            {
                item.IsModified = true;
            }
        }

        #endregion

        #region Каталог и библиотека

        private void AddCatalogItemToSchedule_Click(object sender, RoutedEventArgs e)
        {
            if (CatalogTreeView.SelectedItem is TreeViewItem tvi && tvi.Tag is ExtraScheduleCatalogItem catItem)
            {
                AddCatalogItem(catItem);
            }
            else
            {
                MessageBox.Show("Выберите элемент в библиотеке расходников для добавления.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void AddCatalogItem(ExtraScheduleCatalogItem catItem)
        {
            string currentSection = (SectionFilterComboBox.SelectedItem as string != "Все разделы")
                ? SectionFilterComboBox.SelectedItem as string ?? ""
                : (_allItems.LastOrDefault()?.Group ?? "ОВ1");

            var item = catItem.ToScheduleItem(currentSection);
            _allItems.Add(item);
            ScheduleDataGrid.SelectedItem = item;
            ScheduleDataGrid.ScrollIntoView(item);
            UpdateCounters();
            UpdateSectionFilterOptions();

            StatusMessageTextBlock.Text = $"Элемент «{item.Name}» добавлен из библиотеки";
        }

        private void SaveCurrentToCatalog_Click(object sender, RoutedEventArgs e)
        {
            if (ScheduleDataGrid.SelectedItem is ExtraScheduleItem selected)
            {
                if (string.IsNullOrWhiteSpace(selected.Name))
                {
                    MessageBox.Show("Наименование элемента не может быть пустым.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Поиск или создание категории "Пользовательские"
                var userCat = _catalog.FirstOrDefault(c => c.Title == "Пользовательские");
                if (userCat == null)
                {
                    userCat = new ExtraScheduleCatalogCategory { Title = "Пользовательские", Icon = "⭐" };
                    _catalog.Add(userCat);
                }

                var catalogItem = new ExtraScheduleCatalogItem
                {
                    Name = selected.Name,
                    Mark = selected.Mark,
                    Code = selected.Code,
                    Manufacturer = selected.Manufacturer,
                    Unit = selected.Unit,
                    DefaultCount = selected.Count,
                    Weight = selected.Weight,
                    Note = selected.Note,
                    DefaultGroup = selected.Group
                };

                userCat.Items.Add(catalogItem);
                ExtraScheduleCatalogManager.SaveCatalog(_catalog);
                LoadCatalogTree();

                MessageBox.Show($"Элемент «{selected.Name}» успешно сохранен в библиотеку в категорию «Пользовательские»!", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Выберите строку в таблице, которую хотите сохранить в каталог.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        #endregion

        #region Импорт / Экспорт Excel

        private void ImportClipboard_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!Clipboard.ContainsText())
                {
                    MessageBox.Show("Буфер обмена пуст. Скопируйте строки из Excel (Ctrl+C) и повторите попытку.", "Буфер обмена", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string text = Clipboard.GetText();
                var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length == 0) return;

                int imported = 0;
                string currentGroup = (SectionFilterComboBox.SelectedItem as string != "Все разделы")
                    ? SectionFilterComboBox.SelectedItem as string ?? "ОВ1"
                    : "ОВ1";

                foreach (var line in lines)
                {
                    var cols = line.Split('\t');
                    if (cols.Length == 0) continue;

                    var item = new ExtraScheduleItem
                    {
                        Id = -1,
                        IsNew = true,
                        Group = currentGroup
                    };

                    if (cols.Length == 1)
                    {
                        item.Name = cols[0].Trim();
                    }
                    else if (cols.Length == 2)
                    {
                        item.Name = cols[0].Trim();
                        double.TryParse(cols[1].Trim().Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double cnt);
                        item.Count = cnt > 0 ? cnt : 1.0;
                    }
                    else
                    {
                        // Поддержка стандартного формата ГОСТ СОИМ:
                        // Поз (0), Наименование (1), Марка (2), Код (3), Завод (4), Ед.изм (5), Кол-во (6), Масса (7), Примечание (8)
                        item.Position = cols.Length > 0 ? cols[0].Trim() : "";
                        item.Name = cols.Length > 1 ? cols[1].Trim() : "";
                        item.Mark = cols.Length > 2 ? cols[2].Trim() : "";
                        item.Code = cols.Length > 3 ? cols[3].Trim() : "";
                        item.Manufacturer = cols.Length > 4 ? cols[4].Trim() : "";
                        item.Unit = cols.Length > 5 && !string.IsNullOrWhiteSpace(cols[5]) ? cols[5].Trim() : "шт";

                        if (cols.Length > 6)
                        {
                            double.TryParse(cols[6].Trim().Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double cnt);
                            item.Count = cnt > 0 ? cnt : 1.0;
                        }
                        if (cols.Length > 7)
                        {
                            double.TryParse(cols[7].Trim().Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double w);
                            item.Weight = w;
                        }
                        if (cols.Length > 8)
                        {
                            item.Note = cols[8].Trim();
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(item.Name))
                    {
                        _allItems.Add(item);
                        imported++;
                    }
                }

                UpdateCounters();
                UpdateSectionFilterOptions();
                StatusMessageTextBlock.Text = $"Импортировано строк из буфера: {imported}";
                MessageBox.Show($"Успешно импортировано позиций: {imported}", "Импорт завершен", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка импорта из буфера: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var sb = new StringBuilder();
                // Заголовок
                sb.AppendLine("Раздел\tПозиция\tНаименование\tМарка\tКод изделия\tЗавод-изготовитель\tЕдиница измерения\tКоличество\tМасса, кг\tПримечание");

                foreach (var item in _allItems)
                {
                    sb.AppendLine($"{item.Group}\t{item.Position}\t{item.Name}\t{item.Mark}\t{item.Code}\t{item.Manufacturer}\t{item.Unit}\t{item.Count.ToString(CultureInfo.InvariantCulture)}\t{item.Weight.ToString(CultureInfo.InvariantCulture)}\t{item.Note}");
                }

                Clipboard.SetText(sb.ToString());
                StatusMessageTextBlock.Text = $"Таблица ({_allItems.Count} строк) скопирована в буфер обмена для вставки в Excel";
                MessageBox.Show($"Таблица ({_allItems.Count} строк) скопирована в буфер обмена!\nВы можете сразу открыть Excel и нажать Ctrl+V для вставки.", "Экспорт готов", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Применение в Revit

        private void ApplyToRevit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusMessageTextBlock.Text = "Синхронизация с моделью Revit...";

                var itemsToSync = _allItems.ToList();
                var result = ExtraScheduleService.SyncWithModel(_doc, itemsToSync, _deletedIds);

                _deletedIds.Clear();
                UpdateCounters();

                string msg = $"Синхронизация успешно завершена!\n\n" +
                             $"• Создано новых элементов: {result.created}\n" +
                             $"• Обновлено параметров: {result.updated}\n" +
                             $"• Удалено элементов: {result.deleted}\n\n" +
                             $"Все немоделируемые элементы добавлены в проект и готовы для спецификации!";

                StatusMessageTextBlock.Text = $"Готово: создано {result.created}, обновлено {result.updated}, удалено {result.deleted}";
                MessageBox.Show(msg, "Спецификация обновлена", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.LogError("Ошибка синхронизации с моделью Revit", ex);
                MessageBox.Show($"Ошибка синхронизации с моделью Revit:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion
    }
}
