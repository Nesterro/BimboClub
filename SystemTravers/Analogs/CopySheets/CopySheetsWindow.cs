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
using WpfGrid = System.Windows.Controls.Grid;
using WpfBinding = System.Windows.Data.Binding;

namespace BimboClub.Analogs.CopySheets
{
    public class SheetItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }

        public ViewSheet Sheet { get; set; }
        public string SheetNumber => Sheet?.SheetNumber ?? "";
        public string SheetName => Sheet?.Name ?? "";

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public class TargetDocItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }

        public Document Document { get; set; }
        public string Title => Document?.Title ?? "";

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public class CopySheetsWindow : Window
    {
        private readonly Autodesk.Revit.UI.UIApplication _uiApp;
        private readonly List<Document> _openDocs;

        private ComboBox _cbSourceDoc;
        private ObservableCollection<SheetItem> _sourceSheets;
        private ICollectionView _sheetsView;
        private TextBox _tbFilterSheets;

        private ObservableCollection<TargetDocItem> _targetDocs;
        private ItemsControl _icTargetDocs;

        private CheckBox _chkCopyParameters;
        private CheckBox _chkCopyTitleBlock;
        private CheckBox _chkCopyDraftingViews;
        private CheckBox _chkCopyLegends;
        private CheckBox _chkCopySchedules;

        private RadioButton _rbAddSuffix;
        private TextBox _tbSuffix;
        private RadioButton _rbSkip;
        private RadioButton _rbOverwriteParams;

        public Document SelectedSourceDoc => (_cbSourceDoc.SelectedItem as Document);
        public List<ViewSheet> SelectedSheets => _sourceSheets.Where(s => s.IsSelected).Select(s => s.Sheet).ToList();
        public List<Document> SelectedTargetDocs => _targetDocs.Where(d => d.IsSelected).Select(d => d.Document).ToList();
        public CopySheetsOptions Options { get; private set; }

        public CopySheetsWindow(Autodesk.Revit.UI.UIApplication uiApp)
        {
            _uiApp = uiApp;
            _openDocs = _uiApp.Application.Documents
                .Cast<Document>()
                .Where(d => !d.IsFamilyDocument)
                .ToList();

            _sourceSheets = new ObservableCollection<SheetItem>();
            _targetDocs = new ObservableCollection<TargetDocItem>();

            InitializeUi();
            PopulateSourceDocs();
        }

        private void InitializeUi()
        {
            Title = "Копировать листы в документы — BimboClub Tools";
            Width = 840;
            Height = 650;
            MinHeight = 520;
            MinWidth = 700;
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
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Source Doc Bar
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Split Body
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Bottom Bar
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
                Text = "КОПИРОВАТЬ ЛИСТЫ В ДОКУМЕНТЫ",
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Foreground = Brushes.White
            });
            headerStack.Children.Add(new TextBlock
            {
                Text = "Перенос листов, чертёжных видов, легенд, спецификаций и рамок между открытыми проектами",
                FontSize = 11,
                Foreground = textMuted,
                Margin = new Thickness(0, 3, 0, 0)
            });
            headerBorder.Child = headerStack;
            WpfGrid.SetRow(headerBorder, 0);
            mainGrid.Children.Add(headerBorder);

            // 2. Исходный документ
            Border sourceDocBorder = new Border
            {
                Background = bgCard,
                BorderBrush = borderCard,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(14, 8, 14, 8)
            };
            WpfGrid sourceGrid = new WpfGrid();
            sourceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            sourceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBlock lblSource = new TextBlock
            {
                Text = "Исходный документ:",
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            WpfGrid.SetColumn(lblSource, 0);
            sourceGrid.Children.Add(lblSource);

            _cbSourceDoc = new ComboBox
            {
                DisplayMemberPath = "Title",
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(24, 24, 28)),
                Foreground = Brushes.White,
                BorderBrush = borderCard,
                Padding = new Thickness(8, 4, 8, 4)
            };
            _cbSourceDoc.SelectionChanged += (s, e) => OnSourceDocChanged();
            WpfGrid.SetColumn(_cbSourceDoc, 1);
            sourceGrid.Children.Add(_cbSourceDoc);

            sourceDocBorder.Child = sourceGrid;
            WpfGrid.SetRow(sourceDocBorder, 1);
            mainGrid.Children.Add(sourceDocBorder);

            // 3. Основная область (Две колонки: Слева листы, справа целевые документы и опции)
            WpfGrid bodyGrid = new WpfGrid { Margin = new Thickness(12) };
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5, GridUnitType.Star) });
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) }); // spacer
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4, GridUnitType.Star) });

            // Левая колонка: Список листов
            Border sheetsCard = new Border
            {
                Background = bgCard,
                BorderBrush = borderCard,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10)
            };
            WpfGrid sheetsGrid = new WpfGrid();
            sheetsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header & Filter
            sheetsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Select all buttons
            sheetsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // List

            // Фильтр
            WpfGrid filterGrid = new WpfGrid { Margin = new Thickness(0, 0, 0, 6) };
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBlock lblFilter = new TextBlock
            {
                Text = "Поиск:",
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            WpfGrid.SetColumn(lblFilter, 0);
            filterGrid.Children.Add(lblFilter);

            _tbFilterSheets = new TextBox
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(24, 24, 28)),
                Foreground = Brushes.White,
                BorderBrush = borderCard,
                Padding = new Thickness(4)
            };
            _tbFilterSheets.TextChanged += (s, e) => _sheetsView?.Refresh();
            WpfGrid.SetColumn(_tbFilterSheets, 1);
            filterGrid.Children.Add(_tbFilterSheets);

            WpfGrid.SetRow(filterGrid, 0);
            sheetsGrid.Children.Add(filterGrid);

            // Кнопки выбора
            StackPanel btnSelectPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 8)
            };
            Button btnSelectAll = CreateSmallButton("Выбрать все", (s, e) => SetAllSheets(true));
            Button btnDeselectAll = CreateSmallButton("Снять выбор", (s, e) => SetAllSheets(false));
            btnSelectPanel.Children.Add(btnSelectAll);
            btnSelectPanel.Children.Add(btnDeselectAll);
            WpfGrid.SetRow(btnSelectPanel, 1);
            sheetsGrid.Children.Add(btnSelectPanel);

            // Таблица листов
            DataGrid dgSheets = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HorizontalGridLinesBrush = borderCard,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(24, 24, 28)),
                Foreground = Brushes.White,
                BorderBrush = borderCard,
                RowBackground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(28, 28, 34)),
                AlternatingRowBackground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(34, 34, 40))
            };

            // Чекбокс колонка
            DataGridCheckBoxColumn colCheck = new DataGridCheckBoxColumn
            {
                Binding = new WpfBinding(nameof(SheetItem.IsSelected)) { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
                Width = new DataGridLength(35)
            };
            dgSheets.Columns.Add(colCheck);

            // Номер листа
            DataGridTextColumn colNum = new DataGridTextColumn
            {
                Header = "Номер",
                Binding = new WpfBinding(nameof(SheetItem.SheetNumber)),
                IsReadOnly = true,
                Width = new DataGridLength(90)
            };
            dgSheets.Columns.Add(colNum);

            // Имя листа
            DataGridTextColumn colName = new DataGridTextColumn
            {
                Header = "Имя листа",
                Binding = new WpfBinding(nameof(SheetItem.SheetName)),
                IsReadOnly = true,
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            };
            dgSheets.Columns.Add(colName);

            _sheetsView = CollectionViewSource.GetDefaultView(_sourceSheets);
            _sheetsView.Filter = FilterSheetItem;
            dgSheets.ItemsSource = _sheetsView;

            WpfGrid.SetRow(dgSheets, 2);
            sheetsGrid.Children.Add(dgSheets);

            sheetsCard.Child = sheetsGrid;
            WpfGrid.SetColumn(sheetsCard, 0);
            bodyGrid.Children.Add(sheetsCard);

            // Правая колонка: Документы-приемники и опции
            ScrollViewer rightScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            StackPanel rightPanel = new StackPanel();

            // Блок Целевые документы
            Border targetCard = new Border
            {
                Background = bgCard,
                BorderBrush = borderCard,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 10)
            };
            StackPanel targetStack = new StackPanel();
            targetStack.Children.Add(new TextBlock
            {
                Text = "ЦЕЛЕВЫЕ ДОКУМЕНТЫ",
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 6)
            });

            _icTargetDocs = new ItemsControl
            {
                ItemsSource = _targetDocs,
                ItemTemplate = CreateTargetDocTemplate()
            };
            targetStack.Children.Add(_icTargetDocs);

            StackPanel targetBtnStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 6, 0, 0)
            };
            targetBtnStack.Children.Add(CreateSmallButton("Выбрать все", (s, e) =>
            {
                foreach (var td in _targetDocs) td.IsSelected = true;
            }));
            targetBtnStack.Children.Add(CreateSmallButton("Снять выбор", (s, e) =>
            {
                foreach (var td in _targetDocs) td.IsSelected = false;
            }));
            targetStack.Children.Add(targetBtnStack);

            targetCard.Child = targetStack;
            rightPanel.Children.Add(targetCard);

            // Блок Опции содержимого
            Border optionsCard = new Border
            {
                Background = bgCard,
                BorderBrush = borderCard,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 10)
            };
            StackPanel optionsStack = new StackPanel();
            optionsStack.Children.Add(new TextBlock
            {
                Text = "КОПИРОВАТЬ ЭЛЕМЕНТЫ ЛИСТА",
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 6)
            });

            _chkCopyParameters = CreateDarkCheckBox("Параметры листа", true);
            _chkCopyTitleBlock = CreateDarkCheckBox("Основная надпись (рамка)", true);
            _chkCopyDraftingViews = CreateDarkCheckBox("Чертёжные виды", true);
            _chkCopyLegends = CreateDarkCheckBox("Легенды", true);
            _chkCopySchedules = CreateDarkCheckBox("Спецификации", true);

            optionsStack.Children.Add(_chkCopyParameters);
            optionsStack.Children.Add(_chkCopyTitleBlock);
            optionsStack.Children.Add(_chkCopyDraftingViews);
            optionsStack.Children.Add(_chkCopyLegends);
            optionsStack.Children.Add(_chkCopySchedules);

            optionsCard.Child = optionsStack;
            rightPanel.Children.Add(optionsCard);

            // Блок Конфликты
            Border conflictCard = new Border
            {
                Background = bgCard,
                BorderBrush = borderCard,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10)
            };
            StackPanel conflictStack = new StackPanel();
            conflictStack.Children.Add(new TextBlock
            {
                Text = "ЕСЛИ ЛИСТ С ТАКИМ НОМЕРОМ ЕСТЬ",
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 6)
            });

            StackPanel suffixRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
            _rbAddSuffix = new RadioButton
            {
                Content = "Добавить суффикс:",
                Foreground = Brushes.White,
                IsChecked = true,
                VerticalAlignment = VerticalAlignment.Center
            };
            _tbSuffix = new TextBox
            {
                Text = "_Копия",
                Width = 80,
                Margin = new Thickness(8, 0, 0, 0),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(24, 24, 28)),
                Foreground = Brushes.White,
                BorderBrush = borderCard,
                Padding = new Thickness(3)
            };
            suffixRow.Children.Add(_rbAddSuffix);
            suffixRow.Children.Add(_tbSuffix);
            conflictStack.Children.Add(suffixRow);

            _rbSkip = new RadioButton
            {
                Content = "Пропустить лист",
                Foreground = Brushes.White,
                Margin = new Thickness(0, 4, 0, 2)
            };
            conflictStack.Children.Add(_rbSkip);

            _rbOverwriteParams = new RadioButton
            {
                Content = "Обновить параметры существующего",
                Foreground = Brushes.White,
                Margin = new Thickness(0, 4, 0, 2)
            };
            conflictStack.Children.Add(_rbOverwriteParams);

            conflictCard.Child = conflictStack;
            rightPanel.Children.Add(conflictCard);

            rightScroll.Content = rightPanel;
            WpfGrid.SetColumn(rightScroll, 2);
            bodyGrid.Children.Add(rightScroll);

            WpfGrid.SetRow(bodyGrid, 2);
            mainGrid.Children.Add(bodyGrid);

            // 4. Подвал (Кнопки)
            Border footerBorder = new Border
            {
                Background = bgCard,
                BorderBrush = borderCard,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(14, 10, 14, 10)
            };
            WpfGrid footerGrid = new WpfGrid();
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel btnActionPanel = new StackPanel { Orientation = Orientation.Horizontal };
            Button btnRun = new Button
            {
                Content = "Копировать выбранные листы",
                Width = 240,
                Height = 34,
                Background = accentRed,
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 10, 0)
            };
            btnRun.Click += BtnRun_Click;

            Button btnCancel = new Button
            {
                Content = "Закрыть",
                Width = 90,
                Height = 34,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 52)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btnCancel.Click += (s, e) => DialogResult = false;

            btnActionPanel.Children.Add(btnRun);
            btnActionPanel.Children.Add(btnCancel);
            WpfGrid.SetColumn(btnActionPanel, 1);
            footerGrid.Children.Add(btnActionPanel);

            footerBorder.Child = footerGrid;
            WpfGrid.SetRow(footerBorder, 3);
            mainGrid.Children.Add(footerBorder);
        }

        private void PopulateSourceDocs()
        {
            _cbSourceDoc.ItemsSource = _openDocs;
            var activeDoc = _uiApp.ActiveUIDocument?.Document;
            if (activeDoc != null && _openDocs.Contains(activeDoc))
            {
                _cbSourceDoc.SelectedItem = activeDoc;
            }
            else if (_openDocs.Count > 0)
            {
                _cbSourceDoc.SelectedIndex = 0;
            }
        }

        private void OnSourceDocChanged()
        {
            var srcDoc = SelectedSourceDoc;
            _sourceSheets.Clear();
            _targetDocs.Clear();

            if (srcDoc == null) return;

            // Заполняем листы исходного документа
            var sheets = new FilteredElementCollector(srcDoc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Where(s => !s.IsTemplate)
                .OrderBy(s => s.SheetNumber)
                .ToList();

            foreach (var s in sheets)
            {
                _sourceSheets.Add(new SheetItem { Sheet = s, IsSelected = false });
            }

            // Заполняем доступные целевые документы (все кроме выбранного исходного)
            foreach (var doc in _openDocs)
            {
                if (!doc.Equals(srcDoc))
                {
                    _targetDocs.Add(new TargetDocItem { Document = doc, IsSelected = true });
                }
            }
        }

        private bool FilterSheetItem(object obj)
        {
            if (string.IsNullOrWhiteSpace(_tbFilterSheets?.Text))
                return true;

            if (obj is SheetItem item)
            {
                string filter = _tbFilterSheets.Text.Trim();
                return item.SheetNumber.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                       item.SheetName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
            }
            return true;
        }

        private void SetAllSheets(bool isSelected)
        {
            foreach (var item in _sourceSheets)
            {
                if (FilterSheetItem(item))
                {
                    item.IsSelected = isSelected;
                }
            }
        }

        private void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            var selectedSheets = SelectedSheets;
            if (selectedSheets.Count == 0)
            {
                MessageBox.Show("Выберите хотя бы один лист для копирования.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var targetDocs = SelectedTargetDocs;
            if (targetDocs.Count == 0)
            {
                MessageBox.Show("Выберите хотя бы один целевой документ.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ConflictResolution conflictMode = ConflictResolution.AddSuffix;
            if (_rbSkip.IsChecked == true) conflictMode = ConflictResolution.Skip;
            else if (_rbOverwriteParams.IsChecked == true) conflictMode = ConflictResolution.OverwriteParameters;

            Options = new CopySheetsOptions
            {
                CopySheetParameters = _chkCopyParameters.IsChecked == true,
                CopyTitleBlock = _chkCopyTitleBlock.IsChecked == true,
                CopyDraftingViews = _chkCopyDraftingViews.IsChecked == true,
                CopyLegendViews = _chkCopyLegends.IsChecked == true,
                CopySchedules = _chkCopySchedules.IsChecked == true,
                ConflictMode = conflictMode,
                Suffix = string.IsNullOrWhiteSpace(_tbSuffix.Text) ? "_Копия" : _tbSuffix.Text.Trim()
            };

            DialogResult = true;
        }

        private Button CreateSmallButton(string text, RoutedEventHandler handler)
        {
            var btn = new Button
            {
                Content = text,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 52)),
                Foreground = Brushes.White,
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(0, 0, 6, 0),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontSize = 11
            };
            btn.Click += handler;
            return btn;
        }

        private CheckBox CreateDarkCheckBox(string text, bool isChecked)
        {
            return new CheckBox
            {
                Content = text,
                IsChecked = isChecked,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 3, 0, 3)
            };
        }

        private DataTemplate CreateTargetDocTemplate()
        {
            var factory = new FrameworkElementFactory(typeof(CheckBox));
            factory.SetBinding(CheckBox.IsCheckedProperty, new WpfBinding(nameof(TargetDocItem.IsSelected)));
            factory.SetBinding(CheckBox.ContentProperty, new WpfBinding(nameof(TargetDocItem.Title)));
            factory.SetValue(CheckBox.ForegroundProperty, Brushes.White);
            factory.SetValue(CheckBox.MarginProperty, new Thickness(0, 3, 0, 3));

            return new DataTemplate { VisualTree = factory };
        }
    }
}
