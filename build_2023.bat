@echo off
setlocal enabledelayedexpansion
title JiePinPai Navisworks Plugin Builder (2023)

echo ============================================
echo   JiePinPai Navisworks Plugin Builder
echo   Target: Navisworks Manage 2023
echo ============================================
echo.

REM -- Step 1: Resolve Navisworks 2023 path --
if defined NAVISWORKS_2023_PATH (
    set "NW_PATH=%NAVISWORKS_2023_PATH%"
) else (
    set "NW_PATH=%ProgramFiles%\Autodesk\Navisworks Manage 2023"
)

if not exist "%NW_PATH%" (
    echo [ERROR] Navisworks Manage 2023 not found at: %NW_PATH%
    goto :ERROR
)
echo [OK] Navisworks found: %NW_PATH%

REM -- Step 2: Check API DLL --
if not exist "%NW_PATH%\Autodesk.Navisworks.Api.dll" (
    echo [ERROR] Autodesk.Navisworks.Api.dll not found
    goto :ERROR
)
echo [OK] Navisworks API DLL found

REM -- Step 3: Find MSBuild --
set MSBUILD=

REM Method 1: vswhere
set VSWHERE="%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if exist %VSWHERE% (
    for /f "usebackq tokens=*" %%i in (`%VSWHERE% -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe 2^>nul`) do (
        set MSBUILD=%%i
    )
)

REM Method 2: Standard path
if not defined MSBUILD (
    if exist "%ProgramFiles%\MSBuild\Current\Bin\MSBuild.exe" (
        set "MSBUILD=%ProgramFiles%\MSBuild\Current\Bin\MSBuild.exe"
    )
)

if not defined MSBUILD (
    echo [ERROR] MSBuild not found.
    echo         Please install VS 2022 Build Tools.
    goto :ERROR
)
echo [OK] MSBuild: !MSBUILD!

REM -- Step 4: Restore NuGet packages --
echo.
echo [RESTORE] Restoring NuGet packages ...

cd /d "%~dp0"
"!MSBUILD!" NavisworksPlugin.csproj /t:Restore /p:Configuration=Release /p:NavisworksInstallDir="!NW_PATH!" /v:m

if errorlevel 1 (
    echo.
    echo [WARN] NuGet restore had issues, attempting build anyway...
)

REM -- Step 5: Build --
echo.
echo [BUILD] Compiling ...

"!MSBUILD!" NavisworksPlugin.csproj /p:Configuration=Release /p:NavisworksInstallDir="!NW_PATH!" /t:Rebuild /v:m

if errorlevel 1 (
    echo.
    echo [ERROR] Build failed.
    goto :ERROR
)

:SUCCESS
echo.
echo ============================================
echo   BUILD SUCCESSFUL!
echo ============================================
echo   Output: %~dp0bin\Release\傑出品NavisworksPlugin.dll
echo.
echo   Next: run install_2023.bat
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
