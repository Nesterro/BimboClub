using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using TextBox = System.Windows.Controls.TextBox;
using ComboBox = System.Windows.Controls.ComboBox;
using CheckBox = System.Windows.Controls.CheckBox;
using Button = System.Windows.Controls.Button;
using Grid = System.Windows.Controls.Grid;
using Color = System.Windows.Media.Color;

namespace BimboClub.RuleEditor
{
    public class RuleEditorWindow : Window
    {
        private readonly Document _doc;
        private readonly UIDocument _uidoc;

        public RuleSetModel RuleSet { get; private set; }
        public bool IsRunRequested { get; private set; } = false;
        public bool CurrentViewOnly { get; private set; } = true;

        private TreeView _treeRules;
        private TextBox _txtRuleName;
        private CheckBox _chkRuleEnabled;
        private ComboBox _cbCategories;

        private ListBox _lbConditions;
        private ListBox _lbActions;

        private RadioButton _rdoActiveView;
        private RadioButton _rdoProject;

        private RuleGroupModel _selectedGroup;
        private RuleItemModel _selectedRule;

        private List<string> _availableParams = new List<string>();

        public RuleEditorWindow(Document doc, UIDocument uidoc)
        {
            _doc = doc;
            _uidoc = uidoc;

            Title = "Редактор правил - BimboClub Tools";
            Width = 980;
            Height = 650;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(24, 24, 28)); // #18181C
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 13;

            LoadAvailableParameters();

            // Загрузка или создание набора правил
            RuleSet = RuleEditorXmlService.CreateDefaultRuleSet();

            BuildUI();
            RefreshTree();
        }

        private void LoadAvailableParameters()
        {
            try
            {
                if (_doc != null)
                {
                    HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "ADSK_Наименование", "ADSK_Марка", "ADSK_Количество", "ADSK_Завод-изготовитель",
                        "ADSK_Позиция", "ADSK_Обозначение", "ADSK_Масса", "Имя типа", "Комментарии", "Марка"
                    };

                    FilteredElementCollector collector = new FilteredElementCollector(_doc)
                        .WhereElementIsNotElementType();

                    int count = 0;
                    foreach (Element elem in collector)
                    {
                        if (count++ > 200) break;
                        foreach (Parameter p in elem.Parameters)
                        {
                            if (p.Definition != null && !string.IsNullOrWhiteSpace(p.Definition.Name))
                            {
                                names.Add(p.Definition.Name);
                            }
                        }
                    }
                    _availableParams = names.OrderBy(n => n).ToList();
                }
            }
            catch { }

