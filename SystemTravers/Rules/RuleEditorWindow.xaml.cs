using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.Win32;

namespace BimboClub.Rules
{
    public partial class RuleEditorWindow : Window
    {
        private readonly Document _doc;
        private readonly UIDocument? _uiDoc;
        private RuleSet _currentRuleSet = new();
        private RuleItem? _selectedRule;

        public RuleEditorWindow(Document doc, UIDocument? uiDoc = null)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _uiDoc = uiDoc;
            InitializeComponent();

            LoadInitialRules();
        }

        private void LoadInitialRules()
        {
            // Try default path in AppData
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string defPath = Path.Combine(appData, "BimboClub", "Rules", "BimboClub_def.xml");

            if (File.Exists(defPath))
            {
                try
                {
                    _currentRuleSet = RuleXmlService.LoadFromFile(defPath);
                    BindRulesList();
                    TxtStatus.Text = $"Загружено правил: {_currentRuleSet.Rules.Count} (BimboClub_def.xml)";
                    return;
                }
                catch { }
            }

            _currentRuleSet = CreateDefaultRuleSet();
            BindRulesList();
            TxtStatus.Text = $"Загружено базовых правил: {_currentRuleSet.Rules.Count}";
        }

        private RuleSet CreateDefaultRuleSet()
        {
            var set = new RuleSet { Comment = "Базовые правила ОВиВК" };

            // Example Rule 1: Толщина стенки воздуховода
            var r1 = new RuleItem
            {
                Index = 1,
                Name = "Воздуховоды — Толщина стенки",
                Comment = "Заполнение толщины стенки воздуховода по размерам",
                FormulaDescription = "Запись в параметр 'ADSK_Толщина стенки': [Толщина стенок воздуховода]",
                TargetParam = new ParamIdentifier { Name = "ADSK_Толщина стенки" }
            };
            r1.Filters.Add(new RuleFilter
            {
                FilterType = "ByCategories",
                Categories = new ObservableCollection<string> { "OST_DuctCurves" }
            });
            r1.Filters.Add(new RuleFilter
            {
                FilterType = "ElementIsElementType",
                Inverted = true
            });
            r1.Fragments.Add(new RuleFragment
            {
                FragmentType = "DuctThickness",
                DuctThicknessData = DuctThicknessTable.CreateDefault()
            });
            set.Rules.Add(r1);

            // Example Rule 2: Имя системы
            var r2 = new RuleItem
            {
                Index = 2,
                Name = "Заполнение ADSK_Система",
                Comment = "Копирование имени системы в параметр ADSK_Система",
                FormulaDescription = "Запись в параметр 'ADSK_Система': [Имя системы]",
                TargetParam = new ParamIdentifier { Name = "ADSK_Система" }
            };
            r2.Filters.Add(new RuleFilter
            {
                FilterType = "ByCategories",
                Categories = new ObservableCollection<string> { "OST_DuctCurves", "OST_PipeCurves", "OST_DuctFitting", "OST_PipeFitting" }
            });
            r2.Fragments.Add(new RuleFragment
            {
                FragmentType = "ParameterValue",
                ParamId = new ParamIdentifier { Name = "Имя системы" },
                ParamMode = ParamGettingMode.Self
            });
            set.Rules.Add(r2);

            return set;
        }

