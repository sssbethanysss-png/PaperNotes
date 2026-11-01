param([switch]$SkipTests)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$previousAppData = $env:APPDATA
$previousCliHome = $env:DOTNET_CLI_HOME
$previousPackages = $env:NUGET_PACKAGES
try {
    $env:APPDATA = Join-Path $PSScriptRoot 'work\build-appdata'
    $env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot 'work\dotnet'
    $env:NUGET_PACKAGES = Join-Path $PSScriptRoot 'work\packages'

    $testProject = Join-Path $PSScriptRoot 'tests\Stickies.Tests.csproj'
    $appProject = Join-Path $PSScriptRoot 'src\Stickies\Stickies.csproj'
    $nugetConfig = Join-Path $PSScriptRoot 'NuGet.Config'
    $publishDir = Join-Path $PSScriptRoot 'outputs\PaperNotes'
    $zipPath = Join-Path $PSScriptRoot 'outputs\PaperNotes-Windows.zip'
    $legacyPublishDir = Join-Path $PSScriptRoot 'outputs\PaperNotes-v2'
    $shortcutBat = Join-Path $PSScriptRoot 'packaging\create-desktop-shortcut.bat'
    $docsGlob = Join-Path $PSScriptRoot 'outputs\*.md'
    $smokeScript = Join-Path $PSScriptRoot 'tests\PublishedSmoke.ps1'

    dotnet restore $testProject --configfile $nugetConfig
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed' }

    if (!$SkipTests) {
        # Integration tests write only under work\tests-* (project-local temp dirs).
        dotnet run --project $testProject --no-restore -c Release
        if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
    }

    # Rebuild official publish folder only — do not touch %LOCALAPPDATA%\PaperNotes.
    # Clear contents instead of removing the directory: the folder may be open as a
    # workspace / Explorer window and refuse deletion while still allowing file replace.
    function Clear-PublishDir([string]$dir) {
        if (!(Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null; return }
        Get-ChildItem -LiteralPath $dir -Force | Remove-Item -Recurse -Force
    }
    Clear-PublishDir $publishDir
    if (Test-Path $legacyPublishDir) { Clear-PublishDir $legacyPublishDir }

    dotnet publish $appProject --no-restore -c Release --self-contained false -o $publishDir
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }

    if (Test-Path $shortcutBat) {
        Copy-Item -Path $shortcutBat -Destination $publishDir -Force
    }
    Copy-Item -Path $docsGlob -Destination $publishDir -Force -ErrorAction SilentlyContinue

    if (!$SkipTests) {
        # Smoke uses --data-dir under work\published-* (temporary); never the real user notes.
        & $smokeScript
    }

    Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath -Force
    Write-Host "Published: $(Join-Path $publishDir 'PaperNotes.exe')"
    Write-Host "Zip:       $zipPath"
}
finally {
    $env:APPDATA = $previousAppData
    $env:DOTNET_CLI_HOME = $previousCliHome
    $env:NUGET_PACKAGES = $previousPackages
}
