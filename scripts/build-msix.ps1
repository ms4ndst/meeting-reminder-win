<#
.SYNOPSIS
    Build a signed MeetingReminder.msix package.
#>

[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$Pfx = (Join-Path $PSScriptRoot '..\MeetingReminder.pfx'),
    [SecureString]$Password,
    [string]$OutputMsix = (Join-Path $PSScriptRoot '..\dist\MeetingReminder.msix')
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Module -Name Microsoft.PowerShell.Security)) {
    Import-Module Microsoft.PowerShell.Security -ErrorAction Stop
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Find-WindowsKit-Tool {
    param([string]$exe)
    $candidates = @(
        "$env:ProgramFiles (x86)\Windows Kits\10\bin",
        "$env:ProgramFiles\Windows Kits\10\bin",
        "$env:ProgramFiles (x86)\Windows Kits\11\bin",
        "$env:ProgramFiles\Windows Kits\11\bin"
    )
    foreach ($root in $candidates) {
        if (-not (Test-Path $root)) { continue }
        $found = Get-ChildItem -Path $root -Recurse -Filter $exe -ErrorAction SilentlyContinue |
                 Where-Object { $_.FullName -match 'x64' } |
                 Sort-Object FullName -Descending |
                 Select-Object -First 1
        if ($found) { return $found.FullName }
    }
    $cmd = Get-Command $exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    return $null
}

Write-Host "Locating tools..." -ForegroundColor Cyan

$makeAppx = Find-WindowsKit-Tool 'makeappx.exe'
$signTool = Find-WindowsKit-Tool 'signtool.exe'
$dotnet   = (Get-Command dotnet -ErrorAction SilentlyContinue)?.Source

if (-not $dotnet)   { throw "dotnet not found on PATH. Install the .NET 8 SDK." }
if (-not $makeAppx) { throw "makeappx.exe not found. Install the Windows 10/11 SDK." }
if (-not $signTool) { throw "signtool.exe not found. Install the Windows 10/11 SDK." }

Write-Host "  dotnet:    $dotnet"
Write-Host "  makeappx:  $makeAppx"
Write-Host "  signtool:  $signTool"
Write-Host ""

if (-not (Test-Path $Pfx)) {
    throw "Signing certificate not found at $Pfx`nRun scripts\create-certificate.ps1 first."
}

if (-not $Password) {
    $Password = Read-Host -AsSecureString "Password for MeetingReminder.pfx"
}

# ----- assets -----------------------------------------------------------------

$assetsDir = Join-Path $repoRoot 'MeetingReminder.App\Assets'
$assetsMissing = @(
    'StoreLogo.png', 'Square44x44Logo.png', 'Square150x150Logo.png',
    'Wide310x150Logo.png', 'SplashScreen.png'
) | Where-Object { -not (Test-Path (Join-Path $assetsDir $_)) }

if ($assetsMissing.Count -gt 0) {
    Write-Host "Generating missing assets..." -ForegroundColor Cyan
    & (Join-Path $PSScriptRoot 'generate-assets.ps1')
}

# ----- publish ----------------------------------------------------------------

$publishDir = Join-Path $repoRoot "MeetingReminder.App\bin\$Configuration\net8.0-windows10.0.19041.0\win-x64\publish"
Write-Host "Publishing MeetingReminder.App ($Configuration / win-x64)..." -ForegroundColor Cyan

& $dotnet publish (Join-Path $repoRoot 'MeetingReminder.App\MeetingReminder.App.csproj') `
    -c $Configuration -r win-x64 --self-contained true `
    -p:PublishSingleFile=false -p:PublishReadyToRun=false `
    -p:GenerateAppxPackageOnBuild=false -p:EnableMsixTooling=false --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)" }
if (-not (Test-Path $publishDir)) { throw "Publish folder not found at $publishDir" }

# ----- stage ------------------------------------------------------------------

$stage = Join-Path $env:TEMP "MeetingReminder-msix-stage-$(Get-Random)"
Write-Host "Staging at $stage" -ForegroundColor Cyan
New-Item -ItemType Directory -Path $stage | Out-Null

Copy-Item -Path (Join-Path $publishDir '*') -Destination $stage -Recurse -Force
Copy-Item -Path (Join-Path $repoRoot 'MeetingReminder.App\Package.appxmanifest') -Destination (Join-Path $stage 'AppxManifest.xml')

$stageAssets = Join-Path $stage 'Assets'
if (-not (Test-Path $stageAssets)) { New-Item -ItemType Directory -Path $stageAssets | Out-Null }
Copy-Item -Path (Join-Path $assetsDir '*.png') -Destination $stageAssets -Force

Get-ChildItem -Path $stage -Recurse -Include '*.pdb' | Remove-Item -Force -ErrorAction SilentlyContinue

# ----- makeappx ---------------------------------------------------------------

$outDir = Split-Path $OutputMsix -Parent
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }
if (Test-Path $OutputMsix) { Remove-Item $OutputMsix -Force }

Write-Host "Running MakeAppx -> $OutputMsix" -ForegroundColor Cyan
& $makeAppx pack /d $stage /p $OutputMsix /o /v
if ($LASTEXITCODE -ne 0) { throw "MakeAppx failed (exit $LASTEXITCODE)" }

# ----- sign -------------------------------------------------------------------

$bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($Password)
try { $plain = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($bstr) }
finally { [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }

Write-Host "Signing $OutputMsix" -ForegroundColor Cyan
& $signTool sign /fd SHA256 /a /f $Pfx /p $plain $OutputMsix
$plain = $null
if ($LASTEXITCODE -ne 0) { throw "SignTool failed (exit $LASTEXITCODE)" }

# ----- cleanup ----------------------------------------------------------------

try {
    Start-Sleep -Milliseconds 500
    Remove-Item -Path $stage -Recurse -Force -ErrorAction Stop
} catch {
    Write-Host "Warning: Could not delete staging folder $stage" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Built signed MSIX:" -ForegroundColor Green
Write-Host "  $OutputMsix"
Write-Host ""
Write-Host "To install: Add-AppxPackage -Path $OutputMsix" -ForegroundColor Cyan
Write-Host "Users must first trust the cert: Import-Certificate -FilePath MeetingReminder.cer -CertStoreLocation Cert:\LocalMachine\Root" -ForegroundColor Cyan
