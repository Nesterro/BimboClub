using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
namespace BimboClub
{
	public class App : IExternalApplication
	{
		public static string AssemblyDir
		{
			get;
			private set;
		}
		public Result OnStartup(UIControlledApplication application)
		{
			AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(this.OnAssemblyResolve);
			Logger.Log("Запуск инициализации панели BimboClub Tools.", "INFO");
			string tabName = "BimboClub";
			try
			{
				application.CreateRibbonTab(tabName);
			}
			catch (Exception)
			{
			}
			RibbonPanel ribbonPanel = application.CreateRibbonPanel(tabName, "BimboClub Tools");
			string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			string folderPath2 = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
			string versionNumber = application.ControlledApplication.VersionNumber;
			string text = null;
			string text2 = Path.Combine(new string[]
			{
				folderPath,
				"Autodesk",
				"Revit",
				"Addins",
				versionNumber
			});
			if (File.Exists(Path.Combine(text2, "BimboClub.dll")))
			{
				text = text2;
			}
			else
			{
				string text3 = Path.Combine(new string[]
				{
					folderPath2,
					"Autodesk",
					"Revit",
					"Addins",
					versionNumber
				});
				if (File.Exists(Path.Combine(text3, "BimboClub.dll")))
				{
					text = text3;
				}
			}
			if (string.IsNullOrEmpty(text))
			{
				string path;
				try
				{
					path = new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath;
				}
				catch
				{
					path = Assembly.GetExecutingAssembly().Location;
				}
				text = Path.GetDirectoryName(path);
			}
			App.AssemblyDir = text;
			string text4 = Path.Combine(text, "BimboClub.dll");
			Logger.Log("Вычисленный путь сборки BimboClub: " + text4, "INFO");
			Logger.Log("Вычисленная папка сборки BimboClub: " + text, "INFO");
			PushButtonData pushButtonData = new PushButtonData("cmdCreate3DScheme", "3D схемы", text4, "BimboClub.Command");
			pushButtonData.ToolTip = "Создает изолированные 3D виды для выбранных инженерных систем.";
			PushButtonData pushButtonData2 = new PushButtonData("cmdPlaceTags", "АвтоМарки", text4, "BimboClub.TagCommand");
			pushButtonData2.ToolTip = "Автоматически расставляет выноски и марки для MEP элементов на активном виде с учетом масштаба.";
			PushButtonData pushButtonData3 = new PushButtonData("cmdWallOpening", "Отверстия в стенах", text4, "DuctWallOpenings.DuctWallOpeningsCommand");
			pushButtonData3.ToolTip = "Автоматически расставляет отверстия в стенах в местах пересечения с горизонтальными воздуховодами.";
			PushButtonData pushButtonData4 = new PushButtonData("cmdFloorOpening", "Отверстия в полах", text4, "DuctWallOpenings.DuctFloorOpeningsCommand");
			pushButtonData4.ToolTip = "Автоматически расставляет отверстия в полах/перекрытиях в местах пересечения с вертикальными воздуховодами.";
			PushButtonData pushButtonData5 = new PushButtonData("cmdMergeWallOpenings", "Объединить в стенах", text4, "DuctWallOpenings.MergeWallOpeningsCommand");
			pushButtonData5.ToolTip = "Объединяет выбранные отверстия в стенах или все отверстия выбранного типа по общему максимальному габариту.";
			PushButtonData pushButtonData6 = new PushButtonData("cmdMergeFloorOpenings", "Объединить в полах", text4, "DuctWallOpenings.MergeFloorOpeningsCommand");
			pushButtonData6.ToolTip = "Объединяет выбранные отверстия в полах или все отверстия выбранного типа по общему максимальному габариту.";
			PushButtonData pushButtonData7 = new PushButtonData("cmdParamCopy", "Параметры\nсистемы", text4, "BimboClub.CopyParamCommand");
			pushButtonData7.ToolTip = "Утилита для копирования параметров инженерных систем (воздуховоды и трубопроводы).";
			PushButtonData pushButtonData9 = new PushButtonData("cmdPrintMaster", "Экспорт чертежей", text4, "BimboClub.PrintCommand");
			pushButtonData9.ToolTip = "Пакетный экспорт листов в PDF, DWG и IFC с автоопределением форматов рамок и умным именованием.";

			PushButtonData pushButtonData11 = new PushButtonData("cmdRouter", "Умная трассировка", text4, "BimboClub.RouterCommand");
			pushButtonData11.ToolTip = "Автоматически прокладывает обходы («утки») для воздуховодов и труб в местах пересечения со строительными конструкциями.";
			PushButtonData pushButtonData12 = new PushButtonData("cmdFilters", "Копирование фильтров", text4, "BimboClub.FiltersCommand");
			pushButtonData12.ToolTip = "Копирует выбранные фильтры видов (видимость, активность и переопределение графики) с вида-донора на выбранные виды-получатели.";
			PushButtonData pushButtonData13 = new PushButtonData("cmdRenameFamily", "Переименовать семейство", text4, "BimboClub.RenameFamilyCommand");
			pushButtonData13.ToolTip = "Переименовывает выбранное на виде семейство или позволяет выбрать любое загружаемое семейство из списка в проекте.";
			PushButtonData pushButtonData14 = new PushButtonData("cmdNetworkPlacement", "Расстановка по сети", text4, "BimboClub.PlacementCommand");
			pushButtonData14.ToolTip = "Автоматически расставляет крепления, подвесы или датчики вдоль осей воздуховодов, трубопроводов и лотков с заданным шагом и смещением.";
			PushButtonData pushButtonData15 = new PushButtonData("cmdAddParameterFinal", "Добавить параметры", text4, "BimboClub.AddParameterCommand");
			pushButtonData15.ToolTip = "Добавляет новые или общие (Shared) параметры в проект или семейство Revit.";
			PushButtonData pushButtonData16 = new PushButtonData("cmdJsonExport", "Экспорт JSON", text4, "LES.Revit.JsonExport.LesJsonExportCommand");
			pushButtonData16.ToolTip = "Экспортирует модель Revit в файл JSON (LES CAD/BIM формат).";
			PushButtonData pushButtonData17 = new PushButtonData("cmdExcelExport", "Экспорт Excel", text4, "BimboClub.ExcelExportCommand");
			pushButtonData17.ToolTip = "Экспортирует все виды элементов текущего вида в Excel с группировкой по категориям.";
			PushButtonData pushButtonData23 = new PushButtonData("cmdExcelImport", "Импорт Excel", text4, "BimboClub.ImportExcelTableCommand");
			pushButtonData23.ToolTip = "Импортирует таблицы Excel в чертежные виды или легенды Revit с возможностью последующего обновления.";
			PushButtonData pushButtonData19 = new PushButtonData("cmdRiserNumbering", "Нумерация стояков", text4, "BimboClub.RiserNumberingCommand");
			pushButtonData19.ToolTip = "Находит вертикальные трубы (стояки), пересекающие перекрытия (в том числе из связанных файлов), и присваивает им номера стояков.";
			PushButtonData pushButtonData20 = new PushButtonData("cmdPipeConnect", "Подключение труб", text4, "BimboClub.PipeConnectCommand");
			pushButtonData20.ToolTip = "Создаёт подключение двух перпендикулярных труб с выбором направления (сверху/снизу), типа отвода (90°/45°) и отступов.";
			PushButtonData pushButtonData21 = new PushButtonData("cmdParamFiller", "Заполнение параметров", text4, "BimboClub.ParamFillerCommand");
			pushButtonData21.ToolTip = "Заполнение наименований и количества для труб, воздуховодов и лотков по заданным правилам.";
			PushButtonData pushButtonDataRules = new PushButtonData("cmdRuleEditor", "Редактор правил", text4, "BimboClub.RuleEditor.RuleEditorCommand");
			pushButtonDataRules.ToolTip = "Универсальный редактор правил для создания условий, математических формул и пакетного заполнения параметров элементов.";
			PushButtonData pushButtonData22 = new PushButtonData("cmdCropView", "Область подрезки", text4, "BimboClub.CropViewCommand");
			pushButtonData22.ToolTip = "Устанавливает область подрезки вида вокруг выбранных элементов с заданным отступом.";

			BitmapSource bitmapSource = this.LoadImage(Path.Combine(text, "icon_3d.png"));
			if (bitmapSource != null)
			{
				pushButtonData.LargeImage = bitmapSource;
			}
			BitmapSource bitmapSource2 = this.LoadImage(Path.Combine(text, "icon_3d_16.png"));
			if (bitmapSource2 != null)
			{
				pushButtonData.Image = bitmapSource2;
			}
			BitmapSource bitmapSource3 = this.LoadImage(Path.Combine(text, "icon_tags.png"));
			if (bitmapSource3 != null)
			{
				pushButtonData2.LargeImage = bitmapSource3;
			}
			BitmapSource bitmapSource4 = this.LoadImage(Path.Combine(text, "icon_tags_16.png"));
			if (bitmapSource4 != null)
			{
				pushButtonData2.Image = bitmapSource4;
			}
			BitmapSource bitmapSource5 = this.LoadImage(Path.Combine(text, "icon_wall.png"));
			if (bitmapSource5 != null)
			{
				pushButtonData3.LargeImage = bitmapSource5;
			}
			BitmapSource bitmapSource6 = this.LoadImage(Path.Combine(text, "icon_wall_16.png"));
			if (bitmapSource6 != null)
			{
				pushButtonData3.Image = bitmapSource6;
			}
			BitmapSource bitmapSource7 = this.LoadImage(Path.Combine(text, "icon_floor.png"));
			if (bitmapSource7 != null)
			{
				pushButtonData4.LargeImage = bitmapSource7;
			}
			BitmapSource bitmapSource8 = this.LoadImage(Path.Combine(text, "icon_floor_16.png"));
			if (bitmapSource8 != null)
			{
				pushButtonData4.Image = bitmapSource8;
			}
			BitmapSource bitmapSource9 = this.LoadImage(Path.Combine(text, "icon_wall.png"));
			if (bitmapSource9 != null)
			{
				pushButtonData5.LargeImage = bitmapSource9;
			}
			BitmapSource bitmapSource10 = this.LoadImage(Path.Combine(text, "icon_wall_16.png"));
			if (bitmapSource10 != null)
			{
				pushButtonData5.Image = bitmapSource10;
			}
			BitmapSource bitmapSource11 = this.LoadImage(Path.Combine(text, "icon_floor.png"));
			if (bitmapSource11 != null)
			{
				pushButtonData6.LargeImage = bitmapSource11;
			}
			BitmapSource bitmapSource12 = this.LoadImage(Path.Combine(text, "icon_floor_16.png"));
			if (bitmapSource12 != null)
			{
				pushButtonData6.Image = bitmapSource12;
			}
			BitmapSource bitmapSource13 = this.LoadImage(Path.Combine(text, "icon_copy.png"));
			if (bitmapSource13 != null)
			{
				pushButtonData7.LargeImage = bitmapSource13;
			}
			BitmapSource bitmapSource14 = this.LoadImage(Path.Combine(text, "icon_copy_16.png"));
			if (bitmapSource14 != null)
			{
				pushButtonData7.Image = bitmapSource14;
			}

			BitmapSource bitmapSource17 = this.LoadImage(Path.Combine(text, "icon_print.png"));
			if (bitmapSource17 != null)
			{
				pushButtonData9.LargeImage = bitmapSource17;
			}
			BitmapSource bitmapSource18 = this.LoadImage(Path.Combine(text, "icon_print_16.png"));
			if (bitmapSource18 != null)
			{
				pushButtonData9.Image = bitmapSource18;
			}

			BitmapSource bitmapSource21 = this.LoadImage(Path.Combine(text, "icon_router.png"));
			if (bitmapSource21 != null)
			{
				pushButtonData11.LargeImage = bitmapSource21;
			}
			BitmapSource bitmapSource22 = this.LoadImage(Path.Combine(text, "icon_router_16.png"));
			if (bitmapSource22 != null)
			{
				pushButtonData11.Image = bitmapSource22;
			}
			BitmapSource bitmapSource23 = this.LoadImage(Path.Combine(text, "icon_filters.png"));
			if (bitmapSource23 != null)
			{
				pushButtonData12.LargeImage = bitmapSource23;
			}
			BitmapSource bitmapSource24 = this.LoadImage(Path.Combine(text, "icon_filters_16.png"));
			if (bitmapSource24 != null)
			{
				pushButtonData12.Image = bitmapSource24;
			}
			BitmapSource bitmapSource25 = this.LoadImage(Path.Combine(text, "icon_rename.png"));
			if (bitmapSource25 != null)
			{
				pushButtonData13.LargeImage = bitmapSource25;
			}
			BitmapSource bitmapSource26 = this.LoadImage(Path.Combine(text, "icon_rename_16.png"));
			if (bitmapSource26 != null)
			{
				pushButtonData13.Image = bitmapSource26;
			}
			BitmapSource bitmapSource27 = this.LoadImage(Path.Combine(text, "icon_network.png"));
			if (bitmapSource27 != null)
			{
				pushButtonData14.LargeImage = bitmapSource27;
			}
			BitmapSource bitmapSource28 = this.LoadImage(Path.Combine(text, "icon_network_16.png"));
			if (bitmapSource28 != null)
			{
				pushButtonData14.Image = bitmapSource28;
			}
			BitmapSource bitmapSource29 = this.LoadImage(Path.Combine(text, "icon_param.png"));
			if (bitmapSource29 != null)
			{
				pushButtonData15.LargeImage = bitmapSource29;
			}
			BitmapSource bitmapSource30 = this.LoadImage(Path.Combine(text, "icon_param_16.png"));
			if (bitmapSource30 != null)
			{
				pushButtonData15.Image = bitmapSource30;
			}
			BitmapSource bitmapSource31 = this.LoadImage(Path.Combine(text, "icon_json.png"));
			if (bitmapSource31 != null)
			{
				pushButtonData16.LargeImage = bitmapSource31;
			}
			BitmapSource bitmapSource32 = this.LoadImage(Path.Combine(text, "icon_json_16.png"));
			if (bitmapSource32 != null)
			{
				pushButtonData16.Image = bitmapSource32;
			}
			BitmapSource bitmapSource33 = this.LoadImage(Path.Combine(text, "icon_excel.png"));
			if (bitmapSource33 != null)
			{
				pushButtonData17.LargeImage = bitmapSource33;
				pushButtonData23.LargeImage = bitmapSource33;
			}
			BitmapSource bitmapSource34 = this.LoadImage(Path.Combine(text, "icon_excel_16.png"));
			if (bitmapSource34 != null)
			{
				pushButtonData17.Image = bitmapSource34;
				pushButtonData23.Image = bitmapSource34;
			}

			BitmapSource bitmapSource37 = this.LoadImage(Path.Combine(text, "icon_riser.png"));
			if (bitmapSource37 != null)
			{
				pushButtonData19.LargeImage = bitmapSource37;
			}
			BitmapSource bitmapSource38 = this.LoadImage(Path.Combine(text, "icon_riser_16.png"));
			if (bitmapSource38 != null)
			{
				pushButtonData19.Image = bitmapSource38;
			}
			BitmapSource bitmapSource39 = this.LoadImage(Path.Combine(text, "icon_connect.png"));
			if (bitmapSource39 != null)
			{
				pushButtonData20.LargeImage = bitmapSource39;
			}
			BitmapSource bitmapSource40 = this.LoadImage(Path.Combine(text, "icon_connect_16.png"));
			if (bitmapSource40 != null)
			{
				pushButtonData20.Image = bitmapSource40;
			}
			BitmapSource bitmapSource41 = this.LoadImage(Path.Combine(text, "icon_rules.png"));
			if (bitmapSource41 != null)
			{
				pushButtonData21.LargeImage = bitmapSource41;
				pushButtonDataRules.LargeImage = bitmapSource41;
			}
			BitmapSource bitmapSource42 = this.LoadImage(Path.Combine(text, "icon_rules_16.png"));
			if (bitmapSource42 != null)
			{
				pushButtonData21.Image = bitmapSource42;
				pushButtonDataRules.Image = bitmapSource42;
			}
			BitmapSource bitmapSource43 = this.LoadImage(Path.Combine(text, "icon_crop.png"));
			if (bitmapSource43 != null)
			{
				pushButtonData22.LargeImage = bitmapSource43;
			}
			BitmapSource bitmapSource44 = this.LoadImage(Path.Combine(text, "icon_crop_16.png"));
			if (bitmapSource44 != null)
			{
				pushButtonData22.Image = bitmapSource44;
			}
			// --- Pulldown: Схемы и Виды ---
			try
			{
				PulldownButtonData pdData = new PulldownButtonData("pbViewsAndSchemes", "Схемы и Виды");
				PulldownButton pd = ribbonPanel.AddItem(pdData) as PulldownButton;
				if (pd != null)
				{
					pd.LargeImage = bitmapSource; // icon_3d.png
					pd.Image = bitmapSource2; // icon_3d_16.png
					pd.AddPushButton(pushButtonData); // 3D схемы
					pd.AddPushButton(pushButtonData22); // Область подрезки
					pd.AddPushButton(pushButtonData12); // Копирование фильтров
					pd.AddPushButton(pushButtonData2); // АвтоМарки
				}
			}
			catch (Exception ex)
			{
				Logger.LogError("Не удалось создать выпадающий список 'Схемы и Виды'", ex);
			}

			// --- Pulldown: Отверстия ---
			try
			{
				PulldownButtonData pdData = new PulldownButtonData("pbOpenings", "Отверстия");
				PulldownButton pd = ribbonPanel.AddItem(pdData) as PulldownButton;
				if (pd != null)
				{
					pd.LargeImage = bitmapSource5; // icon_wall.png
					pd.Image = bitmapSource6; // icon_wall_16.png
					pd.AddPushButton(pushButtonData3); // Отверстия в стенах
					pd.AddPushButton(pushButtonData4); // Отверстия в полах
					pd.AddPushButton(pushButtonData5); // Объединить в стенах
					pd.AddPushButton(pushButtonData6); // Объединить в полах
				}
			}
			catch (Exception ex)
			{
				Logger.LogError("Не удалось создать выпадающий список 'Отверстия'", ex);
			}

			// --- Pulldown: Сети MEP ---
			try
			{
				PushButtonData pushButtonDataPipeClamps = new PushButtonData("cmdPipeClamps", "Расстановка хомутов", text4, "BimboClub.PipeClamps.PipeClampsCommand");
				pushButtonDataPipeClamps.ToolTip = "Автоматическая расстановка хомутов по вертикальным трубопроводам с выбором типа и шага по диаметрам.";
				BitmapSource iconClamp16 = this.LoadImage(Path.Combine(text, "icon_router_16.png"));
				if (iconClamp16 != null)
				{
					pushButtonDataPipeClamps.Image = iconClamp16;
				}

				PulldownButtonData pdData = new PulldownButtonData("pbMepNetworks", "Сети MEP");
				PulldownButton pd = ribbonPanel.AddItem(pdData) as PulldownButton;
				if (pd != null)
				{
					pd.LargeImage = bitmapSource21; // icon_router.png
					pd.Image = bitmapSource22; // icon_router_16.png
					pd.AddPushButton(pushButtonDataPipeClamps); // Расстановка хомутов по стоякам
					pd.AddPushButton(pushButtonData11); // Умная трассировка
					pd.AddPushButton(pushButtonData19); // Нумерация стояков
					pd.AddPushButton(pushButtonData20); // Подключение труб
					pd.AddPushButton(pushButtonData14); // Расстановка по сети
				}
			}
			catch (Exception ex)
			{
				Logger.LogError("Не удалось создать выпадающий список 'Сети MEP'", ex);
			}

			PushButtonData pushButtonDataHeatLoss = new PushButtonData("cmdHeatLossPrep", "Расчет\nтеплопотерь", text4, "BimboClub.HeatLoss.HeatLossPrepCommand");
			pushButtonDataHeatLoss.ToolTip = "Автоматизированная подготовка данных и расчет ограждающих конструкций (кубики-маркеры) для теплопотерь.";
			BitmapSource iconHeat = this.LoadImage(Path.Combine(text, "icon_param.png"));
			if (iconHeat != null)
			{
				pushButtonDataHeatLoss.LargeImage = iconHeat;
			}
			BitmapSource iconHeat16 = this.LoadImage(Path.Combine(text, "icon_param_16.png"));
			if (iconHeat16 != null)
			{
				pushButtonDataHeatLoss.Image = iconHeat16;
			}

			// 1. Отдельная крупная кнопка "Расчет теплопотерь" на ленте BimboClub
			try
			{
				ribbonPanel.AddItem(pushButtonDataHeatLoss);
			}
			catch (Exception ex)
			{
				Logger.LogError("Не удалось добавить кнопку 'Расчет теплопотерь' на ленту", ex);
			}

			// --- Pulldown: Параметры ---
			try
			{
				PulldownButtonData pdData = new PulldownButtonData("pbParams", "Параметры");
				PulldownButton pd = ribbonPanel.AddItem(pdData) as PulldownButton;
				if (pd != null)
				{
					pd.LargeImage = bitmapSource41; // icon_rules.png
					pd.Image = bitmapSource42; // icon_rules_16.png
					pd.AddPushButton(pushButtonDataHeatLoss); // Расчет теплопотерь BimboClub
					pd.AddPushButton(pushButtonDataRules); // Редактор правил BimboClub
					pd.AddPushButton(pushButtonData21); // Заполнение параметров
					pd.AddPushButton(pushButtonData15); // Добавить параметры
					pd.AddPushButton(pushButtonData13); // Переименовать семейство
					pd.AddPushButton(pushButtonData7); // Копировать параметры системы
				}
			}
			catch (Exception ex)
			{
				Logger.LogError("Не удалось создать выпадающий список 'Параметры'", ex);
			}

			// --- Pulldown: Импорт/Экспорт ---
			try
			{
				PulldownButtonData pdData = new PulldownButtonData("pbDataOutput", "Импорт/Экспорт");
				PulldownButton pd = ribbonPanel.AddItem(pdData) as PulldownButton;
				if (pd != null)
				{
					pd.LargeImage = bitmapSource33; // icon_excel.png
					pd.Image = bitmapSource34; // icon_excel_16.png
					pd.AddPushButton(pushButtonData9); // Экспорт чертежей
					pd.AddPushButton(pushButtonData16); // Экспорт JSON
					pd.AddPushButton(pushButtonData17); // Экспорт Excel
					pd.AddPushButton(pushButtonData23); // Импорт Excel
				}
			}
			catch (Exception ex)
			{
				Logger.LogError("Не удалось создать выпадающий список 'Импорт/Экспорт'", ex);
			}

			return Result.Succeeded;
		}
		private BitmapSource LoadImage(string path)
		{
			if (!File.Exists(path))
			{
				Logger.Log("Файл иконки не найден по пути: " + path, "WARNING");
				return null;
			}
			BitmapSource result;
			try
			{
				using (FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
				{
					BitmapFrame expr_3D = BitmapDecoder.Create(fileStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
					expr_3D.Freeze();
					result = expr_3D;
				}
			}
			catch (Exception ex)
			{
				Logger.LogError("Не удалось загрузить изображение по пути: " + path, ex);
				result = null;
			}
			return result;
		}
		public Result OnShutdown(UIControlledApplication application)
		{
			AppDomain.CurrentDomain.AssemblyResolve -= new ResolveEventHandler(this.OnAssemblyResolve);
			return Result.Succeeded;
		}
		private Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
		{
			try
			{
				AssemblyName assemblyName = new AssemblyName(args.Name);
				if (assemblyName.Name == "RevitAPI" || assemblyName.Name == "RevitAPIUI" || assemblyName.Name == "System.Net.Http")
				{
					Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
					for (int i = 0; i < assemblies.Length; i++)
					{
						Assembly assembly = assemblies[i];
						if (assembly.GetName().Name == assemblyName.Name)
						{
							Assembly result = assembly;
							return result;
						}
					}
				}
				if (!string.IsNullOrEmpty(App.AssemblyDir))
				{
					string text = Path.Combine(App.AssemblyDir, assemblyName.Name + ".dll");
					if (File.Exists(text))
					{
						Assembly result = Assembly.LoadFrom(text);
						return result;
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogError("Ошибка при разрешении сборки в AssemblyResolve", ex);
			}
			return null;
		}
	}
}
