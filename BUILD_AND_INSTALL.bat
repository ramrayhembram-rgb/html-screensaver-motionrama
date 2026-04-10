@echo off
setlocal enabledelayedexpansion
title HTML Screensaver — Build ^& Install

echo ============================================
echo  HTML Screensaver — Build ^& Install
echo ============================================
echo.

:: ── Check for admin (needed to copy to System32) ─────────────
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [!] This script needs to run as Administrator to install the screensaver.
    echo     Right-click this file and choose "Run as administrator".
    pause
    exit /b 1
)

:: ── Locate this script's folder (the project lives here) ─────
set "SCRIPTDIR=%~dp0"
set "PROJDIR=%SCRIPTDIR%HtmlScreensaver"
set "OUTDIR=%PROJDIR%\bin\Release\net8.0-windows\win-x64\publish"
set "EXENAME=HtmlScreensaver.exe"
set "SCRNAME=HtmlScreensaver.scr"
set "SYSTEM32=C:\Windows\System32"

:: ── Check / install .NET SDK ──────────────────────────────────
echo [1/4] Checking for .NET 8 SDK...
dotnet --list-sdks 2>nul | findstr /C:"8." >nul
if %errorlevel% equ 0 (
    echo       .NET 8 SDK found.
) else (
    echo       .NET 8 SDK not found. Downloading installer...
    echo       (This is a one-time ~220 MB download)
    echo.
    set "DOTNET_INSTALLER=%TEMP%\dotnet-sdk-installer.exe"
    powershell -Command "& { [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; Invoke-WebRequest -Uri 'https://aka.ms/dotnet/8.0/dotnet-sdk-win-x64.exe' -OutFile '!DOTNET_INSTALLER!' -UseBasicParsing }"
    if not exist "!DOTNET_INSTALLER!" (
        echo [ERROR] Download failed. Please install .NET 8 SDK manually from:
        echo         https://dotnet.microsoft.com/download/dotnet/8.0
        pause
        exit /b 1
    )
    echo       Installing .NET 8 SDK silently...
    "!DOTNET_INSTALLER!" /install /quiet /norestart
    del "!DOTNET_INSTALLER!" >nul 2>&1
    :: Refresh PATH so dotnet is found in this session
    for /f "tokens=*" %%i in ('powershell -Command "[System.Environment]::GetEnvironmentVariable(\"PATH\",\"Machine\")"') do set "PATH=%%i;%PATH%"
    echo       .NET 8 SDK installed.
)
echo.

:: ── Restore NuGet packages ────────────────────────────────────
echo [2/4] Restoring packages...
dotnet restore "%PROJDIR%\HtmlScreensaver.csproj"
if %errorlevel% neq 0 (
    echo [ERROR] Package restore failed. Check your internet connection.
    pause
    exit /b 1
)
echo.

:: ── Publish self-contained single .exe ───────────────────────
echo [3/4] Building self-contained screensaver...
dotnet publish "%PROJDIR%\HtmlScreensaver.csproj" ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -o "%OUTDIR%"
if %errorlevel% neq 0 (
    echo [ERROR] Build failed. See output above.
    pause
    exit /b 1
)
echo.

:: ── Rename + copy to System32 ─────────────────────────────────
echo [4/4] Installing screensaver...
if not exist "%OUTDIR%\%EXENAME%" (
    echo [ERROR] Build output not found at: %OUTDIR%\%EXENAME%
    pause
    exit /b 1
)

copy /Y "%OUTDIR%\%EXENAME%" "%SYSTEM32%\%SCRNAME%" >nul
if %errorlevel% neq 0 (
    echo [ERROR] Could not copy to System32. Make sure you ran as Administrator.
    pause
    exit /b 1
)

echo.
echo ============================================
echo  SUCCESS! HtmlScreensaver.scr is installed.
echo ============================================
echo.
echo  Opening Screen Saver Settings now...
echo  1. It should already be selected in the list.
echo     If not, pick "HtmlScreensaver" from the dropdown.
echo  2. Click "Settings" to choose your HTML file and exit button mode.
echo  3. Click "Preview" to test it.
echo  4. Click OK when done.
echo.
pause

:: Open Screen Saver Settings
start "" rundll32.exe desk.cpl,ScreenSaverConfigure
