using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

using ElementId = Autodesk.Revit.DB.ElementId;
using BuiltInCategory = Autodesk.Revit.DB.BuiltInCategory;
using Definition = Autodesk.Revit.DB.Definition;
using DefinitionGroup = Autodesk.Revit.DB.DefinitionGroup;
using DefinitionFile = Autodesk.Revit.DB.DefinitionFile;
using ExternalDefinition = Autodesk.Revit.DB.ExternalDefinition;
using RevitCategory = Autodesk.Revit.DB.Category;
using WpfBinding = System.Windows.Data.Binding;
using RelativeSource = System.Windows.Data.RelativeSource;
using BindingMode = System.Windows.Data.BindingMode;

using ForgeTypeId = Autodesk.Revit.DB.ForgeTypeId;
using SpecTypeId = Autodesk.Revit.DB.SpecTypeId;
using GroupTypeId = Autodesk.Revit.DB.GroupTypeId;

namespace BimboClub
{
    public class DataTypeOption
    {
        public string DisplayName { get; set; }
        public ForgeTypeId Type { get; set; }
        public string FopString { get; set; }

        public override string ToString() => DisplayName;
    }

    public class GroupOption
    {
        public string DisplayName { get; set; }
        public ForgeTypeId Group { get; set; }

        public override string ToString() => DisplayName;
    }

    public class FopDefinitionGroupItem
    {
        public string Name { get; set; }
        public List<FopDefinitionItem> Parameters { get; set; } = new List<FopDefinitionItem>();
        public override string ToString() => Name;
    }

    public class FopDefinitionItem
    {
        public string Name { get; set; }
        public Definition Definition { get; set; }
        public override string ToString() => Name;
    }

    public class CategoryItem : INotifyPropertyChanged
    {
        private bool _isChecked;
        public string Name { get; set; }
        public RevitCategory Category { get; set; }

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

    public class CheckedFamilyItem : INotifyPropertyChanged
    {
        private bool _isChecked;
        public string Name { get; set; }
        public Autodesk.Revit.DB.Family Family { get; set; }

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

    public class AddParameterWindow : Window
    {
        private readonly Autodesk.Revit.UI.UIApplication _uiapp;
        private readonly Autodesk.Revit.DB.Document _doc;

        // Элементы UI
        private RadioButton _rbProjectParam;
        private RadioButton _rbSharedParam;
        
        private TextBox _txtFopPath;
        private Button _btnBrowseFop;
        private ComboBox _cbFopGroup;
        private ComboBox _cbFopParam;
        
        private TextBox _txtParamName;
        private ComboBox _cbDataType;
        private ComboBox _cbParameterGroup;
        
        private RadioButton _rbInstance;
        private RadioButton _rbType;

        private ListBox _lbCategories;
        private Button _btnSelectAll;
        private Button _btnDeselectAll;

        private Button _btnOk;

        // Новые элементы UI для поиска и семейств
        private Button _btnTabCategories;
        private Button _btnTabFamilies;
        private Border _borderCategoriesList;
        private Border _borderFamiliesList;
        private ListBox _lbFamilies;
        private TextBox _txtSearch;
        private int _activeTabIndex = 0;

        private StackPanel _fopPanel;
        private TextBox _tbDataTypeReadOnly;

        private List<CategoryItem> _allCategoryItems = new List<CategoryItem>();
        private List<CheckedFamilyItem> _allFamilyItems = new List<CheckedFamilyItem>();

        // Выбранные значения для экспорта
        public bool IsSharedParameter { get; private set; }
        public string ParameterName { get; private set; }
        public bool IsInstanceBinding { get; private set; }

        public ForgeTypeId SelectedParameterType { get; private set; }
        public ForgeTypeId SelectedParameterGroup { get; private set; }

        public string SelectedFopPath { get; private set; }
        public string SelectedFopGroupName { get; private set; }
        public string SelectedFopParamName { get; private set; }

        public List<RevitCategory> SelectedCategories { get; private set; } = new List<RevitCategory>();
        public List<Autodesk.Revit.DB.Family> SelectedFamilies { get; private set; } = new List<Autodesk.Revit.DB.Family>();

        public AddParameterWindow(Autodesk.Revit.UI.UIApplication uiapp, Autodesk.Revit.DB.Document doc)
        {
            _uiapp = uiapp;
            _doc = doc;

            InitializeComponent();
            LoadDefaultSettings();
        }

