param (
    [Parameter(Mandatory=$true)]
    [string]$Version,
    
    [Parameter(Mandatory=$true)]
    [string[]]$Changelog,

    [switch]$NoManager
)

# 1. Compile BimboClub plugin
Write-Host "--- 1. Compiling solution in Release configuration ---" -ForegroundColor Cyan
dotnet build BimboClub.sln -c Release -p:Version=$Version
if ($LASTEXITCODE -ne 0) {
    Write-Error "Solution build failed!"
    exit 1
}

# 2. Package plugin into ZIP
Write-Host "--- 2. Packaging plugin and icons into ZIP archives ---" -ForegroundColor Cyan
$tempNet48 = "$env:TEMP\BimboClub_Net48_Build"
$tempNet8 = "$env:TEMP\BimboClub_Net8_Build"

if (Test-Path $tempNet48) { Remove-Item $tempNet48 -Recurse -Force }
if (Test-Path $tempNet8) { Remove-Item $tempNet8 -Recurse -Force }
$null = New-Item -ItemType Directory -Path $tempNet48 -Force
$null = New-Item -ItemType Directory -Path $tempNet8 -Force

# Copy net48 files
Copy-Item "SystemTravers\bin\x64\Release\net48\BimboClub.dll" -Destination $tempNet48 -Force
Copy-Item "DuctSystemParamCopy\bin\x64\Release\net48\DuctSystemParamCopy.dll" -Destination $tempNet48 -Force
Copy-Item "SystemTravers\BimboClub.addin" -Destination $tempNet48 -Force
Copy-Item "SystemTravers\icon32.png" -Destination $tempNet48 -Force
Copy-Item "SystemTravers\icon_*.png" -Destination $tempNet48 -Force

# Copy net8 files
Copy-Item "SystemTravers\bin\x64\Release\net8.0-windows\BimboClub.dll" -Destination $tempNet8 -Force
Copy-Item "DuctSystemParamCopy\bin\x64\Release\net8.0-windows\DuctSystemParamCopy.dll" -Destination $tempNet8 -Force
Copy-Item "SystemTravers\BimboClub.addin" -Destination $tempNet8 -Force
Copy-Item "SystemTravers\icon32.png" -Destination $tempNet8 -Force
Copy-Item "SystemTravers\icon_*.png" -Destination $tempNet8 -Force

# Create ZIP packages
$null = New-Item -ItemType Directory -Path "UpdateServerMock\packages" -Force
Compress-Archive -Path "$tempNet48\*" -DestinationPath "UpdateServerMock\packages\bimboclub_net48.zip" -Force
Compress-Archive -Path "$tempNet8\*" -DestinationPath "UpdateServerMock\packages\bimboclub_net8.zip" -Force

Remove-Item $tempNet48 -Recurse -Force
Remove-Item $tempNet8 -Recurse -Force

# 3. Publish standalone single-file compressed BimboClubManager.exe
if (-not $NoManager) {
    Write-Host "--- 3. Publishing standalone single-file compressed BimboClubManager.exe ---" -ForegroundColor Cyan
    dotnet publish BimboClubManager\BimboClubManager.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -p:Version=$Version
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Manager publish failed!"
        exit 1
    }

    # Copy single EXE file to release output
    $singleExe = "BimboClubManager\bin\Release\net8.0-windows\win-x64\publish\BimboClubManager.exe"
    Copy-Item $singleExe -Destination "BimboClubManager.exe" -Force

    # Copy to Yandex.Disk
    $yandexFolder = "D:\Yandex.Disk\Revit\Plugins"
    Stop-Process -Name "BimboClubManager" -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
    Copy-Item $singleExe -Destination "$yandexFolder\BimboClubManager.exe" -Force
} else {
    Write-Host "--- 3. Skipping BimboClubManager compile (-NoManager set) ---" -ForegroundColor Yellow
}

# 4. Update manifest and push to Git
Write-Host "--- 4. Updating manifest and pushing to repository ---" -ForegroundColor Cyan
$manifestPath = "updates/update_manifest.json"
$date = Get-Date -Format "yyyy-MM-dd"
$manifest = [ordered]@{
    latestVersion = $Version
    releaseDate = $date
    changelog = $Changelog
    packages = @{
        net48 = "https://github.com/Nesterro/BimboClub/releases/download/v$Version/bimboclub_net48.zip"
        net8 = "https://github.com/Nesterro/BimboClub/releases/download/v$Version/bimboclub_net8.zip"
    }
}
$json = ConvertTo-Json $manifest -Depth 4
[System.IO.File]::WriteAllText((Resolve-Path $manifestPath), $json, [System.Text.Encoding]::UTF8)

# Git push (BimboClubManager.exe is gitignored and will NOT be committed to git repo)
git add .
git commit -m "Release version v$Version"
git push origin main

# 5. Delete existing release tag if exists and recreate release on GitHub via GitHub CLI
Write-Host "--- 5. Creating Release and uploading assets to GitHub ---" -ForegroundColor Cyan
$ghPath = "C:\Program Files\GitHub CLI\gh.exe"
$notes = "BimboClub Tools version $Version released on $date`n`nChanges:`n" + (($Changelog | ForEach-Object { "- $_" }) -join "`n")

& $ghPath release delete "v$Version" -y --cleanup-tag 2>$null

if ($NoManager) {
    & $ghPath release create "v$Version" "UpdateServerMock\packages\bimboclub_net48.zip" "UpdateServerMock\packages\bimboclub_net8.zip" --title "BimboClub Tools v$Version" --notes $notes
} else {
    & $ghPath release create "v$Version" "BimboClubManager.exe" "UpdateServerMock\packages\bimboclub_net48.zip" "UpdateServerMock\packages\bimboclub_net8.zip" --title "BimboClub Tools v$Version" --notes $notes
}

Write-Host ""
Write-Host "==============================================" -ForegroundColor Green
Write-Host " Version v$Version successfully published!" -ForegroundColor Green
Write-Host "==============================================" -ForegroundColor Green
