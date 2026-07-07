@echo off
setlocal enabledelayedexpansion
title 傑出品 Navisworks 2021 插件 — 一键安装

echo.
echo   ╔══════════════════════════════════════╗
echo   ║  傑出品 Navisworks 2021 查找插件  ║
echo   ║        一 键 安 装 程 序          ║
echo   ╚══════════════════════════════════════╝
echo.

:: ── 获取脚本所在目录（即 release 文件夹） ──
set "PKG_DIR=%~dp0"

:: ── 检查必要文件 ──
if not exist "%PKG_DIR%傑出品NavisworksPlugin.dll" (
    echo [错误] 找不到插件 DLL，请确保以下文件在同一目录：
    echo        %PKG_DIR%
    echo        傑出品NavisworksPlugin.dll
    echo        傑出品NavisworksPlugin_2021.plugin
    echo.
    pause
    exit /b 1
)

if not exist "%PKG_DIR%傑出品NavisworksPlugin_2021.plugin" (
    echo [错误] 找不到插件清单文件。
    pause
    exit /b 1
)

:: ═══════════════════════════════════════════
:: 查找 Navisworks Manage 2021 安装位置
:: ═══════════════════════════════════════════

set "NW_PATH="

:: 方法 1: 注册表
echo [1/4] 正在搜索 Navisworks 2021 安装位置 ...
for /f "skip=2 tokens=2*" %%a in ('reg query "HKLM\SOFTWARE\Autodesk\Navisworks Manage\18.0\Location" /v Path 2^>nul') do (
    set "NW_PATH=%%b"
)
if defined NW_PATH (
    echo        注册表: !NW_PATH!
    goto :found
)

:: 方法 2: 环境变量 NAVISWORKS_2021_PATH
if defined NAVISWORKS_2021_PATH (
    if exist "!NAVISWORKS_2021_PATH!\Autodesk.Navisworks.Api.dll" (
        set "NW_PATH=!NAVISWORKS_2021_PATH!"
        echo        环境变量: !NW_PATH!
        goto :found
    )
)

:: 方法 3: 标准安装路径
set "STD_PATH=C:\Program Files\Autodesk\Navisworks Manage 2021"
if exist "!STD_PATH!\Autodesk.Navisworks.Api.dll" (
    set "NW_PATH=!STD_PATH!"
    echo        标准路径: !NW_PATH!
    goto :found
)

:: 方法 4: 扫描常见盘符
for %%d in (D E F G H) do (
    set "CANDIDATE=%%d:\Navisworks\Navisworks Manage 2021"
    if exist "!CANDIDATE!\Autodesk.Navisworks.Api.dll" (
        set "NW_PATH=!CANDIDATE!"
        echo        扫描盘符: !NW_PATH!
        goto :found
    )
    set "CANDIDATE=%%d:\Program Files\Autodesk\Navisworks Manage 2021"
    if exist "!CANDIDATE!\Autodesk.Navisworks.Api.dll" (
        set "NW_PATH=!CANDIDATE!"
        echo        扫描盘符: !NW_PATH!
        goto :found
    )
)

:: 方法 5: 手动指定
echo.
echo   [注意] 自动检测失败，未找到 Navisworks Manage 2021。
echo.
echo   请手动输入 Navisworks 2021 的安装目录，例如：
echo     D:\Autodesk\Navisworks Manage 2021
echo     E:\Program Files\Autodesk\Navisworks Manage 2021
echo.
set /p NW_PATH="  安装路径: "

if not defined NW_PATH (
    echo   已取消安装。
    pause
    exit /b 1
)

:: 去掉尾部反斜杠
if "!NW_PATH:~-1!"=="\" set "NW_PATH=!NW_PATH:~0,-1!"

if not exist "!NW_PATH!\Autodesk.Navisworks.Api.dll" (
    echo.
    echo   [错误] 该路径下未找到 Navisworks API DLL，请确认后重试。
    pause
    exit /b 1
)

:found
echo.
echo [2/4] 验证: !NW_PATH!
echo        插件目录: !NW_PATH!\Plugins\傑出品NavisworksPlugin

:: ── 创建目标目录并复制 ──
set "TARGET_DIR=!NW_PATH!\Plugins\傑出品NavisworksPlugin"

echo [3/4] 正在安装 ...
if not exist "!TARGET_DIR!" mkdir "!TARGET_DIR!"

copy /y "%PKG_DIR%傑出品NavisworksPlugin.dll" "!TARGET_DIR!\" >nul 2>&1
if errorlevel 1 (
    echo.
    echo   [错误] 无法写入目标目录，请以管理员身份运行此安装程序。
    echo   右键「安装.bat」→「以管理员身份运行」
    echo.
    pause
    exit /b 1
)

copy /y "%PKG_DIR%傑出品NavisworksPlugin_2021.plugin" "!TARGET_DIR!\" >nul 2>&1

echo [4/4] 安装完成！

echo.
echo   ╔══════════════════════════════════════╗
echo   ║         安 装 成 功 ！             ║
echo   ╚══════════════════════════════════════╝
echo.
echo   已安装到: !TARGET_DIR!
echo.
echo   请重启 Navisworks Manage 2021，
echo   在 Add-Ins 选项卡中即可看到「傑出品查找」按钮。
echo.
echo   ※ 如要安装到其他版本，请联系开发者获取对应版本包。
echo.
pause
exit /b 0