        private Style GetRoundedButtonStyle(double radius)
        {
            Style style = new Style(typeof(Button));
            ControlTemplate template = new ControlTemplate(typeof(Button));
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
            border.SetValue(Border.BackgroundProperty, new WpfBinding("Background") { RelativeSource = RelativeSource.TemplatedParent });
            border.SetValue(Border.BorderBrushProperty, new WpfBinding("BorderBrush") { RelativeSource = RelativeSource.TemplatedParent });
            border.SetValue(Border.BorderThicknessProperty, new WpfBinding("BorderThickness") { RelativeSource = RelativeSource.TemplatedParent });

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
            bool isFamily = _doc.IsFamilyDocument;

            this.Title = "Добавить параметр - BimboClub Tools";
            this.Width = isFamily ? 400 : 750;
            this.Height = 550;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.Background = new SolidColorBrush(Color.FromRgb(24, 24, 28)); // #18181C
            this.FontFamily = new FontFamily("Segoe UI");
            this.FontSize = 13;
            this.MinHeight = 500;
            this.MinWidth = isFamily ? 350 : 700;

            // Цветовая палитра
            SolidColorBrush panelBg = new SolidColorBrush(Color.FromRgb(32, 32, 38)); // #202026
            SolidColorBrush borderBrush = new SolidColorBrush(Color.FromRgb(48, 48, 56)); // #303038
            SolidColorBrush inputBg = new SolidColorBrush(Color.FromRgb(45, 45, 53)); // #2D2D35
            Style roundedButtonStyle = GetRoundedButtonStyle(5);

            Style comboBoxItemStyle = new Style(typeof(ComboBoxItem));
            comboBoxItemStyle.Setters.Add(new Setter(ComboBoxItem.BackgroundProperty, inputBg));
            comboBoxItemStyle.Setters.Add(new Setter(ComboBoxItem.ForegroundProperty, Brushes.White));
            this.Resources.Add(typeof(ComboBoxItem), comboBoxItemStyle);

            Grid mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });    // Заголовок
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Контент
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });    // Кнопки
            this.Content = mainGrid;

            // 1. Заголовок (Градиент от черного к алому)
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
                Text = "ДОБАВЛЕНИЕ ПАРАМЕТРОВ В " + (isFamily ? "СЕМЕЙСТВО" : "ПРОЕКТ"),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 15,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerBorder.Child = headerText;

