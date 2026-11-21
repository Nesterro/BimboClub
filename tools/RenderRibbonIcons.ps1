param(
    [Parameter(Mandatory=$true)][string]$ProjectRoot,
    [string]$PreviewPath
)

Add-Type -AssemblyName PresentationCore, WindowsBase
$ErrorActionPreference = 'Stop'
$project = (Resolve-Path -LiteralPath $ProjectRoot).Path
$icons = [ordered]@{
    '3d'      = @{ Label = '3D-схемы'; Shape = 'M 16,6 L 25,11 25,21 16,26 7,21 7,11 Z M 7,11 L 16,16 25,11 M 16,16 L 16,26' }
    connect   = @{ Label = 'Соединение'; Shape = 'M 6,12 L 12,12 12,20 6,20 M 26,12 L 20,12 20,20 26,20 M 12,16 L 20,16' }
    copy      = @{ Label = 'Копирование'; Shape = 'M 13,13 L 25,13 25,25 13,25 Z M 9,21 L 7,21 7,7 21,7 21,9' }
    crop      = @{ Label = 'Подрезка'; Shape = 'M 10,6 L 10,22 26,22 M 6,10 L 22,10 22,26' }
    excel     = @{ Label = 'Таблицы'; Shape = 'M 7,7 L 25,7 25,25 7,25 Z M 7,13 L 25,13 M 7,19 L 25,19 M 14,13 L 14,25 M 20,13 L 20,25' }
    filters   = @{ Label = 'Фильтры'; Shape = 'M 6,8 L 26,8 19,16 19,23 13,26 13,16 Z' }
    floor     = @{ Label = 'Перекрытие'; Shape = 'M 6,16 L 16,10 26,16 16,22 Z M 6,16 L 6,20 16,26 26,20 26,16 M 16,6 L 16,16 M 12,12 L 16,16 20,12' }
    json      = @{ Label = 'Данные JSON'; Shape = 'M 13,7 L 11,7 Q 9,7 9,9 L 9,13 Q 9,16 6,16 Q 9,16 9,19 L 9,23 Q 9,25 11,25 L 13,25 M 19,7 L 21,7 Q 23,7 23,9 L 23,13 Q 23,16 26,16 Q 23,16 23,19 L 23,23 Q 23,25 21,25 L 19,25' }
    network   = @{ Label = 'Сеть'; Shape = 'M 11,16 L 20,10 M 11,16 L 20,22 M 6,13 L 11,13 11,19 6,19 Z M 20,7 L 25,7 25,13 20,13 Z M 20,19 L 25,19 25,25 20,25 Z' }
    param     = @{ Label = 'Параметры'; Shape = 'M 6,10 L 11,10 M 15,10 L 26,10 M 6,16 L 19,16 M 23,16 L 26,16 M 6,22 L 11,22 M 15,22 L 26,22 M 13,7 L 13,13 M 21,13 L 21,19 M 13,19 L 13,25' }
    print     = @{ Label = 'Печать'; Shape = 'M 10,12 L 10,6 22,6 22,12 M 10,21 L 6,21 6,12 26,12 26,21 22,21 M 10,18 L 22,18 22,26 10,26 Z M 22,15 L 23,15' }
    rename    = @{ Label = 'Переименование'; Shape = 'M 16,8 L 7,8 7,25 24,25 24,16 M 13,20 L 14,15 23,6 27,10 18,19 Z' }
    riser     = @{ Label = 'Стояки'; Shape = 'M 16,25 L 16,7 M 11,12 L 16,7 21,12 M 9,19 L 23,19 M 9,25 L 23,25' }
    router    = @{ Label = 'Трассировка'; Shape = 'M 6,23 L 13,23 13,10 25,10 M 21,6 L 25,10 21,14' }
    sizer     = @{ Label = 'Размеры'; Shape = 'M 6,22 L 21,7 26,12 11,27 Z M 10,18 L 13,21 M 14,14 L 17,17 M 18,10 L 21,13' }
    specs     = @{ Label = 'Спецификации'; Shape = 'M 8,6 L 24,6 24,26 8,26 Z M 12,11 L 20,11 M 12,16 L 20,16 M 12,21 L 20,21' }
    tags      = @{ Label = 'Марки'; Shape = 'M 7,8 L 18,8 26,16 16,26 7,17 Z M 11,12 L 13,12' }
    wall      = @{ Label = 'Стены'; Shape = 'M 7,7 L 25,7 25,25 7,25 Z M 7,13 L 25,13 M 7,19 L 12,19 M 20,19 L 25,19 M 12,25 L 12,17 20,17 20,25' }
    merge_wall         = @{ Label = 'Слияние в стене'; Shape = 'M 6,6 L 6,26 M 26,6 L 26,26 M 13,11 L 19,11 19,21 13,21 Z M 8,16 L 11,16 M 21,16 L 24,16' }
    merge_floor        = @{ Label = 'Слияние в полу'; Shape = 'M 6,16 L 16,10 26,16 16,22 Z M 6,20 L 16,26 26,20 M 14,16 L 18,16 M 16,14 L 16,18' }
    opening_assignment = @{ Label = 'Задание на отверстия'; Shape = 'M 7,7 L 19,7 24,12 24,25 7,25 Z M 19,7 L 19,12 24,12 M 11,15 L 19,15 19,21 11,21 Z' }
    system_labels      = @{ Label = 'Параметры системы'; Shape = 'M 6,9 L 15,9 15,16 6,16 Z M 17,16 L 22,16 22,20 M 17,20 L 26,20 26,26 17,26 Z M 9,12 L 12,12' }
    schedule_pack      = @{ Label = 'Пакет спецификаций'; Shape = 'M 11,10 L 25,10 25,26 11,26 Z M 7,22 L 7,6 21,6 M 15,15 L 21,15 M 15,20 L 21,20' }
    family_params      = @{ Label = 'Параметры семейств'; Shape = 'M 6,15 L 14,10 23,15 14,20 Z M 6,15 L 6,21 14,26 23,21 M 14,20 L 14,26 M 20,8 L 26,8' }
    placement          = @{ Label = 'Расстановка по сети'; Shape = 'M 6,23 L 26,23 M 8,23 L 8,15 M 16,23 L 16,10 M 24,23 L 24,15 M 6,15 L 10,15 M 14,10 L 18,10 M 22,15 L 26,15' }
    hangers            = @{ Label = 'Расстановка крепежа'; Shape = 'M 6,7 L 26,7 M 10,7 L 10,21 Q 10,25 14,25 L 18,25 Q 22,25 22,21 L 22,7 M 10,19 L 22,19' }
    leveling           = @{ Label = 'Привязка к уровням'; Shape = 'M 7,23 L 25,23 M 18,8 L 18,19 M 14,15 L 18,19 22,15 M 8,8 L 13,8 M 8,13 L 11,13' }
    xyz                = @{ Label = 'Импорт XYZ'; Shape = 'M 10,7 L 10,22 25,22 M 10,22 L 6,26 M 16,10 L 26,10 M 21,7 L 21,13 M 17,17 L 18,17' }
    heat_loss          = @{ Label = 'Теплопотери'; Shape = 'M 7,25 L 7,7 13,7 13,25 Z M 18,9 Q 22,6 26,9 M 18,16 Q 22,13 26,16 M 18,23 Q 22,20 26,23' }
    rules              = @{ Label = 'Редактор правил'; Shape = 'M 11,6 L 21,6 21,13 11,13 Z M 16,13 L 16,18 M 9,18 L 23,18 M 9,18 L 9,24 M 23,18 L 23,24 M 6,24 L 12,24 M 20,24 L 26,24' }
    batch_params       = @{ Label = 'Пакетные параметры'; Shape = 'M 7,9 L 15,9 M 7,16 L 15,16 M 7,23 L 15,23 M 22,11 L 22,21 M 17,16 L 27,16' }
    server             = @{ Label = 'Revit Server'; Shape = 'M 7,7 L 25,7 25,14 7,14 Z M 7,18 L 25,18 25,25 7,25 Z M 11,10.5 L 12,10.5 M 11,21.5 L 12,21.5 M 17,10.5 L 21,10.5 M 17,21.5 L 21,21.5' }
    excel_export       = @{ Label = 'Экспорт Excel'; Shape = 'M 14,7 L 7,7 7,25 16,25 M 7,13 L 13,13 M 7,19 L 13,19 M 15,16 L 25,16 M 21,12 L 25,16 21,20' }
    excel_import       = @{ Label = 'Импорт Excel'; Shape = 'M 18,7 L 25,7 25,25 16,25 M 19,13 L 25,13 M 19,19 L 25,19 M 7,16 L 17,16 M 13,12 L 17,16 13,20' }
    pipe_connect       = @{ Label = 'Подключение труб'; Shape = 'M 6,11 L 16,11 Q 21,11 21,16 L 21,26 M 6,17 L 14,17 Q 15,17 15,18 L 15,26 M 7,9 L 7,19 M 13,25 L 23,25' }
    family_dxf         = @{ Label = 'Семейство по DXF'; Shape = 'M 6,21 L 6,7 17,7 M 6,17 L 10,17 10,12 15,12 M 13,19 L 20,15 26,19 20,23 Z M 20,23 L 20,27 M 13,19 L 13,23 20,27 26,23 26,19' }
    spaces             = @{ Label = 'Пространства по АР'; Shape = 'M 7,7 L 25,7 25,25 15,25 M 7,7 L 7,25 10,25 M 11,17 L 16,17 16,11 M 16,17 L 21,17 21,22' }
    pipe_clamps        = @{ Label = 'Расстановка хомутов'; Shape = 'M 13,6 L 13,11 M 19,6 L 19,11 M 13,21 L 13,26 M 19,21 L 19,26 M 16,11 A 5,5 0 1 0 16,21 A 5,5 0 1 0 16,11 M 21,16 L 26,16 M 6,16 L 11,16' }
    info               = @{ Label = 'Информация'; Shape = 'M 16,6 A 10,10 0 1 0 16,26 A 10,10 0 1 0 16,6 M 16,15 L 16,21 M 16,11 L 16,11.5' }
    align_axis         = @{ Label = 'Выровнять по оси'; Shape = 'M 6,16 L 26,16 M 7,8 L 13,8 13,12 7,12 Z M 19,20 L 25,20 25,24 19,24 Z' }
    align_elevation    = @{ Label = 'Выровнять по высоте'; Shape = 'M 6,21 L 26,21 M 10,15 L 10,7 M 7,10 L 10,7 13,10 M 19,11 L 25,11 M 22,11 L 22,17' }
    set_distance       = @{ Label = 'Задать расстояние'; Shape = 'M 7,8 L 7,24 M 25,8 L 25,24 M 10,16 L 22,16 M 13,13 L 10,16 13,19 M 19,13 L 22,16 19,19' }
    align_vertical     = @{ Label = 'Выровнять стояк'; Shape = 'M 9,7 L 9,25 M 17,8 L 21,8 25,24 21,24 Z M 13,16 L 17,16' }
    multi_route        = @{ Label = 'Мультипостроение'; Shape = 'M 6,11 L 14,11 20,6 26,6 M 6,17 L 16,17 22,12 26,12 M 6,23 L 18,23 24,18 26,18' }
    base_level         = @{ Label = 'Базовый уровень'; Shape = 'M 6,24 L 26,24 M 9,20 L 16,12 23,20 Z M 16,12 L 16,6' }
    font               = @{ Label = 'Заменить шрифт'; Shape = 'M 6,10 L 19,10 M 12.5,10 L 12.5,25 M 8,25 L 17,25 M 20,19 L 26,19 M 23,19 L 23,25' }
    crop_2d            = @{ Label = 'Подрезка 2D вида'; Shape = 'M 9,7 L 7,7 7,25 25,25 25,20 M 12,11 L 20,11 20,19 12,19 Z M 23,7 L 25,7 25,11' }
    crop_3d            = @{ Label = '3D сечение по плану'; Shape = 'M 15,6 L 25,11 25,21 15,26 6,21 6,11 Z M 6,11 L 15,16 25,11 M 15,16 L 15,26 M 10,9 L 21,24' }
    copy_sheets        = @{ Label = 'Копировать листы'; Shape = 'M 6,21 L 6,6 17,6 M 10,10 L 21,10 26,15 26,26 10,26 Z M 21,10 L 21,15 26,15 M 14,17 L 17,17 M 14,22 L 22,22' }
    views              = @{ Label = 'Схемы и виды'; Shape = 'M 6,16 Q 16,4 26,16 Q 16,28 6,16 Z M 16,12 A 4,4 0 1 0 16,20 A 4,4 0 1 0 16,12' }
    openings           = @{ Label = 'Отверстия'; Shape = 'M 7,7 L 25,7 25,25 7,25 Z M 16,11 A 5,5 0 1 0 16,21 A 5,5 0 1 0 16,11' }
    align_menu         = @{ Label = 'MEP выравнивание'; Shape = 'M 11,6 L 11,26 M 11,8 L 25,8 25,12 11,12 M 11,15 L 20,15 20,19 11,19 M 11,22 L 23,22 23,26 11,26' }
    cut_menu           = @{ Label = 'Быстрая подрезка'; Shape = 'M 12,7 L 7,7 7,12 M 20,7 L 25,7 25,12 M 25,20 L 25,25 20,25 M 12,25 L 7,25 7,20 M 10,22 L 22,10' }
}
function Brush([string]$color) {
    [Windows.Media.SolidColorBrush]::new([Windows.Media.ColorConverter]::ConvertFromString($color))
}
function RenderPng($visual, [int]$width, [int]$height, [string]$destination) {
    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new($width,$height,96,96,[Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [IO.File]::Create($destination)
    try { $encoder.Save($stream) }
    finally { $stream.Dispose() }
}
function DrawIcon($drawing, [string]$shape, [double]$x, [double]$y, [int]$size) {
    $drawing.PushTransform([Windows.Media.TranslateTransform]::new($x,$y))
    $drawing.PushTransform([Windows.Media.ScaleTransform]::new($size / 32,$size / 32))
    $tile = Brush '#412B36'
    $outline = [Windows.Media.Pen]::new((Brush '#B97D92'),1.25)
    $stroke = [Windows.Media.Pen]::new((Brush '#FADCE6'),2.15)
    $stroke.StartLineCap = $stroke.EndLineCap = [Windows.Media.PenLineCap]::Round
    $stroke.LineJoin = [Windows.Media.PenLineJoin]::Round
    $drawing.DrawRoundedRectangle($tile,$outline,[Windows.Rect]::new(1.25,1.25,29.5,29.5),7.5,7.5)
    $drawing.DrawGeometry($null,$stroke,[Windows.Media.Geometry]::Parse($shape))
    $drawing.Pop()
    $drawing.Pop()
}
foreach ($key in $icons.Keys) {
    foreach ($size in 16,32) {
        $suffix = if ($size -eq 16) { '_16' } else { '' }
        $visual = [Windows.Media.DrawingVisual]::new()
        $drawing = $visual.RenderOpen()
        try { DrawIcon $drawing $icons[$key].Shape 0 0 $size }
        finally { $drawing.Close() }
        RenderPng $visual $size $size (Join-Path $project "SystemTravers/icon_$key$suffix.png")
    }
}

if ($PreviewPath) {
    $columns = 6
    $width = 1000
    $height = 100 + [int][Math]::Ceiling($icons.Count / $columns) * 75
    $visual = [Windows.Media.DrawingVisual]::new()
    $drawing = $visual.RenderOpen()
    $font = [Windows.Media.Typeface]::new('Segoe UI')
    $culture = [Globalization.CultureInfo]::GetCultureInfo('ru-RU')
    try {
        $drawing.DrawRectangle((Brush '#19191E'),$null,[Windows.Rect]::new(0,0,$width,$height))
        $drawing.DrawRoundedRectangle((Brush '#24242B'),$null,[Windows.Rect]::new(16,14,($width-32),($height-28)),12,12)
        $title = [Windows.Media.FormattedText]::new('BimboClub · Пиктограммы ленты',
            $culture,[Windows.FlowDirection]::LeftToRight,$font,22,(Brush '#F5EDF1'),1)
        $drawing.DrawText($title,[Windows.Point]::new(36,29))
        $index = 0
        foreach ($icon in $icons.Values) {
            $column = $index % $columns
            $row = [Math]::Floor($index / $columns)
            $x = 36 + $column * 155
            $y = 73 + $row * 75
            DrawIcon $drawing $icon.Shape $x $y 32
            $drawing.DrawRoundedRectangle((Brush '#F0EAED'),$null,[Windows.Rect]::new(($x+42),$y,28,32),5,5)
            DrawIcon $drawing $icon.Shape ($x+48) ($y+8) 16
            $caption = [Windows.Media.FormattedText]::new($icon.Label,$culture,
                [Windows.FlowDirection]::LeftToRight,$font,13,(Brush '#E3D9E0'),1)
            $drawing.DrawText($caption,[Windows.Point]::new($x,($y+37)))
            $index++
        }
    } finally { $drawing.Close() }
    RenderPng $visual $width $height $PreviewPath
}
Write-Output "Rendered $($icons.Count) ribbon symbols at 16 and 32 px."
