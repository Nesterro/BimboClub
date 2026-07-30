using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Input;

namespace BimboClub
{
    public class FamilyItem
    {
        public string Name { get; set; }
        public object RawFamily { get; set; }
        public override string ToString() => Name;
    }

    public class RenameFamilyWindow : Window
    {
        private ComboBox _cbFamilies;
        private TextBox _txtNewName;
        private Button _btnRename;
        private TextBlock _lblCurrentFamilyStatic;

        private List<FamilyItem> _families;
        private object _preselectedFamily;

        public object SelectedFamily => _preselectedFamily ?? ((FamilyItem)_cbFamilies.SelectedItem)?.RawFamily;
        public string NewName => _txtNewName.Text.Trim();
        public bool IsRenameRequested { get; private set; } = false;
        public bool IsDeleteRequested { get; private set; } = false;

        public RenameFamilyWindow(List<FamilyItem> families, object preselectedFamily = null)
        {
            _families = families;
            _preselectedFamily = preselectedFamily;

            InitializeComponent();
            PopulateFamilies();
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
            // Настройка окна
            Title = "Переименование семейства - BimboClub Tools";
            Width = 460;
            Height = 280;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(24, 24, 28)); // #18181C
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 13;
            ResizeMode = ResizeMode.NoResize;

            // Цветовая палитра
            SolidColorBrush controlBg = new SolidColorBrush(Color.FromRgb(40, 40, 48)); // #282830
            SolidColorBrush borderBrush = new SolidColorBrush(Color.FromRgb(48, 48, 56)); // #303038
            SolidColorBrush activeBorderBrush = new SolidColorBrush(Color.FromRgb(255, 46, 91)); // #FF2E5B (Scarlet)

            // Стили
            Style textBoxStyle = new Style(typeof(TextBox));
            textBoxStyle.Setters.Add(new Setter(TextBox.BackgroundProperty, controlBg));
            textBoxStyle.Setters.Add(new Setter(TextBox.ForegroundProperty, Brushes.White));
            textBoxStyle.Setters.Add(new Setter(TextBox.BorderBrushProperty, borderBrush));
            textBoxStyle.Setters.Add(new Setter(TextBox.BorderThicknessProperty, new Thickness(1)));
            textBoxStyle.Setters.Add(new Setter(TextBox.PaddingProperty, new Thickness(8, 0, 8, 0)));
            textBoxStyle.Setters.Add(new Setter(TextBox.VerticalContentAlignmentProperty, VerticalAlignment.Center));

            Style comboBoxStyle = new Style(typeof(ComboBox));
            comboBoxStyle.Setters.Add(new Setter(ComboBox.BackgroundProperty, controlBg));
            comboBoxStyle.Setters.Add(new Setter(ComboBox.ForegroundProperty, Brushes.White));
            comboBoxStyle.Setters.Add(new Setter(ComboBox.BorderBrushProperty, borderBrush));
            comboBoxStyle.Setters.Add(new Setter(ComboBox.VerticalContentAlignmentProperty, VerticalAlignment.Center));

            Style comboBoxItemStyle = new Style(typeof(System.Windows.Controls.ComboBoxItem));
            comboBoxItemStyle.Setters.Add(new Setter(System.Windows.Controls.ComboBoxItem.BackgroundProperty, controlBg));
            comboBoxItemStyle.Setters.Add(new Setter(System.Windows.Controls.ComboBoxItem.ForegroundProperty, Brushes.White));
            this.Resources.Add(typeof(System.Windows.Controls.ComboBoxItem), comboBoxItemStyle);

            Style roundedButtonStyle = GetRoundedButtonStyle(5);

