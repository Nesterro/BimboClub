using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Input;

using ElementId = Autodesk.Revit.DB.ElementId;
using BuiltInCategory = Autodesk.Revit.DB.BuiltInCategory;
using Document = Autodesk.Revit.DB.Document;
using View = Autodesk.Revit.DB.View;
using FilteredElementCollector = Autodesk.Revit.DB.FilteredElementCollector;
using ElementMulticategoryFilter = Autodesk.Revit.DB.ElementMulticategoryFilter;
using Element = Autodesk.Revit.DB.Element;
using ElementType = Autodesk.Revit.DB.ElementType;
using FamilyInstance = Autodesk.Revit.DB.FamilyInstance;

namespace BimboClub
{
    public class TagTypeItem
    {
        public string DisplayName { get; set; }
        public ElementId Id { get; set; }

        public override string ToString()
        {
            return DisplayName;
        }
    }

    public class CategoryTagConfig : INotifyPropertyChanged
    {
        private bool _isChecked;
        private TagTypeItem _selectedTag;

        public string CategoryName { get; set; }
        public BuiltInCategory Category { get; set; }
        
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    OnPropertyChanged(nameof(IsChecked));
                }
            }
        }

        public List<TagTypeItem> AvailableTags { get; set; }

        public TagTypeItem SelectedTag
        {
            get => _selectedTag;
            set
            {
                if (_selectedTag != value)
                {
                    _selectedTag = value;
                    OnPropertyChanged(nameof(SelectedTag));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class FamilyCheckItem : INotifyPropertyChanged
    {
        private bool _isChecked = true;
        public string FamilyName { get; set; }
        public BuiltInCategory Category { get; set; }

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    OnPropertyChanged(nameof(IsChecked));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class TagSelectionWindow : Window
    {
        public List<CategoryTagConfig> Configurations { get; private set; }

        private Document _doc;
        private View _activeView;
        private List<FamilyCheckItem> _allFamilies = new List<FamilyCheckItem>();

        private ListBox _lbCategories;
        private ListBox _lbTags;
        private ListBox _lbFamilies;
        private TextBox _txtFamilySearch;
        private TextBlock _txtRightHeader;
        private Button _btnOk;

        public List<string> SelectedFamilies => _allFamilies.Where(f => f.IsChecked).Select(f => f.FamilyName).ToList();

        public TagSelectionWindow(List<CategoryTagConfig> configs, Document doc, View activeView)
        {
            Configurations = configs;
            _doc = doc;
            _activeView = activeView;
            InitializeComponent();
            PopulateAllFamilies();
            RefreshFamiliesFilter();
        }

        private Style GetRoundedButtonStyle(double radius)
        {
            Style style = new Style(typeof(Button));
            ControlTemplate template = new ControlTemplate(typeof(Button));
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
            border.SetValue(Border.BackgroundProperty, new Binding("Background") { RelativeSource = RelativeSource.TemplatedParent });
            border.SetValue(Border.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = RelativeSource.TemplatedParent });
            border.SetValue(Border.BorderThicknessProperty, new Binding("BorderThickness") { RelativeSource = RelativeSource.TemplatedParent });
            
            FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);
            
            template.VisualTree = border;
            style.Setters.Add(new Setter(Button.TemplateProperty, template));
            return style;
        }

        private void ApplyButtonStyles(Button btn, LinearGradientBrush normalGrad, LinearGradientBrush hoverGrad, Style roundedStyle)
        {
            btn.Style = roundedStyle;
            btn.Background = normalGrad;
            btn.MouseEnter += (s, e) => { if (btn.IsEnabled) btn.Background = hoverGrad; };
            btn.MouseLeave += (s, e) => { if (btn.IsEnabled) btn.Background = normalGrad; };
            btn.IsEnabledChanged += (s, ev) =>
            {
                if (btn.IsEnabled)
                    btn.Background = normalGrad;
                else
                    btn.Background = new SolidColorBrush(Color.FromRgb(70, 70, 82));
            };
        }

        private void ApplyFlatButtonStyles(Button btn, SolidColorBrush normalBrush, SolidColorBrush hoverBrush, Style roundedStyle)
        {
            btn.Style = roundedStyle;
            btn.Background = normalBrush;
            btn.MouseEnter += (s, e) => { if (btn.IsEnabled) btn.Background = hoverBrush; };
            btn.MouseLeave += (s, e) => { if (btn.IsEnabled) btn.Background = normalBrush; };
        }

        private void InitializeComponent()
        {
            this.Title = "Автоматическая расстановка выносок - BimboClub Tools";
            this.Width = 850;
            this.Height = 550;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.Background = new SolidColorBrush(Color.FromRgb(24, 24, 28)); // #18181C
            this.FontFamily = new FontFamily("Segoe UI");
            this.FontSize = 13;
            this.MinHeight = 450;
            this.MinWidth = 750;

            // Цветовая палитра
            SolidColorBrush panelBg = new SolidColorBrush(Color.FromRgb(32, 32, 38)); // #202026
            SolidColorBrush borderBrush = new SolidColorBrush(Color.FromRgb(48, 48, 56)); // #303038
            SolidColorBrush activeBorderBrush = new SolidColorBrush(Color.FromRgb(255, 46, 91)); // #FF2E5B (Scarlet)
            SolidColorBrush cancelNormal = new SolidColorBrush(Color.FromRgb(58, 58, 66));
            SolidColorBrush cancelHover = new SolidColorBrush(Color.FromRgb(78, 78, 88));

            Style checkBoxStyle = new Style(typeof(CheckBox));
            checkBoxStyle.Setters.Add(new Setter(CheckBox.ForegroundProperty, Brushes.White));
            checkBoxStyle.Setters.Add(new Setter(CheckBox.VerticalAlignmentProperty, VerticalAlignment.Center));

            Style roundedButtonStyle = GetRoundedButtonStyle(5);

            Grid mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });    // Заголовок
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Контент (Три колонки)
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });    // Кнопки
            this.Content = mainGrid;

            // 1. Заголовок
            Border headerBorder = new Border
            {
                Background = new LinearGradientBrush(
                    Color.FromRgb(18, 18, 20),
                    Color.FromRgb(179, 14, 45), // #B30E2D (Бренд алый)
                    0.0),
                Padding = new Thickness(15, 10, 15, 10)
            };
            Grid.SetRow(headerBorder, 0);
            mainGrid.Children.Add(headerBorder);

            TextBlock headerText = new TextBlock
            {
                Text = "АВТОМАТИЧЕСКАЯ РАССТАНОВКА ВЫНОСОК (МАРОК)",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 15,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerBorder.Child = headerText;

            // 2. Три колонки контента
            Grid contentGrid = new Grid { Margin = new Thickness(15, 10, 15, 10) };
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) }); // Категории
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });  // Разделитель
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) }); // Выноски
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });  // Разделитель
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Семейства
            Grid.SetRow(contentGrid, 1);
            mainGrid.Children.Add(contentGrid);

            // КОЛОНКА 1: Категории для подписи
            Grid leftPanelGrid = new Grid();
            leftPanelGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
            leftPanelGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
            leftPanelGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(leftPanelGrid, 0);
            contentGrid.Children.Add(leftPanelGrid);

            TextBlock txtLeftHeader = new TextBlock
            {
                Text = "1. Категории для подписи:",
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(txtLeftHeader, 0);
            leftPanelGrid.Children.Add(txtLeftHeader);

            StackPanel selectButtonsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
            Grid.SetRow(selectButtonsPanel, 1);
            leftPanelGrid.Children.Add(selectButtonsPanel);

            Button btnSelectAll = new Button
            {
                Content = "Выбрать все",
                Foreground = Brushes.White,
                Width = 110,
                Height = 26,
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand,
                FontSize = 11
            };
            btnSelectAll.Click += (s, e) =>
            {
                foreach (var config in Configurations)
                {
                    config.IsChecked = true;
                }
            };
            ApplyFlatButtonStyles(btnSelectAll, cancelNormal, cancelHover, roundedButtonStyle);
            selectButtonsPanel.Children.Add(btnSelectAll);

            Button btnDeselectAll = new Button
            {
                Content = "Снять все",
                Foreground = Brushes.White,
                Width = 110,
                Height = 26,
                Cursor = Cursors.Hand,
                FontSize = 11
            };
            btnDeselectAll.Click += (s, e) =>
            {
                foreach (var config in Configurations)
                {
                    config.IsChecked = false;
                }
            };
            ApplyFlatButtonStyles(btnDeselectAll, cancelNormal, cancelHover, roundedButtonStyle);
            selectButtonsPanel.Children.Add(btnDeselectAll);

            Border leftListBorder = new Border
            {
                Background = panelBg,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5)
            };
            Grid.SetRow(leftListBorder, 2);
            leftPanelGrid.Children.Add(leftListBorder);

            _lbCategories = new ListBox
            {
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Foreground = Brushes.White,
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            leftListBorder.Child = _lbCategories;

            DataTemplate catTemplate = new DataTemplate();
            FrameworkElementFactory catGridFactory = new FrameworkElementFactory(typeof(Grid));
            catGridFactory.SetValue(Grid.MarginProperty, new Thickness(5, 5, 5, 5));

            FrameworkElementFactory chk = new FrameworkElementFactory(typeof(CheckBox));
            chk.SetBinding(CheckBox.IsCheckedProperty, new Binding("IsChecked") { Mode = BindingMode.TwoWay });
            chk.SetValue(CheckBox.VerticalAlignmentProperty, VerticalAlignment.Center);
            chk.SetValue(CheckBox.ForegroundProperty, Brushes.White);

            FrameworkElementFactory txtName = new FrameworkElementFactory(typeof(TextBlock));
            txtName.SetBinding(TextBlock.TextProperty, new Binding("CategoryName"));
            txtName.SetValue(TextBlock.MarginProperty, new Thickness(25, 0, 0, 0));
            txtName.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            txtName.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            txtName.SetValue(TextBlock.ForegroundProperty, Brushes.White);

            catGridFactory.AppendChild(chk);
            catGridFactory.AppendChild(txtName);
            catTemplate.VisualTree = catGridFactory;
            _lbCategories.ItemTemplate = catTemplate;
            _lbCategories.ItemsSource = Configurations;
            _lbCategories.SelectionChanged += LbCategories_SelectionChanged;

            // КОЛОНКА 2: Выноски
            Grid rightPanelGrid = new Grid();
            rightPanelGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
            rightPanelGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) }); // placeholder to align top with left column
            rightPanelGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(rightPanelGrid, 2);
            contentGrid.Children.Add(rightPanelGrid);

            _txtRightHeader = new TextBlock
            {
                Text = "2. Выберите марку:",
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(_txtRightHeader, 0);
            rightPanelGrid.Children.Add(_txtRightHeader);

            Border rightListBorder = new Border
            {
                Background = panelBg,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5)
            };
            Grid.SetRow(rightListBorder, 2);
            rightPanelGrid.Children.Add(rightListBorder);

            _lbTags = new ListBox
            {
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Foreground = Brushes.White,
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            rightListBorder.Child = _lbTags;
            _lbTags.SelectionChanged += LbTags_SelectionChanged;

            // КОЛОНКА 3: Семейства для маркировки
            Grid familiesPanelGrid = new Grid();
            familiesPanelGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) }); // Заголовок
            familiesPanelGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) }); // Поиск
            familiesPanelGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) }); // Кнопки
            familiesPanelGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Список
            Grid.SetColumn(familiesPanelGrid, 4);
            contentGrid.Children.Add(familiesPanelGrid);

            TextBlock txtFamHeader = new TextBlock
            {
                Text = "3. Выберите семейства для маркировки:",
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(txtFamHeader, 0);
            familiesPanelGrid.Children.Add(txtFamHeader);

            _txtFamilySearch = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 53)),
                Foreground = Brushes.White,
                BorderBrush = borderBrush,
                Padding = new Thickness(5, 2, 5, 2),
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Введите имя семейства для фильтрации списка"
            };
            _txtFamilySearch.TextChanged += (s, e) => RefreshFamiliesFilter();
            Grid.SetRow(_txtFamilySearch, 1);
            familiesPanelGrid.Children.Add(_txtFamilySearch);

            StackPanel famButtonsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
            Grid.SetRow(famButtonsPanel, 2);
            familiesPanelGrid.Children.Add(famButtonsPanel);

            Button btnSelectAllFam = new Button
            {
                Content = "Выбрать все",
                Foreground = Brushes.White,
                Width = 110,
                Height = 26,
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand,
                FontSize = 11
            };
            btnSelectAllFam.Click += (s, e) =>
            {
                var currentItems = _lbFamilies.ItemsSource as IEnumerable<FamilyCheckItem>;
                if (currentItems != null)
                {
                    foreach (var item in currentItems)
                    {
                        item.IsChecked = true;
                    }
                }
            };
            ApplyFlatButtonStyles(btnSelectAllFam, cancelNormal, cancelHover, roundedButtonStyle);
            famButtonsPanel.Children.Add(btnSelectAllFam);

            Button btnDeselectAllFam = new Button
            {
                Content = "Снять все",
                Foreground = Brushes.White,
                Width = 110,
                Height = 26,
                Cursor = Cursors.Hand,
                FontSize = 11
            };
            btnDeselectAllFam.Click += (s, e) =>
            {
                var currentItems = _lbFamilies.ItemsSource as IEnumerable<FamilyCheckItem>;
                if (currentItems != null)
                {
                    foreach (var item in currentItems)
                    {
                        item.IsChecked = false;
                    }
                }
            };
            ApplyFlatButtonStyles(btnDeselectAllFam, cancelNormal, cancelHover, roundedButtonStyle);
            famButtonsPanel.Children.Add(btnDeselectAllFam);

            Border famListBorder = new Border
            {
                Background = panelBg,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5)
            };
            Grid.SetRow(famListBorder, 3);
            familiesPanelGrid.Children.Add(famListBorder);

            _lbFamilies = new ListBox
            {
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Foreground = Brushes.White,
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            famListBorder.Child = _lbFamilies;

            DataTemplate famTemplate = new DataTemplate();
            FrameworkElementFactory famGridFactory = new FrameworkElementFactory(typeof(Grid));
            famGridFactory.SetValue(Grid.MarginProperty, new Thickness(5, 3, 5, 3));

            FrameworkElementFactory chkFam = new FrameworkElementFactory(typeof(CheckBox));
            chkFam.SetBinding(CheckBox.IsCheckedProperty, new Binding("IsChecked") { Mode = BindingMode.TwoWay });
            chkFam.SetValue(CheckBox.VerticalAlignmentProperty, VerticalAlignment.Center);
            chkFam.SetValue(CheckBox.ForegroundProperty, Brushes.White);

            FrameworkElementFactory txtFamName = new FrameworkElementFactory(typeof(TextBlock));
            txtFamName.SetBinding(TextBlock.TextProperty, new Binding("FamilyName"));
            txtFamName.SetValue(TextBlock.MarginProperty, new Thickness(25, 0, 0, 0));
            txtFamName.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            txtFamName.SetValue(TextBlock.ForegroundProperty, Brushes.White);
            txtFamName.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);

            famGridFactory.AppendChild(chkFam);
            famGridFactory.AppendChild(txtFamName);
            famTemplate.VisualTree = famGridFactory;
            _lbFamilies.ItemTemplate = famTemplate;

            // 4. Нижние кнопки
            Grid bottomGrid = new Grid { Margin = new Thickness(15, 10, 15, 10) };
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            Grid.SetRow(bottomGrid, 2);
            mainGrid.Children.Add(bottomGrid);

            // Кнопка ОК (Алый градиент)
            _btnOk = new Button
            {
                Content = "Маркировать элементы",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0),
                Height = 35,
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand
            };
            _btnOk.Click += BtnOk_Click;
            
            LinearGradientBrush normalGrad = new LinearGradientBrush(Color.FromRgb(255, 46, 91), Color.FromRgb(179, 14, 45), 90.0);
            LinearGradientBrush hoverGrad = new LinearGradientBrush(Color.FromRgb(255, 80, 118), Color.FromRgb(210, 20, 60), 90.0);
            ApplyButtonStyles(_btnOk, normalGrad, hoverGrad, roundedButtonStyle);
            bottomGrid.Children.Add(_btnOk);

            // Кнопка Отмена
            Button btnCancel = new Button
            {
                Content = "Отмена",
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Height = 35,
                Cursor = Cursors.Hand
            };
            btnCancel.Click += (s, e) => { this.DialogResult = false; this.Close(); };
            ApplyFlatButtonStyles(btnCancel, cancelNormal, cancelHover, roundedButtonStyle);
            Grid.SetColumn(btnCancel, 1);
            bottomGrid.Children.Add(btnCancel);

            // События изменения галочек
            foreach (var config in Configurations)
            {
                config.PropertyChanged += Config_PropertyChanged;
            }

            if (_lbCategories.Items.Count > 0)
            {
                _lbCategories.SelectedIndex = 0;
            }
            
            UpdateOkButtonText();
        }

        private void PopulateAllFamilies()
        {
            _allFamilies.Clear();
            if (_doc == null || _activeView == null) return;

            var categories = Configurations.Select(c => c.Category).ToList();
            if (categories.Count == 0) return;

            ElementMulticategoryFilter catFilter = new ElementMulticategoryFilter(categories);
            var elements = new FilteredElementCollector(_doc, _activeView.Id)
                .WherePasses(catFilter)
                .WhereElementIsNotElementType()
                .ToList();

            var familyNamesSet = new HashSet<(string, BuiltInCategory)>();

            foreach (var el in elements)
            {
                if (el.Category == null) continue;
                string famName = "";
                ElementId typeId = el.GetTypeId();
                if (typeId != ElementId.InvalidElementId)
                {
                    var elType = _doc.GetElement(typeId) as ElementType;
                    if (elType != null)
                    {
                        try { famName = elType.FamilyName ?? ""; } catch { }
                    }
                }
                if (string.IsNullOrEmpty(famName) && el is FamilyInstance fi && fi.Symbol?.Family != null)
                {
                    famName = fi.Symbol.Family.Name ?? "";
                }
                if (string.IsNullOrEmpty(famName))
                {
                    famName = el.Name ?? "";
                }

                if (!string.IsNullOrEmpty(famName))
                {
                    familyNamesSet.Add((famName, el.Category.BuiltInCategory));
                }
            }

            foreach (var item in familyNamesSet.OrderBy(x => x.Item1))
            {
                _allFamilies.Add(new FamilyCheckItem
                {
                    FamilyName = item.Item1,
                    Category = item.Item2,
                    IsChecked = true
                });
            }
        }

        private void RefreshFamiliesFilter()
        {
            if (_lbFamilies == null) return;

            var checkedCategories = Configurations.Where(c => c.IsChecked).Select(c => c.Category).ToHashSet();
            string searchText = _txtFamilySearch?.Text?.Trim() ?? "";

            var filtered = _allFamilies.Where(f =>
                checkedCategories.Contains(f.Category) &&
                (string.IsNullOrEmpty(searchText) || f.FamilyName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
            ).ToList();

            _lbFamilies.ItemsSource = filtered;
        }

        private void Config_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CategoryTagConfig.IsChecked))
            {
                UpdateOkButtonText();
                RefreshFamiliesFilter();
            }
        }

        private void UpdateOkButtonText()
        {
            int checkedCount = Configurations.Count(c => c.IsChecked);
            _btnOk.Content = checkedCount > 0 ? $"Маркировать элементы ({checkedCount})" : "Маркировать элементы";
            _btnOk.IsEnabled = checkedCount > 0;
        }

        private void LbCategories_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedConfig = _lbCategories.SelectedItem as CategoryTagConfig;
            if (selectedConfig != null)
            {
                _txtRightHeader.Text = $"2. Выберите марку для категории '{selectedConfig.CategoryName}':";
                _lbTags.SelectionChanged -= LbTags_SelectionChanged;
                _lbTags.ItemsSource = selectedConfig.AvailableTags;
                _lbTags.SelectedItem = selectedConfig.SelectedTag;
                _lbTags.SelectionChanged += LbTags_SelectionChanged;
            }
            else
            {
                _txtRightHeader.Text = "2. Выберите марку:";
                _lbTags.ItemsSource = null;
            }
        }

        private void LbTags_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedConfig = _lbCategories.SelectedItem as CategoryTagConfig;
            var selectedTag = _lbTags.SelectedItem as TagTypeItem;
            
            if (selectedConfig != null && selectedTag != null)
            {
                selectedConfig.SelectedTag = selectedTag;
                if (selectedTag.Id != ElementId.InvalidElementId)
                {
                    selectedConfig.IsChecked = true;
                }
                else
                {
                    selectedConfig.IsChecked = false;
                }
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            int checkedCount = Configurations.Count(c => c.IsChecked);
            if (checkedCount == 0)
            {
                MessageBox.Show("Пожалуйста, выберите хотя бы одну категорию для подписи.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            this.DialogResult = true;
            this.Close();
        }
    }
}