        private void BindRulesList(string? filterText = null)
        {
            IEnumerable<RuleItem> items = _currentRuleSet.Rules;

            if (!string.IsNullOrWhiteSpace(filterText))
            {
                string search = filterText.Trim();
                items = items.Where(r => 
                    (r.Name != null && r.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (r.TargetParam?.Name != null && r.TargetParam.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (r.Comment != null && r.Comment.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            var list = items.ToList();
            LstRules.ItemsSource = list;
            TxtRulesCount.Text = $"{list.Count} из {_currentRuleSet.Rules.Count} правил";

            if (list.Count > 0)
            {
                LstRules.SelectedIndex = 0;
            }
            else
            {
                SelectRule(null);
            }
        }

        private void TxtSearchRule_TextChanged(object sender, TextChangedEventArgs e)
        {
            BindRulesList(TxtSearchRule.Text);
        }

        private void LstRules_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstRules.SelectedItem is RuleItem item)
            {
                SelectRule(item);
            }
            else
            {
                SelectRule(null);
            }
        }

        private void SelectRule(RuleItem? rule)
        {
            _selectedRule = rule;
            if (rule == null)
            {
                PnlRuleDetails.IsEnabled = false;
                TxtRuleName.Text = string.Empty;
                TxtTargetParam.Text = string.Empty;
                BrdFormulaDesc.Visibility = System.Windows.Visibility.Collapsed;
                IcFilters.ItemsSource = null;
                IcFragments.ItemsSource = null;
                return;
            }

            PnlRuleDetails.IsEnabled = true;
            TxtRuleName.Text = rule.Name;
            TxtTargetParam.Text = rule.TargetParam?.Name ?? string.Empty;

            if (!string.IsNullOrEmpty(rule.FormulaDescription))
            {
                BrdFormulaDesc.Visibility = System.Windows.Visibility.Visible;
                TxtFormulaDesc.Text = rule.FormulaDescription;
            }
            else
            {
                BrdFormulaDesc.Visibility = System.Windows.Visibility.Collapsed;
            }

            IcFilters.ItemsSource = rule.Filters;
            IcFragments.ItemsSource = rule.Fragments;
        }

        private void TxtRuleName_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_selectedRule != null)
            {
                _selectedRule.Name = TxtRuleName.Text;
                _selectedRule.Comment = TxtRuleName.Text;
            }
        }

        private void TxtTargetParam_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_selectedRule != null)
            {
                _selectedRule.TargetParam.Name = TxtTargetParam.Text;
            }
        }

        private void BtnAddRule_Click(object sender, RoutedEventArgs e)
        {
            var newRule = new RuleItem
            {
                Index = _currentRuleSet.Rules.Count + 1,
                Name = $"Правило {_currentRuleSet.Rules.Count + 1}",
                TargetParam = new ParamIdentifier { Name = "ADSK_Наименование" }
            };
            newRule.Filters.Add(new RuleFilter { FilterType = "ElementIsElementType", Inverted = true });

            _currentRuleSet.Rules.Add(newRule);
            BindRulesList(TxtSearchRule.Text);
            LstRules.SelectedItem = newRule;
        }

        private void BtnDuplicateRule_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRule == null) return;

            var copy = new RuleItem
            {
                Index = _currentRuleSet.Rules.Count + 1,
                Name = _selectedRule.Name + " (копия)",
                Comment = _selectedRule.Comment,
                FormulaDescription = _selectedRule.FormulaDescription,
                TargetParam = new ParamIdentifier
                {
                    Name = _selectedRule.TargetParam.Name,
                    Guid = _selectedRule.TargetParam.Guid,
                    BuiltInParamId = _selectedRule.TargetParam.BuiltInParamId,
                    StorageType = _selectedRule.TargetParam.StorageType
                }
            };

            foreach (var f in _selectedRule.Filters)
            {
                var fCopy = new RuleFilter
                {
                    FilterType = f.FilterType,
                    Inverted = f.Inverted,
                    MatchType = f.MatchType,
                    Value = f.Value,
                    ParamId = new ParamIdentifier { Name = f.ParamId.Name, Guid = f.ParamId.Guid },
                    RawElement = f.RawElement != null ? new System.Xml.Linq.XElement(f.RawElement) : null
                };
                foreach (var cat in f.Categories) fCopy.Categories.Add(cat);
                copy.Filters.Add(fCopy);
            }

            foreach (var frag in _selectedRule.Fragments)
            {
                copy.Fragments.Add(new RuleFragment
                {
                    FragmentType = frag.FragmentType,
                    StaticText = frag.StaticText,
                    Description = frag.Description,
                    ParamMode = frag.ParamMode,
                    ConvertToMillimeters = frag.ConvertToMillimeters,
                    RoundDecimal = frag.RoundDecimal,
                    ParamId = new ParamIdentifier { Name = frag.ParamId.Name, Guid = frag.ParamId.Guid },
                    DuctThicknessData = frag.DuctThicknessData,
                    RawElement = frag.RawElement != null ? new System.Xml.Linq.XElement(frag.RawElement) : null
                });
            }

            _currentRuleSet.Rules.Add(copy);
            BindRulesList(TxtSearchRule.Text);
            LstRules.SelectedItem = copy;
        }

        private void BtnMoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRule == null) return;
            int idx = _currentRuleSet.Rules.IndexOf(_selectedRule);
            if (idx > 0)
            {
                _currentRuleSet.Rules.Move(idx, idx - 1);
                ReindexRules();
                BindRulesList(TxtSearchRule.Text);
                LstRules.SelectedItem = _selectedRule;
            }
        }

        private void BtnMoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRule == null) return;
            int idx = _currentRuleSet.Rules.IndexOf(_selectedRule);
            if (idx >= 0 && idx < _currentRuleSet.Rules.Count - 1)
            {
                _currentRuleSet.Rules.Move(idx, idx + 1);
                ReindexRules();
                BindRulesList(TxtSearchRule.Text);
                LstRules.SelectedItem = _selectedRule;
            }
        }

        private void ReindexRules()
        {
            for (int i = 0; i < _currentRuleSet.Rules.Count; i++)
            {
                _currentRuleSet.Rules[i].Index = i + 1;
            }
        }

        private void BtnAddCategoryFilter_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRule == null) return;
            var filter = new RuleFilter
            {
                FilterType = "ByCategories",
                Categories = new ObservableCollection<string> { "OST_DuctCurves", "OST_PipeCurves" }
            };
            _selectedRule.Filters.Add(filter);
        }

        private void BtnAddParamFilter_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRule == null) return;
            var filter = new RuleFilter
            {
                FilterType = "ByParameterValue",
                ParamId = new ParamIdentifier { Name = "Маркировка типоразмера" },
                MatchType = FilterMatchType.Equals,
                Value = "Огнезащита"
            };
            _selectedRule.Filters.Add(filter);
        }

        private void BtnDeleteFilter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement btn && btn.Tag is RuleFilter filter && _selectedRule != null)
            {
                _selectedRule.Filters.Remove(filter);
            }
        }

        private void BtnAddStaticText_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRule == null) return;
            var frag = new RuleFragment
            {
                FragmentType = "Const",
                StaticText = " - "
            };
            _selectedRule.Fragments.Add(frag);
        }

        private void BtnAddParamFragment_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRule == null) return;
            var frag = new RuleFragment
            {
                FragmentType = "ParameterValue",
                ParamId = new ParamIdentifier { Name = "ADSK_Марка" },
                ParamMode = ParamGettingMode.Self
            };
            _selectedRule.Fragments.Add(frag);
        }

        private void BtnAddDuctThickness_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRule == null) return;
            var frag = new RuleFragment
            {
                FragmentType = "DuctThickness",
                DuctThicknessData = DuctThicknessTable.CreateDefault()
            };
            _selectedRule.Fragments.Add(frag);
        }

        private void BtnAddLevelFragment_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRule == null) return;
            var frag = new RuleFragment
            {
                FragmentType = "FloorLevelName"
            };
            _selectedRule.Fragments.Add(frag);
        }

        private void BtnDeleteFragment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement btn && btn.Tag is RuleFragment frag && _selectedRule != null)
            {
                _selectedRule.Fragments.Remove(frag);
            }
        }

        private void BtnOpenXml_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "XML Файлы правил (*.xml)|*.xml|Все файлы (*.*)|*.*",
                Title = "Выберите файл правил"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    _currentRuleSet = RuleXmlService.LoadFromFile(dlg.FileName);
                    BindRulesList();
                    string fileName = Path.GetFileName(dlg.FileName);
                    Title = $"BimboClub — Редактор правил — {fileName} ({_currentRuleSet.Rules.Count} правил)";
                    TxtSubtitle.Text = $"Загружен файл: {dlg.FileName}";
                    TxtStatus.Text = $"🟢 Загружено правил: {_currentRuleSet.Rules.Count} из {fileName}";
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("BimboClub | Ошибка", $"Не удалось загрузить файл правил:\n{ex.Message}");
                }
            }
        }

        private void BtnSaveXml_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "XML Файлы правил (*.xml)|*.xml",
                Title = "Сохранить правила в XML",
                FileName = !string.IsNullOrEmpty(_currentRuleSet.FilePath) ? Path.GetFileName(_currentRuleSet.FilePath) : "Правила_BimboClub.xml"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    RuleXmlService.SaveToFile(_currentRuleSet, dlg.FileName);
                    _currentRuleSet.FilePath = dlg.FileName;
                    string fileName = Path.GetFileName(dlg.FileName);
                    TxtStatus.Text = $"💾 Правила успешно сохранены в {fileName}";
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("BimboClub | Ошибка", $"Не удалось сохранить правила:\n{ex.Message}");
                }
            }
        }

        private void BtnExecute_Click(object sender, RoutedEventArgs e)
        {
            if (_currentRuleSet.Rules.Count == 0)
            {
                TaskDialog.Show("BimboClub", "Нет правил для выполнения.");
                return;
            }

            if (RdoAllDoc.IsChecked == true)
                _currentRuleSet.Scope = RuleScope.EntireDocument;
            else if (RdoActiveView.IsChecked == true)
                _currentRuleSet.Scope = RuleScope.ActiveView;
            else if (RdoSelection.IsChecked == true)
                _currentRuleSet.Scope = RuleScope.Selection;

            PrgBar.Visibility = System.Windows.Visibility.Visible;
            PrgBar.Value = 0;
            TxtStatus.Text = "Выполнение правил...";

            var progress = new Progress<double>(val => 
            {
                PrgBar.Value = val;
                TxtStatus.Text = $"Обработка правил ({val:F0}%)...";
            });

            try
            {
                var engine = new RuleEngine(_doc, _uiDoc);
                var result = engine.Execute(_currentRuleSet, progress);

                string report = $"Обработка завершена!\n\n" +
                               $"Выполнено активных правил: {result.ExecutedRules} из {result.TotalRules}\n" +
                               $"Элементов под фильтрами: {result.MatchedElementsCount}\n" +
                               $"Обновлено параметров: {result.UpdatedElementsCount}\n";

                if (result.Errors.Count > 0)
                {
                    report += $"\nПредупреждения ({result.Errors.Count}):\n" + string.Join("\n", result.Errors.Take(5));
                }

                TxtStatus.Text = $"✅ Обработка завершена! Обновлено: {result.UpdatedElementsCount} элементов.";
                TaskDialog.Show("BimboClub | Результат", report);
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"❌ Ошибка выполнения: {ex.Message}";
                TaskDialog.Show("BimboClub | Ошибка", $"Ошибка при выполнении правил:\n{ex.Message}");
            }
            finally
            {
                PrgBar.Visibility = System.Windows.Visibility.Hidden;
            }
        }
    }
}
