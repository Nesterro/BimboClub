using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Autodesk.Revit.DB;

namespace BimboClub
{
    public class DeleteFamilyParamsWindow : Window
    {
        private readonly Document _doc;
        private readonly List<FamilyEntryItem> _allFamilies;
        private List<FamilyParamItem> _allParams = new List<FamilyParamItem>();

        private ComboBox _cbCategoryFilter;
        private System.Windows.Controls.TextBox _txtSearchFamilies;
        private System.Windows.Controls.TextBox _txtSearchParameters;
        private ListView _lvFamilies;
        private ListView _lvParameters;
        private System.Windows.Controls.ProgressBar _progressBar;
        private TextBlock _txtStatus;
        private System.Windows.Controls.Button _btnScanParams;
        private System.Windows.Controls.Button _btnDelete;
        private TextBlock _lblFamiliesHeader;
        private TextBlock _lblParamsHeader;

        private ICollectionView _familiesView;
        private ICollectionView _paramsView;

        public DeleteFamilyParamsWindow(Document doc, List<FamilyEntryItem> families)
        {
            _doc = doc;
            _allFamilies = families;

            InitializeComponent();
            PopulateCategories();
            ApplyDataSources();
        }

        private void InitializeComponent()
        {
            Title = "Удаление параметров из семейств - BimboClub Tools";
            Width = 950;
            Height = 720;
            MinHeight = 550;
            MinWidth = 800;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(24, 24, 28)); // #18181C
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
            FontSize = 13;
            UiThemeHelper.ApplyDarkTheme(this);

