@echo off
setlocal
title 傑出品 Navisworks 2021 插件 — 打包

echo ============================================
echo   傑出品 Navisworks 2021 插件 — 打包工具
echo ============================================
echo.

cd /d "%~dp0"

:: ── Step 1: 编译 ──
echo [1/3] 编译插件 ...
call build.bat 2021
if errorlevel 1 (
    echo [ERROR] 编译失败，无法打包。
    pause
    exit /b 1
)

:: ── Step 2: 复制到发布目录 ──
echo.
echo [2/3] 复制文件到 release\ ...

set RELEASE_DIR=%~dp0release
set BIN_DIR=%~dp0bin\Release
set MANIFEST_DIR=%~dp0manifests

if not exist "%BIN_DIR%\傑出品NavisworksPlugin.dll" (
    echo [ERROR] DLL not found: %BIN_DIR%\傑出品NavisworksPlugin.dll
    pause
    exit /b 1
)

copy /y "%BIN_DIR%\傑出品NavisworksPlugin.dll" "%RELEASE_DIR%\" >nul
echo   [OK] 傑出品NavisworksPlugin.dll

copy /y "%MANIFEST_DIR%\傑出品NavisworksPlugin_2021.plugin" "%RELEASE_DIR%\" >nul
echo   [OK] 傑出品NavisworksPlugin_2021.plugin

copy /y "scripts\install-standalone.bat" "%RELEASE_DIR%\安装.bat" >nul
echo   [OK] 安装.bat

:: ── Step 3: 显示结果 ──
echo.
echo ============================================
echo   打包完成！
echo ============================================
echo.
echo   输出目录: %RELEASE_DIR%
echo.
echo   %RELEASE_DIR%\
echo   ├── 傑出品NavisworksPlugin.dll
echo   ├── 傑出品NavisworksPlugin_2021.plugin
echo   └── 安装.bat
echo.
echo   将此文件夹复制到 U 盘，在目标电脑上运行「安装.bat」即可。
echo ============================================
echo.
pause
