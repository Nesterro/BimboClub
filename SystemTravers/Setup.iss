[Setup]
AppName=BimboClub Tools for Revit
AppVersion=1.0.0
DefaultDirName={userappdata}\Autodesk\Revit\Addins
DefaultGroupName=BimboClub
OutputDir=.\Installer
OutputBaseFilename=BimboClubTools_Setup
Compression=lzma
SolidCompression=yes
PrivilegesRequired=lowest
; Установка идет в профиль текущего пользователя, поэтому права администратора не требуются

[Files]
; Revit 2021
Source: "bin\Release\net48\BimboClub.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2021"; Flags: ignoreversion
Source: "BimboClub.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2021"; Flags: ignoreversion
Source: "bin\Release\net48\icon32.png"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2021"; Flags: ignoreversion
Source: "bin\Release\net48\icon_*.png"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2021"; Flags: ignoreversion
Source: "..\DuctSystemParamCopy\DuctSystemParamCopy.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2021"; Flags: ignoreversion

; Revit 2022
Source: "bin\Release\net48\BimboClub.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2022"; Flags: ignoreversion
Source: "BimboClub.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2022"; Flags: ignoreversion
Source: "bin\Release\net48\icon32.png"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2022"; Flags: ignoreversion
Source: "bin\Release\net48\icon_*.png"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2022"; Flags: ignoreversion
Source: "..\DuctSystemParamCopy\DuctSystemParamCopy.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2022"; Flags: ignoreversion

; Revit 2023
Source: "bin\Release\net48\BimboClub.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2023"; Flags: ignoreversion
Source: "BimboClub.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2023"; Flags: ignoreversion
Source: "bin\Release\net48\icon32.png"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2023"; Flags: ignoreversion
Source: "bin\Release\net48\icon_*.png"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2023"; Flags: ignoreversion
Source: "..\DuctSystemParamCopy\DuctSystemParamCopy.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2023"; Flags: ignoreversion

; Revit 2024
Source: "bin\Release\net48\BimboClub.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2024"; Flags: ignoreversion
Source: "BimboClub.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2024"; Flags: ignoreversion
Source: "bin\Release\net48\icon32.png"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2024"; Flags: ignoreversion
Source: "bin\Release\net48\icon_*.png"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2024"; Flags: ignoreversion
Source: "..\DuctSystemParamCopy\DuctSystemParamCopy.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2024"; Flags: ignoreversion

; Revit 2025 (использует net8.0-windows)
Source: "bin\Release\net8.0-windows\BimboClub.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2025"; Flags: ignoreversion
Source: "BimboClub.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2025"; Flags: ignoreversion
Source: "bin\Release\net8.0-windows\icon32.png"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2025"; Flags: ignoreversion
Source: "bin\Release\net8.0-windows\icon_*.png"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2025"; Flags: ignoreversion
Source: "..\DuctSystemParamCopy\DuctSystemParamCopy.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2025"; Flags: ignoreversion

; Revit 2026 (использует net8.0-windows)
Source: "bin\Release\net8.0-windows\BimboClub.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2026"; Flags: ignoreversion
Source: "BimboClub.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2026"; Flags: ignoreversion
Source: "bin\Release\net8.0-windows\icon32.png"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2026"; Flags: ignoreversion
Source: "bin\Release\net8.0-windows\icon_*.png"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2026"; Flags: ignoreversion
Source: "..\DuctSystemParamCopy\DuctSystemParamCopy.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2026"; Flags: ignoreversion

[Code]
// Здесь можно добавить логику проверки установленных версий Revit, если нужно.
// Текущий скрипт просто копирует файлы в папки всех указанных версий. Если папка версии не существует, она будет создана.
