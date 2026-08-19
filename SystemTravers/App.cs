using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BimboClub
{
	public class App : IExternalApplication
	{
		public static string AssemblyDir { get; private set; }

		public Result OnStartup(UIControlledApplication application)
		{
			AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(this.OnAssemblyResolve);
			Logger.Log("Запуск инициализации панели BimboClub Tools.", "INFO");

			string tabName = "⚫ BimboClub";
			try
			{
				application.CreateRibbonTab(tabName);
			}
			catch (Exception) { }

			RibbonPanel ribbonPanel = application.CreateRibbonPanel(tabName, "BimboClub Tools");

			string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			string folderPath2 = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
			string versionNumber = application.ControlledApplication.VersionNumber;
			string text = null;

			string text2 = System.IO.Path.Combine(folderPath, "Autodesk", "Revit", "Addins", versionNumber);
			if (File.Exists(System.IO.Path.Combine(text2, "BimboClub.dll")))
			{
				text = text2;
			}
			else
			{
				string text3 = System.IO.Path.Combine(folderPath2, "Autodesk", "Revit", "Addins", versionNumber);
				if (File.Exists(System.IO.Path.Combine(text3, "BimboClub.dll")))
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
				text = System.IO.Path.GetDirectoryName(path);
			}
			App.AssemblyDir = text;
			string text4 = System.IO.Path.Combine(text, "BimboClub.dll");

			Logger.Log("Вычисленный путь сборки BimboClub: " + text4, "INFO");
			Logger.Log("Вычисленная папка сборки BimboClub: " + text, "INFO");

			// --- Инициализация кнопок ---
			PushButtonData pushButtonData3D = new PushButtonData("cmdCreate3DScheme", "3D схемы", text4, "BimboClub.Command")
			{
				ToolTip = "Создает изолированные 3D виды для выбранных инженерных систем."
			};

			PushButtonData pushButtonDataTags = new PushButtonData("cmdPlaceTags", "АвтоМарки", text4, "BimboClub.TagCommand")
			{
				ToolTip = "Автоматически расставляет выноски и марки для MEP элементов на активном виде с учетом масштаба."
			};

			PushButtonData pushButtonDataWallOp = new PushButtonData("cmdWallOpening", "Отверстия в стенах", text4, "DuctWallOpenings.DuctWallOpeningsCommand")
			{
				ToolTip = "Автоматически расставляет отверстия в стенах в местах пересечения с горизонтальными воздуховодами."
			};

			PushButtonData pushButtonDataFloorOp = new PushButtonData("cmdFloorOpening", "Отверстия в полах", text4, "DuctWallOpenings.DuctFloorOpeningsCommand")
			{
				ToolTip = "Автоматически расставляет отверстия в полах/перекрытиях в местах пересечения с вертикальными воздуховодами."
			};

			PushButtonData pushButtonDataMergeWallOp = new PushButtonData("cmdMergeWallOpenings", "Объединить в стенах", text4, "DuctWallOpenings.MergeWallOpeningsCommand")
			{
				ToolTip = "Объединяет выбранные отверстия в стенах по общему максимальному габариту."
			};

			PushButtonData pushButtonDataMergeFloorOp = new PushButtonData("cmdMergeFloorOpenings", "Объединить в полах", text4, "DuctWallOpenings.MergeFloorOpeningsCommand")
			{
				ToolTip = "Объединяет выбранные отверстия в полах по общему максимальному габариту."
			};

			PushButtonData pushButtonDataOpeningsBcc = new PushButtonData("cmdOpeningsBcc", "Задание на отверстия", text4, "BimboClub.OpeningsCommand")
			{
				ToolTip = "Моделирование и автоматическая расстановка задания на отверстия на пересечении сетей и конструкций."
			};

			PushButtonData pushButtonDataParamCopy = new PushButtonData("cmdParamCopy", "Параметры\nсистемы", text4, "BimboClub.CopyParamCommand")
			{
				ToolTip = "Утилита для копирования параметров инженерных систем (воздуховоды и трубопроводы)."
			};

			PushButtonData pushButtonDataPrint = new PushButtonData("cmdPrintMaster", "Экспорт чертежей", text4, "BimboClub.PrintCommand")
			{
				ToolTip = "Пакетный экспорт листов в PDF, DWG и IFC с автоопределением форматов рамок и умным именованием."
			};

			PushButtonData pushButtonDataExportPdfBcc = new PushButtonData("cmdExportPdfBcc", "Экспорт PDF", text4, "BimboClub.ExportPdfCommand")
			{
				ToolTip = "Пакетный экспорт выбранных листов проекта в PDF формат."
			};

			PushButtonData pushButtonDataSchedulePackBcc = new PushButtonData("cmdSchedulePackBcc", "Пакет спецификаций", text4, "BimboClub.SchedulePackCommand")
			{
				ToolTip = "Пакетный экспорт спецификаций проекта в CSV/TXT файлы."
			};

			PushButtonData pushButtonDataRouter = new PushButtonData("cmdRouter", "Умная трассировка", text4, "BimboClub.RouterCommand")
			{
				ToolTip = "Автоматически прокладывает обходы («утки») для воздуховодов и труб в местах пересечения со строительными конструкциями."
			};

			PushButtonData pushButtonDataFilters = new PushButtonData("cmdFilters", "Копирование фильтров", text4, "BimboClub.FiltersCommand")
			{
				ToolTip = "Копирует выбранные фильтры видов (видимость, активность и переопределение графики) с вида-донора на выбранные виды-получатели."
			};

			PushButtonData pushButtonDataFilterCopyBcc = new PushButtonData("cmdFilterCopyBcc", "Перенос фильтров", text4, "BimboClub.FilterCopyCommand")
			{
				ToolTip = "Пакетное копирование фильтров видов и переопределений графики между видами и шаблонами видов."
			};

			PushButtonData pushButtonDataRename = new PushButtonData("cmdRenameFamily", "Переименовать семейство", text4, "BimboClub.RenameFamilyCommand")
			{
				ToolTip = "Переименовывает выбранное на виде семейство или позволяет выбрать любое загружаемое семейство из списка в проекте."
			};

			PushButtonData pushButtonDataPlacement = new PushButtonData("cmdNetworkPlacement", "Расстановка по сети", text4, "BimboClub.PlacementCommand")
			{
				ToolTip = "Автоматически расставляет крепления, подвесы или датчики вдоль осей воздуховодов, трубопроводов и лотков с заданным шагом и смещением."
			};

			PushButtonData pushButtonDataHangersBcc = new PushButtonData("cmdHangersBcc", "Расстановка крепежа", text4, "BimboClub.HangersCommand")
			{
				ToolTip = "Расчет и автоматическая расстановка подвесов и крепежа для инженерных сетей."
			};

			PushButtonData pushButtonDataLevelingBcc = new PushButtonData("cmdLevelingBcc", "Привязка к уровням", text4, "BimboClub.LevelingCommand")
			{
				ToolTip = "Автоматическая привязка элементов к ближайшим уровням по высоте со смещением."
			};

			PushButtonData pushButtonDataPlaceXyzBcc = new PushButtonData("cmdPlaceXyzBcc", "Импорт XYZ", text4, "BimboClub.PlaceByXyzCommand")
			{
				ToolTip = "Расстановка семейств по координатам XYZ из файла CSV/TXT."
			};

			PushButtonData pushButtonDataHeatLossBcc = new PushButtonData("cmdHeatLossBcc", "Теплопотери", text4, "BimboClub.HeatLossCommand")
			{
				ToolTip = "Расстановка кубиков теплопотерь по ограждающим конструкциям помещений и создание отчета в Excel."
			};

			PushButtonData pushButtonDataParamRulesBcc = new PushButtonData("cmdParamRulesBcc", "Редактор правил", text4, "BimboClub.ParamRulesCommand")
			{
				ToolTip = "Универсальный редактор правил: фильтрация элементов, целевой параметр и формулы значений."
			};

			PushButtonData pushButtonDataBatchParamsBcc = new PushButtonData("cmdBatchParamsBcc", "Пакетные параметры", text4, "BimboClub.BatchParamsCommand")
			{
				ToolTip = "Пакетное добавление общих параметров из файла ФОП в категории проекта."
			};

			PushButtonData pushButtonDataRevitServerBcc = new PushButtonData("cmdRevitServerBcc", "Revit Server", text4, "BimboClub.RevitServerCommand")
			{
				ToolTip = "Скачивание и выгрузка моделей с Revit Server."
			};

			PushButtonData pushButtonDataJson = new PushButtonData("cmdJsonExport", "Экспорт JSON", text4, "LES.Revit.JsonExport.LesJsonExportCommand")
			{
				ToolTip = "Экспортирует модель Revit в файл JSON (LES CAD/BIM формат)."
			};

			PushButtonData pushButtonDataExcelExp = new PushButtonData("cmdExcelExport", "Экспорт Excel", text4, "BimboClub.ExcelExportCommand")
			{
				ToolTip = "Экспортирует все виды элементов текущего вида в Excel с группировкой по категориям."
			};

			PushButtonData pushButtonDataExcelImp = new PushButtonData("cmdExcelImport", "Импорт Excel", text4, "BimboClub.ImportExcelTableCommand")
			{
				ToolTip = "Импортирует таблицы Excel в чертежные виды или легенды Revit с возможностью последующего обновления."
			};

			PushButtonData pushButtonDataRiser = new PushButtonData("cmdRiserNumbering", "Нумерация стояков", text4, "BimboClub.RiserNumberingCommand")
			{
				ToolTip = "Находит вертикальные трубы (стояки), пересекающие перекрытия (в том числе из связанных файлов), и присваивает им номера стояков."
			};

			PushButtonData pushButtonDataConnect = new PushButtonData("cmdPipeConnect", "Подключение труб", text4, "BimboClub.PipeConnectCommand")
			{
				ToolTip = "Создаёт подключение двух перпендикулярных труб с выбором направления (сверху/снизу), типа отвода (90°/45°) и отступов."
			};

			PushButtonData pushButtonDataCrop = new PushButtonData("cmdCropView", "Область подрезки", text4, "BimboClub.CropViewCommand")
			{
				ToolTip = "Устанавливает область подрезки вида вокруг выбранных элементов с заданным отступом."
			};

			// Загрузка иконок
			BitmapSource bitmapSource3D = LoadImage(System.IO.Path.Combine(text, "icon_3d.png"));
			BitmapSource bitmapSource3D16 = LoadImage(System.IO.Path.Combine(text, "icon_3d_16.png"));
			if (bitmapSource3D != null) pushButtonData3D.LargeImage = bitmapSource3D;
			if (bitmapSource3D16 != null) pushButtonData3D.Image = bitmapSource3D16;

			BitmapSource bitmapSourceTags = LoadImage(System.IO.Path.Combine(text, "icon_tags.png"));
			BitmapSource bitmapSourceTags16 = LoadImage(System.IO.Path.Combine(text, "icon_tags_16.png"));
			if (bitmapSourceTags != null) pushButtonDataTags.LargeImage = bitmapSourceTags;
			if (bitmapSourceTags16 != null) pushButtonDataTags.Image = bitmapSourceTags16;

			BitmapSource bitmapSourceWall = LoadImage(System.IO.Path.Combine(text, "icon_wall.png"));
			BitmapSource bitmapSourceWall16 = LoadImage(System.IO.Path.Combine(text, "icon_wall_16.png"));
			if (bitmapSourceWall != null)
			{
				pushButtonDataWallOp.LargeImage = bitmapSourceWall;
				pushButtonDataMergeWallOp.LargeImage = bitmapSourceWall;
				pushButtonDataOpeningsBcc.LargeImage = bitmapSourceWall;
			}
			if (bitmapSourceWall16 != null)
			{
				pushButtonDataWallOp.Image = bitmapSourceWall16;
				pushButtonDataMergeWallOp.Image = bitmapSourceWall16;
				pushButtonDataOpeningsBcc.Image = bitmapSourceWall16;
			}

			BitmapSource bitmapSourceFloor = LoadImage(System.IO.Path.Combine(text, "icon_floor.png"));
			BitmapSource bitmapSourceFloor16 = LoadImage(System.IO.Path.Combine(text, "icon_floor_16.png"));
			if (bitmapSourceFloor != null)
			{
				pushButtonDataFloorOp.LargeImage = bitmapSourceFloor;
				pushButtonDataMergeFloorOp.LargeImage = bitmapSourceFloor;
			}
			if (bitmapSourceFloor16 != null)
			{
				pushButtonDataFloorOp.Image = bitmapSourceFloor16;
				pushButtonDataMergeFloorOp.Image = bitmapSourceFloor16;
			}

			BitmapSource bitmapSourceCopy = LoadImage(System.IO.Path.Combine(text, "icon_copy.png"));
			BitmapSource bitmapSourceCopy16 = LoadImage(System.IO.Path.Combine(text, "icon_copy_16.png"));
			if (bitmapSourceCopy != null) pushButtonDataParamCopy.LargeImage = bitmapSourceCopy;
			if (bitmapSourceCopy16 != null) pushButtonDataParamCopy.Image = bitmapSourceCopy16;

			BitmapSource bitmapSourcePrint = LoadImage(System.IO.Path.Combine(text, "icon_print.png"));
			BitmapSource bitmapSourcePrint16 = LoadImage(System.IO.Path.Combine(text, "icon_print_16.png"));
			if (bitmapSourcePrint != null)
			{
				pushButtonDataPrint.LargeImage = bitmapSourcePrint;
				pushButtonDataExportPdfBcc.LargeImage = bitmapSourcePrint;
				pushButtonDataSchedulePackBcc.LargeImage = bitmapSourcePrint;
			}
			if (bitmapSourcePrint16 != null)
			{
				pushButtonDataPrint.Image = bitmapSourcePrint16;
				pushButtonDataExportPdfBcc.Image = bitmapSourcePrint16;
				pushButtonDataSchedulePackBcc.Image = bitmapSourcePrint16;
			}

			BitmapSource bitmapSourceRouter = LoadImage(System.IO.Path.Combine(text, "icon_router.png"));
			BitmapSource bitmapSourceRouter16 = LoadImage(System.IO.Path.Combine(text, "icon_router_16.png"));
			if (bitmapSourceRouter != null) pushButtonDataRouter.LargeImage = bitmapSourceRouter;
			if (bitmapSourceRouter16 != null) pushButtonDataRouter.Image = bitmapSourceRouter16;

			BitmapSource bitmapSourceFilters = LoadImage(System.IO.Path.Combine(text, "icon_filters.png"));
			BitmapSource bitmapSourceFilters16 = LoadImage(System.IO.Path.Combine(text, "icon_filters_16.png"));
			if (bitmapSourceFilters != null)
			{
				pushButtonDataFilters.LargeImage = bitmapSourceFilters;
				pushButtonDataFilterCopyBcc.LargeImage = bitmapSourceFilters;
			}
			if (bitmapSourceFilters16 != null)
			{
				pushButtonDataFilters.Image = bitmapSourceFilters16;
				pushButtonDataFilterCopyBcc.Image = bitmapSourceFilters16;
			}

			BitmapSource bitmapSourceRename = LoadImage(System.IO.Path.Combine(text, "icon_rename.png"));
			BitmapSource bitmapSourceRename16 = LoadImage(System.IO.Path.Combine(text, "icon_rename_16.png"));
			if (bitmapSourceRename != null) pushButtonDataRename.LargeImage = bitmapSourceRename;
			if (bitmapSourceRename16 != null) pushButtonDataRename.Image = bitmapSourceRename16;

			BitmapSource bitmapSourceNetwork = LoadImage(System.IO.Path.Combine(text, "icon_network.png"));
			BitmapSource bitmapSourceNetwork16 = LoadImage(System.IO.Path.Combine(text, "icon_network_16.png"));
			if (bitmapSourceNetwork != null)
			{
				pushButtonDataPlacement.LargeImage = bitmapSourceNetwork;
				pushButtonDataHangersBcc.LargeImage = bitmapSourceNetwork;
				pushButtonDataLevelingBcc.LargeImage = bitmapSourceNetwork;
				pushButtonDataPlaceXyzBcc.LargeImage = bitmapSourceNetwork;
			}
			if (bitmapSourceNetwork16 != null)
			{
				pushButtonDataPlacement.Image = bitmapSourceNetwork16;
				pushButtonDataHangersBcc.Image = bitmapSourceNetwork16;
				pushButtonDataLevelingBcc.Image = bitmapSourceNetwork16;
				pushButtonDataPlaceXyzBcc.Image = bitmapSourceNetwork16;
			}

			BitmapSource bitmapSourceJson = LoadImage(System.IO.Path.Combine(text, "icon_json.png"));
			BitmapSource bitmapSourceJson16 = LoadImage(System.IO.Path.Combine(text, "icon_json_16.png"));
			if (bitmapSourceJson != null) pushButtonDataJson.LargeImage = bitmapSourceJson;
			if (bitmapSourceJson16 != null) pushButtonDataJson.Image = bitmapSourceJson16;

			BitmapSource bitmapSourceExcel = LoadImage(System.IO.Path.Combine(text, "icon_excel.png"));
			BitmapSource bitmapSourceExcel16 = LoadImage(System.IO.Path.Combine(text, "icon_excel_16.png"));
			if (bitmapSourceExcel != null)
			{
				pushButtonDataExcelExp.LargeImage = bitmapSourceExcel;
				pushButtonDataExcelImp.LargeImage = bitmapSourceExcel;
			}
			if (bitmapSourceExcel16 != null)
			{
				pushButtonDataExcelExp.Image = bitmapSourceExcel16;
				pushButtonDataExcelImp.Image = bitmapSourceExcel16;
			}

			BitmapSource bitmapSourceRiser = LoadImage(System.IO.Path.Combine(text, "icon_riser.png"));
			BitmapSource bitmapSourceRiser16 = LoadImage(System.IO.Path.Combine(text, "icon_riser_16.png"));
			if (bitmapSourceRiser != null) pushButtonDataRiser.LargeImage = bitmapSourceRiser;
			if (bitmapSourceRiser16 != null) pushButtonDataRiser.Image = bitmapSourceRiser16;

			BitmapSource bitmapSourceConnect = LoadImage(System.IO.Path.Combine(text, "icon_connect.png"));
			BitmapSource bitmapSourceConnect16 = LoadImage(System.IO.Path.Combine(text, "icon_connect_16.png"));
			if (bitmapSourceConnect != null) pushButtonDataConnect.LargeImage = bitmapSourceConnect;
			if (bitmapSourceConnect16 != null) pushButtonDataConnect.Image = bitmapSourceConnect16;

			BitmapSource bitmapSourceCrop = LoadImage(System.IO.Path.Combine(text, "icon_crop.png"));
			BitmapSource bitmapSourceCrop16 = LoadImage(System.IO.Path.Combine(text, "icon_crop_16.png"));
			if (bitmapSourceCrop != null) pushButtonDataCrop.LargeImage = bitmapSourceCrop;
			if (bitmapSourceCrop16 != null) pushButtonDataCrop.Image = bitmapSourceCrop16;

			BitmapSource bitmapSourceParam = LoadImage(System.IO.Path.Combine(text, "icon_param.png"));
			BitmapSource bitmapSourceParam16 = LoadImage(System.IO.Path.Combine(text, "icon_param_16.png"));
			if (bitmapSourceParam != null)
			{
				pushButtonDataHeatLossBcc.LargeImage = bitmapSourceParam;
				pushButtonDataParamRulesBcc.LargeImage = bitmapSourceParam;
				pushButtonDataBatchParamsBcc.LargeImage = bitmapSourceParam;
				pushButtonDataRevitServerBcc.LargeImage = bitmapSourceParam;
			}
			if (bitmapSourceParam16 != null)
			{
				pushButtonDataHeatLossBcc.Image = bitmapSourceParam16;
				pushButtonDataParamRulesBcc.Image = bitmapSourceParam16;
				pushButtonDataBatchParamsBcc.Image = bitmapSourceParam16;
				pushButtonDataRevitServerBcc.Image = bitmapSourceParam16;
			}

			// --- Pulldown: Схемы и Виды ---
			try
			{
				PulldownButtonData pdData = new PulldownButtonData("pbViewsAndSchemes", "Схемы и Виды");
				PulldownButton pd = ribbonPanel.AddItem(pdData) as PulldownButton;
				if (pd != null)
				{
					pd.LargeImage = bitmapSource3D;
					pd.Image = bitmapSource3D16;
					pd.AddPushButton(pushButtonData3D);
					pd.AddPushButton(pushButtonDataCrop);
					pd.AddPushButton(pushButtonDataFilters);
					pd.AddPushButton(pushButtonDataFilterCopyBcc);
					pd.AddPushButton(pushButtonDataTags);
				}
			}
			catch (Exception ex) { Logger.LogError("Ошибка добавления Схемы и Виды", ex); }

			// --- Pulldown: Отверстия ---
			try
			{
				PulldownButtonData pdData = new PulldownButtonData("pbOpenings", "Отверстия");
				PulldownButton pd = ribbonPanel.AddItem(pdData) as PulldownButton;
				if (pd != null)
				{
					pd.LargeImage = bitmapSourceWall;
					pd.Image = bitmapSourceWall16;
					pd.AddPushButton(pushButtonDataOpeningsBcc);
					pd.AddPushButton(pushButtonDataWallOp);
					pd.AddPushButton(pushButtonDataFloorOp);
					pd.AddPushButton(pushButtonDataMergeWallOp);
					pd.AddPushButton(pushButtonDataMergeFloorOp);
				}
			}
			catch (Exception ex) { Logger.LogError("Ошибка добавления Отверстия", ex); }

			// --- Pulldown: Сети MEP ---
			try
			{
				PulldownButtonData pdData = new PulldownButtonData("pbMepNetworks", "Сети MEP");
				PulldownButton pd = ribbonPanel.AddItem(pdData) as PulldownButton;
				if (pd != null)
				{
					pd.LargeImage = bitmapSourceRouter;
					pd.Image = bitmapSourceRouter16;
					pd.AddPushButton(pushButtonDataRouter);
					pd.AddPushButton(pushButtonDataHangersBcc);
					pd.AddPushButton(pushButtonDataRiser);
					pd.AddPushButton(pushButtonDataConnect);
					pd.AddPushButton(pushButtonDataPlacement);
					pd.AddPushButton(pushButtonDataLevelingBcc);
					pd.AddPushButton(pushButtonDataPlaceXyzBcc);
				}
			}
			catch (Exception ex) { Logger.LogError("Ошибка добавления Сети MEP", ex); }

			// 1. Отдельная крупная кнопка "Расстановка хомутов"
			try
			{
				PushButtonData pushButtonDataPipeClamps = new PushButtonData("cmdPipeClamps", "Расстановка\nхомутов", text4, "BimboClub.PipeClamps.PipeClampsCommand")
				{
					ToolTip = "Автоматическая расстановка хомутов по вертикальным трубопроводам с выбором типа, шага и переносом параметров стояка.",
					LargeImage = bitmapSourceRouter,
					Image = bitmapSourceRouter16
				};
				ribbonPanel.AddItem(pushButtonDataPipeClamps);
			}
			catch (Exception ex) { Logger.LogError("Ошибка добавления Расстановка хомутов", ex); }

			// --- Pulldown: Параметры & Теплопотери ---
			try
			{
				PulldownButtonData pdData = new PulldownButtonData("pbParams", "Параметры");
				PulldownButton pd = ribbonPanel.AddItem(pdData) as PulldownButton;
				if (pd != null)
				{
					pd.LargeImage = bitmapSourceCopy;
					pd.Image = bitmapSourceCopy16;
					pd.AddPushButton(pushButtonDataHeatLossBcc);
					pd.AddPushButton(pushButtonDataParamRulesBcc);
					pd.AddPushButton(pushButtonDataBatchParamsBcc);
					pd.AddPushButton(pushButtonDataRename);
					pd.AddPushButton(pushButtonDataParamCopy);
				}
			}
			catch (Exception ex) { Logger.LogError("Ошибка добавления Параметры", ex); }

			// --- Pulldown: Импорт/Экспорт & Сервер ---
			try
			{
				PulldownButtonData pdData = new PulldownButtonData("pbDataOutput", "Экспорт / Сервер");
				PulldownButton pd = ribbonPanel.AddItem(pdData) as PulldownButton;
				if (pd != null)
				{
					pd.LargeImage = bitmapSourceExcel;
					pd.Image = bitmapSourceExcel16;
					pd.AddPushButton(pushButtonDataExportPdfBcc);
					pd.AddPushButton(pushButtonDataSchedulePackBcc);
					pd.AddPushButton(pushButtonDataPrint);
					pd.AddPushButton(pushButtonDataJson);
					pd.AddPushButton(pushButtonDataExcelExp);
					pd.AddPushButton(pushButtonDataExcelImp);
					pd.AddPushButton(pushButtonDataRevitServerBcc);
				}
			}
			catch (Exception ex) { Logger.LogError("Ошибка добавления Импорт/Экспорт", ex); }

			// 2. Отдельная крупная кнопка "Инфо v2.0.1" с прямо отображаемой версией
			try
			{
				string currentVer = InfoCommand.GetCurrentVersion();
				PushButtonData pushButtonDataInfo = new PushButtonData("cmdInfo", $"Инфо\nv{currentVer}", text4, "BimboClub.InfoCommand")
				{
					ToolTip = $"BimboClub Tools v{currentVer}\nКликните для просмотра сведений о плагине и запуска Менеджера обновлений.",
					LargeImage = bitmapSourceCopy,
					Image = bitmapSourceCopy16
				};
				ribbonPanel.AddItem(pushButtonDataInfo);
			}
			catch (Exception ex) { Logger.LogError("Ошибка добавления Инфо", ex); }

			// --- Wpf-маркер вкладки Revit (ModPlus style) ---
			TrySetTabLogo(application, tabName);

			return Result.Succeeded;
		}

		private void TrySetTabLogo(UIControlledApplication application, string tabName)
		{
			EventHandler<Autodesk.Revit.UI.Events.IdlingEventArgs> handler = null;
			handler = (s, e) =>
			{
				try
				{
					application.Idling -= handler;
				}
				catch { }
				ApplyTabLogoWpf(tabName);
			};

			try
			{
				application.Idling += handler;
			}
			catch { }

			ApplyTabLogoWpf(tabName);
		}

		private void ApplyTabLogoWpf(string tabName)
		{
			try
			{
				Assembly adWinAsm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "AdWindows");
				if (adWinAsm == null) return;

				Type compMgrType = adWinAsm.GetType("Autodesk.Windows.ComponentManager");
				if (compMgrType == null) return;

				PropertyInfo ribbonProp = compMgrType.GetProperty("Ribbon", BindingFlags.Public | BindingFlags.Static);
				if (ribbonProp == null) return;

				object ribbonControl = ribbonProp.GetValue(null);
				if (ribbonControl == null) return;

				if (ribbonControl is System.Windows.Threading.DispatcherObject dispatcherObj)
				{
					dispatcherObj.Dispatcher.BeginInvoke(new Action(() =>
					{
						try
						{
							PropertyInfo tabsProp = ribbonControl.GetType().GetProperty("Tabs");
							if (tabsProp == null) return;

							var tabs = tabsProp.GetValue(ribbonControl) as IEnumerable;
							if (tabs == null) return;

							object targetTab = null;
							foreach (var tab in tabs)
							{
								PropertyInfo titleProp = tab.GetType().GetProperty("Title");
								PropertyInfo idProp = tab.GetType().GetProperty("Id");
								string title = titleProp?.GetValue(tab)?.ToString();
								string id = idProp?.GetValue(tab)?.ToString();

								if (title == tabName || id == tabName || (title != null && title.Contains("BimboClub")))
								{
									targetTab = tab;
									break;
								}
							}

							if (targetTab == null) return;

							var tabButtons = FindVisualChildren(ribbonControl as DependencyObject, "RibbonTabButton");
							foreach (var btn in tabButtons)
							{
								PropertyInfo dataContextProp = btn.GetType().GetProperty("DataContext");
								PropertyInfo contentProp = btn.GetType().GetProperty("Content");

								object dc = dataContextProp?.GetValue(btn);
								object contentVal = contentProp?.GetValue(btn);

								if (dc == targetTab || (contentVal != null && contentVal.ToString().Contains("BimboClub")))
								{
									var stackPanel = new StackPanel
									{
										Orientation = Orientation.Horizontal,
										VerticalAlignment = VerticalAlignment.Center,
										Margin = new Thickness(0)
									};

									var redDot = new System.Windows.Shapes.Ellipse
									{
										Width = 8,
										Height = 8,
										Fill = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFD700")), // Gold marker
										Margin = new Thickness(0, 0, 6, 0),
										VerticalAlignment = VerticalAlignment.Center,
										SnapsToDevicePixels = true
									};

									var textBlock = new TextBlock
									{
										Text = tabName,
										VerticalAlignment = VerticalAlignment.Center,
										FontWeight = FontWeights.Bold,
										FontSize = 11.5,
										Margin = new Thickness(0)
									};

									stackPanel.Children.Add(redDot);
									stackPanel.Children.Add(textBlock);

									contentProp?.SetValue(btn, stackPanel);
									break;
								}
							}
						}
						catch { }
					}), System.Windows.Threading.DispatcherPriority.Background);
				}
			}
			catch { }
		}

		private static List<DependencyObject> FindVisualChildren(DependencyObject depObj, string typeName)
		{
			var results = new List<DependencyObject>();
			if (depObj == null) return results;

			int count = VisualTreeHelper.GetChildrenCount(depObj);
			for (int i = 0; i < count; i++)
			{
				DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
				if (child != null)
				{
					if (child.GetType().Name == typeName)
					{
						results.Add(child);
					}
					results.AddRange(FindVisualChildren(child, typeName));
				}
			}
			return results;
		}

		private BitmapSource LoadImage(string path)
		{
			if (!File.Exists(path)) return null;
			try
			{
				using (FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
				{
					BitmapFrame frame = BitmapDecoder.Create(fileStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
					frame.Freeze();
					return frame;
				}
			}
			catch { return null; }
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
							return assembly;
						}
					}
				}
				if (!string.IsNullOrEmpty(App.AssemblyDir))
				{
					string text = System.IO.Path.Combine(App.AssemblyDir, assemblyName.Name + ".dll");
					if (File.Exists(text))
					{
						return Assembly.LoadFrom(text);
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
