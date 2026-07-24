#Requires -Version 5.1
<#
.SYNOPSIS
    SagiBlock — Microsoft Store へ MSIX bundle を提出（StoreBroker）。

.EXAMPLE
    .\scripts\store-submit.ps1
    .\scripts\store-submit.ps1 -AppId 9PKH91ZX0W9J
#>
param(
    [string]$AppId = "",
    [string]$TenantId = "",
    [string]$ClientId = "",
    [string]$ClientSecret = "",
    [string]$BundlePath = (Join-Path $PSScriptRoot "..\build\msix\SagiBlock.msixbundle"),
    [string]$CertNotesPath = (Join-Path $PSScriptRoot "store-cert-notes-en.txt")
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot "_msstore-env.ps1")

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Load-MsStoreEnv -ProjectRoot $projectRoot

if (-not $AppId) {
    $AppId = $env:MS_STORE_PRODUCT_ID
    if (-not $AppId) { $AppId = $env:MS_STORE_APP_ID }
}
if (-not $TenantId) { $TenantId = $env:SB_TENANT_ID }
if (-not $ClientId) { $ClientId = $env:SB_CLIENT_ID }
if (-not $ClientSecret) { $ClientSecret = $env:SB_CLIENT_SECRET }

if (-not $AppId) {
    Write-Error "AppId required (-AppId or MS_STORE_PRODUCT_ID in .env)"
    exit 1
}
if (-not (Test-Path -LiteralPath $BundlePath)) {
    Write-Error "Bundle not found. Run .\build.ps1 first: $BundlePath"
    exit 1
}
if (-not $TenantId -or -not $ClientId -or -not $ClientSecret) {
    Write-Error "Set SB_* or MSSTORE_* in %USERPROFILE%\.msstore-env"
    exit 1
}

$localSb = Join-Path $env:USERPROFILE "StoreBroker\StoreBroker"
if (Test-Path (Join-Path $localSb "StoreBroker.psd1")) {
    Import-Module $localSb -Force
} else {
    $sb = Get-Module -ListAvailable -Name StoreBroker
    if (-not $sb) {
        Write-Host "Installing StoreBroker (CurrentUser)..." -ForegroundColor Gray
        Install-Module -Name StoreBroker -Scope CurrentUser -Force -AllowClobber
    }
    Import-Module -Name StoreBroker -Force
}

$cred = New-Object PSCredential $ClientId, (ConvertTo-SecureString $ClientSecret -AsPlainText -Force)
Set-StoreBrokerAuthentication -TenantId $TenantId -Credential $cred | Out-Null

Write-Host "Getting or creating submission draft..." -ForegroundColor Cyan
$sub = $null
try {
    $sub = Get-ApplicationSubmission -AppId $AppId -NoStatus
} catch {
    Write-Host "No pending submission yet ($($_.Exception.Message))" -ForegroundColor Gray
}
if (-not $sub) {
    $sub = New-ApplicationSubmission -AppId $AppId
}
if (-not $sub) {
    Write-Error "Could not get or create submission for AppId $AppId"
    exit 1
}
Write-Host "Using SubmissionId: $($sub.id)" -ForegroundColor Gray

$fileName = [System.IO.Path]::GetFileName($BundlePath)
$sub.applicationPackages = @(
    @{
        fileName   = $fileName
        fileStatus = "PendingUpload"
    }
)

if (Test-Path -LiteralPath $CertNotesPath) {
    $notes = Get-Content -LiteralPath $CertNotesPath -Raw -Encoding UTF8
    if ($notes.Trim()) {
        $sub.notesForCertification = $notes.Trim()
        Write-Host "Set notesForCertification from $(Split-Path -Leaf $CertNotesPath)" -ForegroundColor Gray
    }
}

Write-Host "Updating submission (ReplacePackages)..." -ForegroundColor Cyan
$maxAttempts = 4
for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
    try {
        Set-ApplicationSubmission -AppId $AppId -UpdatedSubmission $sub
        break
    } catch {
        if ($attempt -eq $maxAttempts) { throw }
        $wait = $attempt * 15
        Write-Host "Set-ApplicationSubmission failed (attempt $attempt/$maxAttempts). Retrying in ${wait}s..." -ForegroundColor Yellow
        Start-Sleep -Seconds $wait
    }
}

$zipPath = Join-Path $env:TEMP ("SagiBlock-submit-{0}.zip" -f ([Guid]::NewGuid().ToString('N').Substring(0, 8)))
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path $BundlePath -DestinationPath $zipPath -CompressionLevel Optimal

try {
    Write-Host "Uploading $fileName ..." -ForegroundColor Cyan
    for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
        try {
            Set-SubmissionPackage -PackagePath $zipPath -UploadUrl $sub.fileUploadUrl
            break
        } catch {
            if ($attempt -eq $maxAttempts) { throw }
            $wait = $attempt * 15
            Write-Host "Upload failed (attempt $attempt/$maxAttempts). Retrying in ${wait}s..." -ForegroundColor Yellow
            Start-Sleep -Seconds $wait
        }
    }
    Write-Host "Committing (start certification)..." -ForegroundColor Cyan
    for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
        try {
            Complete-ApplicationSubmission -AppId $AppId -SubmissionId $sub.id
            break
        } catch {
            if ($attempt -eq $maxAttempts) { throw }
            $wait = $attempt * 15
            Write-Host "Commit failed (attempt $attempt/$maxAttempts). Retrying in ${wait}s..." -ForegroundColor Yellow
            Start-Sleep -Seconds $wait
        }
    }
    Write-Host "Done. SubmissionId: $($sub.id)" -ForegroundColor Green
} finally {
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force -ErrorAction SilentlyContinue }
}
