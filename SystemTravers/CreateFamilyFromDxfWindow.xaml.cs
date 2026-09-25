using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using Microsoft.Win32;

namespace BimboClub
{
    public partial class CreateFamilyFromDxfWindow : Window
    {
        public class CategoryOption
        {
            public string DisplayName { get; set; }
            public BuiltInCategory Category { get; set; }

            public CategoryOption(string displayName, BuiltInCategory category)
            {
                DisplayName = displayName;
                Category = category;
            }
        }

        public string SelectedDxfPath => DxfPathTextBox.Text;
        public string SelectedFamilyName => FamilyNameTextBox.Text.Trim();
        public BuiltInCategory SelectedCategory => (CategoryComboBox.SelectedItem as CategoryOption)?.Category ?? BuiltInCategory.OST_GenericModel;
        public bool AutoPlace => AutoPlaceCheckBox.IsChecked == true;

        public CreateFamilyFromDxfWindow(string initialDxfPath = null)
        {
            InitializeComponent();

            try
            {
                UiThemeHelper.ApplyDarkTheme(this);
            }
            catch (Exception ex)
            {
                Logger.Log($"ApplyDarkTheme in CreateFamilyFromDxfWindow warning: {ex.Message}", "WARN");
            }

            InitCategories();

            if (!string.IsNullOrEmpty(initialDxfPath) && File.Exists(initialDxfPath))
            {
                SetDxfPath(initialDxfPath);
            }
        }

        private void InitCategories()
        {
            List<CategoryOption> categories = new List<CategoryOption>
            {
                new CategoryOption("Обобщенные модели", BuiltInCategory.OST_GenericModel),
                new CategoryOption("Механическое оборудование", BuiltInCategory.OST_MechanicalEquipment),
                new CategoryOption("Арматура трубопроводов", BuiltInCategory.OST_PipeAccessory),
                new CategoryOption("Сантехнические приборы", BuiltInCategory.OST_PlumbingFixtures),
                new CategoryOption("Арматура воздуховодов", BuiltInCategory.OST_DuctAccessory),
                new CategoryOption("Воздухораспределители", BuiltInCategory.OST_DuctTerminal),
                new CategoryOption("Электрооборудование", BuiltInCategory.OST_ElectricalEquipment),
                new CategoryOption("Осветительные приборы", BuiltInCategory.OST_LightingFixtures),
                new CategoryOption("Электроприборы", BuiltInCategory.OST_ElectricalFixtures),
                new CategoryOption("Соединительные детали трубопроводов", BuiltInCategory.OST_PipeFitting),
                new CategoryOption("Соединительные детали воздуховодов", BuiltInCategory.OST_DuctFitting),
                new CategoryOption("Специальное оборудование", BuiltInCategory.OST_SpecialityEquipment),
                new CategoryOption("Мебель", BuiltInCategory.OST_Furniture)
            };

            CategoryComboBox.ItemsSource = categories;
            CategoryComboBox.SelectedIndex = 0; // Обобщенные модели по умолчанию
        }

        private void SetDxfPath(string path)
        {
            DxfPathTextBox.Text = path;
            if (string.IsNullOrEmpty(FamilyNameTextBox.Text))
            {
                FamilyNameTextBox.Text = Path.GetFileNameWithoutExtension(path);
            }
        }

        private void BrowseDxf_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog
            {
                Title = "Выберите файл DXF для создания семейства",
                Filter = "AutoCAD DXF (*.dxf)|*.dxf|Все файлы (*.*)|*.*",
                Multiselect = false,
                CheckFileExists = true
            };

            if (ofd.ShowDialog() == true)
            {
                SetDxfPath(ofd.FileName);
            }
        }

        private void DxfPathTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(DxfPathTextBox.Text) && string.IsNullOrEmpty(FamilyNameTextBox.Text))
            {
                FamilyNameTextBox.Text = Path.GetFileNameWithoutExtension(DxfPathTextBox.Text);
            }
        }

        private void Create_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SelectedDxfPath) || !File.Exists(SelectedDxfPath))
            {
                MessageBox.Show("Пожалуйста, выберите существующий файл DXF.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedFamilyName))
            {
                MessageBox.Show("Пожалуйста, укажите имя семейства.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
