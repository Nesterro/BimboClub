# Инвентарь команд ленты BimboClub

Перечень зарегистрированных точек входа в рабочей ветке 3.0. Классы служат указателями для сверки реализации.

| Команда | Идентификатор | Класс |
| --- | --- | --- |
| 3D схемы | `cmdCreate3DScheme` | `BimboClub.Command` |
| АвтоМарки | `cmdPlaceTags` | `BimboClub.TagCommand` |
| Отверстия в стенах | `cmdWallOpening` | `DuctWallOpenings.DuctWallOpeningsCommand` |
| Отверстия в полах | `cmdFloorOpening` | `DuctWallOpenings.DuctFloorOpeningsCommand` |
| Объединить в стенах | `cmdMergeWallOpenings` | `DuctWallOpenings.MergeWallOpeningsCommand` |
| Объединить в полах | `cmdMergeFloorOpenings` | `DuctWallOpenings.MergeFloorOpeningsCommand` |
| Задание на отверстия | `cmdOpeningsBcc` | `BimboClub.OpeningsCommand` |
| Параметры системы | `cmdParamCopy` | `BimboClub.CopyParamCommand` |
| Печать чертежей | `cmdPrintMaster` | `BimboClub.PrintCommand` |
| Пакет спецификаций | `cmdSchedulePackBcc` | `BimboClub.SchedulePackCommand` |
| Умная трассировка | `cmdRouter` | `BimboClub.RouterCommand` |
| Копировать фильтры | `cmdFilterCopyBcc` | `BimboClub.FilterCopyCommand` |
| Переименовать семейство | `cmdRenameFamily` | `BimboClub.RenameFamilyCommand` |
| Параметры в семействах | `cmdDeleteFamilyParams` | `BimboClub.DeleteFamilyParamsCommand` |
| Расстановка по сети | `cmdNetworkPlacement` | `BimboClub.PlacementCommand` |
| Расстановка крепежа | `cmdHangersBcc` | `BimboClub.HangersCommand` |
| Привязка к уровням | `cmdLevelingBcc` | `BimboClub.LevelingCommand` |
| Импорт XYZ | `cmdPlaceXyzBcc` | `BimboClub.PlaceByXyzCommand` |
| Теплопотери | `cmdHeatLossBcc` | `BimboClub.HeatLossCommand` |
| Редактор правил | `cmdParamRulesBcc` | `BimboClub.ParamRulesCommand` |
| Пакетные параметры | `cmdBatchParamsBcc` | `BimboClub.BatchParamsCommand` |
| Revit Server | `cmdRevitServerBcc` | `BimboClub.RevitServerCommand` |
| Экспорт JSON | `cmdJsonExport` | `LES.Revit.JsonExport.LesJsonExportCommand` |
| Экспорт Excel | `cmdExcelExport` | `BimboClub.ExcelExportCommand` |
| Импорт Excel | `cmdExcelImport` | `BimboClub.ImportExcelTableCommand` |
| Нумерация стояков | `cmdRiserNumbering` | `BimboClub.RiserNumberingCommand` |
| Подключение труб | `cmdPipeConnect` | `BimboClub.PipeConnectCommand` |
| Область подрезки | `cmdCropView` | `BimboClub.CropViewCommand` |
| Допы в спеку | `cmdExtraSchedule` | `BimboClub.ExtraScheduleItems.ExtraScheduleItemsCommand` |
| Соединить коннекторы | `cmdConnectConnectors` | `BimboClub.ConnectConnectorsCommand` |
| Семейство по DXF | `cmdFamilyFromDxf` | `BimboClub.CreateFamilyFromDxfCommand` |
| Пространства по АР | `cmdSpacesFromRooms` | `BimboClub.SpacesFromRooms.SpacesFromRoomsCommand` |
| Расстановка хомутов | `cmdPipeClamps` | `BimboClub.PipeClamps.PipeClampsCommand` |
| Инфо | `cmdInfo` | `BimboClub.InfoCommand` |
| Выровнять по оси | `cmdMepAlign` | `BimboClub.Analogs.MepAlign.MepAlignCommand` |
| Выровнять по высоте | `cmdMepAlignElevation` | `BimboClub.Analogs.MepAlign.MepAlignElevationCommand` |
| Задать расстояние | `cmdMepSetDistance` | `BimboClub.Analogs.MepAlign.MepSetDistanceCommand` |
| Выровнять стояк | `cmdMepAlignVertical` | `BimboClub.Analogs.MepAlign.MepAlignVerticalCommand` |
| Мультипостроение | `cmdMepMultiRoute` | `BimboClub.Analogs.MepAlign.MepMultiRouteCommand` |
| Базовый уровень | `cmdBaseLevel` | `BimboClub.Analogs.BaseLevel.BaseLevelCommand` |
| Заменить шрифт | `cmdFontReplacer` | `BimboClub.Analogs.FontReplacer.FontReplacerCommand` |
| Подрезка 2D вида | `cmdFastCrop2D` | `BimboClub.Analogs.FastViewCut.FastCrop2DCommand` |
| 3D сечение по плану | `cmdFastCrop3D` | `BimboClub.Analogs.FastViewCut.FastCrop3DCommand` |
| Копировать листы | `cmdCopySheets` | `BimboClub.Analogs.CopySheets.CopySheetsCommand` |
| Копировать элементы | `cmdCopyElements` | `BimboClub.Analogs.CopyElements.CopyElementsCommand` |
| Размеры на плане | `cmdPlanDimensions` | `BimboClub.Analogs.InteriorPlanDimensions.PlanDimensionsCommand` |

Команды без регистрации на ленте требуют отдельной сверки использования перед удалением. Старые установки могут обращаться к ним через свои манифесты.