            // Главный грид
            Grid mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(55) });    // 0: Шапка
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 1: Контент
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });    // 2: Подвал
            Content = mainGrid;

            // 0. Шапка
            Border headerBorder = new Border
            {
                Background = new LinearGradientBrush(Color.FromRgb(18, 18, 20), Color.FromRgb(179, 14, 45), 0.0),
                Padding = new Thickness(15, 10, 15, 10)
            };
            Grid.SetRow(headerBorder, 0);
            mainGrid.Children.Add(headerBorder);

            TextBlock headerText = new TextBlock
            {
                Text = "ПЕРЕИМЕНОВАНИЕ СЕМЕЙСТВА В ПРОЕКТЕ",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerBorder.Child = headerText;

            // 1. Контент
            Grid contentGrid = new Grid { Margin = new Thickness(15, 15, 15, 10) };
            Grid.SetRow(contentGrid, 1);
            mainGrid.Children.Add(contentGrid);
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) }); // Выбор семейства
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) }); // Разделитель
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) }); // Новое имя

            // Строка 1: Семейство
            Grid rowFamily = new Grid();
            rowFamily.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            rowFamily.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(rowFamily, 0);
            contentGrid.Children.Add(rowFamily);

            TextBlock lblFamily = new TextBlock { Text = "Семейство:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold };
            Grid.SetColumn(lblFamily, 0);
            rowFamily.Children.Add(lblFamily);

            if (_preselectedFamily != null)
            {
                _lblCurrentFamilyStatic = new TextBlock 
                { 
                    Foreground = activeBorderBrush, 
                    VerticalAlignment = VerticalAlignment.Center, 
                    FontWeight = FontWeights.Bold,
                    TextTrimming = TextTrimming.CharacterEllipsis 
                };
                Grid.SetColumn(_lblCurrentFamilyStatic, 1);
                rowFamily.Children.Add(_lblCurrentFamilyStatic);
            }
            else
            {
                _cbFamilies = new ComboBox { Style = comboBoxStyle, Height = 26, VerticalAlignment = VerticalAlignment.Center };
                UiThemeHelper.StyleComboBoxDark(_cbFamilies, controlBg, borderBrush);
                _cbFamilies.SelectionChanged += CbFamilies_SelectionChanged;
                Grid.SetColumn(_cbFamilies, 1);
                rowFamily.Children.Add(_cbFamilies);
            }

            // Строка 2: Новое имя
            Grid rowNewName = new Grid();
            rowNewName.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            rowNewName.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(rowNewName, 2);
            contentGrid.Children.Add(rowNewName);

            TextBlock lblNewName = new TextBlock { Text = "Новое имя:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold };
            Grid.SetColumn(lblNewName, 0);
            rowNewName.Children.Add(lblNewName);

            _txtNewName = new TextBox { Style = textBoxStyle, Height = 26, VerticalAlignment = VerticalAlignment.Center };
            _txtNewName.KeyDown += TxtNewName_KeyDown;
            Grid.SetColumn(_txtNewName, 1);
            rowNewName.Children.Add(_txtNewName);

            // 2. Подвал с кнопками
            Grid bottomGrid = new Grid { Margin = new Thickness(15, 10, 15, 10) };
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) }); // Удалить
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Разделитель
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) }); // Переименовать
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });  // Отмена
            Grid.SetRow(bottomGrid, 2);
            mainGrid.Children.Add(bottomGrid);

            // Кнопка Удалить
            Button btnDelete = new Button
            {
                Content = "Удалить",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0),
                Height = 30,
                Cursor = Cursors.Hand
            };
            btnDelete.Click += BtnDelete_Click;
            LinearGradientBrush delNormalGrad = new LinearGradientBrush(Color.FromRgb(180, 20, 20), Color.FromRgb(120, 10, 10), 90.0);
            LinearGradientBrush delHoverGrad = new LinearGradientBrush(Color.FromRgb(220, 30, 30), Color.FromRgb(150, 15, 15), 90.0);
            ApplyButtonStyles(btnDelete, delNormalGrad, delHoverGrad, roundedButtonStyle);
            Grid.SetColumn(btnDelete, 0);
            bottomGrid.Children.Add(btnDelete);

            // Кнопка Переименовать
            _btnRename = new Button
            {
                Content = "Переименовать",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0),
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand
            };
            _btnRename.Click += BtnRename_Click;

            LinearGradientBrush normalGrad = new LinearGradientBrush(Color.FromRgb(255, 46, 91), Color.FromRgb(179, 14, 45), 90.0);
            LinearGradientBrush hoverGrad = new LinearGradientBrush(Color.FromRgb(255, 80, 118), Color.FromRgb(210, 20, 60), 90.0);
            ApplyButtonStyles(_btnRename, normalGrad, hoverGrad, roundedButtonStyle);
            Grid.SetColumn(_btnRename, 2);
            bottomGrid.Children.Add(_btnRename);

            // Кнопка Отмена
            Button btnCancel = new Button
            {
                Content = "Отмена",
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Height = 30,
                Cursor = Cursors.Hand
            };
            btnCancel.Click += (s, e) => { DialogResult = false; Close(); };
            ApplyFlatButtonStyles(btnCancel, new SolidColorBrush(Color.FromRgb(58, 58, 66)), new SolidColorBrush(Color.FromRgb(78, 78, 88)), roundedButtonStyle);
            Grid.SetColumn(btnCancel, 3);
            bottomGrid.Children.Add(btnCancel);
        }

        private void PopulateFamilies()
        {
            if (_preselectedFamily != null)
            {
                var fam = _families.FirstOrDefault(f => f.RawFamily == _preselectedFamily);
                if (fam != null)
                {
                    _lblCurrentFamilyStatic.Text = fam.Name;
                    _txtNewName.Text = fam.Name;
                    _txtNewName.Focus();
                    _txtNewName.SelectAll();
                }
            }
            else
            {
                _cbFamilies.Items.Clear();
                foreach (var item in _families)
                {
                    _cbFamilies.Items.Add(item);
                }
                if (_cbFamilies.Items.Count > 0)
                {
                    _cbFamilies.SelectedIndex = 0;
                }
            }
        }

        private void CbFamilies_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            FamilyItem selected = _cbFamilies.SelectedItem as FamilyItem;
            if (selected != null)
            {
                _txtNewName.Text = selected.Name;
                _txtNewName.Focus();
                _txtNewName.SelectAll();
            }
        }

        private void TxtNewName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnRename_Click(this, new RoutedEventArgs());
            }
        }

        private void BtnRename_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedFamily == null)
            {
                MessageBox.Show("Пожалуйста, выберите семейство для переименования.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(NewName))
            {
                MessageBox.Show("Пожалуйста, введите корректное новое имя семейства.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsRenameRequested = true;
            DialogResult = true;
            Close();
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedFamily == null)
            {
                MessageBox.Show("Пожалуйста, выберите семейство для удаления.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsDeleteRequested = true;
            DialogResult = true;
            Close();
        }
    }
}
