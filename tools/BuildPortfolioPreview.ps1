param(
    [Parameter(Mandatory=$true)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $output -Force | Out-Null

function Invoke-Dotnet([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet exited with code $LASTEXITCODE" }
}

Push-Location $project
try {
    Invoke-Dotnet @('publish', 'BimboClubManager/BimboClubManager.csproj', '-c', 'Release',
        '-r', 'win-x64', '--self-contained', 'false', '-p:PublishSingleFile=true', '-p:Version=3.0.0',
        '-p:AssemblyVersion=3.0.0.0', '-p:FileVersion=3.0.0.0', '-p:InformationalVersion=3.0.0-preview',
        '-o', (Join-Path $output 'Manager'), '--nologo', '-v:q')
    foreach ($version in @('3.0.0','3.0.1')) {
        Invoke-Dotnet @('build', 'SystemTravers/BimboClub.csproj', '-c', 'Release', '-p:Platform=x64',
            '-p:BimboClubLive=true', '-p:CopyLocalLockFileAssemblies=true', "-p:Version=$version",
            "-p:AssemblyVersion=$version.0", "-p:FileVersion=$version.0", "-p:InformationalVersion=$version-preview", '--nologo', '-v:q')
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
            Copy-Item 'SystemTravers/icon32.png','SystemTravers/icon_*.png' -Destination $stage -Force
            [IO.File]::WriteAllText((Join-Path $stage 'version.txt'), $version)
            $archive = "bimboclub_$name.zip"
            Compress-Archive -Path (Join-Path $stage '*') -DestinationPath (Join-Path $release $archive) -Force
            $packages[$name] = $archive
        }
        $manifest = [ordered]@{
            latestVersion = $version
            changelog = @('Тестовая версия единого интерфейса и обновления без закрытия Revit')
            packages = $packages
        }
        $manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $release 'update_manifest.json') -Encoding utf8NoBOM
    }
} finally { Pop-Location }
