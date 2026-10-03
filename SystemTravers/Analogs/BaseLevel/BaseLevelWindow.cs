using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Autodesk.Revit.DB;
using WpfGrid = System.Windows.Controls.Grid;

namespace BimboClub.Analogs.BaseLevel
{
    public class BaseLevelWindow : Window
    {
        private readonly Document _doc;
        private readonly List<Level> _allLevels;
        private readonly BaseLevelStorage _storage;

        private ComboBox _cbConfigurations;
        private ObservableCollection<BaseLevelRuleItem> _ruleItems;
        private DataGrid _dgRules;
        private CheckBox _chkOnlyVisibleLevels;
        private RadioButton _rbSelected;
        private RadioButton _rbActiveView;
        private RadioButton _rbEntireModel;

        public BaseLevelConfiguration CurrentConfiguration { get; private set; }
        public bool OnlyVisibleLevels => _chkOnlyVisibleLevels.IsChecked == true;
        public ProcessScope Scope
        {
            get
            {
                if (_rbSelected.IsChecked == true) return ProcessScope.SelectedElements;
                if (_rbEntireModel.IsChecked == true) return ProcessScope.EntireModel;
                return ProcessScope.ActiveView;
            }
        }

        public class LevelItem
        {
            public string DisplayName { get; set; }
            public LevelChoiceMode Mode { get; set; }
            public long LevelId { get; set; }

            public override string ToString() => DisplayName;
        }

        public class BaseLevelRuleItem
        {
            public string Category { get; set; } = "Все категории";
            public string ParamFilter { get; set; } = "";
            public LevelItem BaseLevel { get; set; }
            public LevelItem TopLevel { get; set; }
        }

        public BaseLevelWindow(Document doc, bool hasPreselected)
        {
            _doc = doc;
            _allLevels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();

            _storage = BaseLevelStorage.Load();
            _ruleItems = new ObservableCollection<BaseLevelRuleItem>();

            InitializeUi(hasPreselected);
            LoadConfigurationsToUi();
        }

        private List<LevelItem> GetBaseLevelOptions()
        {
            var list = new List<LevelItem>
            {
                new LevelItem { DisplayName = "Ближайший снизу", Mode = LevelChoiceMode.NearestBelow },
                new LevelItem { DisplayName = "Ближайший", Mode = LevelChoiceMode.Nearest },
                new LevelItem { DisplayName = "Ближайший сверху", Mode = LevelChoiceMode.NearestAbove },
                new LevelItem { DisplayName = "Не изменять", Mode = LevelChoiceMode.Keep }
            };

            foreach (var lvl in _allLevels)
            {
                list.Add(new LevelItem
                {
                    DisplayName = $"{lvl.Name} ({(lvl.Elevation * 304.8):0.##} мм)",
                    Mode = LevelChoiceMode.Specific,
#if NET8_0_OR_GREATER
                    LevelId = lvl.Id.Value
#else
                    LevelId = lvl.Id.IntegerValue
#endif
                });
            }

            return list;
        }

        private List<LevelItem> GetTopLevelOptions()
        {
            var list = new List<LevelItem>
            {
                new LevelItem { DisplayName = "Ближайший сверху", Mode = LevelChoiceMode.NearestAbove },
                new LevelItem { DisplayName = "Ближайший", Mode = LevelChoiceMode.Nearest },
                new LevelItem { DisplayName = "Ближайший снизу", Mode = LevelChoiceMode.NearestBelow },
                new LevelItem { DisplayName = "Не изменять", Mode = LevelChoiceMode.Keep },
                new LevelItem { DisplayName = "Не привязывать (свободно)", Mode = LevelChoiceMode.Unbind }
            };

            foreach (var lvl in _allLevels)
            {
                list.Add(new LevelItem
                {
                    DisplayName = $"{lvl.Name} ({(lvl.Elevation * 304.8):0.##} мм)",
                    Mode = LevelChoiceMode.Specific,
#if NET8_0_OR_GREATER
                    LevelId = lvl.Id.Value
#else
                    LevelId = lvl.Id.IntegerValue
#endif
                });
            }

            return list;
        }

