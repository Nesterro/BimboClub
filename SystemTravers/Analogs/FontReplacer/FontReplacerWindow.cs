using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Autodesk.Revit.DB;
using WpfGrid = System.Windows.Controls.Grid;

namespace BimboClub.Analogs.FontReplacer
{
    public class FontReplacerWindow : Window
    {
        private readonly Document _doc;

        // Tree items
        public ObservableCollection<StyleGroupNode> StyleGroups { get; } = new ObservableCollection<StyleGroupNode>();

        // Controls
        private ComboBox _cbTargetFont;
        private CheckBox _chkWidthScale;
        private System.Windows.Controls.TextBox _txtWidthScale;
        private CheckBox _chkTextSize;
        private System.Windows.Controls.TextBox _txtTextSize;
        private CheckBox _chkBold;
        private CheckBox _chkItalic;
        private CheckBox _chkUnderline;
        private CheckBox _chkBackground;
        private RadioButton _rbTransparent;
        private RadioButton _rbOpaque;
        private CheckBox _chkColor;
        private ComboBox _cbColor;
        private CheckBox _chkRename;
        private System.Windows.Controls.TextBox _txtFindName;
        private System.Windows.Controls.TextBox _txtReplaceName;
        private TextBlock _lblStats;

        public FontReplacementSettings Settings { get; private set; }
        public List<ElementId> SelectedTextTypeIds { get; } = new List<ElementId>();
        public List<ElementId> SelectedDimTypeIds { get; } = new List<ElementId>();
        public List<ElementId> SelectedScheduleIds { get; } = new List<ElementId>();
        public List<ElementId> SelectedFamilyIds { get; } = new List<ElementId>();

        public class StyleItemNode : INotifyPropertyChanged
        {
            private bool _isChecked = true;
            public string Name { get; set; }
            public string CurrentFont { get; set; }
            public ElementId Id { get; set; }

            public bool IsChecked
            {
                get => _isChecked;
                set
                {
                    if (_isChecked != value)
                    {
                        _isChecked = value;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
                    }
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
        }

        public class StyleGroupNode : INotifyPropertyChanged
        {
            private bool? _isChecked = true;
            public string Title { get; set; }
            public ObservableCollection<StyleItemNode> Items { get; } = new ObservableCollection<StyleItemNode>();

            public bool? IsChecked
            {
                get => _isChecked;
                set
                {
                    if (_isChecked != value)
                    {
                        _isChecked = value;
                        if (value.HasValue)
                        {
                            foreach (var item in Items) item.IsChecked = value.Value;
                        }
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
                    }
                }
            }

            public void UpdateCheckState()
            {
                int checkedCount = Items.Count(i => i.IsChecked);
                if (checkedCount == Items.Count) _isChecked = true;
                else if (checkedCount == 0) _isChecked = false;
                else _isChecked = null;

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
            }

            public event PropertyChangedEventHandler PropertyChanged;
        }

        public FontReplacerWindow(Document doc)
        {
            _doc = doc;
            InitializeUi();
            LoadProjectStyles();
        }

        private void InitializeUi()
        {
            Title = "Заменить шрифт — BimboClub Tools";
            Width = 920;
            Height = 680;
            MinHeight = 550;
            MinWidth = 800;
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
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer
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
                Text = "ЗАМЕНИТЬ ШРИФТ В ПРОЕКТЕ",
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Foreground = Brushes.White
            });
            headerStack.Children.Add(new TextBlock
            {
                Text = "Пакетная унификация шрифтов и стилей текста во всех элементах оформления",
                FontSize = 11,
                Foreground = textMuted,
                Margin = new Thickness(0, 3, 0, 0)
            });
            headerBorder.Child = headerStack;
            WpfGrid.SetRow(headerBorder, 0);
            mainGrid.Children.Add(headerBorder);

