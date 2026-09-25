using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;

namespace BimboClub.SpacesFromRooms
{
    public partial class SpacesFromRoomsWindow : Window
    {
        private readonly Document _doc;
        private List<LinkOption> _links;
        private List<LevelOption> _levels;

        public SpaceCreationSettings Settings { get; private set; }

        public SpacesFromRoomsWindow(Document doc)
        {
            _doc = doc;
            InitializeComponent();

            try
            {
                UiThemeHelper.ApplyDarkTheme(this);
            }
            catch (Exception ex)
            {
                Logger.Log($"ApplyDarkTheme in SpacesFromRoomsWindow warning: {ex.Message}", "WARN");
            }

            LoadData();
        }

        private void LoadData()
        {
            // 1. Загрузка связей с помещениями
            _links = SpacesFromRoomsService.GetLinksWithRooms(_doc);
            LinksComboBox.ItemsSource = _links;

            if (_links.Count > 0)
            {
                // Выбираем связь с наибольшим количеством помещений по умолчанию
                var preferred = _links.OrderByDescending(l => l.RoomCount).FirstOrDefault();
                LinksComboBox.SelectedItem = preferred ?? _links[0];
            }

            // 2. Загрузка уровней
            _levels = SpacesFromRoomsService.GetHostLevels(_doc);
            LevelsItemsControl.ItemsSource = _levels;
        }

        private void SelectAllLevels_Click(object sender, RoutedEventArgs e)
        {
            if (_levels == null) return;
            foreach (var lvl in _levels)
            {
                lvl.IsSelected = true;
            }
            LevelsItemsControl.ItemsSource = null;
            LevelsItemsControl.ItemsSource = _levels;
        }

        private void DeselectAllLevels_Click(object sender, RoutedEventArgs e)
        {
            if (_levels == null) return;
            foreach (var lvl in _levels)
            {
                lvl.IsSelected = false;
            }
            LevelsItemsControl.ItemsSource = null;
            LevelsItemsControl.ItemsSource = _levels;
        }

        private void Create_Click(object sender, RoutedEventArgs e)
        {
            LinkOption selectedLinkOpt = LinksComboBox.SelectedItem as LinkOption;
            if (selectedLinkOpt == null || selectedLinkOpt.Instance == null)
            {
                MessageBox.Show("Пожалуйста, выберите связанный файл АР.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            List<Level> selectedLevels = _levels?
                .Where(l => l.IsSelected && l.Level != null)
                .Select(l => l.Level)
                .ToList() ?? new List<Level>();

            if (selectedLevels.Count == 0)
            {
                MessageBox.Show("Пожалуйста, выберите хотя бы один уровень для расстановки пространств.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Settings = new SpaceCreationSettings
            {
                SelectedLink = selectedLinkOpt.Instance,
                SelectedLevels = selectedLevels,
                CopyNameAndNumber = CopyNameAndNumberCheckBox.IsChecked == true,
                CopyHeightsAndLimits = CopyHeightsCheckBox.IsChecked == true,
                EnableRoomBounding = EnableRoomBoundingCheckBox.IsChecked == true,
                UpdateExistingSpaces = UpdateExistingCheckBox.IsChecked == true,
                SkipExistingSpaces = SkipExistingCheckBox.IsChecked == true
            };

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
