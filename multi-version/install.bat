@echo off
setlocal
title JiePinPai Navisworks Plugin - Install

set TARGET_VERSION=%~1
if "%TARGET_VERSION%"=="" set TARGET_VERSION=2023

echo Installing for Navisworks Manage %TARGET_VERSION% ...
echo.

powershell -NoProfile -ExecutionPolicy Bypass ^
    -File "%~dp0scripts\install.ps1" ^
    -Version %TARGET_VERSION%

exit /b %errorlevel%
