$appData = [Environment]::GetFolderPath('ApplicationData')
$commonAppData = [Environment]::GetFolderPath('CommonApplicationData')
$baseDir = Get-Location

$versions = @('2021', '2022', '2023', '2024', '2025', '2026')

foreach ($ver in $versions) {
    # 1. User AppData target
    $targetDir = Join-Path $appData "Autodesk\Revit\Addins\$ver"
    $null = New-Item -ItemType Directory -Path $targetDir -Force

    if ([int]$ver -le 2024) {
        $sourceDll = Join-Path $baseDir "SystemTravers\bin\Release\net48\BimboClub.dll"
        if (-not (Test-Path $sourceDll)) { $sourceDll = Join-Path $baseDir "SystemTravers\bin\Debug\net48\BimboClub.dll" }
    } else {
        $sourceDll = Join-Path $baseDir "SystemTravers\bin\Release\net8.0-windows\BimboClub.dll"
        if (-not (Test-Path $sourceDll)) { $sourceDll = Join-Path $baseDir "SystemTravers\bin\Debug\net8.0-windows\BimboClub.dll" }
    }

    if (Test-Path $sourceDll) {
        Copy-Item -Path $sourceDll -Destination $targetDir -Force
        Write-Host "Скопировано: $sourceDll -> $targetDir" -ForegroundColor Green
    } else {
        Write-Host "Не найден DLL по пути: $sourceDll" -ForegroundColor Red
    }

    # Copy .addin file
    $addinFile = Join-Path $baseDir "SystemTravers\BimboClub.addin"
    if (Test-Path $addinFile) {
        Copy-Item -Path $addinFile -Destination $targetDir -Force
    }

    # Copy icons
    Get-ChildItem -Path (Join-Path $baseDir "SystemTravers") -Filter "icon*.png" | ForEach-Object {
        Copy-Item -Path $_.FullName -Destination $targetDir -Force
    }

    # 2. ProgramData target as well
    $progDataDir = Join-Path $commonAppData "Autodesk\Revit\Addins\$ver"
    if (Test-Path $progDataDir) {
        if (Test-Path $sourceDll) { Copy-Item -Path $sourceDll -Destination $progDataDir -Force }
        if (Test-Path $addinFile) { Copy-Item -Path $addinFile -Destination $progDataDir -Force }
        Get-ChildItem -Path (Join-Path $baseDir "SystemTravers") -Filter "icon*.png" | ForEach-Object {
            Copy-Item -Path $_.FullName -Destination $progDataDir -Force
        }
        Write-Host "Обновлено в ProgramData: $progDataDir" -ForegroundColor Cyan
    }
}

Write-Host "Установка BimboClub завершена!" -ForegroundColor Yellow
