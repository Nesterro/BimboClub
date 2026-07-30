@echo off
chcp 65001 >nul
setlocal EnableDelayedExpansion

cd /d "%~dp0"

echo ============================================
echo  Сборка и установка плагина DuctWallOpenings
echo ============================================

set "TARGET_DIR=%APPDATA%\Autodesk\Revit\Addins\2024\DuctWallOpenings"
set "ADDIN_DIR=%APPDATA%\Autodesk\Revit\Addins\2024"
set "DLL_FULL_PATH=%TARGET_DIR%\DuctWallOpenings.dll"

REM === csproj ===
if exist "DuctWallOpenings.csproj" del /F /Q "DuctWallOpenings.csproj"
(
echo ^<?xml version="1.0" encoding="utf-8"?^>
echo ^<Project ToolsVersion="15.0" DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003"^>
echo   ^<Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props" Condition="Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')" /^>
echo   ^<PropertyGroup^>
echo     ^<Configuration Condition=" '$(Configuration)' == '' "^>Release^</Configuration^>
echo     ^<Platform Condition=" '$(Platform)' == '' "^>AnyCPU^</Platform^>
echo     ^<ProjectGuid^>{A2B7C4D1-9E8F-4A12-B3C5-1D2E3F4A5B6C}^</ProjectGuid^>
echo     ^<OutputType^>Library^</OutputType^>
echo     ^<RootNamespace^>DuctWallOpenings^</RootNamespace^>
echo     ^<AssemblyName^>DuctWallOpenings^</AssemblyName^>
echo     ^<TargetFrameworkVersion^>v4.8^</TargetFrameworkVersion^>
echo     ^<FileAlignment^>512^</FileAlignment^>
echo     ^<Deterministic^>true^</Deterministic^>
echo     ^<LangVersion^>latest^</LangVersion^>
echo   ^</PropertyGroup^>
echo   ^<PropertyGroup Condition=" '$(Configuration)^|$(Platform)' == 'Release^|AnyCPU' "^>
echo     ^<DebugType^>pdbonly^</DebugType^>
echo     ^<Optimize^>true^</Optimize^>
echo     ^<OutputPath^>bin\Release\^</OutputPath^>
echo     ^<DefineConstants^>TRACE^</DefineConstants^>
echo     ^<ErrorReport^>prompt^</ErrorReport^>
echo     ^<WarningLevel^>4^</WarningLevel^>
echo     ^<PlatformTarget^>x64^</PlatformTarget^>
echo   ^</PropertyGroup^>
echo   ^<ItemGroup^>
echo     ^<Reference Include="RevitAPI"^>
echo       ^<HintPath^>C:\Program Files\Autodesk\Revit 2024\RevitAPI.dll^</HintPath^>
echo       ^<Private^>False^</Private^>
echo     ^</Reference^>
echo     ^<Reference Include="RevitAPIUI"^>
echo       ^<HintPath^>C:\Program Files\Autodesk\Revit 2024\RevitAPIUI.dll^</HintPath^>
echo       ^<Private^>False^</Private^>
echo     ^</Reference^>
echo     ^<Reference Include="System" /^>
echo     ^<Reference Include="System.Core" /^>
echo     ^<Reference Include="System.Xml" /^>
echo     ^<Reference Include="PresentationCore" /^>
echo     ^<Reference Include="PresentationFramework" /^>
echo     ^<Reference Include="WindowsBase" /^>
echo     ^<Reference Include="System.Xaml" /^>
echo   ^</ItemGroup^>
echo   ^<ItemGroup^>
echo     ^<Compile Include="DuctWallOpeningsCommand.cs" /^>
echo   ^</ItemGroup^>
echo   ^<Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" /^>
echo ^</Project^>
) > "DuctWallOpenings.csproj"

REM === Папки ===
if not exist "%TARGET_DIR%" mkdir "%TARGET_DIR%"
if not exist "%ADDIN_DIR%" mkdir "%ADDIN_DIR%"

REM === .addin ===
set "ADDIN_FILE=%ADDIN_DIR%\DuctWallOpenings.addin"
if exist "%ADDIN_FILE%" del /F /Q "%ADDIN_FILE%"
(
echo ^<?xml version="1.0" encoding="utf-8"?^>
echo ^<RevitAddIns^>
echo   ^<AddIn Type="Application"^>
echo     ^<Name^>DuctWallOpenings^</Name^>
echo     ^<Assembly^>!DLL_FULL_PATH!^</Assembly^>
echo     ^<AddInId^>D7A4E1F2-3B5C-4D6E-8F1A-2B3C4D5E6F70^</AddInId^>
echo     ^<FullClassName^>DuctWallOpenings.App^</FullClassName^>
echo     ^<VendorId^>CSTM^</VendorId^>
echo     ^<VendorDescription^>Custom MEP Tools^</VendorDescription^>
echo   ^</AddIn^>
echo   ^<AddIn Type="Command"^>
echo     ^<Name^>Отверстия в стенах^</Name^>
echo     ^<Assembly^>!DLL_FULL_PATH!^</Assembly^>
echo     ^<AddInId^>E8B5F2A3-4C6D-5E7F-9A2B-3C4D5E6F7A81^</AddInId^>
echo     ^<FullClassName^>DuctWallOpenings.DuctWallOpeningsCommand^</FullClassName^>
echo     ^<Text^>Отверстия в стенах^</Text^>
echo     ^<VendorId^>CSTM^</VendorId^>
echo     ^<VendorDescription^>Custom MEP Tools^</VendorDescription^>
echo   ^</AddIn^>
echo   ^<AddIn Type="Command"^>
echo     ^<Name^>Отверстия в полах^</Name^>
echo     ^<Assembly^>!DLL_FULL_PATH!^</Assembly^>
echo     ^<AddInId^>F9C6A3B4-5D7E-6F8A-AB3C-4D5E6F7A8B92^</AddInId^>
echo     ^<FullClassName^>DuctWallOpenings.DuctFloorOpeningsCommand^</FullClassName^>
echo     ^<Text^>Отверстия в полах^</Text^>
echo     ^<VendorId^>CSTM^</VendorId^>
echo     ^<VendorDescription^>Custom MEP Tools^</VendorDescription^>
echo   ^</AddIn^>
echo ^</RevitAddIns^>
) > "%ADDIN_FILE%"

