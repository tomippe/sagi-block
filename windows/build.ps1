#Requires -Version 5.1
<#
.SYNOPSIS
    SagiBlock Windows Build Script — EXE (portable) + MSIX (Microsoft Store)

.EXAMPLE
    .\build.ps1              # EXE + MSIX
    .\build.ps1 -Exe         # EXE only
    .\build.ps1 -Clean       # Clean publish first
#>
param(
    [switch]$Exe,
    [switch]$Clean
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$rootDir = $PSScriptRoot
$projectDir = Join-Path $rootDir "SagiBlock"
$publishDir = Join-Path $rootDir "publish"
$csproj = Join-Path $projectDir "SagiBlock.csproj"
$assetsDir = Join-Path $projectDir "Assets"
$versionFile = Join-Path $rootDir "version.txt"
$rids = @("win-x64", "win-arm64")

function Find-MakeAppx {
    $sdkRoot = "C:\Program Files (x86)\Windows Kits\10\bin"
    if (-not (Test-Path $sdkRoot)) { return $null }

    $found = Get-ChildItem $sdkRoot -Recurse -Filter "makeappx.exe" -ErrorAction SilentlyContinue |
        Where-Object { $_.DirectoryName -like "*\x64" } |
        Sort-Object { $_.DirectoryName } -Descending |
        Select-Object -First 1

    if ($found) { return $found.FullName }
    return $null
}

Write-Host "SagiBlock Windows Build Script" -ForegroundColor White

$dotnetVersion = & dotnet --version 2>$null
if (-not $dotnetVersion) {
    Write-Error ".NET 8 SDK is not installed."
    exit 1
}
Write-Host ".NET SDK: $dotnetVersion" -ForegroundColor Gray

$version = (Get-Content $versionFile -Raw).Trim()
if (-not $version) {
    Write-Error "version.txt is empty."
    exit 1
}

$csprojContent = Get-Content $csproj -Raw
$csprojContent = $csprojContent -replace '<Version>[^<]*</Version>', "<Version>$version</Version>"
Set-Content -Path $csproj -Value $csprojContent -NoNewline

if ($Clean -and (Test-Path $publishDir)) {
    Remove-Item -Recurse -Force $publishDir
}
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

$procs = Get-Process -Name "SagiBlock" -ErrorAction SilentlyContinue
if ($procs) {
    Write-Host "Stopping running SagiBlock..." -ForegroundColor Yellow
    $procs | Stop-Process -Force
    Start-Sleep -Seconds 1
}

$storeLogo = Join-Path $assetsDir "StoreLogo.png"
if (-not (Test-Path $storeLogo)) {
    Write-Host "Generating Store assets..." -ForegroundColor Gray
    & (Join-Path $rootDir "generate-assets.ps1")
    if ($LASTEXITCODE -ne 0) {
        Write-Error "generate-assets.ps1 failed"
        exit 1
    }
}

Write-Host "Building EXE (single-file)..." -ForegroundColor Gray

foreach ($rid in $rids) {
    $arch = $rid -replace "win-", ""
    $outDir = Join-Path $publishDir "exe\$arch"

    & dotnet publish $csproj `
        -c Release `
        -r $rid `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:DebugType=none `
        -p:DebugSymbols=false `
        -o $outDir

    if ($LASTEXITCODE -ne 0) {
        Write-Error "EXE build failed for $rid"
        exit 1
    }

    $exePath = Join-Path $outDir "SagiBlock.exe"
    if (Test-Path $exePath) {
        $size = [math]::Round((Get-Item $exePath).Length / 1MB, 1)
        Write-Host "OK: $exePath ($size MB)" -ForegroundColor Green
    }
}

if ($Exe) {
    Write-Host ""
    Write-Host "Build complete (EXE only): v$version" -ForegroundColor Green
    exit 0
}

Write-Host ""
Write-Host "Building MSIX (Store package)..." -ForegroundColor Gray

Remove-Item -Recurse -Force (Join-Path $publishDir "msix"), (Join-Path $publishDir "msix-content") -ErrorAction SilentlyContinue

$makeappx = Find-MakeAppx
if (-not $makeappx) {
    Write-Error "makeappx.exe not found. Install Windows 10/11 SDK: https://developer.microsoft.com/windows/downloads/windows-sdk/"
    exit 1
}
Write-Host "  makeappx.exe: $makeappx" -ForegroundColor Gray

$msixOutputDir = Join-Path $publishDir "msix"
New-Item -ItemType Directory -Path $msixOutputDir -Force | Out-Null

$msixFiles = @()
$msixVersion = "$version.0"

foreach ($rid in $rids) {
    $arch = $rid -replace "win-", ""
    $contentDir = Join-Path $publishDir "msix-content\$arch"

    Write-Host "  Publishing $rid (multi-file for MSIX)..." -ForegroundColor Gray

    & dotnet publish $csproj `
        -c Release `
        -r $rid `
        --self-contained true `
        -p:DebugType=none `
        -p:DebugSymbols=false `
        -o $contentDir

    if ($LASTEXITCODE -ne 0) {
        Write-Error "MSIX publish failed for $rid"
        exit 1
    }

    $contentAssetsDir = Join-Path $contentDir "Assets"
    if (-not (Test-Path $contentAssetsDir)) {
        New-Item -ItemType Directory -Path $contentAssetsDir -Force | Out-Null
    }
    Copy-Item (Join-Path $assetsDir "*.png") $contentAssetsDir -Force

    $manifestSrc = Join-Path $projectDir "Package.appxmanifest"
    $manifestDst = Join-Path $contentDir "AppxManifest.xml"
    $manifestContent = Get-Content $manifestSrc -Raw -Encoding UTF8

    $manifestContent = $manifestContent -replace '(<Identity[^>]*?)Version="[^"]*"', "`$1Version=`"$msixVersion`""

    if ($manifestContent -match '<Identity[^>]*ProcessorArchitecture=') {
        $manifestContent = $manifestContent -replace '(<Identity[^>]*?)ProcessorArchitecture="[^"]*"', "`$1ProcessorArchitecture=`"$arch`""
    } else {
        $manifestContent = $manifestContent -replace '(<Identity[^>]*?)(/>|\s*>)', "`$1`n            ProcessorArchitecture=`"$arch`"`$2"
    }

    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($manifestDst, $manifestContent, $utf8NoBom)

    $makepri = $makeappx -replace "makeappx\.exe", "makepri.exe"
    if (Test-Path $makepri) {
        Write-Host "  Creating resources.pri for $rid ..." -ForegroundColor Gray
        $priConfigFile = Join-Path $contentDir "priconfig.xml"
        & $makepri createconfig /cf $priConfigFile /dq en-US /o 2>$null
        if (Test-Path $priConfigFile) {
            $priOut = Join-Path $contentDir "resources.pri"
            $mnFile = Join-Path $contentDir "AppxManifest.xml"
            & $makepri new /pr $contentDir /cf $priConfigFile /mn $mnFile /of $priOut /o 2>$null
            Remove-Item $priConfigFile -Force -ErrorAction SilentlyContinue
        }
    }

    $msixPath = Join-Path $msixOutputDir "SagiBlock_$arch.msix"
    Write-Host "  Packaging $msixPath ..." -ForegroundColor Gray

    & $makeappx pack /d $contentDir /p $msixPath /o

    if ($LASTEXITCODE -ne 0) {
        Write-Error "makeappx pack failed for $rid"
        exit 1
    }

    $msixSize = [math]::Round((Get-Item $msixPath).Length / 1MB, 1)
    Write-Host "OK: $msixPath ($msixSize MB)" -ForegroundColor Green
    $msixFiles += $msixPath
}

if ($msixFiles.Count -gt 0) {
    Write-Host ""
    Write-Host "Creating MSIX Bundle..." -ForegroundColor Gray

    $bundlePath = Join-Path $msixOutputDir "SagiBlock.msixbundle"
    $bundleContentDir = Join-Path $publishDir "msix-bundle-content"

    if (Test-Path $bundleContentDir) { Remove-Item -Recurse -Force $bundleContentDir }
    New-Item -ItemType Directory -Path $bundleContentDir -Force | Out-Null

    foreach ($msix in $msixFiles) { Copy-Item $msix $bundleContentDir }

    & $makeappx bundle /d $bundleContentDir /p $bundlePath /o

    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Bundle creation failed. Individual MSIX files are still available."
    } else {
        $bundleSize = [math]::Round((Get-Item $bundlePath).Length / 1MB, 1)
        Write-Host "OK: $bundlePath ($bundleSize MB)" -ForegroundColor Green
    }

    Remove-Item -Recurse -Force $bundleContentDir -ErrorAction SilentlyContinue
}

Remove-Item -Recurse -Force (Join-Path $publishDir "msix-content") -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "Build complete: v$version" -ForegroundColor Green
Write-Host "  EXE: publish\exe\x64, publish\exe\arm64" -ForegroundColor Gray
Write-Host "  MSIX: publish\msix\SagiBlock.msixbundle (Partner Center upload)" -ForegroundColor Gray
