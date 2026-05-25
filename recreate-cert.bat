@echo off
REM Recreate the certificate with the default dev password
cd /d "%~dp0"
echo Recreating certificate with default password...
powershell -ExecutionPolicy Bypass -Command "$pass = ConvertTo-SecureString 'MeetingReminder!dev' -AsPlainText -Force; & '.\scripts\create-certificate.ps1' -Password $pass -Subject 'CN=MeetingReminder Developer'"
echo.
echo Certificate recreated. Now run: .\build.bat msix
pause
