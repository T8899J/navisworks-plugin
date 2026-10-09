@echo off
setlocal DisableDelayedExpansion
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0package_2023.ps1" %*
set "RESULT=%errorlevel%"
pause
exit /b %RESULT%
