@echo off
title JiePinPai Navisworks Plugin - Install (2023)

echo ============================================
echo   JiePinPai Navisworks Plugin Installer
echo   Target: Navisworks Manage 2023
echo ============================================
echo.

REM -- Step 1: Check build output --
set SCRIPT_DIR=%~dp0
set DLL_NAME=傑出品NavisworksPlugin
set DLL_PATH=%SCRIPT_DIR%bin\Release\%DLL_NAME%.dll
set PLUGIN_FILE=%SCRIPT_DIR%\%DLL_NAME%.plugin

if not exist "%DLL_PATH%" (
    echo [ERROR] Build output not found:
    echo         %DLL_PATH%
    echo.
    echo         Run build_2023.bat first.
    goto :ERROR
)
echo [OK] Found plugin DLL: %DLL_PATH%

if not exist "%PLUGIN_FILE%" (
    echo [ERROR] Plugin manifest not found:
    echo         %PLUGIN_FILE%
    goto :ERROR
)
echo [OK] Found plugin manifest: %PLUGIN_FILE%

REM -- Step 2: Determine target directory (subfolder matching DLL name) --
REM Navisworks requires: Plugins\YourAssemblyName\YourAssemblyName.dll
set BASE_DIR=F:\Navisworks\Navisworks Manage 2023\Plugins
set TARGET_DIR=%BASE_DIR%\%DLL_NAME%

if not exist "%BASE_DIR%" (
    echo [ERROR] Navisworks Plugins directory not found:
    echo         %BASE_DIR%
    goto :ERROR
)

REM -- Create subfolder if needed --
if not exist "%TARGET_DIR%" (
    mkdir "%TARGET_DIR%"
    if errorlevel 1 (
        echo [WARN] Cannot create %TARGET_DIR%. Trying AppData path...
        goto :USE_APPDATA
    )
)

echo [INSTALL] Copying to %TARGET_DIR% ...

copy /Y "%DLL_PATH%" "%TARGET_DIR%\%DLL_NAME%.dll" >nul
if errorlevel 1 (
    echo [WARN] Cannot write to install directory. Trying AppData path...
    goto :USE_APPDATA
)
echo [OK] Installed DLL

copy /Y "%PLUGIN_FILE%" "%TARGET_DIR%\%DLL_NAME%.plugin" >nul
if errorlevel 1 (
    echo [ERROR] Manifest copy failed.
    goto :ERROR
)
echo [OK] Installed manifest
goto :SUCCESS

:USE_APPDATA
set TARGET_DIR=%APPDATA%\Autodesk\Navisworks Manage 2023\Plugins\%DLL_NAME%

if not exist "%TARGET_DIR%" (
    echo [INFO] Creating user plugin directory...
    mkdir "%TARGET_DIR%"
    if errorlevel 1 (
        echo [ERROR] Cannot create: %TARGET_DIR%
        goto :ERROR
    )
)

copy /Y "%DLL_PATH%" "%TARGET_DIR%\%DLL_NAME%.dll" >nul
if errorlevel 1 (
    echo [ERROR] DLL copy to AppData failed.
    goto :ERROR
)
echo [OK] Installed DLL

copy /Y "%PLUGIN_FILE%" "%TARGET_DIR%\%DLL_NAME%.plugin" >nul
if errorlevel 1 (
    echo [ERROR] Manifest copy failed.
    goto :ERROR
)
echo [OK] Installed manifest

:SUCCESS
echo.
echo ============================================
echo   INSTALL SUCCESSFUL!
echo ============================================
echo.
echo   Location: %TARGET_DIR%
echo.
echo   Usage:
echo   1. Start Navisworks Manage 2023
echo   2. Open a model (.nwd / .nwf / .nwc)
echo   3. Look in ribbon:
echo      - "加载项" (Add-Ins) tab for AddInPlugin button
echo      - OR "傑出品" tab for custom ribbon button
echo   4. Click "傑出品查找" button
echo   5. Select the XML file
echo.
echo   Debug mode:
echo   F:\Navisworks\Navisworks Manage 2023\Roamer.exe /log "%%TEMP%%\nw_plugin_log.txt"
echo ============================================
echo.
pause
exit /b 0

:ERROR
echo.
pause
exit /b 1