REM === Генерация иконок через PowerShell ===
echo Генерация иконок...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "Add-Type -AssemblyName System.Drawing;" ^
  "function MakeIcon($path,$type){" ^
    "$bmp=New-Object System.Drawing.Bitmap(32,32);" ^
    "$g=[System.Drawing.Graphics]::FromImage($bmp);" ^
    "$g.SmoothingMode='AntiAlias';" ^
    "$g.Clear([System.Drawing.Color]::Transparent);" ^
    "$bgPen=New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(80,80,80),2);" ^
    "$ductBrush=New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(70,130,200));" ^
    "$holeBrush=New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(220,80,60));" ^
    "if($type-eq'wall'){" ^
      "$g.FillRectangle((New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(200,200,200))),12,2,8,28);" ^
      "$g.DrawRectangle($bgPen,12,2,8,28);" ^
      "$g.FillRectangle($ductBrush,2,12,28,8);" ^
      "$g.DrawRectangle($bgPen,2,12,28,8);" ^
      "$g.FillRectangle($holeBrush,13,13,6,6);" ^
    "}else{" ^
      "$g.FillRectangle((New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(200,200,200))),2,18,28,8);" ^
      "$g.DrawRectangle($bgPen,2,18,28,8);" ^
      "$g.FillRectangle($ductBrush,12,2,8,28);" ^
      "$g.DrawRectangle($bgPen,12,2,8,28);" ^
      "$g.FillRectangle($holeBrush,13,19,6,6);" ^
    "};" ^
    "$bmp.Save($path,[System.Drawing.Imaging.ImageFormat]::Png);" ^
    "$g.Dispose();$bmp.Dispose();" ^
  "};" ^
  "MakeIcon '%TARGET_DIR%\wall_icon.png' 'wall';" ^
  "MakeIcon '%TARGET_DIR%\floor_icon.png' 'floor';"

REM === MSBuild ===
set "MSBUILD="
for %%V in (2022 2019) do (
    for %%E in (Enterprise Professional Community BuildTools) do (
        if exist "C:\Program Files\Microsoft Visual Studio\%%V\%%E\MSBuild\Current\Bin\MSBuild.exe" (
            set "MSBUILD=C:\Program Files\Microsoft Visual Studio\%%V\%%E\MSBuild\Current\Bin\MSBuild.exe"
            goto :found_msbuild
        )
        if exist "C:\Program Files (x86)\Microsoft Visual Studio\%%V\%%E\MSBuild\Current\Bin\MSBuild.exe" (
            set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\%%V\%%E\MSBuild\Current\Bin\MSBuild.exe"
            goto :found_msbuild
        )
    )
)

echo [ОШИБКА] Не найден MSBuild.
pause
exit /b 1

:found_msbuild
echo Найден MSBuild: %MSBUILD%
echo.

echo --- Сборка проекта ---
"%MSBUILD%" "DuctWallOpenings.csproj" /p:Configuration=Release /p:Platform=AnyCPU /t:Rebuild /v:minimal /nologo

if errorlevel 1 (
    echo.
    echo [ОШИБКА] Сборка не удалась.
    pause
    exit /b 1
)

echo.
echo --- Установка плагина ---

if not exist "bin\Release\DuctWallOpenings.dll" (
    echo [ОШИБКА] DLL не найдена.
    pause
    exit /b 1
)

copy /Y "bin\Release\DuctWallOpenings.dll" "%DLL_FULL_PATH%" >nul
if errorlevel 1 (
    echo [ОШИБКА] Не удалось скопировать DLL. Закройте Revit и повторите.
    pause
    exit /b 1
)

echo.
echo ============================================
echo  Установка завершена!
echo  DLL:    %DLL_FULL_PATH%
echo  Addin:  %ADDIN_FILE%
echo  Иконки: %TARGET_DIR%\wall_icon.png
echo          %TARGET_DIR%\floor_icon.png
echo  Запустите Revit 2024.
echo ============================================
pause
endlocal