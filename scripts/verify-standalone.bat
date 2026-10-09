@echo off
setlocal
powershell -NoProfile -STA -ExecutionPolicy Bypass -File "%~dp0setup.ps1" -Standalone -Action verify %*
exit /b %errorlevel%
