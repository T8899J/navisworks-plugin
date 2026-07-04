@echo off
setlocal
title JiePinPai Navisworks Plugin - Install (2023)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\install_2023.ps1"
exit /b %errorlevel%
