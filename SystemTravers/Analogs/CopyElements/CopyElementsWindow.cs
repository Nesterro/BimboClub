using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Autodesk.Revit.DB;
using WpfGrid = System.Windows.Controls.Grid;
using WpfBinding = System.Windows.Data.Binding;
using MediaColor = System.Windows.Media.Color;

namespace BimboClub.Analogs.CopyElements
{
    public class DocSourceOption
    {
        public string Title { get; set; }
        public Document Doc { get; set; }
        public RevitLinkInstance LinkInstance { get; set; }
        public bool IsLink => LinkInstance != null;

        public override string ToString() => Title;
    }

    public class StandardCategoryGroup
    {
        public string CategoryName { get; set; }
        public ObservableCollection<StandardElementItem> Items { get; } = new ObservableCollection<StandardElementItem>();
    }

    public class StandardElementItem : INotifyPropertyChanged
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

        public Element Element { get; set; }
        public ElementId Id => Element?.Id;
        public string Name { get; set; }
        public string CategoryName { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public class ModelElementItem : INotifyPropertyChanged
    {
        private bool _isSelected = true;
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

        public Element Element { get; set; }
        public string ElementIdText
        {
            get
            {
                if (Element == null) return "";
#if NET8_0_OR_GREATER
                return Element.Id.Value.ToString();
#else
                return Element.Id.IntegerValue.ToString();
#endif
            }
        }
        public string CategoryName => Element?.Category?.Name ?? "Модель";
        public string Name => Element?.Name ?? "";

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public class TargetDocumentItem : INotifyPropertyChanged
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

    public class CopyElementsWindow : Window
    {
        private readonly Autodesk.Revit.UI.UIApplication _uiApp;
        private readonly List<Document> _openDocs;

        private ComboBox _cbSourceDoc;
        private CheckBox _chkShowLinks;

        private TabControl _tabControl;

        // Вкладка 1: Стандарты
        private TextBox _tbFilterStandards;
        private CheckBox _chkRegex;
        private TreeView _tvStandards;
        private ObservableCollection<StandardCategoryGroup> _standardGroups;

        // Вкладка 2: Элементы модели
        private ObservableCollection<ModelElementItem> _modelElements;
        private DataGrid _dgModelElements;
        private TextBlock _lblModelCount;

        // Целевые документы
        private ObservableCollection<TargetDocumentItem> _targetDocs;
        private ItemsControl _icTargetDocs;

        // Опции
        private RadioButton _rbUseDestination;
        private RadioButton _rbCreateNewTypes;
        private CheckBox _chkSuppressWarnings;

        public Document SelectedSourceDoc
        {
            get
            {
                var opt = _cbSourceDoc.SelectedItem as DocSourceOption;
                return opt?.Doc;
            }
        }

        public Autodesk.Revit.DB.Transform SelectedTransform
        {
            get
            {
                var opt = _cbSourceDoc.SelectedItem as DocSourceOption;
                return opt?.LinkInstance != null ? opt.LinkInstance.GetTotalTransform() : Autodesk.Revit.DB.Transform.Identity;
            }
        }

        public List<Document> SelectedTargetDocs => _targetDocs.Where(d => d.IsSelected).Select(d => d.Document).ToList();

        public List<ElementId> SelectedElementIds
        {
            get
            {
                var list = new List<ElementId>();
                if (_tabControl.SelectedIndex == 0)
                {
                    // Из вкладки стандартов
                    foreach (var grp in _standardGroups)
                    {
                        foreach (var item in grp.Items)
                        {
                            if (item.IsSelected && item.Id != null)
                            {
                                list.Add(item.Id);
                            }
                        }
                    }
                }
                else
                {
                    // Из вкладки модели
                    foreach (var m in _modelElements)
                    {
                        if (m.IsSelected && m.Element != null)
                        {
                            list.Add(m.Element.Id);
                        }
                    }
                }
                return list;
            }
        }

        public CopyElementsOptions Options { get; private set; }

        public CopyElementsWindow(Autodesk.Revit.UI.UIApplication uiApp)
        {
            _uiApp = uiApp;
            _openDocs = _uiApp.Application.Documents
                .Cast<Document>()
                .Where(d => !d.IsFamilyDocument)
                .ToList();

            _standardGroups = new ObservableCollection<StandardCategoryGroup>();
            _modelElements = new ObservableCollection<ModelElementItem>();
            _targetDocs = new ObservableCollection<TargetDocumentItem>();

            InitializeUi();
            PopulateSourceDocs();
            LoadModelSelection();
        }

        private void InitializeUi()
        {
            Title = "Копировать элементы в документы — BimboClub Tools";
            Width = 920;
            Height = 680;
            MinHeight = 540;
            MinWidth = 760;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(MediaColor.FromRgb(24, 24, 28));
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 13;

            var bgCard = new SolidColorBrush(MediaColor.FromRgb(32, 32, 38));
            var borderCard = new SolidColorBrush(MediaColor.FromRgb(48, 48, 56));
            var accentRed = new SolidColorBrush(MediaColor.FromRgb(179, 14, 45));
            var textMuted = new SolidColorBrush(MediaColor.FromRgb(180, 180, 200));

            WpfGrid mainGrid = new WpfGrid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Source Doc Bar
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Tabs + Right
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer
            Content = mainGrid;

            // 1. Шапка
            Border headerBorder = new Border
            {
                Background = new LinearGradientBrush(MediaColor.FromRgb(18, 18, 20), MediaColor.FromRgb(179, 14, 45), 0),
                Padding = new Thickness(16, 12, 16, 12)
            };
            StackPanel headerStack = new StackPanel();
            headerStack.Children.Add(new TextBlock
            {
                Text = "КОПИРОВАТЬ ЭЛЕМЕНТЫ В ДОКУМЕНТЫ",
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Foreground = Brushes.White
            });
            headerStack.Children.Add(new TextBlock
            {
                Text = "Перенос стандартов проекта, типоразмеров, фильтров, спецификаций и элементов модели между проектами",
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
            sourceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

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
                Background = new SolidColorBrush(MediaColor.FromRgb(24, 24, 28)),
                Foreground = Brushes.White,
                BorderBrush = borderCard,
                Padding = new Thickness(8, 4, 8, 4)
            };
            _cbSourceDoc.SelectionChanged += (s, e) => OnSourceDocChanged();
            WpfGrid.SetColumn(_cbSourceDoc, 1);
            sourceGrid.Children.Add(_cbSourceDoc);

            _chkShowLinks = new CheckBox
            {
                Content = "Показывать связи",
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(14, 0, 0, 0)
            };
            _chkShowLinks.Checked += (s, e) => PopulateSourceDocs();
            _chkShowLinks.Unchecked += (s, e) => PopulateSourceDocs();
            WpfGrid.SetColumn(_chkShowLinks, 2);
            sourceGrid.Children.Add(_chkShowLinks);

            sourceDocBorder.Child = sourceGrid;
            WpfGrid.SetRow(sourceDocBorder, 1);
            mainGrid.Children.Add(sourceDocBorder);

            // 3. Основная область (Слева вкладки Стандарты/Модель, Справа Целевые документы и опции)
            WpfGrid bodyGrid = new WpfGrid { Margin = new Thickness(12) };
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6, GridUnitType.Star) });
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4, GridUnitType.Star) });

            // Вкладки слева
            _tabControl = new TabControl
            {
                Background = bgCard,
                BorderBrush = borderCard,
                Foreground = Brushes.White
            };

            // Вкладка 1: Стандарты проекта
            TabItem tabStandards = new TabItem { Header = " Стандарты и типоразмеры " };
            tabStandards.Content = CreateStandardsTabContent(borderCard, bgCard);
            _tabControl.Items.Add(tabStandards);

            // Вкладка 2: Элементы модели
            TabItem tabModel = new TabItem { Header = " Элементы модели " };
            tabModel.Content = CreateModelTabContent(borderCard, bgCard);
            _tabControl.Items.Add(tabModel);

            WpfGrid.SetColumn(_tabControl, 0);
            bodyGrid.Children.Add(_tabControl);

            // Правая панель (Целевые проекты и опции)
            bodyGrid.Children.Add(CreateRightPanel(bgCard, borderCard));

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
                Content = "Копировать элементы",
                Width = 220,
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
                Background = new SolidColorBrush(MediaColor.FromRgb(45, 45, 52)),
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

        private UIElement CreateStandardsTabContent(SolidColorBrush borderCard, SolidColorBrush bgCard)
        {
            WpfGrid grid = new WpfGrid { Margin = new Thickness(8) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Filter
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Buttons
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Tree

            // Фильтр
            WpfGrid filterGrid = new WpfGrid { Margin = new Thickness(0, 0, 0, 6) };
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock lblFilter = new TextBlock
            {
                Text = "Поиск:",
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            WpfGrid.SetColumn(lblFilter, 0);
            filterGrid.Children.Add(lblFilter);

            _tbFilterStandards = new TextBox
            {
                Background = new SolidColorBrush(MediaColor.FromRgb(24, 24, 28)),
                Foreground = Brushes.White,
                BorderBrush = borderCard,
                Padding = new Thickness(4)
            };
            _tbFilterStandards.TextChanged += (s, e) => ApplyStandardsFilter();
            WpfGrid.SetColumn(_tbFilterStandards, 1);
            filterGrid.Children.Add(_tbFilterStandards);

            _chkRegex = new CheckBox
            {
                Content = "RegEx",
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            _chkRegex.Checked += (s, e) => ApplyStandardsFilter();
            _chkRegex.Unchecked += (s, e) => ApplyStandardsFilter();
            WpfGrid.SetColumn(_chkRegex, 2);
            filterGrid.Children.Add(_chkRegex);

            WpfGrid.SetRow(filterGrid, 0);
            grid.Children.Add(filterGrid);

            // Кнопки управления выбором
            StackPanel btnBar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            btnBar.Children.Add(CreateSmallButton("Выбрать все", (s, e) => SetAllStandards(true)));
            btnBar.Children.Add(CreateSmallButton("Снять выбор", (s, e) => SetAllStandards(false)));
            WpfGrid.SetRow(btnBar, 1);
            grid.Children.Add(btnBar);

            // Дерево категорий и элементов
            _tvStandards = new TreeView
            {
                Background = new SolidColorBrush(MediaColor.FromRgb(24, 24, 28)),
                Foreground = Brushes.White,
                BorderBrush = borderCard,
                ItemsSource = _standardGroups
            };

            // Шаблон узлов категорий с дочерними элементами
            HierarchicalDataTemplate groupTemplate = new HierarchicalDataTemplate(typeof(StandardCategoryGroup));
            groupTemplate.ItemsSource = new WpfBinding(nameof(StandardCategoryGroup.Items));

            FrameworkElementFactory groupStack = new FrameworkElementFactory(typeof(StackPanel));
            groupStack.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

            FrameworkElementFactory groupText = new FrameworkElementFactory(typeof(TextBlock));
            groupText.SetBinding(TextBlock.TextProperty, new WpfBinding(nameof(StandardCategoryGroup.CategoryName)));
            groupText.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            groupText.SetValue(TextBlock.ForegroundProperty, Brushes.White);
            groupText.SetValue(TextBlock.MarginProperty, new Thickness(4, 2, 4, 2));

            groupStack.AppendChild(groupText);
            groupTemplate.VisualTree = groupStack;

            // Шаблон дочернего элемента с чекбоксом
            DataTemplate itemTemplate = new DataTemplate(typeof(StandardElementItem));
            FrameworkElementFactory itemStack = new FrameworkElementFactory(typeof(StackPanel));
            itemStack.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

            FrameworkElementFactory itemCheck = new FrameworkElementFactory(typeof(CheckBox));
            itemCheck.SetBinding(CheckBox.IsCheckedProperty, new WpfBinding(nameof(StandardElementItem.IsSelected)));
            itemCheck.SetBinding(CheckBox.ContentProperty, new WpfBinding(nameof(StandardElementItem.Name)));
            itemCheck.SetValue(CheckBox.ForegroundProperty, new SolidColorBrush(MediaColor.FromRgb(220, 220, 230)));
            itemCheck.SetValue(CheckBox.MarginProperty, new Thickness(0, 2, 0, 2));

            itemStack.AppendChild(itemCheck);
            itemTemplate.VisualTree = itemStack;

            groupTemplate.ItemTemplate = itemTemplate;
            _tvStandards.ItemTemplate = groupTemplate;

            WpfGrid.SetRow(_tvStandards, 2);
            grid.Children.Add(_tvStandards);

            return grid;
        }

        private UIElement CreateModelTabContent(SolidColorBrush borderCard, SolidColorBrush bgCard)
        {
            WpfGrid grid = new WpfGrid { Margin = new Thickness(8) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header info & reload
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Buttons
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Table

            // Шапка вкладки модели
            WpfGrid topGrid = new WpfGrid { Margin = new Thickness(0, 0, 0, 6) };
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _lblModelCount = new TextBlock
            {
                Text = "Выбрано элементов модели: 0",
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            WpfGrid.SetColumn(_lblModelCount, 0);
            topGrid.Children.Add(_lblModelCount);

            Button btnRefreshSelection = CreateSmallButton("Взять выделенные в Revit", (s, e) => LoadModelSelection());
            WpfGrid.SetColumn(btnRefreshSelection, 1);
            topGrid.Children.Add(btnRefreshSelection);

            WpfGrid.SetRow(topGrid, 0);
            grid.Children.Add(topGrid);

            // Кнопки Выбрать все / Снять
            StackPanel btnBar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            btnBar.Children.Add(CreateSmallButton("Выбрать все", (s, e) =>
            {
                foreach (var item in _modelElements) item.IsSelected = true;
            }));
            btnBar.Children.Add(CreateSmallButton("Снять выбор", (s, e) =>
            {
                foreach (var item in _modelElements) item.IsSelected = false;
            }));
            WpfGrid.SetRow(btnBar, 1);
            grid.Children.Add(btnBar);

            // Таблица элементов модели
            _dgModelElements = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HorizontalGridLinesBrush = borderCard,
                Background = new SolidColorBrush(MediaColor.FromRgb(24, 24, 28)),
                Foreground = Brushes.White,
                BorderBrush = borderCard,
                RowBackground = new SolidColorBrush(MediaColor.FromRgb(28, 28, 34)),
                AlternatingRowBackground = new SolidColorBrush(MediaColor.FromRgb(34, 34, 40)),
                ItemsSource = _modelElements
            };

            DataGridCheckBoxColumn colCheck = new DataGridCheckBoxColumn
            {
                Binding = new WpfBinding(nameof(ModelElementItem.IsSelected)) { UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged },
                Width = new DataGridLength(35)
            };
            _dgModelElements.Columns.Add(colCheck);

            DataGridTextColumn colId = new DataGridTextColumn
            {
                Header = "Id",
                Binding = new WpfBinding(nameof(ModelElementItem.ElementIdText)),
                IsReadOnly = true,
                Width = new DataGridLength(90)
            };
            _dgModelElements.Columns.Add(colId);

            DataGridTextColumn colCat = new DataGridTextColumn
            {
                Header = "Категория",
                Binding = new WpfBinding(nameof(ModelElementItem.CategoryName)),
                IsReadOnly = true,
                Width = new DataGridLength(140)
            };
            _dgModelElements.Columns.Add(colCat);

            DataGridTextColumn colName = new DataGridTextColumn
            {
                Header = "Имя / Тип",
                Binding = new WpfBinding(nameof(ModelElementItem.Name)),
                IsReadOnly = true,
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            };
            _dgModelElements.Columns.Add(colName);

            WpfGrid.SetRow(_dgModelElements, 2);
            grid.Children.Add(_dgModelElements);

            return grid;
        }

        private UIElement CreateRightPanel(SolidColorBrush bgCard, SolidColorBrush borderCard)
        {
            ScrollViewer scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            WpfGrid.SetColumn(scroll, 2);

            StackPanel panel = new StackPanel();

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

            StackPanel targetBtnStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
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
            panel.Children.Add(targetCard);

            // Блок Опции
            Border optionsCard = new Border
            {
                Background = bgCard,
                BorderBrush = borderCard,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10)
            };
            StackPanel optionsStack = new StackPanel();
            optionsStack.Children.Add(new TextBlock
            {
                Text = "ОПЦИИ КОПИРОВАНИЯ",
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 6)
            });

            _rbUseDestination = new RadioButton
            {
                Content = "Использовать существующие типоразмеры",
                Foreground = Brushes.White,
                IsChecked = true,
                Margin = new Thickness(0, 3, 0, 3)
            };
            _rbCreateNewTypes = new RadioButton
            {
                Content = "Создавать новые типоразмеры (дубликаты)",
                Foreground = Brushes.White,
                Margin = new Thickness(0, 3, 0, 6)
            };
            optionsStack.Children.Add(_rbUseDestination);
            optionsStack.Children.Add(_rbCreateNewTypes);

            _chkSuppressWarnings = new CheckBox
            {
                Content = "Подавлять предупреждения Revit",
                IsChecked = true,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 6, 0, 3)
            };
            optionsStack.Children.Add(_chkSuppressWarnings);

            optionsCard.Child = optionsStack;
            panel.Children.Add(optionsCard);

            scroll.Content = panel;
            return scroll;
        }

        private void PopulateSourceDocs()
        {
            var options = new List<DocSourceOption>();

            // Открытые документы
            foreach (var doc in _openDocs)
            {
                options.Add(new DocSourceOption
                {
                    Title = doc.Title,
                    Doc = doc,
                    LinkInstance = null
                });
            }

            // Связанные файлы текущего активного документа
            if (_chkShowLinks.IsChecked == true && _uiApp.ActiveUIDocument?.Document != null)
            {
                var activeDoc = _uiApp.ActiveUIDocument.Document;
                var links = new FilteredElementCollector(activeDoc)
                    .OfClass(typeof(RevitLinkInstance))
                    .Cast<RevitLinkInstance>()
                    .ToList();

                foreach (var link in links)
                {
                    Document linkDoc = link.GetLinkDocument();
                    if (linkDoc != null)
                    {
                        options.Add(new DocSourceOption
                        {
                            Title = $"[Связь] {linkDoc.Title} ({link.Name})",
                            Doc = linkDoc,
                            LinkInstance = link
                        });
                    }
                }
            }

            _cbSourceDoc.ItemsSource = options;
            if (options.Count > 0)
            {
                var activeDoc = _uiApp.ActiveUIDocument?.Document;
                var match = options.FirstOrDefault(o => !o.IsLink && o.Doc.Equals(activeDoc));
                _cbSourceDoc.SelectedItem = match ?? options[0];
            }
        }

        private void OnSourceDocChanged()
        {
            var srcDoc = SelectedSourceDoc;
            _targetDocs.Clear();
            _standardGroups.Clear();

            if (srcDoc == null) return;

            // Обновляем целевые документы
            foreach (var doc in _openDocs)
            {
                if (!doc.Equals(srcDoc))
                {
                    _targetDocs.Add(new TargetDocumentItem { Document = doc, IsSelected = true });
                }
            }

            // Загружаем стандарты проекта из исходного документа
            var standards = CopyElementsEngine.GetProjectStandards(srcDoc);

            var groupsDict = new Dictionary<string, StandardCategoryGroup>();

            foreach (var el in standards)
            {
                string groupName = "Прочее";
                if (el is View v && v.IsTemplate) groupName = "Шаблоны видов";
                else if (el is ParameterFilterElement) groupName = "Фильтры видов";
                else if (el is ViewSchedule) groupName = "Спецификации";
                else if (el is Material) groupName = "Материалы";
                else if (el is TextNoteType) groupName = "Текстовые стили";
                else if (el is DimensionType) groupName = "Размерные стили";
                else if (el is ElementType)
                {
                    string cat = el.Category?.Name ?? "Типоразмеры";
                    groupName = $"Типоразмеры: {cat}";
                }

                if (!groupsDict.TryGetValue(groupName, out var grp))
                {
                    grp = new StandardCategoryGroup { CategoryName = groupName };
                    groupsDict[groupName] = grp;
                }

                grp.Items.Add(new StandardElementItem
                {
                    Element = el,
                    Name = el.Name,
                    CategoryName = groupName,
                    IsSelected = false
                });
            }

            foreach (var kv in groupsDict.OrderBy(k => k.Key))
            {
                _standardGroups.Add(kv.Value);
            }
        }

        private void LoadModelSelection()
        {
            _modelElements.Clear();
            var uiDoc = _uiApp.ActiveUIDocument;
            if (uiDoc == null || uiDoc.Document == null) return;

            var selIds = uiDoc.Selection.GetElementIds();
            if (selIds != null)
            {
                foreach (ElementId id in selIds)
                {
                    Element e = uiDoc.Document.GetElement(id);
                    if (e != null && e.Category != null && e.Category.CategoryType == CategoryType.Model)
                    {
                        _modelElements.Add(new ModelElementItem { Element = e, IsSelected = true });
                    }
                }
            }

            _lblModelCount.Text = $"Выбрано элементов модели: {_modelElements.Count}";
        }

        private void ApplyStandardsFilter()
        {
            string query = _tbFilterStandards?.Text?.Trim() ?? "";
            bool isRegex = _chkRegex?.IsChecked == true;

            Regex regex = null;
            if (isRegex && !string.IsNullOrEmpty(query))
            {
                try { regex = new Regex(query, RegexOptions.IgnoreCase); } catch { }
            }

            foreach (var grp in _standardGroups)
            {
                foreach (var item in grp.Items)
                {
                    if (string.IsNullOrEmpty(query))
                    {
                        // Сброс видимости (не отменяя выбор)
                        continue;
                    }

                    bool match = false;
                    if (regex != null)
                    {
                        match = regex.IsMatch(item.Name);
                    }
                    else
                    {
                        match = item.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
                    }

                    // Если подошло, можно выделить или оставить
                }
            }
        }

        private void SetAllStandards(bool isSelected)
        {
            string query = _tbFilterStandards?.Text?.Trim() ?? "";
            foreach (var grp in _standardGroups)
            {
                foreach (var item in grp.Items)
                {
                    if (string.IsNullOrEmpty(query) || item.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        item.IsSelected = isSelected;
                    }
                }
            }
        }

        private void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            var selectedIds = SelectedElementIds;
            if (selectedIds.Count == 0)
            {
                MessageBox.Show("Выберите хотя бы один элемент для копирования.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var targetDocs = SelectedTargetDocs;
            if (targetDocs.Count == 0)
            {
                MessageBox.Show("Выберите хотя бы один целевой проект.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Options = new CopyElementsOptions
            {
                DuplicateAction = _rbUseDestination.IsChecked == true ? ElementDuplicateAction.UseDestinationTypes : ElementDuplicateAction.CreateNewTypes,
                SuppressWarnings = _chkSuppressWarnings.IsChecked == true,
                Transform = SelectedTransform
            };

            DialogResult = true;
        }

        private Button CreateSmallButton(string text, RoutedEventHandler handler)
        {
            var btn = new Button
            {
                Content = text,
                Background = new SolidColorBrush(MediaColor.FromRgb(45, 45, 52)),
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

        private DataTemplate CreateTargetDocTemplate()
        {
            var factory = new FrameworkElementFactory(typeof(CheckBox));
            factory.SetBinding(CheckBox.IsCheckedProperty, new WpfBinding(nameof(TargetDocumentItem.IsSelected)));
            factory.SetBinding(CheckBox.ContentProperty, new WpfBinding(nameof(TargetDocumentItem.Title)));
            factory.SetValue(CheckBox.ForegroundProperty, Brushes.White);
            factory.SetValue(CheckBox.MarginProperty, new Thickness(0, 3, 0, 3));

            return new DataTemplate { VisualTree = factory };
        }
    }
}
