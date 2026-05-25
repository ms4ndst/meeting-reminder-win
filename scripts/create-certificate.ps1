<#
.SYNOPSIS
    Creates a self-signed code-signing certificate for MSIX packaging.

.PARAMETER Subject
    The certificate subject. Must match the Publisher in Package.appxmanifest.

.PARAMETER Password
    Password used to encrypt the exported .pfx.

.PARAMETER OutputDir
    Where to write .pfx and .cer. Default: repo root.

.PARAMETER YearsValid
    Cert validity period in years. Default 3.
#>

[CmdletBinding()]
param(
    [string]$Subject = 'CN=MeetingReminder Developer',
    [SecureString]$Password,
    [string]$OutputDir = (Join-Path $PSScriptRoot '..'),
    [int]$YearsValid = 3
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Module -Name Microsoft.PowerShell.Security)) {
    Import-Module Microsoft.PowerShell.Security -ErrorAction Stop
}

function Resolve-Out([string]$rel) {
    $full = Join-Path -Path $OutputDir -ChildPath $rel
    return [System.IO.Path]::GetFullPath($full)
}

if (-not $Password) {
    $Password = Read-Host -AsSecureString "Password to encrypt MeetingReminder.pfx (do not share)"
}

Write-Host "Creating self-signed code-signing certificate..." -ForegroundColor Cyan
Write-Host "  Subject: $Subject"
Write-Host "  Valid:   $YearsValid year(s)"
Write-Host "  Output:  $OutputDir"
Write-Host ""

$cert = New-SelfSignedCertificate `
    -Type CodeSigningCert `
    -Subject $Subject `
    -KeyUsage DigitalSignature `
    -FriendlyName "MeetingReminder MSIX signing cert" `
    -CertStoreLocation 'Cert:\CurrentUser\My' `
    -TextExtension @( `
        '2.5.29.37={text}1.3.6.1.5.5.7.3.3', `
        '2.5.29.19={text}') `
    -NotAfter (Get-Date).AddYears($YearsValid)

Write-Host "Certificate created. Thumbprint: $($cert.Thumbprint)" -ForegroundColor Green
Write-Host ""

$pfxPath = Resolve-Out 'MeetingReminder.pfx'
$cerPath = Resolve-Out 'MeetingReminder.cer'

Export-PfxCertificate `
    -Cert $cert `
    -FilePath $pfxPath `
    -Password $Password `
    -ChainOption EndEntityCertOnly `
    -Force | Out-Null

Export-Certificate `
    -Cert $cert `
    -FilePath $cerPath `
    -Force | Out-Null

Write-Host "Wrote:"
Write-Host "  $pfxPath  (KEEP THIS — gitignored)" -ForegroundColor Yellow
Write-Host "  $cerPath  (public — distribute to users for trust install)" -ForegroundColor Yellow
Write-Host ""
Write-Host "Use this exact string for <Identity Publisher=... /> in Package.appxmanifest:" -ForegroundColor Cyan
Write-Host "  $($cert.Subject)" -ForegroundColor White
Write-Host ""
Write-Host "To trust the cert on a user's machine, run as admin:" -ForegroundColor Cyan
Write-Host '  Import-Certificate -FilePath MeetingReminder.cer -CertStoreLocation Cert:\LocalMachine\Root' -ForegroundColor White
