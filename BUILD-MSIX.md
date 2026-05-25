# Building and Installing the MSIX Package

## Quick Build

### Option 1: Quick build script (easiest)
```batch
.\build-msix-quick.bat
```

### Option 2: Using build.bat
```batch
.\build.bat msix
```

### Option 3: Direct PowerShell
```powershell
$pass = ConvertTo-SecureString "MeetingReminder!dev" -AsPlainText -Force
.\scripts\build-all.ps1 -Password $pass
```

## Output

Signed MSIX: **`dist\MeetingReminder.msix`**

## Installing the Certificate

Before installing the MSIX, trust the certificate:

### As Administrator:
```powershell
Import-Certificate -FilePath MeetingReminder.cer -CertStoreLocation Cert:\LocalMachine\Root
```

## Installing the Package

1. Double-click **`dist\MeetingReminder.msix`**
2. Click "Install"

## Certificate Details

- **Subject**: `CN=MeetingReminder Developer`
- **Files**: `MeetingReminder.pfx` (private, gitignored), `MeetingReminder.cer` (public)
- **Default password**: `MeetingReminder!dev` (development only)