            SolidColorBrush panelBg = new SolidColorBrush(System.Windows.Media.Color.FromRgb(32, 32, 38));
            SolidColorBrush controlBg = new SolidColorBrush(System.Windows.Media.Color.FromRgb(40, 40, 48));
            SolidColorBrush borderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(48, 48, 56));
            SolidColorBrush activeBorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 46, 91));
            SolidColorBrush textMuted = new SolidColorBrush(System.Windows.Media.Color.FromRgb(160, 160, 170));
            SolidColorBrush scarletBtn = new SolidColorBrush(System.Windows.Media.Color.FromRgb(179, 14, 45));
            SolidColorBrush scarletHover = new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 20, 60));

            // Стили
            Style textBoxStyle = new Style(typeof(System.Windows.Controls.TextBox));
            textBoxStyle.Setters.Add(new Setter(System.Windows.Controls.TextBox.BackgroundProperty, controlBg));
            textBoxStyle.Setters.Add(new Setter(System.Windows.Controls.TextBox.ForegroundProperty, System.Windows.Media.Brushes.White));
            textBoxStyle.Setters.Add(new Setter(System.Windows.Controls.TextBox.BorderBrushProperty, borderBrush));
            textBoxStyle.Setters.Add(new Setter(System.Windows.Controls.TextBox.PaddingProperty, new Thickness(6, 3, 6, 3)));
            textBoxStyle.Setters.Add(new Setter(System.Windows.Controls.TextBox.VerticalContentAlignmentProperty, VerticalAlignment.Center));

            Style buttonStyle = new Style(typeof(System.Windows.Controls.Button));
            buttonStyle.Setters.Add(new Setter(System.Windows.Controls.Button.BackgroundProperty, controlBg));
            buttonStyle.Setters.Add(new Setter(System.Windows.Controls.Button.ForegroundProperty, System.Windows.Media.Brushes.White));
            buttonStyle.Setters.Add(new Setter(System.Windows.Controls.Button.BorderBrushProperty, borderBrush));
            buttonStyle.Setters.Add(new Setter(System.Windows.Controls.Button.PaddingProperty, new Thickness(10, 4, 10, 4)));

            System.Windows.Controls.Grid rootGrid = new System.Windows.Controls.Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(55) }); // Шапка
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(45) }); // Категории и Быстрые действия
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Списки (Семейства + Параметры)
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(65) }); // Подвал с прогрессом и кнопкой
            Content = rootGrid;

            // 1. Шапка
            Border headerBorder = new Border
            {
                Background = new LinearGradientBrush(System.Windows.Media.Color.FromRgb(18, 18, 20), System.Windows.Media.Color.FromRgb(179, 14, 45), 0.0),
                Padding = new Thickness(15, 10, 15, 10)
            };
            System.Windows.Controls.Grid.SetRow(headerBorder, 0);
            rootGrid.Children.Add(headerBorder);

            TextBlock headerText = new TextBlock
            {
                Text = "УДАЛЕНИЕ ПАРАМЕТРОВ ИЗ СЕМЕЙСТВ",
                Foreground = System.Windows.Media.Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 16,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerBorder.Child = headerText;

            // 2. Панель фильтра категории и сканирования
            Border filterBorder = new Border
            {
                Background = panelBg,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(15, 6, 15, 6)
            };
            System.Windows.Controls.Grid.SetRow(filterBorder, 1);
            rootGrid.Children.Add(filterBorder);

            System.Windows.Controls.Grid filterGrid = new System.Windows.Controls.Grid();
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            filterBorder.Child = filterGrid;

            TextBlock lblCat = new TextBlock { Text = "Фильтр категории:", Foreground = System.Windows.Media.Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            System.Windows.Controls.Grid.SetColumn(lblCat, 0);
            filterGrid.Children.Add(lblCat);

            _cbCategoryFilter = new ComboBox { Height = 28 };
            UiThemeHelper.StyleComboBoxDark(_cbCategoryFilter, controlBg, borderBrush);
            _cbCategoryFilter.SelectionChanged += (s, e) => ApplyCategoryFilter();
            System.Windows.Controls.Grid.SetColumn(_cbCategoryFilter, 1);
            filterGrid.Children.Add(_cbCategoryFilter);

            _btnScanParams = new System.Windows.Controls.Button
            {
                Content = "🔍 Сканировать параметры",
                Style = buttonStyle,
                Height = 28,
                Cursor = System.Windows.Input.Cursors.Hand,
                FontWeight = FontWeights.SemiBold
            };
            _btnScanParams.Click += BtnScanParams_Click;
            System.Windows.Controls.Grid.SetColumn(_btnScanParams, 3);
            filterGrid.Children.Add(_btnScanParams);

            // 3. Основная рабочая область: 2 колонки (Семейства и Параметры)
            System.Windows.Controls.Grid mainGrid = new System.Windows.Controls.Grid { Margin = new Thickness(15, 10, 15, 10) };
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) }); // разделитель
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            System.Windows.Controls.Grid.SetRow(mainGrid, 2);
            rootGrid.Children.Add(mainGrid);

            // 3.1 Левая колонка - Семейства
            Border leftBorder = new Border
            {
                Background = panelBg,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(10)
            };
            System.Windows.Controls.Grid.SetColumn(leftBorder, 0);
            mainGrid.Children.Add(leftBorder);

            System.Windows.Controls.Grid leftGrid = new System.Windows.Controls.Grid();
            leftGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) }); // Заголовок
            leftGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) }); // Поиск
            leftGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) }); // Кнопки выбора
            leftGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Список
            leftBorder.Child = leftGrid;

            _lblFamiliesHeader = new TextBlock
            {
                Text = $"СЕМЕЙСТВА ПРОЕКТА ({_allFamilies.Count})",
                Foreground = activeBorderBrush,
                FontWeight = FontWeights.Bold
            };
            System.Windows.Controls.Grid.SetRow(_lblFamiliesHeader, 0);
            leftGrid.Children.Add(_lblFamiliesHeader);

            _txtSearchFamilies = new System.Windows.Controls.TextBox
            {
                Style = textBoxStyle,
                Text = "Поиск по названию семейства...",
                Foreground = System.Windows.Media.Brushes.Gray,
                Height = 26
            };
            _txtSearchFamilies.GotFocus += (s, e) =>
            {
                if (_txtSearchFamilies.Text == "Поиск по названию семейства...") { _txtSearchFamilies.Text = ""; _txtSearchFamilies.Foreground = System.Windows.Media.Brushes.White; }
            };
            _txtSearchFamilies.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_txtSearchFamilies.Text)) { _txtSearchFamilies.Text = "Поиск по названию семейства..."; _txtSearchFamilies.Foreground = System.Windows.Media.Brushes.Gray; }
            };
            _txtSearchFamilies.TextChanged += (s, e) => ApplySearchFilter();
            System.Windows.Controls.Grid.SetRow(_txtSearchFamilies, 1);
            leftGrid.Children.Add(_txtSearchFamilies);

            StackPanel famButtonsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
            System.Windows.Controls.Grid.SetRow(famButtonsPanel, 2);
            leftGrid.Children.Add(famButtonsPanel);

            System.Windows.Controls.Button btnSelectAllFam = new System.Windows.Controls.Button { Content = "Выбрать все", Style = buttonStyle, Height = 22, FontSize = 11, Margin = new Thickness(0, 0, 5, 0) };
            btnSelectAllFam.Click += (s, e) => SetFamiliesSelection(true);
            famButtonsPanel.Children.Add(btnSelectAllFam);

            System.Windows.Controls.Button btnDeselectAllFam = new System.Windows.Controls.Button { Content = "Снять все", Style = buttonStyle, Height = 22, FontSize = 11, Margin = new Thickness(0, 0, 5, 0) };
            btnDeselectAllFam.Click += (s, e) => SetFamiliesSelection(false);
            famButtonsPanel.Children.Add(btnDeselectAllFam);

            System.Windows.Controls.Button btnInvertFam = new System.Windows.Controls.Button { Content = "Инвертировать", Style = buttonStyle, Height = 22, FontSize = 11 };
            btnInvertFam.Click += (s, e) => InvertFamiliesSelection();
            famButtonsPanel.Children.Add(btnInvertFam);

            _lvFamilies = CreateFamiliesListView();
            System.Windows.Controls.Grid.SetRow(_lvFamilies, 3);
            leftGrid.Children.Add(_lvFamilies);

            // 3.2 Правая колонка - Параметры
            Border rightBorder = new Border
            {
                Background = panelBg,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(10)
            };
            System.Windows.Controls.Grid.SetColumn(rightBorder, 2);
            mainGrid.Children.Add(rightBorder);

            System.Windows.Controls.Grid rightGrid = new System.Windows.Controls.Grid();
            rightGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) }); // Заголовок
            rightGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) }); // Поиск
            rightGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) }); // Кнопки выбора
            rightGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Список
            rightBorder.Child = rightGrid;

            _lblParamsHeader = new TextBlock
            {
                Text = "ПАРАМЕТРЫ ДЛЯ УДАЛЕНИЯ (0)",
                Foreground = activeBorderBrush,
                FontWeight = FontWeights.Bold
            };
            System.Windows.Controls.Grid.SetRow(_lblParamsHeader, 0);
            rightGrid.Children.Add(_lblParamsHeader);

            _txtSearchParameters = new System.Windows.Controls.TextBox
            {
                Style = textBoxStyle,
                Text = "Поиск по имени параметра...",
                Foreground = System.Windows.Media.Brushes.Gray,
                Height = 26
            };
            _txtSearchParameters.GotFocus += (s, e) =>
            {
                if (_txtSearchParameters.Text == "Поиск по имени параметра...") { _txtSearchParameters.Text = ""; _txtSearchParameters.Foreground = System.Windows.Media.Brushes.White; }
            };
            _txtSearchParameters.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_txtSearchParameters.Text)) { _txtSearchParameters.Text = "Поиск по имени параметра..."; _txtSearchParameters.Foreground = System.Windows.Media.Brushes.Gray; }
            };
            _txtSearchParameters.TextChanged += (s, e) => ApplyParamSearchFilter();
            System.Windows.Controls.Grid.SetRow(_txtSearchParameters, 1);
            rightGrid.Children.Add(_txtSearchParameters);

            StackPanel paramButtonsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
            System.Windows.Controls.Grid.SetRow(paramButtonsPanel, 2);
            rightGrid.Children.Add(paramButtonsPanel);

            System.Windows.Controls.Button btnSelectAllParam = new System.Windows.Controls.Button { Content = "Выбрать все", Style = buttonStyle, Height = 22, FontSize = 11, Margin = new Thickness(0, 0, 5, 0) };
            btnSelectAllParam.Click += (s, e) => SetParamsSelection(true);
            paramButtonsPanel.Children.Add(btnSelectAllParam);

            System.Windows.Controls.Button btnDeselectAllParam = new System.Windows.Controls.Button { Content = "Снять все", Style = buttonStyle, Height = 22, FontSize = 11, Margin = new Thickness(0, 0, 5, 0) };
            btnDeselectAllParam.Click += (s, e) => SetParamsSelection(false);
            paramButtonsPanel.Children.Add(btnDeselectAllParam);

            System.Windows.Controls.Button btnInvertParam = new System.Windows.Controls.Button { Content = "Инвертировать", Style = buttonStyle, Height = 22, FontSize = 11 };
            btnInvertParam.Click += (s, e) => InvertParamsSelection();
            paramButtonsPanel.Children.Add(btnInvertParam);

            _lvParameters = CreateParametersListView();
            System.Windows.Controls.Grid.SetRow(_lvParameters, 3);
            rightGrid.Children.Add(_lvParameters);

            // 4. Подвал (Прогресс + Кнопки)
            Border footerBorder = new Border
            {
                Background = panelBg,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(15, 10, 15, 10)
            };
            System.Windows.Controls.Grid.SetRow(footerBorder, 3);
            rootGrid.Children.Add(footerBorder);

            System.Windows.Controls.Grid footerGrid = new System.Windows.Controls.Grid();
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
            footerBorder.Child = footerGrid;

            StackPanel statusPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            _txtStatus = new TextBlock
            {
                Text = "Готово. Нажмите «Сканировать параметры» для сбора параметров из семейств.",
                Foreground = textMuted,
                FontSize = 11.5,
                Margin = new Thickness(0, 0, 0, 4)
            };
            statusPanel.Children.Add(_txtStatus);

            _progressBar = new System.Windows.Controls.ProgressBar
            {
                Height = 10,
                Foreground = activeBorderBrush,
                Background = controlBg,
                BorderBrush = borderBrush,
                Visibility = System.Windows.Visibility.Collapsed
            };
            statusPanel.Children.Add(_progressBar);
            System.Windows.Controls.Grid.SetColumn(statusPanel, 0);
            footerGrid.Children.Add(statusPanel);

            _btnDelete = new System.Windows.Controls.Button
            {
                Content = "🗑 Удалить параметры",
                Background = scarletBtn,
                Foreground = System.Windows.Media.Brushes.White,
                FontWeight = FontWeights.Bold,
                Height = 40,
                Cursor = System.Windows.Input.Cursors.Hand,
                FontSize = 13,
                Margin = new Thickness(15, 0, 0, 0)
            };
            _btnDelete.Click += BtnDelete_Click;
            System.Windows.Controls.Grid.SetColumn(_btnDelete, 1);
            footerGrid.Children.Add(_btnDelete);
        }

        private ListView CreateFamiliesListView()
        {
            ListView lv = new ListView
            {
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = System.Windows.Media.Brushes.White
            };

            GridView gv = new GridView();

            // Чекбокс
            FrameworkElementFactory chkFactory = new FrameworkElementFactory(typeof(System.Windows.Controls.CheckBox));
            chkFactory.SetBinding(System.Windows.Controls.CheckBox.IsCheckedProperty, new System.Windows.Data.Binding("IsSelected") { Mode = BindingMode.TwoWay });
            chkFactory.SetValue(System.Windows.Controls.CheckBox.VerticalAlignmentProperty, VerticalAlignment.Center);
            chkFactory.SetValue(System.Windows.Controls.CheckBox.MarginProperty, new Thickness(2, 0, 2, 0));
            DataTemplate chkTemplate = new DataTemplate { VisualTree = chkFactory };
            gv.Columns.Add(new GridViewColumn { Header = "✓", CellTemplate = chkTemplate, Width = 35 });

            // Название семейства
            gv.Columns.Add(new GridViewColumn { Header = "Имя семейства", DisplayMemberBinding = new System.Windows.Data.Binding("Name"), Width = 230 });

            // Категория
            gv.Columns.Add(new GridViewColumn { Header = "Категория", DisplayMemberBinding = new System.Windows.Data.Binding("CategoryName"), Width = 140 });

            // Типов
            gv.Columns.Add(new GridViewColumn { Header = "Типоразмеров", DisplayMemberBinding = new System.Windows.Data.Binding("SymbolsCount"), Width = 90 });

            lv.View = gv;
            return lv;
        }

        private ListView CreateParametersListView()
        {
            ListView lv = new ListView
            {
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = System.Windows.Media.Brushes.White
            };

            GridView gv = new GridView();

            // Чекбокс
            FrameworkElementFactory chkFactory = new FrameworkElementFactory(typeof(System.Windows.Controls.CheckBox));
            chkFactory.SetBinding(System.Windows.Controls.CheckBox.IsCheckedProperty, new System.Windows.Data.Binding("IsSelected") { Mode = BindingMode.TwoWay });
            chkFactory.SetValue(System.Windows.Controls.CheckBox.VerticalAlignmentProperty, VerticalAlignment.Center);
            chkFactory.SetValue(System.Windows.Controls.CheckBox.MarginProperty, new Thickness(2, 0, 2, 0));
            DataTemplate chkTemplate = new DataTemplate { VisualTree = chkFactory };
            gv.Columns.Add(new GridViewColumn { Header = "✓", CellTemplate = chkTemplate, Width = 35 });

            // Имя параметра
            gv.Columns.Add(new GridViewColumn { Header = "Имя параметра", DisplayMemberBinding = new System.Windows.Data.Binding("Name"), Width = 220 });

            // Тип параметра
            gv.Columns.Add(new GridViewColumn { Header = "Тип", DisplayMemberBinding = new System.Windows.Data.Binding("ParamTypeDisplay"), Width = 80 });

            // Привязка
            gv.Columns.Add(new GridViewColumn { Header = "Привязка", DisplayMemberBinding = new System.Windows.Data.Binding("BindingDisplay"), Width = 85 });

            // Семейств
            gv.Columns.Add(new GridViewColumn { Header = "В семействах", DisplayMemberBinding = new System.Windows.Data.Binding("FamiliesCount"), Width = 85 });

            lv.View = gv;
            return lv;
        }

        private void PopulateCategories()
        {
            _cbCategoryFilter.Items.Clear();
            _cbCategoryFilter.Items.Add("<Все категории>");

            var categories = _allFamilies
                .GroupBy(f => f.CategoryName)
                .OrderBy(g => g.Key)
                .Select(g => $"{g.Key} ({g.Count()})")
                .ToList();

            foreach (var cat in categories)
            {
                _cbCategoryFilter.Items.Add(cat);
            }

            _cbCategoryFilter.SelectedIndex = 0;
        }

        private void ApplyDataSources()
        {
            _familiesView = CollectionViewSource.GetDefaultView(_allFamilies);
            _familiesView.Filter = FilterFamilyItem;
            _lvFamilies.ItemsSource = _familiesView;

            _paramsView = CollectionViewSource.GetDefaultView(_allParams);
            _paramsView.Filter = FilterParamItem;
            _lvParameters.ItemsSource = _paramsView;
        }

        private bool FilterFamilyItem(object item)
        {
            if (!(item is FamilyEntryItem fam)) return false;

            // Категория
            if (_cbCategoryFilter.SelectedIndex > 0)
            {
                string selectedCat = _cbCategoryFilter.SelectedItem.ToString();
                int idx = selectedCat.LastIndexOf(" (");
                if (idx > 0) selectedCat = selectedCat.Substring(0, idx);

                if (!fam.CategoryName.Equals(selectedCat, StringComparison.OrdinalIgnoreCase)) return false;
            }

            // Поиск
            string query = _txtSearchFamilies?.Text?.Trim();
            if (!string.IsNullOrEmpty(query) && query != "Поиск по названию семейства...")
            {
                if (fam.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0 &&
                    fam.CategoryName.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return false;
                }
            }

            return true;
        }

        private bool FilterParamItem(object item)
        {
            if (!(item is FamilyParamItem param)) return false;

            string query = _txtSearchParameters?.Text?.Trim();
            if (!string.IsNullOrEmpty(query) && query != "Поиск по имени параметра...")
            {
                if (param.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0 &&
                    param.ParamTypeDisplay.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return false;
                }
            }

            return true;
        }

        private void ApplyCategoryFilter()
        {
            _familiesView?.Refresh();
            UpdateHeaders();
        }

        private void ApplySearchFilter()
        {
            _familiesView?.Refresh();
            UpdateHeaders();
        }

        private void ApplyParamSearchFilter()
        {
            _paramsView?.Refresh();
            UpdateHeaders();
        }

        private void UpdateHeaders()
        {
            int famCount = _allFamilies.Count(f => FilterFamilyItem(f));
            _lblFamiliesHeader.Text = $"СЕМЕЙСТВА ПРОЕКТА ({famCount})";

            int paramCount = _allParams.Count(p => FilterParamItem(p));
            _lblParamsHeader.Text = $"ПАРАМЕТРЫ ДЛЯ УДАЛЕНИЯ ({paramCount})";
        }

        private void SetFamiliesSelection(bool select)
        {
            foreach (var item in _allFamilies)
            {
                if (FilterFamilyItem(item))
                {
                    item.IsSelected = select;
                }
            }
        }

        private void InvertFamiliesSelection()
        {
            foreach (var item in _allFamilies)
            {
                if (FilterFamilyItem(item))
                {
                    item.IsSelected = !item.IsSelected;
                }
            }
        }

        private void SetParamsSelection(bool select)
        {
            foreach (var item in _allParams)
            {
                if (FilterParamItem(item))
                {
                    item.IsSelected = select;
                }
            }
        }

        private void InvertParamsSelection()
        {
            foreach (var item in _allParams)
            {
                if (FilterParamItem(item))
                {
                    item.IsSelected = !item.IsSelected;
                }
            }
        }

        private void BtnScanParams_Click(object sender, RoutedEventArgs e)
        {
            var selectedFamilies = _allFamilies.Where(f => f.IsSelected && FilterFamilyItem(f)).ToList();
            if (selectedFamilies.Count == 0)
            {
                System.Windows.MessageBox.Show("Пожалуйста, отметьте хотя бы одно семейство для сканирования параметров.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _btnScanParams.IsEnabled = false;
            _btnDelete.IsEnabled = false;
            _progressBar.Visibility = System.Windows.Visibility.Visible;
            _progressBar.Minimum = 0;
            _progressBar.Maximum = selectedFamilies.Count;
            _progressBar.Value = 0;

            try
            {
                _txtStatus.Text = "Сканирование параметров в выбранных семействах...";
                DoEvents();

                _allParams = FamilyParameterService.ScanParameters(
                    _doc,
                    selectedFamilies,
                    (cur, total, msg) =>
                    {
                        _progressBar.Value = cur;
                        _txtStatus.Text = msg;
                        DoEvents();
                    });

                _paramsView = CollectionViewSource.GetDefaultView(_allParams);
                _paramsView.Filter = FilterParamItem;
                _lvParameters.ItemsSource = _paramsView;

                _txtStatus.Text = $"Сканирование завершено. Найдено уникальных параметров: {_allParams.Count}.";
                UpdateHeaders();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка при сканировании: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _btnScanParams.IsEnabled = true;
                _btnDelete.IsEnabled = true;
                _progressBar.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            var targetFamilies = _allFamilies.Where(f => f.IsSelected && FilterFamilyItem(f)).ToList();
            var targetParams = _allParams.Where(p => p.IsSelected && FilterParamItem(p)).Select(p => p.Name).ToList();

            if (targetFamilies.Count == 0)
            {
                System.Windows.MessageBox.Show("Не выбрано ни одного семейства для обработки.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (targetParams.Count == 0)
            {
                System.Windows.MessageBox.Show("Не отмечено ни одного параметра для удаления.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string paramListStr = string.Join("\n • ", targetParams.Take(10));
            if (targetParams.Count > 10) paramListStr += $"\n ... и еще {targetParams.Count - 10} параметров";

            MessageBoxResult confirm = System.Windows.MessageBox.Show(
                $"Вы действительно хотите удалить следующие параметры ({targetParams.Count} шт.):\n • {paramListStr}\n\nИз выбранных семейств ({targetFamilies.Count} шт.)?",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            _btnScanParams.IsEnabled = false;
            _btnDelete.IsEnabled = false;
            _progressBar.Visibility = System.Windows.Visibility.Visible;
            _progressBar.Minimum = 0;
            _progressBar.Maximum = targetFamilies.Count;
            _progressBar.Value = 0;

            try
            {
                DeletionResult result = FamilyParameterService.DeleteParameters(
                    _doc,
                    targetFamilies,
                    targetParams,
                    (cur, total, msg) =>
                    {
                        _progressBar.Value = cur;
                        _txtStatus.Text = msg;
                        DoEvents();
                    });

                _txtStatus.Text = $"Удаление завершено! Изменено семейств: {result.ModifiedFamilies}, удалено параметров: {result.DeletedParametersCount}.";

                string logMsg = $"Результаты пакетного удаления параметров:\n\n" +
                                $" • Обработано семейств: {result.ProcessedFamilies}\n" +
                                $" • Модифицировано семейств: {result.ModifiedFamilies}\n" +
                                $" • Всего удалено параметров: {result.DeletedParametersCount}\n\n";

                if (result.ErrorLogs.Count > 0)
                {
                    logMsg += "Предупреждения / Ошибки:\n" + string.Join("\n", result.ErrorLogs.Take(15));
                    if (result.ErrorLogs.Count > 15) logMsg += $"\n... и еще {result.ErrorLogs.Count - 15} записей.";
                }

                System.Windows.MessageBox.Show(logMsg, "Отчет об удалении", MessageBoxButton.OK, MessageBoxImage.Information);

                // Обновляем список параметров после удаления
                BtnScanParams_Click(sender, e);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Критическая ошибка при удалении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _btnScanParams.IsEnabled = true;
                _btnDelete.IsEnabled = true;
                _progressBar.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        private static void DoEvents()
        {
            System.Windows.Application.Current?.Dispatcher?.Invoke(
                System.Windows.Threading.DispatcherPriority.Background,
                new Action(delegate { }));
        }
    }
}
