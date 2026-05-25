<#
.SYNOPSIS
    One-shot: cert (if missing) -> assets -> tests -> publish -> pack -> sign.

.PARAMETER Password
    Password protecting .pfx. Default: "MeetingReminder!dev".

.PARAMETER Force
    Force re-creation of cert and assets.
#>

[CmdletBinding()]
param(
    [SecureString]$Password,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Module -Name Microsoft.PowerShell.Security)) {
    Import-Module Microsoft.PowerShell.Security -ErrorAction Stop
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

if (-not $Password) {
    $defaultPlain = 'MeetingReminder!dev'
    $Password = ConvertTo-SecureString $defaultPlain -AsPlainText -Force
    Write-Host "Using default dev password for MeetingReminder.pfx." -ForegroundColor DarkGray
}

# ----- 1. cert -----

$pfx = Join-Path $repoRoot 'MeetingReminder.pfx'

if ($Force -or -not (Test-Path $pfx)) {
    Write-Host ""
    Write-Host "=== Step 1/4: certificate ===" -ForegroundColor Magenta
    & (Join-Path $PSScriptRoot 'create-certificate.ps1') -Password $Password
} else {
    Write-Host "Step 1/4: certificate already present (skipping)" -ForegroundColor DarkGray
}

# ----- 2. assets -----

$assetsDir = Join-Path $repoRoot 'MeetingReminder.App\Assets'
$marker = Join-Path $assetsDir 'Square150x150Logo.png'

if ($Force -or -not (Test-Path $marker)) {
    Write-Host ""
    Write-Host "=== Step 2/4: visual assets ===" -ForegroundColor Magenta
    & (Join-Path $PSScriptRoot 'generate-assets.ps1')
} else {
    Write-Host "Step 2/4: assets already present (skipping)" -ForegroundColor DarkGray
}

# ----- 3. tests -----

Write-Host ""
Write-Host "=== Step 3/4: tests ===" -ForegroundColor Magenta
& dotnet test (Join-Path $repoRoot 'MeetingReminder.Tests\MeetingReminder.Tests.csproj') --nologo -c Release
if ($LASTEXITCODE -ne 0) { throw "Tests failed. Aborting MSIX build." }

# ----- 4. msix -----

Write-Host ""
Write-Host "=== Step 4/4: MSIX ===" -ForegroundColor Magenta
& (Join-Path $PSScriptRoot 'build-msix.ps1') -Password $Password

Write-Host ""
Write-Host "All done." -ForegroundColor Green
Write-Host "Signed package: $(Join-Path $repoRoot 'dist\MeetingReminder.msix')"
