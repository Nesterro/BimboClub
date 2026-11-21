param(
    [Parameter(Mandatory=$true)][string]$ProjectRoot,
    [switch]$ManagerOnly
)
Add-Type -AssemblyName PresentationCore, WindowsBase
$ErrorActionPreference = 'Stop'
$project = (Resolve-Path -LiteralPath $ProjectRoot).Path
function Get-BimboClubMark([int]$size, [switch]$ReadableLetters) {
    $visual = [Windows.Media.DrawingVisual]::new()
    $drawing = $visual.RenderOpen()
    try {
        $drawing.PushTransform([Windows.Media.ScaleTransform]::new($size / 64, $size / 64))
        $tile = [Windows.Media.SolidColorBrush]::new([Windows.Media.ColorConverter]::ConvertFromString('#412B36'))
        $ink = [Windows.Media.SolidColorBrush]::new([Windows.Media.ColorConverter]::ConvertFromString('#FADCE6'))
        $drawing.DrawRoundedRectangle($tile, $null, [Windows.Rect]::new(1, 1, 62, 62), 16, 16)
        if ($ReadableLetters) {
            $face = [Windows.Media.Typeface]::new([Windows.Media.FontFamily]::new('Segoe UI'),
                [Windows.FontStyles]::Normal, [Windows.FontWeights]::Bold, [Windows.FontStretches]::Normal)
            $text = [Windows.Media.FormattedText]::new('BC', [Globalization.CultureInfo]::InvariantCulture,
                [Windows.FlowDirection]::LeftToRight, $face, 37, $ink, 1)
            $letters = $text.BuildGeometry([Windows.Point]::new(0, 0))
            $bounds = $letters.Bounds
            $drawing.PushTransform([Windows.Media.TranslateTransform]::new(
                (32 - $bounds.X - $bounds.Width / 2), (32 - $bounds.Y - $bounds.Height / 2)))
            $drawing.DrawGeometry($ink, $null, $letters)
            $drawing.Pop()
        } else {
            $dot = [Windows.Media.SolidColorBrush]::new([Windows.Media.ColorConverter]::ConvertFromString('#C17E96'))
            $pen = [Windows.Media.Pen]::new($ink, 3.8)
            $pen.StartLineCap = $pen.EndLineCap = [Windows.Media.PenLineCap]::Round
            $pen.LineJoin = [Windows.Media.PenLineJoin]::Round
            $drawing.DrawGeometry($null, $pen, [Windows.Media.Geometry]::Parse('M 16,43 L 16,21 L 24,21 C 31,21 32,31 23,31 L 17,31 M 23,31 C 34,31 33,43 24,43 Z M 47,24 C 37,17 32,28 34,35 C 35,42 41,46 47,41'))
            $drawing.DrawEllipse($dot, $null, [Windows.Point]::new(49, 17), 3, 3)
        }
        $drawing.Pop()
    } finally { $drawing.Close() }
    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new($size,$size,96,96,[Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $memory = [IO.MemoryStream]::new()
    try { $encoder.Save($memory); return ,$memory.ToArray() }
    finally { $memory.Dispose() }
}
$sizes = @(16, 24, 32, 48, 64, 256)
function Write-Icon([hashtable]$frames, [string[]]$destinations) {
    $bytes = [IO.MemoryStream]::new()
    $writer = [IO.BinaryWriter]::new($bytes)
    try {
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]$sizes.Count)
        $offset = 6 + 16 * $sizes.Count
        foreach ($size in $sizes) {
            $dimension = if ($size -eq 256) { 0 } else { $size }
            $writer.Write([byte]$dimension)
            $writer.Write([byte]$dimension)
            $writer.Write([uint16]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]32)
            $writer.Write([uint32]$frames[$size].Length)
            $writer.Write([uint32]$offset)
            $offset += $frames[$size].Length
        }
        foreach ($size in $sizes) { $writer.Write([byte[]]$frames[$size]) }
        foreach ($destination in $destinations) {
            [IO.File]::WriteAllBytes((Join-Path $project $destination), $bytes.ToArray())
        }
    } finally { $writer.Dispose(); $bytes.Dispose() }
}

$managerFrames = @{}
foreach ($size in $sizes) { $managerFrames[$size] = Get-BimboClubMark $size -ReadableLetters }
[IO.File]::WriteAllBytes((Join-Path $project 'BimboClubManager/icon_manager.png'), $managerFrames[64])
Write-Icon $managerFrames @('BimboClubManager/icon_manager.ico')

if (!$ManagerOnly) {
    $frames = @{}
    foreach ($size in $sizes) { $frames[$size] = Get-BimboClubMark $size }
    [IO.File]::WriteAllBytes((Join-Path $project 'SystemTravers/icon32.png'), $frames[32])
    Write-Icon $frames @('RevitServerManager/icon_server.ico','RevitServerManagerInstaller/icon_setup.ico')
}
