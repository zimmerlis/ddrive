$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$versionPath = Join-Path $root 'VERSION'
$outputPath = Join-Path $root 'artifacts\publish\portable-win-x64-profile'

$currentText = (Get-Content $versionPath -Raw).Trim()
$currentVersion = [System.Version]::Parse($currentText)
$nextVersion = [System.Version]::new($currentVersion.Major, $currentVersion.Minor, $currentVersion.Build + 1)
$version = $nextVersion.ToString(3)

$dotnetCommand = Get-Command dotnet.exe -ErrorAction SilentlyContinue
$dotnetPath = if ($dotnetCommand) { $dotnetCommand.Source } else { Join-Path $env:LOCALAPPDATA 'dotnet\dotnet.exe' }
if (-not (Test-Path $dotnetPath)) {
    throw 'Das .NET SDK wurde nicht gefunden.'
}

if (Test-Path $outputPath) {
    Remove-Item $outputPath -Recurse -Force
}

$publishArguments = @(
    'publish'
    (Join-Path $root 'src\dDrive.App\dDrive.App.csproj')
    '-c'
    'Release'
    '-p:PublishProfile=portable-win-x64'
    "-p:Version=$version"
    "-p:AssemblyVersion=$version"
    "-p:FileVersion=$version.0"
    "-p:InformationalVersion=$version"
    '-o'
    $outputPath
)

& $dotnetPath @publishArguments
if ($LASTEXITCODE -ne 0) {
    throw "Der Portable-Publish ist fehlgeschlagen (Exitcode $LASTEXITCODE)."
}

Set-Content -Path $versionPath -Value "$version`n" -NoNewline
Get-ChildItem $outputPath -Filter '*.pdb' -File -ErrorAction SilentlyContinue | Remove-Item -Force

Write-Host "Portable dDrive-Version $version wurde erstellt:" -ForegroundColor Green
Write-Host (Join-Path $outputPath 'dDrive.App.exe')