            if (_availableParams.Count == 0)
            {
                _availableParams = new List<string>
                {
                    "ADSK_Наименование", "ADSK_Марка", "ADSK_Количество", "ADSK_Завод-изготовитель",
                    "ADSK_Позиция", "ADSK_Обозначение", "ADSK_Масса", "Имя типа", "Комментарии", "Марка"
                };
            }
        }

        private void BuildUI()
        {
            Grid mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(55) });    // Header
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });    // Footer
            Content = mainGrid;

            // --- 1. HEADER ---
            LinearGradientBrush headerGrad = new LinearGradientBrush(Color.FromRgb(255, 46, 91), Color.FromRgb(179, 14, 45), 0.0);
            Border headerBorder = new Border { Background = headerGrad, Padding = new Thickness(20, 0, 20, 0) };
            Grid headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock headerTitle = new TextBlock
            {
                Text = "РЕДАКТОР НАБОРА ПРАВИЛ (BIMBOCLUB PARAMRULES)",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(headerTitle, 0);
            headerGrid.Children.Add(headerTitle);

            StackPanel topButtons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            Button btnOpenXml = CreateTopButton("Открыть XML");
            btnOpenXml.Click += BtnOpenXml_Click;
            Button btnSaveXml = CreateTopButton("Сохранить XML");
            btnSaveXml.Click += BtnSaveXml_Click;

            topButtons.Children.Add(btnOpenXml);
            topButtons.Children.Add(btnSaveXml);
            Grid.SetColumn(topButtons, 1);
            headerGrid.Children.Add(topButtons);

            headerBorder.Child = headerGrid;
            Grid.SetRow(headerBorder, 0);
            mainGrid.Children.Add(headerBorder);

            // --- 2. CONTENT AREA ---
            Grid contentGrid = new Grid { Margin = new Thickness(15, 10, 15, 10) };
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) }); // Left: Tree
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(15) });  // Splitter space
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Right: Details

            // --- LEFT PANEL: TREE ---
            Border leftBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 35)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(45, 45, 52)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10)
            };

            Grid leftGrid = new Grid();
            leftGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            leftGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            leftGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            TextBlock leftTitle = new TextBlock
            {
                Text = "ДЕРЕВО ПРАВИЛ",
                Foreground = new SolidColorBrush(Color.FromRgb(255, 46, 91)),
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(leftTitle, 0);
            leftGrid.Children.Add(leftTitle);

            _treeRules = new TreeView
            {
                Background = new SolidColorBrush(Color.FromRgb(24, 24, 28)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };
            _treeRules.SelectedItemChanged += TreeRules_SelectedItemChanged;
            Grid.SetRow(_treeRules, 1);
            leftGrid.Children.Add(_treeRules);

            // Action buttons under tree
            WrapPanel treeBtnArea = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            Button btnAddGroup = CreateSmallButton("+ Группа");
            btnAddGroup.Click += BtnAddGroup_Click;
            Button btnAddRule = CreateSmallButton("+ Правило");
            btnAddRule.Click += BtnAddRule_Click;
            Button btnDel = CreateSmallButton("Удалить");
            btnDel.Click += BtnDeleteTreeItem_Click;

            treeBtnArea.Children.Add(btnAddGroup);
            treeBtnArea.Children.Add(btnAddRule);
            treeBtnArea.Children.Add(btnDel);
            Grid.SetRow(treeBtnArea, 2);
            leftGrid.Children.Add(treeBtnArea);

            leftBorder.Child = leftGrid;
            Grid.SetColumn(leftBorder, 0);
            contentGrid.Children.Add(leftBorder);

            // --- RIGHT PANEL: RULE DETAILS ---
            Border rightBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 35)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(45, 45, 52)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(15)
            };

            Grid rightGrid = new Grid();
            rightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header Settings
            rightGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Tabs / Lists

            // Rule Header Settings
            StackPanel ruleHeadPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };
            
            Grid nameGrid = new Grid();
            nameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            nameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _txtRuleName = new TextBox
            {
                Height = 32,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush(Color.FromRgb(38, 38, 44)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(50, 50, 58)),
                Padding = new Thickness(8, 5, 8, 5)
            };
            _txtRuleName.TextChanged += (s, e) =>
            {
                if (_selectedRule != null) _selectedRule.Name = _txtRuleName.Text;
                else if (_selectedGroup != null) _selectedGroup.Name = _txtRuleName.Text;
            };
            Grid.SetColumn(_txtRuleName, 0);
            nameGrid.Children.Add(_txtRuleName);

            _chkRuleEnabled = new CheckBox
            {
                Content = "Активно",
                Foreground = Brushes.White,
                Margin = new Thickness(15, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                IsChecked = true
            };
            _chkRuleEnabled.Checked += (s, e) => SetSelectedEnabled(true);
            _chkRuleEnabled.Unchecked += (s, e) => SetSelectedEnabled(false);
            Grid.SetColumn(_chkRuleEnabled, 1);
            nameGrid.Children.Add(_chkRuleEnabled);

            ruleHeadPanel.Children.Add(nameGrid);

            // Category Picker
            StackPanel catPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            catPanel.Children.Add(new TextBlock { Text = "Категория:", Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 190)), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
            _cbCategories = new ComboBox
            {
                Width = 240,
                Height = 28,
                Background = new SolidColorBrush(Color.FromRgb(38, 38, 44)),
                Foreground = Brushes.White
            };
            _cbCategories.ItemsSource = GetCategoryKeys();
            _cbCategories.SelectionChanged += (s, e) =>
            {
                if (_selectedGroup != null && _cbCategories.SelectedItem != null)
                {
                    _selectedGroup.CategoryKey = _cbCategories.SelectedItem.ToString();
                }
            };
            catPanel.Children.Add(_cbCategories);
            ruleHeadPanel.Children.Add(catPanel);

            Grid.SetRow(ruleHeadPanel, 0);
            rightGrid.Children.Add(ruleHeadPanel);

            // TabControl for Conditions and Actions
            TabControl tabControl = new TabControl
            {
                Background = Brushes.Transparent,
                BorderBrush = new SolidColorBrush(Color.FromRgb(50, 50, 58))
            };

            // TAB 1: Conditions
            TabItem tabCond = new TabItem { Header = "Условия фильтрации", Foreground = Brushes.White };
            Grid condGrid = new Grid { Margin = new Thickness(10) };
            condGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            condGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _lbConditions = new ListBox
            {
                Background = new SolidColorBrush(Color.FromRgb(24, 24, 28)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(45, 45, 52))
            };
            Grid.SetRow(_lbConditions, 0);
            condGrid.Children.Add(_lbConditions);

            StackPanel condBtns = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            Button btnAddCond = CreateSmallButton("+ Условие");
            btnAddCond.Click += BtnAddCond_Click;
            Button btnEditCond = CreateSmallButton("Редактировать");
            btnEditCond.Click += BtnEditCond_Click;
            Button btnDelCond = CreateSmallButton("Удалить");
            btnDelCond.Click += BtnDelCond_Click;

            condBtns.Children.Add(btnAddCond);
            condBtns.Children.Add(btnEditCond);
            condBtns.Children.Add(btnDelCond);
            Grid.SetRow(condBtns, 1);
            condGrid.Children.Add(condBtns);

            tabCond.Content = condGrid;
            tabControl.Items.Add(tabCond);

            // TAB 2: Actions
            TabItem tabAct = new TabItem { Header = "Сопоставление параметров", Foreground = Brushes.White };
            Grid actGrid = new Grid { Margin = new Thickness(10) };
            actGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            actGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _lbActions = new ListBox
            {
                Background = new SolidColorBrush(Color.FromRgb(24, 24, 28)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(45, 45, 52))
            };
            Grid.SetRow(_lbActions, 0);
            actGrid.Children.Add(_lbActions);

            StackPanel actBtns = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            Button btnAddAct = CreateSmallButton("+ Действие");
            btnAddAct.Click += BtnAddAct_Click;
            Button btnEditAct = CreateSmallButton("Редактировать");
            btnEditAct.Click += BtnEditAct_Click;
            Button btnDelAct = CreateSmallButton("Удалить");
            btnDelAct.Click += BtnDelAct_Click;

            actBtns.Children.Add(btnAddAct);
            actBtns.Children.Add(btnEditAct);
            actBtns.Children.Add(btnDelAct);
            Grid.SetRow(actBtns, 1);
            actGrid.Children.Add(actBtns);

            tabAct.Content = actGrid;
            tabControl.Items.Add(tabAct);

            Grid.SetRow(tabControl, 1);
            rightGrid.Children.Add(tabControl);

            rightBorder.Child = rightGrid;
            Grid.SetColumn(rightBorder, 2);
            contentGrid.Children.Add(rightBorder);

            Grid.SetRow(contentGrid, 1);
            mainGrid.Children.Add(contentGrid);

            // --- 3. FOOTER ---
            Grid footerGrid = new Grid { Margin = new Thickness(15, 0, 15, 0) };
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel scopePanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _rdoActiveView = new RadioButton { Content = "На активном виде", Foreground = Brushes.White, IsChecked = true, Margin = new Thickness(0, 0, 15, 0) };
            _rdoProject = new RadioButton { Content = "Во всём проекте", Foreground = Brushes.White };
            scopePanel.Children.Add(_rdoActiveView);
            scopePanel.Children.Add(_rdoProject);
            Grid.SetColumn(scopePanel, 0);
            footerGrid.Children.Add(scopePanel);

            StackPanel actionButtons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            Button btnRun = new Button
            {
                Content = "ЗАПУСТИТЬ ВЫПОЛНЕНИЕ ПРАВИЛ",
                Width = 260,
                Height = 36,
                Background = new LinearGradientBrush(Color.FromRgb(255, 46, 91), Color.FromRgb(179, 14, 45), 90.0),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold
            };
            btnRun.Click += (s, e) =>
            {
                IsRunRequested = true;
                CurrentViewOnly = _rdoActiveView.IsChecked == true;
                DialogResult = true;
                Close();
            };

            Button btnClose = new Button
            {
                Content = "Закрыть",
                Width = 90,
                Height = 36,
                Margin = new Thickness(10, 0, 0, 0),
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 52)),
                Foreground = Brushes.White
            };
            btnClose.Click += (s, e) => { Close(); };

            actionButtons.Children.Add(btnRun);
            actionButtons.Children.Add(btnClose);
            Grid.SetColumn(actionButtons, 1);
            footerGrid.Children.Add(actionButtons);

            Grid.SetRow(footerGrid, 2);
            mainGrid.Children.Add(footerGrid);
        }

        private void RefreshTree()
        {
            _treeRules.Items.Clear();
            if (RuleSet == null) return;

            foreach (var group in RuleSet.Groups)
            {
                TreeViewItem groupItem = new TreeViewItem
                {
                    Header = $"📁 {group.Name} ({group.CategoryKey})",
                    Tag = group,
                    Foreground = Brushes.White,
                    IsExpanded = true
                };

                foreach (var rule in group.Rules)
                {
                    TreeViewItem ruleItem = new TreeViewItem
                    {
                        Header = $"⚙ {rule.Name}",
                        Tag = rule,
                        Foreground = rule.IsEnabled ? Brushes.LightGreen : Brushes.Gray
                    };
                    groupItem.Items.Add(ruleItem);
                }

                _treeRules.Items.Add(groupItem);
            }
        }

        private void TreeRules_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            _selectedGroup = null;
            _selectedRule = null;

            if (_treeRules.SelectedItem is TreeViewItem tvi)
            {
                if (tvi.Tag is RuleGroupModel group)
                {
                    _selectedGroup = group;
                    _txtRuleName.Text = group.Name;
                    _chkRuleEnabled.IsChecked = group.IsEnabled;
                    _cbCategories.SelectedItem = group.CategoryKey;

                    _lbConditions.ItemsSource = null;
                    _lbActions.ItemsSource = null;
                }
                else if (tvi.Tag is RuleItemModel rule)
                {
                    _selectedRule = rule;
                    _txtRuleName.Text = rule.Name;
                    _chkRuleEnabled.IsChecked = rule.IsEnabled;

                    RefreshLists();
                }
            }
        }

        private void RefreshLists()
        {
            if (_selectedRule != null)
            {
                _lbConditions.ItemsSource = null;
                _lbConditions.ItemsSource = _selectedRule.Conditions;

                _lbActions.ItemsSource = null;
                _lbActions.ItemsSource = _selectedRule.Actions;
            }
        }

        private void SetSelectedEnabled(bool isEnabled)
        {
            if (_selectedRule != null) _selectedRule.IsEnabled = isEnabled;
            else if (_selectedGroup != null) _selectedGroup.IsEnabled = isEnabled;
        }

        private void BtnAddGroup_Click(object sender, RoutedEventArgs e)
        {
            RuleGroupModel group = new RuleGroupModel
            {
                Name = "Новая группа",
                CategoryKey = "OST_PipeCurves"
            };
            RuleSet.Groups.Add(group);
            RefreshTree();
        }

        private void BtnAddRule_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedGroup == null && _treeRules.SelectedItem is TreeViewItem tvi && tvi.Tag is RuleGroupModel g)
            {
                _selectedGroup = g;
            }

            if (_selectedGroup != null)
            {
                RuleItemModel newRule = new RuleItemModel { Name = "Новое правило" };
                _selectedGroup.Rules.Add(newRule);
                RefreshTree();
            }
            else
            {
                MessageBox.Show("Пожалуйста, выберите группу перед добавлением правила.");
            }
        }

        private void BtnDeleteTreeItem_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRule != null)
            {
                foreach (var g in RuleSet.Groups)
                {
                    g.Rules.Remove(_selectedRule);
                }
            }
            else if (_selectedGroup != null)
            {
                RuleSet.Groups.Remove(_selectedGroup);
            }
            RefreshTree();
        }

        private void BtnAddCond_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRule != null)
            {
                ConditionEditWindow dlg = new ConditionEditWindow(null, _availableParams);
                if (dlg.ShowDialog() == true)
                {
                    _selectedRule.Conditions.Add(dlg.Condition);
                    RefreshLists();
                }
            }
        }

        private void BtnEditCond_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRule != null && _lbConditions.SelectedItem is RuleConditionModel cond)
            {
                ConditionEditWindow dlg = new ConditionEditWindow(cond, _availableParams);
                if (dlg.ShowDialog() == true)
                {
                    RefreshLists();
                }
            }
        }

        private void BtnDelCond_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRule != null && _lbConditions.SelectedItem is RuleConditionModel cond)
            {
                _selectedRule.Conditions.Remove(cond);
                RefreshLists();
            }
        }

        private void BtnAddAct_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRule != null)
            {
                ActionEditWindow dlg = new ActionEditWindow(null, _availableParams);
                if (dlg.ShowDialog() == true)
                {
                    _selectedRule.Actions.Add(dlg.ActionModel);
                    RefreshLists();
                }
            }
        }

        private void BtnEditAct_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRule != null && _lbActions.SelectedItem is RuleActionModel act)
            {
                ActionEditWindow dlg = new ActionEditWindow(act, _availableParams);
                if (dlg.ShowDialog() == true)
                {
                    RefreshLists();
                }
            }
        }

        private void BtnDelAct_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRule != null && _lbActions.SelectedItem is RuleActionModel act)
            {
                _selectedRule.Actions.Remove(act);
                RefreshLists();
            }
        }

        private void BtnOpenXml_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog
            {
                Filter = "Файлы правил XML (*.xml)|*.xml|Все файлы (*.*)|*.*",
                Title = "Открыть набор правил"
            };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    RuleSet = RuleEditorXmlService.LoadFromFile(dlg.FileName);
                    RefreshTree();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при считывании файла XML: {ex.Message}");
                }
            }
        }

        private void BtnSaveXml_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dlg = new SaveFileDialog
            {
                Filter = "Файлы правил XML (*.xml)|*.xml",
                Title = "Сохранить набор правил",
                FileName = "RulesBimboClub.xml"
            };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    RuleEditorXmlService.SaveToFile(RuleSet, dlg.FileName);
                    MessageBox.Show("Файл правил успешно сохранён!");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка сохранения XML: {ex.Message}");
                }
            }
        }

        private Button CreateTopButton(string text)
        {
            return new Button
            {
                Content = text,
                Height = 28,
                Margin = new Thickness(5, 0, 0, 0),
                Padding = new Thickness(10, 0, 10, 0),
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 52)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };
        }

        private Button CreateSmallButton(string text)
        {
            return new Button
            {
                Content = text,
                Height = 26,
                Margin = new Thickness(0, 0, 6, 6),
                Padding = new Thickness(8, 0, 8, 0),
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 52)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };
        }

        private List<string> GetCategoryKeys()
        {
            return new List<string>
            {
                "OST_PipeCurves",
                "OST_PipeFitting",
                "OST_PipeAccessory",
                "OST_DuctCurves",
                "OST_DuctFitting",
                "OST_DuctAccessory",
                "OST_DuctTerminal",
                "OST_MechanicalEquipment",
                "OST_PlumbingFixtures",
                "OST_CableTray",
                "OST_Conduit",
                "OST_LightingFixtures",
                "OST_ElectricalEquipment",
                "OST_Walls",
                "OST_Floors"
            };
        }
    }
}
