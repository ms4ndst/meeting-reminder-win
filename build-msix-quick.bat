@echo off
REM Quick MSIX build script
cd /d "%~dp0"
echo.
echo Building MeetingReminder MSIX package...
echo.
powershell -ExecutionPolicy Bypass -Command "Import-Module Microsoft.PowerShell.Security -ErrorAction SilentlyContinue; & '.\scripts\build-all.ps1'"
echo.
if exist "dist\MeetingReminder.msix" (
    echo SUCCESS! MSIX package created at: dist\MeetingReminder.msix
    echo.
    echo To install the certificate on this machine run:
    echo   powershell -Command "Import-Certificate -FilePath MeetingReminder.cer -CertStoreLocation Cert:\LocalMachine\Root"
    echo   ^(requires admin^)
    echo.
) else (
    echo Build failed. Check the output above for errors.
)
pause