        private void InitializeUi(bool hasPreselected)
        {
            Title = "Базовый уровень — BimboClub Tools";
            Width = 720;
            Height = 560;
            MinHeight = 450;
            MinWidth = 600;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(24, 24, 28));
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 13;

            var bgCard = new SolidColorBrush(System.Windows.Media.Color.FromRgb(32, 32, 38));
            var borderCard = new SolidColorBrush(System.Windows.Media.Color.FromRgb(48, 48, 56));
            var accentRed = new SolidColorBrush(System.Windows.Media.Color.FromRgb(179, 14, 45));
            var textMuted = new SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 180, 200));

            WpfGrid mainGrid = new WpfGrid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Preset Bar
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Rules Table
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Bottom Scope Bar
            Content = mainGrid;

            // 1. Шапка
            Border headerBorder = new Border
            {
                Background = new LinearGradientBrush(
                    System.Windows.Media.Color.FromRgb(18, 18, 20),
                    System.Windows.Media.Color.FromRgb(179, 14, 45), 0),
                Padding = new Thickness(16, 12, 16, 12)
            };
            StackPanel headerStack = new StackPanel();
            headerStack.Children.Add(new TextBlock
            {
                Text = "БАЗОВЫЙ УРОВЕНЬ",
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Foreground = Brushes.White
            });
            headerStack.Children.Add(new TextBlock
            {
                Text = "Перепривязка элементов к базовым и верхним уровням без смещения геометрии в пространстве",
                FontSize = 11,
                Foreground = textMuted,
                Margin = new Thickness(0, 3, 0, 0)
            });
            headerBorder.Child = headerStack;
            WpfGrid.SetRow(headerBorder, 0);
            mainGrid.Children.Add(headerBorder);

            // 2. Панель конфигураций / пресетов
            Border presetBorder = new Border
            {
                Background = bgCard,
                BorderBrush = borderCard,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(14, 8, 14, 8)
            };
            WpfGrid presetGrid = new WpfGrid();
            presetGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            presetGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            presetGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock lblPreset = new TextBlock
            {
                Text = "Конфигурация:",
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            WpfGrid.SetColumn(lblPreset, 0);
            presetGrid.Children.Add(lblPreset);

            _cbConfigurations = new ComboBox
            {
                Height = 28,
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 53)),
                Foreground = Brushes.Black,
                Margin = new Thickness(0, 0, 10, 0)
            };
            _cbConfigurations.SelectionChanged += (s, e) => OnConfigurationSelected();
            WpfGrid.SetColumn(_cbConfigurations, 1);
            presetGrid.Children.Add(_cbConfigurations);

            StackPanel btnConfigStack = new StackPanel { Orientation = Orientation.Horizontal };
            Button btnAddConfig = CreateIconButton("+", "Добавить новую конфигурацию", (s, e) => AddConfiguration());
            Button btnDupConfig = CreateIconButton("⎘", "Дублировать текущую", (s, e) => DuplicateConfiguration());
            Button btnRenConfig = CreateIconButton("✎", "Переименовать", (s, e) => RenameConfiguration());
            Button btnDelConfig = CreateIconButton("✕", "Удалить конфигурацию", (s, e) => DeleteConfiguration());

            btnConfigStack.Children.Add(btnAddConfig);
            btnConfigStack.Children.Add(btnDupConfig);
            btnConfigStack.Children.Add(btnRenConfig);
            btnConfigStack.Children.Add(btnDelConfig);

            WpfGrid.SetColumn(btnConfigStack, 2);
            presetGrid.Children.Add(btnConfigStack);

            presetBorder.Child = presetGrid;
            WpfGrid.SetRow(presetBorder, 1);
            mainGrid.Children.Add(presetBorder);

            // 3. Таблица правил
            WpfGrid rulesGrid = new WpfGrid();
            rulesGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rulesGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _dgRules = new DataGrid
            {
                ItemsSource = _ruleItems,
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(28, 28, 34)),
                RowBackground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(32, 32, 38)),
                AlternatingRowBackground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(36, 36, 44)),
                Foreground = Brushes.White,
                BorderBrush = borderCard,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HorizontalGridLinesBrush = borderCard,
                Margin = new Thickness(14, 10, 14, 6)
            };

            // Столбец: Категория
            var colCat = new DataGridTextColumn
            {
                Header = "Категория",
                Binding = new System.Windows.Data.Binding("Category"),
                Width = new DataGridLength(140)
            };
            _dgRules.Columns.Add(colCat);

            // Столбец: Фильтр по параметру
            var colParam = new DataGridTextColumn
            {
                Header = "Фильтр (Имя:Значение)",
                Binding = new System.Windows.Data.Binding("ParamFilter"),
                Width = new DataGridLength(140)
            };
            _dgRules.Columns.Add(colParam);

            // Столбец: Базовый уровень
            var baseOptions = GetBaseLevelOptions();
            var colBase = new DataGridComboBoxColumn
            {
                Header = "Базовый уровень",
                ItemsSource = baseOptions,
                SelectedItemBinding = new System.Windows.Data.Binding("BaseLevel"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            };
            _dgRules.Columns.Add(colBase);

            // Столбец: Верхний уровень
            var topOptions = GetTopLevelOptions();
            var colTop = new DataGridComboBoxColumn
            {
                Header = "Верхний уровень",
                ItemsSource = topOptions,
                SelectedItemBinding = new System.Windows.Data.Binding("TopLevel"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            };
            _dgRules.Columns.Add(colTop);

            WpfGrid.SetRow(_dgRules, 0);
            rulesGrid.Children.Add(_dgRules);

            // Панель добавления/удаления строк
            StackPanel ruleBtnStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(14, 0, 14, 8)
            };
            Button btnAddRow = new Button
            {
                Content = "+ Добавить правило",
                Height = 26,
                Padding = new Thickness(10, 2, 10, 2),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(48, 52, 60)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 8, 0)
            };
            btnAddRow.Click += (s, e) =>
            {
                _ruleItems.Add(new BaseLevelRuleItem
                {
                    Category = "Все категории",
                    BaseLevel = baseOptions[0],
                    TopLevel = topOptions[3]
                });
            };

            Button btnRemoveRow = new Button
            {
                Content = "— Удалить правило",
                Height = 26,
                Padding = new Thickness(10, 2, 10, 2),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(48, 52, 60)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btnRemoveRow.Click += (s, e) =>
            {
                if (_dgRules.SelectedItem is BaseLevelRuleItem item)
                {
                    _ruleItems.Remove(item);
                }
            };

            ruleBtnStack.Children.Add(btnAddRow);
            ruleBtnStack.Children.Add(btnRemoveRow);
            WpfGrid.SetRow(ruleBtnStack, 1);
            rulesGrid.Children.Add(ruleBtnStack);

            WpfGrid.SetRow(rulesGrid, 2);
            mainGrid.Children.Add(rulesGrid);

            // 4. Подвал (Область обработки + Установить уровни)
            Border footerBorder = new Border
            {
                Background = bgCard,
                BorderBrush = borderCard,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(14, 10, 14, 12)
            };
            WpfGrid footerGrid = new WpfGrid();
            footerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            footerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Чекбокс видимости
            _chkOnlyVisibleLevels = new CheckBox
            {
                Content = "Учитывать только уровни, видимые на текущем виде",
                IsChecked = false,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 10),
                Cursor = Cursors.Hand
            };
            WpfGrid.SetRow(_chkOnlyVisibleLevels, 0);
            footerGrid.Children.Add(_chkOnlyVisibleLevels);

            // Нижний ряд: Радиокнопки области и кнопка запуска
            WpfGrid actionGrid = new WpfGrid();
            actionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            actionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel scopeStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _rbSelected = new RadioButton
            {
                Content = "Выбранные",
                IsChecked = hasPreselected,
                IsEnabled = hasPreselected,
                Foreground = hasPreselected ? Brushes.White : textMuted,
                Margin = new Thickness(0, 0, 16, 0),
                Cursor = Cursors.Hand
            };
            _rbActiveView = new RadioButton
            {
                Content = "На активном виде",
                IsChecked = !hasPreselected,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 16, 0),
                Cursor = Cursors.Hand
            };
            _rbEntireModel = new RadioButton
            {
                Content = "Во всей модели",
                Foreground = Brushes.White,
                Cursor = Cursors.Hand
            };
            scopeStack.Children.Add(_rbSelected);
            scopeStack.Children.Add(_rbActiveView);
            scopeStack.Children.Add(_rbEntireModel);
            WpfGrid.SetColumn(scopeStack, 0);
            actionGrid.Children.Add(scopeStack);

            Button btnApply = new Button
            {
                Content = "Установить уровни",
                Height = 34,
                Padding = new Thickness(20, 0, 20, 0),
                Background = accentRed,
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btnApply.Click += (s, e) =>
            {
                SaveCurrentUiToConfig();
                _storage.Save();
                DialogResult = true;
            };
            WpfGrid.SetColumn(btnApply, 1);
            actionGrid.Children.Add(btnApply);

            WpfGrid.SetRow(actionGrid, 1);
            footerGrid.Children.Add(actionGrid);

            footerBorder.Child = footerGrid;
            WpfGrid.SetRow(footerBorder, 3);
            mainGrid.Children.Add(footerBorder);
        }

        private Button CreateIconButton(string icon, string tooltip, RoutedEventHandler handler)
        {
            var btn = new Button
            {
                Content = icon,
                Width = 26,
                Height = 26,
                Margin = new Thickness(2, 0, 2, 0),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(48, 52, 60)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                ToolTip = tooltip,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btn.Click += handler;
            return btn;
        }

        private void LoadConfigurationsToUi()
        {
            _cbConfigurations.Items.Clear();
            foreach (var cfg in _storage.Configurations)
            {
                _cbConfigurations.Items.Add(cfg.Name);
            }

            int idx = _storage.Configurations.FindIndex(c => c.Name == _storage.SelectedConfigurationName);
            _cbConfigurations.SelectedIndex = idx >= 0 ? idx : 0;
        }

        private void OnConfigurationSelected()
        {
            string name = _cbConfigurations.SelectedItem as string;
            if (string.IsNullOrEmpty(name)) return;

            CurrentConfiguration = _storage.Configurations.FirstOrDefault(c => c.Name == name);
            if (CurrentConfiguration == null) return;

            _storage.SelectedConfigurationName = name;
            _ruleItems.Clear();

            var baseOpts = GetBaseLevelOptions();
            var topOpts = GetTopLevelOptions();

            foreach (var r in CurrentConfiguration.Rules)
            {
                LevelItem baseItem = baseOpts.FirstOrDefault(b => b.Mode == r.BaseMode && (r.BaseMode != LevelChoiceMode.Specific || b.LevelId == r.SpecificBaseLevelId))
                                     ?? baseOpts[0];
                LevelItem topItem = topOpts.FirstOrDefault(t => t.Mode == r.TopMode && (r.TopMode != LevelChoiceMode.Specific || t.LevelId == r.SpecificTopLevelId))
                                    ?? topOpts[3];

                string pFilter = "";
                if (!string.IsNullOrEmpty(r.ParameterName))
                {
                    pFilter = $"{r.ParameterName}:{r.ParameterValue}";
                }

                _ruleItems.Add(new BaseLevelRuleItem
                {
                    Category = r.CategoryName,
                    ParamFilter = pFilter,
                    BaseLevel = baseItem,
                    TopLevel = topItem
                });
            }
        }

        private void SaveCurrentUiToConfig()
        {
            if (CurrentConfiguration == null) return;

            CurrentConfiguration.Rules.Clear();
            foreach (var item in _ruleItems)
            {
                var rule = new BaseLevelRule
                {
                    CategoryName = item.Category,
                    BaseMode = item.BaseLevel?.Mode ?? LevelChoiceMode.NearestBelow,
                    SpecificBaseLevelId = item.BaseLevel?.LevelId ?? -1,
                    TopMode = item.TopLevel?.Mode ?? LevelChoiceMode.Keep,
                    SpecificTopLevelId = item.TopLevel?.LevelId ?? -1
                };

                if (!string.IsNullOrEmpty(item.ParamFilter))
                {
                    var parts = item.ParamFilter.Split(':');
                    rule.ParameterName = parts[0].Trim();
                    if (parts.Length > 1) rule.ParameterValue = parts[1].Trim();
                }

                CurrentConfiguration.Rules.Add(rule);
            }
        }

        private void AddConfiguration()
        {
            string newName = "Конфигурация " + (_storage.Configurations.Count + 1);
            var cfg = new BaseLevelConfiguration { Name = newName };
            cfg.Rules.Add(new BaseLevelRule { CategoryName = "Стены", BaseMode = LevelChoiceMode.NearestBelow, TopMode = LevelChoiceMode.NearestAbove });
            _storage.Configurations.Add(cfg);
            LoadConfigurationsToUi();
            _cbConfigurations.SelectedItem = newName;
        }

        private void DuplicateConfiguration()
        {
            if (CurrentConfiguration == null) return;
            string newName = CurrentConfiguration.Name + " (копия)";
            var clone = new BaseLevelConfiguration
            {
                Name = newName,
                Rules = CurrentConfiguration.Rules.Select(r => new BaseLevelRule
                {
                    CategoryName = r.CategoryName,
                    ParameterName = r.ParameterName,
                    ParameterValue = r.ParameterValue,
                    BaseMode = r.BaseMode,
                    SpecificBaseLevelId = r.SpecificBaseLevelId,
                    TopMode = r.TopMode,
                    SpecificTopLevelId = r.SpecificTopLevelId
                }).ToList()
            };
            _storage.Configurations.Add(clone);
            LoadConfigurationsToUi();
            _cbConfigurations.SelectedItem = newName;
        }

        private void RenameConfiguration()
        {
            if (CurrentConfiguration == null) return;
            string input = ShowInputDialog("Введите новое имя конфигурации:", "Переименовать", CurrentConfiguration.Name);
            if (!string.IsNullOrWhiteSpace(input))
            {
                CurrentConfiguration.Name = input.Trim();
                _storage.Save();
                LoadConfigurationsToUi();
                _cbConfigurations.SelectedItem = CurrentConfiguration.Name;
            }
        }

        private static string ShowInputDialog(string prompt, string title, string defaultText)
        {
            Window promptWin = new Window
            {
                Title = title,
                Width = 350,
                SizeToContent = SizeToContent.Height,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(32, 32, 38)),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13
            };

            StackPanel sp = new StackPanel { Margin = new Thickness(14) };
            sp.Children.Add(new TextBlock { Text = prompt, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 8) });

            System.Windows.Controls.TextBox tb = new System.Windows.Controls.TextBox
            {
                Text = defaultText,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 53)),
                Foreground = Brushes.White,
                Padding = new Thickness(6, 4, 6, 4),
                Margin = new Thickness(0, 0, 0, 14)
            };
            sp.Children.Add(tb);

            StackPanel btnSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            Button btnCancel = new Button { Content = "Отмена", Width = 80, Height = 28, Margin = new Thickness(0, 0, 8, 0) };
            btnCancel.Click += (s, e) => promptWin.DialogResult = false;

            Button btnOk = new Button
            {
                Content = "OK",
                Width = 80,
                Height = 28,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(179, 14, 45)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold
            };
            btnOk.Click += (s, e) => promptWin.DialogResult = true;

            btnSp.Children.Add(btnCancel);
            btnSp.Children.Add(btnOk);
            sp.Children.Add(btnSp);
            promptWin.Content = sp;

            if (promptWin.ShowDialog() == true)
            {
                return tb.Text;
            }
            return null;
        }

        private void DeleteConfiguration()
        {
            if (_storage.Configurations.Count <= 1)
            {
                MessageBox.Show("Нельзя удалить единственную конфигурацию.", "Удаление", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show($"Удалить конфигурацию '{CurrentConfiguration.Name}'?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _storage.Configurations.Remove(CurrentConfiguration);
                _storage.Save();
                LoadConfigurationsToUi();
            }
        }
    }
}