            // 2. Две колонки
            WpfGrid bodyGrid = new WpfGrid();
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 350 });
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Splitter
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(380) });

            // Левая колонка: Дерево стилей
            WpfGrid leftGrid = new WpfGrid();
            leftGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            leftGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            leftGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Быстрые кнопки выбора
            StackPanel selBtnStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(14, 10, 14, 6) };
            Button btnSelectAll = new Button
            {
                Content = "Выбрать все",
                Padding = new Thickness(10, 3, 10, 3),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(48, 52, 60)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 8, 0)
            };
            btnSelectAll.Click += (s, e) => SetAllChecked(true);

            Button btnUnselectAll = new Button
            {
                Content = "Снять выбор",
                Padding = new Thickness(10, 3, 10, 3),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(48, 52, 60)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btnUnselectAll.Click += (s, e) => SetAllChecked(false);

            selBtnStack.Children.Add(btnSelectAll);
            selBtnStack.Children.Add(btnUnselectAll);
            WpfGrid.SetRow(selBtnStack, 0);
            leftGrid.Children.Add(selBtnStack);

            // Дерево
            TreeView tvStyles = new TreeView
            {
                ItemsSource = StyleGroups,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(28, 28, 34)),
                Foreground = Brushes.White,
                BorderBrush = borderCard,
                Margin = new Thickness(14, 0, 10, 6)
            };

            // Шаблон узла группы
            HierarchicalDataTemplate groupTemplate = new HierarchicalDataTemplate(typeof(StyleGroupNode));
            groupTemplate.ItemsSource = new System.Windows.Data.Binding("Items");
            FrameworkElementFactory spGroup = new FrameworkElementFactory(typeof(StackPanel));
            spGroup.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

            FrameworkElementFactory chkGroup = new FrameworkElementFactory(typeof(CheckBox));
            chkGroup.SetBinding(CheckBox.IsCheckedProperty, new System.Windows.Data.Binding("IsChecked") { Mode = System.Windows.Data.BindingMode.TwoWay });
            chkGroup.SetValue(CheckBox.VerticalAlignmentProperty, VerticalAlignment.Center);
            chkGroup.SetValue(CheckBox.MarginProperty, new Thickness(0, 0, 6, 0));

            FrameworkElementFactory txtGroup = new FrameworkElementFactory(typeof(TextBlock));
            txtGroup.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Title"));
            txtGroup.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            txtGroup.SetValue(TextBlock.ForegroundProperty, Brushes.White);
            txtGroup.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);

            spGroup.AppendChild(chkGroup);
            spGroup.AppendChild(txtGroup);
            groupTemplate.VisualTree = spGroup;
            tvStyles.ItemTemplate = groupTemplate;

            // Шаблон элемента
            DataTemplate itemTemplate = new DataTemplate(typeof(StyleItemNode));
            FrameworkElementFactory spItem = new FrameworkElementFactory(typeof(StackPanel));
            spItem.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

            FrameworkElementFactory chkItem = new FrameworkElementFactory(typeof(CheckBox));
            chkItem.SetBinding(CheckBox.IsCheckedProperty, new System.Windows.Data.Binding("IsChecked") { Mode = System.Windows.Data.BindingMode.TwoWay });
            chkItem.SetValue(CheckBox.VerticalAlignmentProperty, VerticalAlignment.Center);
            chkItem.SetValue(CheckBox.MarginProperty, new Thickness(0, 0, 6, 0));

            FrameworkElementFactory txtItem = new FrameworkElementFactory(typeof(TextBlock));
            txtItem.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Name"));
            txtItem.SetValue(TextBlock.ForegroundProperty, Brushes.White);
            txtItem.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);

            FrameworkElementFactory txtFont = new FrameworkElementFactory(typeof(TextBlock));
            txtFont.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("CurrentFont") { StringFormat = " [{0}]" });
            txtFont.SetValue(TextBlock.ForegroundProperty, textMuted);
            txtFont.SetValue(TextBlock.FontSizeProperty, 11.0);
            txtFont.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);

            spItem.AppendChild(chkItem);
            spItem.AppendChild(txtItem);
            spItem.AppendChild(txtFont);
            itemTemplate.VisualTree = spItem;

            groupTemplate.ItemTemplate = itemTemplate;

            WpfGrid.SetRow(tvStyles, 1);
            leftGrid.Children.Add(tvStyles);

            // Статистика
            _lblStats = new TextBlock
            {
                Text = "Загрузка элементов...",
                Foreground = textMuted,
                FontSize = 11,
                Margin = new Thickness(14, 0, 14, 8)
            };
            WpfGrid.SetRow(_lblStats, 2);
            leftGrid.Children.Add(_lblStats);

            WpfGrid.SetColumn(leftGrid, 0);
            bodyGrid.Children.Add(leftGrid);

            // Разделитель
            GridSplitter splitter = new GridSplitter
            {
                Width = 2,
                Background = borderCard,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            WpfGrid.SetColumn(splitter, 1);
            bodyGrid.Children.Add(splitter);

            // Правая колонка: Настройки замены
            ScrollViewer rightScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            StackPanel rightStack = new StackPanel { Margin = new Thickness(10, 10, 14, 6) };
            rightScroll.Content = rightStack;

            // Блок 1: Целевой шрифт
            Border fontCard = CreateCard("ЦЕЛЕВОЙ ШРИФТ", bgCard, borderCard);
            StackPanel fontStack = new StackPanel();

            _cbTargetFont = new ComboBox
            {
                Height = 30,
                Foreground = Brushes.Black,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 53)),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold
            };

            // Заполняем шрифты
            PopulateSystemFonts();
            fontStack.Children.Add(_cbTargetFont);
            fontCard.Child = fontStack;
            rightStack.Children.Add(fontCard);

            // Блок 2: Параметры начертания
            Border styleCard = CreateCard("СВОЙСТВА ТЕКСТА", bgCard, borderCard);
            StackPanel styleStack = new StackPanel();

            // Коэффициент сжатия
            StackPanel scaleStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            _chkWidthScale = new CheckBox { Content = "Коэф. сжатия:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Width = 150 };
            _txtWidthScale = new System.Windows.Controls.TextBox { Text = "0.8", Width = 70, Height = 26, VerticalAlignment = VerticalAlignment.Center, Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 53)), Foreground = Brushes.White };
            scaleStack.Children.Add(_chkWidthScale);
            scaleStack.Children.Add(_txtWidthScale);
            styleStack.Children.Add(scaleStack);

            // Размер шрифта
            StackPanel sizeStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            _chkTextSize = new CheckBox { Content = "Размер текста (мм):", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Width = 150 };
            _txtTextSize = new System.Windows.Controls.TextBox { Text = "2.5", Width = 70, Height = 26, VerticalAlignment = VerticalAlignment.Center, Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 53)), Foreground = Brushes.White };
            sizeStack.Children.Add(_chkTextSize);
            sizeStack.Children.Add(_txtTextSize);
            styleStack.Children.Add(sizeStack);

            // Начертание (Bold, Italic, Underline)
            TextBlock lblFormat = new TextBlock { Text = "Начертание:", Foreground = textMuted, Margin = new Thickness(0, 4, 0, 4) };
            styleStack.Children.Add(lblFormat);

            WrapPanel formatWrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
            _chkBold = new CheckBox { Content = "Полужирный", Foreground = Brushes.White, Margin = new Thickness(0, 0, 14, 4) };
            _chkItalic = new CheckBox { Content = "Курсив", Foreground = Brushes.White, Margin = new Thickness(0, 0, 14, 4) };
            _chkUnderline = new CheckBox { Content = "Подчеркнутый", Foreground = Brushes.White, Margin = new Thickness(0, 0, 14, 4) };
            formatWrap.Children.Add(_chkBold);
            formatWrap.Children.Add(_chkItalic);
            formatWrap.Children.Add(_chkUnderline);
            styleStack.Children.Add(formatWrap);

            // Подложка (Фон)
            _chkBackground = new CheckBox { Content = "Изменить подложку:", Foreground = Brushes.White, Margin = new Thickness(0, 4, 0, 4) };
            StackPanel bgRadioStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(20, 0, 0, 8) };
            _rbTransparent = new RadioButton { Content = "Прозрачная", IsChecked = true, Foreground = Brushes.White, Margin = new Thickness(0, 0, 14, 0) };
            _rbOpaque = new RadioButton { Content = "Непрозрачная", Foreground = Brushes.White };
            bgRadioStack.Children.Add(_rbTransparent);
            bgRadioStack.Children.Add(_rbOpaque);
            styleStack.Children.Add(_chkBackground);
            styleStack.Children.Add(bgRadioStack);

            // Цвет
            StackPanel colorStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            _chkColor = new CheckBox { Content = "Цвет текста:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Width = 150 };
            _cbColor = new ComboBox
            {
                Width = 140,
                Height = 26,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.Black,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 53))
            };
            _cbColor.Items.Add("Черный");
            _cbColor.Items.Add("Синий");
            _cbColor.Items.Add("Красный");
            _cbColor.Items.Add("Зеленый");
            _cbColor.Items.Add("Серый");
            _cbColor.SelectedIndex = 0;
            colorStack.Children.Add(_chkColor);
            colorStack.Children.Add(_cbColor);
            styleStack.Children.Add(colorStack);

            styleCard.Child = styleStack;
            rightStack.Children.Add(styleCard);

            // Блок 3: Переименование стилей
            Border renameCard = CreateCard("ПЕРЕИМЕНОВАНИЕ СТИЛЕЙ", bgCard, borderCard);
            StackPanel renameStack = new StackPanel();

            _chkRename = new CheckBox { Content = "Заменить текст в именах стилей", Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 6) };
            renameStack.Children.Add(_chkRename);

            WpfGrid renGrid = new WpfGrid();
            renGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            renGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            renGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _txtFindName = new System.Windows.Controls.TextBox { Text = "ISOCPEUR", Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 53)), Foreground = Brushes.White, Padding = new Thickness(4) };
            _txtReplaceName = new System.Windows.Controls.TextBox { Text = "GOST", Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 53)), Foreground = Brushes.White, Padding = new Thickness(4) };

            WpfGrid.SetColumn(_txtFindName, 0);
            WpfGrid.SetColumn(_txtReplaceName, 2);
            renGrid.Children.Add(_txtFindName);
            renGrid.Children.Add(_txtReplaceName);
            renameStack.Children.Add(renGrid);

            renameCard.Child = renameStack;
            rightStack.Children.Add(renameCard);

            WpfGrid.SetColumn(rightScroll, 2);
            bodyGrid.Children.Add(rightScroll);

            WpfGrid.SetRow(bodyGrid, 1);
            mainGrid.Children.Add(bodyGrid);

            // 3. Подвал с кнопкой действия
            Border footerBorder = new Border
            {
                Background = bgCard,
                BorderBrush = borderCard,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(14, 10, 14, 12)
            };
            WpfGrid footerGrid = new WpfGrid();
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Button btnCancel = new Button
            {
                Content = "Отмена",
                Height = 34,
                Padding = new Thickness(16, 0, 16, 0),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(58, 58, 66)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btnCancel.Click += (s, e) => DialogResult = false;
            WpfGrid.SetColumn(btnCancel, 1);
            footerGrid.Children.Add(btnCancel);

            Button btnApply = new Button
            {
                Content = "Заменить шрифт во всем проекте",
                Height = 34,
                Padding = new Thickness(22, 0, 22, 0),
                Background = accentRed,
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btnApply.Click += (s, e) => OnApply();
            WpfGrid.SetColumn(btnApply, 3);
            footerGrid.Children.Add(btnApply);

            footerBorder.Child = footerGrid;
            WpfGrid.SetRow(footerBorder, 2);
            mainGrid.Children.Add(footerBorder);
        }

        private Border CreateCard(string title, SolidColorBrush bg, SolidColorBrush border)
        {
            Border b = new Border
            {
                Background = bg,
                BorderBrush = border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 10)
            };
            StackPanel sp = new StackPanel();
            sp.Children.Add(new TextBlock
            {
                Text = title,
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 180, 200)),
                Margin = new Thickness(0, 0, 0, 8)
            });
            b.Child = sp;
            return b;
        }

        private void PopulateSystemFonts()
        {
            var fontNames = new List<string>();
            foreach (var font in Fonts.SystemFontFamilies)
            {
                fontNames.Add(font.Source);
            }
            fontNames.Sort();

            // Популярные проектные шрифты наверх
            string[] popular = new string[] { "GOST 2.304 type A", "GOST Common", "ISOCPEUR", "Arial", "Calibri", "Segoe UI", "Tahoma" };
            foreach (string p in popular.Reverse())
            {
                if (fontNames.Contains(p))
                {
                    fontNames.Remove(p);
                    fontNames.Insert(0, p);
                }
            }

            foreach (var name in fontNames)
            {
                _cbTargetFont.Items.Add(name);
            }

            _cbTargetFont.SelectedIndex = 0;
        }

        private void LoadProjectStyles()
        {
            StyleGroups.Clear();

            // 1. Текстовые стили
            var groupText = new StyleGroupNode { Title = "Стили текста (TextNoteType)" };
            var textTypes = new FilteredElementCollector(_doc)
                .OfClass(typeof(TextNoteType))
                .Cast<TextNoteType>()
                .OrderBy(t => t.Name)
                .ToList();

            foreach (var t in textTypes)
            {
                string curFont = t.get_Parameter(BuiltInParameter.TEXT_FONT)?.AsString() ?? "Unknown";
                var item = new StyleItemNode { Name = t.Name, CurrentFont = curFont, Id = t.Id };
                item.PropertyChanged += (s, e) => { groupText.UpdateCheckState(); UpdateStats(); };
                groupText.Items.Add(item);
            }
            if (groupText.Items.Count > 0) StyleGroups.Add(groupText);

            // 2. Размерные стили
            var groupDim = new StyleGroupNode { Title = "Размерные стили (DimensionType)" };
            var dimTypes = new FilteredElementCollector(_doc)
                .OfClass(typeof(DimensionType))
                .Cast<DimensionType>()
                .OrderBy(t => t.Name)
                .ToList();

            foreach (var d in dimTypes)
            {
                string curFont = d.get_Parameter(BuiltInParameter.TEXT_FONT)?.AsString() ?? "Unknown";
                var item = new StyleItemNode { Name = d.Name, CurrentFont = curFont, Id = d.Id };
                item.PropertyChanged += (s, e) => { groupDim.UpdateCheckState(); UpdateStats(); };
                groupDim.Items.Add(item);
            }
            if (groupDim.Items.Count > 0) StyleGroups.Add(groupDim);

            // 3. Спецификации
            var groupSched = new StyleGroupNode { Title = "Спецификации (ViewSchedule)" };
            var schedules = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSchedule))
                .Cast<ViewSchedule>()
                .Where(v => !v.IsTemplate)
                .OrderBy(v => v.Name)
                .ToList();

            foreach (var s in schedules)
            {
                var item = new StyleItemNode { Name = s.Name, CurrentFont = "Таблица", Id = s.Id };
                item.PropertyChanged += (s, e) => { groupSched.UpdateCheckState(); UpdateStats(); };
                groupSched.Items.Add(item);
            }
            if (groupSched.Items.Count > 0) StyleGroups.Add(groupSched);

            // 4. Семейства аннотаций и марок
            var groupFam = new StyleGroupNode { Title = "Семейства марок и аннотаций (Family)" };
            BuiltInCategory[] annotCats = new BuiltInCategory[]
            {
                BuiltInCategory.OST_GenericAnnotation,
                BuiltInCategory.OST_TitleBlocks,
                BuiltInCategory.OST_RoomTags,
                BuiltInCategory.OST_DoorTags,
                BuiltInCategory.OST_WindowTags,
                BuiltInCategory.OST_PipeTags,
                BuiltInCategory.OST_DuctTags,
                BuiltInCategory.OST_MultiCategoryTags
            };

            var annotFamilies = new FilteredElementCollector(_doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .Where(f => f.IsEditable && f.FamilyCategory != null && annotCats.Any(c => (long)c == GetCatIdValue(f.FamilyCategory.Id)))
                .OrderBy(f => f.Name)
                .ToList();

            foreach (var f in annotFamilies)
            {
                var item = new StyleItemNode { Name = f.Name, CurrentFont = "RFA", Id = f.Id };
                item.PropertyChanged += (s, e) => { groupFam.UpdateCheckState(); UpdateStats(); };
                groupFam.Items.Add(item);
            }
            if (groupFam.Items.Count > 0) StyleGroups.Add(groupFam);

            UpdateStats();
        }

        private void SetAllChecked(bool isChecked)
        {
            foreach (var g in StyleGroups)
            {
                g.IsChecked = isChecked;
            }
            UpdateStats();
        }

        private void UpdateStats()
        {
            int totalSelected = StyleGroups.Sum(g => g.Items.Count(i => i.IsChecked));
            int total = StyleGroups.Sum(g => g.Items.Count);
            _lblStats.Text = $"Выбрано элементов для обработки: {totalSelected} из {total}";
        }

        private void OnApply()
        {
            SelectedTextTypeIds.Clear();
            SelectedDimTypeIds.Clear();
            SelectedScheduleIds.Clear();
            SelectedFamilyIds.Clear();

            foreach (var g in StyleGroups)
            {
                foreach (var i in g.Items)
                {
                    if (!i.IsChecked) continue;

                    if (g.Title.Contains("TextNoteType")) SelectedTextTypeIds.Add(i.Id);
                    else if (g.Title.Contains("DimensionType")) SelectedDimTypeIds.Add(i.Id);
                    else if (g.Title.Contains("ViewSchedule")) SelectedScheduleIds.Add(i.Id);
                    else if (g.Title.Contains("Family")) SelectedFamilyIds.Add(i.Id);
                }
            }

            int count = SelectedTextTypeIds.Count + SelectedDimTypeIds.Count + SelectedScheduleIds.Count + SelectedFamilyIds.Count;
            if (count == 0)
            {
                MessageBox.Show("Не выбрано ни одного элемента для замены шрифта.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string targetFont = _cbTargetFont.SelectedItem as string ?? "Arial";

            double widthScale = 1.0;
            double.TryParse(_txtWidthScale.Text.Trim().Replace(',', '.'),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out widthScale);

            double textSize = 2.5;
            double.TryParse(_txtTextSize.Text.Trim().Replace(',', '.'),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out textSize);

            int colorRgb = 0;
            if (_cbColor.SelectedIndex == 1) colorRgb = (255 << 16); // Blue
            else if (_cbColor.SelectedIndex == 2) colorRgb = 255; // Red
            else if (_cbColor.SelectedIndex == 3) colorRgb = (255 << 8); // Green
            else if (_cbColor.SelectedIndex == 4) colorRgb = 0x808080; // Gray

            Settings = new FontReplacementSettings
            {
                TargetFontName = targetFont,
                ChangeWidthScale = _chkWidthScale.IsChecked == true,
                WidthScale = widthScale,
                ChangeSize = _chkTextSize.IsChecked == true,
                TextSizeMm = textSize,
                ChangeBold = _chkBold.IsChecked == true,
                IsBold = true,
                ChangeItalic = _chkItalic.IsChecked == true,
                IsItalic = true,
                ChangeUnderline = _chkUnderline.IsChecked == true,
                IsUnderline = true,
                ChangeBackground = _chkBackground.IsChecked == true,
                IsTransparent = _rbTransparent.IsChecked == true,
                ChangeColor = _chkColor.IsChecked == true,
                ColorRgb = colorRgb,
                RenameStyles = _chkRename.IsChecked == true,
                FindTextInName = _txtFindName.Text.Trim(),
                ReplaceTextInName = _txtReplaceName.Text.Trim()
            };

            DialogResult = true;
        }

        private static long GetCatIdValue(ElementId id)
        {
            if (id == null) return -1;
#if NET8_0_OR_GREATER
            return id.Value;
#else
            return id.IntegerValue;
#endif
        }
    }
}
