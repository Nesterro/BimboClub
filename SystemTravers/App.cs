using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
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
		private LiveRibbonUpdate _ribbon;
		private bool _ribbonFailed;

		public Result OnStartup(UIControlledApplication application)
		{
			Assembly assembly = typeof(App).Assembly;
			string text4 = assembly.Location;
			string text = System.IO.Path.GetDirectoryName(text4);
			AssemblyDir = text;
			AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
			_ribbon = new LiveRibbonUpdate(application, text4, assembly.GetName());

			const string tabName = "BimboClub";
			try { application.CreateRibbonTab(tabName); }
			catch (Autodesk.Revit.Exceptions.ArgumentException) { }
			RibbonPanel ribbonPanel = _ribbon.CreatePanel(tabName, "BimboClub Tools");

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

			PushButtonData pushButtonDataPrint = new PushButtonData("cmdPrintMaster", "Печать чертежей", text4, "BimboClub.PrintCommand")
			{
				ToolTip = "Пакетный экспорт листов в PDF и DWG с автоопределением форматов рамок, умным именованием и исправлением кавычек ГОСТ."
			};

			PushButtonData pushButtonDataSchedulePackBcc = new PushButtonData("cmdSchedulePackBcc", "Пакет спецификаций", text4, "BimboClub.SchedulePackCommand")
			{
				ToolTip = "Пакетный экспорт спецификаций проекта в CSV/TXT файлы."
			};

			PushButtonData pushButtonDataRouter = new PushButtonData("cmdRouter", "Умная трассировка", text4, "BimboClub.RouterCommand")
			{
				ToolTip = "Автоматически прокладывает обходы («утки») для воздуховодов и труб в местах пересечения со строительными конструкциями."
			};

			PushButtonData pushButtonDataFilterCopyBcc = new PushButtonData("cmdFilterCopyBcc", "Копировать фильтры", text4, "BimboClub.FilterCopyCommand")
			{
				ToolTip = "Копирует фильтры между видами и шаблонами текущего проекта, включая графику, видимость и состояние «Включён»."
			};

			PushButtonData pushButtonDataRename = new PushButtonData("cmdRenameFamily", "Переименовать семейство", text4, "BimboClub.RenameFamilyCommand")
			{
				ToolTip = "Переименовывает выбранное на виде семейство или позволяет выбрать любое загружаемое семейство из списка в проекте."
			};

			PushButtonData pushButtonDataDeleteFamilyParams = new PushButtonData("cmdDeleteFamilyParams", "Параметры в семействах", text4, "BimboClub.DeleteFamilyParamsCommand")
			{
				ToolTip = "Пакетное удаление выбранных параметров (семейных и общих) из загружаемых семейств проекта по категориям или списку."
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

			PushButtonData pushButtonDataExtraSchedule = new PushButtonData("cmdExtraSchedule", "Допы в\nспеку", text4, "BimboClub.ExtraScheduleItems.ExtraScheduleItemsCommand")
			{
				ToolTip = "Добавление немоделируемых элементов (крепеж, герметики, изоляция, кабели, краска и др.) в спецификации Revit (ADSK) без создания 3D геометрии."
			};

			PushButtonData pushButtonDataConnectConnectors = new PushButtonData("cmdConnectConnectors", "Соединить\nконнекторы", text4, "BimboClub.ConnectConnectorsCommand")
			{
				ToolTip = "Циклическое соединение двух коннекторов (или MEP кривых) с поворотом и перемещением первого выбранного элемента."
			};

			PushButtonData pushButtonDataFamilyDxf = new PushButtonData("cmdFamilyFromDxf", "Семейство\nпо DXF", text4, "BimboClub.CreateFamilyFromDxfCommand")
			{
				ToolTip = "Создание нового семейства на основе DXF файла (шаблон 'Метрическая система, типовая модель'), загрузка в проект и мгновенная активация размещения."
			};

			PushButtonData pushButtonDataSpacesFromRooms = new PushButtonData("cmdSpacesFromRooms", "Пространства\nпо АР", text4, "BimboClub.SpacesFromRooms.SpacesFromRoomsCommand")
			{
				ToolTip = "Автоматическая расстановка пространств MEP по помещениям из связанного файла АР с переносом имен, номеров и высотных отметок."
			};

			SetRibbonIcon(pushButtonData3D, "3d");
			SetRibbonIcon(pushButtonDataTags, "tags");
			SetRibbonIcon(pushButtonDataWallOp, "wall");
			SetRibbonIcon(pushButtonDataFloorOp, "floor");
			SetRibbonIcon(pushButtonDataMergeWallOp, "merge_wall");
			SetRibbonIcon(pushButtonDataMergeFloorOp, "merge_floor");
			SetRibbonIcon(pushButtonDataOpeningsBcc, "opening_assignment");
			SetRibbonIcon(pushButtonDataParamCopy, "system_labels");
			SetRibbonIcon(pushButtonDataPrint, "print");
			SetRibbonIcon(pushButtonDataSchedulePackBcc, "schedule_pack");
			SetRibbonIcon(pushButtonDataRouter, "router");
			SetRibbonIcon(pushButtonDataFilterCopyBcc, "filters");
			SetRibbonIcon(pushButtonDataRename, "rename");
			SetRibbonIcon(pushButtonDataDeleteFamilyParams, "family_params");
			SetRibbonIcon(pushButtonDataPlacement, "placement");
			SetRibbonIcon(pushButtonDataHangersBcc, "hangers");
			SetRibbonIcon(pushButtonDataLevelingBcc, "leveling");
			SetRibbonIcon(pushButtonDataPlaceXyzBcc, "xyz");
			SetRibbonIcon(pushButtonDataHeatLossBcc, "heat_loss");
			SetRibbonIcon(pushButtonDataParamRulesBcc, "rules");
			SetRibbonIcon(pushButtonDataBatchParamsBcc, "batch_params");
			SetRibbonIcon(pushButtonDataRevitServerBcc, "server");
			SetRibbonIcon(pushButtonDataJson, "json");
			SetRibbonIcon(pushButtonDataExcelExp, "excel_export");
			SetRibbonIcon(pushButtonDataExcelImp, "excel_import");
			SetRibbonIcon(pushButtonDataRiser, "riser");
			SetRibbonIcon(pushButtonDataConnect, "pipe_connect");
			SetRibbonIcon(pushButtonDataCrop, "crop");
			SetRibbonIcon(pushButtonDataExtraSchedule, "specs");
			SetRibbonIcon(pushButtonDataConnectConnectors, "connect");
			SetRibbonIcon(pushButtonDataFamilyDxf, "family_dxf");
			SetRibbonIcon(pushButtonDataSpacesFromRooms, "spaces");

			// --- Pulldown: Схемы и Виды ---
			try
			{
				PulldownButtonData pdData = new PulldownButtonData("pbViewsAndSchemes", "Схемы и Виды");
				SetRibbonIcon(pdData, "views");
				PulldownButton pd = ribbonPanel.AddItem(pdData) as PulldownButton;
				if (pd != null)
				{
					pd.AddPushButton(pushButtonData3D);
					pd.AddPushButton(pushButtonDataCrop);
					pd.AddPushButton(pushButtonDataFilterCopyBcc);
					pd.AddPushButton(pushButtonDataTags);
				}
			}
			catch (Exception ex) { _ribbonFailed = true; Logger.LogError("Ошибка добавления Схемы и Виды", ex); }

			// --- Pulldown: Отверстия ---
			try
			{
				PulldownButtonData pdData = new PulldownButtonData("pbOpenings", "Отверстия");
				SetRibbonIcon(pdData, "openings");
				PulldownButton pd = ribbonPanel.AddItem(pdData) as PulldownButton;
				if (pd != null)
				{
					pd.AddPushButton(pushButtonDataOpeningsBcc);
					pd.AddPushButton(pushButtonDataWallOp);
					pd.AddPushButton(pushButtonDataFloorOp);
					pd.AddPushButton(pushButtonDataMergeWallOp);
					pd.AddPushButton(pushButtonDataMergeFloorOp);
				}
			}
			catch (Exception ex) { _ribbonFailed = true; Logger.LogError("Ошибка добавления Отверстия", ex); }

			// --- Pulldown: Сети MEP ---
			try
			{
				PulldownButtonData pdData = new PulldownButtonData("pbMepNetworks", "Сети MEP");
				SetRibbonIcon(pdData, "network");
				PulldownButton pd = ribbonPanel.AddItem(pdData) as PulldownButton;
				if (pd != null)
				{
					pd.AddPushButton(pushButtonDataSpacesFromRooms);
					pd.AddPushButton(pushButtonDataRouter);
					pd.AddPushButton(pushButtonDataHangersBcc);
					pd.AddPushButton(pushButtonDataRiser);
					pd.AddPushButton(pushButtonDataConnect);
					pd.AddPushButton(pushButtonDataConnectConnectors);
					pd.AddPushButton(pushButtonDataPlacement);
					pd.AddPushButton(pushButtonDataLevelingBcc);
					pd.AddPushButton(pushButtonDataPlaceXyzBcc);
				}
			}
			catch (Exception ex) { _ribbonFailed = true; Logger.LogError("Ошибка добавления Сети MEP", ex); }

			// 1. Отдельная крупная кнопка "Расстановка хомутов"
			try
			{
				PushButtonData pushButtonDataPipeClamps = new PushButtonData("cmdPipeClamps", "Расстановка\nхомутов", text4, "BimboClub.PipeClamps.PipeClampsCommand")
				{
					ToolTip = "Автоматическая расстановка хомутов по вертикальным трубопроводам с выбором типа, шага и переносом параметров стояка."
				};
				SetRibbonIcon(pushButtonDataPipeClamps, "pipe_clamps");
				ribbonPanel.AddItem(pushButtonDataPipeClamps);
			}
			catch (Exception ex) { _ribbonFailed = true; Logger.LogError("Ошибка добавления Расстановка хомутов", ex); }

			// --- Pulldown: Параметры & Теплопотери ---
			try
			{
				PulldownButtonData pdData = new PulldownButtonData("pbParams", "Параметры");
				SetRibbonIcon(pdData, "param");
				PulldownButton pd = ribbonPanel.AddItem(pdData) as PulldownButton;
				if (pd != null)
				{
					pd.AddPushButton(pushButtonDataHeatLossBcc);
					pd.AddPushButton(pushButtonDataParamRulesBcc);
					pd.AddPushButton(pushButtonDataBatchParamsBcc);
					pd.AddPushButton(pushButtonDataDeleteFamilyParams);
					pd.AddPushButton(pushButtonDataRename);
					pd.AddPushButton(pushButtonDataParamCopy);
				}
			}
			catch (Exception ex) { _ribbonFailed = true; Logger.LogError("Ошибка добавления Параметры", ex); }

			// --- Pulldown: Импорт/Экспорт & Сервер ---
			try
			{
				PulldownButtonData pdData = new PulldownButtonData("pbDataOutput", "Экспорт / Сервер");
				SetRibbonIcon(pdData, "excel");
				PulldownButton pd = ribbonPanel.AddItem(pdData) as PulldownButton;
				if (pd != null)
				{
					pd.AddPushButton(pushButtonDataExtraSchedule);
					pd.AddPushButton(pushButtonDataPrint);
					pd.AddPushButton(pushButtonDataSchedulePackBcc);
					pd.AddPushButton(pushButtonDataJson);
					pd.AddPushButton(pushButtonDataExcelExp);
					pd.AddPushButton(pushButtonDataExcelImp);
					pd.AddPushButton(pushButtonDataRevitServerBcc);
				}
			}
			catch (Exception ex) { _ribbonFailed = true; Logger.LogError("Ошибка добавления Импорт/Экспорт", ex); }

			// 3. Отдельная крупная кнопка "Допы в спеку"
			// 4. Отдельная крупная кнопка "Семейство по DXF"
			try
			{
				ribbonPanel.AddItem(pushButtonDataFamilyDxf);
			}
			catch (Exception ex) { _ribbonFailed = true; Logger.LogError("Ошибка добавления Семейство по DXF", ex); }

			// 5. Отдельная крупная кнопка "Пространства по АР"
			// 2. Отдельная крупная кнопка "Инфо vX.X.X" с прямо отображаемой версией
			try
			{
				string currentVer = InfoCommand.GetCurrentVersion();
				PushButtonData pushButtonDataInfo = new PushButtonData("cmdInfo", $"Инфо\nv{currentVer}", text4, "BimboClub.InfoCommand")
				{
					ToolTip = $"BimboClub Tools v{currentVer}\nКликните для просмотра сведений о плагине и запуска Менеджера обновлений."
				};
				SetRibbonIcon(pushButtonDataInfo, "info");
				ribbonPanel.AddItem(pushButtonDataInfo);
			}
			catch (Exception ex) { _ribbonFailed = true; Logger.LogError("Ошибка добавления Инфо", ex); }

			// --- Панель: "Аналоги" ---
			try
			{
				RibbonPanel analogsPanel = _ribbon.CreatePanel(tabName, "Работа с моделью");

				// 1. Pulldown: MEP выравнивание
				PulldownButtonData pdMepAlignData = new PulldownButtonData("pbMepAlign", "MEP\nвыравнивание");
				SetRibbonIcon(pdMepAlignData, "align_menu");
				PulldownButton pdMepAlign = analogsPanel.AddItem(pdMepAlignData) as PulldownButton;
				if (pdMepAlign != null)
				{

					// Команда 1: Выровнять по оси
					PushButtonData btnAlign = new PushButtonData("cmdMepAlign", "Выровнять по оси", text4, "BimboClub.Analogs.MepAlign.MepAlignCommand")
					{
						ToolTip = "Соосное выравнивание труб, воздуховодов, лотков и фитингов (относительно ближайшего коннектора)."
					};
					SetRibbonIcon(btnAlign, "align_axis");
					pdMepAlign.AddPushButton(btnAlign);

					// Команда 2: Выровнять по высоте
					PushButtonData btnAlignElev = new PushButtonData("cmdMepAlignElevation", "Выровнять по высоте", text4, "BimboClub.Analogs.MepAlign.MepAlignElevationCommand")
					{
						ToolTip = "Перемещение выравниваемого элемента по оси Z до пересечения траектории с опорным элементом."
					};
					SetRibbonIcon(btnAlignElev, "align_elevation");
					pdMepAlign.AddPushButton(btnAlignElev);

					// Команда 3: Задать расстояние
					PushButtonData btnSetDist = new PushButtonData("cmdMepSetDistance", "Задать расстояние", text4, "BimboClub.Analogs.MepAlign.MepSetDistanceCommand")
					{
						ToolTip = "Установка точного расстояния между MEP кривыми по горизонтали или вертикали (по осям или по граням с учетом изоляции)."
					};
					SetRibbonIcon(btnSetDist, "set_distance");
					pdMepAlign.AddPushButton(btnSetDist);

					// Команда 4: Выровнять стояк вертикально
					PushButtonData btnAlignVert = new PushButtonData("cmdMepAlignVertical", "Выровнять стояк", text4, "BimboClub.Analogs.MepAlign.MepAlignVerticalCommand")
					{
						ToolTip = "Выравнивание стояка строго по вертикали (верхняя точка проецируется точно над нижней)."
					};
					SetRibbonIcon(btnAlignVert, "align_vertical");
					pdMepAlign.AddPushButton(btnAlignVert);

					// Команда 5: Мультипостроение
					PushButtonData btnMultiRoute = new PushButtonData("cmdMepMultiRoute", "Мультипостроение", text4, "BimboClub.Analogs.MepAlign.MepMultiRouteCommand")
					{
						ToolTip = "Параллельная прокладка нескольких трасс с фиксацией углов и авто-отводами."
					};
					SetRibbonIcon(btnMultiRoute, "multi_route");
					pdMepAlign.AddPushButton(btnMultiRoute);
				}

				// 2. Крупная кнопка: Базовый уровень
				PushButtonData btnBaseLevel = new PushButtonData("cmdBaseLevel", "Базовый\nуровень", text4, "BimboClub.Analogs.BaseLevel.BaseLevelCommand")
				{
					ToolTip = "Пакетная перепривязка элементов к базовым и верхним уровням без физического смещения геометрии в пространстве."
				};
				SetRibbonIcon(btnBaseLevel, "base_level");
				analogsPanel.AddItem(btnBaseLevel);

				// 3. Крупная кнопка: Заменить шрифт
				PushButtonData btnFontReplacer = new PushButtonData("cmdFontReplacer", "Заменить\nшрифт", text4, "BimboClub.Analogs.FontReplacer.FontReplacerCommand")
				{
					ToolTip = "Пакетная замена шрифта и свойств текста в размерных стилях, текстовых стилях, спецификациях и семействах марок."
				};
				SetRibbonIcon(btnFontReplacer, "font");
				analogsPanel.AddItem(btnFontReplacer);

				// 4. Pulldown: Быстрая подрезка
				PulldownButtonData pdFastCutData = new PulldownButtonData("pbFastViewCut", "Быстрая\nподрезка");
				SetRibbonIcon(pdFastCutData, "cut_menu");
				PulldownButton pdFastCut = analogsPanel.AddItem(pdFastCutData) as PulldownButton;
				if (pdFastCut != null)
				{

					PushButtonData btnCrop2D = new PushButtonData("cmdFastCrop2D", "Подрезка 2D вида", text4, "BimboClub.Analogs.FastViewCut.FastCrop2DCommand")
					{
						ToolTip = "Быстрая подрезка активного вида (плана, фасада, разреза, узла) по двум угловым точкам."
					};
					SetRibbonIcon(btnCrop2D, "crop_2d");
					pdFastCut.AddPushButton(btnCrop2D);

					PushButtonData btnCrop3D = new PushButtonData("cmdFastCrop3D", "3D сечение по плану", text4, "BimboClub.Analogs.FastViewCut.FastCrop3DCommand")
					{
						ToolTip = "Создание или обновление 3D-вида с границей сечения (секущим кубом) по двум точкам на плане этажа."
					};
					SetRibbonIcon(btnCrop3D, "crop_3d");
					pdFastCut.AddPushButton(btnCrop3D);
				}

				// 5. Крупная кнопка: Копировать листы
				PushButtonData btnCopySheets = new PushButtonData("cmdCopySheets", "Копировать\nлисты", text4, "BimboClub.Analogs.CopySheets.CopySheetsCommand")
				{
					ToolTip = "Копирование листов, чертёжных видов, легенд, спецификаций и рамок между открытыми документами проекта."
				};
				SetRibbonIcon(btnCopySheets, "copy_sheets");
				analogsPanel.AddItem(btnCopySheets);

				// 6. Крупная кнопка: Копировать элементы
				PushButtonData btnCopyElements = new PushButtonData("cmdCopyElements", "Копировать\nэлементы", text4, "BimboClub.Analogs.CopyElements.CopyElementsCommand")
				{
					ToolTip = "Копирование стандартов проекта, типоразмеров, фильтров, спецификаций и элементов модели в открытые документы."
				};
				SetRibbonIcon(btnCopyElements, "copy");
				analogsPanel.AddItem(btnCopyElements);

				// 7. Крупная кнопка: Размеры на плане
				PushButtonData btnPlanDimensions = new PushButtonData("cmdPlanDimensions", "Размеры\nна плане", text4, "BimboClub.Analogs.InteriorPlanDimensions.PlanDimensionsCommand")
				{
					ToolTip = "Автоматическая расстановка цепочек размеров стен, проёмов, толщин и привязок колонн к осям на плане этажа."
				};
				SetRibbonIcon(btnPlanDimensions, "sizer");
				analogsPanel.AddItem(btnPlanDimensions);
			}
			catch (Exception ex) { _ribbonFailed = true; Logger.LogError("Ошибка создания панели Аналоги", ex); }

			if (!_ribbonFailed)
			{
				_ribbon.Start();
				RibbonTabMarker.Schedule(tabName);
			}
			else
			{
				_ribbon.Stop();
				AppDomain.CurrentDomain.AssemblyResolve -= OnAssemblyResolve;
			}
			return _ribbonFailed ? Result.Failed : Result.Succeeded;
		}

		private void SetRibbonIcon(ButtonData button, string icon)
		{
			button.LargeImage = LoadImage(System.IO.Path.Combine(AssemblyDir, $"icon_{icon}.png"));
			button.Image = LoadImage(System.IO.Path.Combine(AssemblyDir, $"icon_{icon}_16.png"));
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
			_ribbon?.Stop();
			AppDomain.CurrentDomain.AssemblyResolve -= OnAssemblyResolve;
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
				if (args.RequestingAssembly == typeof(App).Assembly && !string.IsNullOrEmpty(App.AssemblyDir))
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
