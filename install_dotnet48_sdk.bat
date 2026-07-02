@echo off
title Install .NET Framework 4.8.1 Developer Pack

echo ============================================
echo   Install .NET Framework 4.8.1 Developer Pack
echo   (Windows 11 需要 4.8.1 版本)
echo ============================================
echo.

REM Check admin
net session >nul 2>nul
if %errorlevel% neq 0 (
    echo ERROR: Please run as Administrator.
    echo Right-click this file - "Run as administrator"
    pause
    exit /b 1
)

echo Downloading .NET Framework 4.8.1 Developer Pack...
echo.
set INSTALLER=%TEMP%\ndp481-devpack.exe
powershell -Command "(New-Object System.Net.WebClient).DownloadFile('https://go.microsoft.com/fwlink/?linkid=2203306', '%INSTALLER%')"

if not exist "%INSTALLER%" (
    echo Download failed. Trying alternative link...
    powershell -Command "(New-Object System.Net.WebClient).DownloadFile('https://download.visualstudio.microsoft.com/download/pr/04820ddb-3b38-4a0b-abaf-191a2c3ada35/4a1ba2e09b3bdd37cc64d8c1330c9c0b/ndp481-devpack-enu.exe', '%INSTALLER%')"
)

if not exist "%INSTALLER%" (
    echo Download failed.
    echo Please download manually:
    echo 1. Open: https://dotnet.microsoft.com/en-us/download/dotnet-framework/net481
    echo 2. Find "Developer Pack" section
    echo 3. Download and install
    pause
    exit /b 1
)

echo.
echo Installing .NET Framework 4.8.1 Developer Pack...
echo.

"%INSTALLER%" /quiet /norestart

set RESULT=%errorlevel%

echo.
if %RESULT% equ 0 (
    echo SUCCESS! .NET Framework 4.8.1 SDK installed.
) else if %RESULT% equ 3010 (
    echo SUCCESS! .NET Framework 4.8.1 SDK installed. Reboot recommended.
) else (
    echo Installation failed (code: %RESULT%).
    echo.
    echo Please install manually:
    echo 1. Open: https://dotnet.microsoft.com/en-us/download/dotnet-framework/net481
    echo 2. Find and download "Developer Pack"
    echo 3. Run the installer
)
echo.
pause