            // 2. Зона контента
            Grid contentGrid = new Grid { Margin = new Thickness(15, 10, 15, 10) };
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(350) }); // Левая панель (Настройки)
            if (!isFamily)
            {
                contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(15) });  // Разделитель
                contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Правая панель (Категории)
            }
            Grid.SetRow(contentGrid, 1);
            mainGrid.Children.Add(contentGrid);

            // ЛЕВАЯ ПАНЕЛЬ: Настройки параметра
            ScrollViewer leftScrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            Grid.SetColumn(leftScrollViewer, 0);
            contentGrid.Children.Add(leftScrollViewer);

            StackPanel leftPanel = new StackPanel { Margin = new Thickness(0, 0, 5, 0) };
            leftScrollViewer.Content = leftPanel;

            // Инфо о документе
            TextBlock txtDocInfo = new TextBlock
            {
                Text = "Документ: " + Path.GetFileName(_doc.Title),
                Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 190)),
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 10),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            leftPanel.Children.Add(txtDocInfo);

            // Выбор типа параметра
            leftPanel.Children.Add(CreateHeaderLabel("Тип параметра:"));
            StackPanel typeRadioPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 15) };
            _rbProjectParam = new RadioButton
            {
                Content = isFamily ? "Параметр семейства" : "Проектный параметр",
                Foreground = Brushes.White,
                IsChecked = true,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            _rbProjectParam.Checked += RbParamType_Changed;
            _rbSharedParam = new RadioButton
            {
                Content = "Общий параметр (ФОП)",
                Foreground = Brushes.White,
                Margin = new Thickness(15, 0, 0, 0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            _rbSharedParam.Checked += RbParamType_Changed;
            typeRadioPanel.Children.Add(_rbProjectParam);
            typeRadioPanel.Children.Add(_rbSharedParam);
            leftPanel.Children.Add(typeRadioPanel);

            // Блок настроек ФОП (по умолчанию скрыт/заблокирован)
            _fopPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };
            
            _fopPanel.Children.Add(CreateHeaderLabel("Файл общих параметров (ФОП):"));
            Grid fopFileGrid = new Grid { Margin = new Thickness(0, 5, 0, 5) };
            fopFileGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fopFileGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            
            _txtFopPath = new TextBox
            {
                IsReadOnly = true,
                Background = inputBg,
                Foreground = Brushes.White,
                BorderBrush = borderBrush,
                Padding = new Thickness(5, 4, 5, 4),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_txtFopPath, 0);
            fopFileGrid.Children.Add(_txtFopPath);

            _btnBrowseFop = new Button
            {
                Content = "Обзор",
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Height = 25,
                Margin = new Thickness(5, 0, 0, 0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            _btnBrowseFop.Click += BtnBrowseFop_Click;
            ApplyFlatButtonStyles(_btnBrowseFop, new SolidColorBrush(Color.FromRgb(58, 58, 66)), new SolidColorBrush(Color.FromRgb(78, 78, 88)), roundedButtonStyle);
            Grid.SetColumn(_btnBrowseFop, 1);
            fopFileGrid.Children.Add(_btnBrowseFop);
            _fopPanel.Children.Add(fopFileGrid);

            _fopPanel.Children.Add(CreateHeaderLabel("Группа параметров ФОП:"));
            _cbFopGroup = new ComboBox
            {
                Background = inputBg,
                Foreground = Brushes.White,
                BorderBrush = borderBrush,
                Margin = new Thickness(0, 5, 0, 5),
                Padding = new Thickness(5, 4, 5, 4)
            };
            UiThemeHelper.StyleComboBoxDark(_cbFopGroup, inputBg, borderBrush);
            _cbFopGroup.SelectionChanged += CbFopGroup_SelectionChanged;
            _fopPanel.Children.Add(_cbFopGroup);

            _fopPanel.Children.Add(CreateHeaderLabel("Параметр ФОП:"));
            _cbFopParam = new ComboBox
            {
                Background = inputBg,
                Foreground = Brushes.White,
                BorderBrush = borderBrush,
                Margin = new Thickness(0, 5, 0, 10),
                Padding = new Thickness(5, 4, 5, 4)
            };
            UiThemeHelper.StyleComboBoxDark(_cbFopParam, inputBg, borderBrush);
            _cbFopParam.SelectionChanged += CbFopParam_SelectionChanged;
            _fopPanel.Children.Add(_cbFopParam);

            leftPanel.Children.Add(_fopPanel);

            // Основные поля ввода (Имя, Тип данных, Группа Revit)
            leftPanel.Children.Add(CreateHeaderLabel("Имя параметра:"));
            _txtParamName = new TextBox
            {
                Background = inputBg,
                Foreground = Brushes.White,
                BorderBrush = borderBrush,
                Margin = new Thickness(0, 5, 0, 15),
                Padding = new Thickness(5, 4, 5, 4)
            };
            leftPanel.Children.Add(_txtParamName);

            leftPanel.Children.Add(CreateHeaderLabel("Тип данных:"));
            _cbDataType = new ComboBox
            {
                Background = inputBg,
                Foreground = Brushes.White,
                BorderBrush = borderBrush,
                Margin = new Thickness(0, 5, 0, 15),
                Padding = new Thickness(5, 4, 5, 4)
            };
            UiThemeHelper.StyleComboBoxDark(_cbDataType, inputBg, borderBrush);
            _cbDataType.ItemsSource = GetDataTypeOptions();
            _cbDataType.SelectedIndex = 0; // "Текст" по умолчанию
            leftPanel.Children.Add(_cbDataType);

            _tbDataTypeReadOnly = new TextBox
            {
                Background = inputBg,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 190)),
                BorderBrush = borderBrush,
                Margin = new Thickness(0, 5, 0, 15),
                Padding = new Thickness(5, 4, 5, 4),
                IsReadOnly = true,
                Visibility = Visibility.Collapsed
            };
            leftPanel.Children.Add(_tbDataTypeReadOnly);

            leftPanel.Children.Add(CreateHeaderLabel("Группа параметров в Revit:"));
            _cbParameterGroup = new ComboBox
            {
                Background = inputBg,
                Foreground = Brushes.White,
                BorderBrush = borderBrush,
                Margin = new Thickness(0, 5, 0, 15),
                Padding = new Thickness(5, 4, 5, 4)
            };
            UiThemeHelper.StyleComboBoxDark(_cbParameterGroup, inputBg, borderBrush);
            _cbParameterGroup.ItemsSource = GetGroupOptions();
            _cbParameterGroup.SelectedIndex = 0; // "Данные" по умолчанию
            leftPanel.Children.Add(_cbParameterGroup);

            // Тип привязки (Instance vs Type)
            leftPanel.Children.Add(CreateHeaderLabel("Привязка параметра:"));
            StackPanel bindPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 10) };
            _rbInstance = new RadioButton
            {
                Content = "Экземпляр (Instance)",
                Foreground = Brushes.White,
                IsChecked = true,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            _rbType = new RadioButton
            {
                Content = "Тип (Type)",
                Foreground = Brushes.White,
                Margin = new Thickness(25, 0, 0, 0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            bindPanel.Children.Add(_rbInstance);
            bindPanel.Children.Add(_rbType);
            leftPanel.Children.Add(bindPanel);

            // ПРАВАЯ ПАНЕЛЬ: Категории и Семейства для проекта с фильтрацией поиска
            if (!isFamily)
            {
                Grid rightPanelGrid = new Grid();
                rightPanelGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 0: Поиск
                rightPanelGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 1: Вкладки
                rightPanelGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) }); // 2: Управление кнопками
                rightPanelGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 3: Списки
                Grid.SetColumn(rightPanelGrid, 2);
                contentGrid.Children.Add(rightPanelGrid);

                // Поиск
                Grid searchGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
                searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                
                TextBlock lblSearch = new TextBlock
                {
                    Text = "Поиск: ",
                    Foreground = Brushes.White,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 5, 0)
                };
                Grid.SetColumn(lblSearch, 0);
                searchGrid.Children.Add(lblSearch);

                _txtSearch = new TextBox
                {
                    Background = inputBg,
                    Foreground = Brushes.White,
                    BorderBrush = borderBrush,
                    Padding = new Thickness(5, 4, 5, 4)
                };
                _txtSearch.TextChanged += (s, e) => ApplyFilter();
                Grid.SetColumn(_txtSearch, 1);
                searchGrid.Children.Add(_txtSearch);
                Grid.SetRow(searchGrid, 0);
                rightPanelGrid.Children.Add(searchGrid);

                // Вкладки (Категории / Семейства)
                Grid tabsGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
                tabsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                tabsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                _btnTabCategories = new Button
                {
                    Content = "Категории",
                    Height = 28,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 2, 0)
                };
                _btnTabCategories.Click += (s, e) => SwitchTab(0);
                _btnTabCategories.Style = roundedButtonStyle;
                Grid.SetColumn(_btnTabCategories, 0);
                tabsGrid.Children.Add(_btnTabCategories);

                _btnTabFamilies = new Button
                {
                    Content = "Семейства",
                    Height = 28,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(2, 0, 0, 0)
                };
                _btnTabFamilies.Click += (s, e) => SwitchTab(1);
                _btnTabFamilies.Style = roundedButtonStyle;
                Grid.SetColumn(_btnTabFamilies, 1);
                tabsGrid.Children.Add(_btnTabFamilies);

                Grid.SetRow(tabsGrid, 1);
                rightPanelGrid.Children.Add(tabsGrid);

                // Кнопки Выбрать все / Снять все
                Grid selectButtonsGrid = new Grid { Margin = new Thickness(0, 0, 0, 5) };
                selectButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                selectButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                Grid.SetRow(selectButtonsGrid, 2);
                rightPanelGrid.Children.Add(selectButtonsGrid);

                _btnSelectAll = new Button
                {
                    Content = "Выбрать все",
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Height = 25,
                    Margin = new Thickness(0, 0, 5, 0),
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                _btnSelectAll.Click += (s, e) => { SetAllActiveTabChecked(true); };
                ApplyFlatButtonStyles(_btnSelectAll, new SolidColorBrush(Color.FromRgb(58, 58, 66)), new SolidColorBrush(Color.FromRgb(78, 78, 88)), roundedButtonStyle);
                Grid.SetColumn(_btnSelectAll, 0);
                selectButtonsGrid.Children.Add(_btnSelectAll);

                _btnDeselectAll = new Button
                {
                    Content = "Снять все",
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Height = 25,
                    Margin = new Thickness(5, 0, 0, 0),
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                _btnDeselectAll.Click += (s, e) => { SetAllActiveTabChecked(false); };
                ApplyFlatButtonStyles(_btnDeselectAll, new SolidColorBrush(Color.FromRgb(58, 58, 66)), new SolidColorBrush(Color.FromRgb(78, 78, 88)), roundedButtonStyle);
                Grid.SetColumn(_btnDeselectAll, 1);
                selectButtonsGrid.Children.Add(_btnDeselectAll);

                // Список категорий
                _borderCategoriesList = new Border
                {
                    Background = panelBg,
                    BorderBrush = borderBrush,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(5)
                };
                Grid.SetRow(_borderCategoriesList, 3);
                rightPanelGrid.Children.Add(_borderCategoriesList);

                _lbCategories = new ListBox
                {
                    BorderThickness = new Thickness(0),
                    Background = Brushes.Transparent,
                    Foreground = Brushes.White,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch
                };
                _borderCategoriesList.Child = _lbCategories;

                DataTemplate catTemplate = new DataTemplate();
                FrameworkElementFactory chk = new FrameworkElementFactory(typeof(CheckBox));
                chk.SetBinding(CheckBox.IsCheckedProperty, new WpfBinding("IsChecked") { Mode = System.Windows.Data.BindingMode.TwoWay });
                chk.SetValue(CheckBox.VerticalAlignmentProperty, VerticalAlignment.Center);
                chk.SetValue(CheckBox.ForegroundProperty, Brushes.White);
                chk.SetBinding(CheckBox.ContentProperty, new WpfBinding("Name"));
                chk.SetValue(CheckBox.PaddingProperty, new Thickness(8, 0, 0, 0));
                chk.SetValue(CheckBox.MarginProperty, new Thickness(5, 4, 5, 4));
                chk.SetValue(CheckBox.BackgroundProperty, Brushes.Transparent);
                chk.SetValue(CheckBox.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
                catTemplate.VisualTree = chk;
                _lbCategories.ItemTemplate = catTemplate;

                // Список семейств
                _borderFamiliesList = new Border
                {
                    Background = panelBg,
                    BorderBrush = borderBrush,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(5),
                    Visibility = Visibility.Collapsed
                };
                Grid.SetRow(_borderFamiliesList, 3);
                rightPanelGrid.Children.Add(_borderFamiliesList);

                _lbFamilies = new ListBox
                {
                    BorderThickness = new Thickness(0),
                    Background = Brushes.Transparent,
                    Foreground = Brushes.White,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch
                };
                _borderFamiliesList.Child = _lbFamilies;

                DataTemplate famTemplate = new DataTemplate();
                FrameworkElementFactory chkFam = new FrameworkElementFactory(typeof(CheckBox));
                chkFam.SetBinding(CheckBox.IsCheckedProperty, new WpfBinding("IsChecked") { Mode = System.Windows.Data.BindingMode.TwoWay });
                chkFam.SetValue(CheckBox.VerticalAlignmentProperty, VerticalAlignment.Center);
                chkFam.SetValue(CheckBox.ForegroundProperty, Brushes.White);
                chkFam.SetBinding(CheckBox.ContentProperty, new WpfBinding("Name"));
                chkFam.SetValue(CheckBox.PaddingProperty, new Thickness(8, 0, 0, 0));
                chkFam.SetValue(CheckBox.MarginProperty, new Thickness(5, 4, 5, 4));
                chkFam.SetValue(CheckBox.BackgroundProperty, Brushes.Transparent);
                chkFam.SetValue(CheckBox.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
                famTemplate.VisualTree = chkFam;
                _lbFamilies.ItemTemplate = famTemplate;

                // Загружаем списки
                PopulateCategories();
                PopulateFamilies();
                SwitchTab(0);
            }

            // 3. Нижние кнопки действия
            Grid bottomGrid = new Grid { Margin = new Thickness(15, 10, 15, 10) };
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            Grid.SetRow(bottomGrid, 2);
            mainGrid.Children.Add(bottomGrid);

            // Кнопка ОК (Алый градиент)
            _btnOk = new Button
            {
                Content = "Добавить параметр",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0),
                Height = 35,
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            _btnOk.Click += BtnOk_Click;

            LinearGradientBrush normalGrad = new LinearGradientBrush(Color.FromRgb(255, 46, 91), Color.FromRgb(179, 14, 45), 90.0);
            LinearGradientBrush hoverGrad = new LinearGradientBrush(Color.FromRgb(255, 80, 118), Color.FromRgb(210, 20, 60), 90.0);
            ApplyButtonStyles(_btnOk, normalGrad, hoverGrad, roundedButtonStyle);
            bottomGrid.Children.Add(_btnOk);

            // Кнопка Отмена (Темный)
            Button btnCancel = new Button
            {
                Content = "Отмена",
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Height = 35,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnCancel.Click += (s, e) => { this.DialogResult = false; this.Close(); };

            SolidColorBrush cancelNormal = new SolidColorBrush(Color.FromRgb(58, 58, 66));
            SolidColorBrush cancelHover = new SolidColorBrush(Color.FromRgb(78, 78, 88));
            ApplyFlatButtonStyles(btnCancel, cancelNormal, cancelHover, roundedButtonStyle);

            Grid.SetColumn(btnCancel, 1);
            bottomGrid.Children.Add(btnCancel);

            // Первоначальное переключение состояния полей
            ToggleFopFields(false);
        }

        private TextBlock CreateHeaderLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 5, 0, 2)
            };
        }

        private void RbParamType_Changed(object sender, RoutedEventArgs e)
        {
            bool isShared = _rbSharedParam?.IsChecked ?? false;
            ToggleFopFields(isShared);
        }

        private void ToggleFopFields(bool isShared)
        {
            if (_txtFopPath == null) return;

            if (_fopPanel != null)
            {
                _fopPanel.Visibility = isShared ? Visibility.Visible : Visibility.Collapsed;
            }

            _txtParamName.IsReadOnly = isShared;
            _txtParamName.Foreground = isShared ? new SolidColorBrush(Color.FromRgb(180, 180, 190)) : Brushes.White;

            if (_cbDataType != null)
            {
                _cbDataType.Visibility = isShared ? Visibility.Collapsed : Visibility.Visible;
            }

            if (_tbDataTypeReadOnly != null)
            {
                _tbDataTypeReadOnly.Visibility = isShared ? Visibility.Visible : Visibility.Collapsed;
            }

            if (!isShared)
            {
                _txtParamName.Text = "";
                _cbDataType.SelectedIndex = 0;
            }
            else
            {
                CbFopParam_SelectionChanged(null, null);
            }
        }

        private void PopulateCategories()
        {
            _allCategoryItems.Clear();
            foreach (RevitCategory cat in _doc.Settings.Categories)
            {
                if (cat.AllowsBoundParameters && cat.CategoryType == Autodesk.Revit.DB.CategoryType.Model && !string.IsNullOrEmpty(cat.Name))
                {
                    _allCategoryItems.Add(new CategoryItem { Name = cat.Name, Category = cat, IsChecked = false });
                }
            }
            _lbCategories.ItemsSource = _allCategoryItems.OrderBy(i => i.Name).ToList();
        }

        private void PopulateFamilies()
        {
            _allFamilyItems.Clear();
            try
            {
                var collector = new Autodesk.Revit.DB.FilteredElementCollector(_doc);
                var families = collector.OfClass(typeof(Autodesk.Revit.DB.Family)).Cast<Autodesk.Revit.DB.Family>();
                foreach (var fam in families)
                {
                    if (fam.IsEditable && !string.IsNullOrEmpty(fam.Name))
                    {
                        _allFamilyItems.Add(new CheckedFamilyItem { Name = fam.Name, Family = fam, IsChecked = false });
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Не удалось загрузить семейства", ex);
            }
            _lbFamilies.ItemsSource = _allFamilyItems.OrderBy(i => i.Name).ToList();
        }

        private void SetAllActiveTabChecked(bool isChecked)
        {
            if (_activeTabIndex == 0)
            {
                var visibleItems = _lbCategories?.ItemsSource as IEnumerable<CategoryItem>;
                if (visibleItems != null)
                {
                    foreach (var item in visibleItems)
                    {
                        item.IsChecked = isChecked;
                    }
                }
            }
            else
            {
                var visibleItems = _lbFamilies?.ItemsSource as IEnumerable<CheckedFamilyItem>;
                if (visibleItems != null)
                {
                    foreach (var item in visibleItems)
                    {
                        item.IsChecked = isChecked;
                    }
                }
            }
        }

        private void SwitchTab(int tabIndex)
        {
            _activeTabIndex = tabIndex;
            if (_activeTabIndex == 0)
            {
                _borderCategoriesList.Visibility = Visibility.Visible;
                _borderFamiliesList.Visibility = Visibility.Collapsed;
                
                _btnTabCategories.Background = new SolidColorBrush(Color.FromRgb(179, 14, 45));
                _btnTabCategories.Foreground = Brushes.White;
                
                _btnTabFamilies.Background = new SolidColorBrush(Color.FromRgb(32, 32, 38));
                _btnTabFamilies.Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 190));
            }
            else
            {
                _borderCategoriesList.Visibility = Visibility.Collapsed;
                _borderFamiliesList.Visibility = Visibility.Visible;
                
                _btnTabFamilies.Background = new SolidColorBrush(Color.FromRgb(179, 14, 45));
                _btnTabFamilies.Foreground = Brushes.White;
                
                _btnTabCategories.Background = new SolidColorBrush(Color.FromRgb(32, 32, 38));
                _btnTabCategories.Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 190));
            }
        }

        private void ApplyFilter()
        {
            string filterText = _txtSearch?.Text?.Trim() ?? "";
            
            if (_lbCategories != null && _allCategoryItems != null)
            {
                if (string.IsNullOrEmpty(filterText))
                {
                    _lbCategories.ItemsSource = _allCategoryItems.OrderBy(i => i.Name).ToList();
                }
                else
                {
                    _lbCategories.ItemsSource = _allCategoryItems
                        .Where(i => i.Name.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0)
                        .OrderBy(i => i.Name)
                        .ToList();
                }
            }

            if (_lbFamilies != null && _allFamilyItems != null)
            {
                if (string.IsNullOrEmpty(filterText))
                {
                    _lbFamilies.ItemsSource = _allFamilyItems.OrderBy(i => i.Name).ToList();
                }
                else
                {
                    _lbFamilies.ItemsSource = _allFamilyItems
                        .Where(i => i.Name.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0)
                        .OrderBy(i => i.Name)
                        .ToList();
                }
            }
        }

        private void LoadDefaultSettings()
        {
            // Попробуем прочесть настроенный ФОП по умолчанию
            try
            {
                string defaultFop = _doc.Application.SharedParametersFilename;
                if (!string.IsNullOrEmpty(defaultFop) && File.Exists(defaultFop))
                {
                    _txtFopPath.Text = defaultFop;
                    LoadFopData(defaultFop);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Не удалось загрузить исходный файл ФОП по умолчанию", ex);
            }
        }

        private void BtnBrowseFop_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog
            {
                Filter = "Файлы общих параметров (*.txt)|*.txt",
                Title = "Выберите файл общих параметров (ФОП)"
            };

            if (dlg.ShowDialog() == true)
            {
                _txtFopPath.Text = dlg.FileName;
                LoadFopData(dlg.FileName);
            }
        }

        private void LoadFopData(string fopPath)
        {
            try
            {
                string originalFop = _doc.Application.SharedParametersFilename;
                _doc.Application.SharedParametersFilename = fopPath;

                DefinitionFile defFile = _doc.Application.OpenSharedParameterFile();
                if (defFile == null)
                {
                    _doc.Application.SharedParametersFilename = originalFop;
                    MessageBox.Show("Не удалось открыть или прочитать выбранный файл ФОП.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                List<FopDefinitionGroupItem> groups = new List<FopDefinitionGroupItem>();
                foreach (DefinitionGroup dg in defFile.Groups)
                {
                    FopDefinitionGroupItem groupItem = new FopDefinitionGroupItem { Name = dg.Name };
                    foreach (Definition def in dg.Definitions)
                    {
                        groupItem.Parameters.Add(new FopDefinitionItem { Name = def.Name, Definition = def });
                    }

                    if (groupItem.Parameters.Count > 0)
                    {
                        groups.Add(groupItem);
                    }
                }

                _doc.Application.SharedParametersFilename = originalFop;

                _cbFopGroup.ItemsSource = groups;
                if (groups.Count > 0)
                {
                    _cbFopGroup.SelectedIndex = 0;
                }
                else
                {
                    _cbFopGroup.ItemsSource = null;
                    _cbFopParam.ItemsSource = null;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при анализе файла ФОП:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CbFopGroup_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedGroup = _cbFopGroup.SelectedItem as FopDefinitionGroupItem;
            if (selectedGroup != null)
            {
                _cbFopParam.ItemsSource = selectedGroup.Parameters;
                if (selectedGroup.Parameters.Count > 0)
                {
                    _cbFopParam.SelectedIndex = 0;
                }
            }
            else
            {
                _cbFopParam.ItemsSource = null;
            }
        }

        private void CbFopParam_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedParamItem = _cbFopParam?.SelectedItem as FopDefinitionItem;
            if (selectedParamItem != null)
            {
                Definition def = selectedParamItem.Definition;
                if (_txtParamName != null) _txtParamName.Text = def.Name;

                // Выбираем соответствующий тип данных в ComboBox и блокируем изменение
                ForgeTypeId specId = def.GetDataType();
                var found = _cbDataType?.Items.Cast<DataTypeOption>().FirstOrDefault(o => o.Type == specId);
                if (found != null)
                {
                    if (_cbDataType != null) _cbDataType.SelectedItem = found;
                    if (_tbDataTypeReadOnly != null) _tbDataTypeReadOnly.Text = found.DisplayName;
                }
                else
                {
                    if (_tbDataTypeReadOnly != null) _tbDataTypeReadOnly.Text = "Неизвестный тип";
                }
            }
            else
            {
                if (_txtParamName != null) _txtParamName.Text = "";
                if (_tbDataTypeReadOnly != null) _tbDataTypeReadOnly.Text = "";
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            bool isShared = _rbSharedParam.IsChecked ?? false;

            // Валидация
            if (isShared)
            {
                if (string.IsNullOrEmpty(_txtFopPath.Text) || !File.Exists(_txtFopPath.Text))
                {
                    MessageBox.Show("Пожалуйста, укажите существующий файл общих параметров (ФОП).", "Валидация", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (_cbFopParam.SelectedItem == null)
                {
                    MessageBox.Show("Пожалуйста, выберите параметр из ФОП.", "Валидация", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(_txtParamName.Text))
                {
                    MessageBox.Show("Пожалуйста, введите имя параметра.", "Валидация", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            if (!_doc.IsFamilyDocument)
            {
                var selectedCats = _allCategoryItems.Where(i => i.IsChecked).Select(i => i.Category).ToList();
                var selectedFams = _allFamilyItems.Where(i => i.IsChecked).Select(i => i.Family).ToList();

                if (selectedCats.Count == 0 && selectedFams.Count == 0)
                {
                    MessageBox.Show("Пожалуйста, выберите хотя бы одну категорию или семейство для привязки параметра.", "Валидация", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                SelectedCategories = selectedCats;
                SelectedFamilies = selectedFams;
            }

            // Заполняем экспортируемые свойства
            IsSharedParameter = isShared;
            ParameterName = _txtParamName.Text.Trim();
            IsInstanceBinding = _rbInstance.IsChecked ?? true;

            var selectedType = _cbDataType.SelectedItem as DataTypeOption;
            if (selectedType != null)
            {
                SelectedParameterType = selectedType.Type;
            }

            var selectedGroup = _cbParameterGroup.SelectedItem as GroupOption;
            if (selectedGroup != null)
            {
                SelectedParameterGroup = selectedGroup.Group;
            }

            if (isShared)
            {
                SelectedFopPath = _txtFopPath.Text;
                var g = _cbFopGroup.SelectedItem as FopDefinitionGroupItem;
                SelectedFopGroupName = g?.Name;
                var p = _cbFopParam.SelectedItem as FopDefinitionItem;
                SelectedFopParamName = p?.Name;
            }

            this.DialogResult = true;
            this.Close();
        }

        private static List<DataTypeOption> GetDataTypeOptions()
        {
            return new List<DataTypeOption>
            {
                new DataTypeOption { DisplayName = "Текст (Text)", Type = SpecTypeId.String.Text, FopString = "TEXT" },
                new DataTypeOption { DisplayName = "Длина (Length)", Type = SpecTypeId.Length, FopString = "LENGTH" },
                new DataTypeOption { DisplayName = "Целое число (Integer)", Type = SpecTypeId.Int.Integer, FopString = "INTEGER" },
                new DataTypeOption { DisplayName = "Угол (Angle)", Type = SpecTypeId.Angle, FopString = "ANGLE" },
                new DataTypeOption { DisplayName = "Число (Number)", Type = SpecTypeId.Number, FopString = "NUMBER" },
                new DataTypeOption { DisplayName = "Да/Нет (Yes/No)", Type = SpecTypeId.Boolean.YesNo, FopString = "YESNO" }
            };
        }

        private static List<GroupOption> GetGroupOptions()
        {
            return new List<GroupOption>
            {
                new GroupOption { DisplayName = "Данные (Data)", Group = GroupTypeId.Data },
                new GroupOption { DisplayName = "Идентификация (Identity Data)", Group = GroupTypeId.IdentityData },
                new GroupOption { DisplayName = "Текст (Text)", Group = GroupTypeId.Text },
                new GroupOption { DisplayName = "Механизмы (Mechanical)", Group = GroupTypeId.Mechanical },
                new GroupOption { DisplayName = "Трубопроводы (Plumbing)", Group = GroupTypeId.Plumbing },
                new GroupOption { DisplayName = "Электрика (Electrical)", Group = GroupTypeId.Electrical },
                new GroupOption { DisplayName = "Строительство (Construction)", Group = GroupTypeId.Construction },
                new GroupOption { DisplayName = "Общие (General)", Group = GroupTypeId.General },
                new GroupOption { DisplayName = "Графика (Graphics)", Group = GroupTypeId.Graphics },
                new GroupOption { DisplayName = "Зависимости (Constraints)", Group = GroupTypeId.Constraints }
            };
        }
    }
}
