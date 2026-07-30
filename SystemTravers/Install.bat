@echo off
chcp 65001 > nul
echo ======================================================
echo   BimboClub Tools — Автоматическая установка плагина
echo ======================================================
echo.

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
    "$revitFolders = @('2021', '2022', '2023', '2024', '2025', '2026'); ^
     $appData = [Environment]::GetFolderPath('ApplicationData'); ^
     $baseDir = Get-Location; ^
     $copiedCount = 0; ^
     foreach ($ver in $revitFolders) { ^
         $targetDir = Join-Path $appData \"Autodesk\Revit\Addins\$ver\"; ^
         if (-not (Test-Path $targetDir)) { continue; } ^
         if ([int]$ver -le 2024) { ^
             $sourceDll = Join-Path $baseDir \"bin\Release\net48\BimboClub.dll\"; ^
             if (-not (Test-Path $sourceDll)) { $sourceDll = Join-Path $baseDir \"bin\Debug\net48\BimboClub.dll\"; } ^
         } else { ^
             $sourceDll = Join-Path $baseDir \"bin\Release\net8.0-windows\BimboClub.dll\"; ^
             if (-not (Test-Path $sourceDll)) { $sourceDll = Join-Path $baseDir \"bin\Debug\net8.0-windows\BimboClub.dll\"; } ^
         } ^
         if (-not (Test-Path $sourceDll)) { ^
             Write-Host \"[ОШИБКА] Сборка BimboClub.dll не найдена! Сначала запустите 'dotnet build -c Release'.\" -ForegroundColor Red; ^
             exit; ^
         } ^
         Copy-Item -Path $sourceDll -Destination $targetDir -Force; ^
         $sourceAddin = Join-Path $baseDir \"BimboClub.addin\"; ^
         if (Test-Path $sourceAddin) { Copy-Item -Path $sourceAddin -Destination $targetDir -Force; } ^
         $sourceIcon = Join-Path $baseDir \"icon32.png\"; ^
         if (Test-Path $sourceIcon) { Copy-Item -Path $sourceIcon -Destination $targetDir -Force; } ^
         $icons = @('icon_3d.png', 'icon_tags.png', 'icon_wall.png', 'icon_floor.png', 'icon_copy.png', 'icon_sizer.png', 'icon_print.png', 'icon_specs.png', 'icon_router.png', 'icon_filters.png', 'icon_rename.png', 'icon_network.png', 'icon_param.png', 'icon_json.png'); ^
         foreach ($ic in $icons) { ^
             $srcIc = Join-Path $baseDir $ic; ^
             if (Test-Path $srcIc) { Copy-Item -Path $srcIc -Destination $targetDir -Force; } ^
         } ^
         if ([int]$ver -le 2024) { ^
             $sourceParamCopy = Join-Path $baseDir \"bin\Release\net48\DuctSystemParamCopy.dll\"; ^
             if (-not (Test-Path $sourceParamCopy)) { $sourceParamCopy = Join-Path $baseDir \"..\DuctSystemParamCopy\bin\Release\net48\DuctSystemParamCopy.dll\"; } ^
         } else { ^
             $sourceParamCopy = Join-Path $baseDir \"bin\Release\net8.0-windows\DuctSystemParamCopy.dll\"; ^
             if (-not (Test-Path $sourceParamCopy)) { $sourceParamCopy = Join-Path $baseDir \"..\DuctSystemParamCopy\bin\Release\net8.0-windows\DuctSystemParamCopy.dll\"; } ^
         } ^
         if (-not (Test-Path $sourceParamCopy)) { $sourceParamCopy = Join-Path $baseDir \"DuctSystemParamCopy.dll\"; } ^
         if (-not (Test-Path $sourceParamCopy)) { $sourceParamCopy = Join-Path $baseDir \"..\DuctSystemParamCopy\DuctSystemParamCopy.dll\"; } ^
         if (Test-Path $sourceParamCopy) { Copy-Item -Path $sourceParamCopy -Destination $targetDir -Force; } ^
         Write-Host \"[УСПЕХ] Плагин успешно скопирован в Revit $ver\" -ForegroundColor Green; ^
         $copiedCount++; ^
     } ^
     if ($copiedCount -eq 0) { ^
         Write-Host \"[ПРЕДУПРЕЖДЕНИЕ] Не найдено установленных версий Revit (2021-2026).\" -ForegroundColor Yellow; ^
     }"

echo.
echo Установка завершена!
pause
