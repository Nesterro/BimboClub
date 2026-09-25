using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace BimboClub.ExtraScheduleItems
{
    [DataContract]
    public class CatalogItemDto
    {
        [DataMember] public string Name { get; set; } = "";
        [DataMember] public string Mark { get; set; } = "";
        [DataMember] public string Code { get; set; } = "";
        [DataMember] public string Manufacturer { get; set; } = "";
        [DataMember] public string Unit { get; set; } = "шт";
        [DataMember] public double DefaultCount { get; set; } = 1.0;
        [DataMember] public double Weight { get; set; } = 0.0;
        [DataMember] public string Note { get; set; } = "";
        [DataMember] public string DefaultGroup { get; set; } = "";
        [DataMember] public string DefaultCategory { get; set; } = "Обобщенные модели";
    }

    [DataContract]
    public class CatalogCategoryDto
    {
        [DataMember] public string Title { get; set; } = "";
        [DataMember] public string Icon { get; set; } = "📁";
        [DataMember] public List<CatalogItemDto> Items { get; set; } = new List<CatalogItemDto>();
    }

    [DataContract]
    public class CatalogRootDto
    {
        [DataMember] public List<CatalogCategoryDto> Categories { get; set; } = new List<CatalogCategoryDto>();
    }

    public static class ExtraScheduleCatalogManager
    {
        private static readonly string CatalogDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BimboClub",
            "ExtraScheduleItems"
        );

        private static readonly string CatalogFilePath = Path.Combine(CatalogDir, "Catalog.json");

        public static ObservableCollection<ExtraScheduleCatalogCategory> LoadCatalog()
        {
            try
            {
                if (File.Exists(CatalogFilePath))
                {
                    string json = File.ReadAllText(CatalogFilePath, Encoding.UTF8);
                    using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                    {
                        var serializer = new DataContractJsonSerializer(typeof(CatalogRootDto));
                        var root = serializer.ReadObject(ms) as CatalogRootDto;
                        if (root?.Categories != null && root.Categories.Count > 0)
                        {
                            var result = new ObservableCollection<ExtraScheduleCatalogCategory>();
                            foreach (var catDto in root.Categories)
                            {
                                var cat = new ExtraScheduleCatalogCategory
                                {
                                    Title = catDto.Title,
                                    Icon = string.IsNullOrEmpty(catDto.Icon) ? "📁" : catDto.Icon
                                };
                                if (catDto.Items != null)
                                {
                                    foreach (var itemDto in catDto.Items)
                                    {
                                        cat.Items.Add(new ExtraScheduleCatalogItem
                                        {
                                            Name = itemDto.Name,
                                            Mark = itemDto.Mark,
                                            Code = itemDto.Code,
                                            Manufacturer = itemDto.Manufacturer,
                                            Unit = itemDto.Unit,
                                            DefaultCount = itemDto.DefaultCount,
                                            Weight = itemDto.Weight,
                                            Note = itemDto.Note,
                                            DefaultGroup = itemDto.DefaultGroup,
                                            DefaultCategory = string.IsNullOrWhiteSpace(itemDto.DefaultCategory) ? "Обобщенные модели" : itemDto.DefaultCategory
                                        });
                                    }
                                }
                                result.Add(cat);
                            }
                            return result;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Ошибка загрузки каталога немоделируемых элементов", ex);
            }

            // Если файла нет или ошибка - возвращаем базовый типовой каталог
            var defaultCatalog = CreateDefaultCatalog();
            SaveCatalog(defaultCatalog);
            return defaultCatalog;
        }

        public static void SaveCatalog(ObservableCollection<ExtraScheduleCatalogCategory> categories)
        {
            try
            {
                if (!Directory.Exists(CatalogDir))
                {
                    Directory.CreateDirectory(CatalogDir);
                }

                var root = new CatalogRootDto();
                foreach (var cat in categories)
                {
                    var catDto = new CatalogCategoryDto
                    {
                        Title = cat.Title,
                        Icon = cat.Icon
                    };
                    foreach (var it in cat.Items)
                    {
                        catDto.Items.Add(new CatalogItemDto
                        {
                            Name = it.Name,
                            Mark = it.Mark,
                            Code = it.Code,
                            Manufacturer = it.Manufacturer,
                            Unit = it.Unit,
                            DefaultCount = it.DefaultCount,
                            Weight = it.Weight,
                            Note = it.Note,
                            DefaultGroup = it.DefaultGroup,
                            DefaultCategory = string.IsNullOrWhiteSpace(it.DefaultCategory) ? "Обобщенные модели" : it.DefaultCategory
                        });
                    }
                    root.Categories.Add(catDto);
                }

                using (var ms = new MemoryStream())
                {
                    var serializer = new DataContractJsonSerializer(typeof(CatalogRootDto));
                    serializer.WriteObject(ms, root);
                    string json = Encoding.UTF8.GetString(ms.ToArray());
                    File.WriteAllText(CatalogFilePath, json, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Ошибка сохранения каталога немоделируемых элементов", ex);
            }
        }

        public static ObservableCollection<ExtraScheduleCatalogCategory> CreateDefaultCatalog()
        {
            var categories = new ObservableCollection<ExtraScheduleCatalogCategory>();

            // 1. Крепеж и подвесы
            var fasteners = new ExtraScheduleCatalogCategory { Title = "Крепеж и подвесы", Icon = "🔩" };
            fasteners.Items.Add(new ExtraScheduleCatalogItem { Name = "Шпилька резьбовая М8", Mark = "DIN 975", Unit = "м", DefaultCount = 10.0, Weight = 0.32, Note = "Оцинкованная" });
            fasteners.Items.Add(new ExtraScheduleCatalogItem { Name = "Шпилька резьбовая М10", Mark = "DIN 975", Unit = "м", DefaultCount = 10.0, Weight = 0.49, Note = "Оцинкованная" });
            fasteners.Items.Add(new ExtraScheduleCatalogItem { Name = "Шпилька резьбовая М12", Mark = "DIN 975", Unit = "м", DefaultCount = 10.0, Weight = 0.72, Note = "Оцинкованная" });
            fasteners.Items.Add(new ExtraScheduleCatalogItem { Name = "Гайка шестигранная М8", Mark = "DIN 934", Unit = "шт", DefaultCount = 50.0, Weight = 0.005, Note = "Класс прочности 8.0" });
            fasteners.Items.Add(new ExtraScheduleCatalogItem { Name = "Гайка шестигранная М10", Mark = "DIN 934", Unit = "шт", DefaultCount = 50.0, Weight = 0.011, Note = "Класс прочности 8.0" });
            fasteners.Items.Add(new ExtraScheduleCatalogItem { Name = "Шайба увеличенная М8", Mark = "DIN 9021", Unit = "шт", DefaultCount = 50.0, Weight = 0.006 });
            fasteners.Items.Add(new ExtraScheduleCatalogItem { Name = "Шайба увеличенная М10", Mark = "DIN 9021", Unit = "шт", DefaultCount = 50.0, Weight = 0.012 });
            fasteners.Items.Add(new ExtraScheduleCatalogItem { Name = "Анкер забивной стальной М8", Mark = "Цанга М8", Unit = "шт", DefaultCount = 30.0, Weight = 0.015 });
            fasteners.Items.Add(new ExtraScheduleCatalogItem { Name = "Анкер забивной стальной М10", Mark = "Цанга М10", Unit = "шт", DefaultCount = 30.0, Weight = 0.025 });
            fasteners.Items.Add(new ExtraScheduleCatalogItem { Name = "Перфолента монтажная 20х0.7 мм", Mark = "Прямая", Unit = "м", DefaultCount = 25.0, Weight = 0.12, Note = "В рулоне 25м" });
            fasteners.Items.Add(new ExtraScheduleCatalogItem { Name = "Дюбель-гвоздь 6х40 мм", Mark = "SM-L", Unit = "шт", DefaultCount = 100.0, Weight = 0.004 });
            fasteners.Items.Add(new ExtraScheduleCatalogItem { Name = "Струбцина монтажная лучевая М8", Mark = "TKN М8", Unit = "шт", DefaultCount = 20.0, Weight = 0.14 });
            fasteners.Items.Add(new ExtraScheduleCatalogItem { Name = "Струбцина монтажная лучевая М10", Mark = "TKN М10", Unit = "шт", DefaultCount = 20.0, Weight = 0.15 });
            categories.Add(fasteners);

            // 2. Герметизация и уплотнения
            var sealants = new ExtraScheduleCatalogCategory { Title = "Герметизация и уплотнения", Icon = "🧪" };
            sealants.Items.Add(new ExtraScheduleCatalogItem { Name = "Герметик силиконовый нейтральный 310 мл", Mark = "Санитарный", Unit = "туб", DefaultCount = 5.0, Weight = 0.35, Note = "Серый / прозрачный" });
            sealants.Items.Add(new ExtraScheduleCatalogItem { Name = "Герметик полиуретановый шовный 600 мл", Mark = "PU-40", Unit = "туб", DefaultCount = 3.0, Weight = 0.75 });
            sealants.Items.Add(new ExtraScheduleCatalogItem { Name = "Лента межфланцевая уплотнительная 15х4 мм", Mark = "Пенополиэтилен", Unit = "м", DefaultCount = 50.0, Weight = 0.02, Note = "Самоклеящаяся" });
            sealants.Items.Add(new ExtraScheduleCatalogItem { Name = "Пена монтажная огнестойкая пистолетная 750 мл", Mark = "B1 / EI 240", Unit = "баллон", DefaultCount = 4.0, Weight = 0.95 });
            sealants.Items.Add(new ExtraScheduleCatalogItem { Name = "Пена монтажная профессиональная 750 мл", Mark = "PRO 65", Unit = "баллон", DefaultCount = 6.0, Weight = 0.90 });
            sealants.Items.Add(new ExtraScheduleCatalogItem { Name = "Шнур термостойкий базальтовый d=10 мм", Mark = "ШБТ-10", Unit = "м", DefaultCount = 20.0, Weight = 0.08 });
            categories.Add(sealants);

            // 3. Изоляция и ленты
            var insulation = new ExtraScheduleCatalogCategory { Title = "Изоляция и скотч", Icon = "🛡️" };
            insulation.Items.Add(new ExtraScheduleCatalogItem { Name = "Скотч алюминиевый армированный 50 мм х 50 м", Mark = "LAS 50", Unit = "рул", DefaultCount = 3.0, Weight = 0.45 });
            insulation.Items.Add(new ExtraScheduleCatalogItem { Name = "Лента армированная тканевая TPL 50 мм х 50 м", Mark = "TPL", Unit = "рул", DefaultCount = 2.0, Weight = 0.38, Note = "Серая" });
            insulation.Items.Add(new ExtraScheduleCatalogItem { Name = "Клей для технической теплоизоляции 1.0 л", Mark = "K-Flex K 414", Unit = "банка", DefaultCount = 2.0, Weight = 1.05 });
            insulation.Items.Add(new ExtraScheduleCatalogItem { Name = "Очиститель для теплоизоляции 1.0 л", Mark = "Cleaner", Unit = "л", DefaultCount = 1.0, Weight = 0.95 });
            categories.Add(insulation);

            // 4. Электрика и слаботочка
            var electrical = new ExtraScheduleCatalogCategory { Title = "Электрика и автоматика", Icon = "⚡" };
            electrical.Items.Add(new ExtraScheduleCatalogItem { Name = "Стяжка кабельная нейлоновая 200х3.6 мм", Mark = "CV-200", Unit = "упак", DefaultCount = 2.0, Weight = 0.12, Note = "100 шт/упак" });
            electrical.Items.Add(new ExtraScheduleCatalogItem { Name = "Стяжка кабельная нейлоновая 300х4.8 мм", Mark = "CV-300", Unit = "упак", DefaultCount = 2.0, Weight = 0.22, Note = "100 шт/упак" });
            electrical.Items.Add(new ExtraScheduleCatalogItem { Name = "Бирка маркировочная кабельная У-134", Mark = "Квадратная", Unit = "шт", DefaultCount = 50.0, Weight = 0.005 });
            electrical.Items.Add(new ExtraScheduleCatalogItem { Name = "Дюбель-хомут для кабеля d=19-25 мм", Mark = "ДХ", Unit = "шт", DefaultCount = 100.0, Weight = 0.004 });
            electrical.Items.Add(new ExtraScheduleCatalogItem { Name = "Гильза кабельная медная луженая ГМЛ-10", Mark = "ГМЛ 10-6", Unit = "шт", DefaultCount = 20.0, Weight = 0.008 });
            categories.Add(electrical);

            // 5. Окраска и антикор
            var coatings = new ExtraScheduleCatalogCategory { Title = "Окраска и антикор", Icon = "🎨" };
            coatings.Items.Add(new ExtraScheduleCatalogItem { Name = "Грунтовка антикоррозийная быстросохнущая", Mark = "ГФ-021", Unit = "кг", DefaultCount = 15.0, Weight = 1.0, Note = "Красно-коричневая" });
            coatings.Items.Add(new ExtraScheduleCatalogItem { Name = "Эмаль алкидная глянцевая", Mark = "ПФ-115", Unit = "кг", DefaultCount = 20.0, Weight = 1.0, Note = "Серая / белая" });
            coatings.Items.Add(new ExtraScheduleCatalogItem { Name = "Растворитель / Обезжириватель", Mark = "Уайт-спирит", Unit = "л", DefaultCount = 5.0, Weight = 0.85 });
            categories.Add(coatings);

            // 6. Пользовательские (пустая по умолчанию)
            var userCat = new ExtraScheduleCatalogCategory { Title = "Пользовательские", Icon = "⭐" };
            categories.Add(userCat);

            return categories;
        }
    }
}
