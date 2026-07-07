@echo off
setlocal enabledelayedexpansion
title JiePinPai Navisworks Plugin Builder (Multi-Version)

:: Parse version argument, default to 2023
set TARGET_VERSION=%~1
if "%TARGET_VERSION%"=="" set TARGET_VERSION=2023

echo ============================================
echo   JiePinPai Navisworks Plugin Builder
echo   Target: Navisworks Manage %TARGET_VERSION%
echo ============================================
echo.

:: ---- Step 1: Resolve Navisworks installation path ----
:: Priority: env var ^> standard path ^> F: drive ^> 2023 fallback
set NW_PATH=

:: 1a: Environment variable
if defined NAVISWORKS_%TARGET_VERSION%_PATH (
    call set "NW_PATH=%%NAVISWORKS_%TARGET_VERSION%_PATH%%"
    echo [ENV] NAVISWORKS_%TARGET_VERSION%_PATH = !NW_PATH!
)

:: 1b: Standard Autodesk install
if not defined NW_PATH (
    set "STD_PATH=C:\Program Files\Autodesk\Navisworks Manage %TARGET_VERSION%"
    if exist "!STD_PATH!\Autodesk.Navisworks.Api.dll" (
        set "NW_PATH=!STD_PATH!"
        echo [OK] Found: !NW_PATH!
    )
)

:: 1c: F: drive custom path
if not defined NW_PATH (
    set "F_PATH=F:\Navisworks\Navisworks Manage %TARGET_VERSION%"
    if exist "!F_PATH!\Autodesk.Navisworks.Api.dll" (
        set "NW_PATH=!F_PATH!"
        echo [OK] Found: !NW_PATH!
    )
)

:: 1d: Fallback to 2023 (only version installed locally)
if not defined NW_PATH (
    set "FALLBACK=F:\Navisworks\Navisworks Manage 2023"
    if exist "!FALLBACK!\Autodesk.Navisworks.Api.dll" (
        set "NW_PATH=!FALLBACK!"
        echo [WARN] Navisworks %TARGET_VERSION% not found locally.
        echo        Compiling against 2023 API -- compatible DLL for all versions.
        echo        Path: !NW_PATH!
    )
)

if not defined NW_PATH (
    echo [ERROR] Cannot find any Navisworks API DLL.
    echo        Set NAVISWORKS_%TARGET_VERSION%_PATH or install Navisworks.
    goto :ERROR
)

:: ---- Step 2: Find dotnet ----
set DOTNET=
for %%d in ("C:\Users\BOY\AppData\Local\Microsoft\dotnet\dotnet.exe" "%ProgramFiles%\dotnet\dotnet.exe") do (
    if exist %%d set "DOTNET=%%~d"
)
if not defined DOTNET (
    echo [ERROR] .NET SDK not found. Install from https://dotnet.microsoft.com
    goto :ERROR
)
echo [OK] dotnet found

:: ---- Step 3: Build ----
echo.
echo [BUILD] Building for Navisworks %TARGET_VERSION% ...
echo        API path: !NW_PATH!
cd /d "%~dp0"

"!DOTNET!" build NavisworksPlugin.Multi.csproj -c Release -p:NavisworksPath="!NW_PATH!" -p:NavisworksVersion=%TARGET_VERSION%

if errorlevel 1 (
    echo.
    echo [ERROR] Build failed.
    goto :ERROR
)

:SUCCESS
echo.
echo ============================================
echo   BUILD SUCCESSFUL ^(target: %TARGET_VERSION%^)
echo ============================================
echo   Output: bin\Release\傑出品NavisworksPlugin.dll
echo   Manifest: manifests\傑出品NavisworksPlugin_%TARGET_VERSION%.plugin
echo.
echo   Next: package.bat to create release folder
echo ============================================
echo.
pause
exit /b 0

:ERROR
echo.
echo ============================================
echo   BUILD FAILED.
echo ============================================
echo.
pause
exit /b 1
