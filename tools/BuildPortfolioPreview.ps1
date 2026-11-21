param(
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [string[]]$Versions = @('3.0.0','3.0.1'),
    [switch]$SkipManager,
    [switch]$PublicRelease
)
$ErrorActionPreference = 'Stop'
if ($PublicRelease -and $Versions.Count -ne 1) {
    throw 'Specify exactly one version with -Versions for a public release.'
}
$project = Split-Path -Parent $PSScriptRoot
$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $output -Force | Out-Null

function Invoke-Dotnet([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet exited with code $LASTEXITCODE" }
}

Push-Location $project
try {
    if (!$SkipManager) {
        $managerVersion = if ($PublicRelease) { $Versions[0] } else { '3.0.0' }
        $managerInfo = if ($PublicRelease) { $managerVersion } else { "$managerVersion-preview" }
        $publishArgs = @('publish', 'BimboClubManager/BimboClubManager.csproj', '-c', 'Release',
            '-r', 'win-x64', '--self-contained', $PublicRelease.IsPresent.ToString().ToLowerInvariant(),
            '-p:PublishSingleFile=true', "-p:Version=$managerVersion", "-p:AssemblyVersion=$managerVersion.0",
            "-p:FileVersion=$managerVersion.0", "-p:InformationalVersion=$managerInfo",
            '-o', (Join-Path $output 'Manager'), '--nologo', '-v:q')
        if ($PublicRelease) {
            $publishArgs += @('-p:EnableCompressionInSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true')
        }
        Invoke-Dotnet $publishArgs
    }
    foreach ($version in $Versions) {
        $productVersion = if ($PublicRelease) { $version } else { "$version-preview" }
        Invoke-Dotnet @('build', 'SystemTravers/BimboClub.csproj', '-c', 'Release', '-p:Platform=x64',
            '-p:BimboClubLive=true', '-p:CopyLocalLockFileAssemblies=true', "-p:Version=$version",
            "-p:AssemblyVersion=$version.0", "-p:FileVersion=$version.0", "-p:InformationalVersion=$productVersion", '--nologo', '-v:q')
        Invoke-Dotnet @('build', 'DuctSystemParamCopy/DuctSystemParamCopy.csproj', '-c', 'Release',
            '-p:Platform=x64', '--nologo', '-v:q')
        $release = Join-Path $output $version
        New-Item -ItemType Directory -Path $release -Force | Out-Null
        $packages = [ordered]@{}
        foreach ($name in @('net48','net8')) {
            $framework = if ($name -eq 'net48') { 'net48' } else { 'net8.0-windows' }
            $stage = Join-Path $release "$name-content"
            New-Item -ItemType Directory -Path $stage -Force | Out-Null
            Get-ChildItem "SystemTravers/bin/x64/Release/$framework" -Filter '*.dll' |
                Where-Object { $_.Name -notmatch '^(BimboClub\.Tools\.V|RevitAPI(UI)?\.dll$)' } |
                Copy-Item -Destination $stage -Force
            Copy-Item "DuctSystemParamCopy/bin/x64/Release/$framework/DuctSystemParamCopy.dll" -Destination $stage -Force
            Copy-Item 'SystemTravers/BimboClub.addin','SystemTravers/icon32.png','SystemTravers/icon_*.png' -Destination $stage -Force
            [IO.File]::WriteAllText((Join-Path $stage 'version.txt'), $version)
            $archive = "bimboclub_$name.zip"
            Compress-Archive -Path (Join-Path $stage '*') -DestinationPath (Join-Path $release $archive) -Force
            $packages[$name] = $archive
        }
        $manifest = [ordered]@{
            latestVersion = $version
            changelog = @(if ($version -eq '3.0.2') {
                @('Единая команда копирования фильтров видов и шаблонов с переносом состояния «Включён».',
                  'Новые пиктограммы на ленте Revit.')
            } else {
                @('Тестовая версия единого интерфейса и обновления без закрытия Revit')
            })
            packages = $packages
        }
        $manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $release 'update_manifest.json') -Encoding utf8NoBOM
    }
} finally { Pop-Location }
