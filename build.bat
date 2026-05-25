@echo off
REM Convenience wrapper for MeetingReminder.
REM
REM   build.bat             -> Debug build
REM   build.bat release     -> Release build
REM   build.bat publish     -> Release single-file publish
REM   build.bat test        -> Build + run xUnit tests
REM   build.bat clean       -> Wipe bin/obj
REM   build.bat msix        -> Full MSIX pipeline

setlocal enabledelayedexpansion
cd /d "%~dp0"

set TARGET=%1
if "%TARGET%"=="" set TARGET=debug

if /I "%TARGET%"=="debug" (
    dotnet build -c Debug
    goto :eof
)
if /I "%TARGET%"=="release" (
    dotnet build -c Release
    goto :eof
)
if /I "%TARGET%"=="test" (
    dotnet test -c Debug --nologo
    goto :eof
)
if /I "%TARGET%"=="publish" (
    dotnet publish MeetingReminder.App\MeetingReminder.App.csproj -c Release -r win-x64 --self-contained ^
        -p:PublishSingleFile=true ^
        -p:IncludeNativeLibrariesForSelfExtract=true
    echo.
    echo Single-file exe is at:
    echo   MeetingReminder.App\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\MeetingReminder.exe
    goto :eof
)
if /I "%TARGET%"=="clean" (
    dotnet clean
    for /d /r %%d in (bin obj) do if exist "%%d" rd /s /q "%%d"
    goto :eof
)
if /I "%TARGET%"=="msix" goto :msix

echo Unknown target: %TARGET%
echo Usage: build.bat [debug^|release^|publish^|test^|clean^|msix]
exit /b 1

:msix
set "PS="
where pwsh >nul 2>nul && set "PS=pwsh"
if not defined PS (
    where powershell >nul 2>nul && set "PS=powershell"
)
if not defined PS (
    if exist "%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" set "PS=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
)
if not defined PS (
    echo Could not find pwsh or powershell.
    exit /b 1
)
"%PS%" -ExecutionPolicy Bypass -NoProfile -File "%~dp0scripts\build-all.ps1"
goto :eof
