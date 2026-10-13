[CmdletBinding()]
param(
    [ValidateSet('2021', '2022', '2023', '2024', '2025', '2026')]
    [string[]]$Versions = @('2021', '2022', '2023', '2024', '2025', '2026'),
    [switch]$SkipBuild,
    [string]$AddinsRoot = (Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'Autodesk\Revit\Addins')
)

$ErrorActionPreference = 'Stop'
$baseDir = $PSScriptRoot
if (Get-Process -Name Revit -ErrorAction SilentlyContinue) {
    throw 'Закройте Revit перед установкой, сохранив изменения в моделях.'
}
if (-not $SkipBuild) {
    foreach ($project in @('SystemTravers\BimboClub.csproj', 'DuctSystemParamCopy\DuctSystemParamCopy.csproj')) {
        & dotnet build (Join-Path $baseDir $project) -c Release -p:Platform=x64 --nologo
        if ($LASTEXITCODE -ne 0) { throw "Не удалось собрать $project. Установка отменена." }
    }
}
# Validate every payload before writing to any Revit installation.
$payloads = @{}
foreach ($framework in @($Versions | ForEach-Object { if ([int]$_ -le 2024) { 'net48' } else { 'net8.0-windows' } } | Select-Object -Unique)) {
    $buildDir = Join-Path $baseDir "SystemTravers\bin\x64\Release\$framework"
    $dll = Join-Path $buildDir 'BimboClub.dll'
    $copyDll = Join-Path $baseDir "DuctSystemParamCopy\bin\x64\Release\$framework\DuctSystemParamCopy.dll"
    $manifest = Join-Path $baseDir 'SystemTravers\BimboClub.addin'
    foreach ($required in @($dll, $copyDll, $manifest)) {
        if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
            throw "Не найден обязательный файл: $required. Выполните сборку Release."
        }
    }
    $version = [Diagnostics.FileVersionInfo]::GetVersionInfo($dll).FileVersion
    if (-not $version) { throw "Не удалось прочитать версию $dll" }
    $files = @(Get-ChildItem -LiteralPath $buildDir -Filter '*.dll' |
        Where-Object { $_.Name -notmatch '^RevitAPI(UI)?\.dll$' } |
        Select-Object -ExpandProperty FullName)
    $files += $copyDll
    $files += @(Get-ChildItem -LiteralPath (Join-Path $baseDir 'SystemTravers') -Filter 'icon*.png' | Select-Object -ExpandProperty FullName)
    $payloads[$framework] = @{ Files = $files; Manifest = $manifest; Version = $version }
}
foreach ($year in $Versions) {
    $framework = if ([int]$year -le 2024) { 'net48' } else { 'net8.0-windows' }
    $payload = $payloads[$framework]
    $target = Join-Path $AddinsRoot $year
    $null = New-Item -ItemType Directory -Path $target -Force
    foreach ($file in $payload.Files) { Copy-Item -LiteralPath $file -Destination $target -Force }
    Copy-Item -LiteralPath $payload.Manifest -Destination $target -Force
    Set-Content -LiteralPath (Join-Path $target 'version.txt') -Value $payload.Version -Encoding UTF8
    Write-Host "Revit $year : установлен BimboClub $($payload.Version)" -ForegroundColor Green
}
Write-Host 'Установка BimboClub завершена.' -ForegroundColor Green
