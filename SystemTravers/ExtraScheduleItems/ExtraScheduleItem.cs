using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BimboClub.ExtraScheduleItems
{
    public class ExtraScheduleItem : INotifyPropertyChanged
    {
        private int _id;
        private string _group = "";
        private string _position = "";
        private string _name = "";
        private string _mark = "";
        private string _code = "";
        private string _manufacturer = "";
        private string _unit = "шт";
        private double _count = 1.0;
        private double _weight = 0.0;
        private string _note = "";
        private bool _isModified;
        private bool _isNew;
        private bool _isSelected;

        /// <summary>
        /// ID элемента Revit (если -1, значит еще не создан в модели)
        /// </summary>
        public int Id
        {
            get => _id;
            set { if (_id != value) { _id = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// ADSK_Группирование (Раздел проекта: ОВ1, ОВ2, ВК, ЭОМ, АР и т.д.)
        /// </summary>
        public string Group
        {
            get => _group;
            set { if (_group != value) { _group = value ?? ""; MarkModified(); OnPropertyChanged(); } }
        }

        /// <summary>
        /// ADSK_Позиция
        /// </summary>
        public string Position
        {
            get => _position;
            set { if (_position != value) { _position = value ?? ""; MarkModified(); OnPropertyChanged(); } }
        }

        /// <summary>
        /// ADSK_Наименование
        /// </summary>
        public string Name
        {
            get => _name;
            set { if (_name != value) { _name = value ?? ""; MarkModified(); OnPropertyChanged(); } }
        }

        /// <summary>
        /// ADSK_Марка (или Обозначение)
        /// </summary>
        public string Mark
        {
            get => _mark;
            set { if (_mark != value) { _mark = value ?? ""; MarkModified(); OnPropertyChanged(); } }
        }

        /// <summary>
        /// ADSK_Код изделия
        /// </summary>
        public string Code
        {
            get => _code;
            set { if (_code != value) { _code = value ?? ""; MarkModified(); OnPropertyChanged(); } }
        }

        /// <summary>
        /// ADSK_Завод-изготовитель
        /// </summary>
        public string Manufacturer
        {
            get => _manufacturer;
            set { if (_manufacturer != value) { _manufacturer = value ?? ""; MarkModified(); OnPropertyChanged(); } }
        }

        /// <summary>
        /// ADSK_Единица измерения (шт, м, кг, компл, туб, рул)
        /// </summary>
        public string Unit
        {
            get => _unit;
            set { if (_unit != value) { _unit = value ?? "шт"; MarkModified(); OnPropertyChanged(); } }
        }

        /// <summary>
        /// ADSK_Количество
        /// </summary>
        public double Count
        {
            get => _count;
            set { if (Math.Abs(_count - value) > 0.0001) { _count = value; MarkModified(); OnPropertyChanged(); } }
        }

        /// <summary>
        /// ADSK_Масса
        /// </summary>
        public double Weight
        {
            get => _weight;
            set { if (Math.Abs(_weight - value) > 0.0001) { _weight = value; MarkModified(); OnPropertyChanged(); } }
        }

        /// <summary>
        /// ADSK_Примечание
        /// </summary>
        public string Note
        {
            get => _note;
            set { if (_note != value) { _note = value ?? ""; MarkModified(); OnPropertyChanged(); } }
        }

        public bool IsModified
        {
            get => _isModified;
            set { if (_isModified != value) { _isModified = value; OnPropertyChanged(); } }
        }

        public bool IsNew
        {
            get => _isNew;
            set { if (_isNew != value) { _isNew = value; OnPropertyChanged(); } }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
        }

        private void MarkModified()
        {
            if (!_isNew)
            {
                IsModified = true;
            }
        }

        public ExtraScheduleItem Clone()
        {
            return new ExtraScheduleItem
            {
                Id = -1,
                IsNew = true,
                Group = this.Group,
                Position = this.Position,
                Name = this.Name,
                Mark = this.Mark,
                Code = this.Code,
                Manufacturer = this.Manufacturer,
                Unit = this.Unit,
                Count = this.Count,
                Weight = this.Weight,
                Note = this.Note
            };
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ExtraScheduleCatalogItem
    {
        public string Name { get; set; } = "";
        public string Mark { get; set; } = "";
        public string Code { get; set; } = "";
        public string Manufacturer { get; set; } = "";
        public string Unit { get; set; } = "шт";
        public double DefaultCount { get; set; } = 1.0;
        public double Weight { get; set; } = 0.0;
        public string Note { get; set; } = "";
        public string DefaultGroup { get; set; } = "";

        public ExtraScheduleItem ToScheduleItem(string groupOverride = null)
        {
            return new ExtraScheduleItem
            {
                Id = -1,
                IsNew = true,
                Name = this.Name,
                Mark = this.Mark,
                Code = this.Code,
                Manufacturer = this.Manufacturer,
                Unit = this.Unit,
                Count = this.DefaultCount,
                Weight = this.Weight,
                Note = this.Note,
                Group = string.IsNullOrWhiteSpace(groupOverride) ? this.DefaultGroup : groupOverride
            };
        }
    }

    public class ExtraScheduleCatalogCategory
    {
        public string Title { get; set; } = "";
        public string Icon { get; set; } = "📁";
        public ObservableCollection<ExtraScheduleCatalogItem> Items { get; set; } = new ObservableCollection<ExtraScheduleCatalogItem>();
    }
}
